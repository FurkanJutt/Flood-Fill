using System;

namespace FloodFill.ThreeD.Solver
{
    public enum FloodFillDifficulty
    {
        Easy,
        Normal,
        Hard,
        Perfect
    }

    [Serializable]
    public sealed class MoveSolverSettings
    {
        public int exactSolverRegionThreshold = 128;
        public int beamWidth = 128;
        public int greedyRuns = 6;
        public int runtimeExactSearchTimeBudgetMs = 40;
        public int developmentExactSearchTimeBudgetMs = 200;
        public int exactMaxExpandedStates = 100000;
        public int beamSearchTimeBudgetMs = 200;
        public int greedySearchTimeBudgetMs = 200;
        public int maximumSearchMemoryMegabytes = 32;
        public int lookaheadDepth = 3;
        public int lookaheadBranchLimit = 2;
        public int lookaheadNodeLimit = 128;
        public bool allowAsyncForLargeBoards = true;
        public int asyncRegionThreshold = 64;
        public bool logSolverPerformance;

        public int complexityThreshold1 = 80;
        public int complexityThreshold2 = 150;
        public int complexityThreshold3 = 250;
        public int complexityThreshold4 = 400;
        public float complexityMultiplier1 = 1.10f;
        public float complexityMultiplier2 = 1.25f;
        public float complexityMultiplier3 = 1.40f;
        public float complexityMultiplier4 = 1.60f;
        public int maximumMoveBudget = 1000000;

        internal MoveSolverRuntimeSettings CreateRuntimeSettings(bool developmentBudget)
        {
            int memoryLimitedExactNodes = (int)Math.Min(int.MaxValue, Math.Max(100L,
                (long)Math.Max(1, maximumSearchMemoryMegabytes) * 1024L * 1024L / 128L));
            return new MoveSolverRuntimeSettings(
                Math.Max(1, exactSolverRegionThreshold),
                Math.Max(1, beamWidth),
                Math.Max(1, greedyRuns),
                Math.Max(1, developmentBudget
                    ? developmentExactSearchTimeBudgetMs
                    : runtimeExactSearchTimeBudgetMs),
                Math.Min(Math.Max(1, exactMaxExpandedStates), memoryLimitedExactNodes),
                Math.Max(1, beamSearchTimeBudgetMs),
                Math.Max(1, maximumSearchMemoryMegabytes),
                Math.Max(1, Math.Min(5, lookaheadDepth)),
                Math.Max(1, Math.Min(6, lookaheadBranchLimit)),
                Math.Max(8, Math.Min(4096, lookaheadNodeLimit)),
                Math.Max(1, greedySearchTimeBudgetMs));
        }
    }

    internal readonly struct MoveSolverRuntimeSettings
    {
        public MoveSolverRuntimeSettings(
            int exactRegionThreshold,
            int beamWidth,
            int greedyRuns,
            int exactMilliseconds,
            int exactNodes,
            int beamMilliseconds,
            int maximumSearchMemoryMegabytes,
            int lookaheadDepth,
            int lookaheadBranchLimit,
            int lookaheadNodeLimit,
            int greedyMilliseconds)
        {
            ExactRegionThreshold = exactRegionThreshold;
            BeamWidth = beamWidth;
            GreedyRuns = greedyRuns;
            ExactMilliseconds = exactMilliseconds;
            ExactNodes = exactNodes;
            BeamMilliseconds = beamMilliseconds;
            MaximumSearchMemoryMegabytes = maximumSearchMemoryMegabytes;
            LookaheadDepth = lookaheadDepth;
            LookaheadBranchLimit = lookaheadBranchLimit;
            LookaheadNodeLimit = lookaheadNodeLimit;
            GreedyMilliseconds = greedyMilliseconds;
        }

        public int GreedyMilliseconds { get; }
        public int ExactRegionThreshold { get; }
        public int BeamWidth { get; }
        public int GreedyRuns { get; }
        public int ExactMilliseconds { get; }
        public int ExactNodes { get; }
        public int BeamMilliseconds { get; }
        public int MaximumSearchMemoryMegabytes { get; }
        public int LookaheadDepth { get; }
        public int LookaheadBranchLimit { get; }
        public int LookaheadNodeLimit { get; }
    }

    [Serializable]
    public struct MoveBudgetProfile
    {
        public int bestSolutionMoves;
        public int easyMoves;
        public int normalMoves;
        public int hardMoves;
        public int perfectMoves;
        public bool isProvenOptimal;
        public float complexityMultiplier;

        public int GetMoves(FloodFillDifficulty difficulty)
        {
            switch (difficulty)
            {
                case FloodFillDifficulty.Easy: return easyMoves;
                case FloodFillDifficulty.Hard: return hardMoves;
                case FloodFillDifficulty.Perfect: return perfectMoves;
                default: return normalMoves;
            }
        }
    }

    public readonly struct FloodFillSolverMove3D
    {
        public FloodFillSolverMove3D(int regionId, int colorIndex)
        {
            RegionId = regionId;
            ColorIndex = colorIndex;
        }

        public int RegionId { get; }
        public int ColorIndex { get; }
    }

    public sealed class FloodFillSolverResult
    {
        public int BestMoveCount { get; internal set; }
        public bool IsProvenOptimal { get; internal set; }
        public int[] BestColorSequence { get; internal set; } = Array.Empty<int>();
        public int[] BestRegionSequence { get; internal set; } = Array.Empty<int>();
        public double ElapsedMilliseconds { get; internal set; }
        public double GraphBuildMilliseconds { get; internal set; }
        public double GreedyMilliseconds { get; internal set; }
        public double BeamMilliseconds { get; internal set; }
        public double ExactMilliseconds { get; internal set; }
        public int ExploredStates { get; internal set; }
        public int RegionCount { get; internal set; }
        public int DistinctColorCount { get; internal set; }
        public int PlayableVoxelCount { get; internal set; }
        public int GreedyMoveCount { get; internal set; }
        public int BeamMoveCount { get; internal set; }
        public int ExactMoveCount { get; internal set; } = -1;
        public bool UsedFallback { get; internal set; }
        public bool SolutionValidated { get; internal set; }
        public string SearchAlgorithm { get; internal set; } = string.Empty;
        public string SearchTerminationReason { get; internal set; } = string.Empty;
        public int UniqueStates { get; internal set; }
        public int DuplicateStatesSkipped { get; internal set; }
        public long EstimatedPeakSearchMemoryBytes { get; internal set; }
        public MoveBudgetProfile BudgetProfile { get; internal set; }
    }

    public sealed class FloodFillBoardSnapshot3D
    {
        public FloodFillBoardSnapshot3D(
            int width,
            int height,
            int depth,
            int colorCount,
            int[] logicalIndices,
            byte[] colors,
            int startingVoxel)
        {
            Width = width;
            Height = height;
            Depth = depth;
            ColorCount = colorCount;
            this.logicalIndices = logicalIndices == null ? null : (int[])logicalIndices.Clone();
            this.colors = colors == null ? null : (byte[])colors.Clone();
            StartingVoxel = startingVoxel;
        }

        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }
        public int ColorCount { get; }
        private readonly int[] logicalIndices;
        private readonly byte[] colors;
        public int[] LogicalIndices => logicalIndices == null ? null : (int[])logicalIndices.Clone();
        public byte[] Colors => colors == null ? null : (byte[])colors.Clone();
        public int StartingVoxel { get; }
        public int VoxelCount => logicalIndices?.Length ?? 0;
    }
}
