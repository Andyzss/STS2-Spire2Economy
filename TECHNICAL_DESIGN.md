# Spire Economy v0.1 技术设计

状态：债务、贷款与商店还款主流程已实现；黑市待实现
调查日期：2026-09-01
目标游戏：Slay the Spire 2 Steam 公测分支 `v0.111.0`（Build ID `24724944`）

> 范围更新（2026-09-02）：多人营火遗物交易已取消，卡牌交易和玩家金币交易也不加入本项目。
> 本文的交易 API 调查仅保留为历史技术资料，不再代表实施计划。当前权威进度见
> [`PROJECT_STATUS.md`](PROJECT_STATUS.md)。

## 1. 基线与调查范围

项目以 [`sethmcleod/sts2-mod-template`](https://github.com/sethmcleod/sts2-mod-template)
的提交 `d99ea83936539370780625f4421f1c8afe7c4a57` 为基线。模板使用：

- C# / .NET 9
- Godot.NET.Sdk 4.5.1
- BaseLib 3.4.5
- Harmony 2（游戏附带 `0Harmony.dll`）

本机安装位于：

```text
C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2
```

版本依据为 `release_info.json`：提交 `41cef1ea`、版本 `v0.111.0`、主程序集哈希
`222455745`。Steam 清单显示公测分支、Build ID `24724944`。本次通过 `sts2.dll`、
`sts2.xml` 和 BaseLib 3.3.8 源码提交
`89dc60502831761029e55883ca574180124348a4` 完成最初 API 调查；项目依赖随后升级到 BaseLib
3.4.5，因此首次编译与运行验证必须同时承担版本升级检查。

注意：这些是社区和未承诺稳定的游戏内部 API；升级游戏或 BaseLib 后必须重新做签名检查。

### UI 库分工

BaseLib 继续负责现有卡牌、配置和存档集成。RitsuLib 0.5.18 已完成兼容性评估，但当前 v0.1 不增加
未使用的硬依赖。商店还款入口复制游戏原生 `NPopupYesNoButton`，金额选择使用原生
`NModalContainer` / `NGenericPopup`，从而保留官方贴图、字体、Shader、音效及手柄焦点行为。
未来若黑市需要通用窗口、节点挂载或复杂主题系统，再按实际使用范围引入 RitsuLib。债务规则与服务层
不引用任何 UI 库，避免将经济逻辑绑定到具体界面框架。

## 2. 已发现的 API 与钩子

### 2.1 商店

- `MerchantInventory.Player`：当前商店所属玩家。
- `MerchantInventory.CardEntries`、`RelicEntries`、`PotionEntries`：库存入口。
- `MerchantEntry.Cost`：最终购买价。
- `MerchantEntry.OnTryPurchaseWrapper(MerchantInventory, bool)`：统一购买入口，返回
  `Task<bool>`。
- `MerchantEntry.PurchaseCompleted` / `PurchaseFailed`：购买结果事件。
- `Hook.ModifyMerchantPrice(...)`：标准价格修改。
- `Hook.AfterItemPurchased(...)`：购买成功后的通知。
- `LordsParasol.PurchaseEverything(...)`：以 `ignoreCost=true` 逐项调用原版购买包装器，
  包含卡牌、遗物、药水和免费卡牌移除。
- `TheCourier.ShouldRefillMerchantEntry(...)`：成功购买后由原版包装器决定补货。
- `NMerchantCard/Relic/Potion/CardRemoval.OnTryPurchase(...)`：可证明玩家从商店 UI
  主动发起单次交易的最窄调用边界。
- `NMerchantInventory`：商店 Godot UI；`Open`、`Close`、`UpdateNavigation` 等方法存在。

结论：标准 API 能读价格和观察成功购买，但没有“余额不足时由外部支付差额”的公开钩子。
贷款需要在 `OnTryPurchaseWrapper` 的余额检查附近做最小 Harmony 补丁，不能先把借款加到钱包。

### 2.2 卡牌与诅咒

- `BaseLib.Abstracts.ConstructedCardModel` / `CustomCardModel`：自定义卡牌基类。
- `Pool(typeof(CurseCardPool))`：把 `DebtCurse` 放入诅咒池模型体系；其自然生成必须通过
  `CanBeGenerated...` 等覆盖显式关闭。
- `CardModel.IsPlayable`：受保护虚属性，可让 Debt 永远不可打出。
- `CardModel.AddExtraArgsToDescription(LocString)` 与动态变量：可把当前债务注入卡面文字。
- `Player.Deck` 与 `CardPile.Cards`：查找唯一 Debt 卡。
- `CardPile.AddInternal(...)` / `RemoveInternal(...)`、`RunState.RemoveCard(...)`：牌组变更。
- `Hook.BeforeCardRemoved(...)`：只有通知语义，没有取消返回值。
- BaseLib `SavedSpireField<CardModel, int>`：可把债务值写入该卡的 `SavedProperties`，并随卡
  存档、读档和网络序列化。

结论：债务值以唯一 `DebtCurse` 实例上的 `SavedSpireField<DebtCurse,int>` 为唯一事实来源。
`DebtManager` 是唯一写入口，卡面只读取并显示该值，不另存副本。正常移除保护需要 Harmony，
因为现有 `BeforeCardRemoved` 不能否决。

### 2.3 遗物

- `Player.Relics`：只读遗物列表。
- `Player.AddRelicInternal(RelicModel, int, bool)` / `RemoveRelicInternal(RelicModel, bool)`：
  实际增删入口。
- `RelicModel.Owner`、`Rarity`、`MerchantCost`、`ToSerializable()`。
- `RelicModel.AfterObtained()` / `AfterRemoved()` 与玩家的 `RelicObtained` / `RelicRemoved` 事件。

结论：遗物转移必须走游戏的增删入口并保留生命周期回调；不可直接修改底层列表。某些遗物
具有不可逆拾取效果、槽位修改、宠物或唯一状态，默认应拒绝，只有明确通过规则的遗物才可交易。

### 2.4 营火动作

- `Hook.ModifyRestSiteOptions(IRunState, Player, ICollection<RestSiteOption>)`：公开扩展点。
- `BaseLib.Abstracts.CustomRestSiteOption`：支持自定义图标。
- `RestSiteOption.IsEnabled`、`OnSelect(): Task<bool>`、本地/远端选择后 VFX。
- `Hook.ShouldDisableRemainingRestSiteOptions(...)`：决定选择后是否禁用其余动作。

历史结论：技术上可通过公开钩子加入 Trade 入口，但交易功能现已取消，本项目不会使用这些接口。

### 2.5 事件

- BaseLib `CustomEventModel`：自动注册自定义事件，并提供本地化、图片和选项辅助方法。
- `EventModel.IsAllowed(IRunState)`、`GenerateInitialOptions()`、`SetEventState(...)`、
  `SetEventFinished(...)`。
- `EventModel.Owner`、`Rng`、`IsShared`、`IsDeterministic`。
- `Hook.ModifyNextEvent(...)` 与 BaseLib 的自定义事件内容字典。

结论：黑市可作为非共享、可离开的自定义事件实现。库存必须只使用 `EventModel.Rng` 或运行 RNG，
不能使用 `System.Random`，否则多人和重放可能分歧。稀有度优先由 BaseLib 事件池权重/允许条件实现；
若当前 BaseLib 没有可配置权重接口，再考虑一个狭窄的事件抽取 Harmony 补丁。

### 2.6 多人同步

- `RunManager.Instance.NetService`：当前 `INetGameService`。
- `INetGameService.NetId`、`IsConnected`、`SendMessage<T>`、`RegisterMessageHandler<T>`。
- `INetHostGameService` / `INetClientGameService`：区分主机和客户端能力。
- `RunState.Players`、`GetPlayer(ulong)`、`Player.NetId`。
- BaseLib `ICustomMessage` / `CustomMessageWrapper`：可靠、自定义、可广播消息。
- BaseLib `ICustomTargetedMessage`：带 `RunLocation` 的消息，可经地点缓冲器处理。
- `PacketWriter` / `PacketReader`：自定义序列化。
- `RunLocationTargetedMessageBuffer`：避免跨房间的迟到消息污染状态。

历史结论：上述接口足以构建主机权威的自定义协议，但交易功能现已取消。目前只保留这些发现，
供未来验证债务状态的多人同步使用。

### 2.7 存档与读档

- `CardModel.ToSerializable()` / `FromSerializable(...)`。
- `RelicModel.ToSerializable()` / `FromSerializable(...)`。
- `Player.FromSerializable(...)`、`RunManager.InitializeSavedRun(...)`。
- BaseLib `SavedSpireField<CardModel, int>` 支持整数，适合 Debt 卡实例字段。
- `IRunState.ExtraFields` 和 `Player.ExtraFields` 存在，但不应为了债务修改游戏序列化类型。

结论：债务随 Debt 卡保存。交易会话是房间内瞬态状态，不写入存档；保存/断线/离开房间时应
取消未提交会话。黑市库存由事件 RNG 生成并在事件实例生命期保持；若未来允许事件中途存档，
需要再增加显式库存序列化。

## 3. 系统架构

```text
Normal Shop Adapter ─┐
                     ├─> LoanService ─> DebtManager ─> DebtCurse(saved amount)
Black Market Adapter ┘

BlackMarketEvent ─> BlackMarketMerchantInventoryFactory ─> MerchantInventory
        ├──────────> native merchant_room.tscn / NMerchantInventory presentation
        ├──────────> LoanService through the normal merchant purchase wrapper
        └──────────> RelicSaleRules / game relic lifecycle APIs (sale service; UI pending)
```

边界规则：

- `Debt` 不引用黑市或交易。
- `BlackMarket` 只依赖贷款接口，不读取/修改 Debt 卡内部字段。
- 平衡数值全部来自 `EconomyConfig`，不散落在玩法类中。
- UI 只发出命令并渲染状态；业务验证留在服务层，主机再次验证。

### 2.8 稀有度分类（已确认）

- `CardRarity` 实际值为：`None, Basic, Common, Uncommon, Rare, Ancient, Event, Token, Status, Curse, Quest`。
- 游戏没有 `Gold` 卡牌稀有度；v0.1 黑市卡池按已确认规则使用 `Rare`。
- `RelicRarity` 实际值为：`None, Starter, Common, Uncommon, Rare, Shop, Event, Ancient`。
- 最初 v0.1 规则只允许 `Rare`；当前设计已扩展为 1 件经过安全筛选的 `Ancient` 加 1 件
  `Rare`，仍明确排除 `Shop`。该扩展需要继续通过实机平衡验证。

### 3.1 DebtManager

职责：按玩家查找 Debt 卡、保证最多一张、读取/设置保存字段、债务归零时移除卡、债务增加时
创建卡。加载异常存档出现多张卡时保留最大非负债务值而不累加，并删除其余副本；零债务卡会
被删除。运行时变更使用 `CardPileCmd`，每个玩家用独立异步锁串行化。

### 3.2 LoanService

输入：玩家、`PurchaseContext` 和执行购买的回调。上下文保存来源、是否要求金币、
最终 `MerchantEntry.Cost` 与商品类型。算法必须满足：

1. `shortfall = max(0, price - player.Gold)`。
2. `shortfall == 0` 时走原购买路径。
3. 新债务超过 `EconomyConfig.MaxDebt` 时拒绝。
4. 借款不进入钱包；购买结算只消耗玩家现有金币，并把差额写入 Debt。
5. 以一次性 `MerchantEntry` 预约防止重复回调重复记债；原版返回失败时恢复购买前金币且不记债。
6. 当前原版回调先发商品、后由补丁持久化债务；若 Debt 卡添加异常，无法借助公开 API 原子撤销
   已发放遗物。当前选择抛出显式错误而不是静默赠送，运行测试前这是一个未闭合的 API 风险。
7. 只有 `PurchaseSource.PlayerInitiated` 可以建立预约；UI 预览只改变显示可用性，自动、强制、
   免费、未知与非商店来源全部拒绝。
8. 收费卡牌移除是项目所有者明确保留的融资例外；免费/自动移除仍拒绝。

### 3.3 DebtCurse

继承 `SpireEconomyCard`，使用 `CardType.Curse`、`CardRarity.Curse`、`TargetType.None`，覆盖
`IsPlayable=false`，关闭自然生成和升级。`SavedSpireField<DebtCurse,int>` 保存余额，卡面动态变量
只显示该值。`IsRemovable`、牌组移除命令与直接状态移除形成三层保护；只有 DebtManager 的显式
授权作用域可在清债或修复存档时移除。

### 3.4 已取消的交易设计

多人营火遗物交易已于 2026-09-02 取消，不再实现 `TradeService`、`TradeSession`、
`RelicTradeRules`、Trade 营火入口或相关网络消息。现有 `Trading` 源文件只是未注册、无行为的早期
占位骨架，可在后续整理时删除。黑市仍需要独立的 `RelicSaleRules`，但该规则只判断玩家能否把
遗物卖给黑市，不支持玩家之间转移。

### 3.5 BlackMarketEvent / BlackMarketInventory

事件非共享，每名玩家独立处理。事件使用 `EventLayoutType.Custom`，但场景直接复用原版
`merchant_room.tscn`；Harmony 适配层把事件库存接入原版 `NMerchantRoom` 与 `NMerchantInventory`。
不能使用 `fake_merchant.tscn` 承载卡牌，因为该场景的展示槽是遗物网格，会出现只有价格而没有
卡牌模型的问题。鼠标、键盘/手柄导航、商人手指、悬停说明、卡牌移除选择及购买反馈继续由
原版商店 UI 负责。库存为：

进入事件时只展示房间，不自动打开库存。玩家点击商人才打开商品页；关闭商品页后由原版
`InventoryClosed` 回调重新启用商人与离开按钮。领主阳伞只在本次事件第一次打开库存时结算，
避免配合补货反复免费取得商品。

- 2 张本职业 `CardRarity.Rare` 卡。
- 2 张无色 `CardRarity.Rare` 卡。
- 3 张其他可玩角色卡：`Common`、`Uncommon`、`Rare` 各一张。
- 2 件遗物；优先放入一件通过能力检查及 denylist 的 `Ancient` 遗物，其余以 `Rare` 补足。
- 3 瓶按原版规则生成的随机药水。
- 1 个卡牌移除服务，基础价格固定为 200。
- v0.1 不生成 mystery slot。

卡牌在原版基础价格上增加 25%，药水和普通遗物保留原版基础价格；之后再调用原版 `Hook.ModifyMerchantPrice`，因此会员卡与
补货折扣有效。`The Courier` 使用原版补货入口。`Lord's Parasol` 使用 `ignoreCost=true` 的原版购买包装器逐槽取得
商品，不创建债务。卖价为基础价乘 `RelicSalePricePercent / 100m`；出售业务服务保留，但商店式
界面的出售按钮仍待实现。

`Ancient` 遗物在原版中不是普通商店商品，部分模型的 `MerchantCost` 是不可购买的哨兵值。
黑市不得直接使用该数值；当前统一采用命名基础价 200，再应用原版折扣。

`Ancient` 遗物额外随机收取 5–15 点最大生命。金额随商品生成并保持稳定，以较小的红色原版数字显示在金币价格正下方；
购买失败不扣除，购买成功只结算一次，`ignoreCost=true`（领主阳伞）不结算该代价。普通 Rare 与
非 Ancient 遗物不损失最大生命；Event 遗物不会进入当前特殊商品池。

## 4. 预计需要的 Harmony 补丁

### 本阶段已加入

1. `MerchantEntry.get_EnoughGold`：只在匹配的 UI preview/player transaction scope 中，对标准
   商店卡牌、遗物、药水和卡牌移除服务提供融资可用性。
2. `MerchantEntry.OnTryPurchaseWrapper`：建立一次性融资预约，成功后记入精确短缺额，失败不改债务。
3. `MerchantCardRemovalEntry.OnTryPurchaseWrapper`：覆盖卡牌移除专用的异步购买包装器；取消选牌
   时释放预约，成功移除后才提交债务。
4. `CardModel.get_IsRemovable` / `get_IsTransformable`：Debt Curse 对普通选择器不可移除或变形。
5. `CardPileCmd.RemoveFromDeck` 两个重载与 `CardModel.RemoveFromState`：阻止绕过 UI 的普通移除，
   同时允许 DebtManager 的显式内部授权路径。
6. `Player.FromSerializable` / `SyncWithSerializedPlayer`：加载及重同步后校验零张/一张规则。
7. `NMerchantInventory.Initialize` / `Open` / `DoOpenAnimation` / `UpdateNavigation`：注入并刷新
   还款控件，使其跟随商店动画并接入基础键盘/手柄焦点图。
8. `NTopBarGold.Initialize`：在原版金币控件旁挂载独立的债务文本子节点。节点不参与原版布局，
   因此不会挤压或移动其他顶部栏控件。
9. `Hook.ModifyNextEvent`：在原版完成事件选择后使用运行 RNG 按配置概率替换为黑市；已访问后不再
   抽取。黑市自身 `IsAllowed=false`，避免同时作为无权重的普通 BaseLib 事件进入队列。
10. 四类 `NMerchant*.UpdateVisual` / `OnTryPurchase`：分别建立短生命周期的 UI 预览与玩家主动
   购买上下文，使 Lord's Parasol、AutoSlay 和未知直接调用者天然绕过 LoanService。
11. `MerchantEntry.get_Cost`：仅对标记为黑市的库存，在事件房中补调用原版价格 Hook，使
    Membership Card 与 The Courier 的折扣生效。
12. 三类黑市 `MerchantEntry.CalcCost`：卡牌和遗物每次生成/补货后重套黑市倍率；移除服务固定
    200 基础价。普通商店条目没有黑市标记，不受影响。
13. `NMerchantInventory.GetClosestStockedSlot`：原版默认每个商店展示槽都有 Entry；黑市刻意留下
    未使用槽位时改用带 null 检查的最近库存槽搜索，避免手柄导航在 `_Ready` 阶段中断。

### 本地化策略

- 所有玩家可见文字必须来自游戏本地化表；玩法和 UI 类中不得硬编码显示文本。
- 英文是缺失翻译时的语义基准，但发布包必须同时覆盖当前游戏的 16 个语言代码：
  `deu/eng/esp/fra/ind/ita/jpn/kor/pol/ptb/rus/spa/tha/tur/zhs/zht`。
- 每种语言必须保持相同键集合与格式化占位符。自动化测试检查键覆盖和 `{Debt}` 占位符。
- 后续 Trading 与 Black Market 的每个新增玩家可见键，必须在同一个变更中加入全部语言表。

### v0.1 还款边界

- 只能在标准商店偿还；黑市实现后也可提供相同入口。
- 不能在地图、战斗、营火或奖励画面随时偿还。
- 还款不会消耗购买机会，也不会把贷款金额放入钱包。
- 该限制让欠款凭证至少持续到下一次商店，保留路线选择与诅咒占牌的代价。
- 还款控件作为卡牌移除服务旁的商店操作显示，并随商店库存一起开合，不使用独立窗口。
- 金额滑杆支持鼠标拖动、键盘方向键及手柄方向输入；确认按钮接入商店焦点导航图。

### 暂不需要，原型失败后再评估

- 自定义事件注册：BaseLib `CustomEventModel` 已覆盖。
- 黑市事件权重：先验证 BaseLib 事件池是否能表达稀有权重。

## 5. 多人风险

交易功能取消后，不再需要交易会话、双方确认或遗物转移同步。现阶段仍需关注：

- **模组/配置一致性**：联机双方必须使用相同模组版本和影响平衡的配置；MaxDebt 与倍率应由主机
  作为权威值。
- **标准购买同步**：Debt 卡添加必须复用游戏已有牌组同步路径，不能只在本地改集合。
- **债务实时变化**：新增 Debt 卡复用游戏牌组同步路径，但现有卡上的 `SavedSpireField` 数值变化
  仍需双人实机验证。

## 6. 建议目录结构

```text
SpireEconomyCode/
├── MainFile.cs
├── Configuration/
│   └── EconomyConfig.cs
├── Cards/
│   └── SpireEconomyCard.cs
├── Debt/
│   ├── DebtManager.cs
│   ├── LoanService.cs
│   └── DebtCurse.cs
├── BlackMarket/
│   ├── BlackMarketEvent.cs
│   ├── BlackMarketInventory.cs
│   └── RelicSaleRules.cs
├── Patches/              # 实现阶段加入最小 Harmony 补丁
└── UI/                   # 商店还款、交易、黑市场景
```

## 7. 实施顺序

1. 建立反射/签名测试，锁定 v0.111.0 的补丁目标。
2. 实现 `DebtCurse` 保存字段、唯一性、不可打出和受保护移除。
3. 实现 `DebtManager`，覆盖新债务、重复借款、归零移除、存读档测试。
4. 实现纯领域 `LoanService` 与事务测试。
5. 接入标准商店购买 Harmony 补丁；再加入 Repay Debt UI。
6. 实现黑市库存、事件、倍率价格、贷款购买和遗物出售。
7. 做单人、双人、保存读取、重复回调及版本升级回归测试。

## 8. 待验证假设

1. 作者确认为 `Andy`。
2. `SavedSpireField` 对现有 Debt 卡数值变更的实时多人广播方式仍需验证；新增卡已复用游戏奖励同步器。
3. 原版遗物购买在取得遗物时抛异常后的回滚能力没有公开事务 API，需要运行期故障注入验证。
4. 黑市出售遗物时，哪些遗物能够安全执行 `AfterRemoved` 仍需逐类验证；未知遗物默认不可出售。
5. 还款界面的实机手柄导航仍需验证。
6. BaseLib 3.4.5 与游戏 v0.111.0 已完成启动及基础玩法验证；游戏更新后仍需重新检查补丁签名。
