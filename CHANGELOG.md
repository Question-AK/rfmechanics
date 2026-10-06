# Changelog

## 1.2.1-rp.11 - 2026-10-07

- While a Half-Giant holds right-click to throw a pulled rock, the rock now sits on the raised fist instead of hanging beside the forearm (third person). Other players see the same. The carry pose and first person are unchanged.
- Local retest only.
## 1.2.1-rp.10 - 2026-10-07

- A drifter carried by a Half-Giant now hangs head-down from one ankle in third person, as if held by the leg. Carried animals keep their pose, and first person is unchanged.
- Local retest only.
## 1.2.1-rp.9 - 2026-10-07

- Fixed Half-Giant carrying refusing a nearby animal or drifter as "out of reach". The reach is now measured from the eyes to the nearest part of the creature you are looking at: 7 blocks for animals and 3 for drifters, as before. Releasing uses the same distance check.
- Local retest only.
## 1.2.1-rp.8 - 2026-10-07

- A Half-Giant can pull a single rock loose. With an empty main hand and nothing carried, press Race Ability while looking at a natural rock or cracked rock block within reach that has at least 2 open faces among its four sides and top, such as an outcrop corner or edge. Flat ground and wall faces are refused, and so are rocks in claims where you cannot build, reinforced rocks and rocks with nothing holding them. Pulling costs the same satiety as quarrying a block, once per pull.
- The pulled rock shows at its real one-block size in hand (third person) and on the ground. It cannot be placed.
- Throw it like a carried creature: hold right-click for at least 0.35 s and let go. It flies at full one-block size and hits each creature once for 6 blunt damage, with knockback. PvP and creature-attack permissions apply as for thrown stones. It breaks into its normal loose stones where it lands, and it never gives back a rock block.
- New config keys `EnableHalfGiantRockPull` (true), `HalfGiantRockPullMinOpenFaces` (2), `HalfGiantRockThrowDamage` (6), `HalfGiantRockThrowWeight` (0.5), `HalfGiantRockThrowSpeed` (0.4) and `HalfGiantRockThrowFlightTimeoutSeconds` (10). Existing configs keep their values.
- Local retest only.
## 1.2.1-rp.7 - 2026-10-06

- A Half-Giant can throw a carried animal or drifter. With it in the main hand, hold right-click for at least 0.35 s and let go. Aim works like a thrown stone. Smaller creatures fly further: a chicken goes much further than a boar or sheep. The creature lands alive, with vanilla fall damage only.
- A thrown creature hits each other creature it touches once, with blunt damage that grows with its size (1 to 6). PvP and creature-attack permissions apply as for thrown stones.
- Provisional: a thrown drifter that lands in a claim where you cannot build is removed without drops, and you are told. Animals are unaffected.
- Held creatures now show at their real size next to the same creature standing in the world (third person). Creatures already carried before this update show at an average size until released and picked up again. First person is unchanged.
- New config keys `EnableHalfGiantAnimalThrow`, `HalfGiantAnimalThrowSpeed` (0.45), `HalfGiantAnimalThrowMinimumSpeed` (0.15), `HalfGiantAnimalThrowFullSpeedVolume` (0.15), `HalfGiantAnimalThrowDamagePerVolume` (3), `HalfGiantAnimalThrowMinimumDamage` (1), `HalfGiantAnimalThrowMaximumDamage` (6), `HalfGiantAnimalThrowFlightTimeoutSeconds` (5) and `HalfGiantAnimalThrowRemovesHostilesInForeignClaims` (true). Existing configs keep their values.
- Local retest only.

## 1.2.1-rp.6 - 2026-10-06

- A Half-Giant can now pick up drifters (all six types) with Race Ability, from up to 3 blocks away; animals keep their 7-block reach. Bowtorn and shivers cannot be picked up.
- A released drifter keeps its saved state and stays hostile. It may come back in its standing or crawling form, because the game picks that form when it spawns.
- New config keys `HalfGiantAnimalCarryTagExemptCodePathPrefixes` (default `["drifter-"]`) and `HalfGiantAnimalCarryTagExemptReach` (default 3.0). Both defaults are provisional. Existing configs keep their values.
- Local retest only.

## 1.2.1-rp.5 - 2026-10-06

- A carried animal now moves freely between hands: X swaps it into the main hand and back, and it can be dragged into the offhand slot. It shows a right-hand hold pose.
- Race Ability releases a carried animal from either hand, offhand first. Picking up still needs an empty offhand.
- Left-click does nothing while the animal is in the main hand (no attack or mining with it).
- Local retest only.

