# 3D Flood Fill audio system

The demo scene is ready to play: `Assets/FloodFill/Scenes/FloodFill3DDemo.unity` contains one `FloodAudioManager` prefab instance. The prefab and mixer live in `Assets/FloodFill/Audio/`. Starter clip references use the existing sounds in `Assets/Sounds`; no new sound files or dependencies were added. All clip choices are serialized Inspector assignments. There are no runtime asset paths, Resources loads, or per-cube AudioSources.

## Assign and audition clips

1. Before Play Mode, select **FloodAudioManager** in the demo hierarchy, or open its prefab. Assign the four music tracks and expand **UI Sound Effects** / **Gameplay Sound Effects** for individual cues. Each cue has clip, alternates, volume, pitch, small random pitch range, retrigger interval, instance cap, and fade tuning.
2. During Play Mode, select **DontDestroyOnLoad > FloodAudioManager**. Edit that runtime instance, then press **Refresh / Reapply Audio Settings**. Changed music assignments crossfade; unchanged music keeps playing at its current position. Volumes, routing, active cue pitch/volume, thresholds, spread speed/fade, and pool size are reapplied. Existing one-shot effects finish their current clip; later events use new assignments.
3. Use the Inspector buttons for menu/normal/medium/hard music, button/color/flood/completion/failure cues. **Return To Game Music** or **Refresh** ends a music preview and follows the actual game again. Previews do not alter moves, board state, or the solver.
4. **Stop All Audio** fades music and active effects. Music remains stopped until Return/Refresh, a new level, or a menu/game context change. Music and SFX enable flags are independent of their stored volumes.

The supplied prefab has 12 pooled SFX sources plus exactly two music sources, all on the same persistent GameObject. Sources are created once at initialization. Changing pool size explicitly with Refresh grows the pool or fades retiring voices before shrinking it. No AudioSource is attached to a cube or added during a flood event.

Inspector diagnostics show music state/clip, game state, remaining moves, starting budget, music/SFX levels, crossfading, active SFX, and playback-start counts. Audio variation uses a dedicated random generator and does not consume Unity's gameplay random stream.

## Existing Settings screen

The current navy/blue panel, typography, current-block option, and navigation remain. The settings content is spaced to include Master, Music, and Sound Effects sliders with percentage labels, plus Music Enabled and Sound Effects Enabled toggles. No second settings menu was created.

**Show Current Block** remains a visible, explicit settings row with its original saved preference. Opening Settings also repairs a hidden legacy auto-layout row without creating duplicates. After updating scripts during an existing Play session, restart Play Mode once to rebuild the controls and load updated prefab routing.

Music and SFX toggles share the same `Toggle On` / `Toggle Off` cue definitions. The SFX toggle's own short confirmation uses a pooled voice routed to Master, scaled by the stored SFX level, so it can confirm muting without being silenced by that same mute. Master volume still applies; all other gameplay/UI effects mute immediately. No additional source or mixer group is created.

Values apply immediately through exposed mixer parameters. Muting preserves the selected volume. Initialization and refresh use `SetValueWithoutNotify` / `SetIsOnWithoutNotify`. Sliders do not write to disk on every drag sample: values are saved when settings close, on focus loss/backgrounding, on orderly shutdown, and when the owning manager is destroyed. Keys are `FloodFill3D.Audio.Master`, `.Music`, `.Sfx`, `.MusicEnabled`, and `.SfxEnabled`.

## Routing and music policy

`FloodFillAudio.mixer` is a real Unity-authored asset:

- **Master**: exposed `MasterVolume`; compressor threshold -3 dB, attack 5 ms, release 100 ms, zero makeup gain.
- **Master / Music**: exposed `MusicVolume`, routed from both music sources.
- **Master / SFX**: exposed `SfxVolume`, routed from ordinary reusable SFX pool voices; compressor threshold -10 dB, attack 5 ms, release 100 ms, zero makeup gain.
- All groups appear in the mixer's **All** view. Mixer updates use unscaled time. Linear volume converts with `20 * log10(value)` and a safe -80 dB floor for silence.

