using System;
using FloodFill.ThreeD.Solver;

namespace FloodFill.Development.AutoSolver
{
    // UI/playback metadata adapter; all region construction belongs to the runtime core.
    internal static class AutoSolverGraphBuilder
    {
        public static bool TryBuild(AutoSolverBoardSnapshot snapshot, out AutoSolverGraph graph, out string error)
        {
            graph = null; error = null;
            if (snapshot == null || !FloodFillRegionGraph3D.TryBuild(new FloodFillBoardSnapshot3D(
                snapshot.Width, snapshot.Height, snapshot.Depth, snapshot.ColorCount,
                snapshot.LogicalIndices, snapshot.Colors, snapshot.StartingVoxelIndex), out var core))
            { error = "Invalid occupied voxel snapshot."; return false; }
            var representativeVoxels = new int[core.RegionCount];
            var voxelAt = new System.Collections.Generic.Dictionary<int, int>();
            for (int i = 0; i < snapshot.VoxelCount; i++) voxelAt.Add(snapshot.LogicalIndices[i], i);
            var neighbors = new int[core.RegionCount][];
            for (int r = 0; r < core.RegionCount; r++)
            {
                representativeVoxels[r] = voxelAt[core.RepresentativeLogicalIndices[r]];
                neighbors[r] = new int[core.NeighborOffsets[r + 1] - core.NeighborOffsets[r]];
                Array.Copy(core.Neighbors, core.NeighborOffsets[r], neighbors[r], 0, neighbors[r].Length);
            }
            graph = new AutoSolverGraph
            {
                Core = core, Width = snapshot.Width, Height = snapshot.Height, Depth = snapshot.Depth,
                ColorCount = core.ColorCount, TotalVoxels = core.TotalVoxelCount, VoxelRegionIds = core.VoxelRegionIds,
                RepresentativeVoxelIndices = representativeVoxels, RegionVoxelCounts = core.RegionVoxelCounts,
                RegionColors = core.RegionColors, Neighbors = neighbors, ConnectedComponentCount = core.IsConnected ? 1 : 2
            };
            return true;
        }
    }
}
