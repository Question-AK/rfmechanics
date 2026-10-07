param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot 'bin/Release/Mods/rfmechanics.dll'),
    # The environment default lets a fixed verifier command line run the harness without editing it.
    [string]$HarnessPath = $env:RFM_HALFGIANT_HARNESS,
    [switch]$ExpectCreativeRegression
)

$ErrorActionPreference = 'Stop'
Add-Type -Path @(
    (Join-Path $PSScriptRoot 'src/HalfGiantReachRules.cs'),
    (Join-Path $PSScriptRoot 'src/HalfGiantQuarryRules.cs'),
    (Join-Path $PSScriptRoot 'src/HalfGiantRockRules.cs')
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

$rock = [rfmechanics.HalfGiantRockRules]
Assert-True ($rock::CountOpenFaces($true, $true, $false, $false, $true) -eq 3) 'A corner rock has three open faces'
Assert-True ($rock::CountOpenFaces($true, $false, $false, $false, $true) -eq 2) 'An edge rock has two open faces'
Assert-True ($rock::CountOpenFaces($true, $false, $false, $false, $false) -eq 1) 'A wall face has one open face'
Assert-True ($rock::CountOpenFaces($false, $false, $false, $false, $true) -eq 1) 'Flat ground has one open face'
Assert-True ($rock::CountOpenFaces($false, $false, $false, $false, $false) -eq 0) 'A buried rock has no open faces'
Assert-True ($rock::IsEligible('game', 'rock-granite', 3, 2, $true)) 'A natural rock corner can be pulled'
Assert-True ($rock::IsEligible('game', 'crackedrock-granite', 2, 2, $true)) 'A cracked rock edge can be pulled'
Assert-True (-not $rock::IsEligible('game', 'rock-granite', 1, 2, $true)) 'Flat ground and wall faces are below the open-face minimum'
Assert-True (-not $rock::IsEligible('game', 'rock-granite', 0, 0, $true)) 'A zero minimum still needs one open face'
Assert-True (-not $rock::IsEligible('game', 'stonebricks-granite', 3, 2, $true)) 'Worked stone cannot be pulled'
Assert-True (-not $rock::IsEligible('game', 'ore-quartz', 3, 2, $true)) 'Ore cannot be pulled'
Assert-True (-not $rock::IsEligible('examplemod', 'rock-granite', 3, 2, $true)) 'Only game-domain rock can be pulled'
Assert-True (-not $rock::IsEligible('game', 'rock-granite', 5, 2, $false)) 'A detached rock is refused, since BreakIfFloating would drop the raw block'
Assert-True ($rock::IsWithinReach(100, 9.45, 1.0)) 'A block centre inside picking range plus margin is reachable'
Assert-True (-not $rock::IsWithinReach(110, 9.45, 1.0)) 'A block centre beyond picking range plus margin is refused'
Assert-True (-not $rock::IsWithinReach(0, 0, 1.0)) 'No picking range means no pull'
$reinforced = $rock::ResolvePull($true, $false)
$restored = $rock::ResolvePull($false, $false)
$pulled = $rock::ResolvePull($false, $true)
Assert-True ($reinforced -eq [rfmechanics.HalfGiantRockPullOutcome]::Reinforced -and $rock::ResolvePull($true, $true) -eq $reinforced) 'A block still present after the break is a reinforced refusal'
Assert-True ($restored -eq [rfmechanics.HalfGiantRockPullOutcome]::Restored) 'A failed hand-off restores the block'
Assert-True ($pulled -eq [rfmechanics.HalfGiantRockPullOutcome]::Pulled) 'A removed block in hand is a pull'
$saturation = [float]50
foreach ($outcome in $reinforced, $restored, $pulled) {
    if ($rock::ShouldChargeSatiety($outcome)) { $saturation = [rfmechanics.HalfGiantQuarryRules]::ApplySatietyCost($saturation, 10) }
}
Assert-Near $saturation 40 'Satiety is charged exactly once per successful pull and never on refusal or restore'
Assert-Near ($rock::HeldScale(2.1, $rock::DefaultHolderSize)) (1 / 2.1) 'A Half-Giant holds the rock at one block'
Assert-Near ($rock::HeldScale(1, $rock::DefaultHolderSize)) 1 'A normal-size holder needs no scaling'
Assert-Near ($rock::HeldScale(0, $rock::DefaultHolderSize)) (1 / 2.1) 'A stack without a holder size falls back to Half-Giant size'
Assert-Near ($rock::HeldScale([float]::NaN, $rock::DefaultHolderSize)) (1 / 2.1) 'A NaN holder size falls back'
Assert-Near ($rock::HeldScale([float]::PositiveInfinity, $rock::DefaultHolderSize)) (1 / 2.1) 'An infinite holder size falls back'
$hits = New-Object 'System.Collections.Generic.List[long]'
Assert-True ($rock::RecordHit($hits, 5)) 'The first touch of a target hits'
Assert-True (-not $rock::RecordHit($hits, 5)) 'The same target is never hit twice'
Assert-True ($rock::RecordHit($hits, 6) -and $hits.Count -eq 2) 'Each other target is hit once'
Assert-True ($rock::IsLandingDrop($false, 'game', 'stone-granite')) 'Landing keeps loose stones'
Assert-True (-not $rock::IsLandingDrop($true, 'game', 'rock-granite')) 'Landing never yields the raw rock block'
Assert-True (-not $rock::IsLandingDrop($true, 'game', 'crackedrock-granite')) 'Landing never yields a cracked rock block'

$carrySystem = Get-Content (Join-Path $PSScriptRoot 'src/HalfGiantAnimalCarryModSystem.cs') -Raw
$useStart = $carrySystem.IndexOf('private TextCommandResult Use(')
$useSource = $carrySystem.Substring($useStart, $carrySystem.IndexOf('private TextCommandResult Capture(') - $useStart)
$releaseAt = $useSource.IndexOf('return Release(player, offhand, config);')
$captureAt = $useSource.IndexOf('Capture(player, offhand, config)')
$pullAt = $useSource.IndexOf('HalfGiantRockPull.Pull(sapi, player, config)')
Assert-True ($releaseAt -ge 0 -and $captureAt -gt $releaseAt -and $pullAt -gt $captureAt) 'R releases first, then captures, then pulls a rock'
Assert-True ($useSource.Contains('player.CurrentEntitySelection?.Entity == null && player.CurrentBlockSelection != null')) 'A targeted creature takes precedence over a rock'
Assert-True ($useSource.Contains('if (!config.EnableHalfGiantAnimalCarry && !pullsRock)')) 'Rock pulling is not blocked by the animal-carry switch'

$pull = Get-Content (Join-Path $PSScriptRoot 'src/HalfGiantRockPull.cs') -Raw
$breakAt = $pull.IndexOf('blocks.BreakBlock(pos, player, 0);')
Assert-True ($breakAt -ge 0) 'Pull uses the vanilla break path with zero drops'
foreach ($guard in 'EnumGameMode.Spectator', 'mainHand.Empty', 'ItemCarriedAnimal or ItemCarriedRock', 'HalfGiantRockRules.IsWithinReach(', 'HalfGiantRockRules.IsEligible(', 'IsAttached(blocks, pos)', 'Claims.TryAccess(player, pos, EnumBlockAccessFlags.BuildOrBreak)') {
    $guardAt = $pull.IndexOf($guard)
    Assert-True ($guardAt -ge 0 -and $guardAt -lt $breakAt) "Pull checks $guard before breaking"
}
Assert-True ($pull.IndexOf('blocks.GetBlock(pos).Id == rockId') -gt $breakAt) 'A block still present after the break is detected as reinforced'
Assert-True ($pull -match 'private static bool HandOff\([^)]*\)\s*\{\s*if \(!mainHand\.Empty\) return false;') 'The hand-off re-checks the main hand before filling it'
Assert-True ($pull.Contains('outcome == HalfGiantRockPullOutcome.Restored && blocks.GetBlock(pos).Id == 0') -and $pull.Contains('blocks.SetBlock(rockId, pos);')) 'A failed hand-off restores the rock only into air'
Assert-True ($pull.Contains('if (HalfGiantRockRules.ShouldChargeSatiety(outcome))') -and ([regex]::Matches($pull, 'ApplySatietyCost\(')).Count -eq 1) 'Satiety is charged in one place, gated by the outcome'
Assert-True (-not $quarrySystem.Contains('HalfGiantRockPull')) 'The quarry satiety handler is unchanged by rock pulling'

$item = Get-Content (Join-Path $PSScriptRoot 'src/ItemCarriedRock.cs') -Raw
$spawnAt = $item.IndexOf('api.World.SpawnPriorityEntity(thrown);')
$catchAt = $item.IndexOf('catch (Exception error)')
$catchReturnAt = $item.IndexOf('return;', [math]::Max($catchAt, 0))
$takeAt = $item.IndexOf('slot.TakeOut(1);')
Assert-True ($spawnAt -ge 0 -and $catchAt -gt $spawnAt -and $catchReturnAt -gt $catchAt -and $takeAt -gt $catchReturnAt) 'The thrown stack is taken only after a successful spawn'
Assert-True (([regex]::Matches($item, 'TakeOut')).Count -eq 1) 'Nothing else takes the held rock'
Assert-True ($item.IndexOf('CollisionTester.IsColliding(') -lt $spawnAt) 'A blocked launch point refuses the throw before spawning'
Assert-True ($item.Contains('thrown.Collectible = false;') -and $item.Contains('thrown.DamageType = EnumDamageType.BluntAttack;')) 'The thrown rock is blunt and not collectible'
Assert-True ($item.Contains('slot != player.InventoryManager.ActiveHotbarSlot') -and $item.Contains('RaceTraits.HasTrait(player, config.HalfGiantTraitCode)')) 'Only a Half-Giant throws, from the main hand'
Assert-True ($item.Contains('EntityProjectileBase.GetProjectileDirection(') -and $item -match 'ThrowWindupSeconds = 0\.35f') 'The throw shares the vanilla aim and 0.35 s windup'
Assert-True ($item -match 'override void OnHeldAttackStart\([^)]*\)\s*\{\s*handling = EnumHandHandling\.PreventDefault;\s*\}') 'A held rock cannot attack or mine'
Assert-True ($item.Contains('GetDefaultBlockMeshRef(rock)') -and $item.Contains('HalfGiantRockRules.HeldScale(')) 'The held rock draws its block at true size'
Assert-True (-not ($item -match '(?i)fullwindup')) 'The dead full-windup item-offset stack attribute is removed from ItemCarriedRock'
Assert-True ($item.Contains('StartAnimation(HalfGiantAnimalCarryRules.ThrowWindupAnimationCode)') -and $item.Contains('StopAnimation(HalfGiantAnimalCarryRules.ThrowWindupAnimationCode)')) 'Rock windup starts and stops the dedicated animation code, not the shared "aim"'

$entity = Get-Content (Join-Path $PSScriptRoot 'src/EntityThrownRock.cs') -Raw
Assert-True ($entity.Contains(': EntityProjectileBase') -and -not $entity.Contains(': EntityThrownItem')) 'The thrown rock avoids EntityThrownItem repeat hits'
Assert-True ($entity -match 'ItemStack\? OnCollected\(Entity byEntity\)\s*\{\s*return null;' -and $entity -match 'bool CanCollect\(Entity byEntity\)\s*\{\s*return false;') 'The raw rock block can never be collected'
Assert-True ($entity -match 'void DamageProjectile\(Entity target\)\s*\{\s*\}') 'A hit does not destroy the rock without drops'
Assert-True ($entity.Contains('HalfGiantRockRules.RecordHit(entitiesHit, target.EntityId)')) 'Entity impacts go through the once-per-target rule'
Assert-True ($entity.Contains('FiredBy is EntityPlayer') -and $entity.Contains('base.CanDealDamage(target)')) 'An orphaned rock deals no damage; vanilla PvP checks still apply'
Assert-True ($entity.Contains('rock.Drops') -and -not $entity.Contains('GetDrops(') -and $entity.Contains('HalfGiantRockRules.IsLandingDrop(')) 'Landing uses the block drop list, filtered against raw rock'
Assert-True ($entity.Contains('if (shattered || !Alive || World.Side != EnumAppSide.Server) return;')) 'A rock shatters once, on the server'
foreach ($landing in 'override void OnCollided()', 'override void OnCollideWithLiquid()', 'HalfGiantRockThrowFlightTimeoutSeconds') {
    Assert-True ($entity.Contains($landing)) "The rock shatters via $landing"
}

$itemJson = Get-Content (Join-Path $PSScriptRoot 'assets/rfmechanics/itemtypes/carriedrock.json') -Raw
Assert-True ($itemJson -match 'class:\s*"ItemCarriedRock"' -and $itemJson -match 'maxStackSize:\s*1,') 'The pulled rock is a single-stack item'
Assert-True (-not $itemJson.Contains('tpHandFullWindupTransform')) 'carriedrock.json no longer defines a full-windup item offset'
$recipeHits = Get-ChildItem -Path (Join-Path $PSScriptRoot 'assets') -Recurse -File -Filter '*.json' | Where-Object { $_.Name -ne 'carriedrock.json' -and $_.FullName -notmatch '[\\/]lang[\\/]' } | Select-String -Pattern 'carriedrock' -SimpleMatch
Assert-True (-not $recipeHits) 'No recipe or patch turns a pulled rock into anything else'
$entityJson = Get-Content (Join-Path $PSScriptRoot 'assets/rfmechanics/entities/thrownrock.json') -Raw
Assert-True ($entityJson -match 'class:\s*"EntityThrownRock"' -and $entityJson -match 'hitboxSize:\s*\{\s*x:\s*0\.9,\s*y:\s*0\.9\s*\}' -and $entityJson -match 'size:\s*1,') 'The thrown rock draws at size 1 with a 0.9 hitbox'
$modSystem = Get-Content (Join-Path $PSScriptRoot 'src/RFMechanicsModSystem.cs') -Raw
Assert-True ($modSystem.Contains('RegisterItemClass("ItemCarriedRock", typeof(ItemCarriedRock))') -and $modSystem.Contains('RegisterEntity("EntityThrownRock", typeof(EntityThrownRock))')) 'The item and entity classes are registered'
$config = Get-Content (Join-Path $PSScriptRoot 'src/RFMechanicsConfig.cs') -Raw
Assert-True ($config -match 'EnableHalfGiantRockPull \{ get; set; \} = true;' -and $config -match 'HalfGiantRockPullMinOpenFaces \{ get; set; \} = 2;' -and $config -match 'HalfGiantRockThrowDamage \{ get; set; \} = 6\.0;' -and $config -match 'HalfGiantRockThrowFlightTimeoutSeconds \{ get; set; \} = 10\.0;') 'Rock pull defaults match the card'
$weight = [regex]::Match($config, 'HalfGiantRockThrowWeight \{ get; set; \} = ([0-9.]+);')
Assert-True ($weight.Success -and [double]$weight.Groups[1].Value -gt 0) 'Rock weight is positive so knockback pushes away'

if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) { throw "FAIL: production assembly not found: $AssemblyPath" }
if (-not $HarnessPath -or -not (Test-Path -LiteralPath $HarnessPath -PathType Leaf)) { throw "FAIL: initialized production harness not found (pass -HarnessPath or set RFM_HALFGIANT_HARNESS): $HarnessPath" }

if ($ExpectCreativeRegression) {
    & $HarnessPath --halfgiant-reach --rfm-dll $AssemblyPath --expect-creative-regression
}
else {
    & $HarnessPath --halfgiant-reach --rfm-dll $AssemblyPath
}
if ($LASTEXITCODE -ne 0) { throw "FAIL: initialized production harness exited $LASTEXITCODE" }

Write-Output "PASS: $script:checks Half-Giant quarry and rock-pull assertions; initialized production reach assertions ran in the supplied net10 harness. Gameplay remains a player check."
