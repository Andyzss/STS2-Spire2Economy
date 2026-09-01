# v0.1 阶段 1–4 验证矩阵

状态词：`已实现` 表示已有代码路径；`静态检查` 表示只核对源码/程序集签名；`运行未验证` 表示
尚未完成单元测试或游戏内测试。

| 用例 | 覆盖位置 | 当前状态 |
|---|---|---|
| 借 1 金币 | `LoanMathTests.BorrowOneGoldUsesExactShortfall` | 已实现；运行未验证 |
| 恰好借到 MaxDebt | `BorrowExactlyToMaxDebtIsAllowed` | 已实现；运行未验证 |
| 尝试超过 MaxDebt | `BorrowBeyondMaxDebtIsRejected` | 已实现；运行未验证 |
| 多次融资仍只有一张 Debt Curse | `DebtManager.TryAddDebtAsync` 的查找/复用/去重 | 静态检查；游戏内未验证 |
| 部分还款 | `LoanMath` 边界测试 + `DebtManager.TryRepayAsync` | 已实现；运行未验证 |
| 全额还款 | `TryRepayAsync` 的零值内部移除路径 | 静态检查；游戏内未验证 |
| 活跃债务存档/读档 | `SavedSpireField` + `Player.FromSerializable` 补丁 | 静态检查；游戏内未验证 |
| 清债后存档/读档 | 零债务卡清理 | 静态检查；游戏内未验证 |
| 普通卡牌移除不能删除 Debt Curse | 三层移除保护补丁 | 静态检查；游戏内未验证 |
| 内部清债可以删除 Debt Curse | `DebtRemovalAuthorization` | 静态检查；游戏内未验证 |
| 原版足额购买不受影响 | 无融资预约时原任务原样返回 | 静态检查；游戏内未验证 |
| 失败购买不改变金币/债务/库存 | 失败分支恢复金币、不提交债务；库存依赖原版失败契约 | 静态检查；游戏内未验证 |
| 还款入口属于商店布局 | 挂载为 `%MerchantCardRemoval` 同级节点并跟随库存动画 | 静态检查；游戏内未验证 |
| 手柄进入与退出还款操作 | 卡牌移除槽右侧进入；滑杆上方或确认按钮左侧返回 | 静态检查；实机手柄未验证 |
| 手柄选择精确金额 | `HSlider` 步长 1，左右调节，上下切换确认 | 已实现；实机手柄未验证 |
| 无金币时不进入无效控件 | 禁用滑杆/按钮并恢复卡牌移除槽原导航 | 已实现；实机手柄未验证 |
| 普通购买后刷新可还金额 | `NMerchantInventory.OnPurchaseCompleted` 后刷新 | 已实现；游戏内未验证 |

安装工具链后，先运行单元测试，再在单人新档逐项执行以上用例；最后用双人房验证新增 Debt 卡和
已有 Debt 数值变化的同步行为。后者目前仍是明确的多人 API 假设。
