param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2"
)

$ErrorActionPreference = "Stop"
$assemblyPath = Join-Path $GamePath "data_sts2_windows_x86_64\sts2.dll"
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "sts2.dll not found: $assemblyPath"
}

$assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)

function Require-Type([string]$name) {
    $type = $assembly.GetType($name)
    if ($null -eq $type) { throw "Missing type: $name" }
    return $type
}

function Require-Method([string]$typeName, [string]$methodName, [string[]]$parameterTypes) {
    $type = Require-Type $typeName
    [string[]]$expectedParameterTypes = @()
    if ($null -ne $parameterTypes) { $expectedParameterTypes = @($parameterTypes) }
    $method = $type.GetMethods([System.Reflection.BindingFlags]"Public,NonPublic,Static,Instance") |
        Where-Object {
            $actualParameterTypes = @($_.GetParameters() | ForEach-Object { $_.ParameterType.FullName })
            $_.Name -eq $methodName -and
            ($actualParameterTypes -join ",") -eq ($expectedParameterTypes -join ",")
        } |
        Select-Object -First 1
    if ($null -eq $method) { throw "Missing method: $typeName.$methodName($($expectedParameterTypes -join ','))" }
}

function Require-Field([string]$typeName, [string]$fieldName, [string]$fieldType) {
    $type = Require-Type $typeName
    $field = $type.GetField($fieldName,
        [System.Reflection.BindingFlags]"Public,NonPublic,Static,Instance")
    if ($null -eq $field -or $field.FieldType.FullName -ne $fieldType) {
        throw "Missing field: $typeName.$fieldName ($fieldType)"
    }
}

Require-Method "MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry" "OnTryPurchaseWrapper" @(
    "MegaCrit.Sts2.Core.Entities.Merchant.MerchantInventory", "System.Boolean")
Require-Method "MegaCrit.Sts2.Core.Commands.CardPileCmd" "RemoveFromDeck" @(
    "MegaCrit.Sts2.Core.Models.CardModel", "System.Boolean")
Require-Method "MegaCrit.Sts2.Core.Commands.CardPileCmd" "RemoveFromDeck" @(
    'System.Collections.Generic.IReadOnlyList`1[[MegaCrit.Sts2.Core.Models.CardModel, sts2, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null]]', "System.Boolean")
Require-Method "MegaCrit.Sts2.Core.Entities.Players.Player" "FromSerializable" @(
    "MegaCrit.Sts2.Core.Saves.Runs.SerializablePlayer")
Require-Method "MegaCrit.Sts2.Core.Entities.Players.Player" "SyncWithSerializedPlayer" @(
    "MegaCrit.Sts2.Core.Saves.Runs.SerializablePlayer")
Require-Method "MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantInventory" "Initialize" @(
    "MegaCrit.Sts2.Core.Entities.Merchant.MerchantInventory", "MegaCrit.Sts2.Core.Entities.Merchant.MerchantDialogueSet")
Require-Method "MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantInventory" "UpdateNavigation" @()
Require-Method "MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantInventory" "DoOpenAnimation" @()
Require-Method "MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantInventory" "OnPurchaseCompleted" @(
    "MegaCrit.Sts2.Core.Entities.Merchant.PurchaseStatus", "MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry")
Require-Field "MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantCardRemoval" "_removalVisual" "Godot.Sprite2D"
Require-Method "MegaCrit.Sts2.Core.Nodes.Multiplayer.NGenericPopup" "Create" @()
Require-Method "MegaCrit.Sts2.Core.Nodes.CommonUi.NPopupYesNoButton" "SetText" @("System.String")
Require-Method "MegaCrit.Sts2.Core.Nodes.CommonUi.NPopupYesNoButton" "DisconnectHotkeys" @()
Require-Method "MegaCrit.Sts2.Core.Nodes.GodotExtensions.NClickableControl" "SetEnabled" @("System.Boolean")
Require-Method "MegaCrit.sts2.Core.Nodes.TopBar.NTopBarGold" "Initialize" @(
    "MegaCrit.Sts2.Core.Entities.Players.Player")
Require-Field "MegaCrit.sts2.Core.Nodes.TopBar.NTopBarGold" "_goldLabel" "MegaCrit.Sts2.addons.mega_text.MegaLabel"
Require-Method "MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet" "CreateAndShow" @(
    "Godot.Control", "MegaCrit.Sts2.Core.HoverTips.IHoverTip", "MegaCrit.Sts2.Core.HoverTips.HoverTipAlignment")
Require-Method "MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet" "Remove" @("Godot.Control")

$cardRarity = Require-Type "MegaCrit.Sts2.Core.Entities.Cards.CardRarity"
$cardNames = [Enum]::GetNames($cardRarity)
if ($cardNames -contains "Gold") { throw "CardRarity unexpectedly contains Gold; revisit black-market classification." }
if ($cardNames -notcontains "Rare") { throw "CardRarity.Rare is missing." }

$relicRarity = Require-Type "MegaCrit.Sts2.Core.Entities.Relics.RelicRarity"
if ([Enum]::GetNames($relicRarity) -notcontains "Rare") { throw "RelicRarity.Rare is missing." }

$manifest = Get-Content (Join-Path $PSScriptRoot "..\SpireEconomy.json") -Raw | ConvertFrom-Json
$supportedLanguages = @("deu", "eng", "esp", "fra", "ind", "ita", "jpn", "kor",
    "pol", "ptb", "rus", "spa", "tha", "tur", "zhs", "zht")
$localizationRoot = Join-Path $PSScriptRoot "..\SpireEconomy\localization"
foreach ($language in $supportedLanguages) {
    foreach ($table in @("cards.json", "gameplay_ui.json", "settings_ui.json")) {
        $localizationPath = Join-Path $localizationRoot "$language\$table"
        if (-not (Test-Path -LiteralPath $localizationPath)) {
            throw "Missing localization table: $localizationPath"
        }
        Get-Content -LiteralPath $localizationPath -Raw | ConvertFrom-Json | Out-Null
    }
}
$project = [xml](Get-Content (Join-Path $PSScriptRoot "..\SpireEconomy.csproj") -Raw)
$directoryBuild = [xml](Get-Content (Join-Path $PSScriptRoot "..\Directory.Build.props") -Raw)

$baseLibPackage = $project.Project.ItemGroup.PackageReference |
    Where-Object { $_.Include -eq "Alchyr.Sts2.BaseLib" }
if ($baseLibPackage.Version -ne "3.4.5") { throw "Project must compile against BaseLib 3.4.5." }

$baseLibDependency = $manifest.dependencies | Where-Object { $_.id -eq "BaseLib" }
if ($baseLibDependency.min_version -ne "3.4.5") { throw "Manifest must require BaseLib 3.4.5." }

$godotPath = [string]$directoryBuild.Project.PropertyGroup.GodotPath
if ([string]::IsNullOrWhiteSpace($godotPath) -or -not (Test-Path -LiteralPath $godotPath)) {
    throw "Directory.Build.props does not point to an existing Godot executable."
}

Write-Output "Static API and project-file checks passed for sts2.dll v0.111.0 targets."
