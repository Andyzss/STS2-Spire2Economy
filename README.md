# Spire Economy

《杀戮尖塔 2》经济扩展模组 v0.1。

计划功能：

- 商店贷款与个人债务
- 商店还款
- 稀有黑市事件

当前已经完成 Debt Curse 持久化/唯一性/移除保护、`DebtManager`、`LoanService`、标准商店
卡牌/遗物/药水/卡牌移除贷款、商店还款 UI、顶部栏欠款显示和 16 种语言本地化。黑市玩法尚未实现。

多人营火遗物交易已于 2026-09-02 从项目范围中取消；本项目也不实现卡牌交易或玩家之间的
金币交易。当前范围与完成情况见 [PROJECT_STATUS.md](PROJECT_STATUS.md)，架构与 API 见
[TECHNICAL_DESIGN.md](TECHNICAL_DESIGN.md)，工具链安装见
[BUILD_PREREQUISITES.md](BUILD_PREREQUISITES.md)，验证状态见 [VALIDATION.md](VALIDATION.md)。

## 本机构建

前置条件：

- .NET 9 SDK
- Steam 版 Slay the Spire 2
- BaseLib 3.4.5
- 发布资源包时需要 Godot 4.5.1 .NET 版

```sh
dotnet build SpireEconomy.csproj
```

构建会通过 `Sts2PathDiscovery.props` 自动查找游戏，并把 DLL 与清单复制到
`Slay the Spire 2/mods/SpireEconomy/`。发布 PCK 前需在 `Directory.Build.props` 设置
`GodotPath`。