The audio manager reads `FloodFillGameManager3D.CurrentMoveBudget` and `MoveCount`. It never reads UI text or solver paths. During generation it requests Normal; when gameplay becomes active it uses the authoritative budget and the following exclusive rules:

| Priority | Condition | Track |
|---|---|---|
| 1 | Remaining moves <= hard threshold (default 10) | Hard |
| 2 | Remaining moves <= starting budget × medium fraction (default 0.5), and above hard threshold | Medium |
| 3 | Otherwise, or dynamic music disabled | Normal |

For 35 moves: 35–18 Normal, 17–11 Medium, 10–0 Hard. For 15 moves: Hard takes priority at 10. Inspector percentage is normalized: **0.5 means 50%**.

Menu, setup, how-to, and settings share menu music. Opening a popup does not restart it. Pause and results remain in gameplay context. Music state reevaluates through existing game UI/state notifications and explicit navigation hooks; the game state is not polled in Update. A short transition cooldown coalesces changes; Hard and explicit refresh/context changes bypass it.

Crossfades blend squared gains with the Inspector curve before taking square roots, so combined normalized power stays bounded. The default duration is 1.75 seconds. If another track is requested while both sources are occupied, the quieter one is briefly released before it is reused; a third track is never created. Outgoing gain is held while an incoming background-loaded clip is still loading. The previous source is muted/stopped after its fade. Result music ducks independently without changing the user's saved music volume.

Missing clips are intentional safe cases: Hard falls back to Medium, then Normal; Medium falls back to Normal; missing Normal/menu resolves to silence. Normal never unexpectedly borrows a harder track. Missing back/confirm/cancel/toggle/slider cues fall back to generic Button Click. Other missing gameplay/popup cues are silent. Alternate-only assignments also work.

## Flood and gameplay integration

`VoxelBoardManager3D` emits one compact event after its existing six-neighbor recolor and capture logic. It reports affected/newly captured counts, the existing animation duration, and first/last animated wave starts, including color anticipation. Audio tracking uses two scalar bounds during the existing traversal: no per-cube timing array, sorting, extra traversal, or per-frame cube inspection. Recolor and capture animation calls and durations are unchanged.

**Color Spread** is the single sound assignment for recoloring. The Inspector's **Test Flood Spread** and actual gameplay call the identical playback path, using their respective animation durations. Legacy Progress, End, and Large Capture assignments are retained as hidden serialized data for existing scenes; the gameplay scheduler does not play those layers.

At initialization or an explicit Refresh after clip/loop settings change, a short section of the assigned clip is copied into a small PCM loop with 4 ms softened edges. Original audio assets are unchanged. The supplied preset uses a 0.105-second start offset and 0.16-second loop from the existing bubble clip. Alternate clips are prepared once too, starting at zero. Changing the primary clip clears its old offset; tune the offset/loop section then Refresh. Use Decompress On Load for short spread clips. Streaming clips or clips whose PCM data cannot be read safely fall back to looping the original clip.

One pooled source loops natively throughout the animation. Playback speed rises from 0.8× to 1.65× between the first and last wave starts, then fades smoothly over the last 0.2 seconds (short waves cap the fade to 40% of their duration). Base pitch and volume come from Color Spread. Its generic one-shot retrigger/instance/random-pitch/fade settings do not govern this continuous effect; use the Color Spread speed, loop, and fade controls. The source pauses with scaled visual time, resumes with the animation, and is released at its end. Restarts and menu transitions cancel it. No per-wave clip creation, PCM reads, playback retrigger loop, or extra ending cue occurs during gameplay. Result sounds remain separate and play after the animation.

The loading screen waits for the board, validated move budget, session save, and final HUD/render preparation. An additional 0.5-second unscaled buffer follows readiness. The Level Started cue begins with the entrance when loading closes. The board makes one quick 360-degree spin while popping from 82% scale to its resting scale (default duration 0.55 seconds). Its exact resting rotation/scale are restored at completion. Color buttons, direct color selection, cube clicks, orbit, pan, zoom, and zoom reset stay locked throughout the entrance; gameplay also waits for the solver to be ready. Completion, cancellation, and rapid restart update the shared lock rather than relying on a separate input timer. Pausing cannot bypass the lock. Inspector clip assignments are retained.

