# RF Mechanics

Prerelease candidate for Vintage Story 1.22.6. In-game acceptance is still pending.

Local feedback prototype **0.1.3-feedback.1**, cumulative on delivered orcbrace.1,
accepted Orc smell .2, Watchfulness .9 and dev.11 movement. Build preparation only;
not installed or published. Miles evaluates it in VS - Diet Test after installation.

Brief racial messages use one noninteractive fading line, distinct race colors and
built-in fonts. Goblin rot consumption/repair and Clamber, Elf Watchfulness/woodland
movement, Dwarf mining-depth milestones, and Orc scent/Burn/Frenzy use confirmed
outcomes and restrained transitions. No panel or counter. Oresong is unchanged.
Messages are localized in assets/rfmechanics/lang/en.json. EnableRacialFeedback
controls these new lines. Rapid stance changes replace the previous line, with no
historical queue. Passive messages yield to deliberate actions. Blood pursuit and
bracing also share the line so simultaneous Orc messages cannot overlap.

Dwarf lines follow the depth fraction already used by the mining curve, relative to
sea level (not the local surface): 25%, 50%, 75%. They require breaking stone/ore,
only the deepest newly reached band is announced, at most once per band per life/join.
These are depth fractions, not a claim of a 25/50/75% speed bonus.

Thew growth/stability produce no smoke. Wisps represent the net reduction from a
server metabolic update, including actual reserve-funded debt repayment. Growth
that offsets consumption is silent. Slower loss has thin occasional wisps, faster
loss fuller/frequent ones. The same rate drives local and nearby-player feedback;
local wisps are below the eyes and shorter-lived. Particles already emitted fade.
The metabolic cadence is unchanged (normally six seconds), so changes in smoke can
lag feeding by that interval. Initialization, admin edits and death resets are not
counted as metabolic expenditure. Old smoke state/priority and steam settings no
longer drive the effect. PuffSmokeColorRgb, PuffOpacity, PuffRenderRange, EnablePuff
and PuffTickIntervalMs still apply; particle geometry/lifetime are restrained in code.

Frenzy still begins below 50% satiety, reaches the existing 25% maximum and preserves
the reserve/debt exhaustion safeguard. New FrenzyResponseExponent=1.5 and
FrenzyResponseWriteThreshold=0.0025 replace the old saved cubic curve/write threshold
without editing installed configuration. Gate, maximum and debt-rate settings remain
active. Updates are normally every 500 ms, not every frame.

| Food | Speed bonus | Sprint debt/game-hour | Walk debt/game-hour |
| --- | --- | --- | --- |
| 50% | 0% | 0 | 0 |
| 40% | 2.24% | 0 | 0 |
| 30% | 6.32% | 0 | 0 |
| 25% | 8.84% | 0 | 0 |
| 20% | 11.62% | 0.27885 | 0.09760 |
| 10% | 17.89% | 0.42933 | 0.15026 |
| 0% | 25% | 0.60000 | 0.21000 |

Costs shown assume the benefit is available throughout qualifying movement. Idle is
always zero extra Frenzy debt; ordinary starvation/Burn costs still apply. Below the
existing 25% debt gate, deliberate ground sprinting pays full rate, ordinary travel
35% (FrenzyWalkingDebtMultiplier). Requires input and measured displacement aligned
with intended travel at both sample endpoints. Blocked input, riding, swimming,
climbing, flying, airborne motion, hurt/knockback, vanilla agent repulsion, stale intervals and detected
teleports are excluded. Teleport version changes reject even short same-dimension
teleports between samples; push/knockback observations provide a 1.5-second grace. This conservative rule may undercount obstacle-rich movement;
unrecognised external mod pushes concurrent with aligned input remain a limitation.

Pursuit retains its configured 35% Frenzy/pursuit combined cap. Bracing still cuts off
its debit at 30% food, above the 25% extra-debt threshold; its 30% Frenzy bonus is now
6.32%. No protection, Burn cost, band, food restriction or bracing economy redesign.
Current local BurnThewPerHp=0.03 was inspected but not edited and can dominate costs.
/rfthew dump additionally reports measured exertion, actual Frenzy debt rate and
net Thew loss rate. Read-only diagnostics do not replace player observation.

