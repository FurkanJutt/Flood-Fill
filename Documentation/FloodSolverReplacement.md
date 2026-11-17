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
