using System;
using System.Collections.Generic;
using UnityEngine;

namespace FloodFill.ThreeD
{
    public readonly struct VoxelShapeBounds
    {
        public VoxelShapeBounds(int minX, int maxX, int minY, int maxY, int minZ, int maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public int MinX { get; }
        public int MaxX { get; }
        public int MinY { get; }
        public int MaxY { get; }
        public int MinZ { get; }
        public int MaxZ { get; }
        public int Width => MaxX >= MinX ? MaxX - MinX + 1 : 0;
        public int Height => MaxY >= MinY ? MaxY - MinY + 1 : 0;
        public int Depth => MaxZ >= MinZ ? MaxZ - MinZ + 1 : 0;
        public bool IsValid => Width > 0 && Height > 0 && Depth > 0;
        public Vector3 Center => new Vector3(
            (MinX + MaxX) * 0.5f,
            (MinY + MaxY) * 0.5f,
            (MinZ + MaxZ) * 0.5f);
        public static VoxelShapeBounds Invalid => new VoxelShapeBounds(0, -1, 0, -1, 0, -1);
    }

    public sealed class ProceduralVoxelShapeResult
    {
        public bool[,,] Mask { get; internal set; }
        public VoxelShapeBounds Bounds { get; internal set; }
        public int ActiveVoxelCount { get; internal set; }
        public int GenerationAttempt { get; internal set; }
        public int Seed { get; internal set; }
    }

    public static class ProceduralVoxelShapeGenerator
    {
        private static readonly Vector3Int[] Directions =
        {
            Vector3Int.right,
            Vector3Int.left,
            Vector3Int.up,
            Vector3Int.down,
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1)
        };

        private readonly struct FrontierCandidate
        {
            public FrontierCandidate(Vector3Int position, Vector3Int direction)
            {
                Position = position;
                Direction = direction;
            }

            public Vector3Int Position { get; }
            public Vector3Int Direction { get; }
        }

        public static bool TryGenerate(
            int width,
            int height,
            int depth,
            ProceduralVoxelShapeSettings settings,
            int seed,
            out ProceduralVoxelShapeResult result)
        {
            result = null;
            if (width < 1 || height < 1 || depth < 1 || settings == null)
            {
                return false;
            }

            int margin = Mathf.Clamp(
                settings.edgeMargin,
                0,
                Mathf.Max(0, (Mathf.Min(width, Mathf.Min(height, depth)) - 1) / 2));
            Vector3Int minimum = new Vector3Int(margin, margin, margin);
            Vector3Int maximum = new Vector3Int(
                width - margin - 1,
                height - margin - 1,
                depth - margin - 1);
            int allowedWidth = maximum.x - minimum.x + 1;
            int allowedHeight = maximum.y - minimum.y + 1;
            int allowedDepth = maximum.z - minimum.z + 1;
            int capacity = allowedWidth * allowedHeight * allowedDepth;
            float minFill = Mathf.Clamp(settings.minSolidFillPercent, 0.05f, 1f);
            float maxFill = Mathf.Clamp(settings.maxSolidFillPercent, minFill, 1f);
            int minimumSolidVoxels = Mathf.Clamp(Mathf.CeilToInt(capacity * minFill), 1, capacity);
            int maximumSolidVoxels = Mathf.Clamp(
                Mathf.FloorToInt(capacity * maxFill),
                minimumSolidVoxels,
                capacity);
            int requiredX = Mathf.Clamp(settings.minimumSpanX, 1, allowedWidth);
            int requiredY = Mathf.Clamp(settings.minimumSpanY, 1, allowedHeight);
            int requiredZ = Mathf.Clamp(settings.minimumSpanZ, 1, allowedDepth);

            int attempts = Mathf.Max(1, settings.maxGenerationAttempts);
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                int attemptSeed = unchecked(seed + attempt * 104729);
                var random = new System.Random(attemptSeed);
                int targetCount = random.Next(minimumSolidVoxels, maximumSolidVoxels + 1);
                Vector3Int[] attractors = CreateAttractors(minimum, maximum, settings, random);
                bool[,,] solid = GrowConnectedSolid(
                    width,
                    height,
                    depth,
                    minimum,
                    maximum,
                    targetCount,
                    settings,
                    attractors,
                    random);

                CarveExteriorNotches(solid, minimumSolidVoxels, settings, random);
                FillInternalCavities(solid);
                bool[,,] surface = ExtractSurface(solid);
                int activeCount = CountActiveVoxels(surface);
                VoxelShapeBounds bounds = CalculateBounds(surface);
                if (activeCount == 0 || !bounds.IsValid ||
                    bounds.Width < requiredX || bounds.Height < requiredY || bounds.Depth < requiredZ ||
                    !ValidateConnectivity(surface, activeCount))
                {
                    continue;
                }

                result = new ProceduralVoxelShapeResult
                {
                    Mask = surface,
                    Bounds = bounds,
                    ActiveVoxelCount = activeCount,
                    GenerationAttempt = attempt,
                    Seed = seed
                };
                return true;
            }

            return false;
        }

