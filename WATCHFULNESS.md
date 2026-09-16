# Current prototype: awareness and discovery, 2026-09-16

Updated: 2026-09-16. Version 0.1.3-watchfulness.7. Player acceptance pending.

The README and current source describe revision 7. Earlier revision notes below
are historical and do not define this candidate. The canonical task handoff is
`notes/race-mechanics/session-handoffs/2026-09-16-watchfulness-discovery-codex.md`
in the workspace, with exact build/install identities and the player checklist.

Observation uses the central ellipse with NDC half-axes 0.48 / 0.55, or roughly
48% of screen width and 55% of screen height. Existing glimpses tolerate 0.62 / 0.68.
Default observation is two seconds (WatchfulnessObservationSeconds); unfocused
progress decays at two seconds per second. Zoom release clears progress. Each
glimpse lasts at most 0.75 seconds; looking away starts an irreversible 0.12-second
fade. Another glimpse needs a fresh observation period. Limit: eight focused
centre-ray checks per sample and two simultaneous glimpses.

Actual opaque entity meshes, texture alpha and live animation matrices are borrowed
read-only from EntityShapeRenderer, including PlayerModelLib's subclass family.
The shader clips to expanded selection bounds and erases broad bands. Unsupported
renderers or unready meshes are skipped; no generic shape substitute is shown.
Solids in the conservative camera-to-clip-volume hull suppress the entire effect.
This can over-suppress near terrain; it avoids single-ray wall leakage. Missing
chunks and the shared 8192-voxel/frame budget fail closed. Leaves/plants/fluids/fire
pass. Partial/chiselled solids and glass occupy a fully blocking voxel. Very large
animated extremities outside the clip box are omitted; shader-specific warping,
separately rendered held items and alternate-dimension partition queries are not
covered. Runtime shader compilation, alignment and performance need player testing.

Awareness is a fixed world-space pale wisp, 0.9 seconds, base width 1.3 blocks and
minimum projected billboard width 28 framebuffer pixels; feathering occupies less
than that full rectangle. It does not follow the target. Synthetic previews bypass
range fading and motion, while normal awareness retains 5-block near suppression,
40-block default range and independent 5–15-second cooldowns. Discovery also includes
living EntityAgent creatures without the ordinary fauna classifier's creatureDiet.

## Historical revision notes
> Revision 2 (2026-09-16): supersedes presentation/settings below. Radius defaults
> to 40 (clamped 10â€“64); saved local radius is updated during installation. No cues
> within 5 blocks, fade-in over 5â€“12, fade-out across the outer 20% of the radius.
> Muted broken grey-green streak, 0.44-block billboard independent of creature size,
> quick 0.08-second onset then fade, total 0.8 seconds. Per-target initial eligibility
> jitter 0â€“1.2 seconds; each emitted notice draws a fresh cooldown uniformly from
> WatchfulnessCooldownMinimumSeconds=5 to WatchfulnessCooldownMaximumSeconds=15.
> Old WatchfulnessCooldownSeconds is retired. No queued delayed motion: fresh visible
> displacement is required after eligibility. Sampling remains shared at 100 ms,
> but targets no longer share an identical cooldown. DDA cap is now 192 voxels/ray.
> Render state is captured before mesh operations and restored with a using/finally
> scope: buffer-zero blend enable/factors/equations, depth enable/write mask, GL
> program, VAO and vertex/index buffers. No global blend helper, engine shader
> Stop/Use, texture, colour-uniform, framebuffer or cull-state mutation is used.
> Chat symptom has a concrete state-leak fix; visual resolution still needs Miles.
> Hearing investigation uses existing offscreen sounds first, per the later handoff.
# Watchfulness cue prototype Ã¢â‚¬â€ 2026-09-16

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
| WatchfulnessRadius | 20 blocks | 4Ã¢â‚¬â€œ32 |
| WatchfulnessCooldownSeconds | 2 seconds | 0.5Ã¢â‚¬â€œ10 |
| WatchfulnessMinimumSpeed | 0.2 blocks/second | 0.05Ã¢â‚¬â€œ5 |
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
