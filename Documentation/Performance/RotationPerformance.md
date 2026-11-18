# Cube rotation rendering

The voxel color property blocks kept every cube on an individual render path with the existing Flat Kit shader. Its classic GPU instancing variant does not declare `_BaseColor` and `_ColorDim` as per-instance properties, and persistent property blocks also prevent SRP batching. Camera rotation exposes this draw-submission cost on large boards.

Steady cubes now share a cached material per authored source material and palette color. The existing shader, shaded-color calculation, model, geometry, and animation timings are preserved. Property blocks exist only during color interpolation and are cleared when the wave completes or is interrupted by a new steady color. Pool reuse clears overrides and reuses the source palette; animation frames do not create materials. Renderer material-slot bindings and color property IDs are cached once per cube.

The camera reuses its pointer event data, checks UI once per active mouse update, and skips UI raycasting while idle. Touch UI blocking remains in place.

## Graphics measurement

Unity 6000.5.8f1, Apple M1 Pro, the project's URP pipeline, original voxel prefab and Flat Kit shader. Each case uses 16 warmup frames and 32 measured frames, rotating 13 degrees per frame around the board at a 25-degree pitch. The 512×512 render request is followed by a synchronous GPU readback wait. These are comparative rendering costs, not complete player FPS or on-device frame timings. Both paths use the same scene, lighting, colors, geometry, and camera positions.

| Board | Original per-cube overrides, median | Shared palette, median |
|---|---:|---:|
| 1,728 cubes | 33.87 ms | 4.06 ms |
| 8,000 cubes | 137.66 ms | 13.96 ms |

The final rendered images had zero mean RGB difference in both comparisons. Six shared material variants serve the six-color board. The verification also checks authored material preservation, identical-color sharing, animated color overrides, completion/interruption cleanup, absence of intermediate color materials, and pooled cube reuse. See [raw rendering results](rotation-rendering.txt).

The native Play Mode regression passed 526 assertions covering real launches, settings, cube recoloring, source reuse, spread acceleration/fade, and restarts; exact counts vary with frame timing. See [Play Mode results](rotation-play-mode.txt).

To repeat the rendering comparison in an isolated project copy (the command replaces its open scene):

```sh
Unity -batchmode -projectPath /path/to/copy -quit -executeMethod FloodFill.EditorTools.FloodRotationPerformanceVerification.Run -logFile /tmp/flood-rotation.log
```

A graphics device is required; omit `-nographics`. The reference path recreates the former per-cube color blocks, and the optimized path calls the actual `VoxelCell3D.SetColor` implementation. The test does not change project assets or the player's saved game. Full development-build FPS remains dependent on target resolution, device GPU, startup physics/solver work, and how many cubes are actively animating.
