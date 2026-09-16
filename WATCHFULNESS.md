# Watchfulness cue prototype — 2026-09-16

Status: needs player test. Isolated prototype over accepted dev.11; no deployment.

Elf **Race Stance (Clamber / Watchfulness)** toggles Watchfulness, default Ctrl+H.
The existing `rfclamber` binding is preserved. Held Race Ability (default R) still
controls telescopic zoom independently. Goblin Clamber retains its saved lifecycle.
Watchfulness starts off and clears on death, disconnect and leaving elf; its server
state is never serialized. Race invalidation is checked every 100 ms on the server.

Settings in `ModConfig/rfmechanics.json` (relaunch after editing):

| Setting | Default | Clamp |
| --- | --- | --- |
| EnableElfWatchfulness | true | Server activation gate and client presentation gate |
| WatchfulnessRadius | 20 blocks | 4–32 |
| WatchfulnessCooldownSeconds | 2 seconds | 0.5–10 |
| WatchfulnessMinimumSpeed | 0.2 blocks/second | 0.05–5 |
| WatchfulnessDiagnostics | false | Opt-in five-second client log counters |

Client-side positions use Entity.Pos / InternalY, the renderers' translation source.
Sample every 100 ms, normalize by elapsed time, reject displacement below 0.025 blocks,
gaps over 0.5 s, jumps over 2 blocks, or speed over 15 blocks/s. At exactly 100 ms,
the jitter floor makes the effective minimum speed 0.25 blocks/s. No intent flags,
animation transforms, footsteps or foliage effects are used. Players exclude self;
fauna uses the existing harvestable + creatureDiet classifier. Some modded animals
without those attributes are excluded; tiny fauna are not separately tuned.

The pale feathered billboard is 0.24 blocks across and fades over 0.5 seconds at a
fixed world point. No names, health or tracking. Both ends of a sampled interval
must be on screen. Samples advance even when offscreen/blocked/budget-limited;
there is no replay queue. Leaving view or losing visibility permanently retires a cue.
Camera matrices and the current projection are used every frame, including zoom.

Visibility is an explicit voxel ray policy: air, leaves, plants, water, lava and fire
pass; all other materials (including unknown materials, glass, slabs and chisel
blocks) occupy their entire voxel for blocking. Missing chunks fail closed. Five rays
check the center and billboard corners at emission and every live rendered frame.
GPU depth testing is disabled only for the explicitly checked billboard, allowing
leaves to pass. Thin gaps and partial blocks are intentionally conservative; this is
not geometric shape visibility. No claim of appearance or shader runtime validation.

Bounds: 1 s spatial-index refresh; at most 256 creature visits per query, 64 retained
candidates, 64 samples per 100 ms, 8 emission visibility checks per sample, 8 live
cues (8 visibility checks/frame). Each visibility check has at most 5 rays of 128
voxel visits. Query radius is bounded before walking the existing spatial index,
with no all-entity scan or intermediate nearby array. Dense scenes can omit targets
beyond the fixed query/candidate limits; sampling rotates emission priority. The
stock partition query is oriented to the main world; alternate dimensions are not
a supported detection scene in this prototype. Dimension changes clear old state.

Turn off with the same stance key. For a persistent disable set
`EnableElfWatchfulness=false` on client and server, then relaunch normally. Revert
the package to the recorded dev.11 artifact using the existing local packager; do
not add a second RF Mechanics ZIP alongside the installed folder. No world migration
or cleanup is required. Existing saves, profiles and configs were not changed.

Player checks: moving/stopped/turning animals; a moving/stopped remote player;
knockback; movement behind leaves versus a wall; step behind a wall while a cue is
alive; turn toward movement that already stopped; move camera and hold zoom; crowded
animals; toggle off/death/rejoin/change race; regress goblin Clamber and held zoom.
Server and client both need this prototype for activation; no remote deployment is
authorized. After appearance approval, investigate reliable offscreen footsteps
before hearing multipliers. Hearing, bow accuracy, night vision and observer-speed
distance scaling are intentionally deferred by the prototype request.
