# 构建前置条件

本项目当前锁定以下版本：

- Slay the Spire 2：`v0.111.0`（Steam 公测分支，Build ID `24724944`）
- .NET SDK：`9.x`（不是只有 .NET 9 Runtime）
- Godot：`4.5.1-stable .NET/Mono`，必须精确为 4.5.1
- Python：`3.10+`（资源/检查脚本；推荐 3.12）
- BaseLib：`3.4.5`（NuGet 在 restore 时获取；游戏 mods 目录也必须安装 3.4.5 或更高兼容版本）

## Windows 安装命令

在 PowerShell 中安装 SDK 与 Python：

```powershell
winget install --exact --id Microsoft.DotNet.SDK.9
winget install --exact --id Python.Python.3.12
```

Godot 请下载官方 `Godot_v4.5.1-stable_mono_win64.zip`：

```powershell
$godotZip = "$env:TEMP\Godot_v4.5.1-stable_mono_win64.zip"
Invoke-WebRequest "https://github.com/godotengine/godot/releases/download/4.5.1-stable/Godot_v4.5.1-stable_mono_win64.zip" -OutFile $godotZip
Expand-Archive -LiteralPath $godotZip -DestinationPath "C:\Tools" -Force
```

然后把 `Directory.Build.props` 中的 `GodotPath` 设置为实际 exe，例如：

```xml
<GodotPath>C:\Tools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe</GodotPath>
```

## 版本确认

```powershell
dotnet --list-sdks
python --version
& "C:\Tools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe" --version
```

预期分别看到 `9.x`、`Python 3.10+`、`4.5.1.stable.mono`。

## 还原、测试、构建与发布

在仓库根目录执行：

```powershell
dotnet restore .\SpireEconomy.sln
dotnet test .\SpireEconomy.Tests\SpireEconomy.Tests.csproj
dotnet build .\SpireEconomy.csproj -c Debug
& "C:\Tools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe" --headless --import --path .
dotnet publish .\SpireEconomy.csproj -c Debug
```

`dotnet build` 会把 DLL/清单复制到游戏 mods 目录；资源或本地化有变化时必须执行 Godot import
与 `dotnet publish` 生成 PCK。发布前关闭游戏，发布后重新启动。

日常开发也可以在项目根目录使用一键安装脚本；它会拒绝在游戏运行时覆盖文件，并验证 DLL、清单和
PCK 都已安装：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-dev.ps1
```

如果 Windows 不允许直接打开 `.ps1`，请双击 `scripts\install-dev.cmd`。该启动器会以正确的
PowerShell 参数运行脚本，并保留窗口以便查看成功信息或错误。

## 当前机器状态（2026-09-01）

- 已安装 .NET SDK `9.0.317`。
- 已安装 Godot `4.5.1.stable.mono`：
  `C:\Tools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe`。
- 未发现 Python。
- 项目已改为 BaseLib `3.4.5`；静态版本一致性检查通过。

尚不能声称编译、测试或游戏内验证成功。Codex 沙箱中的首次 NuGet restore 因无法读取用户级
`NuGet.Config` 而停止；请在普通 PowerShell 中执行上面的 restore/test/build 命令。