When Play/Start confirmation and Level Started share a clip, the existing same-frame guard prevents doubling it. A successfully played confirmation during asynchronous level calculation also satisfies a matching deferred start cue for that launch. Different clips remain distinct. New-level resets clear launch tracking.

Accepted color changes emit exactly one color cue. Invalid or repeated selections do not. Palette controls suppress generic UI clicks. Level start, result, restart, and pause/resume hooks guard duplicate notifications. Result cues wait for the current flood animation to finish; restarts/menu transitions cancel old wave schedules. The entrance adds a temporary input lock; the existing win animation, rendering, level generation, and solver remain unchanged.

Standard uGUI Buttons, Toggles, and Sliders are bound centrally, without modifying prefabs individually. Existing generated button factories bind immediately. A reusable-buffer scan of uGUI's selectable registry also discovers other dynamic controls; it performs no per-frame scene hierarchy search. Destroyed controls are pruned, listeners are not duplicated, and original callbacks are retained. Named Back/Confirm/Cancel controls receive their role; generic clicks are the default. `FloodAudioUIOverride` is optional for unusual controls or explicit silence. Identical UI clip requests in one frame are suppressed, and slider feedback is throttled.

App backgrounding pauses only this manager's playing sources and freezes its transitions. Resuming unpauses those sources; it does not recreate them or change global listener settings. Crossfades, volume controls, UI effects, and fade-outs use unscaled time.

## AudioClip import recommendations

