# Spire Economy

《杀戮尖塔 2》经济扩展模组 v0.1。

计划功能：

- 商店贷款与个人债务
- 商店还款
- 多人营火遗物交易
- 稀有黑市事件

当前完成阶段 1–4：Debt Curse 持久化/唯一性/移除保护、`DebtManager`、`LoanService`、
标准商店卡牌与遗物贷款、鼠标优先的商店还款 UI，以及存读档校验。黑市玩法和多人营火交易
尚未实现。架构与 API 见 [TECHNICAL_DESIGN.md](TECHNICAL_DESIGN.md)，工具链安装见
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
