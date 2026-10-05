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
Assert-True ([rfmechanics.HalfGiantReachRules]::ShouldOverride($true, $true, $true)) 'Reach enable and survival gate'
Assert-Near ([rfmechanics.HalfGiantReachRules]::ResolvePickingRange(4.5, 9.45, $true, $true, $true)) 9.45 'Half-Giant survival reach target'
Assert-Near ([rfmechanics.HalfGiantReachRules]::ResolvePickingRange(4.5, 9.45, $false, $true, $true)) 4.5 'Disabled reach restores baseline'
Assert-Near ([rfmechanics.HalfGiantReachRules]::ResolvePickingRange(4.5, 9.45, $true, $false, $true)) 4.5 'Race change restores baseline'
Assert-Near ([rfmechanics.HalfGiantReachRules]::ResolvePickingRange(4.5, 9.45, $true, $true, $false)) 4.5 'Non-survival preserves baseline'
$quarryPatch = Get-Content (Join-Path $PSScriptRoot 'src/HalfGiantQuarryPatch.cs') -Raw
Assert-True ($quarryPatch.Contains('nameof(Block.OnGettingBroken)')) 'Quarry uses empty-hand Block.OnGettingBroken seam'
Assert-True (-not $quarryPatch.Contains('GetMiningSpeed')) 'Quarry does not broaden held-item mining speed'
$quarrySystem = Get-Content (Join-Path $PSScriptRoot 'src/HalfGiantQuarryModSystem.cs') -Raw
Assert-True ($quarrySystem.Contains('DidBreakBlock += OnDidBreakBlock')) 'Satiety charge observes successful server breaks'
Assert-True ($quarrySystem.Contains('GetBlock(oldBlockId)')) 'Satiety charge validates original broken block identity'
$reachBehavior = Get-Content (Join-Path $PSScriptRoot 'src/HalfGiantReachBehavior.cs') -Raw
Assert-True ($reachBehavior.Contains('BroadcastPlayerData')) 'Server reach changes broadcast player data'
Assert-True ($reachBehavior.Contains('CurrentGameMode != EnumGameMode.Survival')) 'Creative and other non-survival ranges are preserved'
Write-Output "PASS: $script:checks Half-Giant reach/quarry assertions; gameplay timing, drops, range authority and mode transitions remain player checks."
