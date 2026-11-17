using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FloodFill.ThreeD.Solver
{
    public sealed class FloodFillRegionGraph3D
    {
        private static readonly int[] DirectionX = { 1, -1, 0, 0, 0, 0 };
        private static readonly int[] DirectionY = { 0, 0, 1, -1, 0, 0 };
        private static readonly int[] DirectionZ = { 0, 0, 0, 0, 1, -1 };

        private FloodFillRegionGraph3D()
        {
        }

        public bool IsConnected { get; private set; }
        public int[] VoxelRegionIds { get; private set; }
        public int RegionCount { get; private set; }
        public int ColorCount { get; private set; }
        public int TotalVoxelCount { get; private set; }
        public int StartingRegion { get; private set; }
        public byte[] RegionColors { get; private set; }
        public int[] RegionVoxelCounts { get; private set; }
        public int[] RepresentativeLogicalIndices { get; private set; }
        public int[] NeighborOffsets { get; private set; }
        public int[] Neighbors { get; private set; }
        public double BuildMilliseconds { get; private set; }

        public static bool TryBuild(
            FloodFillBoardSnapshot3D snapshot,
            out FloodFillRegionGraph3D graph)
        {
            graph = null;
            if (snapshot == null || snapshot.Width < 1 || snapshot.Height < 1 ||
                snapshot.Depth < 1 || snapshot.ColorCount < 1 || snapshot.ColorCount > 256 || snapshot.VoxelCount < 1 ||
                snapshot.Colors == null || snapshot.Colors.Length != snapshot.VoxelCount ||
                snapshot.StartingVoxel < 0 || snapshot.StartingVoxel >= snapshot.VoxelCount)
            {
                return false;
            }

            Stopwatch watch = Stopwatch.StartNew();
            if ((long)snapshot.Width * snapshot.Height > int.MaxValue ||
                (long)snapshot.Width * snapshot.Height * snapshot.Depth > int.MaxValue) return false;
            int volume = snapshot.Width * snapshot.Height * snapshot.Depth;
            int[] logicalIndices = snapshot.LogicalIndices;
            byte[] voxelColors = snapshot.Colors;
            if (logicalIndices == null || logicalIndices.Length != snapshot.VoxelCount) return false;
            var voxelAtLogicalIndex = new int[volume];
            var voxelRegion = new int[snapshot.VoxelCount];
            Array.Fill(voxelAtLogicalIndex, -1);
            Array.Fill(voxelRegion, -1);
            for (int i = 0; i < snapshot.VoxelCount; i++)
            {
                int logicalIndex = logicalIndices[i];
                if (logicalIndex < 0 || logicalIndex >= volume ||
                    voxelAtLogicalIndex[logicalIndex] >= 0 ||
                    voxelColors[i] >= snapshot.ColorCount)
                {
                    return false;
                }
                voxelAtLogicalIndex[logicalIndex] = i;
            }

            var colors = new List<byte>();
            var counts = new List<int>();
            var representatives = new List<int>();
            var queue = new int[snapshot.VoxelCount];
            var order = new int[snapshot.VoxelCount];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => logicalIndices[a].CompareTo(logicalIndices[b]));
            for (int orderIndex = 0; orderIndex < order.Length; orderIndex++)
            {
                int startVoxel = order[orderIndex];
                if (voxelRegion[startVoxel] >= 0)
                {
                    continue;
                }

                int regionId = colors.Count;
                byte color = voxelColors[startVoxel];
                int head = 0;
                int tail = 0;
                int voxelCount = 0;
                queue[tail++] = startVoxel;
                voxelRegion[startVoxel] = regionId;
                while (head < tail)
                {
                    int voxel = queue[head++];
                    voxelCount++;
                    int logicalIndex = logicalIndices[voxel];
                    FromIndex(logicalIndex, snapshot.Width, snapshot.Height,
                        out int x, out int y, out int z);
                    for (int direction = 0; direction < 6; direction++)
                    {
                        int nx = x + DirectionX[direction];
                        int ny = y + DirectionY[direction];
                        int nz = z + DirectionZ[direction];
                        if (!IsInside(nx, ny, nz, snapshot.Width, snapshot.Height, snapshot.Depth))
                        {
                            continue;
                        }

                        int neighborVoxel = voxelAtLogicalIndex[
                            ToIndex(nx, ny, nz, snapshot.Width, snapshot.Height)];
                        if (neighborVoxel < 0 || voxelRegion[neighborVoxel] >= 0 ||
                            voxelColors[neighborVoxel] != color)
                        {
                            continue;
                        }

                        voxelRegion[neighborVoxel] = regionId;
                        queue[tail++] = neighborVoxel;
                    }
                }

                colors.Add(color);
                counts.Add(voxelCount);
                representatives.Add(logicalIndices[startVoxel]);
            }

            var edges = new HashSet<ulong>();
            var degrees = new int[colors.Count];
            for (int voxel = 0; voxel < snapshot.VoxelCount; voxel++)
            {
                int region = voxelRegion[voxel];
                int logicalIndex = logicalIndices[voxel];
                FromIndex(logicalIndex, snapshot.Width, snapshot.Height,
                    out int x, out int y, out int z);
                for (int direction = 0; direction < 6; direction++)
                {
                    int nx = x + DirectionX[direction];
                    int ny = y + DirectionY[direction];
                    int nz = z + DirectionZ[direction];
                    if (!IsInside(nx, ny, nz, snapshot.Width, snapshot.Height, snapshot.Depth))
                    {
                        continue;
                    }

                    int neighborVoxel = voxelAtLogicalIndex[
                        ToIndex(nx, ny, nz, snapshot.Width, snapshot.Height)];
                    if (neighborVoxel < 0)
                    {
                        continue;
                    }

                    int neighborRegion = voxelRegion[neighborVoxel];
                    if (region == neighborRegion)
                    {
                        continue;
                    }

                    int low = Math.Min(region, neighborRegion);
                    int high = Math.Max(region, neighborRegion);
                    ulong edge = ((ulong)(uint)low << 32) | (uint)high;
                    if (edges.Add(edge))
                    {
                        degrees[low]++;
                        degrees[high]++;
                    }
                }
            }

            var offsets = new int[colors.Count + 1];
            for (int i = 0; i < colors.Count; i++)
            {
                offsets[i + 1] = offsets[i] + degrees[i];
            }
            var neighbors = new int[offsets[colors.Count]];
            var cursors = (int[])offsets.Clone();
            foreach (ulong edge in edges)
            {
                int low = (int)(edge >> 32);
                int high = (int)(edge & uint.MaxValue);
                neighbors[cursors[low]++] = high;
                neighbors[cursors[high]++] = low;
            }

            for (int r = 0; r < colors.Count; r++) Array.Sort(neighbors, offsets[r], offsets[r + 1] - offsets[r]);
            var reached = new bool[colors.Count];
            int qHead = 0, qTail = 0;
            queue[qTail++] = 0; reached[0] = true;
            while (qHead < qTail)
            {
                int r = queue[qHead++];
                for (int e = offsets[r]; e < offsets[r + 1]; e++)
                    if (!reached[neighbors[e]]) { reached[neighbors[e]] = true; queue[qTail++] = neighbors[e]; }
            }
            watch.Stop();
            graph = new FloodFillRegionGraph3D
            {
                IsConnected = qTail == colors.Count,
                VoxelRegionIds = voxelRegion,
                RegionCount = colors.Count,
                ColorCount = snapshot.ColorCount,
                TotalVoxelCount = snapshot.VoxelCount,
                StartingRegion = voxelRegion[snapshot.StartingVoxel],
                RegionColors = colors.ToArray(),
                RegionVoxelCounts = counts.ToArray(),
                RepresentativeLogicalIndices = representatives.ToArray(),
                NeighborOffsets = offsets,
                Neighbors = neighbors,
                BuildMilliseconds = watch.Elapsed.TotalMilliseconds
            };
            return graph.StartingRegion >= 0;
        }

        private static int ToIndex(int x, int y, int z, int width, int height)
        {
            return x + width * (y + height * z);
        }

        private static void FromIndex(
            int index, int width, int height, out int x, out int y, out int z)
        {
            x = index % width;
            int yz = index / width;
            y = yz % height;
            z = yz / height;
        }

        private static bool IsInside(
            int x, int y, int z, int width, int height, int depth)
        {
            return x >= 0 && x < width && y >= 0 && y < height && z >= 0 && z < depth;
        }
    }
}
