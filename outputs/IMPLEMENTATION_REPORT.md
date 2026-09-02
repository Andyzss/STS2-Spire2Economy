# Spire Economy v0.1 阶段 1–4 实施报告

> 归档说明（2026-09-02）：这是 2026-09-01 的阶段快照，其中“未编译、未运行验证”和后续交易
> 计划已经过时。当前范围与验证状态请以根目录 [`PROJECT_STATUS.md`](../PROJECT_STATUS.md) 和
> [`VALIDATION.md`](../VALIDATION.md) 为准。多人营火遗物交易已取消。

日期：2026-09-01
目标游戏：Slay the Spire 2 `v0.111.0` / Steam Build `24724944`
BaseLib：`3.4.5`
作者：Andy

## 状态结论

- **已实现**：Debt Curse 持久字段、唯一性、不可打出、普通移除/变形保护、内部清债路径、
  `DebtManager`、`LoanService`、标准商店卡牌/遗物融资、一次性购买预约、鼠标优先还款 UI、
  玩家反序列化与重同步后的债务校验。
- **已静态检查**：所有 Harmony 目标在本机 `sts2.dll` 中存在且签名匹配；项目 JSON/XML 可解析；
  `scripts/static_api_check.ps1` 执行通过。
- **运行已验证**：无。当前已安装 .NET SDK 9.0.317 与 Godot 4.5.1；NuGet restore 在 Codex
  沙箱中因用户配置读取权限停止，尚未编译或执行测试。
- **当时未实现**：黑市玩法、利息、破产、讨债事件、卡牌移除贷款。多人营火交易及交易 UI
  后来于 2026-09-02 正式取消。

## 1. 文件变更

新增：

- `BUILD_PREREQUISITES.md`
- `VALIDATION.md`
- `SpireEconomyCode/Patches/DebtRemovalPatches.cs`
- `SpireEconomyCode/Patches/DebtSaveLoadPatches.cs`
- `SpireEconomyCode/Patches/MerchantLoanPatches.cs`
- `SpireEconomyCode/Patches/MerchantRepaymentUiPatches.cs`
- `SpireEconomyCode/UI/DebtRepaymentPanel.cs`
- `SpireEconomy.Tests/SpireEconomy.Tests.csproj`
- `SpireEconomy.Tests/LoanMathTests.cs`
- `SpireEconomy.Tests/DebtLoadMathTests.cs`
- `scripts/static_api_check.ps1`
- `outputs/IMPLEMENTATION_REPORT.md`

修改：

- `README.md`
- `TECHNICAL_DESIGN.md`
- `SpireEconomy.csproj`
- `SpireEconomy.sln`
- `cards.csv`
- `SpireEconomy/localization/eng/cards.json`
- `SpireEconomyCode/MainFile.cs`
- `SpireEconomyCode/Cards/SpireEconomyCard.cs`
- `SpireEconomyCode/Debt/DebtCurse.cs`
- `SpireEconomyCode/Debt/DebtManager.cs`
- `SpireEconomyCode/Debt/LoanService.cs`

## 2. 架构决定

1. 债务唯一事实来源是 Debt Curse 实例上的 `SavedSpireField<DebtCurse,int>`；
   `DebtManager` 是唯一写入口，卡面只读取显示。
2. 每个玩家使用独立异步锁串行化借款和还款。多张异常 Debt Curse 不累加，取最大非负值，
   保留一张并删除其余；零值卡全部删除。
3. `LoanService` 集中处理 shortfall、capacity、MaxDebt 和还款边界，UI 与 Harmony 补丁不重复
   硬编码上限。
4. 标准足额购买保持原任务不变。融资仅允许 `MerchantCardEntry` 与 `MerchantRelicEntry`；
   `MerchantCardRemovalEntry` 和药水等入口不放行。
5. 缺金购买不向钱包加入借款。原版 `LoseGold` 把现有余额扣到 0，成功后只把购买前余额与价格的
   精确差额写成债务。
