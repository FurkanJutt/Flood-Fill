# Loading and move preparation

Level launches first show an opaque loading screen and disable camera, palette, and cube input. Pool cleanup, cube acquisition, and color setup yield after approximately 4 ms of main-thread work per batch. Individual Unity operations and logical shape generation can exceed that interval; this is cooperative scheduling, not a hard frame-time guarantee. The synchronous board-generation API remains available for existing Editor tools and verification.

After board setup, the main thread captures an immutable solver snapshot. A single background worker builds the region graph, runs the solver, validates the solution, and performs a guaranteed fallback if needed. Rapid restarts cancel old work and a semaphore prevents overlapping searches. The fallback also receives cancellation. Graph construction scans the existing logical occupancy map instead of sorting voxel indices, reads snapshot arrays once, and discovers each adjacency only in the three positive directions. Region IDs and sorted adjacency are preserved.

Normal gameplay, including Editor Play Mode and development builds, uses the same bounded search settings as release builds. Longer development budgets remain available to Editor analysis tools. The refinement budgets are per phase rather than a hard total timeout; initial monotone completion and validation still run to establish a valid solution. Shorter gameplay searches may find longer solutions than deeper analysis, and their difficulty budgets derive from those validated results. Optimality is claimed only when proven by the existing solver.

The prepared result is applied and saved while loading is still visible. The hidden HUD updates and warms its text meshes. The complete board is activated behind the loading screen for a frame and its physics transforms are synchronized. After that preparation frame, loading remains visible for an additional 0.5 seconds of unscaled time. Only then does gameplay appear and play the existing spin/pop entrance and level-start sound. The entrance keeps input locked until its own completion. There is no artificial percentage; the additional delay follows actual readiness rather than replacing it.

Play, custom Start, next level, restart, and Continue share this readiness gate. Back cancels preparation and prevents stale task results from revealing gameplay. Cancelling Continue retains the saved board and budget. A failed board setup provides Retry and Back controls.

## Verification

The native Unity Play Mode run passed 723 assertions, including the largest selectable shell board assembling across frames, a validated solver result returned from another managed thread, loading/HUD visibility, deferred entrance sound, input locks, rapid restarts, new-load cancellation, cancelled Continue preserving progress, and successful relaunch. Counts vary with frame timing. See [Play Mode results](loading-play-mode.txt).

A separate .NET benchmark compared the old graph from revision `36dfdfd` with the optimized graph. All 60 graphs matched region IDs, colors, voxel membership/counts, starting region, connectivity, representatives, and sorted CSR adjacency across full and shell boards. Cancellation and all benchmark solver results were also verified. These are standalone CPU measurements, not Unity frame timings or device FPS.

| Full board | Original graph | Optimized graph | Gameplay solve | Deeper analysis solve |
|---|---:|---:|---:|---:|
| 6³ | 0.052 ms | 0.049 ms | 165 ms / 10 moves | 554 ms / 10 moves |
| 12³ | 0.705 ms | 0.589 ms | 440 ms / 17 moves | 948 ms / 16 moves |
| 20³ | 7.988 ms | 6.263 ms | 815 ms / 24 moves | 1,691 ms / 21 moves |

Each graph timing averages 100 warmed builds; solver timing is a single bounded run and varies with scheduling. On the 8,000-voxel graph, managed allocation fell from 1,229,224 to about 1,181,069 bytes per build. See [raw benchmark](loading-solver-benchmark.txt).

Run the existing native regression in an isolated Unity project copy:

```sh
Unity -batchmode -nographics -projectPath /path/to/copy -executeMethod FloodFill.EditorTools.FloodAudioVerification.BeginBatch -logFile /tmp/flood-loading.log
```

The verification isolates preferences and saved data. Full target-device loading smoothness and rendered FPS remain device QA; no signed APK/IPA is produced by managed compilation checks.

Double tap now smoothly restores both default zoom and the original board center, retaining the current viewing angle. It also recenters a panned view when zoom was already normal. New pan or zoom input interrupts the reset animation.
