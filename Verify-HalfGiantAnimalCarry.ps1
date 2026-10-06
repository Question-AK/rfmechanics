param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot 'bin/Release/Mods/rfmechanics.dll')
)

$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'src/HalfGiantAnimalCarryRules.cs')
$script:checks = 0
function Assert-True([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:checks++
}

$allow = [string[]]@('example:smallfauna')
$deny = [string[]]@('example:forbidden')
Assert-True ([rfmechanics.HalfGiantAnimalCarryRules]::IsEligible($true, $true, $true, $true, $true, $true, $true, $true, 1.452, 1.2, 1.452, 1.2, 'game:pig-eurasian-adult-elder-male', $allow, $deny)) 'Resolved elder-boar-sized tagged animal is eligible'
Assert-True (-not [rfmechanics.HalfGiantAnimalCarryRules]::IsEligible($true, $true, $true, $true, $true, $true, $true, $true, 1.453, 1.2, 1.452, 1.2, 'game:pig-eurasian-adult-elder-male', $allow, $deny)) 'Oversize volume is rejected'
Assert-True (-not [rfmechanics.HalfGiantAnimalCarryRules]::IsEligible($true, $true, $true, $true, $true, $true, $true, $true, 1.452, 1.21, 1.452, 1.2, 'game:pig-eurasian-adult-elder-male', $allow, $deny)) 'Oversize dimension is rejected'
Assert-True ([rfmechanics.HalfGiantAnimalCarryRules]::IsEligible($true, $true, $true, $true, $true, $true, $true, $true, 9, 9, 1.452, 1.2, 'example:smallfauna', $allow, $deny)) 'Namespaced allow override is honored'
Assert-True (-not [rfmechanics.HalfGiantAnimalCarryRules]::IsEligible($true, $true, $true, $true, $true, $true, $true, $true, 0.1, 0.1, 1.452, 1.2, 'example:forbidden', $allow, $deny)) 'Namespaced deny override wins'
Assert-True (-not [rfmechanics.HalfGiantAnimalCarryRules]::IsEligible($true, $true, $true, $true, $false, $true, $true, $true, 0.1, 0.1, 1.452, 1.2, 'game:pig', $allow, $deny)) 'Visibility and reach are required'
Assert-True (-not [rfmechanics.HalfGiantAnimalCarryRules]::IsEligible($true, $true, $true, $true, $true, $true, $true, $false, 0.1, 0.1, 1.452, 1.2, 'game:pig', $allow, $deny)) 'Mounted tethered or inventory-bearing animals are rejected'

$rules = [rfmechanics.HalfGiantAnimalCarryRules]
$admission = [rfmechanics.HalfGiantCarryAdmission]
$tagExempt = [string[]]@('drifter-')
foreach ($drifter in 'normal', 'deep', 'tainted', 'corrupt', 'nightmare', 'double-headed') {
    Assert-True ($rules::Admit($false, "drifter-$drifter", $tagExempt) -eq $admission::TagExempt) "Untagged drifter-$drifter is admitted by prefix"
}
Assert-True ($rules::Admit($false, 'bowtorn-surface', $tagExempt) -eq $admission::Refused) 'Bowtorn is refused'
Assert-True ($rules::Admit($false, 'shiver-surface', $tagExempt) -eq $admission::Refused) 'Shiver is refused'
Assert-True ($rules::Admit($false, 'wolf-eurasian-adult-male', $tagExempt) -eq $admission::Refused) 'Untagged non-drifter is refused'
Assert-True ($rules::Admit($false, 'drifter-normal', $null) -eq $admission::Refused) 'A cleared prefix list admits no drifter'
Assert-True ($rules::Admit($true, 'pig-eurasian-adult-female', $tagExempt) -eq $admission::Animal) 'Tagged animals keep animal admission'
Assert-True ($rules::CaptureReach($admission::TagExempt, 7.0, 3.0) -eq 3.0) 'Drifters use the shorter tag-exempt reach'
Assert-True ($rules::CaptureReach($admission::Animal, 7.0, 3.0) -eq 7.0) 'Animals keep the animal reach'

$configSource = Get-Content (Join-Path $PSScriptRoot 'src/RFMechanicsConfig.cs') -Raw
Assert-True ($configSource -match 'HalfGiantAnimalCarryTagExemptCodePathPrefixes \{ get; set; \} = new\[\] \{ "drifter-" \};') 'Tag-exempt prefixes default to drifters only'
Assert-True ($configSource -match 'HalfGiantAnimalCarryTagExemptReach \{ get; set; \} = 3\.0;' -and $configSource -match 'HalfGiantAnimalCarryReach \{ get; set; \} = 7\.0;') 'Default reaches are 3 blocks for drifters and 7 for animals'

$id = [guid]::NewGuid().ToString('N')
Assert-True ([rfmechanics.HalfGiantAnimalCarryRules]::IsSnapshotValid('EntityAgent', 'game:pig-eurasian-adult-elder-male', [byte[]](1,2,3), $id)) 'Serialized snapshot requires class code bytes and opaque identity'
Assert-True (-not [rfmechanics.HalfGiantAnimalCarryRules]::IsSnapshotValid('EntityAgent', 'game:pig', [byte[]](1), 'not-an-identity')) 'Malformed capture identity is rejected'
Assert-True (-not [rfmechanics.HalfGiantAnimalCarryRules]::CanRelease($true, $false, $true, $true, $false)) 'Blocked release preserves carried state'
Assert-True (-not [rfmechanics.HalfGiantAnimalCarryRules]::CanRelease($true, $true, $true, $true, $true)) 'Released capture identity rejects replay'
Assert-True (-not [rfmechanics.HalfGiantAnimalCarryRules]::ShouldConsumeCapture($false)) 'Failed capture or release does not consume carried state'
Assert-True ([rfmechanics.HalfGiantAnimalCarryRules]::ShouldConsumeCapture($true)) 'Successful spawn consumes carried state exactly once'

$carrySource = Get-Content (Join-Path $PSScriptRoot 'src/HalfGiantAnimalCarryModSystem.cs') -Raw
Assert-True ($carrySource.IndexOf('TryDeserialize(className, creatureCode, bytes, out Entity? entity)') -lt $carrySource.IndexOf('sapi.World.SpawnEntity(entity)')) 'Release deserializes before spawning'
Assert-True ($carrySource.IndexOf('releasedCaptureIdentities.Add(captureIdentity)') -gt $carrySource.IndexOf('sapi.World.SpawnEntity(entity)')) 'Replay identity is consumed only after spawn'
Assert-True ($carrySource.Contains('offhand.MarkDirty();') -and $carrySource.Contains('carrySlot.MarkDirty();')) 'Inventory mutations are marked dirty'

$useSource = $carrySource.Substring($carrySource.IndexOf('private TextCommandResult Use('), $carrySource.IndexOf('private TextCommandResult Capture(') - $carrySource.IndexOf('private TextCommandResult Use('))
$offhandRelease = $useSource.IndexOf('return Release(player, offhand, config);')
$mainHandRelease = $useSource.IndexOf('return Release(player, mainHand, config);')
Assert-True ($offhandRelease -ge 0 -and $mainHandRelease -gt $offhandRelease) 'Release checks the offhand before the main hand'
Assert-True ($useSource.Contains('player.InventoryManager.ActiveHotbarSlot')) 'Main-hand release uses the active hotbar slot'
Assert-True ($useSource.Contains('Capture(player, offhand, config)') -and -not $useSource.Contains('Capture(player, mainHand')) 'Capture stays offhand-only'

$captureSource = $carrySource.Substring($carrySource.IndexOf('private TextCommandResult Capture('), $carrySource.IndexOf('private TextCommandResult Release(') - $carrySource.IndexOf('private TextCommandResult Capture('))
$admitAt = $captureSource.IndexOf('HalfGiantAnimalCarryRules.Admit(')
$reachAt = $captureSource.IndexOf('CanSeeAndReach(player.Entity, target, reach)')
Assert-True ($admitAt -ge 0 -and $reachAt -gt $admitAt -and $captureSource.Contains('HalfGiantAnimalCarryRules.CaptureReach(admission,')) 'Capture reach follows the admission kind'
foreach ($guard in 'Claims.TryAccess(player, target.Pos.AsBlockPos', 'HasOwnerAccess(player, agent)', 'IsOrdinary(agent)', 'HalfGiantAnimalCarryDenyCodePathPrefixes', 'HalfGiantAnimalCarryRules.IsEligible(', 'TrySerialize(agent,') {
    $guardAt = $captureSource.IndexOf($guard)
    Assert-True ($guardAt -gt $admitAt) "Admitted drifters still pass $guard"
}

$itemJson = Get-Content (Join-Path $PSScriptRoot 'assets/rfmechanics/itemtypes/carriedanimal.json') -Raw
$storageMatch = [regex]::Match($itemJson, '(?m)^\s*storageFlags:\s*(\d+)\s*,')
Assert-True ($storageMatch.Success) 'Carried animal declares storage flags'
$storageFlags = [int]$storageMatch.Groups[1].Value
Assert-True (($storageFlags -band 1) -ne 0 -and ($storageFlags -band 256) -ne 0) 'Carried animal fits General and Offhand slots so X can flip it back'
Assert-True ($itemJson -match 'heldLeftTpIdleAnimation:\s*"holdinglanternlefthand"' -and $itemJson -match 'heldRightTpIdleAnimation:\s*"holdinglanternrighthand"') 'Carried animal has a hold pose in each hand'

$itemSource = Get-Content (Join-Path $PSScriptRoot 'src/ItemCarriedAnimal.cs') -Raw
Assert-True ($itemSource -match 'override void OnHeldAttackStart\([^)]*\)\s*\{\s*handling = EnumHandHandling\.PreventDefault;\s*\}') 'Held carried animal cannot attack'

$chickenVolume = 0.5 * 0.5 * 0.6
$boarVolume = 1.452
$chickenSpeed = $rules::ThrowSpeed($chickenVolume, 0.15, 0.45, 0.15)
$drifterSpeed = $rules::ThrowSpeed(0.6 * 0.6 * 1.3, 0.15, 0.45, 0.15)
$boarSpeed = $rules::ThrowSpeed($boarVolume, 0.15, 0.45, 0.15)
Assert-True ([math]::Abs($chickenSpeed - 0.45) -lt 1e-9) 'A chicken leaves the hand at full throw speed'
Assert-True ($chickenSpeed -gt $drifterSpeed -and $drifterSpeed -gt $boarSpeed) 'Throw speed falls as creature volume grows'
Assert-True ([math]::Abs($boarSpeed - 0.15) -lt 1e-9) 'A boar is held at the speed floor'
Assert-True ($rules::ThrowSpeed(0.01, 0.15, 0.45, 0.15) -le 0.45) 'Tiny creatures never exceed the base speed'
Assert-True ($rules::ThrowSpeed([double]::NaN, 0.15, 0.45, 0.15) -eq 0.15 -and $rules::ThrowSpeed(1, 0.15, 0, 0.15) -eq 0) 'Invalid volume uses the floor; a zero base speed throws nothing'
Assert-True ($rules::IsThrowReady(0.34, 0.35) -eq $false -and $rules::IsThrowReady(0.35, 0.35)) 'Throw requires the full windup'

Assert-True ([math]::Abs($rules::HeldScale(1.0, 2.1, 2.1) - (1.0 / 2.1)) -lt 1e-6) 'Held scale divides creature size by holder size'
Assert-True ([math]::Abs($rules::HeldScale(1.1, 2.1, 2.1) - (1.1 / 2.1)) -lt 1e-6) 'A larger-bodied boar keeps its own size factor'
Assert-True ([math]::Abs($rules::HeldScale(0, 0, 2.1) - (1.0 / 2.1)) -lt 1e-6) 'Stacks without stored sizes fall back to a size-1 creature in a halfgiant hand'
Assert-True ([math]::Abs($rules::HeldScale(1.0, [float]::NaN, 2.1) - (1.0 / 2.1)) -lt 1e-6) 'A NaN holder size falls back to the halfgiant default'

$hit = New-Object 'System.Collections.Generic.HashSet[long]'
Assert-True ($rules::ShouldHit($hit, 10, 1, 2, $true)) 'First contact with a creature hits'
Assert-True (-not $rules::ShouldHit($hit, 10, 1, 2, $true)) 'The same creature is not hit twice in one flight'
Assert-True ($rules::ShouldHit($hit, 11, 1, 2, $true)) 'A second creature is hit once'
Assert-True (-not $rules::ShouldHit($hit, 1, 1, 2, $true) -and -not $rules::ShouldHit($hit, 2, 1, 2, $true)) 'Neither the thrower nor the thrown creature is hit'
Assert-True (-not $rules::ShouldHit($hit, 12, 1, 2, $false) -and $rules::ShouldHit($hit, 12, 1, 2, $true)) 'A refused hit does not use up the target'
Assert-True (-not $rules::CanDamage($true, $true, $false, $true, $true) -and -not $rules::CanDamage($true, $true, $true, $false, $true)) 'Players need PvP and attackplayers'
Assert-True (-not $rules::CanDamage($false, $true, $true, $true, $false) -and $rules::CanDamage($false, $true, $false, $false, $true)) 'Creatures need attackcreatures only'
Assert-True ($rules::HitDamage($boarVolume, 3, 1, 6) -gt $rules::HitDamage($chickenVolume, 3, 1, 6)) 'Hit damage grows with thrown creature size'
Assert-True ($rules::HitDamage($chickenVolume, 3, 1, 6) -eq 1 -and $rules::HitDamage(100, 3, 1, 6) -eq 6) 'Hit damage stays within its minimum and maximum'
Assert-True (-not $rules::HasLanded($true, 100, 150) -and $rules::HasLanded($true, 150, 150) -and -not $rules::HasLanded($false, 4000, 150)) 'Landing needs ground or liquid after the first physics ticks'

$landing = [rfmechanics.HalfGiantThrowLanding]
Assert-True ($rules::Landing($true, $true, $false) -eq $landing::RemoveWithoutDrops) 'A hostile landing in a claim the thrower cannot build in is removed'
Assert-True ($rules::Landing($false, $true, $false) -eq $landing::Keep) 'Animals landing in a foreign claim are unaffected'
Assert-True ($rules::Landing($true, $true, $true) -eq $landing::Keep -and $rules::Landing($true, $false, $false) -eq $landing::Keep) 'Own-claim landings and the disabled rule keep hostiles'

$throwSource = $carrySource.Substring($carrySource.IndexOf('internal void Throw('), $carrySource.IndexOf('private void TickThrownCreatures(') - $carrySource.IndexOf('internal void Throw('))
$throwSpawnAt = $throwSource.IndexOf('sapi.World.SpawnEntity(entity)')
Assert-True ($throwSource.IndexOf('TryDeserialize(className, creatureCode, bytes, out Entity? entity)') -lt $throwSpawnAt) 'Throw deserializes before spawning'
Assert-True ($throwSource.IndexOf('releasedCaptureIdentities.Contains(captureIdentity)') -lt $throwSpawnAt -and $throwSource.IndexOf('releasedCaptureIdentities.Add(captureIdentity)') -gt $throwSpawnAt) 'Throw checks the replay guard and consumes the identity only after spawn'
Assert-True ($throwSource.IndexOf('carrySlot.TakeOut(1)') -gt $throwSpawnAt -and $throwSource.Substring(0, $throwSpawnAt).IndexOf('TakeOut') -lt 0) 'Throw takes the stack only after a successful spawn'
$catchAt = $throwSource.IndexOf('catch (Exception error)')
$catchReturnAt = $throwSource.IndexOf('return;', [math]::Max($catchAt, 0))
Assert-True ($catchAt -gt $throwSpawnAt -and $catchReturnAt -gt $catchAt -and $throwSource.IndexOf('carrySlot.TakeOut(1)') -gt $catchReturnAt) 'A failed spawn returns before consuming the stack'
Assert-True ($throwSource.Contains('carrySlot != player.InventoryManager.ActiveHotbarSlot')) 'The server throws only from the main hand'
Assert-True ($throwSource.Contains('EntityProjectileBase.GetProjectileDirection(')) 'Aim uses the vanilla projectile direction'
Assert-True ($carrySource -match 'creature\.Die\(EnumDespawnReason\.Removed\)' -and $carrySource -notmatch 'Die\(EnumDespawnReason\.Death') 'Claim removal despawns without death drops'
Assert-True ($carrySource.Contains('ItemCarriedAnimal.CreatureSizeKey') -and $carrySource.Contains('ItemCarriedAnimal.HolderSizeKey')) 'Capture stores creature and holder sizes'
$releaseSource = $carrySource.Substring($carrySource.IndexOf('private TextCommandResult Release('), $carrySource.IndexOf('internal void Throw(') - $carrySource.IndexOf('private TextCommandResult Release('))
Assert-True ($releaseSource.Contains('entity.Pos.Motion.Set(0, 0, 0);') -and -not $releaseSource.Contains('thrownCreatures')) 'Release still places the creature at rest, untracked'

Assert-True ($itemSource.Contains('slot != byEntity.RightHandItemSlot') -and $itemSource.Contains('api.Side == EnumAppSide.Client')) 'The item aims from the main hand; the client only animates'
Assert-True ($itemSource.Contains('IsThrowReady(secondsUsed, ThrowWindupSeconds)') -and $itemSource -match 'ThrowWindupSeconds = 0\.35f') 'The item enforces the 0.35 s windup'
Assert-True ($itemSource -notmatch 'TakeOut') 'The client-side item never removes the stack'

if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) { throw "FAIL: production assembly not found: $AssemblyPath" }
Write-Output "PASS: $script:checks Half-Giant animal-carry eligibility, drifter admission and reach, snapshot identity, preservation, replay, hand-swap, release-order, throw speed, held scale, once-per-target hit, claim landing and throw consume-order assertions passed. Native entity serialization, collision and flight remain player/review checks."
