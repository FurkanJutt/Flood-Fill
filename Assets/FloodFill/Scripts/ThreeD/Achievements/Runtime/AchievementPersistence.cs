using System;
using System.IO;
using UnityEngine;
namespace FloodFill.ThreeD.Achievements
{
    public sealed class AchievementPersistence
    {
        public string Path {get;}
        public bool Writable {get;private set;}=true;
        public string LastError {get;private set;}
        public AchievementPersistence(string path){Path=path;}
        public AchievementSaveData Load()
        {
            bool existed=false;
            foreach(string path in new[]{Path,Path+".bak"})
            {
                if(!File.Exists(path))continue;existed=true;
                try
                {
                    var data=JsonUtility.FromJson<AchievementSaveData>(File.ReadAllText(path));
                    if(data!=null&&data.version==1&&data.tiers!=null&&data.levels!=null&&data.receipts!=null&&data.activeDays!=null){Normalize(data);return data;}
                }
                catch(Exception e){LastError=e.Message;}
            }
            if(existed){Writable=false;LastError="Achievement data could not be recovered; original files preserved. "+LastError;Debug.LogWarning(LastError);}
            return new AchievementSaveData();
        }
        private static void Normalize(AchievementSaveData data)
        {
            if(data.statistics==null)data.statistics=new System.Collections.Generic.List<AchievementStatistics>();
            foreach(var p in data.tiers)
            {
                if(p.uniqueKeys==null)p.uniqueKeys=new System.Collections.Generic.List<string>();if(p.claimedRewardIds==null)p.claimedRewardIds=new System.Collections.Generic.List<string>();if(p.recentColors==null)p.recentColors=new System.Collections.Generic.List<int>();if(p.colorCounts==null||p.colorCounts.Length!=6)p.colorCounts=new int[6];
            }
            foreach(var r in data.receipts)if(r.pending==null)r.pending=new System.Collections.Generic.List<AchievementDeferred>();
            foreach(var r in data.levels){if(r.earnedGoals==null)r.earnedGoals=new System.Collections.Generic.List<string>();if(r.assignedGoals==null)r.assignedGoals=new System.Collections.Generic.List<string>();}
            foreach(var s in data.statistics)
            {
                if(s.powerUses==null||s.powerUses.Length!=3)s.powerUses=new int[3];if(s.powerDirectCubes==null||s.powerDirectCubes.Length!=3)s.powerDirectCubes=new long[3];if(s.powerConnectedCubes==null||s.powerConnectedCubes.Length!=3)s.powerConnectedCubes=new long[3];if(s.powerMergedRegions==null||s.powerMergedRegions.Length!=3)s.powerMergedRegions=new long[3];
            }
        }
        public bool Save(AchievementSaveData data)
        {
            if(!Writable)return false;
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));string temp=Path+".tmp";
                File.WriteAllText(temp,JsonUtility.ToJson(data));
                if(File.Exists(Path))File.Replace(temp,Path,Path+".bak");else File.Move(temp,Path);
                LastError=null;return true;
            }
            catch(Exception e){LastError=e.Message;Debug.LogWarning("Achievement save failed: "+e.Message);return false;}
        }
    }
}
