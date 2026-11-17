# 3D Flood Fill audio system

The demo scene is ready to play: `Assets/FloodFill/Scenes/FloodFill3DDemo.unity` contains one `FloodAudioManager` prefab instance. The prefab and mixer live in `Assets/FloodFill/Audio/`. Starter clip references use the existing sounds in `Assets/Sounds`; no new sound files or dependencies were added. All clip choices are serialized Inspector assignments. There are no runtime asset paths, Resources loads, or per-cube AudioSources.

## Assign and audition clips

1. Before Play Mode, select **FloodAudioManager** in the demo hierarchy, or open its prefab. Assign the four music tracks and expand **UI Sound Effects** / **Gameplay Sound Effects** for individual cues. Each cue has clip, alternates, volume, pitch, small random pitch range, retrigger interval, instance cap, and fade tuning.
2. During Play Mode, select **DontDestroyOnLoad > FloodAudioManager**. Edit that runtime instance, then press **Refresh / Reapply Audio Settings**. Changed music assignments crossfade; unchanged music keeps playing at its current position. Volumes, routing, active cue pitch/volume, thresholds, flood limits, and pool size are reapplied. Existing one-shot effects finish their current clip; later events use new assignments.
3. Use the Inspector buttons for menu/normal/medium/hard music, button/color/flood/large-capture/completion/failure cues. **Return To Game Music** or **Refresh** ends a music preview and follows the actual game again. Previews do not alter moves, board state, or the solver.
4. **Stop All Audio** fades music and active effects. Music remains stopped until Return/Refresh, a new level, or a menu/game context change. Music and SFX enable flags are independent of their stored volumes.

The supplied prefab has 12 pooled SFX sources plus exactly two music sources, all on the same persistent GameObject. Sources are created once at initialization. Changing pool size explicitly with Refresh grows the pool or fades retiring voices before shrinking it. No AudioSource is attached to a cube or added during a flood event.

Inspector diagnostics show music state/clip, game state, remaining moves, starting budget, music/SFX levels, crossfading, active SFX, and playback-start counts. Audio variation uses a dedicated random generator and does not consume Unity's gameplay random stream.

## Existing Settings screen

The current navy/blue panel, typography, current-block option, and navigation remain. The settings content is spaced to include Master, Music, and Sound Effects sliders with percentage labels, plus Music Enabled and Sound Effects Enabled toggles. No second settings menu was created.

Values apply immediately through exposed mixer parameters. Muting preserves the selected volume. Initialization and refresh use `SetValueWithoutNotify` / `SetIsOnWithoutNotify`. Sliders do not write to disk on every drag sample: values are saved when settings close, on focus loss/backgrounding, on orderly shutdown, and when the owning manager is destroyed. Keys are `FloodFill3D.Audio.Master`, `.Music`, `.Sfx`, `.MusicEnabled`, and `.SfxEnabled`.

## Routing and music policy

`FloodFillAudio.mixer` is a real Unity-authored asset:

- **Master**: exposed `MasterVolume`; compressor threshold -3 dB, attack 5 ms, release 100 ms, zero makeup gain.
- **Master / Music**: exposed `MusicVolume`, routed from both music sources.
- **Master / SFX**: exposed `SfxVolume`, routed from the reusable SFX pool; compressor threshold -10 dB, attack 5 ms, release 100 ms, zero makeup gain.
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

`VoxelBoardManager3D` emits one compact event after its existing six-neighbor recolor and capture logic. It reports affected/newly captured counts, the existing animation duration, actual last wave start, and existing wave-step delay. Added bookkeeping tracks timing only; the recolor algorithm and animation calls/durations are unchanged.

`Block Color Change` is the **Flood Start** cue. Defaults allow four quiet progress events, spaced by at least 0.12 real seconds and quantized to actual animation wave boundaries, with at most two simultaneous flood voices. Progress follows scaled animation time and therefore pauses with the visuals at `timeScale = 0`; the retrigger safety floor remains real-time. Size adds only modest capped volume/pitch variation. There is no sound per voxel. A small outside component merged into an already captured area counts only the union of recolored and newly captured cubes; unchanged captured mass does not inflate its sound intensity. Timing excludes non-animated capture bookkeeping. Short cue attacks play at their configured gain immediately rather than waiting for a frame to fade in; cue fade duration controls release and tails.

An assigned Flood End or Large Capture cue can finish the wave. A final win/failure cue replaces extra flood finishing feedback on the last move. Large Capture uses the actual newly captured count (default threshold 30). If the pool is busy, a finishing cue briefly waits for fading flood voices rather than exceeding the cap. Assign only a start clip and leave progress/end/large clips empty to use a single-clip design.

