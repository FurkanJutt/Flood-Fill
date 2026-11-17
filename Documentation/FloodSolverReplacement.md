# Solver replacement audit and design

The gameplay rule is **free flood**: `HandleVoxelClicked` calls `RecolorConnectedRegion` on any clicked voxel, changes its complete six-connected source-color component, recalculates the captured component from that click, and counts one move. The source need not be the previous captured component. A color sequence alone is insufficient; representative region IDs must accompany it.

Dependencies before replacement:

- `FloodFillGameManager3D` captures a board snapshot, builds the region graph, calls `FloodFillMoveSolver3D`, and uses its validated result and `MoveBudgetCalculator3D` for difficulty budgets. Existing async generation/cancellation is retained.
- `VoxelBoardManager3D.TryCreateSolverSnapshot` is the Unity adapter. Gameplay, shape generation, input and rendering are untouched.
- `FloodFillMoveSolver64`, `FloodFillMoveSolverLarge`, and `FloodFillMoveLookahead3D` are old search implementations, including their caches/state layouts. They are replaced, including existing local edits within these files.
- Editor `AutoSolverSearch` and `AutoSolverGraphBuilder` duplicate runtime search and graph construction. They become adapters to the same core. `AutoSolverWindow`, `AutoSolverPlaybackController`, and `AutoSolverVisualTuner` retain controls and animations; playback still invokes the real manager handler and checks live colors.
- `FloodFillMoveSolverProfiler` depends on the old lookahead API and needs migration.
- `FloodFillSolverModels3D` public result/settings/snapshot interfaces and budget calculator remain compatible.

Design: immutable snapshot with canonical coordinate ordering; maximal same-color region graph with sparse adjacency; immutable byte-per-original-region search states. A captured-only bitset cannot represent this game's free-flood moves. Byte states scale beyond 512 regions without shifts or voxel-board copies. Full byte equality follows hashing; collisions never imply equality. Component analysis enumerates all boundary groups simultaneously and evaluates merged region/voxel mass and future frontier. Greedy portfolio and memoized depth-limited lookahead establish incumbents. Bounded diverse beam search with greedy completions improves them. IDA* considers **all** legal recolorings, including non-merging moves, and uses only distinct-colors-minus-one as its admissible bound; fixed-origin distance bounds are unsafe for free flood. Exact exhaustion alone proves optimality. Time/node/memory limits preserve every validated incumbent. Cancellation before the first solution reports cancellation; after an incumbent it returns that incumbent with a cancellation reason.

Budgets preserve percentages, minimum bonuses, complexity multipliers and rounding. Editor analysis never writes the level budget and reports the original budget source length separately from its analysis.

## Final implementation and compatibility

The shared engine includes a diverse unrestricted beam and a complementary beam that grows a component from several origins. The restricted beam uses maximum transition distance and remaining colors to rank states efficiently. These origin-dependent bounds are deliberately excluded from the free-flood optimality proof. A solution includes both a color and an original-region representative for every move. Every shorter incumbent is replay-validated before replacing the current best.

Runtime generation always runs search on a worker. The old asynchronous toggle/threshold were removed rather than leaving settings that no longer affect behavior. Unity snapshot capture and application of the result stay on the main thread. Default runtime portfolio/beam/exact phase limits are 200/200/40 ms; Editor limits are 500/1500/200 ms. Each search phase also has a configurable node limit and estimated retained-state memory limit. A first monotone valid completion precedes timed improvement, so phase deadlines are cooperative rather than hard end-to-end latency guarantees. Large boards may need increased budgets; device profiling is still required.

The Editor retains its existing configurable time/state/beam controls, including longer optimization runs. The analysis result separates validity/optimality from whether it fits the **remaining** move budget. It shows the original budget-source length, its proof flag, and whether the budget came from the current shared engine or a persisted session whose original engine was not recorded. Analysis never writes the existing level budget. Perfect budgets use the validated best-known length; the proof flag distinguishes proven minima. The percentages, minimum bonuses, complexity multipliers, rounding, and budget caps remain unchanged.

## Files

Deleted obsolete engines and their `.meta` files:

- `Assets/FloodFill/Scripts/ThreeD/Solver/FloodFillMoveSolver64.cs`
- `Assets/FloodFill/Scripts/ThreeD/Solver/FloodFillMoveSolverLarge.cs`
- `Assets/FloodFill/Scripts/ThreeD/Solver/FloodFillMoveLookahead3D.cs`

Created (with Unity metadata for scripts):