6. 每个商品只有一个活动融资预约，并区分 UI 的“可融资显示”与当前购买调用授权，阻止双击或
   重复回调造成重复发货/单次记债；同一玩家全部未提交预约也会合计占用借款容量。
7. 还款先校验 `0 < amount <= min(gold, debt)`，钱包与债务以同额结算；异常时恢复两者。

## 3. Harmony 补丁

- `MerchantEntry.get_EnoughGold`
- `MerchantEntry.OnTryPurchaseWrapper`
- `CardModel.get_IsRemovable`
- `CardModel.get_IsTransformable`
- `CardModel.RemoveFromState`
- `CardPileCmd.RemoveFromDeck(CardModel, bool)`
- `CardPileCmd.RemoveFromDeck(IReadOnlyList<CardModel>, bool)`
- `Player.FromSerializable(SerializablePlayer)`
- `Player.SyncWithSerializedPlayer(SerializablePlayer)`
- `NMerchantInventory.Initialize(...)`
- `NMerchantInventory.Open()`

## 4. 未解决的 API 假设

1. 原版购买回调先发商品，补丁随后写债务。公开 API 没有可同时回滚商品、金币、历史记录和网络
   消息的事务；若新增 Debt 卡极端情况下写入失败，目前会抛出显式错误，但无法可靠撤销已发遗物。
2. 新增 Debt 卡使用 `RewardSynchronizer.SyncLocalObtainedCard`；已有 Debt 卡持久字段的实时多人
   增量同步仍需协议验证。存档/重连序列化路径已接入，但不能据此宣称实时同步完成。
3. `Player.FromSerializable` 和 `SyncWithSerializedPlayer` 的 Postfix 被假设发生在 Deck 完整建立后；
   从 IL/签名看成立，仍需用真实存档验证。
4. 最简 Godot 面板未加入原版键盘/手柄焦点图；当前仅承诺鼠标操作。
5. 卡面本地化动态参数与 BaseLib 3.4.5 的运行时渲染需实际启动确认。

## 5. 测试与验证

已加入 xUnit 纯逻辑测试：

- 借 1 金币。
- 恰好达到 MaxDebt。
- 超过 MaxDebt 被拒绝。
- 足额购买 shortfall 为 0。
- 还款上下界。
- 部分还款。
- 全额还款归零。
- 非法还款不能产生负余额。
- 空/已清债存档归零。
- 重复 Debt Curse 选择一个规范值而非累加。
- 损坏的负存档值钳制为 0。

集成用例及当前状态见 `VALIDATION.md`。测试源已写入，但尚未成功完成 NuGet restore，因此未执行。

已执行且通过：

```powershell
pwsh -NoProfile -File .\scripts\static_api_check.ps1
```

已执行但按预期失败：

```powershell
dotnet build .\SpireEconomy.sln --no-restore
```

最初失败原因是未安装 SDK；SDK 安装后，restore 又因 Codex 沙箱无法读取用户级
`NuGet.Config` 而停止。这些都不是源码编译结果。

## 6. 工具链导致的未验证项

- BaseLib 3.4.5 NuGet restore、C# 编译与 ModAnalyzers。
- NuGet restore 和 xUnit 执行。
- Godot 资源导入/PCK 发布。
- 游戏启动、卡面、鼠标 UI 布局。
- 所有商店购买、还款、移除、存读档集成用例。
- 双人房实时债务同步。

精确安装版本与命令见 `BUILD_PREREQUISITES.md`。

## 7. 建议下一步

先安装 .NET 9 SDK，运行 restore/test/build，修复所有编译或分析器问题；再安装 Godot 4.5.1
.NET 版发布 PCK，并按 `VALIDATION.md` 逐项做单人游戏内验证。单人流程稳定后，优先验证已有 Debt
数值变化的多人同步，必要时加入独立的权威债务消息，再进入黑市阶段。营火交易不再实施。
