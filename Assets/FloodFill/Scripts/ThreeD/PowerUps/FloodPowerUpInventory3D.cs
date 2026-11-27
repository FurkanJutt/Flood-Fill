using System;
using System.IO;
using UnityEngine;
namespace FloodFill.ThreeD.PowerUps
{
    // Write-ahead transaction: inventory and the corresponding complete attempt are durable together.
    public static class FloodPowerUpInventory3D
    {
        [Serializable] private sealed class Store
        {
            public int version=1;
            public int[] quantities=new int[3];
            public long revision;
            public FloodFillSessionSaveData3D pendingSession;
            public bool hasPendingSession;
        }
        private static Store data;
        private static FloodPowerUpSettings3D settings;
        public static event Action Changed;
        public static string LastError {get;private set;}
        public static string StorePath=>Path.Combine(Application.persistentDataPath,"power-up-inventory-v1.json");
        public static long Revision {get{Ensure();return data.revision;}}
        private static FloodPowerUpSettings3D Settings
        {
            get{if(settings==null)settings=Resources.Load<FloodPowerUpSettings3D>("FloodPowerUps");if(settings==null)settings=ScriptableObject.CreateInstance<FloodPowerUpSettings3D>();return settings;}
        }
        public static void Configure(FloodPowerUpSettings3D value)
        {
            settings=value;Ensure();bool changed=false;
            for(int i=0;i<3;i++){int n=Mathf.Clamp(data.quantities[i],0,Mathf.Max(1,Settings.Definition((FloodPowerUpKind)i).maximumQuantity));changed|=n!=data.quantities[i];data.quantities[i]=n;}
            if(changed)Write();
        }
        private static void Ensure()
        {
            if(data!=null)return;
            bool existed=File.Exists(StorePath)||File.Exists(StorePath+".bak");
            foreach(string path in new[]{StorePath,StorePath+".bak"})
            {
                if(!File.Exists(path))continue;
                try{var loaded=JsonUtility.FromJson<Store>(File.ReadAllText(path));if(loaded!=null&&loaded.version==1&&loaded.quantities?.Length==3&&Array.TrueForAll(loaded.quantities,q=>q>=0)&&loaded.revision>=0){data=loaded;break;}}
                catch(Exception e){Debug.LogWarning("Power-up inventory recovery: "+e.Message);}
            }
            if(data==null)
            {
                data=new Store();
                for(int i=0;i<3;i++)data.quantities[i]=existed?0:Mathf.Clamp(Settings.Definition((FloodPowerUpKind)i).startingQuantity,0,Settings.Definition((FloodPowerUpKind)i).maximumQuantity);
                if(existed)LastError="Damaged inventory could not be recovered; quantities are unavailable rather than refunded.";
                Write();
            }
        }
        private static bool Write()
        {
            if(!Settings.saveInventory)return true;
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                string temporary=StorePath+".tmp";File.WriteAllText(temporary,JsonUtility.ToJson(data));
                if(File.Exists(StorePath))File.Replace(temporary,StorePath,StorePath+".bak");else File.Move(temporary,StorePath);
                LastError=null;return true;
            }
            catch(Exception e){LastError="Power-up save failed: "+e.Message;Debug.LogWarning(LastError);return false;}
        }
        public static int Quantity(FloodPowerUpKind kind){Ensure();return Mathf.Clamp(data.quantities[(int)kind],0,Mathf.Max(1,Settings.Definition(kind).maximumQuantity));}
        public static bool Available(FloodPowerUpKind kind)=>Settings.Definition(kind).enabled&&(Settings.Unlimited||Quantity(kind)>0);
        public static bool Commit(FloodPowerUpKind kind,FloodFillSessionSaveData3D after,bool free=false)
        {
            Ensure();if(!free&&!Available(kind))return false;
            int previous=data.quantities[(int)kind];long revision=data.revision;var pending=data.pendingSession;bool hadPending=data.hasPendingSession;
            if(!free&&!Settings.Unlimited)data.quantities[(int)kind]=Math.Max(0,previous-1);
            data.revision++;after.powerUpRevision=data.revision;data.pendingSession=JsonUtility.FromJson<FloodFillSessionSaveData3D>(JsonUtility.ToJson(after));data.hasPendingSession=true;
            if(!Write()){data.quantities[(int)kind]=previous;data.revision=revision;data.pendingSession=pending;data.hasPendingSession=hadPending;return false;}
            // If this write fails, the already durable journal is recovered before the next Continue load.
            FloodFillSessionPersistence3D.Save(after);Changed?.Invoke();return true;
        }
        public static void Acknowledge(long revision)
        {
            Ensure();if(!data.hasPendingSession||data.pendingSession==null||revision<data.pendingSession.powerUpRevision)return;
            if(data.pendingSession.gameState==(int)FloodFillGameManager3D.GameState.Won&&data.pendingSession.numberedLevel>0)
            {
                var saved=data.pendingSession;
                try
                {
                    if(FloodFillSessionPersistence3D.TryLoadLevel(saved.numberedMode,saved.difficulty,saved.numberedLevel,out var level,out _))
                    {
                        var tracker=new Goals.FloodSecondaryGoalTracker3D(level.id,level.goals,level.initialCaptured,level.palette.Length,saved.secondaryGoalAttempt);
                        FloodLevelProgression3D.RecordSecondaryGoals(saved.numberedMode,saved.difficulty,saved.numberedLevel,level.goals,tracker.Complete(true,saved.moveCount,saved.board.currentPlayerColor,saved.elapsedSeconds),saved.elapsedSeconds);
                    }
                    FloodLevelProgression3D.Record(saved.numberedMode,saved.difficulty,saved.numberedLevel,saved.moveCount,FloodLevelProgression3D.Stars(saved.moveCount,saved.currentMoveBudget));
                }
                catch(Exception e){LastError="Winning power-up transaction will be recovered later: "+e.Message;return;}
            }
            Achievements.FloodAchievementService.RecoverCheckpoint(data.pendingSession);
            var pending=data.pendingSession;data.pendingSession=null;data.hasPendingSession=false;if(!Write()){data.pendingSession=pending;data.hasPendingSession=true;}
        }
        public static void RecoverPendingSession()
        {
            Ensure();if(!data.hasPendingSession||data.pendingSession==null)return;
            FloodFillSessionSaveData3D current=null;
            try{if(File.Exists(FloodFillSessionPersistence3D.SavePath))current=JsonUtility.FromJson<FloodFillSessionSaveData3D>(File.ReadAllText(FloodFillSessionPersistence3D.SavePath));}catch(Exception){ }
            if(current!=null&&current.powerUpRevision>=data.pendingSession.powerUpRevision){Acknowledge(current.powerUpRevision);return;}
            FloodFillSessionPersistence3D.Save(data.pendingSession);
        }
        public static bool Add(FloodPowerUpKind kind,int amount)
        {
            Ensure();RecoverPendingSession();int old=data.quantities[(int)kind];
            data.quantities[(int)kind]=(int)Math.Max(0,Math.Min((long)Mathf.Max(1,Settings.Definition(kind).maximumQuantity),(long)old+amount));
            if(!Write()){data.quantities[(int)kind]=old;return false;}Changed?.Invoke();return true;
        }
#if UNITY_EDITOR
        public static void ResetCacheForVerification(){data=null;settings=null;LastError=null;}
#endif
    }
}
