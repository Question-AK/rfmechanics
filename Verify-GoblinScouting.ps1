# Offline Goblin scouting and free-hand climbing production-rule checks; no game/server or test application.
$ErrorActionPreference = 'Stop'
# PowerShell 5.1 cannot compile nullable annotations or MathF, so this runs the production rule with equivalent syntax.
$testConfig = @'
namespace rfmechanics {
    public class RFMechanicsConfig {
        public string[] GoblinScoutingDrifterFamilyCodes { get; set; }
        public string[] GoblinScoutingAnimalFamilyCodes { get; set; }
        public double GoblinScoutingCrouchedDarkGroundFactor { get; set; }
        public double GoblinScoutingEmptyHandWallClimbFactor { get; set; }
        public double GoblinScoutingOneHandWallClimbFactor { get; set; }
        public double GoblinScoutingStandingFactor { get; set; }
        public double GoblinScoutingSprintingFactor { get; set; }
        public double GoblinScoutingAnimalFactor { get; set; }

        public RFMechanicsConfig() {
            GoblinScoutingDrifterFamilyCodes = new[] { "drifter", "shiver", "bowtorn" };
            GoblinScoutingAnimalFamilyCodes = new[] { "bear", "hyena", "wolf" };
            GoblinScoutingCrouchedDarkGroundFactor = 0.15;
            GoblinScoutingEmptyHandWallClimbFactor = 0.25;
            GoblinScoutingOneHandWallClimbFactor = 0.35;
            GoblinScoutingStandingFactor = 0.35;
            GoblinScoutingSprintingFactor = 0.50;
            GoblinScoutingAnimalFactor = 0.70;
        }
    }
}
'@
$rules = (Get-Content (Join-Path $PSScriptRoot 'src/GoblinScoutingRules.cs') -Raw) -replace 'string\[\]\?', 'string[]' -replace 'string\?', 'string' -replace 'System.MathF.Max', 'System.Math.Max'
Add-Type -TypeDefinition "$testConfig`n$rules"
$script:checks = 0
function Assert-True([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:checks++
}
function Assert-Near([double]$Actual, [double]$Expected, [string]$Name) {
    Assert-True ([Math]::Abs($Actual - $Expected) -lt 0.000001) $Name
}
function Resolve([bool]$Drifter, [bool]$Animal, [bool]$Dark, [bool]$Light, [bool]$Targeted, [bool]$Contact, [bool]$Sneak, [bool]$Ground, [bool]$Climbing, [int]$Hands, [bool]$Sprint, $Config) {
    $state = [rfmechanics.GoblinScoutingState]::None
    $factor = [rfmechanics.GoblinScoutingRules]::ResolveFactor($Drifter, $Animal, $Dark, $Light, $Targeted, $Contact, $Sneak, $Ground, $Climbing, $Hands, $Sprint, $Config, [ref]$state)
    return @($factor, $state)
}

$cfg = [rfmechanics.RFMechanicsConfig]::new()
$configSource = Get-Content (Join-Path $PSScriptRoot 'src/RFMechanicsConfig.cs') -Raw
foreach ($setting in @(
    'GoblinScoutingDrifterFamilyCodes { get; set; } = new[] { "drifter", "shiver", "bowtorn" };',
    'GoblinScoutingAnimalFamilyCodes { get; set; } = new[] { "bear", "hyena", "wolf" };',
    'GoblinScoutingCrouchedDarkGroundFactor { get; set; } = 0.15;',
    'GoblinScoutingEmptyHandWallClimbFactor { get; set; } = 0.25;',
    'GoblinScoutingOneHandWallClimbFactor { get; set; } = 0.35;',
    'GoblinScoutingStandingFactor { get; set; } = 0.35;',
    'GoblinScoutingSprintingFactor { get; set; } = 0.50;',
    'GoblinScoutingAnimalFactor { get; set; } = 0.70;'
)) {
    Assert-True ($configSource.Contains($setting)) "Production config default $setting"
}
foreach ($code in @('drifter-normal', 'drifter-deep', 'drifter-tainted', 'drifter-corrupt', 'drifter-nightmare', 'drifter-double-headed', 'shiver-surface', 'shiver-deep', 'shiver-tainted', 'shiver-corrupt', 'shiver-nightmare', 'shiver-stilt', 'shiver-bellhead', 'shiver-deepsplit', 'bowtorn-surface', 'bowtorn-deep', 'bowtorn-tainted', 'bowtorn-corrupt', 'bowtorn-nightmare', 'bowtorn-gearfoot')) {
    Assert-True ([rfmechanics.GoblinScoutingRules]::MatchesFamily($code, $cfg.GoblinScoutingDrifterFamilyCodes)) "Observed drifter-like variant $code is covered"
}
Assert-True (-not [rfmechanics.GoblinScoutingRules]::MatchesFamily('drifterish', $cfg.GoblinScoutingDrifterFamilyCodes)) 'Family match remains narrow'
foreach ($code in @('wolf-eurasian-adult-male', 'hyena-spotted-adult-female', 'bear-black-adult')) {
    Assert-True ([rfmechanics.GoblinScoutingRules]::MatchesFamily($code, $cfg.GoblinScoutingAnimalFamilyCodes)) "Known animal variant $code is covered"
}

$result = Resolve $true $false $true $false $false $false $true $true $false 2 $false $cfg
Assert-Near $result[0] 0.15 'Crouched dark ground is the best drifter-like concealment'
Assert-True ($result[1] -eq [rfmechanics.GoblinScoutingState]::CrouchedDarkGround) 'Crouch wins state ordering over wall overlap'
$result = Resolve $true $false $true $false $false $false $false $false $true 2 $true $cfg
Assert-Near $result[0] 0.25 'Empty-hand wall climb remains below crouched-ground concealment'
Assert-True ($result[1] -eq [rfmechanics.GoblinScoutingState]::EmptyHandWallClimb) 'Climbing wins state ordering over sprinting'
$result = Resolve $true $false $true $false $false $false $false $false $true 1 $false $cfg
Assert-Near $result[0] 0.35 'One-hand wall climb uses the standing trial factor'
$result = Resolve $true $false $true $false $false $false $false $true $false 2 $true $cfg
Assert-Near $result[0] 0.50 'Sprinting is less concealed than standing'
$result = Resolve $true $false $true $false $false $false $false $true $false 2 $false $cfg
Assert-Near $result[0] 0.35 'Standing dark ground trial factor'
$result = Resolve $true $false $true $true $false $false $true $true $false 2 $false $cfg
Assert-Near $result[0] 1 'Emitting active or offhand light disables concealment'
$result = Resolve $true $false $false $false $false $false $true $true $false 2 $false $cfg
Assert-Near $result[0] 1 'Daylight excludes dark-only concealment'
$result = Resolve $true $false $true $false $true $false $true $true $false 2 $false $cfg
Assert-Near $result[0] 1 'Active target stays revealed'
$result = Resolve $true $false $true $false $false $true $true $true $false 2 $false $cfg
Assert-Near $result[0] 1 'Close contact stays revealed'
$result = Resolve $false $true $true $false $false $false $true $true $false 2 $false $cfg
Assert-Near $result[0] 0.70 'Known animals receive weaker concealment'
$result = Resolve $false $false $true $false $false $false $true $true $false 2 $false $cfg
Assert-Near $result[0] 1 'Unrelated targets remain unchanged'

$climbing = Get-Content (Join-Path $PSScriptRoot 'src/GoblinClimbingPatch.cs') -Raw
Assert-True ($climbing.Contains('climbDownSpeed * dt * 60f * motionFactor')) 'Hand factor applies to ascent'
Assert-True ($climbing.Contains('climbUpSpeed * motionFactor')) 'Hand factor applies to descent'
Assert-True ($climbing.Contains('ForgetFreeHandGrip')) 'Zero hands release an existing wall grip'

# SQ-71 guards the first custom Motion.Y write against the complete native eligibility map.
# This checker cannot invoke native ApplyTests; SQ-87 provides its independent production oracle.
$motionMethod = [regex]::Match($climbing, 'public static void MotionAndCollisionPostfix[\s\S]*?(?=public static void ApplyTestsPostfix)').Value
Assert-True ($motionMethod.Contains('OnNativeLadder(__instance, entity!, pos, controls)')) 'MotionAndCollision consults native ladder authority with current behavior and controls'
$ladderCallIndex = $motionMethod.IndexOf('OnNativeLadder(')
$gripCallIndex = $motionMethod.IndexOf('TryGetGrip(')
Assert-True ($ladderCallIndex -ge 0 -and $gripCallIndex -gt $ladderCallIndex) 'Native ladder check runs before any wall grip or motion write, not as a guard after'
Assert-True (-not $motionMethod.Contains('if (controls.IsClimbing)')) 'Native ladder check does not gate on the stale per-tick IsClimbing flag'
$ladderMethod = [regex]::Match($climbing, 'private static bool OnNativeLadder[\s\S]*?(?=\n        private static GoblinClimbFilter)').Value
Assert-True ($ladderMethod.Contains('entity.Properties.CanClimb')) 'Native ladder authority keeps the CanClimb gate'
Assert-True ($ladderMethod.Contains('entity.Properties.CanClimbAnywhere && entity.Alive')) 'Native ladder authority uses live CanClimbAnywhere eligibility'
Assert-True ($ladderMethod.Contains('BlockLayersAccess.Default') -and $ladderMethod.Contains('BlockLayersAccess.Solid')) 'Native ladder authority selects default or solid layers'
Assert-True ($ladderMethod.Contains('int x = (int)pos.X;') -and $ladderMethod.Contains('int baseY = (int)pos.Y;') -and $ladderMethod.Contains('int z = (int)pos.Z;')) 'Native ladder authority truncates coordinates toward zero'
Assert-True ($ladderMethod.Contains('CollisionBox.Y2') -and $ladderMethod.Contains('Math.Ceiling')) 'Native ladder authority scans the native collision height'
Assert-True ($ladderMethod.Contains('new Cuboidd().SetAndTranslate')) 'Native ladder authority uses the translated collision box'
Assert-True ($ladderMethod.Contains('.IsClimbable(')) 'Native ladder authority queries the position-aware block predicate'
Assert-True ($ladderMethod.Contains('.ShortestDistanceFrom(box, scanPos) < touchDistance')) 'Native ladder authority requires strict collision touch distance'
Assert-True ($ladderMethod.Contains('.IterateHorizontalOffsets(offset)') -and $ladderMethod.Contains('controls.IsStepping')) 'Native ladder authority uses native neighbor order only when not stepping'
Assert-True ($ladderMethod.Contains('box.Y2 <= stepThreshold + fractionalY')) 'Native ladder authority excludes grounded short neighbor boxes'
Assert-True (-not $ladderMethod.Contains('IsClimbing =') -and -not $ladderMethod.Contains('ClimbingOnFace =') -and -not $ladderMethod.Contains('.Motion')) 'Native ladder authority remains a pure pre-write query'
Assert-True ($climbing.Contains('if (controls.IsClimbing) { corners.Forget(entity!); return; }')) 'ApplyTests corner-hygiene cleanup is unchanged by the write-seam fix'
$patch = Get-Content (Join-Path $PSScriptRoot 'src/GoblinScoutingPatch.cs') -Raw
Assert-True ($patch.Contains('AiTaskBaseTargetable), "CanSensePlayer"')) 'Classic targetable AI consumer is patched'
Assert-True ($patch.Contains('AiTaskBaseTargetableR), "GetDetectionRangeMultiplier"')) 'Revised targetable AI consumer is patched'
Assert-True ($patch.Contains('GetLightHsv') -and $patch.Contains('RightHandItemSlot') -and $patch.Contains('LeftHandItemSlot')) 'Both held slots use emitted-light API'
Assert-True (-not $patch.Contains('Stats.Set("animalSeekingRange"')) 'Scouting does not double-apply animalSeekingRange'
Assert-True ($patch.Contains('GetBlended("animalSeekingRange")')) 'Scouting composes the existing seeking trait once at the consumer'
Assert-True (-not $patch.Contains('tunneling')) 'Scouting leaves tunnel speed source separate'
$movement = Get-Content (Join-Path $PSScriptRoot 'src/RFGoblinScoutingBehavior.cs') -Raw
Assert-True ($movement.Contains('"goblinscouting"') -and -not $movement.Contains('"tunneling"')) 'Dark sneak speed has its own movement-stat source'
Assert-True ($movement.Contains('player.Controls.Sneak') -and $movement.Contains('HasEmittingHeldLight')) 'Dark sneak speed requires sneaking and no emitted held light'
Assert-True ($movement.Contains('EnumAppSide.Server')) 'Dark sneak speed remains server authoritative'
Assert-True ($patch.Contains('!IsGoblin')) 'Other races are neutral'
Write-Output "PASS: $script:checks Goblin scouting/free-hand production-rule assertions; gameplay remains a player check."