Future direction only: a sharp/vivid Frenzy screen effect, preserving learned scent
colors/shapes, a clear center, smooth recovery and compatibility with smell-focus
fade. The engine has PsychedelicStrength shader plumbing worth investigating later;
no screen shader, color grading or psychedelic behavior is added here. Broader Thew
food-category, maintenance, starvation, Burn-borrowing and band-balance proposals
remain pending and are not part of this prototype.

Tap the saved **Race Ability** binding (default R) as an Orc to brace; tap again to
release. Walking, attacks and sprinting remain available. Natural skin uses vanilla
leather jerkin's Tier 1 profile from every direction. While braced, hits within a
120-degree horizontal frontal arc use iron lamellar's Tier 3 profile instead.
These are complete vanilla armor profiles, including weapon-tier losses, not damage
division by tier. Natural protection cannot increase damage at extreme weapon tiers.
Interim armor rule: normal shield/armor calculation and wear happen first, followed
by exactly one natural-protection calculation. Equipped armor never prevents activation.
This additional protection can make armored Orcs very tough; armor balancing is deferred.
The former low-health wild-animal resistance is retired, including its no-armor gate.
Legacy OrcWildResist config values remain readable but have no effect.

Bracing directly debits 1 satiety point/second initially, rising linearly to 5 after
30 seconds. Extra cost from rested: 10s = 16.67, 20s = 46.67, 30s = 90, 60s = 240
points, plus ordinary hunger. Releasing stops debit; full exertion takes 60 seconds
to settle. Early reactivation retains the remaining exertion and cost. Forced release
at 30% of actual food capacity; restarting requires 32%. No new Thew/debt or hit cost.
New settings: EnableOrcNaturalProtection, EnableOrcBracing,
OrcBraceFrontalArcDegrees, OrcBraceInitialSatietyPerSecond,
OrcBraceMaxSatietyPerSecond, OrcBraceRampSeconds, OrcBraceRecoverySeconds,
OrcBraceLowFoodFraction, OrcBraceRestartFoodMargin. Change server config and relaunch
normally to reload. Existing player config has not been changed by preparation.

Protection covers entity/player/unknown-source blunt, piercing and slashing attacks.
Unknown or missing-entity attacks get T1 only. Projectiles use incoming motion;
missing/stopped/vertical motion has no frontal benefit. Environmental sources,
internal/bleeding damage and other damage types bypass skin. Vanilla physical DoT
ticks recreated without a source entity get T1 only. Mods that bypass vanilla health
delegates or mislabel attacks require separate compatibility testing.

Feedback uses one replaceable, fading discovery-style line, with no permanent meter
or message queue. Active state and exertion are session-only and reset on death,
disconnect, class/trait change or world exit. Ordinary satiety changes persist through
the game's existing hunger storage. `/rfbrace` reads server state and the most recent
natural-protection calculation; `/rfthew dump` includes that report.
Run `./Verify-OrcBracing.ps1` for offline production-rule checks, and
`./Build.ps1 -Configuration Release -ReleaseCandidate` for the existing mod package.
Neither command launches or installs the game. Gameplay remains a player check.

Retained Orc smell **0.1.3-orchunt.2** behavior:
Orc Ctrl+H (saved stance rebind retained) enables all scent visuals. The first whiff
arrives on the next 50 ms sample. Moving/sprinting gives occasional whiffs; stopping
or sitting automatically builds concentration in four seconds, extending base range
from 20 to 64 blocks. Body size scales range 0.7-1.2, capped at 64. Sneaking/slow
movement below 1.5 blocks/s can build partial focus. Smell uses no Race Ability input;
the independent bracing toggle now owns Orc Race Ability.

Standing/sitting also fades the surrounding world over six seconds, to maximum 0.90
ambient weight. Scent shaders keep their own color/alpha so they remain readable.
Moving eases concentration down over up to two seconds and restores vision over up
to one second. Stance off resets concentration and stops all scent emission; existing
wisps fade within 0.2 seconds and vision returns within one second. Stance remains
session-only. Death/race exit/world exit clear the effect.

