using System;
using UnityEngine;

namespace FloodFill.Development.AutoSolver
{
    internal sealed class AutoSolverBoardSnapshot
    {
        public int Width;
        public int Height;
        public int Depth;
        public int ColorCount;
        public int MoveCount;
        public int MoveLimit;
        public int OriginalSolutionLength;
        public string OriginalSolutionSource;
        public bool OriginalOptimalityProven;
        public int SelectedColor;
        public int CurrentPlayerColor;
        public string GameState;
        public int[] LogicalIndices;
        public byte[] Colors;
        public bool[] Captured;
        public Component[] Voxels;
        public int StartingVoxelIndex;

        public int VoxelCount => Colors != null ? Colors.Length : 0;
        public int CapturedCount
        {
            get
            {
                if (Captured == null) return 0;
                int count = 0;
                for (int i = 0; i < Captured.Length; i++) if (Captured[i]) count++;
                return count;
            }
        }
        public int RemainingMoves => Mathf.Max(0, MoveLimit - MoveCount);
    }

    internal sealed class AutoSolverGraph
    {
        public FloodFill.ThreeD.Solver.FloodFillRegionGraph3D Core;
        public int Width;
        public int Height;
        public int Depth;
        public int ColorCount;
        public int TotalVoxels;
        public int[] VoxelRegionIds;
        public int[] RepresentativeVoxelIndices;
        public int[] RegionVoxelCounts;
        public byte[] RegionColors;
        public int[][] Neighbors;
        public int ConnectedComponentCount;
        public int RegionCount => RegionColors != null ? RegionColors.Length : 0;
    }

    internal readonly struct AutoSolverMove
    {
        public AutoSolverMove(int regionId, int colorIndex)
        {
            RegionId = regionId;
            ColorIndex = colorIndex;
        }
        public int RegionId { get; }
        public int ColorIndex { get; }
    }

    internal enum AutoSolverConclusion
    {
        Solved,
        ProvenNoSolution,
        Inconclusive,
        Cancelled,
        Invalid
    }

    internal sealed class AutoSolverResult
    {
        public FloodFill.ThreeD.Solver.FloodFillSolverResult CoreResult;
        public AutoSolverConclusion Conclusion;
        public AutoSolverMove[] Moves = Array.Empty<AutoSolverMove>();
        public int[] CapturedPerMove = Array.Empty<int>();
        public int ExploredStates;
        public int RegionCount;
        public int PlayableVoxels;
        public int RemainingMoveLimit;
        public bool IsProvenOptimal;
        public bool SearchWasExhaustive;
        public bool ValidationPassed;
        public double CalculationMilliseconds;
        public string Message;
    }
}
