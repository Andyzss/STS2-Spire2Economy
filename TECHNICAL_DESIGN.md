# Spire Economy v0.1 技术设计

状态：v0.1 阶段 1–4 已实现，待工具链编译与运行验证
调查日期：2026-09-01
目标游戏：Slay the Spire 2 Steam 公测分支 `v0.111.0`（Build ID `24724944`）

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

结论：加入 Trade 入口本身不需要自建 Harmony 补丁。`OnSelect` 的布尔结果与现有营火同步语义
必须通过原型测试确认；只有交易成功提交后才能消耗该玩家的营火动作，取消或拒绝不得消耗。

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

结论：交易协议应为“客户端提出意图，主机验证并提交，主机广播结果”。客户端绝不直接转移
金币或遗物。消息默认 `Reliable`、`ShouldBuffer=true`，并包含会话 ID、双方 NetId、遗物 ModelId/
实例标识、金币数、营火 `RunLocation` 和状态版本。

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

TradeRestSiteOption ─> TradeService ─> TradeSession
                                ├──> RelicTradeRules
                                └──> authoritative multiplayer messages

BlackMarketEvent ─> BlackMarketInventory
        ├──────────> LoanService (purchase only)
        └──────────> RelicSaleRules / game relic lifecycle APIs
```

边界规则：

- `Debt` 不引用黑市或交易。
- `Trading` 不引用债务或黑市。
- `BlackMarket` 只依赖贷款接口，不读取/修改 Debt 卡内部字段。
- 平衡数值全部来自 `EconomyConfig`，不散落在玩法类中。
- UI 只发出命令并渲染状态；业务验证留在服务层，主机再次验证。

### 2.8 稀有度分类（已确认）

- `CardRarity` 实际值为：`None, Basic, Common, Uncommon, Rare, Ancient, Event, Token, Status, Curse, Quest`。
- 游戏没有 `Gold` 卡牌稀有度；v0.1 黑市卡池按已确认规则使用 `Rare`。
- `RelicRarity` 实际值为：`None, Starter, Common, Uncommon, Rare, Shop, Event, Ancient`。
- v0.1 黑市高稀有遗物池只使用 `Rare`，明确排除 `Shop` 与 `Ancient`。

### 3.1 DebtManager

职责：按玩家查找 Debt 卡、保证最多一张、读取/设置保存字段、债务归零时移除卡、债务增加时
创建卡。加载异常存档出现多张卡时保留最大非负债务值而不累加，并删除其余副本；零债务卡会
被删除。运行时变更使用 `CardPileCmd`，每个玩家用独立异步锁串行化。

### 3.2 LoanService

输入：玩家、最终价格、购买来源、执行购买的回调。算法必须满足：

1. `shortfall = max(0, price - player.Gold)`。
2. `shortfall == 0` 时走原购买路径。
3. 新债务超过 `EconomyConfig.MaxDebt` 时拒绝。
4. 借款不进入钱包；购买结算只消耗玩家现有金币，并把差额写入 Debt。
5. 以一次性 `MerchantEntry` 预约防止重复回调重复记债；原版返回失败时恢复购买前金币且不记债。
6. 当前原版回调先发商品、后由补丁持久化债务；若 Debt 卡添加异常，无法借助公开 API 原子撤销
   已发放遗物。当前选择抛出显式错误而不是静默赠送，运行测试前这是一个未闭合的 API 风险。

### 3.3 DebtCurse

继承 `SpireEconomyCard`，使用 `CardType.Curse`、`CardRarity.Curse`、`TargetType.None`，覆盖
`IsPlayable=false`，关闭自然生成和升级。`SavedSpireField<DebtCurse,int>` 保存余额，卡面动态变量
只显示该值。`IsRemovable`、牌组移除命令与直接状态移除形成三层保护；只有 DebtManager 的显式
授权作用域可在清债或修复存档时移除。

### 3.4 TradeService / TradeSession / RelicTradeRules

`TradeSession` 是显式状态机：`Draft -> AwaitingConfirmations -> Committed`，任一方取消、状态过期、
房间变化、断线或验证失败都进入 `Cancelled/Rejected`。任意报价变化都清空双方确认。

主机提交前重新检查：双方仍在同一营火；尚未消费动作；遗物仍属于报价者；金币仍足够；遗物
仍可转移；会话版本匹配。提交时按可回滚顺序操作；成功后广播最终快照并消费相关营火动作。

`RelicTradeRules` 采用稳定 `ModelId` 的显式禁止列表，并叠加结构规则（Starter、已熔化、宠物、
会改变永久槽位/角色基础状态的遗物等）。v0.1 初期应采用保守 allow-list 或强 deny-list。

### 3.5 BlackMarketEvent / BlackMarketInventory

事件非共享，每名玩家独立处理。初始选项始终含“离开”。库存为：

- 2 个 `RelicRarity.Rare` 遗物；v0.1 排除 Shop 与 Ancient。
- 2–3 张 `CardRarity.Rare` 卡；游戏没有 Gold 卡牌稀有度。
- v0.1 不生成 mystery slot。

价格为基础商店价乘 `BlackMarketPricePercent / 100m`。卖价为基础价乘
`RelicSalePricePercent / 100m`。购买通过 `LoanService`；出售先经资格规则，再通过正式遗物移除
生命周期，最后增加金币。失败时必须回滚。

## 4. 预计需要的 Harmony 补丁

### 本阶段已加入

1. `MerchantEntry.get_EnoughGold`：仅对 `MerchantCardEntry` / `MerchantRelicEntry` 把可融资商品
   显示为可购买；卡牌移除、药水及其他入口不放行。
2. `MerchantEntry.OnTryPurchaseWrapper`：建立一次性融资预约，成功后记入精确短缺额，失败不改债务。
3. `CardModel.get_IsRemovable` / `get_IsTransformable`：Debt Curse 对普通选择器不可移除或变形。
4. `CardPileCmd.RemoveFromDeck` 两个重载与 `CardModel.RemoveFromState`：阻止绕过 UI 的普通移除，
   同时允许 DebtManager 的显式内部授权路径。
5. `Player.FromSerializable` / `SyncWithSerializedPlayer`：加载及重同步后校验零张/一张规则。
6. `NMerchantInventory.Initialize` / `Open`：注入并刷新最简鼠标还款控件。键盘/手柄焦点图尚未接入。
7. `NTopBarGold.Initialize`：在原版金币控件旁挂载独立的债务文本子节点。节点不参与原版布局，
   因此不会挤压或移动其他顶部栏控件。

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

- Trade 营火入口：已有 `Hook.ModifyRestSiteOptions`。
- 自定义事件注册：BaseLib `CustomEventModel` 已覆盖。
- 自定义多人消息注册：BaseLib `CustomMessageWrapper` / `CustomTargetedMessageWrapper` 已覆盖。
- 黑市事件权重：先验证 BaseLib 事件池是否能表达稀有权重。

## 5. 多人风险

- **双重提交**：双方确认或消息重试可能触发两次；主机必须用会话 ID 和终态幂等处理。
- **过期报价**：确认后金币/遗物变化；提交前必须重新验证并带状态版本。
- **实例歧义**：同一玩家可能持有多件相同 ModelId 的可堆叠遗物；只传 ModelId 不一定足够。
- **生命周期副作用**：遗物 `AfterRemoved/AfterObtained` 可能不是可逆的，交易需保守限制。
- **营火消费归属**：已确认成功交易消耗双方营火动作；本阶段不实现交易。
- **断线与房间切换**：未提交会话必须取消；迟到消息需由 `RunLocation` 丢弃或缓冲。
- **模组/配置一致性**：联机双方必须使用相同模组版本和影响平衡的配置；MaxDebt 与倍率应由主机
  作为权威值。
- **标准购买同步**：Debt 卡添加必须复用游戏已有牌组同步路径，不能只在本地改集合。

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
├── Trading/
│   ├── TradeService.cs
│   ├── TradeSession.cs
│   └── RelicTradeRules.cs
├── BlackMarket/
│   ├── BlackMarketEvent.cs
│   └── BlackMarketInventory.cs
├── Multiplayer/          # 实现阶段加入消息 DTO/处理器
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
7. 单独完成多人协议原型，先只验证可靠消息、主机身份和双确认状态机。
8. 在协议验证后实现营火 Trade 入口和遗物原子转移。
9. 做单人、双人、断线重连、保存读取、重复消息及版本升级回归测试。

## 8. 待验证假设

1. 作者确认为 `Andy`。
2. 金币换遗物只允许买方付给遗物持有者，还是允许反向补差价（未来交易阶段）。
3. 遗物的移除/重新获得生命周期是否足以判定安全转移；未知遗物将默认不可交易。
4. `SavedSpireField` 对现有 Debt 卡数值变更的实时多人广播方式仍需验证；新增卡已复用游戏奖励同步器。
5. 原版遗物购买在取得遗物时抛异常后的回滚能力没有公开事务 API，需要运行期故障注入验证。
6. 注入的最简 Godot 还款面板尺寸、鼠标命中和商店刷新时序需要实际游戏验证。
7. BaseLib 3.4.5 与游戏 v0.111.0 公测分支的运行时兼容性需实际启动验证。
8. 本机已安装 .NET SDK 9.0.317 与 Godot 4.5.1 Mono，尚未安装 Python；NuGet restore 在
   Codex 沙箱中因用户配置读取权限停止，仍未完成编译与游戏内验证。
