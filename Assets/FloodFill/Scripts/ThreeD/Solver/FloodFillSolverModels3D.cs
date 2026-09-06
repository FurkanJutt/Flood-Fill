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
        public int exactSolverRegionThreshold = 64;
        public int beamWidth = 128;
        public int greedyRuns = 6;
        public int runtimeExactSearchTimeBudgetMs = 40;
        public int developmentExactSearchTimeBudgetMs = 200;
        public int exactMaxExpandedStates = 100000;
        public int beamSearchTimeBudgetMs = 80;
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
            return new MoveSolverRuntimeSettings(
                Math.Max(1, exactSolverRegionThreshold),
                Math.Max(1, beamWidth),
                Math.Max(1, greedyRuns),
                Math.Max(1, developmentBudget
                    ? developmentExactSearchTimeBudgetMs
                    : runtimeExactSearchTimeBudgetMs),
                Math.Max(1, exactMaxExpandedStates),
                Math.Max(1, beamSearchTimeBudgetMs));
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
            int beamMilliseconds)
        {
            ExactRegionThreshold = exactRegionThreshold;
            BeamWidth = beamWidth;
            GreedyRuns = greedyRuns;
            ExactMilliseconds = exactMilliseconds;
            ExactNodes = exactNodes;
            BeamMilliseconds = beamMilliseconds;
        }

        public int ExactRegionThreshold { get; }
        public int BeamWidth { get; }
        public int GreedyRuns { get; }
        public int ExactMilliseconds { get; }
        public int ExactNodes { get; }
        public int BeamMilliseconds { get; }
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
        public int PlayableVoxelCount { get; internal set; }
        public int GreedyMoveCount { get; internal set; }
        public int BeamMoveCount { get; internal set; }
        public int ExactMoveCount { get; internal set; } = -1;
        public bool UsedFallback { get; internal set; }
        public bool SolutionValidated { get; internal set; }
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
            LogicalIndices = logicalIndices;
            Colors = colors;
            StartingVoxel = startingVoxel;
        }

        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }
        public int ColorCount { get; }
        public int[] LogicalIndices { get; }
        public byte[] Colors { get; }
        public int StartingVoxel { get; }
        public int VoxelCount => LogicalIndices?.Length ?? 0;
    }
}
