# SpireEconomy v0.1 原版兼容性审计

审计目标：Slay the Spire 2 `v0.111.0`（`release_info.json` commit `41cef1ea`）。
本文件只覆盖 Debt、Loan 与 Merchant；黑市、交易、利息、破产、讨债和新遗物不在本次范围。

## 结论

贷款现在必须同时满足：来源是玩家在原版商店 UI 中主动选择、交易需要支付金币、
最终 `Cost > 0`、金币不足、商品类型受支持、结算后不超过 `MaxDebt=300`。
Debt 只会在成功的贷款购买回调之后由 `DebtManager.TryAddDebtAsync` 增加。

卡牌移除是项目所有者明确保留的例外：**收费的商店卡牌移除可以贷款**；
Lord's Parasol 的免费自动移除以及其他免费移除不能产生 Debt。

## v0.111.0 程序集调查证据

### Lord's Parasol（领主阳伞）

`LordsParasol.AfterRoomEntered` 只在 `MerchantRoom` 中取得本地玩家的
`MerchantInventory`，然后异步运行 `PurchaseEverything`。

`PurchaseEverything` 的顺序是：

1. 阻止商店输入并打开商店界面。
2. 依次遍历 `CharacterCardEntries`。
3. 依次遍历 `ColorlessCardEntries`。
4. 依次遍历 `RelicEntries`。
5. 依次遍历 `PotionEntries`。
6. 最后处理 `CardRemovalEntry`。
7. 恢复商店输入和地图导航。

前四类商品都调用 `MerchantEntry.OnTryPurchaseWrapper(inventory, true)`；
卡牌移除调用 `MerchantCardRemovalEntry.OnTryPurchaseWrapper(inventory, true, false)`。
第一个布尔参数 `ignoreCost=true`，因此派生商品逻辑不会调用 `PlayerCmd.LoseGold`。

原版包装器仍然执行正常的库存检查、派生商品获得逻辑、清空或补货、
`Hook.AfterItemPurchased` 和 `PurchaseCompleted`。因此卡牌、遗物、药水、同步消息、
购买历史与其他原版获得副作用仍走原版路径，而不是由 SpireEconomy 重新发放物品。

### The Courier 与 Membership Card

- `TheCourier.ShouldRefillMerchantEntry` 对遗物持有者返回 true，原版包装器在购买成功后
  调用具体商品的 `RestockAfterPurchase`。
- `TheCourier.ModifyMerchantPrice` 和 `MembershipCard.ModifyMerchantPrice` 都通过
  `Hook.ModifyMerchantPrice` 参与最终价格计算。
- `MerchantEntry.Cost` 读取基础 `_cost` 后，在普通 `MerchantRoom` 中调用所有
  `ModifyMerchantPrice` 监听器，最后把 decimal 结果转换为 int。
- SpireEconomy 在单次交易开始时读取 `entry.Cost`，没有根据 BasePrice 重算价格。

### 原版包装器与获得副作用

`MerchantEntry.OnTryPurchaseWrapper` 的成功路径是：库存检查 → 金币/ignoreCost 检查 →
具体 `OnTryPurchase` → Courier 补货或清空槽位 → `Hook.AfterItemPurchased` →
`PurchaseCompleted`。

- 卡牌使用 `CardPileCmd.Add`，成功后扣金币并同步卡牌。
- 遗物扣金币后使用原版 `RelicCmd.Obtain`，再同步遗物。
- 药水使用原版 `PotionCmd.TryToProcure`，成功后扣金币并同步药水。
- 卡牌移除使用原版 `OneOffSynchronizer.DoLocalMerchantCardRemoval`。
- `MawBank.AfterItemPurchased` 会收到原版上报的 `goldSpent`；免费购买为 0，不会错误触发。

SpireEconomy 不复制上述逻辑，只在包装器返回成功后记录准确的 shortfall，因而不会主动
重复触发获得物品、补货、AfterItemPurchased、统计或同步。

### 金币获得与失去

`PlayerCmd.LoseGold` 使用 `Math.Max(0, Gold - amount)`，Gold 不会变成负数。
`PlayerCmd.GainGold` 经过原版 Modify/AfterGoldGained Hook 后只增加 `Player.Gold`。
SpireEconomy 没有 Harmony Patch 覆盖 `PlayerCmd.GainGold`、`LoseGold`、`SetGold` 或
`Player.Gold`，也没有从获得金币的 Hook 调用还款。