## 1.2.1-rp.4 - 2026-10-06

- Fixes Half-Giant empty-hand quarrying: the server no longer rejects the break for missing pickaxe tier, so natural rock and cracked rock break instead of resetting.
- Fixes the race handbook page losing most of its text after "Controls" (an unescaped ">" in the controls note).
- Local retest only.

## 1.2.1-rp.3 - 2026-10-06

- Fixes releasing a carried animal: placement no longer fails before spawning; the animal faces away from the Half-Giant like a creative-mode placement.
- Raises animal pickup/release reach from 3.5 to 7 blocks.
- Half-Giants can carry any tagged animal smaller than an adult brown bear (volume), including sheep and wolves; no bear can be carried. Saved rp.1/rp.2 defaults update automatically; custom values are kept.
- Local retest only: release, reach and size limits remain player checks.

## 1.2.1-rp.2 - 2026-10-06

- Corrects the Half-Giant animal-carry boar size reference, including the invalid default already saved by rp.1; no manual config changes are needed.
- Retains automatic collision-size eligibility without a species list, all rp.1 features and unchanged dependencies. Larger animals remain refused.
- Local retest only: pickup/release and oversized refusal remain player checks.

## 1.2.1-rp.1 - 2026-10-06

- Adds Half-Giant Survival interaction reach and empty-hand quarrying of natural rock and cracked rock; preserves ordinary drops, with a food cost per eligible block.
- Adds Goblin dark scouting against drifter, shiver and bowtorn families; empty-handed wall climbing is less concealed than crouched ground movement in darkness.
- Goblin wall climbing uses full speed with two free hands, half speed with one, and releases with neither; native ladder motion, tree climbing and Clamber controls are retained.
- Adds Dwarf Stonebrace on Ctrl+H: slower movement and reduced physical-attack damage and knockback, with stronger protection deeper underground and in stone enclosure. Environmental damage is unchanged; no added hunger cost or shield requirement.
- Adds a race handbook with controls, limits and conditional Diet Setup guidance; does not activate example diets or change saved bindings.
- Retains the reviewed crop-behavior merge compatibility fix and all 1.2.0 Orc, Elf, Watchfulness, movement, water and retired-Dwarf migration features. Dependencies are unchanged.
- Adds Half-Giant offhand animal capture/release on Race Ability while leaving the main hand available. Uses native animal-state serialization and a boar-sized default limit; live save/reopen and pose testing remain pending. Replay protection is process-local, not restart-persistent.
- Unpublished local test candidate. Real climbing physics, UI, combat ordering, death/reconnect and multiplayer behavior remain player checks; no worldgen changes are included. Test animal carrying with expendable animals in a disposable world.

## 1.2.0 - 2026-10-04

- Adds Half-Giant support (requires Race Framework 1.1.0, which adds the race): step height 2.1 blocks.
- Half-Giants wade and breathe in water up to about 3 blocks deep and wade about 1.5× as fast as a Human.
  They swim up heavily with Space and sink when idle.
- The Half-Giant third-person camera starts two zoom steps further out the first time. After that, the
  player's own distance is remembered between sessions (client file
  `ModConfig/rfmechanics-halfgiant-camera.json`), because the game resets it each session. A failed save of
  that file keeps the distance in memory and no longer interrupts play.
- Existing Mountain and Hill Dwarf characters (retired classes `rf-mountain-dwarf` and `rf-hill-dwarf`) become
  Commoners at login, before initialization, keeping their race model, inventory and gear. Pair this with Race
  Framework 1.1.0, which removes those classes.
- Fixes racial feedback errors while a player is still loading in: feedback waits until the player's entity
  state is ready, then resumes normally.
- Keeps all 1.1.2 mechanics, thresholds and notifications.

Known limitations:

- Movement settings in `ModConfig/rfmechanics.json` (step height and Half-Giant water) are read separately by
  the server and each client and are not synced. Keep them the same on both; the defaults already match.
- Half-Giant multiplayer and armor-interaction testing remain limited.

For Vintage Story 1.22.6.

## 1.1.2 - 2026-09-24

- Cumulative repair based on 1.1.0; restores the accepted mechanics omitted by the
  non-cumulative 1.1.1 candidate, including Orc mechanics and Elf Watchfulness.
- Adds shared exact-prefix classification for standard living trunks, redwood trunk
  sections and narrow living trunks, retaining separate Elf/Goblin permissions.
