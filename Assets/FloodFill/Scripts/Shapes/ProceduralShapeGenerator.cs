using System;
using System.Collections.Generic;
using UnityEngine;

namespace FloodFill.Shapes
{
    public readonly struct ShapeBounds
    {
        public ShapeBounds(int minX, int maxX, int minY, int maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        public int MinX { get; }
        public int MaxX { get; }
        public int MinY { get; }
        public int MaxY { get; }
        public int Width => MaxX >= MinX ? MaxX - MinX + 1 : 0;
        public int Height => MaxY >= MinY ? MaxY - MinY + 1 : 0;
        public bool IsValid => Width > 0 && Height > 0;
        public static ShapeBounds Invalid => new ShapeBounds(0, -1, 0, -1);
    }

    public sealed class ProceduralShapeResult
    {
        public bool[,] Mask { get; internal set; }
        public ShapeBounds Bounds { get; internal set; }
        public int ActiveCellCount { get; internal set; }
        public int GenerationAttempt { get; internal set; }
        public int Seed { get; internal set; }
    }

    public static class ProceduralShapeGenerator
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        private readonly struct FrontierCandidate
        {
            public FrontierCandidate(Vector2Int position, Vector2Int parent, Vector2Int direction)
            {
                Position = position;
                Parent = parent;
                Direction = direction;
            }

            public Vector2Int Position { get; }
            public Vector2Int Parent { get; }
            public Vector2Int Direction { get; }
        }

        public static bool TryGenerate(
            int width,
            int height,
            ProceduralShapeSettings settings,
            int seed,
            out ProceduralShapeResult result)
        {
            result = null;
            if (width < 1 || height < 1 || settings == null)
            {
                return false;
            }

            int margin = Mathf.Max(0, settings.edgeMargin);
            int maximumMargin = Mathf.Max(0, (Mathf.Min(width, height) - 1) / 2);
            margin = Mathf.Min(margin, maximumMargin);
            int minAllowedX = margin;
            int maxAllowedX = width - margin - 1;
            int minAllowedY = margin;
            int maxAllowedY = height - margin - 1;
            int allowedWidth = maxAllowedX - minAllowedX + 1;
            int allowedHeight = maxAllowedY - minAllowedY + 1;
            int capacity = allowedWidth * allowedHeight;
            int logicalCapacity = width * height;

            float minimumFill = Mathf.Clamp(settings.minFillPercent, 0.01f, 1f);
            float maximumFill = Mathf.Clamp(settings.maxFillPercent, minimumFill, 1f);
            int minimumCells = Mathf.Clamp(
                Mathf.CeilToInt(logicalCapacity * minimumFill),
                1,
                capacity);
            int maximumCells = Mathf.Clamp(
                Mathf.FloorToInt(logicalCapacity * maximumFill),
                minimumCells,
                capacity);
            int requiredWidth = Mathf.Clamp(settings.minimumShapeWidth, 1, allowedWidth);
            int requiredHeight = Mathf.Clamp(settings.minimumShapeHeight, 1, allowedHeight);
            int attempts = Mathf.Max(1, settings.maxGenerationAttempts);

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                int attemptSeed = unchecked(seed + attempt * 104729);
                var random = new System.Random(attemptSeed);
                int targetCells = random.Next(minimumCells, maximumCells + 1);
                bool[,] mask = GenerateConnectedMask(
                    width,
                    height,
                    minAllowedX,
                    maxAllowedX,
                    minAllowedY,
                    maxAllowedY,
                    targetCells,
                    settings,
                    random);

                CleanupMask(
                    mask,
                    minAllowedX,
                    maxAllowedX,
                    minAllowedY,
                    maxAllowedY,
                    minimumCells,
                    maximumCells,
                    settings,
                    random);

                int activeCellCount = CountActiveCells(mask);
                ShapeBounds bounds = CalculateBounds(mask);
                if (activeCellCount < minimumCells || activeCellCount > maximumCells ||
                    !bounds.IsValid || bounds.Width < requiredWidth || bounds.Height < requiredHeight ||
                    !ValidateConnectivity(mask, activeCellCount))
                {
                    continue;
                }

                result = new ProceduralShapeResult
                {
                    Mask = mask,
                    Bounds = bounds,
                    ActiveCellCount = activeCellCount,
                    GenerationAttempt = attempt,
                    Seed = seed
                };
                return true;
            }

            return false;
        }

