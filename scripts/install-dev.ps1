param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "SpireEconomy.csproj"
$gameExecutable = Join-Path $GamePath "SlayTheSpire2.exe"
$modDirectory = Join-Path $GamePath "mods\SpireEconomy"
$packPath = Join-Path $modDirectory "SpireEconomy.pck"

if (-not (Test-Path -LiteralPath $gameExecutable)) {
    throw "Slay the Spire 2 was not found at: $GamePath"
}

if (Get-Process -Name "SlayTheSpire2" -ErrorAction SilentlyContinue) {
    throw "Slay the Spire 2 is running. Close the game before installing the mod."
}

$buildPropsPath = Join-Path $projectRoot "Directory.Build.props"
$buildProps = [xml](Get-Content -LiteralPath $buildPropsPath -Raw)
$godotPath = [string]$buildProps.Project.PropertyGroup.GodotPath
if ([string]::IsNullOrWhiteSpace($godotPath) -or -not (Test-Path -LiteralPath $godotPath)) {
    throw "GodotPath in Directory.Build.props does not point to an existing executable: $godotPath"
}

$godotVersion = (& $godotPath --version | Select-Object -First 1)
if ($godotVersion -notmatch '^4\.5\.1') {
    throw "Godot 4.5.1 is required, but this executable reported: $godotVersion"
}

Write-Host "Building SpireEconomy ($Configuration)..."
& dotnet build $projectFile -c $Configuration "-p:Sts2Path=$GamePath"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

New-Item -ItemType Directory -Force -Path $modDirectory | Out-Null
$exportStartedAt = [DateTime]::UtcNow
Write-Host "Exporting localization and assets to SpireEconomy.pck..."
$godotArguments = "--headless --path `"$projectRoot`" --export-pack `"BasicExport`" `"$packPath`""
# Godot's Windows executable is a GUI-subsystem process. An interactive PowerShell invocation can
# return before it finishes, so file validation must use Start-Process -Wait.
$godotProcess = Start-Process -FilePath $godotPath -ArgumentList $godotArguments `
    -Wait -PassThru -NoNewWindow
if ($godotProcess.ExitCode -ne 0) {
    throw "Godot PCK export failed with exit code $($godotProcess.ExitCode)"
}

if (-not (Test-Path -LiteralPath $packPath)) {
    throw "Godot reported success but did not create: $packPath"
}

$pack = Get-Item -LiteralPath $packPath
if ($pack.Length -lt 10240 -or $pack.LastWriteTimeUtc -lt $exportStartedAt.AddSeconds(-2)) {
    throw "The PCK was not freshly exported or is unexpectedly small: $packPath"
}

$requiredFiles = @("SpireEconomy.dll", "SpireEconomy.pdb", "SpireEconomy.json", "SpireEconomy.pck")
foreach ($file in $requiredFiles) {
    $installedPath = Join-Path $modDirectory $file
    if (-not (Test-Path -LiteralPath $installedPath)) {
        throw "Installation is incomplete; missing: $installedPath"
    }
}

Write-Host "SpireEconomy development install completed successfully:" -ForegroundColor Green
Get-ChildItem -LiteralPath $modDirectory |
    Where-Object Name -In $requiredFiles |
    Select-Object Name, Length, LastWriteTime
