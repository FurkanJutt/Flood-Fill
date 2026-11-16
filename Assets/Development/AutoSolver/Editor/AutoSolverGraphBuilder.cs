using System;
using System.Collections.Generic;

namespace FloodFill.Development.AutoSolver
{
    internal static class AutoSolverGraphBuilder
    {
        private static readonly int[] Dx = { 1, -1, 0, 0, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1, 0, 0 };
        private static readonly int[] Dz = { 0, 0, 0, 0, 1, -1 };

        public static bool TryBuild(
            AutoSolverBoardSnapshot snapshot,
            out AutoSolverGraph graph,
            out string error)
        {
            graph = null;
            error = null;
            if (snapshot == null || snapshot.Width < 1 || snapshot.Height < 1 ||
                snapshot.Depth < 1 || snapshot.ColorCount < 2 || snapshot.ColorCount > 255 ||
                snapshot.VoxelCount < 1 || snapshot.LogicalIndices == null ||
                snapshot.LogicalIndices.Length != snapshot.VoxelCount)
            {
                error = "The active board snapshot has invalid dimensions or color data.";
                return false;
            }

            int volume = checked(snapshot.Width * snapshot.Height * snapshot.Depth);
            var voxelAt = new int[volume];
            Array.Fill(voxelAt, -1);
            for (int i = 0; i < snapshot.VoxelCount; i++)
            {
                int logical = snapshot.LogicalIndices[i];
                if (logical < 0 || logical >= volume || voxelAt[logical] >= 0 ||
                    snapshot.Colors[i] >= snapshot.ColorCount)
                {
                    error = "The board contains duplicate coordinates or an invalid color index.";
                    return false;
                }
                voxelAt[logical] = i;
            }

            var voxelRegions = new int[snapshot.VoxelCount];
            Array.Fill(voxelRegions, -1);
            var colors = new List<byte>();
            var counts = new List<int>();
            var representatives = new List<int>();
            var queue = new int[snapshot.VoxelCount];
            for (int start = 0; start < snapshot.VoxelCount; start++)
            {
                if (voxelRegions[start] >= 0) continue;
                int region = colors.Count;
                byte color = snapshot.Colors[start];
                int head = 0, tail = 0, voxelCount = 0;
                queue[tail++] = start;
                voxelRegions[start] = region;
                while (head < tail)
                {
                    int voxel = queue[head++];
                    voxelCount++;
                    int logical = snapshot.LogicalIndices[voxel];
                    Decode(logical, snapshot.Width, snapshot.Height, out int x, out int y, out int z);
                    for (int d = 0; d < 6; d++)
                    {
                        int nx = x + Dx[d], ny = y + Dy[d], nz = z + Dz[d];
                        if (!Inside(nx, ny, nz, snapshot)) continue;
                        int adjacentVoxel = voxelAt[Encode(nx, ny, nz, snapshot.Width, snapshot.Height)];
                        if (adjacentVoxel < 0 || voxelRegions[adjacentVoxel] >= 0 ||
                            snapshot.Colors[adjacentVoxel] != color) continue;
                        voxelRegions[adjacentVoxel] = region;
                        queue[tail++] = adjacentVoxel;
                    }
                }
                colors.Add(color);
                counts.Add(voxelCount);
                representatives.Add(start);
            }

            var neighborSets = new HashSet<int>[colors.Count];
            for (int i = 0; i < neighborSets.Length; i++) neighborSets[i] = new HashSet<int>();
            for (int voxel = 0; voxel < snapshot.VoxelCount; voxel++)
            {
                int logical = snapshot.LogicalIndices[voxel];
                Decode(logical, snapshot.Width, snapshot.Height, out int x, out int y, out int z);
                int region = voxelRegions[voxel];
                for (int d = 0; d < 6; d++)
                {
                    int nx = x + Dx[d], ny = y + Dy[d], nz = z + Dz[d];
                    if (!Inside(nx, ny, nz, snapshot)) continue;
                    int neighborVoxel = voxelAt[Encode(nx, ny, nz, snapshot.Width, snapshot.Height)];
                    if (neighborVoxel < 0) continue;
                    int neighborRegion = voxelRegions[neighborVoxel];
                    if (region != neighborRegion) neighborSets[region].Add(neighborRegion);
                }
            }

            var neighbors = new int[neighborSets.Length][];
            for (int i = 0; i < neighbors.Length; i++)
            {
                neighbors[i] = new int[neighborSets[i].Count];
                neighborSets[i].CopyTo(neighbors[i]);
            }

            int connectedComponents = CountConnectedComponents(neighbors);
            graph = new AutoSolverGraph
            {
                Width = snapshot.Width,
                Height = snapshot.Height,
                Depth = snapshot.Depth,
                ColorCount = snapshot.ColorCount,
                TotalVoxels = snapshot.VoxelCount,
                VoxelRegionIds = voxelRegions,
                RepresentativeVoxelIndices = representatives.ToArray(),
                RegionVoxelCounts = counts.ToArray(),
                RegionColors = colors.ToArray(),
                Neighbors = neighbors,
                ConnectedComponentCount = connectedComponents
            };
            return true;
        }

        private static int CountConnectedComponents(int[][] neighbors)
        {
            var visited = new bool[neighbors.Length];
            var queue = new int[neighbors.Length];
            int components = 0;
            for (int start = 0; start < neighbors.Length; start++)
            {
                if (visited[start]) continue;
                components++;
                int head = 0, tail = 0;
                queue[tail++] = start;
                visited[start] = true;
                while (head < tail)
                {
                    int current = queue[head++];
                    for (int i = 0; i < neighbors[current].Length; i++)
                    {
                        int next = neighbors[current][i];
                        if (visited[next]) continue;
                        visited[next] = true;
                        queue[tail++] = next;
                    }
                }
            }
            return components;
        }

        private static bool Inside(int x, int y, int z, AutoSolverBoardSnapshot s) =>
            x >= 0 && x < s.Width && y >= 0 && y < s.Height && z >= 0 && z < s.Depth;

        private static int Encode(int x, int y, int z, int width, int height) =>
            x + width * (y + height * z);

        private static void Decode(int index, int width, int height, out int x, out int y, out int z)
        {
            x = index % width;
            int yz = index / width;
            y = yz % height;
            z = yz / height;
        }
    }
}