        public static bool ValidateConnectivity(bool[,] mask, int expectedActiveCellCount = -1)
        {
            if (mask == null)
            {
                return false;
            }

            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            Vector2Int start = default;
            bool foundStart = false;
            for (int x = 0; x < width && !foundStart; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!mask[x, y])
                    {
                        continue;
                    }

                    start = new Vector2Int(x, y);
                    foundStart = true;
                    break;
                }
            }

            if (!foundStart)
            {
                return false;
            }

            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int neighbor = current + Directions[i];
                    if (!IsInside(neighbor.x, neighbor.y, width, height) ||
                        !mask[neighbor.x, neighbor.y] || !visited.Add(neighbor))
                    {
                        continue;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            int requiredCount = expectedActiveCellCount >= 0
                ? expectedActiveCellCount
                : CountActiveCells(mask);
            return visited.Count == requiredCount;
        }

        public static int CountActiveCells(bool[,] mask)
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
                    if (mask[x, y])
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public static ShapeBounds CalculateBounds(bool[,] mask)
        {
            if (mask == null)
            {
                return ShapeBounds.Invalid;
            }

            int minX = mask.GetLength(0);
            int maxX = -1;
            int minY = mask.GetLength(1);
            int maxY = -1;
            for (int x = 0; x < mask.GetLength(0); x++)
            {
                for (int y = 0; y < mask.GetLength(1); y++)
                {
                    if (!mask[x, y])
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            return maxX >= minX
                ? new ShapeBounds(minX, maxX, minY, maxY)
                : ShapeBounds.Invalid;
        }

        private static bool[,] GenerateConnectedMask(
            int width,
            int height,
            int minAllowedX,
            int maxAllowedX,
            int minAllowedY,
            int maxAllowedY,
            int targetCells,
            ProceduralShapeSettings settings,
            System.Random random)
        {
            var mask = new bool[width, height];
            var activeCells = new HashSet<Vector2Int>();
            var frontier = new List<FrontierCandidate>();
            var frontierPositions = new HashSet<Vector2Int>();

            int centerX = (minAllowedX + maxAllowedX) / 2;
            int centerY = (minAllowedY + maxAllowedY) / 2;
            int offset = Mathf.Max(0, settings.seedOffsetRadius);
            var seedCell = new Vector2Int(
                Mathf.Clamp(centerX + random.Next(-offset, offset + 1), minAllowedX, maxAllowedX),
                Mathf.Clamp(centerY + random.Next(-offset, offset + 1), minAllowedY, maxAllowedY));
            ActivateCell(
                seedCell,
                mask,
                activeCells,
                frontier,
                frontierPositions,
                minAllowedX,
                maxAllowedX,
                minAllowedY,
                maxAllowedY);

            Vector2Int lastCell = seedCell;
            Vector2Int lastDirection = Vector2Int.zero;
            while (activeCells.Count < targetCells)
            {
                RemoveActivatedFrontierEntries(frontier, frontierPositions, activeCells);
                if (frontier.Count == 0)
                {
                    break;
                }

                int selectedIndex = SelectWeightedFrontierIndex(
                    frontier,
                    lastCell,
                    lastDirection,
                    centerX,
                    centerY,
                    maxAllowedX - minAllowedX + 1,
                    maxAllowedY - minAllowedY + 1,
                    settings,
                    random);
                FrontierCandidate selected = frontier[selectedIndex];
                frontier.RemoveAt(selectedIndex);
                frontierPositions.Remove(selected.Position);

                ActivateCell(
                    selected.Position,
                    mask,
                    activeCells,
                    frontier,
                    frontierPositions,
                    minAllowedX,
                    maxAllowedX,
                    minAllowedY,
                    maxAllowedY);
                lastCell = selected.Position;
                lastDirection = selected.Direction;

                int minimumRadius = Mathf.Max(0, settings.minBrushRadius);
                int maximumRadius = Mathf.Max(minimumRadius, settings.maxBrushRadius);
                int brushRadius = random.NextDouble() <= Mathf.Clamp01(settings.brushChance)
                    ? random.Next(minimumRadius, maximumRadius + 1)
                    : 0;
                if (brushRadius > 0 && activeCells.Count < targetCells)
                {
                    PaintBrush(
                        selected.Position,
                        brushRadius,
                        targetCells,
                        mask,
                        activeCells,
                        frontier,
                        frontierPositions,
                        minAllowedX,
                        maxAllowedX,
                        minAllowedY,
                        maxAllowedY);
                }
            }

            return mask;
        }

        private static int SelectWeightedFrontierIndex(
            List<FrontierCandidate> frontier,
            Vector2Int lastCell,
            Vector2Int lastDirection,
            int centerX,
            int centerY,
            int allowedWidth,
            int allowedHeight,
            ProceduralShapeSettings settings,
            System.Random random)
        {
            var weights = new double[frontier.Count];
            double totalWeight = 0d;
            float maximumDistance = Mathf.Max(1f, (allowedWidth + allowedHeight) * 0.5f);
            for (int i = 0; i < frontier.Count; i++)
            {
                FrontierCandidate candidate = frontier[i];
                float distance = Mathf.Abs(candidate.Position.x - centerX) +
                    Mathf.Abs(candidate.Position.y - centerY);
                float centerFactor = 1f - Mathf.Clamp01(distance / maximumDistance);
                double weight = 1d + Mathf.Clamp01(settings.centerBias) * centerFactor * 3d;

                if (candidate.Direction == lastDirection && lastDirection != Vector2Int.zero)
                {
                    weight *= 1d + Mathf.Clamp01(settings.directionPersistence) * 4d;
                }

                if (candidate.Parent == lastCell)
                {
                    weight *= 1d + (1d - Mathf.Clamp01(settings.branchChance)) * 2d;
                }
                else
                {
                    weight *= 1d + Mathf.Clamp01(settings.branchChance);
                }

                weights[i] = weight;
                totalWeight += weight;
            }

            double roll = random.NextDouble() * totalWeight;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll <= 0d)
                {
                    return i;
                }
            }

            return weights.Length - 1;
        }

        private static void PaintBrush(
            Vector2Int center,
            int radius,
            int targetCells,
            bool[,] mask,
            HashSet<Vector2Int> activeCells,
            List<FrontierCandidate> frontier,
            HashSet<Vector2Int> frontierPositions,
            int minAllowedX,
            int maxAllowedX,
            int minAllowedY,
            int maxAllowedY)
        {
            var positions = new List<Vector2Int>();
            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    positions.Add(center + new Vector2Int(offsetX, offsetY));
                }
            }

            positions.Sort((left, right) =>
                ManhattanDistance(left, center).CompareTo(ManhattanDistance(right, center)));
            for (int i = 0; i < positions.Count && activeCells.Count < targetCells; i++)
            {
                Vector2Int position = positions[i];
                if (!IsAllowed(position, minAllowedX, maxAllowedX, minAllowedY, maxAllowedY) ||
                    activeCells.Contains(position) || !HasActiveNeighbor(position, mask))
                {
                    continue;
                }

                ActivateCell(
                    position,
                    mask,
                    activeCells,
                    frontier,
                    frontierPositions,
                    minAllowedX,
                    maxAllowedX,
                    minAllowedY,
                    maxAllowedY);
            }
        }

        private static void ActivateCell(
            Vector2Int position,
            bool[,] mask,
            HashSet<Vector2Int> activeCells,
            List<FrontierCandidate> frontier,
            HashSet<Vector2Int> frontierPositions,
            int minAllowedX,
            int maxAllowedX,
            int minAllowedY,
            int maxAllowedY)
        {
            if (!activeCells.Add(position))
            {
                return;
            }

            mask[position.x, position.y] = true;
            frontierPositions.Remove(position);
            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int neighbor = position + Directions[i];
                if (!IsAllowed(neighbor, minAllowedX, maxAllowedX, minAllowedY, maxAllowedY) ||
                    activeCells.Contains(neighbor) || !frontierPositions.Add(neighbor))
                {
                    continue;
                }

                frontier.Add(new FrontierCandidate(neighbor, position, Directions[i]));
            }
        }

        private static void RemoveActivatedFrontierEntries(
            List<FrontierCandidate> frontier,
            HashSet<Vector2Int> frontierPositions,
            HashSet<Vector2Int> activeCells)
        {
            for (int i = frontier.Count - 1; i >= 0; i--)
            {
                if (!activeCells.Contains(frontier[i].Position))
                {
                    continue;
                }

                frontierPositions.Remove(frontier[i].Position);
                frontier.RemoveAt(i);
            }
        }

        private static void CleanupMask(
            bool[,] mask,
            int minAllowedX,
            int maxAllowedX,
            int minAllowedY,
            int maxAllowedY,
            int minimumCells,
            int maximumCells,
            ProceduralShapeSettings settings,
            System.Random random)
        {
            int activeCellCount = CountActiveCells(mask);
            int iterations = Mathf.Clamp(settings.cleanupIterations, 0, 5);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var gapsToFill = new List<Vector2Int>();
                for (int x = minAllowedX; x <= maxAllowedX; x++)
                {
                    for (int y = minAllowedY; y <= maxAllowedY; y++)
                    {
                        if (!mask[x, y] && CountActiveNeighbors(mask, x, y) >= 3)
                        {
                            gapsToFill.Add(new Vector2Int(x, y));
                        }
                    }
                }

                for (int i = 0; i < gapsToFill.Count && activeCellCount < maximumCells; i++)
                {
                    Vector2Int gap = gapsToFill[i];
                    mask[gap.x, gap.y] = true;
                    activeCellCount++;
                }

                var spikesToRemove = new List<Vector2Int>();
                for (int x = minAllowedX; x <= maxAllowedX; x++)
                {
                    for (int y = minAllowedY; y <= maxAllowedY; y++)
                    {
                        if (mask[x, y] && CountActiveNeighbors(mask, x, y) == 1 &&
                            random.NextDouble() < Mathf.Clamp01(settings.spikeRemovalChance))
                        {
                            spikesToRemove.Add(new Vector2Int(x, y));
                        }
                    }
                }

                for (int i = 0; i < spikesToRemove.Count && activeCellCount > minimumCells; i++)
                {
                    Vector2Int spike = spikesToRemove[i];
                    mask[spike.x, spike.y] = false;
                    activeCellCount--;
                }
            }
        }

        private static int CountActiveNeighbors(bool[,] mask, int x, int y)
        {
            int count = 0;
            for (int i = 0; i < Directions.Length; i++)
            {
                int neighborX = x + Directions[i].x;
                int neighborY = y + Directions[i].y;
                if (IsInside(neighborX, neighborY, mask.GetLength(0), mask.GetLength(1)) &&
                    mask[neighborX, neighborY])
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasActiveNeighbor(Vector2Int position, bool[,] mask)
        {
            return CountActiveNeighbors(mask, position.x, position.y) > 0;
        }

        private static int ManhattanDistance(Vector2Int left, Vector2Int right)
        {
            return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
        }

        private static bool IsAllowed(
            Vector2Int position,
            int minAllowedX,
            int maxAllowedX,
            int minAllowedY,
            int maxAllowedY)
        {
            return position.x >= minAllowedX && position.x <= maxAllowedX &&
                position.y >= minAllowedY && position.y <= maxAllowedY;
        }

        private static bool IsInside(int x, int y, int width, int height)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }
    }
}
