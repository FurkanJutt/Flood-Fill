using System;
using FloodFill.ThreeD.Solver;
using UnityEngine;

namespace FloodFill.ThreeD.Goals
{
    public enum SecondaryGoalType { MoveSaver, FinalColor, BigCapture, QuickStart, ComboCapture, SpeedRun, LimitedColorUsage, FinalSprint, FastExpansion, FinalColorEfficiency }
    public enum SecondaryGoalState { NotCompleted, InProgress, CompletedThisAttempt, PreviouslyEarned, FailedThisAttempt }
    [Serializable] public sealed class SecondaryGoalDefinition
    {
        public string id;
        public SecondaryGoalType type;
        public int target, deadline, color = -1, count;
        public double seconds;
        public bool estimatedTiming;
        public string Description(string[] names, string[] formats = null)
        {
            string c = color >= 0 && names != null && color < names.Length ? names[color] : "color " + (color + 1);
            if(formats!=null&&(int)type>=0&&(int)type<formats.Length&&!string.IsNullOrEmpty(formats[(int)type]))
                return formats[(int)type].Replace("{color}",c).Replace("{target}",target.ToString()).Replace("{deadline}",deadline.ToString()).Replace("{count}",count.ToString()).Replace("{seconds}",Math.Ceiling(seconds).ToString());
            switch (type)
            {
                case SecondaryGoalType.MoveSaver: return $"Finish within {target} moves.";
                case SecondaryGoalType.FinalColor: return $"Finish with {c}.";
                case SecondaryGoalType.BigCapture: return $"Capture {target} new cubes in one move.";
                case SecondaryGoalType.QuickStart: case SecondaryGoalType.FastExpansion: return $"Capture {count}% within {deadline} moves.";
                case SecondaryGoalType.ComboCapture: return $"Capture {target}+ new cubes on {count} moves.";
                case SecondaryGoalType.SpeedRun: return $"Finish within {Math.Ceiling(seconds)} seconds.";
                case SecondaryGoalType.LimitedColorUsage: return $"Select {c} at most {target} times.";
                case SecondaryGoalType.FinalSprint: return $"After reaching {count}%, finish in {deadline} more moves.";
                default: return $"Finish with {c} within {target} moves.";
            }
        }
    }
    [Serializable] public sealed class SecondaryGoalProgress { public int value, sprintStart = -1; public bool met, failed; }
    [Serializable] public sealed class SecondaryGoalAttempt
    {
        public string levelId;
        public int lastMove, initialCaptured;
        public int[] colorSelections;
        public SecondaryGoalProgress[] progress;
    }
    [Serializable] public sealed class FloodNumberedLevelDefinition3D
    {
        public int version = 1, generatorVersion = 1, goalGeneratorVersion = 1;
        public string id, boardHash, validation = "Validated winning witness";
        public int mode, difficulty, level, seed, moveBudget, regionCount, initialCaptured;
        public string itemIdentifier, generationParameters;
        public Color[] palette;
        [NonSerialized] public VoxelBoardSaveData3D board;
        public CompactInitialBoard3D snapshot;
        public MoveBudgetProfile budgetProfile;
        public int[] witnessRegions, witnessColors;
        public SecondaryGoalDefinition[] goals;
        public int requestedGoalCount = 3;
        public double goalGenerationMilliseconds;
        public FloodFillBoardSnapshot3D Snapshot()
        {
            var indices = new int[board.voxels.Length]; var colors = new byte[indices.Length]; int start = -1;
            for (int i = 0; i < indices.Length; i++) { var v = board.voxels[i]; indices[i] = v.x + board.width * (v.y + board.height * v.z); colors[i] = (byte)v.colorIndex; if (v.starting) start = i; }
            return new FloodFillBoardSnapshot3D(board.width, board.height, board.depth, palette.Length, indices, colors, start);
        }
        public FloodFillSolverResult SolverResult() => new FloodFillSolverResult { SolutionValidated = true, BestMoveCount = witnessColors.Length, BestColorSequence = witnessColors, BestRegionSequence = witnessRegions, BudgetProfile = budgetProfile, RegionCount = regionCount, PlayableVoxelCount = board.voxels.Length, SearchAlgorithm = "Saved validated level witness", IsProvenOptimal = budgetProfile.isProvenOptimal };
    }
    [Serializable] public sealed class CompactInitialBoard3D
    {
        public int width,height,depth,volumeMode,currentPlayerColor,generationSeed;
        public string voxels;
        public static CompactInitialBoard3D Pack(VoxelBoardSaveData3D board)
        {
            var bytes=new byte[board.voxels.Length*4];
            for(int i=0;i<board.voxels.Length;i++)
            {
                var v=board.voxels[i];int logical=v.x+board.width*(v.y+board.height*v.z);
                if(v.colorIndex<0||v.colorIndex>=64||logical<0||logical>0xffffff)throw new ArgumentException("Snapshot packing limit exceeded.");
                bytes[i*4]=(byte)logical;bytes[i*4+1]=(byte)(logical>>8);bytes[i*4+2]=(byte)(logical>>16);bytes[i*4+3]=(byte)(v.colorIndex|(v.captured?128:0)|(v.starting?64:0));
            }
            return new CompactInitialBoard3D{width=board.width,height=board.height,depth=board.depth,volumeMode=board.volumeMode,currentPlayerColor=board.currentPlayerColor,generationSeed=board.generationSeed,voxels=Convert.ToBase64String(bytes)};
        }
        public VoxelBoardSaveData3D Unpack()
        {
            if(width<1||height<1||depth<1||width>64||height>64||depth>64)throw new ArgumentException("Invalid snapshot dimensions.");
            byte[] bytes=Convert.FromBase64String(voxels);if(bytes.Length==0||bytes.Length%4!=0||bytes.Length/4>width*height*depth)throw new ArgumentException("Invalid snapshot payload.");
            var board=new VoxelBoardSaveData3D{width=width,height=height,depth=depth,volumeMode=volumeMode,currentPlayerColor=currentPlayerColor,generationSeed=generationSeed,voxels=new VoxelSaveData3D[bytes.Length/4]};
            for(int i=0;i<board.voxels.Length;i++){int logical=bytes[i*4]|bytes[i*4+1]<<8|bytes[i*4+2]<<16;int flags=bytes[i*4+3];board.voxels[i]=new VoxelSaveData3D{x=logical%width,y=logical/width%height,z=logical/(width*height),colorIndex=flags&63,captured=(flags&128)!=0,starting=(flags&64)!=0};}
            return board;
        }
    }
    public sealed class SecondarySolutionTrace
    {
        public int total, regions, initialCaptured, finalColor;
        public int[] colors, newlyCaptured, captured, colorSelections, initialColorCounts;
        public int Length => colors.Length;
    }
}
