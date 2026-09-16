# Offline checks of production arithmetic; no game/server or standalone application.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'src/OrcMetabolismFeedbackRules.cs')
$script:checks = 0
function Assert-Near([double]$Actual, [double]$Expected, [string]$Name) {
    if ([double]::IsNaN($Actual) -or [Math]::Abs($Actual - $Expected) -gt 0.000001) { throw "$Name : $Actual != $Expected" }
    $script:checks++
}
function Assert-True([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw $Name }
    $script:checks++
}
# Independently specified curve examples, boundaries and monotonicity.
foreach ($case in @(@(1,0),@(0.5,0),@(0.4,0.022360679775),@(0.3,0.063245553203),
    @(0.25,0.088388347648),@(0.2,0.116189500386),@(0.1,0.1788854382),@(0,0.25))) {
    Assert-Near (0.25 * [rfmechanics.OrcMetabolismFeedbackRules]::Curve($case[0],0.5,1.5)) $case[1] "Speed at satiety $($case[0])"
}
$last = 0
foreach ($i in 0..100) {
    $value = [rfmechanics.OrcMetabolismFeedbackRules]::Curve(1-$i/100.0,0.5,1.5)
    Assert-True ($value -ge $last -and $value -le 1) 'Hunger curve monotonic and bounded'
    $last = $value
}
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Curve([double]::NaN,0.5,1.5)) 0 'Invalid food cannot grant speed'

# Actual net change, not the reason for expenditure. Growth can cancel consumption.
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::LossPerHour(0.5,0.51,0.1)) 0 'Growth: no loss smoke'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::LossPerHour(0.5,0.5,0.1)) 0 'Stable, including gain offsetting consumption: no smoke'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::LossPerHour(0.5,0.49975,0.1)) 0.0025 'Ordinary slow loss'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::LossPerHour(0.5,0.488,0.1)) 0.12 'Rapid loss'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::LossPerHour(0.5,0,0)) 0 'No initialized interval: no smoke'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::LossPerHour(0.5,0,-1)) 0 'Calendar discontinuity: no smoke'

# Deliberate motion versus obstacles, knockback, rides, reverse/sideways displacement.
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Exertion(0,0,0.5,0,1,$true,$true,0.35)) 0 'Blocked input is free'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Exertion(0,1,0.5,0,1,$false,$true,0.35)) 0 'Idle/pushed/riding/ineligible sample is free'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Exertion(0,-1,0.5,0,1,$true,$true,0.35)) 0 'Reverse displacement is free'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Exertion(1,0,0.5,0,1,$true,$true,0.35)) 0 'Sideways displacement is free'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Exertion(0,1,0.5,0,1,$true,$true,0.35)) 1 'Sprinting cost'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Exertion(0,1,0.5,0,1,$true,$false,0.35)) 0.35 'Ordinary movement reduced cost'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Exertion(0,50,0.5,0,1,$true,$true,0.35)) 0 'Teleport-sized sample is free'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Exertion(0,0.5,0.5,0,1,$false,$true,0.35)) 0 'Rejected short teleport/push interval is free'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Exertion(0,1,3,0,1,$true,$true,0.35)) 0 'Stale interval is free'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Debt(0.6,1,1,0)) 0 'Idle Frenzy adds no debt'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Debt(0.6,1,1,0.35)) 0.21 'Peak walking hourly cost'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Debt(0.6,1,1,1)) 0.6 'Peak sprint hourly cost'
$curve = [rfmechanics.OrcMetabolismFeedbackRules]::Curve(0.2,0.5,1.5)
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Debt(0.6,$curve,1,1)) 0.278854800927 '20% food sprint cost per game hour'
Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::Debt(0.6,$curve,1,0.35)) 0.097599180324 '20% food walking cost per game hour'

foreach ($case in @(@(100,0),@(76,0),@(75,1),@(50,2),@(25,3),@(0,3))) {
    Assert-Near ([rfmechanics.OrcMetabolismFeedbackRules]::DepthBand($case[0],100)) $case[1] 'Quarter-depth bands'
}
# Cumulative source invariants: these are not substitutes for in-game verification.
$oresong = @(git diff 241faa8 -- src/DwarfOreSongClient.cs src/DwarfOreSongModSystem.cs src/DwarfOreSongShared.cs src/DwarfOreSongIndex.cs)
Assert-True ($oresong.Count -eq 0) 'All Oresong source unchanged'
$protection = @(git diff 241faa8 -- src/OrcBracingRules.cs src/OrcNaturalProtectionPatch.cs src/BandBehavior.cs src/ThewDebtRepayPatch.cs)
Assert-True ($protection.Count -eq 0) 'Bracing protection/body/debt repayment calculations preserved'
Write-Output "PASS: $script:checks arithmetic and preservation checks. Rendering, networking and gameplay remain player checks."