- `Assets/FloodFill/Scripts/ThreeD/Solver/FloodSolver.cs` — portfolio, lookahead, unrestricted beam, budget handling, IDA*, incumbent retention and diagnostics.
- `Assets/FloodFill/Scripts/ThreeD/Solver/FloodAnchoredSearch.cs` — bounded search from multiple origins with transition-distance ordering.
- `Assets/FloodFill/Scripts/ThreeD/Solver/FloodSolverState.cs` — byte-region states and complete collision-safe equality.
- `Assets/FloodFill/Scripts/ThreeD/Solver/FloodMoveSimulator.cs` — shared free-flood simulator and component/frontier analysis.
- `Assets/FloodFill/Scripts/ThreeD/Solver/FloodHeuristics.cs` — six greedy scoring strategies and separated admissible/ordering estimates.
- `Assets/FloodFill/Scripts/ThreeD/Solver/FloodSolverValidator.cs` — full sequence replay and captured counts.
- `Assets/FloodFill/Editor/FloodSolverGameplayVerification.cs` — isolated real-game handler verification.
- `Tools/FloodSolver.Tests/` — standalone regression/benchmark runner and instructions.
- This report and `Documentation/FloodSolverBenchmarks/` — measured CSV evidence.

Modified:

- `FloodFillMoveSolver3D.cs` — compatibility facade over the new engine.
- `FloodFillRegionGraph3D.cs` — canonical region order, sorted adjacency, input/overflow checks, voxel mapping and connectivity.
- `FloodFillSolverModels3D.cs` — immutable snapshot copies, phase settings and result status; original budget arithmetic retained.
- `FloodFillGameManager3D.cs` — always-asynchronous search and copied settings.
- `FloodFillMoveSolverProfiler.cs` — simulator probe, canonical graph validation, larger cases and meaningful depth sweep.
- Editor `AutoSolverGraphBuilder.cs`, `AutoSolverSearch.cs` — shared-core adapters replacing duplicate graph/search engines.
- Editor `AutoSolverModels.cs`, `AutoSolverAdapter.cs`, `AutoSolverDiagnostics.cs`, `AutoSolverWindow.cs` — original-budget provenance and search statistics.

The playback controller, visual tuner, voxel/gameplay methods, generation, materials, shaders, camera, input, scenes, and production UI appearance were not changed. The Editor development-tool folder remains removable: runtime files contain no dependencies on it.

## Measured standalone results

Seeds 1103 and 2207, six colors, sizes 6/8/10/12/16, four shapes per size: hollow shell, solid cube, deterministic irregular volume with holes, and a few large groups. There are 40 cases. Exact raw occupied coordinates/colors are reproducible from `Tools/FloodSolver.Tests/Program.cs`. The preserved pre-replacement engine was copied before removal and tested on the same boards; this baseline already included the local runtime lookahead changes present at task start. It is not an assertion about the unidentified 64-move board.

Both comparison runs allowed 1500 ms beam and 200 ms exact phases; the new Editor portfolio additionally allows up to 500 ms, while the old portfolio had its own fixed runs. Wall time is the measured value, not the configured maximum. Measurements use the installed desktop .NET 10 runtime; Unity-native gameplay validation is recorded separately. Time budgets and system load can change the incumbent; tie-breaking itself is deterministic.

Across 40 boards: old mean **16.4** moves, new mean **14.25** (**13.1% fewer**). New results improved 27 cases, tied 12, and regressed one case by one move (6-shell seed 1103: old 12, new 13 with generous old beam; its original default-budget baseline was 14). Mean elapsed time was approximately **491 ms old / 481 ms new**. This is a small fixed-seed sample, not a guarantee of universal improvement.

| Size | Old shell moves (two seeds) | New Editor shell moves | New runtime shell moves | New mean / median / worst, all eight cases | Mean Editor time | Estimated peak retained search storage |
|---|---|---|---|---|---|---|
| 6³ | 12, 14 | 13, 13 | 13, 13 | 9.38 / 10.5 / 13 | 377 ms | 0.52 MiB |
| 8³ | 19, 20 | 17, 18 | 17, 18 | 11.50 / 12.5 / 18 | 279 ms | 1.00 MiB |
| 10³ | 25, 24 | 23, 21 | 23, 21 | 14.00 / 15.5 / 23 | 377 ms | 1.59 MiB |
| 12³ | 29, 31 | 26, 26 | 26, 27 | 16.38 / 18 / 26 | 537 ms | 2.42 MiB |
| 16³ | 41, 42 | 33, 34 | 36, 36 | 20.00 / 22.5 / 34 | 836 ms | 2.72 MiB |

Maximum graph in the suite: 2,258 regions, 4,096 occupied voxels. All 40 returned sequences passed independent voxel replay. All fit both their newly generated Normal budget and the comparable old Normal budget (the latter uses old solution lengths with the preserved formula). The former is a consistency check because a budget derived from a validated solution naturally accommodates that solution. These percentages do not establish that unrelated existing levels or all larger boards fit their budgets. The new sequence fits the old Perfect budget in 39/40 cases.

