# Offline production rules and retirement checks; no game/server or test application.
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'src/OrcSkinRules.cs')
$script:checks = 0
function Assert-True([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:checks++
}
function Assert-Near([double]$Actual, [double]$Expected, [string]$Name) {
    Assert-True ([Math]::Abs($Actual - $Expected) -lt 0.000001) $Name
}
# Installed vanilla armor.json tin-bronze lamellar: T2, flat .6, relative .77,
# within/above losses .1/.2 flat, .03/.15 relative. Independent worked examples.
Assert-Near ([rfmechanics.OrcSkinRules]::Protect(8,0)) 1.702 'Tier-zero attack'
Assert-Near ([rfmechanics.OrcSkinRules]::Protect(8,1)) 1.89825 'Tier-one attack'
Assert-Near ([rfmechanics.OrcSkinRules]::Protect(8,2)) 2.0938532 'Standard wolf 8 HP T2 attack'
Assert-Near ([rfmechanics.OrcSkinRules]::Protect(0,2)) 0 'Fully shield-blocked hit remains zero'
foreach ($damage in @(0.01,0.1,1,5,8,20,100)) { foreach ($weapon in -1..25) {
    $after = [rfmechanics.OrcSkinRules]::Protect($damage,$weapon)
    Assert-True ($after -ge 0 -and $after -le $damage) 'Natural skin never amplifies damage'
}}
$root = Join-Path $PSScriptRoot 'src'
Assert-True (-not (Test-Path (Join-Path $root 'OrcBracingModSystem.cs'))) 'Active bracing system retired'
Assert-True (-not (Test-Path (Join-Path $root 'OrcBracingRules.cs'))) 'Bracing cost/facing state retired'
$input = Get-Content (Join-Path $root 'RaceAbilityHotkeyModSystem.cs') -Raw
Assert-True (-not $input.Contains('[PlayerRace.Orc]')) 'No Orc Race Ability handler'
Assert-True (-not $input.Contains('TriggerOnUpAlso')) 'Other race press-only handler retained'
foreach ($key in @('feedback-brace','feedback-release','feedback-brace-disabled','feedback-brace-hungry','feedback-brace-recovered')) {
    $lang = ((Get-Content (Join-Path $PSScriptRoot 'assets/rfmechanics/lang/en.json') | Where-Object { $_ -notmatch '^\s*//' }) -join "`n") | ConvertFrom-Json
    Assert-True ($null -eq $lang.PSObject.Properties[$key]) 'Bracing announcement removed'
}
$skin = Get-Content (Join-Path $root 'OrcSkinModSystem.cs') -Raw
Assert-True (-not $skin.Contains('InventoryManager')) 'Equipment does not gate natural skin'
Assert-True (-not $skin.Contains('Saturation')) 'No skin food debit'
Assert-True (-not $skin.Contains('Yaw')) 'All-direction protection without a frontal gate'
Assert-True (-not $skin.Contains(' -> ')) 'No markup-unsafe diagnostic arrow'
Write-Output "PASS: $script:checks production-rule and retirement assertions; gameplay remains a player check."
