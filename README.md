# RF Mechanics

Prerelease candidate for Vintage Story 1.22.6. In-game acceptance is still pending.

Local Orc hunting trial **0.1.3-orchunt.1**, cumulative on Watchfulness revision 9.
Orc Ctrl+H (saved Race Stance rebind retained) toggles occasional nearby whiffs.
Hold Race Ability (default R or saved rebind) for a quick sniff; continue holding
for deeper focus. Walking keeps the sniff active; sprint/jump/hurt shed deep focus
and allow automatic recovery. Stance starts off each session. No species HUD labels.

Fresh blood uses pointed red droplets with a tighter bearing even nearby. Server
pursuit requires actual BloodTrail or The Hunter bleeding state, including players
when the provider applies bleeding to them. Running toward the target builds up to
+20% walkspeed over six seconds; two-second grace then three-second decay. Combined
Frenzy and pursuit contribution is capped at +35% (other movement modifiers remain).
Changing prey resets charge. A brief discovery impression announces engagement,
with no repeat during an episode and a 15-second minimum between announcements.
Target swimming suppresses blood scent until it emerges still bleeding. Orc swimming
is independent. No terrain obstruction or scent pathfinding is implemented.

The sparse Diet test profile has neither bleeding provider: sensory/controls testing
is available, blood pursuit is inactive. No provider is bundled or installed here.
`.rfhunttest` reports real adapter/blood/focus state without creating test bleeding.
Use `Orc*` properties in rfmechanics.json to tune this trial; defaults leave the saved
older smell timing/range properties untouched. Config and saves need no migration.
The 100 ms quick threshold follows a 50 ms input settle and 50 ms emission cadence;
standing reaches full focus at four seconds, walking reaches half focus in about six.
Base quick/deep/passive ranges are 24/64/20 blocks, multiplied by body size 0.7?1.2
and capped at 64. Blood range is 40 blocks, independent of focus and stance.
Whiffs last 0.65 seconds, every 3.4?4.6 seconds; held focus darkens by at most 35%.

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