- Keeps branch foliage classification separate and excludes placed trunk variants.
- Redwood climbing gameplay acceptance remains pending.


## 1.1.0 - 2026-09-17

- All races can step up one block; Elves can step up two.
- Improved Elf tree climbing and Goblin wall climbing around corners; added Goblin Clamber stance and dry-earth climbing.
- Added Elf Watchfulness with subtle movement cues and brief silhouettes during focused observation.
- Improved Orc scent focus and tracking, including blood pursuit with supported bleeding mods.
- Orcs gain passive Tier 2 thick skin regardless of equipment; refined Frenzy and Thew expenditure feedback.
- Added subtle racial feedback messages. Race Ability now defaults to R; existing key bindings are preserved.

For Vintage Story 1.22.6. Cumulative from the locally tested 0.1.3-orcskin.2 build;
armor interaction and broader multiplayer checks remain incomplete.
Earlier entries below preserve development history, including superseded trials.

## 0.1.3-orcskin.2 - Thick skin regardless of equipment

- Removes the chest armor, shirt and coat gate: all Orcs receive passive Tier 2 skin.
- Preserves protection arithmetic and normal armor/shield-first damage order.
- Updates /rfskin diagnostics; armor interaction gameplay testing remains deferred.


## 0.1.3-orcskin.1 - Bare-torso protection, unpublished local trial

- Replaces Orc bracing with passive vanilla Tier 2 tin-bronze lamellar protection.
- Requires empty chest armor, shirt and coat slots, checked on each physical hit.
- Removes bracing input, drain, recovery, HUD and announcement lines; preserves
  its historical prototype for future Dwarf design, with no Dwarf implementation.
- Adds short `/rfskin` diagnostics without chat-markup arrows.
- Retains feedback/Thew/Frenzy, smell, Watchfulness and movement from feedback.1.
- Adds production rules, slot eligibility and regression-preservation checks.




## 0.1.3-feedback.1 - Racial feedback and Thew/Frenzy prototype, not installed

- Adds localized, race-themed ephemeral feedback with no historical queue.
- Retains Oresong; adds three sparse Dwarf mining-depth lines.
- Makes Thew smoke mean actual net metabolic reserve loss, never growth or debt alone.
- Smooths hunger-driven Frenzy, charges additional debt only for qualified exertion,
  and retains maximum bonuses, exhaustion safeguard and combined pursuit cap.
- Preserves delivered bracing protection/economy, smell and Watchfulness.
- Adds offline arithmetic/preservation checks and diagnostic rates for player testing.

## 0.1.3-orcbrace.1 - Orc combat prototype, unpublished and not installed

- Replaces health-dependent wild-animal resistance with Tier 1 natural skin, and
  Tier 3 frontal protection while braced, using vanilla jerkin/iron-lamellar profiles.
- Adds server-owned Race Ability toggle, escalating satiety cost, gradual exertion
  recovery, low-food release and restart hysteresis. Supports saved mouse/key rebindings.
- Keeps attacks and movement available, validates direction per hit, and clears
  active state on lifecycle/identity changes. Equipment calculation precedes skin.
- Adds a single discovery-style feedback line and read-only `/rfbrace` diagnostics.
- Preserves accepted smell stance, Watchfulness, movement, Thew, Burn and Frenzy.
- Includes offline checks of production protection, direction and food rules.

## 0.1.3-orchunt.2 ? automatic smell stance, unpublished local trial

- Removes Orc held Race Ability sniffing and its hotbar input blocker; reserves R
  for separate bracing work, preserving other races' input behavior.
- Adds natural standing/sitting/slow-movement concentration and tunable ambient fade,
  smooth movement/off vision recovery and immediate stance-on whiff.
- Gates blood visuals on stance while retaining independent pursuit and acknowledgement.
- Preserves learned scent shapes and raises the bounded source limit for crowded scenes.
- Leaves Thew, Burn, Frenzy, protection, pursuit tuning and bleeding providers unchanged.

## 0.1.3-orchunt.1 ? local hunting prototype, unpublished

- Retains Watchfulness revision 9 and accepted dev.11 movement.
- Adds Orc stance whiffs, quick sniffing, slower walking concentration and automatic
  recovery from deep-focus loss; removes the speed/ground-contact cancellation latch.
- Adds small learned scent shapes, ordinary player scent and directional blood droplets.
- Adds server-owned, real-bleeding-gated pursuit with direction, switching, grace,
  decay, Frenzy stacking limit and a restrained discovery acknowledgement.