New tuning: OrcFocusFadeSeconds=6, OrcFocusMaximumDarkness=0.90,
OrcFocusVisionRecoverySeconds=1, OrcFocusRecoverySeconds=2,
OrcSlowFocusLevel=0.35, OrcSlowFocusMaxSpeed=1.5, OrcSmellSourceLimit=12 (cap 16).
Existing OrcDeepFocusSeconds=4, OrcPassiveRange=20, OrcDeepRange=64,
OrcWhiffIntervalSeconds=4 and OrcWhiffDurationSeconds=0.65 remain adjustable.
Moving whiffs repeat every 3.4-4.6 seconds; concentration lengthens/joins them into
continuous scent from 75% focus. OrcQuickSniffMs/OrcQuickRange are now legacy.
No configuration migration or save change is needed.

Pursuit physics/acknowledgement, blood adapters and tuning are unchanged: server-owned
actual bleeding detection, 40-block radius, six-second ramp to +20%, two-second grace,
three-second decay and +35% combined Frenzy/pursuit cap. Blood visuals now require
stance; the physical benefit and brief engagement impression do not. Target swimming
still interrupts eligibility until it surfaces bleeding. Orc swimming is separate.
Diet still has neither BloodTrail nor The Hunter: blood gameplay cannot be tested
there without a separately authorized provider setup. No dependency/profile expansion.

Distinct scent shapes, body-size differences and close-range blood direction are
preserved. Terrain remains unresolved: no scent obstruction/attenuation through walls.
The source limit is raised to 12 for crowded scenes; scans remain bounded at 256 visits,
64 candidates, with 1200 live wisps and particle-setting limits. `.rfhunttest` reports
stance, resting/sitting, measured speed, concentration, darkness and real blood status;
it does not create bleeding or identify targets in ordinary gameplay.

Retained Watchfulness trial: elf Ctrl+H enables the stance.
Without racial zoom, moving animals/players can produce a broad pale wisp.
Hold Race Ability (default R, existing rebind retained) and observe a living agent
near the centre for two seconds for a 1.05-second broken glimpse of its animated shape,
with peak opacity 30% before texture, band and fade masks. Fade-in is 0.08 seconds;
the smooth fade-out lasts 0.45 seconds.
Leaves permit detection; solid occupied voxels conservatively suppress the full effect.
Looking away fades a glimpse within 0.36 seconds. Continued viewing never repeats it:
leave the wider attention area for 0.3 seconds, then observe again for two seconds.
Releasing/re-holding zoom alone does not rearm it. No hearing, night-vision or goblin Clamber changes.

Client commands (dot prefix): `.rfwatchtest` is moving-target testing only;
`.rfwatchpreview` arms synthetic awareness at 10/25/38 metres; `.rfwatchglimpse`
arms a real focused-target glimpse with observation time and look-away gate skipped once. Close chat
within ten seconds; hold racial zoom for the glimpse. Previews are not normal
detection validation. Commands write bounded rendering traces to the client log.
Disable with Ctrl+H, or `EnableElfWatchfulness=false` in existing config and relaunch.
Default radius 40, movement cooldown independently 5–15 seconds per target,
`WatchfulnessObservationSeconds=2`. Visual acceptance and performance are unverified.

**Summary:** Race-specific abilities and survival mechanics, from elven climbing and dwarven Ore-Song to Orc Thew and goblin scavenging.

### Development and AI use

**I've been a developer for about five years, and for the last two years I've worked closely with advanced AI models.** C# is a language I have much less experience with, so I use LLM coding tools to supplement my knowledge of the language and Vintage Story's modding API.

That includes generating and explaining code, researching implementation options, investigating errors and helping with documentation. AI has a substantial role in this project's development. I choose what goes into the mod, playtest as development progresses, and handle release decisions and maintenance. That does not mean every situation has been tested. The source is available for anyone who wants to inspect it, contribute or make their own version.

### What it does

RF Mechanics is the gameplay companion to Race Framework. It gives races different ways to explore, gather and survive.

