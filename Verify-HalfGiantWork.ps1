param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot 'bin/Release/Mods/rfmechanics.dll'),
    [Parameter(Mandatory = $true)]
    [string]$HarnessPath,
    [switch]$ExpectCreativeRegression
)

$ErrorActionPreference = 'Stop'
Add-Type -Path @(
    (Join-Path $PSScriptRoot 'src/HalfGiantReachRules.cs'),
    (Join-Path $PSScriptRoot 'src/HalfGiantQuarryRules.cs')
)
$script:checks = 0
function Assert-True([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:checks++
}
function Assert-Near([float]$Actual, [float]$Expected, [string]$Name) {
    Assert-True ([Math]::Abs($Actual - $Expected) -lt 0.0001) $Name
}
Assert-True ([rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $false, $true, 'game', 'rock-granite')) 'Half-Giant empty hand can quarry natural rock'
Assert-True ([rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $false, $true, 'game', 'crackedrock-granite')) 'Half-Giant empty hand can quarry natural cracked rock'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($false, $true, $false, $true, 'game', 'rock-granite')) 'Quarry enable gate'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $false, $false, $true, 'game', 'rock-granite')) 'Race gate'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $true, $true, 'game', 'rock-granite')) 'Creative gate'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $false, $false, 'game', 'rock-granite')) 'Active-hand gate'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $false, $true, 'game', 'ore-quartz')) 'Ore gate'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $false, $true, 'game', 'stonebricks-granite')) 'Worked-stone gate'
Assert-True (-not [rfmechanics.HalfGiantQuarryRules]::MayQuarry($true, $true, $false, $true, 'examplemod', 'rock-granite')) 'Natural-domain gate'
Assert-Near ([rfmechanics.HalfGiantQuarryRules]::ApplySatietyCost(50, 10)) 40 'One successful break costs ten satiety exactly once'
Assert-Near ([rfmechanics.HalfGiantQuarryRules]::ApplySatietyCost(5, 10)) 0 'Satiety cost does not underflow'
$quarryPatch = Get-Content (Join-Path $PSScriptRoot 'src/HalfGiantQuarryPatch.cs') -Raw
Assert-True ($quarryPatch.Contains('nameof(Block.OnGettingBroken)')) 'Quarry uses empty-hand Block.OnGettingBroken seam'
Assert-True (-not $quarryPatch.Contains('GetMiningSpeed')) 'Quarry does not broaden held-item mining speed'
$quarrySystem = Get-Content (Join-Path $PSScriptRoot 'src/HalfGiantQuarryModSystem.cs') -Raw
Assert-True ($quarrySystem.Contains('DidBreakBlock += OnDidBreakBlock')) 'Satiety charge observes successful server breaks'
Assert-True ($quarrySystem.Contains('GetBlock(oldBlockId)')) 'Satiety charge validates original broken block identity'

if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) { throw "FAIL: production assembly not found: $AssemblyPath" }
if (-not (Test-Path -LiteralPath $HarnessPath -PathType Leaf)) { throw "FAIL: initialized production harness not found: $HarnessPath" }

if ($ExpectCreativeRegression) {
    & $HarnessPath --halfgiant-reach --rfm-dll $AssemblyPath --expect-creative-regression
}
else {
    & $HarnessPath --halfgiant-reach --rfm-dll $AssemblyPath
}
if ($LASTEXITCODE -ne 0) { throw "FAIL: initialized production harness exited $LASTEXITCODE" }

Write-Output "PASS: $script:checks unchanged Half-Giant quarry assertions; initialized production reach assertions ran in the supplied net10 harness. Gameplay remains a player check."
