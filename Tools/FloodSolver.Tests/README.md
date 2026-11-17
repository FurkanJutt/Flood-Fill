Run the dependency-free core regression suite with .NET 10:

```sh
dotnet run --project Tools/FloodSolver.Tests -c Release -- /tmp/flood-benchmarks.csv
```

Optional modes: `--unit` (correctness only), `--sweep` (lookahead depth 1–5), `--runtime` (runtime settings on seeded shells). These arguments follow `--` in the command above. Outputs use invariant seed data; elapsed-time limits can change which incumbent is reached across machines.

Tests cover independently constructed voxel adjacency, every simulated move against a separate voxel BFS, complete solutions, exhaustive small-board minima, actual FNV hash collisions, region counts over 512, snapshots, malformed coordinates/dimensions, cancellation before/after an incumbent, original budget percentages/rounding, and estimated retained search memory under a 1 MiB setting. The memory limit applies to retained search storage, not total CLR/Unity process memory.

For native Unity gameplay validation, outside Play Mode run **Tools > Flood Fill > Verify Solver Against Gameplay**. It uses a temporary preview scene, authentic procedural shapes, and real `HandleVoxelClicked` calls; it checks all voxel colors, captured counts, move counting, completion, and unchanged existing budgets. It writes `Logs/FloodSolverGameplayVerification.txt`. It does not save or replace the player's session. The check is synchronous and intended for batch verification; use the Auto Solver window for animated Play Mode inspection.

Batch invocation (use a separate project copy if the original is open):

```sh
Unity -batchmode -nographics -quit -projectPath /path/to/project-copy -executeMethod FloodFill.EditorTools.FloodSolverGameplayVerification.Run -logFile /tmp/flood-gameplay.log
```
