param(
    [Parameter(Mandatory = $true)]
    [string]$Pattern,
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2"
)

$ErrorActionPreference = "Stop"
$dataDirectory = Join-Path $GamePath "data_sts2_windows_x86_64"
$assemblyPath = Join-Path $dataDirectory "sts2.dll"
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "sts2.dll not found: $assemblyPath"
}

$assemblyMap = @{}
Get-ChildItem -LiteralPath $dataDirectory -Filter "*.dll" | ForEach-Object {
    $assemblyMap[$_.BaseName] = $_.FullName
}

$resolver = {
    param($context, $assemblyName)
    if ($assemblyMap.ContainsKey($assemblyName.Name)) {
        try { return $context.LoadFromAssemblyPath($assemblyMap[$assemblyName.Name]) }
        catch { return $null }
    }
    return $null
}

[System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving($resolver)
try {
    foreach ($dependency in @("GodotSharp", "0Harmony", "SmartFormat", "SmartFormat.ZString")) {
        if ($assemblyMap.ContainsKey($dependency)) {
            try {
                [void][System.Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath(
                    $assemblyMap[$dependency])
            }
            catch { }
        }
    }

    $assembly = [System.Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath($assemblyPath)
    $oneByte = @{}
    $twoByte = @{}
    [System.Reflection.Emit.OpCodes].GetFields("Public,Static") | ForEach-Object {
        $opcode = $_.GetValue($null)
        $value = [uint16]([int]$opcode.Value -band 0xffff)
        if ($value -le 0xff) { $oneByte[[byte]$value] = $opcode }
        else { $twoByte[[byte]($value -band 0xff)] = $opcode }
    }

    $flags = [System.Reflection.BindingFlags]"Public,NonPublic,Instance,Static,DeclaredOnly"
    $matches = foreach ($type in $assembly.GetTypes()) {
        foreach ($method in $type.GetMethods($flags)) {
            $body = $method.GetMethodBody()
            if ($null -eq $body) { continue }
            $bytes = $body.GetILAsByteArray()
            $index = 0
            while ($index -lt $bytes.Length) {
                $first = $bytes[$index++]
                $opcode = if ($first -eq 0xfe) { $twoByte[$bytes[$index++]] } else { $oneByte[$first] }
                $operandType = $opcode.OperandType.ToString()
                $size = 0
                if ($operandType -eq "InlineSwitch") {
                    $count = [BitConverter]::ToInt32($bytes, $index)
                    $size = 4 + (4 * $count)
                }
                elseif ($operandType -in @("InlineBrTarget", "InlineField", "InlineI", "InlineMethod",
                    "InlineSig", "InlineString", "InlineTok", "InlineType", "ShortInlineR")) {
                    $size = 4
                }
                elseif ($operandType -in @("InlineI8", "InlineR")) { $size = 8 }
                elseif ($operandType -eq "InlineVar") { $size = 2 }
                elseif ($operandType -in @("ShortInlineBrTarget", "ShortInlineI", "ShortInlineVar")) {
                    $size = 1
                }

                if ($operandType -in @("InlineField", "InlineMethod", "InlineTok", "InlineType")) {
                    $token = [BitConverter]::ToInt32($bytes, $index)
                    try {
                        $target = $method.Module.ResolveMember(
                            $token,
                            $method.DeclaringType.GetGenericArguments(),
                            $method.GetGenericArguments())
                        $targetName = "$($target.DeclaringType.FullName).$($target.Name)"
                        if ($targetName -match $Pattern) {
                            [pscustomobject]@{
                                Caller = "$($method.DeclaringType.FullName).$($method.Name)"
                                Opcode = $opcode.Name
                                Target = $targetName
                            }
                        }
                    }
                    catch { }
                }
                $index += $size
            }
        }
    }

    $matches | Sort-Object Caller, Target -Unique
}
finally {
    [System.Runtime.Loader.AssemblyLoadContext]::Default.remove_Resolving($resolver)
}
