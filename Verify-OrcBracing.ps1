# Offline checks of the actual production rules. No game/server, test executable,
# world, installed config or profile is created or opened.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'src/OrcBracingRules.cs')
$script:checks = 0
function Assert-True([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:checks++
}
function Assert-Near([double]$Actual, [double]$Expected, [string]$Name, [double]$Tolerance = 0.000001) {
    Assert-True ([Math]::Abs($Actual - $Expected) -le $Tolerance) "$Name ($Actual vs $Expected)"
}
$tuning = New-Object rfmechanics.OrcBraceTuning
$tuning.Normalize()

# Independent expected arithmetic: flat then percentage, weapon tier losses.
Assert-Near ([rfmechanics.OrcProtectionRules]::Protect(10,0,1)) 5.85 'T1 baseline, tier-zero weapon'
Assert-Near ([rfmechanics.OrcProtectionRules]::Protect(10,1,1)) 6.0282 'T1 baseline, tier-one weapon'
Assert-Near ([rfmechanics.OrcProtectionRules]::Protect(10,2,1)) 6.702 'T1 skin, tier-two weapon; negative flat clipped'
Assert-Near ([rfmechanics.OrcProtectionRules]::Protect(10,2,3)) 2.438545 'T3 skin, tier-two weapon'
Assert-Near ([rfmechanics.OrcProtectionRules]::Protect(10,3,3)) 2.678287968 'T3 skin, tier-three weapon'
Assert-Near ([rfmechanics.OrcProtectionRules]::Protect(0,2,3)) 0 'Shield fully blocked: skin cannot add damage'
foreach ($damage in @(0.01,0.1,1,5,10,100)) {
    foreach ($weapon in -1..25) {
        $skin = [rfmechanics.OrcProtectionRules]::Protect($damage,$weapon,1)
        $brace = [rfmechanics.OrcProtectionRules]::Protect($damage,$weapon,3)
        Assert-True ($skin -ge 0 -and $skin -le $damage) 'Baseline never amplifies a hit'
        Assert-True ($brace -ge 0 -and $brace -le $skin + 1e-12) 'Braced profile never worse than baseline'
    }
}
# Multiple simultaneous bearings, independent of movement or attacker selection.
Assert-True ([rfmechanics.OrcProtectionRules]::InFront(0,0,-1,120)) 'Facing north, north attacker'
Assert-True (-not [rfmechanics.OrcProtectionRules]::InFront(0,0,1,120)) 'Facing north, south attacker'
Assert-True (-not [rfmechanics.OrcProtectionRules]::InFront(0,1,0,120)) 'Facing north, east attacker'
Assert-True ([rfmechanics.OrcProtectionRules]::InFront([Math]::PI,0,1,120)) 'Turn south, south attacker'
Assert-True (-not [rfmechanics.OrcProtectionRules]::InFront([Math]::PI,0,-1,120)) 'Turn south, north attacker'
foreach ($angle in @(-60,60)) {
    $rad = $angle * [Math]::PI / 180
    Assert-True ([rfmechanics.OrcProtectionRules]::InFront(0,[Math]::Sin($rad),-[Math]::Cos($rad),120)) 'Arc inclusive edge'
}
foreach ($angle in @(-60.1,60.1,90,180)) {
    $rad = $angle * [Math]::PI / 180
    Assert-True (-not [rfmechanics.OrcProtectionRules]::InFront(0,[Math]::Sin($rad),-[Math]::Cos($rad),120)) 'Outside arc'
}
Assert-True (-not [rfmechanics.OrcProtectionRules]::InFront(0,0,0,120)) 'Missing horizontal direction'
Assert-True (-not [rfmechanics.OrcProtectionRules]::InFront([double]::NaN,0,-1,120)) 'Invalid facing'
Assert-True ([rfmechanics.OrcProtectionRules]::InFront(0,0,-0.4,120)) 'Front projectile moving south uses reverse velocity'
Assert-True (-not [rfmechanics.OrcProtectionRules]::InFront(0,0,0.4,120)) 'Rear projectile moving north'

foreach ($example in @(@(10,16.6666666667),@(20,46.6666666667),@(30,90),@(60,240))) {
    $state = New-Object rfmechanics.OrcBraceState
    $state.Active = $true
    [double]$food = 1500
    Assert-True (-not $state.Advance($example[0],[ref]$food,1500,$tuning)) 'Enough food stays active'
    Assert-Near (1500 - $food) $example[1] "Cost after $($example[0]) seconds"
}
$state = New-Object rfmechanics.OrcBraceState
$state.Active = $true
[double]$food = 1500
$null = $state.Advance(30,[ref]$food,1500,$tuning)
$state.Active = $false
$null = $state.Advance(15,[ref]$food,1500,$tuning)
Assert-Near $food 1410 'Release stops drain'
Assert-Near $state.Exertion 0.75 '15 seconds recovers quarter of full exertion'
$state.Active = $true
Assert-Near ($tuning.Rate($state.Exertion)) 4 'Early restart retains expensive rate'
$null = $state.Advance(5,[ref]$food,1500,$tuning)
Assert-Near $food 1388.33333333333 'Restart cost integrates remaining exertion'
$state.Active = $false
$null = $state.Advance(60,[ref]$food,1500,$tuning)
Assert-Near $state.Exertion 0 'Complete recovery'

# Tick partition and toggle accounting: no reset on instantaneous release/restart.
$whole = New-Object rfmechanics.OrcBraceState
$sliced = New-Object rfmechanics.OrcBraceState
$whole.Active = $sliced.Active = $true
[double]$a = 1500; [double]$b = 1500
$null = $whole.Advance(45,[ref]$a,1500,$tuning)
for ($i=0; $i -lt 450; $i++) {
    $null = $sliced.Advance(0.1,[ref]$b,1500,$tuning)
    $sliced.Active = $false
    $null = $sliced.Advance(0,[ref]$b,1500,$tuning)
    $sliced.Active = $true
}
Assert-Near $b $a '450 rapid reactivations do not reset cost'
Assert-Near $sliced.Exertion $whole.Exertion 'Tick-size-independent escalation'

$state = New-Object rfmechanics.OrcBraceState
Assert-True (-not $state.CanStart(450,1500,$tuning)) 'Cannot start at cutoff'
Assert-True (-not $state.CanStart(479.9,1500,$tuning)) 'Restart hysteresis'
Assert-True ($state.CanStart(480,1500,$tuning)) 'May start at 32 percent'
$state.Active = $true
[double]$food = 480
Assert-True ($state.Advance(30,[ref]$food,1500,$tuning)) 'Low-food forced release'
Assert-Near $food 450 'Food debit stops exactly at floor'
Assert-True (-not $state.Active) 'Forced release clears active flag'
Assert-True (-not $state.CanStart($food,1500,$tuning)) 'Forced release cannot immediately reactivate'
Assert-True ($state.Exertion -gt 0) 'Forced release retains residual exertion'
$null = $state.Advance(60,[ref]$food,1500,$tuning)
Assert-Near $food 450 'Recovery consumes no food'
Assert-Near $state.Exertion 0 'Recovery after forced release'
$state.Active = $true
$food = 200
Assert-True ($state.Advance(0.1,[ref]$food,1500,$tuning)) 'Already below cutoff releases'
Assert-Near $food 200 'Cannot create food by clamping up to floor'
$state.Active = $true
$food = 1500
Assert-True ($state.Advance(600,[ref]$food,1500,$tuning)) 'Long tick forced release'
Assert-Near $food 450 'Long tick cannot overdraw'
Assert-Near $state.Exertion 0 'Long tick recovers after exact exhaustion time'
Assert-True (-not $state.CanStart(100,0,$tuning)) 'Missing hunger capacity rejected'
Assert-True (-not $state.CanStart([double]::NaN,1500,$tuning)) 'Invalid food rejected'
$tuning.Arc = [double]::NaN
$tuning.InitialRate = -1
$tuning.MaximumRate = 0
$tuning.RampSeconds = 0
$tuning.RecoverySeconds = [double]::PositiveInfinity
$tuning.Normalize()
Assert-Near $tuning.Arc 120 'Nonfinite arc fallback'
Assert-Near $tuning.InitialRate 0.1 'Positive initial drain enforced'
Assert-True ($tuning.MaximumRate -ge $tuning.InitialRate) 'Cap cannot be below starting drain'
Assert-Near $tuning.RampSeconds 1 'Zero ramp sanitized'
Assert-Near $tuning.RecoverySeconds 60 'Nonfinite recovery fallback'
Write-Output "PASS: $script:checks production-rule assertions. No gameplay or Harmony runtime claim."
