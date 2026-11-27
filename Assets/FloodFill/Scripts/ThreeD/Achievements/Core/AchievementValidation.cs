using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace FloodFill.ThreeD.Achievements
{
    public static class AchievementValidation
    {
        public static List<string> Validate(AchievementCatalogData catalog)
        {
            var issues=new List<string>();var ids=new HashSet<string>();var tiers=new HashSet<string>();
            if(catalog==null||catalog.version<1||catalog.achievements==null){issues.Add("ERROR: Missing or unsupported catalog.");return issues;}
            if(catalog.levelsPerCategory<1||catalog.modes<1||catalog.modes>3||catalog.difficulties!=4)issues.Add("ERROR: Invalid catalog dimensions.");
            foreach(var d in catalog.achievements)
            {
                if(d==null){issues.Add("ERROR: Null definition.");continue;}
                string at=d.name+" ("+d.id+"): ";
                if(!ValidId(d.id)||!ids.Add(d.id))issues.Add("ERROR: "+at+"duplicate/missing achievement ID.");
                if(string.IsNullOrWhiteSpace(d.name)||string.IsNullOrWhiteSpace(d.description)||string.IsNullOrWhiteSpace(d.category))issues.Add("ERROR: "+at+"missing name, description or category.");
                if(d.version<1||!Enum.IsDefined(typeof(AchievementMetric),d.metric)||!Enum.IsDefined(typeof(AchievementAggregation),d.aggregation)||!Enum.IsDefined(typeof(AchievementCalculation),d.calculation))issues.Add("ERROR: "+at+"unsupported definition/condition.");
                if(d.tiers==null||d.tiers.Count<1||d.tiers.Count>3){issues.Add("ERROR: "+at+"tier count must be 1–3.");continue;}
                double previous=d.metric==AchievementMetric.FastestSeconds?double.MaxValue:0;
                foreach(var t in d.tiers)
                {
                    if(t==null){issues.Add("ERROR: "+at+"null tier.");continue;}
                    if(!ValidId(t.id)||!tiers.Add(t.id))issues.Add("ERROR: "+at+"duplicate/missing tier ID.");
                    if(double.IsNaN(t.target)||double.IsInfinity(t.target)||t.target<=0)issues.Add("ERROR: "+at+"invalid positive threshold.");
                    if(d.calculation==AchievementCalculation.Percentage&&(t.target>100||UnsupportedPercentageMetric(d.metric)))issues.Add("ERROR: "+at+"percentage progress supports numbered levels/categories only and targets up to 100%.");
                    bool ordered=d.metric==AchievementMetric.FastestSeconds?t.target<previous:t.target>previous;
                    if(!t.overrideFilters&&!ordered)issues.Add("ERROR: "+at+"thresholds must increase (decrease for fastest time).");
                    previous=t.target;var f=d.Filters(t);
                    if(f==null){issues.Add("ERROR: "+at+"missing filters.");continue;}
                    if(d.calculation==AchievementCalculation.Percentage&&(f.custom==1||f.minimumOccupied>0))issues.Add("ERROR: "+at+"percentage denominator is unavailable for custom/occupied-cube filters.");
                    if(f.mode< -1||f.mode>2||f.difficulty< -1||f.difficulty>3||f.custom< -1||f.custom>1||f.powerKind< -2||f.powerKind>2||f.finalColor< -1||f.finalColor>5)issues.Add("ERROR: "+at+"unsupported mode, difficulty, power, color or game kind.");
                    if(f.size!=0&&Array.IndexOf(new[]{6,8,10,12,15,18,20},f.size)<0)issues.Add("ERROR: "+at+"unsupported player board size.");
                    if(f.maxMoveRatio<0||f.maxMoveRatio>1||f.minimumCapturePercent<0||f.minimumCapturePercent>100||f.maxSeconds<0||f.stars<0||f.stars>3||f.goals<0||f.goals>3||f.minimumDirect<0||f.minimumMerged<0||f.minimumConnected<0||f.minPowerUses<0||f.maxTargetColors<0||f.maxTargetColors>6)issues.Add("ERROR: "+at+"invalid gameplay filter bounds.");
                    if((d.metric==AchievementMetric.WinCount||d.metric==AchievementMetric.ColorMinimum||d.metric==AchievementMetric.ColorMaximum)&&d.aggregation!=AchievementAggregation.Count&&d.aggregation!=AchievementAggregation.UniqueLevels)issues.Add("ERROR: "+at+"this condition supports all wins or unique-level counting only.");
                    bool scoped=f.mode>=0||f.difficulty>=0||f.custom>=0||f.size>0||f.minimumOccupied>0||!f.allowPowerUps||!f.allowReplays;
                    if((d.metric==AchievementMetric.ActiveDays||d.metric==AchievementMetric.DailyStreak||AchievementEngine.IsMeta(d.metric))&&scoped)issues.Add("ERROR: "+at+"calendar/meta conditions do not support gameplay scope filters.");
                    if(d.metric==AchievementMetric.ActiveHours&&(f.minimumOccupied>0||!f.allowPowerUps||!f.allowReplays))issues.Add("ERROR: "+at+"active time supports mode, difficulty, game kind and size scope only.");
                    bool catalogMetric=d.metric<=AchievementMetric.FullGoalCategory||d.metric==AchievementMetric.SizeCollection;
                    if(catalogMetric&&(!f.allowPowerUps||!f.allowReplays))issues.Add("ERROR: "+at+"use a qualifying WinCount condition for assistance/replay restrictions; permanent catalog records cannot certify them.");
                    if(d.metric<=AchievementMetric.FullGoalCategory&&f.custom==1||d.metric==AchievementMetric.ModeMinimum&&f.mode>=0)issues.Add("ERROR: "+at+"unsupported scope for this numbered-catalog condition.");
                    if((d.metric==AchievementMetric.FirstStarSequence||d.metric==AchievementMetric.FullGoalCategory)&&(f.size>0||f.minimumOccupied>0))issues.Add("ERROR: "+at+"whole-category conditions support mode and difficulty scope only.");
                    int cap=catalog.levelsPerCategory*(f.mode<0?catalog.modes:1)*(f.difficulty<0?catalog.difficulties:1);
                    if(d.metric==AchievementMetric.CatalogLevels&&t.target>cap||d.metric==AchievementMetric.ColorCollection&&t.target>6||d.metric==AchievementMetric.SizeCollection&&t.target>7||d.metric==AchievementMetric.FirstStarSequence&&t.target>catalog.levelsPerCategory)issues.Add("ERROR: "+at+"threshold exceeds the applicable content capacity.");
                    if(!f.allowPowerUps&&(f.minPowerUses>0||f.powerKinds>0||f.powerFinish))issues.Add("ERROR: "+at+"power use contradicts the unassisted filter.");
                    var rewardIds=new HashSet<string>();
                    if(t.rewards!=null)foreach(var r in t.rewards)if(r==null||!ValidId(r.id)||!rewardIds.Add(r.id)||r.enabled&&!r.Valid)issues.Add("ERROR: "+at+"invalid reward quantity/identifier/type or duplicate entry ID.");
                    if(f.minimumDirect>=7&&f.powerKind==2)issues.Add("WARNING: "+at+"seven direct Blast edits cannot occur on current surface boards.");
                }
                if(string.IsNullOrWhiteSpace(d.iconKey))issues.Add("WARNING: "+at+"icon missing; generic fallback used.");
                if(!d.enabled&&string.IsNullOrWhiteSpace(d.disabledReason))issues.Add("WARNING: "+at+"disabled without explanation.");
                if(d.metric==AchievementMetric.AllNonHidden&&!d.hidden)issues.Add("WARNING: "+at+"meta excludes itself and all other meta definitions to prevent cycles.");
                if(d.metric==AchievementMetric.CapturePeak||d.metric==AchievementMetric.CapturePercentPeak||d.metric==AchievementMetric.FastestSeconds||d.metric==AchievementMetric.ScorePeak)issues.Add("WARNING: "+at+"practical feasibility needs gameplay calibration; static validation is not a proof.");
            }
            return issues;
        }
        private static bool ValidId(string id)=>!string.IsNullOrEmpty(id)&&Regex.IsMatch(id,"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$");
        private static bool UnsupportedPercentageMetric(AchievementMetric m)=>m!=AchievementMetric.CatalogLevels&&m!=AchievementMetric.FullCategories&&m!=AchievementMetric.TouchedCategories;
        public static int FilledStars(int tierCount,int completed)=>completed>=tierCount?3:tierCount==1?0:Math.Min(completed,2);
        public static bool HasRewards(AchievementTier tier){if(tier.rewards!=null)foreach(var r in tier.rewards)if(r!=null&&r.Valid)return true;return false;}
    }
}