Accepted color changes emit exactly one color cue. Invalid or repeated selections do not. Palette controls suppress generic UI clicks. Level start, result, restart, and pause/resume hooks guard duplicate notifications. Result cues wait for the current flood animation to finish; restarts/menu transitions cancel old wave schedules. The game's existing pause behavior, win animation, input, rendering, camera, level generation, and solver are unchanged.

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
- `FloodAudioEffects.cs` — the same partial manager's SFX pool and wave scheduler.
- `FloodAudioSettings.cs` — serializable cue tuning, isolated audio random source, volume and intensity policy.
- `FloodAudioSignals.cs` — small typed gameplay/navigation hooks.
- `FloodAudioUIBinder.cs` — central listener ownership and dynamic uGUI binding.
- `FloodAudioUIOverride.cs` — optional per-control exception.
- `Assets/FloodFill/Scripts/ThreeD/FloodFillMobileUIAudio3D.cs` — existing settings UI's added audio controls.

Created Editor scripts: `Assets/FloodFill/Editor/FloodAudioSetup.cs` and `FloodAudioVerification.cs`. Their Unity authoring/reflection and testing APIs are inside Editor-only code. Runtime integration uses typed calls/events, not reflection.

Created assets: `Assets/FloodFill/Audio/FloodFillAudio.mixer` and `FloodAudioManager.prefab`, with metadata. Modified scripts: `FloodFillGameManager3D.cs`, `VoxelBoardManager3D.cs`, and `FloodFillMobileUI3D.cs`. The Editor-only `FloodFill3DDemoCreator.cs` also retains the configured audio prefab when regenerating the demo; its board/material/camera creation logic is unchanged. The demo scene changed only by adding its audio prefab instance/root. No solver file was modified.

**Tools > Flood Fill > Audio > Create or Repair Audio Assets** repairs mixer/prefab routing while preserving existing clip assignments. If recreating a missing prefab, assign its new empty clip fields through the Inspector. **Install Audio Prefab In Open Scene** adds a root instance if absent and marks that scene dirty; save it normally. These tools are optional for the already configured demo. No audio asset creation or asset searching occurs during gameplay.

## Verification and remaining limits

Native Unity Play Mode verification covers startup loops/routing, actual generated Play button callbacks, repeated binding, exact threshold boundaries, hard priority, unchanged-track playback, interrupted power-bounded fades, live reassignment/refresh, real settings sliders, mute/volume preservation, saved startup settings, valid/invalid palette selections, an actual voxel recolor handler, flood voice/progression caps, paused wave versus unscaled music/UI behavior, pool refresh, duplicate result/pause notifications, rapid real restarts, missing/alternate clips, additive-scene persistence, duplicate-manager rejection, and app pause/resume. Audio variation is checked against Unity's random state. The recorded run passed 153 assertions, including checks during each interrupted-fade/progression step; the exact count can vary with generated wave duration. See `Documentation/Audio/play-mode-verification.txt`.

Android and iOS checks use Unity's `PlayerBuildInterface.CompilePlayerScripts`, with Editor folders excluded, rather than merely compiling against desktop Editor definitions. Both Android and iOS compiled 63 player assemblies successfully. See `Documentation/Audio/mobile-player-compilation.txt`. These checks compile managed player scripts; they do not produce signed APK/IPA packages, exercise IL2CPP/native linking, or verify device audio latency.

The tests use a temporary project copy, separate company/product settings, and isolated preferences/session data. They do not consume or overwrite the player's saved level. The runtime test methods never alter game counters. Tests are headless, so final listening, music-loop seam quality, perceived cue balance, device performance, and speaker/headphone checks remain human/device QA. Starter clip choices are editable examples, not newly authored success/failure sounds.

To rerun in an isolated Unity project copy:

```sh
Unity -batchmode -nographics -projectPath /path/to/copy -executeMethod FloodFill.EditorTools.FloodAudioVerification.BeginBatch -logFile /tmp/flood-audio-playmode.log
Unity -batchmode -nographics -projectPath /path/to/copy -quit -executeMethod FloodFill.EditorTools.FloodAudioSetup.CompileMobilePlayers -logFile /tmp/flood-audio-mobile.log
```

The Play Mode runner exits with success/failure and writes `Logs/FloodAudioVerification.txt`; do not add `-quit` to that asynchronous Play Mode command. Player compilation writes `Logs/FloodAudioPlayerCompilation.txt`.