The runtime shell sweep used roughly 135–429 ms per board on this host. Lower-level search storage estimates are not Unity/CLR process RSS or total allocations. Tests also validated solutions under a 1 MiB retained-search setting; temporary analysis/graph/runtime memory is additional.

Lookahead-only sweep (beam/exact improvements limited to 1 ms, six portfolio strategies, shell seed 1103):

| Depth | 8³ greedy moves / elapsed | 12³ greedy moves / elapsed |
|---|---|---|
| 1 | 23 / 82 ms | 32 / 180 ms |
| 2 | 23 / 59 ms | 33 / 123 ms |
| 3 | 19 / 82 ms | 31 / 163 ms |
| 4 | 19 / 105 ms | 29 / 231 ms |
| 5 | 19 / 169 ms | 30 / 355 ms |

Depth 3 is the default. Greater depth helped one sample at depth 4, cost more, and was not monotonically better because move ordering and per-decision node limits restrict the explored sequences.

## Correctness evidence

- Standalone full suite passed 895,376 assertions across unit checks and 40 benchmark cases; the final targeted unit rerun passed 7,026 assertions including two additional checks using a real FNV32 hash collision. These counts depend on the paths found within time limits.
- Thirty 2³ boards with fixed seeds and three colors matched an independent exhaustive breadth-first oracle for minimum length and optimality. The oracle enumerates all source regions and target colors, including nonmerging recolorings.
- Every benchmark move matched independent voxel BFS, captured mass, and the reported move count, and ended fully captured.
- Tests cover canonical graph independence from input order; >64, >128, >256, >512 regions; malformed/duplicate coordinates and integer dimensions; immutable snapshots; cancellation before a solution and preservation after an incumbent; existing budget rounding/percentages; and bounded estimated retained search memory.
- Runtime and Editor assemblies compile with Unity 6000.5.8f1's actual compiler and reference response files. Native Unity batch validation and animation limits are recorded below.

## Why the old systems could perform poorly

The Editor greedy backend selected `CollectMoves(...)[0]`, whose order came from region traversal and target-color enumeration. It could repeatedly recolor a small early component instead of choosing high-value global merges. Its independent beam/greedy implementation and graph builder also disagreed with the runtime engine. The runtime beam strongly weighted immediate merged mass and explored a large source-component × target-color branching space, limiting useful depth. Its large-state backend did not share the small-state exact search. These are observed architectural problems, not a proven diagnosis of the unidentified 64-move run. The preserved old runtime engine was already much better than 64 moves on the reproducible 296-voxel samples.

The replacement combines complete frontier/group evaluation, a greedy portfolio, sequence lookahead, greedy completions during bounded search, and lower-branching searches from multiple origins. Exact search uses safe bounds and never reports a beam result as optimal merely because it fits a budget.

## Manual benchmark and remaining limits

The only saved 296-voxel session found had **27 moves already played**, a **27-move budget**, and persisted original solution length **21**, with generation seed 0. The original per-voxel colors before those moves were not retained. Shape seed alone is insufficient to reconstruct those colors. It cannot establish identity with the user's 35-move / approximately 24-move manual board. **That mandatory benchmark is unavailable, not passed.** The seeded 296-voxel boards solved in 17/18 moves are different boards.

Most large-board minima remain unproven. Free-flood exact search has a deliberately conservative distinct-color lower bound and may time out; adaptive region thresholds are configurable and are not representation limits. Memory diagnostics estimate retained search state storage; no strict total-process memory guarantee is claimed. No target mobile-device performance was measured. Restored old sessions keep their historical budgets and are identified as such in Editor diagnostics. Headless logical execution cannot certify animation appearance; visually inspect the unchanged Auto Solver controls and animation in Play Mode before release.

## Final native Unity result

Unity 6000.5.8f1 batch execution exited successfully. **All 30 native cases passed**: 6³/8³/10³/12³/16³, seeds 1103/2207, shell / irregular-with-holes / authentic production procedural shapes. For every move, the real manager `SelectColor` and private `HandleVoxelClicked` path ran; every voxel color matched the core simulator, captured counts matched, exactly one move was counted, and every solution fully captured the board. The existing test budget remained unchanged throughout execution. The checks used an intentionally generous existing budget to isolate playback correctness; normal-budget arithmetic and solution length consistency were checked separately in the standalone suite. The player's saved session and active scene were not used or modified.

The run took place in an isolated temporary project copy because Unity already had the working project open. It compiled the replacement with Unity's own build pipeline. `FloodSolverBenchmarks/unity-gameplay.txt` records each native case and move count. Different incumbents from the .NET benchmark are expected under wall-time limits and a different runtime. Animation appearance and all interactive control interactions have not been visually inspected; the controller/visual tuner are unchanged, and actual logical playback compatibility is verified.