- Connects BloodTrail 1.2.5 state and The Hunter 0.2.58 active bleeding effects.
  Neither provider is installed in sparse Diet; pursuit stays inactive there.
- Bounds spatial scans and live wisps; restores incoming renderer state.
- No game/server launch, save migration, profile expansion or publication.

## 0.1.3-watchfulness.9 — local discovery pacing trial, unpublished

- Sets focused silhouette peak opacity to 30% and extends natural fade-out from
  0.15 to 0.45 seconds, with smooth easing; quick onset and peak hold are retained.
- Extends attention-loss fade from 0.12 to 0.36 seconds. Terrain still blocks immediately.
- Prevents repeated glimpses during continuous attention. Each target needs 0.3 seconds
  outside the wider focus area and a fresh observation period before reappearing.
- Explicit glimpse preview bypasses observation and the look-away gate once.

## 0.1.3-watchfulness.8 — local opacity trial, unpublished

- Lowers focused glimpse peak opacity from 80% to 65% at Miles's request.
- Player retest pending; all other Watchfulness behavior remains as in revision 7.

## 0.1.3-watchfulness.7 — local awareness/discovery prototype, unpublished

- Replaces the tiny awareness dot with a wider, feathered pale wisp and minimum
  projected width; preserves independent movement triggers outside racial zoom.
- Adds two-second focused observation while Watchfulness and racial zoom are active,
  including stationary living agents. Brief broken silhouettes borrow actual entity
  meshes and current animation poses, with no permanent material changes.
- Conservatively tests the viewing volume against solid voxels, clips glimpses to
  that checked volume, and fails closed on missing chunks or budget exhaustion.
- Uses the engine's saved perspective projection for both admission and rendering;
  extends scoped state restoration to the new texture, culling and animation bindings.
- Adds labelled previews and bounded emission/retirement/submission/projection traces.
- Build verification is separate from pending player acceptance. No remote deployment.

## 0.1.3-dev.11 — combined local test, unpublished

- Integrates elf two-block stepping and the default-R racial hotkey with the existing
  elf/goblin corner traversal and persistent goblin Clamber stance.
- Preserves saved hotkey bindings; players using X should rebind Race Ability in Controls
  because vanilla's hand-swap action also uses X.
- Explicitly excludes the preserved, unapproved retired-dwarf migration source from compilation.
- The dev.9/dev.10 entries below describe source increments, not separately deployed packages.
- Local player acceptance remains pending. No server deployment or public release.

## 0.1.3-dev.10 — race ability hotkey default, unpublished

- The Race Ability hotkey now defaults to **R**. It defaulted to C, which vanilla already binds to
  the character inventory, so on a fresh install the press opened that dialog and never reached
  Ore-Song or goblin spit. X is not an alternative: vanilla binds it to flip hand slots, which
  reports every press as handled.
- The hotkey identifier is unchanged, so any existing rebind is kept exactly as it was. Only a
  player with no saved binding for Race Ability picks up the new default; anyone already rebound
  has to change it in Settings -> Controls, or use Restore Defaults.
- Ctrl+H Clamber is unchanged, and no retired hotkey identifier was revived.
- Local build only; in-game acceptance pending.

## 0.1.3-dev.9 — elf two-block stepping, unpublished

- Elves step up two blocks instead of one. It is automatic and stance-independent: no new toggle,
  no hotkey, and nothing to hold. Every other race, humans included, keeps one-block stepping.
- Vanilla clearance is unchanged, so a two-block ledge with a block above it still refuses the
  step, and openings too small to fit through still stop you.
- Add `ElfStepHeightOverride` (2.0). `StepHeightValue` (1.0) is now the baseline for every race
  without an override rather than a universal value; `EnableStepHeight` still turns the whole
  system off and restores each entity's own original step height, elves included.
- Existing configs pick up the elf default simply by not having the new key. A config from before
  universal stepping that tuned the old `ElfStepHeightValue` has that number carried onto
  `ElfStepHeightOverride`, where it was always meant to apply, instead of onto everyone.
- Local build only; in-game acceptance pending.

## 0.1.3-dev.8 — elf trunk corner traversal, unpublished

- Elves climbing a tree now wrap around the outside edge of the trunk instead of dropping when
  the gripped column goes diagonal, the same way goblins wrap a building corner.
- Elf climbing also picks its face by preference rather than scan order: the face already held
  wins, then the one best matching the direction of travel, so moving around a trunk no longer
  snaps back to the north face.
