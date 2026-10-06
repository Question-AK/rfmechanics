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

$itemJson = Get-Content (Join-Path $PSScriptRoot 'assets/rfmechanics/itemtypes/carriedanimal.json') -Raw
$storageMatch = [regex]::Match($itemJson, '(?m)^\s*storageFlags:\s*(\d+)\s*,')
Assert-True ($storageMatch.Success) 'Carried animal declares storage flags'
$storageFlags = [int]$storageMatch.Groups[1].Value
Assert-True (($storageFlags -band 1) -ne 0 -and ($storageFlags -band 256) -ne 0) 'Carried animal fits General and Offhand slots so X can flip it back'
Assert-True ($itemJson -match 'heldLeftTpIdleAnimation:\s*"holdinglanternlefthand"' -and $itemJson -match 'heldRightTpIdleAnimation:\s*"holdinglanternrighthand"') 'Carried animal has a hold pose in each hand'

$itemSource = Get-Content (Join-Path $PSScriptRoot 'src/ItemCarriedAnimal.cs') -Raw
Assert-True ($itemSource -match 'override void OnHeldAttackStart\([^)]*\)\s*\{\s*handling = EnumHandHandling\.PreventDefault;\s*\}') 'Held carried animal cannot attack'

if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) { throw "FAIL: production assembly not found: $AssemblyPath" }
Write-Output "PASS: $script:checks Half-Giant animal-carry eligibility, snapshot identity, preservation, replay, hand-swap and release-order assertions passed. Native entity serialization and collision remain player/review checks."
