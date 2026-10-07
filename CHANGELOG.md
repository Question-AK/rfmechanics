# Changelog

## 1.3.1 - 2026-10-07

First public release of the 1.3 line. It replaces 1.3.0, which was withdrawn before release, and lists every change since
1.2.0.

Half-Giants (requires Race Framework 1.1.0 or later):

- Longer picking reach in Survival, for working at a distance. Melee reach is unchanged.
- With an empty main hand, a Half-Giant quarries natural rock and cracked rock faster. Drops are normal, and each
  block costs some satiety. Tools, ore and worked stone are unaffected.
- Carry animals: with an empty offhand, press Race Ability (default R) while looking at an animal up to 7 blocks
  away. Anything smaller than an adult brown bear can be carried; bears cannot. Aim is forgiving for moving animals.
- Drifters can be carried too, from up to 3 blocks away. Bowtorn and shivers cannot. A released drifter is still
  hostile and may come back standing or crawling.
- Press Race Ability while looking at clear ground to set the creature down. For 1 second after a pickup, presses
  are ignored so the creature is not dropped again by accident.
- A carried creature can move between hands (X, or drag it). Left-click does nothing while it is in the main hand.
  It keeps its saved state through drops and transfers, and only a Half-Giant can release or throw it.
- Throw: with a creature or rock in the main hand, hold right-click for at least 0.35 s and let go. Holding for
  2 seconds gives a charged throw at twice the speed; the arm pulls back further as the charge builds. Aim works
  like a thrown stone.
- Thrown creatures land alive, with normal fall damage only, and smaller ones fly further. A thrown creature hits
  each creature it touches once, for 1 to 6 blunt damage by size. PvP and creature-attack permissions apply.
- A thrown drifter that lands in a claim where you cannot build is removed without drops, and you are told.
- Pull a rock loose: with an empty main hand and nothing carried, press Race Ability at an edge or corner of natural
  rock or cracked rock. Flat ground, wall faces, reinforced rock, unsupported rock and claims where you cannot build
  are refused. A pull costs the same satiety as quarrying a block.
- A pulled rock cannot be placed. Thrown, it hits each creature once for 6 blunt damage with knockback, then breaks
  into its normal loose stones. It never gives back a rock block.
- Carried creatures and pulled rocks show at their real size in third person.

Dwarves:

- Stonebrace on Race Stance (default Ctrl+H): slower movement and much less knockback, and less damage from physical
  attacks. Protection grows with depth and nearby stone, and fades over 2 seconds after leaving them. Falls, fire,
  poison, starvation and other environmental harm are unchanged. No extra hunger cost. Ore-Song stays on R.

Goblins:

- Dark scouting: in darkness, without a held light, drifters, shivers and bowtorn notice a goblin later. Crouching on
  the ground hides best, empty-handed wall climbing less, and sprinting least. Bears, hyenas and wolves are less
  affected. Close contact and existing hostility are not cleared.
- Crouching in darkness without a held light is faster.
- Clamber wall climbing needs free hands: full speed with both, half speed with one, and no grip with neither.
  Ladders behave as in the base game while Clamber is on.
- Goblin crop stunting now merges with other mods' crop behaviors (for example Almanac farming practice) instead of
  replacing them.

Everyone:

- A "Races and abilities" handbook page explains each race's controls, active and passive abilities, limits and Diet
  Setup guidance. It does not turn on any diet.
- New settings in `ModConfig/rfmechanics.json` for all of the above. Existing files keep their values and gain the
  new keys with their defaults.
- Keeps all 1.2.0 mechanics, including Half-Giant wading and camera memory, Elf Watchfulness, Orc mechanics and the
  retired-dwarf-class migration.

Known limitations:

- Protection against duplicating a carried creature lasts only while the game or server runs; it is not kept across
  a restart.
- First-person sizes of carried creatures and pulled rocks are not tuned yet.
- Rock throw speed, damage and knockback have had little gameplay testing.
- The arm pull-back animation is timed for the default 2-second charge. If a server changes the full-charge time,
  the animation no longer lines up with full charge; throw power still follows the setting.
- Movement settings (step height and Half-Giant water) are still not synced from the server. Keep them the same on
  the server and every client; the defaults match.

For Vintage Story 1.22.6.

## 1.3.0 - 2026-10-07 (withdrawn)

Not released. Its handbook page still contained test-build wording and missed several abilities. Use 1.3.1.

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
