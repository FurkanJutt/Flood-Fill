using System;
using System.Collections.Generic;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

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

    public readonly struct ProceduralVoxelGenerationTimings
    {
        public ProceduralVoxelGenerationTimings(
            double growth,
            double notch,
            double exteriorFlood,
            double surfaceExtraction,
            double validation,
            double total)
        {
            GrowthMilliseconds = growth;
            NotchMilliseconds = notch;
            ExteriorFloodMilliseconds = exteriorFlood;
            SurfaceExtractionMilliseconds = surfaceExtraction;
            ValidationMilliseconds = validation;
            TotalMilliseconds = total;
        }

        public double GrowthMilliseconds { get; }
        public double NotchMilliseconds { get; }
        public double ExteriorFloodMilliseconds { get; }
        public double SurfaceExtractionMilliseconds { get; }
        public double ValidationMilliseconds { get; }
        public double TotalMilliseconds { get; }
    }

    public sealed class ProceduralVoxelShapeResult
    {
        public bool[,,] Mask { get; internal set; }
        public VoxelShapeBounds Bounds { get; internal set; }
        public int ActiveVoxelCount { get; internal set; }
        public int TargetSolidVoxelCount { get; internal set; }
        public int SolidVoxelCount { get; internal set; }
        public int GenerationAttempt { get; internal set; }
        public int Seed { get; internal set; }
        public ProceduralVoxelGenerationTimings Timings { get; internal set; }
    }

    public static class ProceduralVoxelShapeGenerator
    {
        private const byte Empty = 0;
        private const byte Frontier = 1;
        private const byte Solid = 2;

        private static readonly int[] DirectionX = { 1, -1, 0, 0, 0, 0 };
        private static readonly int[] DirectionY = { 0, 0, 1, -1, 0, 0 };
        private static readonly int[] DirectionZ = { 0, 0, 0, 0, 1, -1 };
        private static readonly GeneratorWorkspace Workspace = new GeneratorWorkspace();

        private sealed class GeneratorWorkspace
        {
            public byte[] States = Array.Empty<byte>();
            public byte[] OutsideAir = Array.Empty<byte>();
            public byte[] Surface = Array.Empty<byte>();
            public byte[] Visited = Array.Empty<byte>();
            public int[] FrontierSlots = Array.Empty<int>();
            public int[] SurfaceComponents = Array.Empty<int>();
            public sbyte[] FrontierDirections = Array.Empty<sbyte>();
            public int[] Queue = Array.Empty<int>();
            public readonly List<int> Frontier = new List<int>();
            public readonly List<int> NotchCandidates = new List<int>();
            public readonly List<int> RemovedNotches = new List<int>();
            public readonly int[] AttractorX = new int[8];
            public readonly int[] AttractorY = new int[8];
            public readonly int[] AttractorZ = new int[8];

            public void Prepare(int volume, int paddedVolume)
            {
                EnsureCapacity(ref States, volume);
                EnsureCapacity(ref Surface, volume);
                EnsureCapacity(ref Visited, volume);
                EnsureCapacity(ref FrontierSlots, volume);
                EnsureCapacity(ref SurfaceComponents, volume);
                EnsureCapacity(ref FrontierDirections, volume);
                EnsureCapacity(ref OutsideAir, paddedVolume);
                EnsureCapacity(ref Queue, Mathf.Max(volume, paddedVolume));
                Array.Clear(States, 0, volume);
                Array.Clear(Surface, 0, volume);
                Array.Clear(Visited, 0, volume);
                Array.Clear(SurfaceComponents, 0, volume);
                Array.Clear(OutsideAir, 0, paddedVolume);
                for (int i = 0; i < volume; i++)
                {
                    FrontierSlots[i] = -1;
                    FrontierDirections[i] = -1;
                }

                Frontier.Clear();
                NotchCandidates.Clear();
                RemovedNotches.Clear();
                if (Frontier.Capacity < volume)
                {
                    Frontier.Capacity = volume;
                    NotchCandidates.Capacity = volume;
                    RemovedNotches.Capacity = volume;
                }
            }

            private static void EnsureCapacity<T>(ref T[] buffer, int required)
            {
                if (buffer.Length < required)
                {
                    buffer = new T[required];
                }
            }
        }

        private struct TimingAccumulator
        {
            public double Growth;
            public double Notch;
            public double Exterior;
            public double Surface;
            public double Validation;
        }

        private readonly struct EffectiveShapeProfile
        {
            public EffectiveShapeProfile(
                float minFill,
                float maxFill,
                float brushChance,
                float branchChance,
                float directionPersistence,
                float centerBias,
                float lobeStrength,
                int minimumLobes,
                int maximumLobes,
                int weightedCandidateSamples,
                float simplification)
            {
                MinFill = minFill;
                MaxFill = maxFill;
                BrushChance = brushChance;
                BranchChance = branchChance;
                DirectionPersistence = directionPersistence;
                CenterBias = centerBias;
                LobeStrength = lobeStrength;
                MinimumLobes = minimumLobes;
                MaximumLobes = maximumLobes;
                WeightedCandidateSamples = weightedCandidateSamples;
                Simplification = simplification;
            }

            public float MinFill { get; }
            public float MaxFill { get; }
            public float BrushChance { get; }
            public float BranchChance { get; }
            public float DirectionPersistence { get; }
            public float CenterBias { get; }
            public float LobeStrength { get; }
            public int MinimumLobes { get; }
            public int MaximumLobes { get; }
            public int WeightedCandidateSamples { get; }
            public float Simplification { get; }
        }

        public static bool TryGenerate(
            int width,
            int height,
            int depth,
            ProceduralVoxelShapeSettings settings,
            int seed,
            out ProceduralVoxelShapeResult result)
        {
            lock (Workspace)
            {
                return TryGenerateLocked(width, height, depth, settings, seed, out result);
            }
        }

        private static bool TryGenerateLocked(
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

            Stopwatch totalWatch = Stopwatch.StartNew();
            var timings = new TimingAccumulator();
            int margin = Mathf.Clamp(
                settings.edgeMargin,
                0,
                Mathf.Max(0, (Mathf.Min(width, Mathf.Min(height, depth)) - 1) / 2));
            int minX = margin;
            int minY = margin;
            int minZ = margin;
            int maxX = width - margin - 1;
            int maxY = height - margin - 1;
            int maxZ = depth - margin - 1;
            int allowedWidth = maxX - minX + 1;
            int allowedHeight = maxY - minY + 1;
            int allowedDepth = maxZ - minZ + 1;
            int capacity = allowedWidth * allowedHeight * allowedDepth;
            EffectiveShapeProfile profile = CreateEffectiveProfile(
                width, height, depth, settings);
            float minFill = profile.MinFill;
            float maxFill = profile.MaxFill;
            int minimumSolid = Mathf.Clamp(Mathf.CeilToInt(capacity * minFill), 1, capacity);
            int maximumSolid = Mathf.Clamp(
                Mathf.FloorToInt(capacity * maxFill), minimumSolid, capacity);
            int requiredX = Mathf.Clamp(settings.minimumSpanX, 1, allowedWidth);
            int requiredY = Mathf.Clamp(settings.minimumSpanY, 1, allowedHeight);
            int requiredZ = Mathf.Clamp(settings.minimumSpanZ, 1, allowedDepth);
            int volume = width * height * depth;
            int paddedWidth = width + 2;
            int paddedHeight = height + 2;
            int paddedDepth = depth + 2;
            int paddedVolume = paddedWidth * paddedHeight * paddedDepth;
            int attempts = Mathf.Clamp(settings.maxGenerationAttempts, 1, 3);

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                Workspace.Prepare(volume, paddedVolume);
                var random = new System.Random(unchecked(seed + attempt * 104729));
                int targetSolid = random.Next(minimumSolid, maximumSolid + 1);
                int attractorCount = CreateAttractors(
                    minX, maxX, minY, maxY, minZ, maxZ, profile, random);
                bool useBrush = attempt < 3;
                bool useNotches = attempt == 1 && settings.enableNotches &&
                    settings.edgeNotchPasses > 0 && settings.edgeNotchChance > 0f;

                Stopwatch phaseWatch = Stopwatch.StartNew();
                int seedIndex = GrowConnectedSolid(
                    width, height, minX, maxX, minY, maxY, minZ, maxZ,
                    targetSolid, attractorCount, settings, profile,
                    useBrush, random, out int solidCount);
                phaseWatch.Stop();
                timings.Growth += phaseWatch.Elapsed.TotalMilliseconds;
                if (solidCount < minimumSolid)
                {
                    continue;
                }

                phaseWatch.Restart();
                if (useNotches)
                {
                    solidCount = CarveNotchesBatch(
                        width, height, depth, minimumSolid, seedIndex, solidCount, settings, random);
                }
                phaseWatch.Stop();
                timings.Notch += phaseWatch.Elapsed.TotalMilliseconds;

                phaseWatch.Restart();
                FloodOutsideAir(width, height, depth, paddedWidth, paddedHeight, paddedDepth);
                phaseWatch.Stop();
                timings.Exterior += phaseWatch.Elapsed.TotalMilliseconds;

                phaseWatch.Restart();
                int rawSurfaceCount = ExtractExteriorSurfaceFlags(
                    width, height, depth, paddedWidth, paddedHeight);
                phaseWatch.Stop();
                timings.Surface += phaseWatch.Elapsed.TotalMilliseconds;

                phaseWatch.Restart();
                bool[,,] mask = BuildLargestConnectedSurface(
                    width, height, depth, rawSurfaceCount,
                    out int surfaceCount, out VoxelShapeBounds bounds);
                bool valid = surfaceCount > 0 && bounds.IsValid &&
                    bounds.Width >= requiredX && bounds.Height >= requiredY &&
                    bounds.Depth >= requiredZ;
                phaseWatch.Stop();
                timings.Validation += phaseWatch.Elapsed.TotalMilliseconds;
                if (!valid)
                {
                    continue;
                }

                totalWatch.Stop();
                result = new ProceduralVoxelShapeResult
                {
                    Mask = mask,
                    Bounds = bounds,
                    ActiveVoxelCount = surfaceCount,
                    TargetSolidVoxelCount = targetSolid,
                    SolidVoxelCount = solidCount,
                    GenerationAttempt = attempt,
                    Seed = seed,
                    Timings = new ProceduralVoxelGenerationTimings(
                        timings.Growth, timings.Notch, timings.Exterior, timings.Surface,
                        timings.Validation, totalWatch.Elapsed.TotalMilliseconds)
                };
                return true;
            }

            totalWatch.Stop();
            return false;
        }

        public static bool ValidateConnectivity(bool[,,] mask, int expectedActiveCount = -1)
        {
            if (mask == null)
            {
                return false;
            }

            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            int depth = mask.GetLength(2);
            int volume = width * height * depth;
            var visited = new byte[volume];
            var queue = new int[volume];
            int start = -1;
            int actualCount = 0;
            for (int z = 0; z < depth; z++)
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (!mask[x, y, z])
                {
                    continue;
                }

                int index = ToIndex(x, y, z, width, height);
                if (start < 0)
                {
                    start = index;
                }
                actualCount++;
            }

            if (start < 0)
            {
                return false;
            }

            int head = 0;
            int tail = 0;
            int reached = 0;
            queue[tail++] = start;
            visited[start] = 1;
            while (head < tail)
            {
                int index = queue[head++];
                reached++;
                FromIndex(index, width, height, out int x, out int y, out int z);
                for (int direction = 0; direction < 6; direction++)
                {
                    int nx = x + DirectionX[direction];
                    int ny = y + DirectionY[direction];
                    int nz = z + DirectionZ[direction];
                    if (!IsInside(nx, ny, nz, width, height, depth) || !mask[nx, ny, nz])
                    {
                        continue;
                    }

                    int neighbor = ToIndex(nx, ny, nz, width, height);
                    if (visited[neighbor] != 0)
                    {
                        continue;
                    }

                    visited[neighbor] = 1;
                    queue[tail++] = neighbor;
                }
            }

            int required = expectedActiveCount >= 0 ? expectedActiveCount : actualCount;
            return reached == required;
        }

        public static int CountActiveVoxels(bool[,,] mask)
        {
            if (mask == null)
            {
                return 0;
            }

            int count = 0;
            for (int x = 0; x < mask.GetLength(0); x++)
            for (int y = 0; y < mask.GetLength(1); y++)
            for (int z = 0; z < mask.GetLength(2); z++)
            {
                if (mask[x, y, z])
                {
                    count++;
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
            for (int y = 0; y < mask.GetLength(1); y++)
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

            return maxX >= minX
                ? new VoxelShapeBounds(minX, maxX, minY, maxY, minZ, maxZ)
                : VoxelShapeBounds.Invalid;
        }

        private static EffectiveShapeProfile CreateEffectiveProfile(
            int width,
            int height,
            int depth,
            ProceduralVoxelShapeSettings settings)
        {
            int largestDimension = Mathf.Max(width, Mathf.Max(height, depth));
            int startSize = Mathf.Max(1, settings.simplificationStartSize);
            int fullSize = Mathf.Max(startSize + 1, settings.fullSimplificationSize);
            float sizeProgress = Mathf.InverseLerp(startSize, fullSize, largestDimension);
            float simplification = sizeProgress *
                Mathf.Clamp01(settings.largeBoardSimplification);

            float minFill = Mathf.Clamp(
                settings.minSolidFillPercent + simplification * 0.08f, 0.05f, 1f);
            float maxFill = Mathf.Clamp(
                settings.maxSolidFillPercent + simplification * 0.05f, minFill, 1f);
            float brushChance = Mathf.Lerp(
                settings.brushChance, 0.60f, simplification);
            float branchChance = Mathf.Clamp01(settings.branchChance) *
                Mathf.Lerp(1f, 0.35f, simplification);
            float directionPersistence = Mathf.Clamp01(settings.directionPersistence) *
                Mathf.Lerp(1f, 0.50f, simplification);
            float centerBias = Mathf.Lerp(
                settings.centerBias, 0.78f, simplification);
            float lobeStrength = Mathf.Clamp01(settings.lobeStrength) *
                Mathf.Lerp(1f, 0.30f, simplification);
            int minimumLobes = Mathf.Clamp(settings.minimumLobes, 1, 8);
            int configuredMaximumLobes = Mathf.Clamp(
                settings.maximumLobes, minimumLobes, 8);
            int maximumLobes = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(
                    configuredMaximumLobes, minimumLobes, simplification)),
                minimumLobes,
                configuredMaximumLobes);
            int weightedCandidateSamples = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(
                    settings.weightedCandidateSamples, 12f, simplification)),
                1,
                16);

            return new EffectiveShapeProfile(
                minFill,
                maxFill,
                brushChance,
                branchChance,
                directionPersistence,
                centerBias,
                lobeStrength,
                minimumLobes,
                maximumLobes,
                weightedCandidateSamples,
                simplification);
        }

        private static int GrowConnectedSolid(
            int width, int height,
            int minX, int maxX, int minY, int maxY, int minZ, int maxZ,
            int targetCount, int attractorCount, ProceduralVoxelShapeSettings settings,
            EffectiveShapeProfile profile, bool useBrush,
            System.Random random, out int solidCount)
        {
            int centerX = (minX + maxX) / 2;
            int centerY = (minY + maxY) / 2;
            int centerZ = (minZ + maxZ) / 2;
            int offset = Mathf.Max(0, settings.seedOffsetRadius);
            int seedX = Mathf.Clamp(centerX + random.Next(-offset, offset + 1), minX, maxX);
            int seedY = Mathf.Clamp(centerY + random.Next(-offset, offset + 1), minY, maxY);
            int seedZ = Mathf.Clamp(centerZ + random.Next(-offset, offset + 1), minZ, maxZ);
            int seedIndex = ToIndex(seedX, seedY, seedZ, width, height);
            solidCount = 0;
            Activate(seedIndex, width, height, minX, maxX, minY, maxY, minZ, maxZ, ref solidCount);

            int previousDirection = -1;
            while (solidCount < targetCount && Workspace.Frontier.Count > 0)
            {
                int frontierSlot = random.NextDouble() < profile.BranchChance
                    ? random.Next(Workspace.Frontier.Count)
                    : SelectTournamentCandidate(
                        width, height, minX, maxX, minY, maxY, minZ, maxZ,
                        attractorCount, previousDirection, profile, random);
                int selected = RemoveFrontierAt(frontierSlot);
                previousDirection = Workspace.FrontierDirections[selected];
                Activate(selected, width, height, minX, maxX, minY, maxY, minZ, maxZ, ref solidCount);

                if (!useBrush || solidCount >= targetCount ||
                    random.NextDouble() >= profile.BrushChance)
                {
                    continue;
                }

                FromIndex(selected, width, height, out int x, out int y, out int z);
                int directionOffset = random.Next(6);
                for (int i = 0; i < 6 && solidCount < targetCount; i++)
                {
                    int direction = (directionOffset + i) % 6;
                    int nx = x + DirectionX[direction];
                    int ny = y + DirectionY[direction];
                    int nz = z + DirectionZ[direction];
                    if (!IsInside(nx, ny, nz, minX, maxX, minY, maxY, minZ, maxZ) ||
                        random.NextDouble() > 0.45d)
                    {
                        continue;
                    }

                    int neighbor = ToIndex(nx, ny, nz, width, height);
                    if (Workspace.States[neighbor] == Solid)
                    {
                        continue;
                    }

                    if (Workspace.States[neighbor] == Frontier)
                    {
                        RemoveFrontierAt(Workspace.FrontierSlots[neighbor]);
                    }

                    Activate(neighbor, width, height, minX, maxX, minY, maxY,
                        minZ, maxZ, ref solidCount);
                }
            }

            return seedIndex;
        }

        private static int SelectTournamentCandidate(
            int width, int height,
            int minX, int maxX, int minY, int maxY, int minZ, int maxZ,
            int attractorCount, int previousDirection, EffectiveShapeProfile profile,
            System.Random random)
        {
            int samples = profile.WeightedCandidateSamples;
            int bestSlot = random.Next(Workspace.Frontier.Count);
            float bestScore = ScoreCandidate(
                Workspace.Frontier[bestSlot], width, height, minX, maxX, minY, maxY,
                minZ, maxZ, attractorCount, previousDirection, profile, random);
            for (int sample = 1; sample < samples; sample++)
            {
                int slot = random.Next(Workspace.Frontier.Count);
                float score = ScoreCandidate(
                    Workspace.Frontier[slot], width, height, minX, maxX, minY, maxY,
                    minZ, maxZ, attractorCount, previousDirection, profile, random);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = slot;
                }
            }

            return bestSlot;
        }

        private static float ScoreCandidate(
            int index, int width, int height,
            int minX, int maxX, int minY, int maxY, int minZ, int maxZ,
            int attractorCount, int previousDirection, EffectiveShapeProfile profile,
            System.Random random)
        {
            FromIndex(index, width, height, out int x, out int y, out int z);
            float centerX = (minX + maxX) * 0.5f;
            float centerY = (minY + maxY) * 0.5f;
            float centerZ = (minZ + maxZ) * 0.5f;
            float sizeX = maxX - minX + 1;
            float sizeY = maxY - minY + 1;
            float sizeZ = maxZ - minZ + 1;
            float maximumDistanceSquared = Mathf.Max(
                1f, sizeX * sizeX + sizeY * sizeY + sizeZ * sizeZ);
            float dx = x - centerX;
            float dy = y - centerY;
            float dz = z - centerZ;
            float legacyCenterProximity = 1f - Mathf.Clamp01(
                (dx * dx + dy * dy + dz * dz) / maximumDistanceSquared);
            float halfX = (maxX - minX) * 0.5f;
            float halfY = (maxY - minY) * 0.5f;
            float halfZ = (maxZ - minZ) * 0.5f;
            float centerRadiusSquared = Mathf.Max(
                1f, halfX * halfX + halfY * halfY + halfZ * halfZ);
            float compactCenterProximity = 1f - Mathf.Clamp01(
                (dx * dx + dy * dy + dz * dz) / centerRadiusSquared);
            float centerProximity = Mathf.Lerp(
                legacyCenterProximity, compactCenterProximity, profile.Simplification);
            float nearestAttractorSquared = maximumDistanceSquared;
            for (int i = 0; i < attractorCount; i++)
            {
                dx = x - Workspace.AttractorX[i];
                dy = y - Workspace.AttractorY[i];
                dz = z - Workspace.AttractorZ[i];
                float distanceSquared = dx * dx + dy * dy + dz * dz;
                if (distanceSquared < nearestAttractorSquared)
                {
                    nearestAttractorSquared = distanceSquared;
                }
            }

            float lobeProximity = 1f - Mathf.Clamp01(
                nearestAttractorSquared / maximumDistanceSquared);
            float score = 0.12f + centerProximity * profile.CenterBias +
                lobeProximity * profile.LobeStrength * 2.4f;
            if (Workspace.FrontierDirections[index] == previousDirection)
            {
                score += profile.DirectionPersistence * 2.2f;
            }

            float scoreJitter = Mathf.Lerp(0.18f, 0.06f, profile.Simplification);
            return score * (1f - scoreJitter +
                (float)random.NextDouble() * scoreJitter * 2f);
        }

        private static int CreateAttractors(
            int minX, int maxX, int minY, int maxY, int minZ, int maxZ,
            EffectiveShapeProfile profile, System.Random random)
        {
            int minimum = Mathf.Clamp(profile.MinimumLobes, 1, 8);
            int maximum = Mathf.Clamp(profile.MaximumLobes, minimum, 8);
            int count = random.Next(minimum, maximum + 1);
            for (int i = 0; i < count; i++)
            {
                Workspace.AttractorX[i] = random.Next(minX, maxX + 1);
                Workspace.AttractorY[i] = random.Next(minY, maxY + 1);
                Workspace.AttractorZ[i] = random.Next(minZ, maxZ + 1);
            }

            return count;
        }

        private static void Activate(
            int index, int width, int height,
            int minX, int maxX, int minY, int maxY, int minZ, int maxZ,
            ref int solidCount)
        {
            if (Workspace.States[index] == Solid)
            {
                return;
            }

            Workspace.States[index] = Solid;
            Workspace.FrontierSlots[index] = -1;
            solidCount++;
            FromIndex(index, width, height, out int x, out int y, out int z);
            for (int direction = 0; direction < 6; direction++)
            {
                int nx = x + DirectionX[direction];
                int ny = y + DirectionY[direction];
                int nz = z + DirectionZ[direction];
                if (!IsInside(nx, ny, nz, minX, maxX, minY, maxY, minZ, maxZ))
                {
                    continue;
                }

                int neighbor = ToIndex(nx, ny, nz, width, height);
                if (Workspace.States[neighbor] != Empty)
                {
                    continue;
                }

                Workspace.States[neighbor] = Frontier;
                Workspace.FrontierDirections[neighbor] = (sbyte)direction;
                Workspace.FrontierSlots[neighbor] = Workspace.Frontier.Count;
                Workspace.Frontier.Add(neighbor);
            }
        }

        private static int RemoveFrontierAt(int slot)
        {
            int lastSlot = Workspace.Frontier.Count - 1;
            int removed = Workspace.Frontier[slot];
            if (slot != lastSlot)
            {
                int moved = Workspace.Frontier[lastSlot];
                Workspace.Frontier[slot] = moved;
                Workspace.FrontierSlots[moved] = slot;
            }

            Workspace.Frontier.RemoveAt(lastSlot);
            Workspace.FrontierSlots[removed] = -1;
            return removed;
        }

        private static int CarveNotchesBatch(
            int width, int height, int depth, int minimumSolid, int seedIndex,
            int solidCount, ProceduralVoxelShapeSettings settings, System.Random random)
        {
            int volume = width * height * depth;
            Workspace.NotchCandidates.Clear();
            Workspace.RemovedNotches.Clear();
            for (int index = 0; index < volume; index++)
            {
                if (Workspace.States[index] == Solid &&
                    IsRawSurfaceSolid(index, width, height, depth))
                {
                    Workspace.NotchCandidates.Add(index);
                }
            }

            Shuffle(Workspace.NotchCandidates, random);
            int passes = Mathf.Clamp(settings.edgeNotchPasses, 0, 2);
            int minimumNeighbors = Mathf.Clamp(settings.minimumNotchSolidNeighbors, 2, 5);
            for (int pass = 0; pass < passes && solidCount > minimumSolid; pass++)
            {
                for (int i = 0; i < Workspace.NotchCandidates.Count &&
                    solidCount > minimumSolid; i++)
                {
                    int candidate = Workspace.NotchCandidates[i];
                    if (Workspace.States[candidate] != Solid ||
                        random.NextDouble() > settings.edgeNotchChance ||
                        CountSolidNeighbors(candidate, width, height, depth) < minimumNeighbors)
                    {
                        continue;
                    }

                    Workspace.States[candidate] = Empty;
                    Workspace.RemovedNotches.Add(candidate);
                    solidCount--;
                }
            }

            if (Workspace.RemovedNotches.Count == 0 ||
                ValidateSolidConnectivity(width, height, depth, seedIndex, solidCount))
            {
                return solidCount;
            }

            for (int i = 0; i < Workspace.RemovedNotches.Count; i++)
            {
                Workspace.States[Workspace.RemovedNotches[i]] = Solid;
            }

            return solidCount + Workspace.RemovedNotches.Count;
        }

        private static bool ValidateSolidConnectivity(
            int width, int height, int depth, int start, int expectedCount)
        {
            int volume = width * height * depth;
            Array.Clear(Workspace.Visited, 0, volume);
            if (start < 0 || Workspace.States[start] != Solid)
            {
                start = -1;
                for (int i = 0; i < volume; i++)
                {
                    if (Workspace.States[i] == Solid)
                    {
                        start = i;
                        break;
                    }
                }
            }

            if (start < 0)
            {
                return false;
            }

            int head = 0;
            int tail = 0;
            int reached = 0;
            Workspace.Queue[tail++] = start;
            Workspace.Visited[start] = 1;
            while (head < tail)
            {
                int index = Workspace.Queue[head++];
                reached++;
                FromIndex(index, width, height, out int x, out int y, out int z);
                for (int direction = 0; direction < 6; direction++)
                {
                    int nx = x + DirectionX[direction];
                    int ny = y + DirectionY[direction];
                    int nz = z + DirectionZ[direction];
                    if (!IsInside(nx, ny, nz, width, height, depth))
                    {
                        continue;
                    }

                    int neighbor = ToIndex(nx, ny, nz, width, height);
                    if (Workspace.States[neighbor] != Solid || Workspace.Visited[neighbor] != 0)
                    {
                        continue;
                    }

                    Workspace.Visited[neighbor] = 1;
                    Workspace.Queue[tail++] = neighbor;
                }
            }

            return reached == expectedCount;
        }

        private static void FloodOutsideAir(
            int width, int height, int depth,
            int paddedWidth, int paddedHeight, int paddedDepth)
        {
            int head = 0;
            int tail = 0;
            Workspace.Queue[tail++] = 0;
            Workspace.OutsideAir[0] = 1;
            while (head < tail)
            {
                int paddedIndex = Workspace.Queue[head++];
                FromIndex(paddedIndex, paddedWidth, paddedHeight, out int px, out int py, out int pz);
                for (int direction = 0; direction < 6; direction++)
                {
                    int nx = px + DirectionX[direction];
                    int ny = py + DirectionY[direction];
                    int nz = pz + DirectionZ[direction];
                    if (!IsInside(nx, ny, nz, paddedWidth, paddedHeight, paddedDepth))
                    {
                        continue;
                    }

                    int neighborPadded = ToIndex(nx, ny, nz, paddedWidth, paddedHeight);
                    if (Workspace.OutsideAir[neighborPadded] != 0)
                    {
                        continue;
                    }

                    int x = nx - 1;
                    int y = ny - 1;
                    int z = nz - 1;
                    if (IsInside(x, y, z, width, height, depth) &&
                        Workspace.States[ToIndex(x, y, z, width, height)] == Solid)
                    {
                        continue;
                    }

                    Workspace.OutsideAir[neighborPadded] = 1;
                    Workspace.Queue[tail++] = neighborPadded;
                }
            }
        }

        private static int ExtractExteriorSurfaceFlags(
            int width, int height, int depth, int paddedWidth, int paddedHeight)
        {
            int volume = width * height * depth;
            Array.Clear(Workspace.Surface, 0, volume);
            int surfaceCount = 0;
            for (int z = 0; z < depth; z++)
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = ToIndex(x, y, z, width, height);
                if (Workspace.States[index] != Solid ||
                    !TouchesOutsideAir(x, y, z, width, height, depth, paddedWidth, paddedHeight))
                {
                    continue;
                }

                Workspace.Surface[index] = 1;
                surfaceCount++;
            }

            return surfaceCount;
        }

        private static bool[,,] BuildLargestConnectedSurface(
            int width,
            int height,
            int depth,
            int rawSurfaceCount,
            out int surfaceCount,
            out VoxelShapeBounds bounds)
        {
            int volume = width * height * depth;
            Array.Clear(Workspace.SurfaceComponents, 0, volume);
            int componentId = 0;
            int largestComponentId = 0;
            int largestComponentCount = 0;
            int labeledCount = 0;
            for (int start = 0; start < volume && labeledCount < rawSurfaceCount; start++)
            {
                if (Workspace.Surface[start] == 0 ||
                    Workspace.SurfaceComponents[start] != 0)
                {
                    continue;
                }

                componentId++;
                int head = 0;
                int tail = 0;
                int componentCount = 0;
                Workspace.Queue[tail++] = start;
                Workspace.SurfaceComponents[start] = componentId;
                while (head < tail)
                {
                    int index = Workspace.Queue[head++];
                    componentCount++;
                    labeledCount++;
                    FromIndex(index, width, height, out int x, out int y, out int z);
                    for (int direction = 0; direction < 6; direction++)
                    {
                        int nx = x + DirectionX[direction];
                        int ny = y + DirectionY[direction];
                        int nz = z + DirectionZ[direction];
                        if (!IsInside(nx, ny, nz, width, height, depth))
                        {
                            continue;
                        }

                        int neighbor = ToIndex(nx, ny, nz, width, height);
                        if (Workspace.Surface[neighbor] == 0 ||
                            Workspace.SurfaceComponents[neighbor] != 0)
                        {
                            continue;
                        }

                        Workspace.SurfaceComponents[neighbor] = componentId;
                        Workspace.Queue[tail++] = neighbor;
                    }
                }

                if (componentCount > largestComponentCount)
                {
                    largestComponentCount = componentCount;
                    largestComponentId = componentId;
                }
            }

            var mask = new bool[width, height, depth];
            surfaceCount = 0;
            int minX = width;
            int minY = height;
            int minZ = depth;
            int maxX = -1;
            int maxY = -1;
            int maxZ = -1;
            for (int index = 0; index < volume; index++)
            {
                if (Workspace.SurfaceComponents[index] != largestComponentId ||
                    largestComponentId == 0)
                {
                    Workspace.Surface[index] = 0;
                    continue;
                }

                Workspace.Surface[index] = 1;
                FromIndex(index, width, height, out int x, out int y, out int z);
                mask[x, y, z] = true;
                surfaceCount++;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                minZ = Mathf.Min(minZ, z);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
                maxZ = Mathf.Max(maxZ, z);
            }

            bounds = maxX >= minX
                ? new VoxelShapeBounds(minX, maxX, minY, maxY, minZ, maxZ)
                : VoxelShapeBounds.Invalid;
            return mask;
        }

        private static bool TouchesOutsideAir(
            int x, int y, int z, int width, int height, int depth,
            int paddedWidth, int paddedHeight)
        {
            for (int direction = 0; direction < 6; direction++)
            {
                int nx = x + DirectionX[direction];
                int ny = y + DirectionY[direction];
                int nz = z + DirectionZ[direction];
                if (!IsInside(nx, ny, nz, width, height, depth))
                {
                    return true;
                }

                if (Workspace.States[ToIndex(nx, ny, nz, width, height)] == Solid)
                {
                    continue;
                }

                int outsideIndex = ToIndex(nx + 1, ny + 1, nz + 1, paddedWidth, paddedHeight);
                if (Workspace.OutsideAir[outsideIndex] != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRawSurfaceSolid(int index, int width, int height, int depth)
        {
            FromIndex(index, width, height, out int x, out int y, out int z);
            for (int direction = 0; direction < 6; direction++)
            {
                int nx = x + DirectionX[direction];
                int ny = y + DirectionY[direction];
                int nz = z + DirectionZ[direction];
                if (!IsInside(nx, ny, nz, width, height, depth) ||
                    Workspace.States[ToIndex(nx, ny, nz, width, height)] != Solid)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountSolidNeighbors(int index, int width, int height, int depth)
        {
            FromIndex(index, width, height, out int x, out int y, out int z);
            int count = 0;
            for (int direction = 0; direction < 6; direction++)
            {
                int nx = x + DirectionX[direction];
                int ny = y + DirectionY[direction];
                int nz = z + DirectionZ[direction];
                if (IsInside(nx, ny, nz, width, height, depth) &&
                    Workspace.States[ToIndex(nx, ny, nz, width, height)] == Solid)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Shuffle(List<int> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                int temporary = values[i];
                values[i] = values[swap];
                values[swap] = temporary;
            }
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

        private static bool IsInside(
            int x, int y, int z,
            int minX, int maxX, int minY, int maxY, int minZ, int maxZ)
        {
            return x >= minX && x <= maxX && y >= minY && y <= maxY &&
                z >= minZ && z <= maxZ;
        }
    }
}
