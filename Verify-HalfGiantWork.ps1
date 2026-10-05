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
$survivalOverrideActive = $false
$survivalRange = [rfmechanics.HalfGiantReachRules]::ResolveManagedPickingRange(4.5, 4.5, 9.45, $false, 9.45, $true, $true, $true, [ref]$survivalOverrideActive)
Assert-Near $survivalRange 9.45 'Production reach state applies the Half-Giant override in Survival'
Assert-True $survivalOverrideActive 'Production reach state records ownership after applying the override'
$creativeOverrideActive = $true
$creativeRange = [rfmechanics.HalfGiantReachRules]::ResolveManagedPickingRange($survivalRange, 4.5, 9.45, $true, 9.45, $true, $true, $false, [ref]$creativeOverrideActive)
Assert-Near $creativeRange 4.5 'Survival-to-Creative restores the owned Half-Giant override baseline'
Assert-True (-not $creativeOverrideActive) 'Survival-to-Creative clears Half-Giant override ownership'
$customCreativeOverrideActive = $true
$customCreativeRange = [rfmechanics.HalfGiantReachRules]::ResolveManagedPickingRange(16, 4.5, 9.45, $true, 9.45, $true, $true, $false, [ref]$customCreativeOverrideActive)
Assert-Near $customCreativeRange 16 'Creative custom picking range is preserved when it is not the owned override'
Assert-True (-not $customCreativeOverrideActive) 'Creative custom range clears stale Half-Giant override ownership'
$raceChangeOverrideActive = $true
$raceChangeRange = [rfmechanics.HalfGiantReachRules]::ResolveManagedPickingRange(9.45, 4.5, 9.45, $true, 9.45, $true, $false, $true, [ref]$raceChangeOverrideActive)
Assert-Near $raceChangeRange 4.5 'Race change restores the owned Half-Giant override baseline'
Assert-True (-not $raceChangeOverrideActive) 'Race change clears Half-Giant override ownership'
$disabledOverrideActive = $true
$disabledRange = [rfmechanics.HalfGiantReachRules]::ResolveManagedPickingRange(9.45, 4.5, 9.45, $true, 9.45, $false, $true, $true, [ref]$disabledOverrideActive)
Assert-Near $disabledRange 4.5 'Disabling reach restores the owned Half-Giant override baseline'
Assert-True (-not $disabledOverrideActive) 'Disabling reach clears Half-Giant override ownership'
$recreatedOverrideActive = $true
$recreatedRange = [rfmechanics.HalfGiantReachRules]::ResolveManagedPickingRange(4.5, 4.5, 9.45, $true, 9.45, $true, $true, $true, [ref]$recreatedOverrideActive)
Assert-Near $recreatedRange 9.45 'Persisted Half-Giant baseline reapplies after a recreated Survival entity'
Assert-True $recreatedOverrideActive 'Recreated Survival entity retains Half-Giant override ownership'
$quarryPatch = Get-Content (Join-Path $PSScriptRoot 'src/HalfGiantQuarryPatch.cs') -Raw
Assert-True ($quarryPatch.Contains('nameof(Block.OnGettingBroken)')) 'Quarry uses empty-hand Block.OnGettingBroken seam'
Assert-True (-not $quarryPatch.Contains('GetMiningSpeed')) 'Quarry does not broaden held-item mining speed'
$quarrySystem = Get-Content (Join-Path $PSScriptRoot 'src/HalfGiantQuarryModSystem.cs') -Raw
Assert-True ($quarrySystem.Contains('DidBreakBlock += OnDidBreakBlock')) 'Satiety charge observes successful server breaks'
Assert-True ($quarrySystem.Contains('GetBlock(oldBlockId)')) 'Satiety charge validates original broken block identity'
$reachBehavior = Get-Content (Join-Path $PSScriptRoot 'src/HalfGiantReachBehavior.cs') -Raw
Assert-True ($reachBehavior.Contains('BroadcastPlayerData')) 'Server reach changes broadcast player data'
Assert-True ($reachBehavior.Contains('ResolveManagedPickingRange')) 'Reach behavior uses the production state-transition resolver'
Write-Output "PASS: $script:checks Half-Giant reach/quarry assertions; gameplay timing, drops, range authority and mode transitions remain player checks."
