using System;
using System.Collections.Generic;

namespace FloodFill.ThreeD.Achievements
{
    public enum AchievementMetric
    {
        CatalogLevels = 0,
        FullCategories = 1,
        TouchedCategories = 2,
        ModeMinimum = 3,
        PermanentStars = 4,
        StarLevels = 5,
        FirstStarSequence = 6,
        PermanentGoals = 7,
        AllGoalLevels = 8,
        FullGoalCategory = 9,
        WinCount = 10,
        ActionCount = 11,
        CapturedCubes = 12,
        CapturePeak = 13,
        MergePeak = 14,
        CapturePercentPeak = 15,
        CaptureChain = 16,
        SavedMoves = 17,
        FastestSeconds = 18,
        WinStreak = 19,
        ScoreTotal = 20,
        ScorePeak = 21,
        MultiplierCount = 22,
        MultiplierChain = 23,
        ColorCollection = 24,
        ColorMinimum = 25,
        ColorMaximum = 26,
        ColorRotation = 27,
        SizeCollection = 28,
        ActiveDays = 29,
        DailyStreak = 30,
        ActiveHours = 31,
        ReturnAfterDays = 32,
        AllNonHidden = 33,
        AchievementCount = 34,
        TierCount = 35,
    }
    public enum AchievementCalculation { Absolute, Percentage }
    public enum AchievementAggregation { Count, UniqueLevels, Maximum, Minimum }
    public enum AchievementEvent { Catalog, Win, Action, Outcome, Time, Day, Meta }
    [Serializable] public sealed class AchievementFilters
    {
        public int mode=-1, difficulty=-1, custom=-1, size, minimumOccupied, finalColor=-1;
        public bool allowReplays=true, allowPowerUps=true, requireWin, allGoals, finalMove, hintFollowed;
        public int stars, goals, powerKind=-1, powerKinds, minimumDirect, minimumMerged, minimumConnected;
        public double minimumCapturePercent, maxSeconds, maxMoveRatio;
        public int maxTargetColors, minimumFailures, minPowerUses;
        public bool powerFinish;
        public bool improvedMoves, improvedTime, improvedScore, improvedGoals, oneToThreeStars, noZeroMoves, normalFinish;
        public bool MatchesScope(AchievementAttempt a)
        {
            return a!=null&&a.eligible&&(mode<0||a.mode==mode)&&(difficulty<0||a.difficulty==difficulty)&&
                (custom<0||(a.custom?1:0)==custom)&&(size<=0||a.size==size)&&(minimumOccupied<=0||a.occupied>=minimumOccupied)&&
                (allowReplays||!a.wasCompleted)&&(allowPowerUps||!a.assisted);
        }
        public bool Matches(AchievementAttempt a)
        {
            if(!MatchesScope(a))return false;
            if(finalColor>=0&&a.finalColor!=finalColor||stars>0&&a.stars<stars||goals>0&&a.goalsEarned<goals)return false;
            if(allGoals&&(a.goalsAssigned<=0||a.goalsEarned<a.goalsAssigned))return false;
            if(finalMove&&a.moves!=a.budget||maxSeconds>0&&a.seconds>maxSeconds)return false;
            if(maxMoveRatio>0&&(a.budget<=0||a.moves>a.budget*maxMoveRatio+1e-7))return false;
            if(maxTargetColors>0&&(BitCount(a.targetMask)<1||BitCount(a.targetMask)>maxTargetColors))return false;
            if(minimumFailures>0&&a.previousFailures<minimumFailures)return false;
            if(noZeroMoves&&(!a.historyComplete||a.zeroMoves>0||a.moves<1)||normalFinish&&(a.lastAction==null||a.lastAction.kind!=-1))return false;
            if(improvedMoves&&!a.improvedMoves||improvedTime&&!a.improvedTime||improvedScore&&!a.improvedScore||improvedGoals&&!a.improvedGoals||oneToThreeStars&&!a.oneToThreeStars)return false;
            if(powerKinds>0&&BitCount(a.powerMask)<powerKinds)return false;
            if(minPowerUses>0){int uses=0;if(powerKind>=0)uses=a.powerUses[powerKind];else foreach(int n in a.powerUses)uses+=n;if(uses<minPowerUses)return false;}
            return true;
        }
        public bool MatchesAction(AchievementAttempt a,AchievementAction action,bool scopeOnly=false)
        {
            if(!(scopeOnly?MatchesScope(a):Matches(a))||action==null)return false;
            if(powerKind>=0&&action.kind!=powerKind||powerKind==-2&&action.kind<0)return false;
            if(hintFollowed&&!action.hintFollowed)return false;
            return action.direct>=minimumDirect&&action.merged>=minimumMerged&&action.connected>=minimumConnected&&action.capturePercent+1e-7>=minimumCapturePercent;
        }
        public static int BitCount(int v){int n=0;while(v!=0){n+=v&1;v=(int)((uint)v>>1);}return n;}
    }
    [Serializable] public sealed class AchievementReward
    {
        public string id,type,identifier,displayName,iconKey;
        public long quantity;
        public bool enabled=true;
        public bool Valid=>enabled&&!string.IsNullOrWhiteSpace(id)&&!string.IsNullOrWhiteSpace(type)&&!string.IsNullOrWhiteSpace(identifier)&&quantity>0;
    }
    [Serializable] public sealed class AchievementTier
    {
        public string id,description;
        public double target=1;
        public bool overrideFilters;
        public AchievementFilters filters=new AchievementFilters();
        public List<AchievementReward> rewards=new List<AchievementReward>();
    }
    [Serializable] public sealed class AchievementDefinition
    {
        public string id,name,description,category,iconKey,disabledReason;
        public int order,version=1;
        public bool enabled=true,hidden;
        public AchievementMetric metric;
        public AchievementAggregation aggregation;
        public AchievementCalculation calculation;
        public AchievementFilters filters=new AchievementFilters();
        public List<AchievementTier> tiers=new List<AchievementTier>();
        public AchievementFilters Filters(AchievementTier tier)=>tier.overrideFilters?tier.filters:filters;
    }
    [Serializable] public sealed class AchievementCatalogData
    {
        public int version=1,levelsPerCategory=100,modes=3,difficulties=4;
        public List<AchievementDefinition> achievements=new List<AchievementDefinition>();
    }
    [Serializable] public sealed class AchievementTierProgress
    {
        public string achievementId,tierId,completedUtc,lastAttemptId;
        public int definitionVersion;
        public double value,current;
        public bool completed,seen,hasValue;
        public List<string> uniqueKeys=new List<string>(),claimedRewardIds=new List<string>();
        public List<int> recentColors=new List<int>();
        public int[] colorCounts=new int[6];
    }
    [Serializable] public sealed class AchievementLevelRecord
    {
        public string key;
        public int mode,difficulty,level,size,occupied,budget,stars,bestMoves,failures;
        public bool completed,hasTime,hasScore,legacy,goalsKnown,custom,swapFinish,blastFinish,hadOneStar;
        public double bestSeconds;
        public int bestScore;
        public List<string> earnedGoals=new List<string>(),assignedGoals=new List<string>();
        // One bit per logical voxel, not an indefinitely growing capture event history.
        public string capturedBits;
    }
    [Serializable] public sealed class AchievementDeferred
    {
        public string tierId;public AchievementMetric metric;public double value;
    }
    [Serializable] public sealed class AchievementReceipt
    {
        public string attemptId,statisticKey;
        public int sequence;
        public bool terminal;
        public double creditedSeconds;
        public List<AchievementDeferred> pending=new List<AchievementDeferred>();
    }
    [Serializable] public sealed class AchievementStatistics
    {
        public string key;
        public int mode,difficulty,size;public bool custom;
        public long attempts,wins,failures,abandons,restarts,replayWins,normalMoves,capturedCubes,score,savedMoves,hintsFollowed,maxMultiplierUses,swapMerge3Uses;
        public int[] powerUses=new int[3];
        public long[] powerDirectCubes=new long[3],powerConnectedCubes=new long[3],powerMergedRegions=new long[3];
        public int peakRawCapture,peakRawMerge,peakComponentSize;
        public long zeroCaptureMoves;
        public int peakCapture,peakMerge,peakScore,peakCaptureChain,peakMultiplierChain;
        public double peakCapturePercent,activeSeconds;
    }
    [Serializable] public sealed class AchievementSaveData
    {
        public int version=1,importVersion;
        public string profileId,trackingStartedUtc,lastActivityUtc,lastCompletionDay;
        public double activeSeconds;
        public bool returnPending;
        public int dailyStreak,bestDailyStreak;
        public List<string> activeDays=new List<string>();
        public List<AchievementTierProgress> tiers=new List<AchievementTierProgress>();
        public List<AchievementLevelRecord> levels=new List<AchievementLevelRecord>();
        public List<AchievementReceipt> receipts=new List<AchievementReceipt>();
        public List<AchievementStatistics> statistics=new List<AchievementStatistics>();
    }
    [Serializable] public sealed class AchievementAction
    {
        public int sequence,kind=-1,direct,connected,merged,rawConnected,rawMerged,resultingComponent,revisitedCubes,multiplier,scoreDelta,scoreAfter,targetColor=-1;
        public double capturePercent;
        public bool hintFollowed,finishing;
        public int[] freshCubeIds=Array.Empty<int>();
    }
    [Serializable] public sealed class AchievementAttempt
    {
        public string id,key,puzzleHash;
        public bool eligible=true,custom,assisted,historyComplete=true,wasCompleted;
        public string ineligibleReason;
        public int mode,difficulty,level,size,occupied,budget,seed,moves,score,stars,finalColor=-1;
        public double seconds;
        public int goalsAssigned,goalsEarned,powerMask,targetMask,zeroMoves,captureChain,multiplierChain;
        public int peakCapture,peakMerge,previousFailures;
        public bool improvedMoves,improvedTime,improvedScore,improvedGoals,oneToThreeStars;
        public string capturedBits,connectionHistory;
        public string[] assignedGoalIds=Array.Empty<string>(),earnedGoalIds=Array.Empty<string>();
        public int[] powerUses=new int[3];
        public AchievementAction lastAction;
        public List<AchievementAction> pendingActions=new List<AchievementAction>();
        public int sequence,hintColor=-1;
        public long hintRevision=-1;
        public int[] hintPrimary=Array.Empty<int>();
        public bool hintPaid;
    }
    public interface IAchievementCatalogProvider { AchievementCatalogData Load(); }
    public interface IAchievementClock { DateTime UtcNow {get;} }
    public sealed class SystemAchievementClock:IAchievementClock { public DateTime UtcNow=>DateTime.UtcNow; }
    public interface IAchievementRewardService
    {
        bool CanGrant(AchievementReward reward);
        // The economy must durably deduplicate this token, including a retry after a crash.
        bool TryGrant(string idempotencyToken,IReadOnlyList<AchievementReward> rewards,out string error);
    }
    public readonly struct AchievementUnlock
    {
        public readonly AchievementDefinition definition;public readonly AchievementTier tier;
        public AchievementUnlock(AchievementDefinition d,AchievementTier t){definition=d;tier=t;}
    }
}
