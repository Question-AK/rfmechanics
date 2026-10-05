# Offline Stonebrace rule and wiring checks; no game/server or test application.
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'src/DwarfStonebraceRules.cs')
$script:checks = 0
function Assert-True([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:checks++
}
function Assert-Near([double]$Actual, [double]$Expected, [string]$Name) {
    Assert-True ([Math]::Abs($Actual - $Expected) -lt 0.000001) $Name
}

Assert-Near ([rfmechanics.DwarfStonebraceRules]::DepthFraction(110, 110, 0)) 0 'Surface depth is neutral'
Assert-Near ([rfmechanics.DwarfStonebraceRules]::DepthFraction(55, 110, 0)) 0.5 'Sea-level-to-floor depth normalization'
Assert-Near ([rfmechanics.DwarfStonebraceRules]::DepthFraction(0, 110, 0)) 1 'Floor is maximum depth'
Assert-Near ([rfmechanics.DwarfStonebraceRules]::DepthFraction(-20, 110, 0)) 1 'Depth clamps below floor'
Assert-Near ([rfmechanics.DwarfStonebraceRules]::EnclosureFraction([int[]](2,2,2,2,0,0), 2, 4, 4)) 1 'Three-by-three tunnel cross-section qualifies despite open ends'
Assert-Near ([rfmechanics.DwarfStonebraceRules]::EnclosureFraction([int[]](3,3,3,3,0,0), 2, 4, 4)) 0.5 'Wider cavern tapers enclosure'
Assert-Near ([rfmechanics.DwarfStonebraceRules]::Reduction(1, 0, .3, .6, .6)) .3 'Deep open space target'
Assert-Near ([rfmechanics.DwarfStonebraceRules]::Reduction(1, 1, .3, .6, .6)) .6 'Deep close stone target'
Assert-Near ([rfmechanics.DwarfStonebraceRules]::Reduction(1, 1, .3, .9, .9)) .6 'Total reduction hard cap'
Assert-Near ([rfmechanics.DwarfStonebraceRules]::FadeToward(.6, 0, .6, 2, 2)) 0 'Release fade reaches neutral'
Assert-Near ([rfmechanics.DwarfStonebraceRules]::ApplyDamage(8, .6)) 3.2 'Post-armor damage is reduced once'
Assert-Near ([rfmechanics.DwarfStonebraceRules]::ApplyDamage(0, .6)) 0 'Fully blocked damage remains zero'
Assert-True ([rfmechanics.DwarfStonebraceRules]::IsPhysicalAttack($true, $true)) 'Physical attack with attacker provenance qualifies'
Assert-True (-not [rfmechanics.DwarfStonebraceRules]::IsPhysicalAttack($true, $false)) 'Physical label without attacker provenance is excluded'
Assert-True (-not [rfmechanics.DwarfStonebraceRules]::IsPhysicalAttack($false, $true)) 'Environmental provenance is excluded'
Assert-True ([rfmechanics.DwarfStonebraceRules]::MatchesStone('rock-granite', [string[]]@('rock-'))) 'Natural raw stone prefix qualifies'
Assert-True (-not [rfmechanics.DwarfStonebraceRules]::MatchesStone('stonebricks-granite', [string[]]@('rock-'))) 'Worked stone is excluded by finite material policy'
Assert-True (-not [rfmechanics.DwarfStonebraceRules]::MatchesStone('rock-granite', [string[]]@())) 'Empty material policy fails neutral'

$root = Join-Path $PSScriptRoot 'src'
$system = Get-Content (Join-Path $root 'DwarfStonebraceModSystem.cs') -Raw
$patch = Get-Content (Join-Path $root 'DwarfStonebracePatch.cs') -Raw
$hotkey = Get-Content (Join-Path $root 'RaceAbilityHotkeyModSystem.cs') -Raw
Assert-True ($patch.Contains('ApplyOnDamageDelegates')) 'Damage patch uses post-armor delegate seam'
Assert-True ($system.Contains('source.KnockbackStrength')) 'Knockback is damage-source scoped, not entity-property scoped'
Assert-True ($system.Contains('GetChunkAtBlockPos(pos) != null')) 'Unloaded samples fail neutral without chunk loading'
Assert-True ($system.Contains('source.GetCauseEntity() != null') -and $system.Contains('EnumDamageType.BluntAttack')) 'Runtime gate requires physical type and actual attacker provenance'
Assert-True ($system.Contains('Dictionary<string, State>') -and $system.Contains('states.TryGetValue(player.PlayerUID')) 'Independent players retain separate stance state'
Assert-True ($system.Contains('PlayerJoin += Remove') -and $system.Contains('PlayerDeath += Death')) 'Rejoin and death clear stance lifecycle'
Assert-True ($system.Contains('RaceTraits.HasTrait(player, config.DwarfTraitCode)')) 'Server verifies dwarf identity'
Assert-True ($hotkey.Contains('PlayerRace.Dwarf') -and $hotkey.Contains('DwarfStonebraceModSystem')) 'Ctrl+H routes dwarves to Stonebrace'
Assert-True ($hotkey.Contains('GoblinClamberStanceModSystem')) 'Goblin Clamber routing remains available'
Assert-True ($hotkey.Contains('DwarfOreSongModSystem')) 'Dwarf Ore-Song remains on Race Ability'
Write-Output "PASS: $script:checks Stonebrace rule and wiring assertions; gameplay remains a player check."
