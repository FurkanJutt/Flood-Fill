using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FloodFill.ThreeD.Goals;
using FloodFill.ThreeD.Solver;
using UnityEngine;
namespace FloodFill.ThreeD
{
    // Additive immutable definitions in the existing persistence service; session checkpoints stay separate.
    public static partial class FloodFillSessionPersistence3D
    {
        private static readonly Dictionary<string,FloodNumberedLevelDefinition3D> Levels=new Dictionary<string,FloodNumberedLevelDefinition3D>();
        private static readonly Queue<string> LevelOrder=new Queue<string>();
        private static void Cache(string path,FloodNumberedLevelDefinition3D definition)
        {
            if(!Levels.ContainsKey(path)){while(Levels.Count>=8)Levels.Remove(LevelOrder.Dequeue());LevelOrder.Enqueue(path);}
            Levels[path]=definition;
        }
        public static string LevelId(int mode,int difficulty,int level) { FloodLevelProgression3D.Index(mode,difficulty,level);return $"{mode}_{difficulty}_{level}"; }
        public static string LevelPath(int mode,int difficulty,int level)=>Path.Combine(Application.persistentDataPath,"numbered-levels-v1",LevelId(mode,difficulty,level)+".json");
        private static string HashBoardJson(string json)
        {
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-","");
        }
        public static string BoardHash(VoxelBoardSaveData3D board)=>HashBoardJson(Regex.Replace(JsonUtility.ToJson(board),",\"covered\":(?:true|false)",""));
        private static bool MatchesBoardHash(VoxelBoardSaveData3D board,string expected)
        {
            if(BoardHash(board)==expected)return true;
            // Accept files written during the coverage-field rollout, before compact snapshots
            // discarded that attempt-only flag. Geometry and colors remain fully hashed.
            string json=JsonUtility.ToJson(board);
            return HashBoardJson(json)==expected||HashBoardJson(Regex.Replace(json,"\"captured\":(true|false),\"covered\":(?:true|false)","\"captured\":$1,\"covered\":$1"))==expected;
        }
        public static bool DiscardUnreadableLevel(int mode,int difficulty,int level,out string error)
        {
            error=null;string path=LevelPath(mode,difficulty,level);
            try
            {
                // Only called after the player's explicit overwrite confirmation.
                if(File.Exists(path))File.Move(path,path+".replaced-"+DateTime.UtcNow.Ticks+".bak");
                Levels.Clear();LevelOrder.Clear();return true;
            }
            catch(Exception e){error="The previous save could not be replaced: "+e.Message;return false;}
        }
        public static bool TryLoadLevel(int mode,int difficulty,int level,out FloodNumberedLevelDefinition3D definition,out string error)
        {
            definition=null;error=null;string path=LevelPath(mode,difficulty,level);
            if(Levels.TryGetValue(path,out definition))return true;
            if(!File.Exists(path))return false;
            try
            {
                definition=JsonUtility.FromJson<FloodNumberedLevelDefinition3D>(File.ReadAllText(path));
                if(definition?.snapshot!=null)definition.board=definition.snapshot.Unpack();
                if(definition==null||definition.version!=1||definition.goalGeneratorVersion!=1||definition.generatorVersion!=1||definition.id!=LevelId(mode,difficulty,level)||definition.mode!=mode||definition.difficulty!=difficulty||definition.level!=level||definition.board?.voxels==null||definition.palette==null||definition.goals==null||definition.goals.Length>3||!MatchesBoardHash(definition.board,definition.boardHash)||definition.moveBudget<1||definition.moveBudget>1000000)throw new InvalidDataException("Unsupported or damaged definition.");
                if(!FloodFillRegionGraph3D.TryBuild(definition.Snapshot(),out var graph))throw new InvalidDataException("Invalid board snapshot.");
                var trace=FloodSecondaryGoalGenerator3D.Trace(graph,definition.witnessRegions,definition.witnessColors,definition.moveBudget,2048);
                if(definition.initialCaptured!=trace.initialCaptured||definition.regionCount!=graph.RegionCount||definition.moveBudget<definition.witnessColors.Length)throw new InvalidDataException("Inconsistent trace metadata.");
                foreach(var goal in definition.goals)if(goal==null||!FloodSecondaryGoalGenerator3D.Demonstrated(goal,trace))throw new InvalidDataException("Goal witness is invalid.");
                Cache(path,definition);return true;
            }
            catch(Exception e){definition=null;error="Saved level could not be read. Its file has been preserved: "+e.Message;return false;}
        }
        public static bool SaveLevel(FloodNumberedLevelDefinition3D definition,out string error)
        {
            error=null;string path=LevelPath(definition.mode,definition.difficulty,definition.level);
            try
            {
                // Existing definitions are immutable; never replace them after a generator update.
                if(File.Exists(path))throw new IOException("A saved definition already exists.");
                Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+".tmp";
                definition.boardHash=BoardHash(definition.board);definition.snapshot=CompactInitialBoard3D.Pack(definition.board);
                File.WriteAllText(temporary,JsonUtility.ToJson(definition));File.Move(temporary,path);Cache(path,definition);return true;
            }
            catch(Exception e){error="The level could not be saved: "+e.Message;return false;}
        }
    }
}
