# v0.1 阶段 1–4 验证矩阵

状态词：`自动测试通过` 表示已有可重复测试；`游戏内确认` 表示已由当前测试版本实际操作；
`待专项验证` 表示代码已存在，但尚未单独完成该场景的运行验证。

| 用例 | 覆盖位置 | 当前状态 |
|---|---|---|
| 借 1 金币 | `LoanMathTests.BorrowOneGoldUsesExactShortfall` | 自动测试通过 |
| 恰好借到 MaxDebt | `BorrowExactlyToMaxDebtIsAllowed` | 自动测试通过 |
| 尝试超过 MaxDebt | `BorrowBeyondMaxDebtIsRejected` | 自动测试通过 |
| 多次融资仍只有一张 Debt Curse | `DebtManager.TryAddDebtAsync` 的查找/复用/去重 | 核心贷款流程游戏内确认；重复卡专项验证待补 |
| 部分还款 | `LoanMath` 边界测试 + `DebtManager.TryRepayAsync` | 自动测试通过；游戏内确认 |
| 全额还款 | `TryRepayAsync` 的零值内部移除路径 | 自动测试通过；移除卡牌待专项确认 |
| 活跃债务存档/读档 | `SavedSpireField` + `Player.FromSerializable` 补丁 | 待专项验证 |
| 清债后存档/读档 | 零债务卡清理 | 待专项验证 |
| 普通卡牌移除不能删除 Debt Curse | 三层移除保护补丁 | 待专项验证 |
| 内部清债可以删除 Debt Curse | `DebtRemovalAuthorization` | 待专项验证 |
| 原版足额购买不受影响 | 无融资预约时原任务原样返回 | 游戏内确认 |
| 失败购买不改变金币/债务/库存 | 失败分支恢复金币、不提交债务；库存依赖原版失败契约 | 自动逻辑覆盖；运行期故障待验证 |
| 贷款购买药水 | 通用 `MerchantEntry.OnTryPurchaseWrapper` 融资预约 | 游戏内确认 |
| 贷款移除卡牌 | `MerchantCardRemovalEntry.OnTryPurchaseWrapper` 专用补丁 | 游戏内确认 |
| 取消贷款移除卡牌 | 专用补丁失败分支释放预约并恢复金币 | 游戏内确认 |
| 还款入口属于商店布局 | 挂载为 `%MerchantCardRemoval` 同级节点并跟随库存动画 | 游戏内确认；最新无延迟修复待目视回归 |
| 手柄进入与退出还款操作 | 卡牌移除槽右侧进入；滑杆上方或确认按钮左侧返回 | 静态检查；实机手柄未验证 |
| 手柄选择精确金额 | `HSlider` 步长 1，左右调节，上下切换确认 | 已实现；实机手柄未验证 |
| 无债务时点击还款 | 原版商人气泡显示自定义本地化对白 | 游戏内确认 |
| 有债务但无金币时点击还款 | 原版商人气泡显示自定义本地化对白 | 游戏内确认 |
| 普通购买后刷新可还金额 | `NMerchantInventory.OnPurchaseCompleted` 后刷新 | 游戏内确认 |
| 16 种语言键与占位符一致 | `LocalizationCoverageTests` | 自动测试通过 |
| 欠款凭证第二行不显示字面 `NL` | `EveryDebtCardPlacesAutomaticRemovalOnANewLine` | 自动测试通过；游戏内目视待确认 |

当前自动化测试共 19 项并全部通过。下一轮应优先完成存读档、普通移除保护、手柄导航和双人债务
同步专项验证，再开始黑市实现。