程序集扫描到直接金币支付/损失的事件包括 Crystal Sphere、Endless Conveyor、
Luminous Choir、Morphic Grove、Ranwid the Elder、Tea Master、Waterlogged Scriptorium、
Welcome to Wongo's、Whispering Hollow 与 Zen Weaver。它们继续直接读取 `Player.Gold`
并调用原版 `PlayerCmd.LoseGold`，不会看到信用额度，也不会进入 LoanService。

金币奖励来源包括 Colossal Flower、Dense Vegetation、Endless Conveyor、Jungle Maze
Adventure、Lost Wisp、Sunken Statue、Sunken Treasury、The Lantern Key、This or That、
Trash Heap、Trial、GoldReward，以及 Old Coin、Maw Bank 等遗物/奖励路径。它们只增加 Gold。

其他相关原版机制：原版卡牌 `Debt`、Seal of Gold、Silken Tress、ThieveryPower 会失去
Gold；Bowler Hat、Ectoplasm、Dragon Fruit 会修改或响应金币获得。这些机制都没有被
SpireEconomy 的贷款补丁拦截。

## 单次交易上下文

新增 `PurchaseContext`，包含来源、是否要求金币付款、最终价格和商品类型，并派生：

- `PlayerInitiated`
- `IsFree`
- `IsAutomatic`
- `IsForced`
- `IsFinanceEligible`

运行时 `MerchantPurchaseScope` 只围住原版商店 UI 的同步调用边界：

- `UpdateVisual` 使用 `UiPreview`，只让可贷款商品在 UI 中显示为可选择。
- `OnTryPurchase` 使用 `PlayerInitiated`，允许该次包装器创建贷款预留。
- Lord's Parasol、AutoSlay、事件和其他直接调用包装器的代码没有此上下文。
- 上下文按单个 `MerchantEntry` 匹配，不会把整个商店会话标记为允许或禁止贷款。

因此 Lord's Parasol + The Courier 的自动阶段不会借款；补货之后玩家亲自选择的新商品
会获得一个新的 PlayerInitiated 上下文，仍可正常贷款。

## 兼容性矩阵

| 原版机制 | 类型 | 原版行为 | SpireEconomy 预期行为 | 当前实现 | 风险等级 | 自动测试 | 游戏内验证 |
|---|---|---|---|---|---|---|---|
| 普通卡牌购买 | 商店 | 扣 Gold、入牌组、购买 Hook | 金币不足时只借 shortfall | UI 交易上下文 + 原版包装器 | MEDIUM | 有 | 必须 |
| 普通遗物购买 | 商店 | 扣 Gold、原版 Obtain/Hook | 同上且只获得一次 | 原版包装器，Debt 后提交 | MEDIUM | 策略测试 | 必须 |
| 普通药水购买 | 商店 | 先检查药水位，再扣 Gold | 药水位失败时不借款 | 原版返回 false 时不提交 Debt | MEDIUM | 策略测试 | 必须 |
| 收费卡牌移除 | 商店服务 | 选择并移除卡牌、扣 Gold | 项目例外：允许贷款 | 专用重载补丁 + UI 上下文 | HIGH | 有 | 必须 |
| 免费卡牌移除 | 免费服务 | 不扣 Gold | 正常移除且 Debt 不变 | RequiresGoldPayment=false/无上下文 | HIGH | 有 | 必须 |
| Cost=0 商品 | 免费商品 | 免费获得 | Debt 不变 | 策略拒绝 `FinalCost <= 0` | MEDIUM | 有 | 必须 |
| Lord's Parasol | 自动免费购买 | ignoreCost 获取所有槽位及移除 | Gold/Debt 不变，副作用正常 | 无 UI 上下文且 ignoreCost=true | HIGH | 有策略测试 | **必须** |
| Lord's Parasol + The Courier | 自动购买+补货 | 免费初始获取，成功后补货 | 自动阶段零 Debt；后来手购可借 | 每次 transaction 独立判定 | HIGH | 有组合策略测试 | **必须** |
| AutoSlay ShopRoomHandler | 自动购买 | 直接调用 wrapper(false) | 不得使用贷款 | 无 UI 上下文，保持原版 EnoughGold | MEDIUM | 来源策略测试 | 可选 |
| The Courier | 补货/折扣 | 成功后补货并修改价格 | 使用最终 Cost，补货一次 | 不替换原版包装器 | MEDIUM | 最终价测试 | 必须 |
| Membership Card | 折扣 | ModifyMerchantPrice | shortfall 基于折后价 | 读取 `entry.Cost` | MEDIUM | 有 | 必须 |
| Maw Bank | 购买 Hook | 付费购买后停止积累 | 贷款购买仍是正常购买 | 原版 AfterItemPurchased 只触发一次 | MEDIUM | 重复结算测试 | 建议 |
| 事件金币支付 | 事件 | 按 Gold 决定选项/扣款 | 不读取信用额度，不生 Debt | 无 Player/事件金币补丁 | LOW | 非商店来源测试 | 必须抽查 |
| 事件/遗物失去金币 | 金币 | LoseGold 并钳制为 0 | 只改变 Gold | 无全局金币补丁 | LOW | 架构静态检查 | 必须抽查 |
| 奖励/事件/遗物获得金币 | 金币 | GainGold 与原版 Hook | Gold 增加，Debt 不变 | 没有自动还款路径 | LOW | 架构静态检查 | 必须抽查 |
| 其他 Mod 直接调用 wrapper | Mod 兼容 | 来源不明确 | 默认不自动借款 | 没有 UI 上下文则拒绝融资 | MEDIUM | 未知来源测试 | P2 |
| 重复购买回调 | 交易 | 每项一次 | 只能提交一次 Debt | entry 预留 + settlement gate | MEDIUM | 有 | 建议 |
| 最终价格重复读取 | 价格 | Cost Hook 可被多次调用 | 每次应稳定得到同一结算价 | 预留保存首次最终 Cost | MEDIUM | 最终价测试 | 必须 |