- Add `EnableElfCornerTraversal` and `ElfCornerGraceTicks` (6 ticks, about 0.2s). Turning it off
  restores the previous elf scan exactly, north-first face pick included. Goblin trunk climbing
  keeps using the goblin keys.
- Goblin and elf climbing now share one implementation of the scan, face scoring and corner
  window; only the list of grippable blocks differs between them. No change to goblin behaviour
  is intended.
- Local build only; in-game acceptance pending.

## 0.1.3-dev.7 — goblin outside-corner traversal, unpublished

- Climb around outside (convex) building corners instead of dropping off them. When the wall a
  goblin is holding runs out, gravity stays suspended briefly while a second scan looks at the
  four diagonal columns for the wall continuing around the corner.
- Pick the climbing face by preference rather than by scan order: the face already held wins,
  then the one best matching the direction of travel. Inside corners no longer snap the goblin
  to the north face regardless of which way it is going.
- The window suspends gravity only — it never holds a face whose block has gone — and is cleared
  on landing, so stepping away from a wall still falls immediately.
- Add `EnableGoblinCornerTraversal` and `GoblinCornerGraceTicks` (6 ticks, about 0.2s). Turning
  corner traversal off restores the previous scan exactly, north-first face pick included.
- Accepted in game on a local client; not published.

## 0.1.3-dev.4 — goblin Clamber stance and dry earth climbing, unpublished

- Add the Clamber stance on Ctrl+H (goblins only): a sticky per-player mode that gates wall
  climbing. Off by default on a new character, and kept across rejoin and death until toggled
  off. Tree trunks and vanilla ladders are never gated, and other races are unaffected.
- The stance lives on the player's watched attributes and is only ever written by the server;
  the client asks for a state and never sets race state itself.
- Climb dry earth with Clamber on: soil, packed dirt, trampled earth, dry packed dirt, bony
  soil, cob and forest floor. Sand, gravel, dirty/muddy/sludgy gravel, farmland, raw clay and
  peat stay unclimbable, and there is no all-material wildcard.
- Repair four rock entries that matched no block in 1.22.6 and so never granted climbing:
  `mossybrick-`/`lichenbrick-` (really `mossystonebricks`/`lichenstonebricks`) and
  `peatbrick-`/`refractorybrick-` (really bare `peatbrick` and plural `refractorybricks`).
  A one-time migration repairs existing configs; other customizations are left alone.
- Add `EnableGoblinClamberStance`, `EnableGoblinEarthClimbing` and `GoblinEarthClimbCodes`.
  Earth entries match a bare code or that code plus a variant, so `cob` does not also catch
  the cobblestone family.
- Local build only; in-game acceptance pending for the earth surfaces and the stance.

## 0.1.3-dev.3 — universal one-block stepping, unpublished

- Step over a full block as any race, humans included; the elf-only gate and the per-player
  step-height toggle are gone. Vanilla clearance still blocks low ceilings and tight openings.
- Retire the Ctrl+H elf toggle hotkey and the `/rfelfstepheight` command, reserving Ctrl+H
  for the planned stance control. Old saved toggles can no longer disable stepping.
- Replace `EnableElfStepHeight`/`ElfStepHeightValue`/`ElfStepHeightDefaultEnabled` with
  `EnableStepHeight`/`StepHeightValue`; a one-time migration carries a customized value across.
- Local build only; in-game acceptance pending.

## 0.1.3-dev.1 — seated Ore-Song, unpublished

- Replace the instant dwarf scan with seated, empty-hand stone contact, a settling period,
  a knock, ten seconds of listening and three seconds of recovery.
- Search loaded server terrain out to 96 blocks using a shared work budget and bounded
  chunk-summary cache. No terrain loading/generation; incomplete searches are identified.
- Play staggered mineral voices with coarse bearings, nearby enveloping sound, average
  grade harmonics and size-dependent chorus. Add optional sensory captions.
- Replace the ten legacy cues with sixteen original synthesized material voices,
  including separate gem and bismuth voices, three variations and rough/clear layers.
- Local build only; multiplayer performance and in-game audio acceptance pending.

## 0.1.2-rc.2 ? candidate, unpublished

- Remove the crop-stunting patch for the absent vanilla bellpepper asset, fixing its 1.22.6 startup error.

## 0.1.2-rc.1 ? candidate, unpublished

- Snapshot of current development for gameplay acceptance; not an approved stable release.
- Include MIT licensing for original work, credits and applicable third-party notices in packages.
- Add safe build/package commands, full source identity and immutable Release ZIPs.
- Document AI-assisted development, existing-save limitations and the development/stable release workflow.