- **Every race, including humans:** step up a full block without jumping — two for elves. Low ceilings and openings too small to fit through still stop you.
- **Dwarves:** mining bonuses that vary with depth, plus **Ore-Song**. Sit beside stone or ore, empty your main hand, and press Race Ability (default R) while aiming at a wall within two blocks. Settle, knock, and listen for distant mineral voices with broad directional cues. Standing or moving ends the listen. Placed ore sings too.
- **Elves:** step up two blocks rather than one — always on, with no stance to hold or key to press, and the same ceiling and clearance limits as everyone else. They also move through branchy leaves, climb log-grown trees, gain tree-proximity movement, reduced fall damage, leaf gathering, zoom, and 15% lower hunger. Climbing follows the way you are moving and carries you around the outside edge of a trunk rather than dropping you. Chiseled-log climbing is an open diagnostic investigation, not a proven shipped fix.
- **Orcs:** maintain **Thew** through feeding, with changing body size and physical capabilities. Frenzy offers a burst of speed with recovery costs. Hold scent to locate creatures: walking retains a weaker partial sense, while standing still builds full quality.
- **Goblins:** eat rot to build an aura that accelerates nearby food spoilage, earn spit charges for block repair, and show separate aura and charge fly systems. They tunnel, take less fall damage, and mine stone/Ore at 0.4x. Tree trunks climb freely; rock and dry earth (soil, packed dirt, bony soil, cob, forest floor) need the Clamber stance on Ctrl+H, which starts off and stays as you leave it. Darkvision is optional and disabled by default.

Elf attunement and goblin digging/spit-packed material conversion are not active mechanics. The elf harvest multiplier remains unwired. Mechanics and balance values are configurable; development and in-game acceptance are ongoing.

### Installation

For the standard setup, install Race Framework and its dependencies, then RF Mechanics on both server and client. Mechanics activate through the relevant race traits. Look for the race ability binding in the game's Controls menu.

Diet Setup is an optional companion for race-specific food rules.

### Existing worlds — untested

I have not tested adding this mod to an existing world. No new-world requirement is currently known, but compatibility is not guaranteed.

**Installing on an existing save is at your own risk. I am not responsible for problems, lost progress or save damage resulting from doing so.** Make a full backup and test on a separate copy first. Keep the original backup: removing the mod does not necessarily undo saved changes.

RF Mechanics saves character state and can change food freshness and repaired objects. Uninstalling does not restore those changes. Custom blocks already present in a save also depend on the mod's definitions.

### Ideas for future updates

These are directions I would like to explore, subject to design work and playtesting:

- **Goblin reclamation:** recovering useful parts from worn-out tools, damaged objects and picked-over ruins.
- **Dwarven hearths and feasts:** making settled homes and shared meals part of dwarven life.
- **Elven cultivation:** selecting and growing plants across generations.
- **Gnomish mechanisms:** compact devices, traps, timers and controls.
- **Life around water:** wetlands and fishing for Frogs; diving and underwater exploration for Merpeople.
- **Dragon-kin windworks:** capturing wind power and building around exposed terrain.

These are not features in the current download or promises for the next update. More work on existing mechanics will continue alongside new features and races.

### Source, permissions and credits

Original work is MIT-licensed. Forks, modifications and contributions are welcome; retain the included copyright and license notices. Vintage Story-derived textures and sound material remain under Anego Studios' applicable terms. See the included third-party notices for the details.

Thanks to **Fuami's Spyglass** for the FOV implementation reference, **123Gurkensalat's Scaffolding** for climbing/collision research, **Algorytmiczny's More Bugs** for rot-fly inspiration, and **Xandu and El_Neuman's xSkills work** for mechanics references. Thanks also to **Anego Studios** for Vintage Story and its modding tools. Detailed attribution is included with the mod.

[Source code](https://github.com/Miles-Johnson/rfmechanics) · [Report a problem](https://github.com/Miles-Johnson/rfmechanics/issues)


## Build and contribute

Use `Build.ps1` to build/package locally without installing. See [RELEASING.md](RELEASING.md) for development branches, clean candidate builds, testing and the explicit publication gate. Original work is MIT; retain the [third-party notices](THIRD_PARTY_NOTICES.md).
