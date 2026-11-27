using System;
using System.Collections.Generic;
using System.Globalization;

namespace FloodFill.ThreeD.Achievements
{
    /// <summary>Pure serializable reducer. No Unity objects, scene discovery, reflection or frame polling.</summary>
    public sealed class AchievementEngine
    {
        public AchievementCatalogData Catalog {get;private set;}
        public AchievementSaveData Data {get;}
        public event Action<AchievementUnlock> Unlocked;
        public event Action<string> Changed;
        public event Action<bool> Dirty;
        private readonly IAchievementClock clock;
        private readonly Dictionary<string,AchievementTierProgress> progress=new Dictionary<string,AchievementTierProgress>();
        private readonly Dictionary<string,AchievementLevelRecord> levels=new Dictionary<string,AchievementLevelRecord>();
        private readonly Dictionary<string,AchievementReceipt> receipts=new Dictionary<string,AchievementReceipt>();
        private readonly Dictionary<AchievementEvent,List<AchievementDefinition>> index=new Dictionary<AchievementEvent,List<AchievementDefinition>>();
        private bool importing,metaDirty=true;
        private readonly HashSet<string> activeDateSet=new HashSet<string>();
        private readonly Dictionary<string,AchievementStatistics> statistics=new Dictionary<string,AchievementStatistics>();
        public AchievementEngine(AchievementCatalogData catalog,AchievementSaveData data,IAchievementClock clock=null)
        {
            Data=data??new AchievementSaveData();this.clock=clock??new SystemAchievementClock();
            if(string.IsNullOrEmpty(Data.trackingStartedUtc))Data.trackingStartedUtc=Now;
            if(string.IsNullOrEmpty(Data.profileId))Data.profileId=Guid.NewGuid().ToString("N");
            foreach(var p in Data.tiers)if(p!=null&&!string.IsNullOrEmpty(p.tierId))progress[p.tierId]=p;
            foreach(var l in Data.levels)if(l!=null&&!string.IsNullOrEmpty(l.key))levels[l.key]=l;
            foreach(var r in Data.receipts)if(r!=null&&!string.IsNullOrEmpty(r.attemptId))receipts[r.attemptId]=r;
            foreach(var stat in Data.statistics)if(stat!=null&&!string.IsNullOrEmpty(stat.key))statistics[stat.key]=stat;
            foreach(string day in Data.activeDays)activeDateSet.Add(day);
            Reload(catalog);
            if(DateTime.TryParse(Data.lastActivityUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var last)&&this.clock.UtcNow>=last.AddDays(7))Data.returnPending=true;
        }
        private string Now=>clock.UtcNow.ToString("O",CultureInfo.InvariantCulture);
        public static bool IsMeta(AchievementMetric m)=>m==AchievementMetric.AllNonHidden||m==AchievementMetric.AchievementCount||m==AchievementMetric.TierCount;
        public void Reload(AchievementCatalogData catalog)
        {
            var issues=AchievementValidation.Validate(catalog);
            foreach(string issue in issues)if(issue.StartsWith("ERROR:",StringComparison.Ordinal))throw new ArgumentException(issue);
            Catalog=catalog;metaDirty=true;index.Clear();foreach(AchievementEvent e in Enum.GetValues(typeof(AchievementEvent)))index[e]=new List<AchievementDefinition>();
            foreach(var d in catalog.achievements)if(d.enabled)index[EventFor(d.metric)].Add(d);
        }
        private static AchievementEvent EventFor(AchievementMetric m)
        {
            if(m<=AchievementMetric.FullGoalCategory||m==AchievementMetric.SizeCollection)return AchievementEvent.Catalog;
            switch(m)
            {
                case AchievementMetric.ActionCount:case AchievementMetric.CapturedCubes:case AchievementMetric.CapturePeak:case AchievementMetric.MergePeak:case AchievementMetric.CapturePercentPeak:case AchievementMetric.CaptureChain:case AchievementMetric.MultiplierCount:case AchievementMetric.MultiplierChain:case AchievementMetric.ScoreTotal:case AchievementMetric.ScorePeak:return AchievementEvent.Action;
                case AchievementMetric.WinStreak:case AchievementMetric.ColorRotation:return AchievementEvent.Outcome;
                case AchievementMetric.ActiveHours:return AchievementEvent.Time;
                case AchievementMetric.ActiveDays:case AchievementMetric.DailyStreak:return AchievementEvent.Day;
                case AchievementMetric.AllNonHidden:case AchievementMetric.AchievementCount:case AchievementMetric.TierCount:return AchievementEvent.Meta;
                default:return AchievementEvent.Win;
            }
        }
        public AchievementTierProgress Progress(AchievementDefinition d,AchievementTier t)
        {
            if(progress.TryGetValue(t.id,out var p))return p;
            p=new AchievementTierProgress{achievementId=d.id,tierId=t.id};progress[t.id]=p;Data.tiers.Add(p);return p;
        }
        public AchievementLevelRecord Level(string key)
        {
            if(levels.TryGetValue(key,out var l))return l;
            l=new AchievementLevelRecord{key=key};levels[key]=l;Data.levels.Add(l);return l;
        }
        private AchievementReceipt Receipt(AchievementAttempt a)
        {
            if(receipts.TryGetValue(a.id,out var r))return r;
            r=new AchievementReceipt{attemptId=a.id,statisticKey=Bucket(a).key};receipts[a.id]=r;Data.receipts.Add(r);if(a.historyComplete)Bucket(a).attempts++;return r;
        }
        public void Import(IEnumerable<AchievementLevelRecord> records)
        {
            if(Data.importVersion>=2)return;
            importing=true;
            try
            {
                foreach(var record in records)
                {
                    if(record==null)continue;
                    if(!levels.TryGetValue(record.key,out var existing)){levels.Add(record.key,record);Data.levels.Add(record);}
                    else if(!existing.completed&&record.completed)
                    {
                        // Recover reliable numbered completion after the old mobile-solver exclusion.
                        // Preserve capture deduplication and failures; do not synthesize action totals.
                        record.capturedBits=existing.capturedBits;record.failures=existing.failures;
                        if(!record.goalsKnown&&existing.goalsKnown){record.goalsKnown=true;record.assignedGoals=new List<string>(existing.assignedGoals);}
                        levels[record.key]=record;Data.levels[Data.levels.IndexOf(existing)]=record;
                    }
                }
                // Historical first completion is reliable; successful-attempt counters remain unknown.
                bool any=false;foreach(var l in Data.levels)any|=l.completed;
                if(any)foreach(var d in index[AchievementEvent.Win])if(d.id=="ff3d.001")foreach(var t in d.tiers)Set(d,t,Progress(d,t),1,true);
                RefreshDerived();Data.importVersion=2;
            }
            finally{importing=false;}
            Dirty?.Invoke(true);
        }
        public void RefreshDerived()
        {
            Evaluate(AchievementEvent.Catalog,null,null,false,0);
            foreach(var d in Catalog.achievements)foreach(var t in d.tiers)
            {
                if(!d.enabled)continue;var p=Progress(d,t);if(p.completed)continue;var f=d.Filters(t);
                bool simple=f.allowReplays&&f.allowPowerUps&&f.stars==0&&f.goals==0&&!f.allGoals&&!f.finalMove&&!f.noZeroMoves&&!f.normalFinish&&f.finalColor<0&&f.maxTargetColors==0&&f.minimumFailures==0&&!f.improvedMoves&&!f.improvedTime&&!f.improvedScore&&!f.improvedGoals&&!f.oneToThreeStars&&f.powerKinds==0&&f.minPowerUses==0&&!f.powerFinish&&f.minimumDirect==0&&f.minimumMerged==0&&f.minimumConnected==0&&f.minimumCapturePercent==0&&f.powerKind<0;
                if(d.metric==AchievementMetric.FastestSeconds&&simple)
                {
                    bool found=p.hasValue;double best=found?p.value:double.MaxValue;
                    foreach(var r in Data.levels)if(r.completed&&r.hasTime)
                    {var a=new AchievementAttempt{eligible=true,mode=r.mode,difficulty=r.difficulty,custom=r.custom,size=r.size,occupied=r.occupied};if(f.MatchesScope(a)){best=Math.Min(best,r.bestSeconds);found=true;}}
                    if(found)Set(d,t,p,best,false);
                }
                if(d.metric==AchievementMetric.WinCount&&d.aggregation==AchievementAggregation.UniqueLevels&&f.powerFinish&&f.allowPowerUps)
                {
                    foreach(var r in Data.levels)if(r.completed&&(f.powerKind==0?r.swapFinish:f.powerKind==2&&r.blastFinish))
                    {var a=new AchievementAttempt{eligible=true,mode=r.mode,difficulty=r.difficulty,custom=r.custom,size=r.size,occupied=r.occupied};if(f.MatchesScope(a)&&!p.uniqueKeys.Contains(r.key))p.uniqueKeys.Add(r.key);}
                    Set(d,t,p,p.uniqueKeys.Count,false);
                }
                if(d.metric==AchievementMetric.WinCount&&d.aggregation==AchievementAggregation.UniqueLevels&&simple&&f.maxSeconds==0)
                {
                    foreach(var r in Data.levels)if(r.completed&&(f.maxMoveRatio>0?r.budget>0&&r.bestMoves<=r.budget*f.maxMoveRatio:true))
                    {var a=new AchievementAttempt{eligible=true,mode=r.mode,difficulty=r.difficulty,custom=r.custom,size=r.size,occupied=r.occupied};if(f.MatchesScope(a)&&!p.uniqueKeys.Contains(r.key))p.uniqueKeys.Add(r.key);}
                    Set(d,t,p,p.uniqueKeys.Count,false);
                }
                if(!simple||f.maxMoveRatio>0||f.maxSeconds>0||f.minimumOccupied>0||f.requireWin)continue;
                var stats=Statistics(f);double value=0;bool derived=true;
                switch(d.metric)
                {
                    case AchievementMetric.CapturedCubes:value=stats.capturedCubes;break;
                    case AchievementMetric.ScoreTotal:value=stats.score;break;
                    case AchievementMetric.ScorePeak:value=stats.peakScore;break;
                    case AchievementMetric.MultiplierCount:value=stats.maxMultiplierUses;break;
                    case AchievementMetric.ActiveHours:value=stats.activeSeconds/3600;break;
                    case AchievementMetric.ActiveDays:value=Data.activeDays.Count;break;
                    case AchievementMetric.DailyStreak:value=Data.bestDailyStreak;break;
                    default:derived=false;break;
                }
                if(derived)Set(d,t,p,value,false);
            }
            // Successful power and followed-hint totals remain available after all original tiers unlock.
            foreach(var d in index[AchievementEvent.Action])if(d.metric==AchievementMetric.ActionCount)foreach(var t in d.tiers)
            {
                var p=Progress(d,t);var f=d.Filters(t);
                if(!p.completed&&f.powerKind==0&&f.minimumMerged==3&&f.minimumDirect==0&&f.minimumConnected==0&&f.minimumCapturePercent==0&&!f.requireWin){Set(d,t,p,Statistics(f).swapMerge3Uses,false);continue;}
                if(p.completed||!f.allowPowerUps||f.minimumOccupied>0||f.minimumDirect>0||f.minimumMerged>0||f.minimumConnected>0||f.minimumCapturePercent>0||f.requireWin)continue;
                var s=Statistics(f);double value=f.hintFollowed?s.hintsFollowed:f.powerKind>=0?s.powerUses[f.powerKind]:f.powerKind==-2?(long)s.powerUses[0]+s.powerUses[1]+s.powerUses[2]:s.normalMoves+(long)s.powerUses[0]+s.powerUses[1]+s.powerUses[2];Set(d,t,p,value,false);
            }
            EvaluateMeta();
        }
        public void RegisterLevelMetadata(AchievementAttempt a,bool goalsKnown)
        {
            if(a==null||!a.eligible||a.level<=0)return;var r=Level(a.key);
            r.mode=a.mode;r.difficulty=a.difficulty;r.level=a.level;r.size=a.size;r.occupied=a.occupied;
            if(!r.completed||r.budget<=0)r.budget=a.budget;
            if(goalsKnown){r.goalsKnown=true;r.assignedGoals=new List<string>(a.assignedGoalIds);}
            Evaluate(AchievementEvent.Catalog,a,null,false,0);EvaluateMeta();Dirty?.Invoke(false);
        }
        public void ResumeBaseline(AchievementAttempt a,double seconds)
        {
            if(a==null||!a.eligible||string.IsNullOrEmpty(a.id))return;
            var r=Receipt(a);r.creditedSeconds=Math.Max(r.creditedSeconds,Math.Max(0,seconds));Dirty?.Invoke(false);
        }
        public void ObserveTime(AchievementAttempt a,double seconds)
        {
            if(a==null||!a.eligible||string.IsNullOrEmpty(a.id)||double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)return;
            var r=Receipt(a);if(r.terminal)return;
            double delta=Math.Max(0,seconds-r.creditedSeconds);if(delta<=0)return;
            r.creditedSeconds=Math.Max(r.creditedSeconds,seconds);Data.activeSeconds+=delta;Bucket(a).activeSeconds+=delta;
            Activity();Evaluate(AchievementEvent.Time,a,null,false,0);EvaluateMeta();Dirty?.Invoke(false);
        }
        public void Commit(AchievementAttempt a,bool won,bool lost)
        {
            if(a==null||!a.eligible||string.IsNullOrEmpty(a.id))return;
            var receipt=Receipt(a);if(receipt.terminal)return;
            ObserveTime(a,a.seconds);var record=Level(a.key);
            var action=a.lastAction;
            if(a.pendingActions!=null&&a.pendingActions.Count>0)
            {
                foreach(var pending in a.pendingActions)CommitAction(a,record,receipt,pending);
            }
            else if(action!=null)CommitAction(a,record,receipt,action);
            if(won||lost)
            {
                a.wasCompleted=record.completed;a.previousFailures=record.failures;
                a.improvedMoves=won&&record.completed&&a.moves<record.bestMoves;
                a.improvedTime=won&&record.hasTime&&a.seconds<record.bestSeconds;
                a.improvedScore=won&&record.hasScore&&a.score>record.bestScore;
                a.oneToThreeStars=won&&record.completed&&(record.hadOneStar||record.stars==1)&&record.stars<3&&a.stars==3;
                int freshGoals=0;foreach(string id in a.earnedGoalIds)if(!record.earnedGoals.Contains(id))freshGoals++;
                a.improvedGoals=won&&record.completed&&freshGoals>0;
                if(won)
                {
                    var stats=Bucket(a);stats.wins++;if(record.completed)stats.replayWins++;stats.savedMoves+=Math.Max(0,a.budget-a.moves);
                    Evaluate(AchievementEvent.Win,a,action,true,0);CompletionDay();
                    UpdateRecord(record,a);Evaluate(AchievementEvent.Catalog,a,action,true,0);
                }
                else {record.failures++;Bucket(a).failures++;receipt.pending.Clear();}
                Evaluate(AchievementEvent.Outcome,a,action,won,0);receipt.terminal=true;
                EvaluateMeta();Dirty?.Invoke(true);
            }
            else Dirty?.Invoke(false);
        }
        private void CommitAction(AchievementAttempt a,AchievementLevelRecord record,AchievementReceipt receipt,AchievementAction action)
        {
            if(action==null||action.sequence<=receipt.sequence)return;
            int unique=CreditCubes(record,action.freshCubeIds);receipt.sequence=action.sequence;
            var stats=Bucket(a);stats.capturedCubes+=unique;stats.score+=Math.Max(0,action.scoreDelta);stats.peakScore=Math.Max(stats.peakScore,action.scoreAfter>0?action.scoreAfter:a.score);
            if(action.kind>=0&&action.kind<3){stats.powerUses[action.kind]++;stats.powerDirectCubes[action.kind]+=action.direct;stats.powerConnectedCubes[action.kind]+=action.connected;stats.powerMergedRegions[action.kind]+=action.merged;}
            stats.peakRawCapture=Math.Max(stats.peakRawCapture,action.rawConnected);stats.peakRawMerge=Math.Max(stats.peakRawMerge,action.rawMerged);stats.peakComponentSize=Math.Max(stats.peakComponentSize,action.resultingComponent);if(action.kind==-1&&action.connected==0)stats.zeroCaptureMoves++;
            if(action.kind==0&&action.merged>=3)stats.swapMerge3Uses++;
            if(action.kind==-1){stats.normalMoves++;if(action.hintFollowed)stats.hintsFollowed++;if(action.multiplier==7)stats.maxMultiplierUses++;stats.peakCapture=Math.Max(stats.peakCapture,action.connected);stats.peakMerge=Math.Max(stats.peakMerge,action.merged);stats.peakCapturePercent=Math.Max(stats.peakCapturePercent,action.capturePercent);stats.peakCaptureChain=Math.Max(stats.peakCaptureChain,a.captureChain);stats.peakMultiplierChain=Math.Max(stats.peakMultiplierChain,a.multiplierChain);}
            Activity();Evaluate(AchievementEvent.Action,a,action,false,unique);EvaluateMeta();
        }
        public int Sequence(string attemptId)=>receipts.TryGetValue(attemptId,out var r)?r.sequence:0;
        public void Abandon(AchievementAttempt a,bool restart=false)
        {
            if(a==null||!a.eligible||string.IsNullOrEmpty(a.id))return;Receipt(a);AbandonById(a.id,restart);
        }
        public void AbandonById(string attemptId,bool restart=false)
        {
            if(string.IsNullOrEmpty(attemptId))return;
            if(!receipts.TryGetValue(attemptId,out var r)){r=new AchievementReceipt{attemptId=attemptId};receipts[attemptId]=r;Data.receipts.Add(r);}
            if(r.terminal)return;
            Evaluate(AchievementEvent.Outcome,null,null,false,0);r.pending.Clear();r.terminal=true;
            if(!string.IsNullOrEmpty(r.statisticKey)&&statistics.TryGetValue(r.statisticKey,out var stats)){stats.abandons++;if(restart)stats.restarts++;}
            Dirty?.Invoke(true);
        }
        private AchievementStatistics Bucket(AchievementAttempt a)
        {
            string key=a.mode+":"+a.difficulty+":"+(a.custom?1:0)+":"+a.size;
            if(statistics.TryGetValue(key,out var result))return result;
            result=new AchievementStatistics{key=key,mode=a.mode,difficulty=a.difficulty,custom=a.custom,size=a.size};statistics[key]=result;Data.statistics.Add(result);return result;
        }
        public AchievementStatistics Statistics(AchievementFilters f=null)
        {
            var result=new AchievementStatistics();
            foreach(var s in Data.statistics)
            {
                if(f!=null&&(f.mode>=0&&f.mode!=s.mode||f.difficulty>=0&&f.difficulty!=s.difficulty||f.custom>=0&&f.custom!=(s.custom?1:0)||f.size>0&&f.size!=s.size))continue;
                result.attempts+=s.attempts;result.wins+=s.wins;result.failures+=s.failures;result.abandons+=s.abandons;result.restarts+=s.restarts;result.replayWins+=s.replayWins;result.normalMoves+=s.normalMoves;result.capturedCubes+=s.capturedCubes;result.score+=s.score;result.savedMoves+=s.savedMoves;result.hintsFollowed+=s.hintsFollowed;result.swapMerge3Uses+=s.swapMerge3Uses;result.maxMultiplierUses+=s.maxMultiplierUses;result.activeSeconds+=s.activeSeconds;
                for(int k=0;k<3;k++){result.powerUses[k]+=s.powerUses[k];result.powerDirectCubes[k]+=s.powerDirectCubes[k];result.powerConnectedCubes[k]+=s.powerConnectedCubes[k];result.powerMergedRegions[k]+=s.powerMergedRegions[k];}
                result.peakRawCapture=Math.Max(result.peakRawCapture,s.peakRawCapture);result.peakRawMerge=Math.Max(result.peakRawMerge,s.peakRawMerge);result.peakComponentSize=Math.Max(result.peakComponentSize,s.peakComponentSize);result.zeroCaptureMoves+=s.zeroCaptureMoves;
                result.peakCapture=Math.Max(result.peakCapture,s.peakCapture);result.peakMerge=Math.Max(result.peakMerge,s.peakMerge);result.peakScore=Math.Max(result.peakScore,s.peakScore);result.peakCaptureChain=Math.Max(result.peakCaptureChain,s.peakCaptureChain);result.peakMultiplierChain=Math.Max(result.peakMultiplierChain,s.peakMultiplierChain);result.peakCapturePercent=Math.Max(result.peakCapturePercent,s.peakCapturePercent);
            }
            return result;
        }
        private void UpdateRecord(AchievementLevelRecord r,AchievementAttempt a)
        {
            if(!a.historyComplete)r.legacy=true;
            r.custom=a.custom;r.mode=a.mode;r.difficulty=a.difficulty;r.level=a.level;r.size=a.size;r.occupied=a.occupied;r.budget=a.budget;
            if(r.stars==1||r.stars<1&&a.stars==1)r.hadOneStar=true;
            r.bestMoves=r.completed?Math.Min(r.bestMoves,a.moves):a.moves;r.completed=true;r.stars=Math.Max(r.stars,a.stars);
            r.bestSeconds=r.hasTime?Math.Min(r.bestSeconds,a.seconds):a.seconds;r.hasTime=true;
            r.bestScore=r.hasScore?Math.Max(r.bestScore,a.score):a.score;r.hasScore=true;
            if(a.lastAction!=null&&a.lastAction.finishing){r.swapFinish|=a.lastAction.kind==0;r.blastFinish|=a.lastAction.kind==2;}
            r.assignedGoals=new List<string>(a.assignedGoalIds);r.goalsKnown=true;
            foreach(string id in a.earnedGoalIds)if(!r.earnedGoals.Contains(id))r.earnedGoals.Add(id);
        }
        private int CreditCubes(AchievementLevelRecord r,int[] ids)
        {
            if(ids==null||ids.Length==0)return 0;
            byte[] bits=DecodeBits(r.capturedBits);int count=0;
            foreach(int id in ids)if(id>=0&&id<262144)
            {
                int slot=id>>3;if(slot>=bits.Length)Array.Resize(ref bits,slot+1);
                byte mask=(byte)(1<<(id&7));if((bits[slot]&mask)==0){bits[slot]|=mask;count++;}
            }
            r.capturedBits=Convert.ToBase64String(bits);return count;
        }
        public static byte[] DecodeBits(string s){try{return string.IsNullOrEmpty(s)?Array.Empty<byte>():Convert.FromBase64String(s);}catch(FormatException){return Array.Empty<byte>();}}
        private void Evaluate(AchievementEvent e,AchievementAttempt a,AchievementAction action,bool won,int unique)
        {
            foreach(var d in index[e])foreach(var t in d.tiers)
            {
                var p=Progress(d,t);if(p.completed)continue;var f=d.Filters(t);
                if(e==AchievementEvent.Catalog){Set(d,t,p,CatalogProgress(d,f),false);continue;}
                if(e==AchievementEvent.Time){Set(d,t,p,Statistics(f).activeSeconds/3600,false);continue;}
                if(e==AchievementEvent.Day){Set(d,t,p,d.metric==AchievementMetric.ActiveDays?Data.activeDays.Count:Data.bestDailyStreak,false);continue;}
                bool match=f.Matches(a);
                if(e==AchievementEvent.Outcome)
                {
                    if(!won||!match){p.current=0;p.recentColors.Clear();continue;}
                    if(d.metric==AchievementMetric.ColorRotation)
                    {
                        int old=p.recentColors.IndexOf(a.finalColor);if(old>=0)p.recentColors.RemoveRange(0,old+1);
                        p.recentColors.Add(a.finalColor);p.current=p.recentColors.Count;
                    }
                    else p.current++;
                    Set(d,t,p,Math.Max(p.value,p.current),false);continue;
                }
                if(e==AchievementEvent.Action)
                {
                    if(d.metric==AchievementMetric.CaptureChain||d.metric==AchievementMetric.MultiplierChain)
                    {
                        if(action.kind==1)continue; // Revealing advice is not a move.
                        if(p.lastAttemptId!=a.id){p.lastAttemptId=a.id;p.current=0;}
                        bool qualifying=action.kind==-1&&f.MatchesAction(a,action,f.requireWin)&&
                            (d.metric==AchievementMetric.CaptureChain?action.connected>=Math.Max(1,f.minimumConnected):action.multiplier==7);
                        p.current=qualifying?p.current+1:0;
                        if(f.requireWin){Defer(d,t,p,a,action,unique);}
                        else Set(d,t,p,Math.Max(p.value,p.current),false);
                        continue;
                    }
                    if(!f.MatchesAction(a,action,f.requireWin))continue;
                    if(f.requireWin){Defer(d,t,p,a,action,unique);continue;}
                    ApplyAction(d,t,p,a,action,unique);continue;
                }
                if(!match)continue;
                switch(d.metric)
                {
                    case AchievementMetric.WinCount:
                        if(f.minimumDirect>0||f.minimumMerged>0||f.minimumConnected>0||f.minimumCapturePercent>0||f.powerFinish)
                            if(action==null||!f.MatchesAction(a,action)||f.powerFinish&&!action.finishing)continue;
                        if(d.aggregation==AchievementAggregation.UniqueLevels){if(!p.uniqueKeys.Contains(a.key)){p.uniqueKeys.Add(a.key);Set(d,t,p,p.uniqueKeys.Count,false);}}
                        else Set(d,t,p,p.value+1,false);break;
                    case AchievementMetric.SavedMoves:Set(d,t,p,p.value+Math.Max(0,a.budget-a.moves),false);break;
                    case AchievementMetric.FastestSeconds:Set(d,t,p,p.hasValue?Math.Min(p.value,a.seconds):a.seconds,false);break;
                    case AchievementMetric.ScoreTotal:Set(d,t,p,p.value+a.score,false);break;
                    case AchievementMetric.ScorePeak:Set(d,t,p,Math.Max(p.value,a.score),false);break;
                    case AchievementMetric.ColorCollection:case AchievementMetric.ColorMinimum:case AchievementMetric.ColorMaximum:
                        if(a.finalColor<0||a.finalColor>5)break;
                        if(d.aggregation==AchievementAggregation.UniqueLevels){string colorKey=a.key+"#"+a.finalColor;if(p.uniqueKeys.Contains(colorKey))break;p.uniqueKeys.Add(colorKey);}
                        p.colorCounts[a.finalColor]++;
                        int min=int.MaxValue,max=0,distinct=0;foreach(int n in p.colorCounts){min=Math.Min(min,n);max=Math.Max(max,n);if(n>0)distinct++;}
                        Set(d,t,p,d.metric==AchievementMetric.ColorCollection?distinct:d.metric==AchievementMetric.ColorMinimum?min:max,false);break;
                    case AchievementMetric.ReturnAfterDays:if(Data.returnPending){Set(d,t,p,7,false);}break;
                }
            }
            if(e==AchievementEvent.Win)
            {
                FlushDeferred(a,won);Data.returnPending=false;
            }
        }
        private void ApplyAction(AchievementDefinition d,AchievementTier t,AchievementTierProgress p,AchievementAttempt a,AchievementAction x,int unique)
        {
            double value=p.value;var f=d.Filters(t);
            bool simple=f.allowPowerUps&&f.allowReplays&&f.minimumOccupied==0&&f.minimumDirect==0&&f.minimumMerged==0&&f.minimumConnected==0&&f.minimumCapturePercent==0&&!f.requireWin;
            if(simple)
            {
                var totals=Statistics(f);
                switch(d.metric)
                {
                    case AchievementMetric.ActionCount:
                        if(f.hintFollowed)value=totals.hintsFollowed;
                        else if(f.powerKind>=0)value=totals.powerUses[f.powerKind];
                        else if(f.powerKind==-2)value=(long)totals.powerUses[0]+totals.powerUses[1]+totals.powerUses[2];
                        else value=totals.normalMoves+(long)totals.powerUses[0]+totals.powerUses[1]+totals.powerUses[2];
                        Set(d,t,p,value,false);return;
                    case AchievementMetric.CapturedCubes:Set(d,t,p,totals.capturedCubes,false);return;
                    case AchievementMetric.ScoreTotal:Set(d,t,p,totals.score,false);return;
                    case AchievementMetric.ScorePeak:Set(d,t,p,totals.peakScore,false);return;
                    case AchievementMetric.MultiplierCount:Set(d,t,p,totals.maxMultiplierUses,false);return;
                }
            }
            switch(d.metric)
            {
                case AchievementMetric.ActionCount:value++;break;
                case AchievementMetric.ScoreTotal:value+=Math.Max(0,x.scoreDelta);break;
                case AchievementMetric.ScorePeak:value=Math.Max(value,a.score);break;
                case AchievementMetric.CapturedCubes:value+=unique;break;
                case AchievementMetric.CapturePeak:if(x.kind==-1)value=Math.Max(value,x.connected);break;
                case AchievementMetric.MergePeak:if(x.kind==-1)value=Math.Max(value,x.merged);break;
                case AchievementMetric.CapturePercentPeak:if(x.kind==-1)value=Math.Max(value,x.capturePercent);break;
                case AchievementMetric.CaptureChain:if(x.kind==-1)value=Math.Max(value,a.captureChain);break;
                case AchievementMetric.MultiplierCount:if(x.kind==-1&&x.multiplier==7)value++;break;
                case AchievementMetric.MultiplierChain:if(x.kind==-1)value=Math.Max(value,a.multiplierChain);break;
            }
            Set(d,t,p,value,false);
        }
        private void Defer(AchievementDefinition d,AchievementTier t,AchievementTierProgress p,AchievementAttempt a,AchievementAction x,int unique)
        {
            if(x.kind!=-1&&(d.metric==AchievementMetric.CapturePeak||d.metric==AchievementMetric.MergePeak||d.metric==AchievementMetric.CapturePercentPeak||d.metric==AchievementMetric.MultiplierCount))return;
            var r=Receipt(a);var pending=r.pending.Find(v=>v.tierId==t.id);
            if(pending==null){pending=new AchievementDeferred{tierId=t.id,metric=d.metric};r.pending.Add(pending);}
            switch(d.metric)
            {
                case AchievementMetric.ActionCount:pending.value++;break;
                case AchievementMetric.CapturedCubes:pending.value+=unique;break;
                case AchievementMetric.ScoreTotal:pending.value+=Math.Max(0,x.scoreDelta);break;
                case AchievementMetric.ScorePeak:pending.value=Math.Max(pending.value,a.score);break;
                case AchievementMetric.MultiplierCount:if(x.kind==-1&&x.multiplier==7)pending.value++;break;
                default:pending.value=Math.Max(pending.value,d.metric==AchievementMetric.CapturePeak?x.connected:d.metric==AchievementMetric.MergePeak?x.merged:d.metric==AchievementMetric.CapturePercentPeak?x.capturePercent:p.current);break;
            }
        }
        private void FlushDeferred(AchievementAttempt a,bool won)
        {
            var r=Receipt(a);
            if(won)foreach(var v in r.pending)foreach(var d in index[AchievementEvent.Action])foreach(var t in d.tiers)
                if(t.id==v.tierId&&d.Filters(t).Matches(a)){var p=Progress(d,t);bool sum=v.metric==AchievementMetric.ActionCount||v.metric==AchievementMetric.CapturedCubes||v.metric==AchievementMetric.MultiplierCount||v.metric==AchievementMetric.ScoreTotal;Set(d,t,p,sum?p.value+v.value:Math.Max(p.value,v.value),false);}
            r.pending.Clear();
        }
        private double CatalogProgress(AchievementDefinition d,AchievementFilters f)
        {
            double count=CatalogValue(d.metric,f);if(d.calculation!=AchievementCalculation.Percentage)return count;
            int denominator=0;
            if(d.metric==AchievementMetric.FullCategories||d.metric==AchievementMetric.TouchedCategories)denominator=(f.mode<0?Catalog.modes:1)*(f.difficulty<0?Catalog.difficulties:1);
            else
            {
                int[] sizes={6,8,10,12,15,18,20};for(int level=1;level<=Catalog.levelsPerCategory;level++)if(f.size==0||f.size==sizes[Math.Min(sizes.Length-1,(level-1)/15)])denominator++;
                denominator*= (f.mode<0?Catalog.modes:1)*(f.difficulty<0?Catalog.difficulties:1);
            }
            return denominator>0?count*100/denominator:0;
        }
        private double CatalogValue(AchievementMetric metric,AchievementFilters filters)
        {
            var counts=new int[Catalog.modes*Catalog.difficulties];int count=0,stars=0,goals=0;var sizes=new HashSet<int>();
            foreach(var r in Data.levels)
            {
                if(!r.completed)continue;
                var a=new AchievementAttempt{eligible=true,mode=r.mode,difficulty=r.difficulty,level=r.level,custom=r.custom,size=r.size,occupied=r.occupied,budget=r.budget,moves=r.bestMoves,stars=r.stars,seconds=r.hasTime?r.bestSeconds:double.MaxValue};
                if(!filters.Matches(a)||!filters.allowPowerUps&&r.legacy)continue;
                if(metric==AchievementMetric.AllGoalLevels&&!AllGoals(r))continue;
                if(metric==AchievementMetric.StarLevels&&r.stars<filters.stars)continue;
                if(metric==AchievementMetric.SizeCollection){if(Array.IndexOf(new[]{6,8,10,12,15,18,20},r.size)>=0)sizes.Add(r.size);continue;}
                // Numbered catalog metrics never include custom games.
                if(r.level<=0)continue;
                count++;stars+=r.stars;goals+=r.earnedGoals.Count;
                if(r.mode>=0&&r.mode<Catalog.modes&&r.difficulty>=0&&r.difficulty<Catalog.difficulties)counts[r.mode*Catalog.difficulties+r.difficulty]++;
            }
            switch(metric)
            {
                case AchievementMetric.FullCategories:int full=0;foreach(int n in counts)if(n>=Catalog.levelsPerCategory)full++;return full;
                case AchievementMetric.TouchedCategories:int touched=0;foreach(int n in counts)if(n>0)touched++;return touched;
                case AchievementMetric.ModeMinimum:int min=int.MaxValue;for(int m=0;m<Catalog.modes;m++){int n=0;for(int k=0;k<Catalog.difficulties;k++)n+=counts[m*Catalog.difficulties+k];min=Math.Min(min,n);}return min;
                case AchievementMetric.PermanentStars:return stars;
                case AchievementMetric.PermanentGoals:return goals;
                case AchievementMetric.FirstStarSequence:
                    int best=0;for(int m=0;m<Catalog.modes;m++)for(int k=0;k<Catalog.difficulties;k++){if(filters.mode>=0&&filters.mode!=m||filters.difficulty>=0&&filters.difficulty!=k)continue;int seq=0;for(int l=1;l<=Catalog.levelsPerCategory;l++){if(!levels.TryGetValue(NumberedKey(m,k,l),out var r)||!r.completed||r.stars<3)break;seq++;}best=Math.Max(best,seq);}return best;
                case AchievementMetric.FullGoalCategory:
                    for(int m=0;m<Catalog.modes;m++)for(int k=0;k<Catalog.difficulties;k++){if(filters.mode>=0&&filters.mode!=m||filters.difficulty>=0&&filters.difficulty!=k)continue;bool all=true,any=false;for(int l=1;l<=Catalog.levelsPerCategory;l++){if(!levels.TryGetValue(NumberedKey(m,k,l),out var r)||!r.goalsKnown){all=false;break;}if(r.assignedGoals.Count>0){any=true;if(!AllGoals(r)){all=false;break;}}}if(all&&any)return 1;}return 0;
                case AchievementMetric.SizeCollection:return sizes.Count;
                default:return count;
            }
        }
        private static bool AllGoals(AchievementLevelRecord r){if(!r.goalsKnown||r.assignedGoals.Count==0)return false;foreach(string id in r.assignedGoals)if(!r.earnedGoals.Contains(id))return false;return true;}
        public static string NumberedKey(int mode,int difficulty,int level)=>mode+":"+difficulty+":"+level;
        private void Set(AchievementDefinition d,AchievementTier t,AchievementTierProgress p,double value,bool historical)
        {
            if(double.IsNaN(value)||double.IsInfinity(value))return;
            bool changed=!p.hasValue||Math.Abs(p.value-value)>1e-8;p.value=value;p.hasValue=true;
            bool complete=d.metric==AchievementMetric.FastestSeconds?value<=t.target:value+1e-7>=t.target;
            if(complete&&!p.completed)
            {
                p.completed=true;p.completedUtc=Now;p.definitionVersion=d.version;if(!IsMeta(d.metric))metaDirty=true;p.seen=importing||historical;changed=true;
                if(!p.seen)Unlocked?.Invoke(new AchievementUnlock(d,t));Dirty?.Invoke(true);
            }
            if(changed)Changed?.Invoke(d.id);
        }
        private void EvaluateMeta()
        {
            if(!metaDirty)return;metaDirty=false;
            int full=0,tiers=0,nonHiddenFull=0,nonHiddenTotal=0;
            foreach(var d in Catalog.achievements)
            {
                if(!d.enabled||IsMeta(d.metric))continue;bool done=true;
                foreach(var t in d.tiers){bool c=Progress(d,t).completed;if(c)tiers++;else done=false;}if(done)full++;
                if(!d.hidden){nonHiddenTotal++;if(done)nonHiddenFull++;}
            }
            foreach(var d in index[AchievementEvent.Meta])foreach(var t in d.tiers)
                Set(d,t,Progress(d,t),d.metric==AchievementMetric.AllNonHidden?(nonHiddenTotal>0&&nonHiddenFull==nonHiddenTotal?1:0):d.metric==AchievementMetric.AchievementCount?full:tiers,false);
        }
        private void Activity()
        {
            var now=clock.UtcNow;string day=now.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
            if(DateTime.TryParse(Data.lastActivityUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var last)&&now<last)return;
            if(last!=default&&now>=last.AddDays(7))Data.returnPending=true;
            Data.lastActivityUtc=Now;if(activeDateSet.Add(day)){Data.activeDays.Add(day);Evaluate(AchievementEvent.Day,null,null,false,0);}
        }
        private void CompletionDay()
        {
            string day=clock.UtcNow.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
            if(day==Data.lastCompletionDay)return;
            if(DateTime.TryParseExact(Data.lastCompletionDay,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var previous))
            {double delta=(clock.UtcNow.Date-previous.Date).TotalDays;if(delta<0)return;Data.dailyStreak=delta==1?Data.dailyStreak+1:1;}
            else Data.dailyStreak=1;
            Data.lastCompletionDay=day;Data.bestDailyStreak=Math.Max(Data.bestDailyStreak,Data.dailyStreak);Evaluate(AchievementEvent.Day,null,null,false,0);
        }
        public void MarkTierSeen(AchievementDefinition d,AchievementTier t){var p=Progress(d,t);if(p.completed)p.seen=true;Changed?.Invoke(d.id);Dirty?.Invoke(false);}
        public void MarkSeen(AchievementDefinition d){foreach(var t in d.tiers){var p=Progress(d,t);if(p.completed)p.seen=true;}Changed?.Invoke(d.id);Dirty?.Invoke(false);}
        public bool TryClaim(AchievementDefinition d,AchievementTier t,IAchievementRewardService service,out string error)
        {
            error=null;var p=Progress(d,t);if(!p.completed){error="Tier is locked.";return false;}
            var rewards=new List<AchievementReward>();foreach(var r in t.rewards)if(r.Valid&&!p.claimedRewardIds.Contains(r.id))rewards.Add(r);
            if(rewards.Count==0){error="No unclaimed reward is configured.";return false;}
            if(service==null){error="Reward distribution is not available yet.";return false;}
            foreach(var r in rewards)if(!service.CanGrant(r)){error="No handler for "+r.type;return false;}
            // Persisted stable entry IDs allow newly configured rewards on a completed tier later.
            string token=Data.profileId+"/"+d.id+"/"+t.id;foreach(var r in rewards)token+="/"+r.id;
            try{if(!service.TryGrant(token,rewards,out error))return false;}catch(Exception e){error=e.Message;return false;}
            foreach(var r in rewards)p.claimedRewardIds.Add(r.id);Dirty?.Invoke(true);Changed?.Invoke(d.id);return true;
        }
    }
}
