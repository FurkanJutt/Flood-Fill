using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace FloodFill.ThreeD.Achievements
{
    /// <summary>One offline service; runtime catalog may later be supplied by another provider.</summary>
    public sealed class FloodAchievementService:MonoBehaviour
    {
        private static FloodAchievementService instance;
        public static FloodAchievementService Instance
        {
            get
            {
                if(instance==null&&Application.isPlaying)
                {
                    var go=new GameObject("FloodAchievementService");instance=go.AddComponent<FloodAchievementService>();
                }
                return instance;
            }
        }
        public FloodAchievementSettings settings;
        public AchievementEngine Engine {get;private set;}
        public AchievementPersistence Persistence {get;private set;}
        public IAchievementRewardService RewardService {get;set;}
        public bool Ready=>Engine!=null&&Persistence!=null&&Persistence.Writable&&settings!=null&&settings.enableAchievementSystem;
        private bool dirty,important,applicationPaused,applicationQuitting;
        private readonly Dictionary<string,int> durableSequences=new Dictionary<string,int>();
        private readonly HashSet<string> changedAttempts=new HashSet<string>();
        private float saveAt;
        public event Action<AchievementUnlock> Unlocked;
        public event Action<string> Changed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetStatics(){instance=null;}
        private void Awake()
        {
            if(instance!=null&&instance!=this){Destroy(gameObject);return;}instance=this;DontDestroyOnLoad(gameObject);
            settings=settings!=null?settings:Resources.Load<FloodAchievementSettings>("FloodAchievements");
            if(settings==null||settings.database==null||!settings.enableAchievementSystem)return;
            Persistence=new AchievementPersistence(Path.Combine(Application.persistentDataPath,Path.GetFileName(settings.saveFileName)));
            try
            {
                var stored=Persistence.Load();bool needsHeader=string.IsNullOrEmpty(stored.profileId)||string.IsNullOrEmpty(stored.trackingStartedUtc);
                Engine=new AchievementEngine(settings.database.Load(),stored);
                foreach(var r in Engine.Data.receipts)durableSequences[r.attemptId]=r.sequence;
                Engine.Dirty+=OnDirty;Engine.Unlocked+=u=>{if(settings.debugLogging)Debug.Log("Achievement unlocked: "+u.definition.id+" / "+u.tier.id);Unlocked?.Invoke(u);};Engine.Changed+=id=>Changed?.Invoke(id);
                if(settings.enableRetroactiveInitialization&&Persistence.Writable)Engine.Import(ReadHistoricalRecords());
                Engine.RefreshDerived();
                if(needsHeader)OnDirty(true);
                Flush();
            }
            catch(Exception e){Debug.LogError("Achievement catalog could not be initialized: "+e.Message);Engine=null;}
        }
        [Serializable]private sealed class HistoricalSnapshot {public int width,height,depth;public string voxels;}
        [Serializable]private sealed class HistoricalDefinition
        {
            public int version,generatorVersion,goalGeneratorVersion,mode,difficulty,level,moveBudget;
            public string id;public HistoricalSnapshot snapshot;public Goals.SecondaryGoalDefinition[] goals;
        }
        private IEnumerable<AchievementLevelRecord> ReadHistoricalRecords()
        {
            int[] sizes={6,8,10,12,15,18,20};
            for(int m=0;m<3;m++)for(int d=0;d<4;d++)for(int l=1;l<=FloodLevelProgression3D.LevelsPerCategory;l++)
            {
                var old=FloodLevelProgression3D.Get(m,d,l);string path=FloodFillSessionPersistence3D.LevelPath(m,d,l);if(!old.completed&&!File.Exists(path))continue;
                bool validTime=old.hasCompletionTime&&!double.IsNaN(old.bestCompletionSeconds)&&!double.IsInfinity(old.bestCompletionSeconds)&&old.bestCompletionSeconds>=0;
                var record=new AchievementLevelRecord{key=AchievementEngine.NumberedKey(m,d,l),mode=m,difficulty=d,level=l,hadOneStar=old.stars==1,size=sizes[Math.Min(6,(l-1)/15)],completed=old.completed,stars=old.stars>=1&&old.stars<=3?old.stars:0,bestMoves=old.bestMoves>=0?old.bestMoves:int.MaxValue,hasTime=validTime,bestSeconds=validTime?old.bestCompletionSeconds:0,legacy=true};
                foreach(string id in old.earnedGoalIds??Array.Empty<string>())if(!string.IsNullOrEmpty(id)&&!record.earnedGoals.Contains(id))record.earnedGoals.Add(id);
                if(File.Exists(path))try
                {
                    var meta=JsonUtility.FromJson<HistoricalDefinition>(File.ReadAllText(path));
                    if(meta!=null&&meta.version==1&&meta.generatorVersion==1&&meta.goalGeneratorVersion==1&&meta.id==m+"_"+d+"_"+l&&meta.mode==m&&meta.difficulty==d&&meta.level==l&&meta.moveBudget>0&&meta.moveBudget<=1000000&&meta.snapshot!=null&&meta.snapshot.width==record.size&&meta.snapshot.height==record.size&&meta.snapshot.depth==record.size&&meta.goals!=null&&meta.goals.Length<=3)
                    {
                        var payload=Convert.FromBase64String(meta.snapshot.voxels);if(payload.Length>0&&payload.Length%4==0&&payload.Length/4<=record.size*record.size*record.size)
                        {
                            record.budget=meta.moveBudget;record.occupied=payload.Length/4;record.goalsKnown=true;
                            foreach(var goal in meta.goals)if(goal!=null&&!string.IsNullOrEmpty(goal.id)&&!record.assignedGoals.Contains(goal.id))record.assignedGoals.Add(goal.id);else record.goalsKnown=false;
                        }
                    }
                }
                catch(Exception e){if(settings.debugLogging)Debug.LogWarning("Achievement metadata skipped: "+e.Message);}
                yield return record;
            }
        }
        private void OnDirty(bool critical){if(!dirty)saveAt=Time.unscaledTime+(settings!=null?settings.saveDebounceDuration:.75f);dirty=true;important|=critical;}
        private void LateUpdate(){if(dirty&&(important&&settings.saveOnCompletion||Time.unscaledTime>=saveAt))Flush();}
        public int DurableSequence(string id)=>durableSequences.TryGetValue(id,out int sequence)?sequence:0;
        public void Flush()
        {
            if(Engine!=null&&dirty&&Persistence.Save(Engine.Data))
            {
                foreach(string id in changedAttempts)durableSequences[id]=Engine.Sequence(id);changedAttempts.Clear();dirty=important=false;
            }
        }
        private void OnApplicationPause(bool paused){applicationPaused=paused;if(paused&&settings!=null&&settings.saveOnPause)Flush();}
        private void OnApplicationFocus(bool focused){if(!focused&&settings!=null&&settings.saveOnPause)Flush();}
        private void OnApplicationQuit(){applicationQuitting=true;if(settings!=null&&settings.saveOnExit)Flush();}
        private void OnDestroy(){if(instance==this){Flush();instance=null;}}
        public static void RecoverCheckpoint(FloodFillSessionSaveData3D data)
        {
            try { RecoverCheckpointCore(data); }
            catch(Exception e){Debug.LogWarning("Achievement checkpoint recovery deferred: "+e.Message);}
        }
        private static void RecoverCheckpointCore(FloodFillSessionSaveData3D data)
        {
            if(!Application.isPlaying||data==null||data.achievementAttempt==null&&string.IsNullOrEmpty(data.abandonedAchievementAttemptId))return;
            var service=Instance;if(service==null||!service.Ready)return;
            if(!string.IsNullOrEmpty(data.abandonedAchievementAttemptId)){service.Engine.AbandonById(data.abandonedAchievementAttemptId,data.abandonedAchievementWasRestart);service.changedAttempts.Add(data.abandonedAchievementAttemptId);}
            var a=data.achievementAttempt;
            if(a==null){service.Flush();return;}
            a.moves=data.moveCount;a.score=data.score;a.seconds=data.elapsedSeconds;a.budget=data.currentMoveBudget;a.finalColor=data.board?.currentPlayerColor??-1;
            bool won=data.gameState==(int)FloodFillGameManager3D.GameState.Won,lost=data.gameState==(int)FloodFillGameManager3D.GameState.Lost;
            if(won)
            {
                a.stars=FloodLevelProgression3D.Stars(a.moves,a.budget);
                if(a.lastAction!=null)a.lastAction.finishing=true;
                // Derive one terminal goal mask from the same checkpoint, including recovered power-up wins.
                if(data.numberedLevel>0&&data.secondaryGoalAttempt!=null&&FloodFillSessionPersistence3D.TryLoadLevel(data.numberedMode,data.difficulty,data.numberedLevel,out var definition,out _))
                {
                    var attempt=JsonUtility.FromJson<Goals.SecondaryGoalAttempt>(JsonUtility.ToJson(data.secondaryGoalAttempt));
                    var tracker=new Goals.FloodSecondaryGoalTracker3D(definition.id,definition.goals,definition.initialCaptured,definition.palette.Length,attempt);
                    var earned=tracker.Complete(true,a.moves,a.finalColor,a.seconds);var ids=new List<string>();
                    for(int i=0;i<earned.Length;i++)if(earned[i])ids.Add(definition.goals[i].id);
                    a.goalsEarned=ids.Count;a.earnedGoalIds=ids.ToArray();
                }
            }
            service.Engine.Commit(a,won,lost);service.changedAttempts.Add(a.id);if(service.important&&service.settings.saveOnCompletion||service.settings.saveOnPause&&(service.applicationPaused||!Application.isFocused)||service.applicationQuitting&&service.settings.saveOnExit)service.Flush();
        }
        public void ReloadCatalog(IAchievementCatalogProvider provider=null){if(!Ready)return;Engine.Reload((provider??settings.database).Load());Engine.RefreshDerived();Changed?.Invoke(null);}
        public Sprite Icon(AchievementDefinition d)=>settings.database.Icon(d.iconKey)??settings.defaultIcon;
        public bool HasAttention()
        {
            if(!Ready)return false;
            foreach(var d in Engine.Catalog.achievements)if(d.enabled)foreach(var t in d.tiers)
            {
                var p=Engine.Progress(d,t);if(p.completed&&!p.seen)return true;
                if(settings.enableRewardUI&&p.completed)foreach(var r in t.rewards)if(r.Valid&&!p.claimedRewardIds.Contains(r.id))return true;
            }
            return false;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void MarkDevelopment(string reason)
        {
            var game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();game?.MarkAchievementAttemptIneligible(reason);
        }
#endif
    }
}