        public static bool ValidateConnectivity(bool[,,] mask, int expectedActiveCount = -1)
        {
            if (mask == null)
            {
                return false;
            }

            Vector3Int start = default;
            bool foundStart = false;
            for (int x = 0; x < mask.GetLength(0) && !foundStart; x++)
            {
                for (int y = 0; y < mask.GetLength(1) && !foundStart; y++)
                {
                    for (int z = 0; z < mask.GetLength(2); z++)
                    {
                        if (!mask[x, y, z])
                        {
                            continue;
                        }

                        start = new Vector3Int(x, y, z);
                        foundStart = true;
                        break;
                    }
                }
            }

            if (!foundStart)
            {
                return false;
            }

            var visited = new HashSet<Vector3Int> { start };
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector3Int neighbor = current + Directions[i];
                    if (!IsInside(mask, neighbor) || !mask[neighbor.x, neighbor.y, neighbor.z] ||
                        !visited.Add(neighbor))
                    {
                        continue;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            int requiredCount = expectedActiveCount >= 0
                ? expectedActiveCount
                : CountActiveVoxels(mask);
            return visited.Count == requiredCount;
        }

        public static int CountActiveVoxels(bool[,,] mask)
        {
            if (mask == null)
            {
                return 0;
            }

            int count = 0;
            for (int x = 0; x < mask.GetLength(0); x++)
            {
                for (int y = 0; y < mask.GetLength(1); y++)
                {
                    for (int z = 0; z < mask.GetLength(2); z++)
                    {
                        if (mask[x, y, z])
                        {
                            count++;
                        }
                    }
                }
            }

            return count;
        }

        public static VoxelShapeBounds CalculateBounds(bool[,,] mask)
        {
            if (mask == null)
            {
                return VoxelShapeBounds.Invalid;
            }

            int minX = mask.GetLength(0);
            int minY = mask.GetLength(1);
            int minZ = mask.GetLength(2);
            int maxX = -1;
            int maxY = -1;
            int maxZ = -1;
            for (int x = 0; x < mask.GetLength(0); x++)
            {
                for (int y = 0; y < mask.GetLength(1); y++)
                {
                    for (int z = 0; z < mask.GetLength(2); z++)
                    {
                        if (!mask[x, y, z])
                        {
                            continue;
                        }

                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        minZ = Mathf.Min(minZ, z);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                        maxZ = Mathf.Max(maxZ, z);
                    }
                }
            }

            return maxX >= minX
                ? new VoxelShapeBounds(minX, maxX, minY, maxY, minZ, maxZ)
                : VoxelShapeBounds.Invalid;
        }

        private static bool[,,] GrowConnectedSolid(
            int width,
            int height,
            int depth,
            Vector3Int minimum,
            Vector3Int maximum,
            int targetCount,
            ProceduralVoxelShapeSettings settings,
            Vector3Int[] attractors,
            System.Random random)
        {
            var mask = new bool[width, height, depth];
            var active = new HashSet<Vector3Int>();
            var frontier = new List<FrontierCandidate>();
            var frontierPositions = new HashSet<Vector3Int>();
            Vector3Int center = new Vector3Int(
                (minimum.x + maximum.x) / 2,
                (minimum.y + maximum.y) / 2,
                (minimum.z + maximum.z) / 2);
            int offset = Mathf.Max(0, settings.seedOffsetRadius);
            Vector3Int seed = new Vector3Int(
                Mathf.Clamp(center.x + random.Next(-offset, offset + 1), minimum.x, maximum.x),
                Mathf.Clamp(center.y + random.Next(-offset, offset + 1), minimum.y, maximum.y),
                Mathf.Clamp(center.z + random.Next(-offset, offset + 1), minimum.z, maximum.z));
            Activate(seed, mask, active, frontier, frontierPositions, minimum, maximum);

            Vector3Int previousDirection = Vector3Int.zero;
            while (active.Count < targetCount && frontier.Count > 0)
            {
                int selectedIndex = random.NextDouble() < settings.branchChance
                    ? random.Next(frontier.Count)
                    : SelectWeightedFrontier(
                        frontier,
                        previousDirection,
                        center,
                        maximum - minimum + Vector3Int.one,
                        attractors,
                        settings,
                        random);
                FrontierCandidate selected = frontier[selectedIndex];
                frontier.RemoveAt(selectedIndex);
                frontierPositions.Remove(selected.Position);
                if (active.Contains(selected.Position))
                {
                    continue;
                }

                Activate(
                    selected.Position,
                    mask,
                    active,
                    frontier,
                    frontierPositions,
                    minimum,
                    maximum);
                previousDirection = selected.Direction;

                if (active.Count < targetCount && random.NextDouble() < settings.brushChance)
                {
                    int directionOffset = random.Next(Directions.Length);
                    for (int i = 0; i < Directions.Length && active.Count < targetCount; i++)
                    {
                        Vector3Int brushed = selected.Position +
                            Directions[(directionOffset + i) % Directions.Length];
                        if (!IsInside(brushed, minimum, maximum) || active.Contains(brushed) ||
                            random.NextDouble() > 0.45)
                        {
                            continue;
                        }

                        Activate(
                            brushed,
                            mask,
                            active,
                            frontier,
                            frontierPositions,
                            minimum,
                            maximum);
                    }
                }
            }

            return mask;
        }

        private static int SelectWeightedFrontier(
            List<FrontierCandidate> frontier,
            Vector3Int previousDirection,
            Vector3Int center,
            Vector3Int size,
            Vector3Int[] attractors,
            ProceduralVoxelShapeSettings settings,
            System.Random random)
        {
            var weights = new float[frontier.Count];
            float totalWeight = 0f;
            float maximumDistance = Mathf.Max(1f, new Vector3(size.x, size.y, size.z).magnitude);
            for (int i = 0; i < frontier.Count; i++)
            {
                FrontierCandidate candidate = frontier[i];
                float weight = 0.12f;
                if (candidate.Direction == previousDirection)
                {
                    weight += settings.directionPersistence * 2.2f;
                }

                float centerProximity = 1f - Mathf.Clamp01(
                    Vector3.Distance(candidate.Position, center) / maximumDistance);
                weight += centerProximity * settings.centerBias;

                float nearestAttractor = maximumDistance;
                for (int attractorIndex = 0; attractorIndex < attractors.Length; attractorIndex++)
                {
                    nearestAttractor = Mathf.Min(
                        nearestAttractor,
                        Vector3.Distance(candidate.Position, attractors[attractorIndex]));
                }

                float lobeProximity = 1f - Mathf.Clamp01(nearestAttractor / maximumDistance);
                weight += lobeProximity * settings.lobeStrength * 2.4f;
                weight *= 0.82f + (float)random.NextDouble() * 0.36f;
                weights[i] = Mathf.Max(0.001f, weight);
                totalWeight += weights[i];
            }

            double selection = random.NextDouble() * totalWeight;
            for (int i = 0; i < weights.Length; i++)
            {
                selection -= weights[i];
                if (selection <= 0d)
                {
                    return i;
                }
            }

            return weights.Length - 1;
        }

        private static Vector3Int[] CreateAttractors(
            Vector3Int minimum,
            Vector3Int maximum,
            ProceduralVoxelShapeSettings settings,
            System.Random random)
        {
            int minLobes = Mathf.Clamp(settings.minimumLobes, 1, 8);
            int maxLobes = Mathf.Clamp(settings.maximumLobes, minLobes, 8);
            int count = random.Next(minLobes, maxLobes + 1);
            var attractors = new Vector3Int[count];
            for (int i = 0; i < count; i++)
            {
                attractors[i] = new Vector3Int(
                    random.Next(minimum.x, maximum.x + 1),
                    random.Next(minimum.y, maximum.y + 1),
                    random.Next(minimum.z, maximum.z + 1));
            }

            return attractors;
        }

        private static void Activate(
            Vector3Int position,
            bool[,,] mask,
            HashSet<Vector3Int> active,
            List<FrontierCandidate> frontier,
            HashSet<Vector3Int> frontierPositions,
            Vector3Int minimum,
            Vector3Int maximum)
        {
            if (!active.Add(position))
            {
                return;
            }

            mask[position.x, position.y, position.z] = true;
            for (int i = 0; i < Directions.Length; i++)
            {
                Vector3Int neighbor = position + Directions[i];
                if (!IsInside(neighbor, minimum, maximum) || active.Contains(neighbor) ||
                    !frontierPositions.Add(neighbor))
                {
                    continue;
                }

                frontier.Add(new FrontierCandidate(neighbor, Directions[i]));
            }
        }

        private static void CarveExteriorNotches(
            bool[,,] mask,
            int minimumCount,
            ProceduralVoxelShapeSettings settings,
            System.Random random)
        {
            int activeCount = CountActiveVoxels(mask);
            int passes = Mathf.Max(0, settings.edgeNotchPasses);
            for (int pass = 0; pass < passes && activeCount > minimumCount; pass++)
            {
                var candidates = new List<Vector3Int>();
                for (int x = 0; x < mask.GetLength(0); x++)
                {
                    for (int y = 0; y < mask.GetLength(1); y++)
                    {
                        for (int z = 0; z < mask.GetLength(2); z++)
                        {
                            var position = new Vector3Int(x, y, z);
                            if (mask[x, y, z] && IsSurfaceVoxel(mask, position))
                            {
                                candidates.Add(position);
                            }
                        }
                    }
                }

                Shuffle(candidates, random);
                for (int i = 0; i < candidates.Count && activeCount > minimumCount; i++)
                {
                    if (random.NextDouble() > settings.edgeNotchChance)
                    {
                        continue;
                    }

                    Vector3Int candidate = candidates[i];
                    mask[candidate.x, candidate.y, candidate.z] = false;
                    if (ValidateConnectivity(mask, activeCount - 1))
                    {
                        activeCount--;
                    }
                    else
                    {
                        mask[candidate.x, candidate.y, candidate.z] = true;
                    }
                }
            }
        }

        private static void FillInternalCavities(bool[,,] solid)
        {
            int width = solid.GetLength(0);
            int height = solid.GetLength(1);
            int depth = solid.GetLength(2);
            var exterior = new bool[width + 2, height + 2, depth + 2];
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(Vector3Int.zero);
            exterior[0, 0, 0] = true;
            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector3Int neighbor = current + Directions[i];
                    if (neighbor.x < 0 || neighbor.x >= width + 2 ||
                        neighbor.y < 0 || neighbor.y >= height + 2 ||
                        neighbor.z < 0 || neighbor.z >= depth + 2 ||
                        exterior[neighbor.x, neighbor.y, neighbor.z])
                    {
                        continue;
                    }

                    int solidX = neighbor.x - 1;
                    int solidY = neighbor.y - 1;
                    int solidZ = neighbor.z - 1;
                    if (solidX >= 0 && solidX < width && solidY >= 0 && solidY < height &&
                        solidZ >= 0 && solidZ < depth && solid[solidX, solidY, solidZ])
                    {
                        continue;
                    }

                    exterior[neighbor.x, neighbor.y, neighbor.z] = true;
                    queue.Enqueue(neighbor);
                }
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        if (!solid[x, y, z] && !exterior[x + 1, y + 1, z + 1])
                        {
                            solid[x, y, z] = true;
                        }
                    }
                }
            }
        }

        private static bool[,,] ExtractSurface(bool[,,] solid)
        {
            var surface = new bool[
                solid.GetLength(0),
                solid.GetLength(1),
                solid.GetLength(2)];
            for (int x = 0; x < solid.GetLength(0); x++)
            {
                for (int y = 0; y < solid.GetLength(1); y++)
                {
                    for (int z = 0; z < solid.GetLength(2); z++)
                    {
                        var position = new Vector3Int(x, y, z);
                        surface[x, y, z] = solid[x, y, z] && IsSurfaceVoxel(solid, position);
                    }
                }
            }

            return surface;
        }

        private static bool IsSurfaceVoxel(bool[,,] mask, Vector3Int position)
        {
            for (int i = 0; i < Directions.Length; i++)
            {
                Vector3Int neighbor = position + Directions[i];
                if (!IsInside(mask, neighbor) || !mask[neighbor.x, neighbor.y, neighbor.z])
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInside(bool[,,] mask, Vector3Int position)
        {
            return position.x >= 0 && position.x < mask.GetLength(0) &&
                position.y >= 0 && position.y < mask.GetLength(1) &&
                position.z >= 0 && position.z < mask.GetLength(2);
        }

        private static bool IsInside(Vector3Int position, Vector3Int minimum, Vector3Int maximum)
        {
            return position.x >= minimum.x && position.x <= maximum.x &&
                position.y >= minimum.y && position.y <= maximum.y &&
                position.z >= minimum.z && position.z <= maximum.z;
        }

        private static void Shuffle<T>(List<T> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }
        }
    }
}