## Harmony Patch 清单与影响

| Patch | 原因 | 对其他 Mod 的潜在影响 |
|---|---|---|
| `MerchantEntry.get_EnoughGold` Postfix | 让可融资商品在原版 UI/包装器中通过金币检查 | 只在匹配的 UI preview/player transaction scope 中改变 false；自动和未知调用保持原版 |
| `MerchantEntry.OnTryPurchaseWrapper` Prefix/Postfix | 建立预留并在原版成功后提交 Debt | 不跳过原方法，不复制购买副作用 |
| `MerchantCardRemovalEntry.OnTryPurchaseWrapper` Prefix/Postfix | 支持项目明确保留的收费移除贷款 | 仅玩家 UI 发起且付费时介入 |
| 四类 `NMerchant*.UpdateVisual` Prefix/Postfix | 限定贷款可用状态的 UI 预览来源 | 短暂 ThreadStatic scope，不修改节点或价格 |
| 四类 `NMerchant*.OnTryPurchase` Prefix/Postfix | 证明本次调用来自玩家商店 UI | 不改变原方法返回值和输入方式 |
| Merchant repayment UI patches | 添加还款入口、导航和刷新 | 不参与购买/自动购买资格判断 |
| Debt save/load/removal patches | 保持唯一 Debt Curse 与移除保护 | 仅识别本 Mod 的 DebtCurse |

没有针对 Lord's Parasol、The Courier 或任何具体事件的 `HasRelic`/类型分支。
没有 Patch 全局 Gold 属性或 PlayerCmd 金币方法。

## 尚存限制与假设

1. Lord's Parasol 的 IL 调用链已确认，但仍标为 HIGH，直到真实游戏中验证卡牌、遗物、
   药水、免费移除、Courier 补货、导航和 Debt 前后值。
2. 原版在一次购买中多次读取 `Cost`；当前原版价格 Hook 是确定性的。若其他 Mod 提供
   有副作用或每次变化的价格 Hook，预留价格与后来读取值可能不同，需 P2 联调。
3. 原版具体商品逻辑与包装器是异步的，卡牌/药水/遗物的内部操作顺序不同。
   SpireEconomy 在原版完整成功后提交 Debt，并通过预检查、pending debt 与双重 gate
   避免正常失败和重复回调；但进程崩溃、其他 Mod 在成功途中抛出异常等灾难性情况
   无法对已发生的所有原版副作用做通用回滚。
4. UI 上下文依赖 v0.111.0 的 `NMerchantCard/Relic/Potion/CardRemoval.OnTryPurchase`
   同步启动包装器。静态 API 检查会在签名变化时失败；游戏更新后必须重新审计 IL。