These are starting points; listen on target devices and check the Audio Profiler. Unity documents Streaming as a low-memory option for continuous audio, Decompress On Load for small effects, and PCM/ADPCM/Vorbis tradeoffs in its [Audio Clip Import Settings reference](https://docs.unity.com/en-us/engine/6000.6/manual/audio/reference/class-audio-clip).

| Use | Load type | Compression / quality | Channels and preload |
|---|---|---|---|
| Background music | Streaming | Vorbis, start around 65–80% | Preserve stereo; background loading on; preload off |
| Short UI effects | Decompress On Load | Vorbis around 80%; PCM is an option for tiny cues | Mono if stereo is unnecessary; preload on |
| Short flood effects | Decompress On Load | Vorbis around 80%; consider ADPCM for noisy repeated textures | Usually mono; preload on |

Preserve source sample rate initially; reduce it only after listening. Use loop-ready music assets and leave headroom in source material. The eight import files changed by setup belong only to the four assigned music clips and four assigned short effects; other import settings and all audio-file contents are unchanged. The included music imports stream rather than decompressing every assigned track. Inactive music sources release their clip reference after fading out.

## Files and setup tools

Created runtime scripts:

- `Assets/FloodFill/Scripts/ThreeD/Audio/FloodAudioManager.cs` — ownership, settings, game events, Inspector controls and diagnostics.
- `FloodAudioMusic.cs` — the same partial manager's two-source music and crossfades.
- `FloodAudioEffects.cs` — the same partial manager's SFX pool and continuous spread playback.
- `FloodAudioSettings.cs` — serializable cue tuning, isolated audio random source, volume and intensity policy.
- `FloodAudioSignals.cs` — small typed gameplay/navigation hooks.
- `FloodAudioUIBinder.cs` — central listener ownership and dynamic uGUI binding.
- `FloodAudioUIOverride.cs` — optional per-control exception.
- `Assets/FloodFill/Scripts/ThreeD/UI/Mobile/FloodFillMobileUIAudio3D.cs` — existing settings UI's added audio controls.

Created Editor scripts: `Assets/FloodFill/Editor/FloodAudioSetup.cs` and `FloodAudioVerification.cs`. Their Unity authoring/reflection and testing APIs are inside Editor-only code. Runtime integration uses typed calls/events, not reflection.

Created assets: `Assets/FloodFill/Audio/FloodFillAudio.mixer` and `FloodAudioManager.prefab`, with metadata. Modified scripts: `FloodFillGameManager3D.cs`, `VoxelBoardManager3D.cs`, and `FloodFillMobileUI3D.cs`. The Editor-only `FloodFill3DDemoCreator.cs` also retains the configured audio prefab when regenerating the demo; its board/material/camera creation logic is unchanged. The demo scene changed only by adding its audio prefab instance/root. No solver file was modified.

**Tools > Flood Fill > Audio > Create or Repair Audio Assets** repairs mixer/prefab routing while preserving existing clip assignments. If recreating a missing prefab, assign its new empty clip fields through the Inspector. **Install Audio Prefab In Open Scene** adds a root instance if absent and marks that scene dirty; save it normally. These tools are optional for the already configured demo. No audio asset creation or asset searching occurs during gameplay.

## Verification and remaining limits

Native Unity Play Mode verification covers startup loops/routing, actual generated Play button callbacks, repeated binding, exact threshold boundaries, hard priority, unchanged-track playback, interrupted music fades, live reassignment/refresh, real settings controls, saved startup settings, valid/invalid palette selections, actual voxel recolor handlers, pause synchronization, pool refresh, duplicate result/pause notifications, rapid restarts, missing/alternate clips, additive-scene persistence, duplicate-manager rejection, and app pause/resume. A deterministic 33-voxel spread verifies a single continuously playing source, increasing pitch, fade before the animation ends, no extra sound layers, and an identical prepared loop in Inspector preview and gameplay. The recorded run passed 723 assertions; the exact count varies with frame timing. See `Documentation/Audio/play-mode-verification.txt`.

The same headless Editor run measured spread scheduling at 0.194 microseconds per call and active spread updates at 0.270 microseconds per call, with zero managed bytes allocated over 10,000 calls to each. Loop preparation is excluded because it runs only during initialization/explicit settings changes. These are isolated CPU measurements, not a rendered FPS or device-audio benchmark. The Unity Profiler markers `FloodAudio.StartSpread` and `FloodAudio.UpdateSpread` identify this work in a real gameplay capture; cube tween/material updates and GPU rendering remain separate costs. This measurement does not establish the cause of a previously observed full-game FPS drop.

Android and iOS checks use Unity's `PlayerBuildInterface.CompilePlayerScripts`, with Editor folders excluded, rather than merely compiling against desktop Editor definitions. Both Android and iOS compiled 63 player assemblies successfully. See `Documentation/Audio/mobile-player-compilation.txt`. These checks compile managed player scripts; they do not produce signed APK/IPA packages, exercise IL2CPP/native linking, or verify device audio latency.

The tests use a temporary project copy, separate company/product settings, and isolated preferences/session data. They do not consume or overwrite the player's saved level. The runtime test methods never alter game counters. Tests are headless, so final listening, music-loop seam quality, perceived cue balance, device performance, and speaker/headphone checks remain human/device QA. Starter clip choices are editable examples, not newly authored success/failure sounds.

To rerun in an isolated Unity project copy:

```sh
Unity -batchmode -nographics -projectPath /path/to/copy -executeMethod FloodFill.EditorTools.FloodAudioVerification.BeginBatch -logFile /tmp/flood-audio-playmode.log
Unity -batchmode -nographics -projectPath /path/to/copy -quit -executeMethod FloodFill.EditorTools.FloodAudioSetup.CompileMobilePlayers -logFile /tmp/flood-audio-mobile.log
```

The Play Mode runner exits with success/failure and writes `Logs/FloodAudioVerification.txt`; do not add `-quit` to that asynchronous Play Mode command. Player compilation writes `Logs/FloodAudioPlayerCompilation.txt`.

### Inspector lifecycle regression

The Inspector diagnostic getters tolerate empty/uninitialized arrays, invalid music indices, null slots and destroyed AudioSources. They do not initialize or add sources while the Inspector reads them. The focused Unity check passed 23 cases using the same property-reflection access as NaughtyAttributes, including reads before Awake. Run **Tools > Flood Fill > Audio > Verify Inspector Diagnostics** outside Play Mode to repeat it. Evidence: `Documentation/Audio/inspector-diagnostics-verification.txt`.

Loading preparation and solver scheduling are documented in [Loading and move preparation](Performance/LoadingAndMovePreparation.md).
