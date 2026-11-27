using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FloodFill.ThreeD.Achievements;
sealed class Clock:IAchievementClock {public DateTime UtcNow{get;set;}=new DateTime(2026,11,27,0,0,0,DateTimeKind.Utc);}
sealed class Rewards:IAchievementRewardService
{public bool fail;public int grants;private readonly HashSet<string> tokens=new HashSet<string>();public bool CanGrant(AchievementReward r)=>r.type=="test";public bool TryGrant(string token,IReadOnlyList<AchievementReward> rewards,out string error){error=fail?"Simulated failure":null;if(fail)return false;if(tokens.Add(token))grants++;return true;}}
static class Program
{
 static int checks;static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
 static void Check(bool x,string message){checks++;if(!x)throw new Exception(message);}
 static AchievementCatalogData Catalog(bool testSupplies=true)
 {var data=JsonSerializer.Deserialize<AchievementCatalogData>(File.ReadAllText("Documentation/Achievements/Catalog.v1.json"),Json);
  // Test the implemented supply-gated evaluators in memory; never change the shipped disabled assets.
  if(testSupplies)foreach(var d in data.achievements)if(new[]{46,47,48,49,52,53,54,55}.Contains(d.order))d.enabled=true;return data;}

 static AchievementDefinition D(AchievementEngine e,int n)=>e.Catalog.achievements.Single(x=>x.id=="ff3d."+n.ToString("000"));
 static AchievementTierProgress P(AchievementEngine e,int n,int t=0)=>e.Progress(D(e,n),D(e,n).tiers[t]);
 static AchievementAttempt Attempt(int level=1,int mode=0,int difficulty=0)=>new AchievementAttempt{id=Guid.NewGuid().ToString("N"),key=AchievementEngine.NumberedKey(mode,difficulty,level),mode=mode,difficulty=difficulty,level=level,size=6,occupied=152,budget=20,moves=10,seconds=20,stars=3,finalColor=0,targetMask=1};
 static AchievementAction Action(int seq=1,int kind=-1)=>new AchievementAction{sequence=seq,kind=kind,connected=25,merged=3,capturePercent=25,multiplier=7,scoreDelta=100,freshCubeIds=new[]{10,11,12}};
 static void Main()
 {
  var catalog=Catalog();Check(Catalog(false).achievements.Count(d=>d.enabled)==103,"103 presently enabled; nine unavailable retained");Check(catalog.achievements.Count==112,"112 definitions");Check(catalog.achievements.Sum(d=>d.tiers.Count)==242,"242 milestones");Check(catalog.achievements.Count(d=>d.enabled)==111,"111 enabled");Check(catalog.achievements.Select(d=>d.category).Distinct().Count()==17,"17 categories");Check(!AchievementValidation.Validate(catalog).Any(x=>x.StartsWith("ERROR:")),"catalog validation");
  Check(catalog.achievements.SelectMany(d=>d.tiers).All(t=>t.rewards.Count==0),"no invented rewards");
  var clock=new Clock();var e=new AchievementEngine(catalog,new AchievementSaveData(),clock);int unlocks=0;e.Unlocked+=u=>unlocks++;
  for(int i=1;i<=35;i++){var a=Attempt(i);e.Commit(a,true,false);e.Commit(a,true,false);}
  Check(P(e,7).completed&&P(e,7,1).completed&&!P(e,7,2).completed,"cumulative multiple tier crossing");Check(P(e,7,2).value==35,"35 unique Easy wins");
  for(int i=0;i<5;i++)e.Commit(Attempt(1),true,false);Check(P(e,7,2).value==35,"replays not unique");
  var custom=Attempt();custom.custom=true;custom.level=0;custom.key="custom:test";e.Commit(custom,true,false);Check(P(e,7,2).value==35,"custom does not alter numbered progression");Check(P(e,97).completed,"custom first");
  var power=Attempt(40);power.lastAction=Action(1,0);power.lastAction.direct=1;power.lastAction.merged=3;e.Commit(power,false,false);e.Commit(power,false,false);Check(P(e,46).value==1,"power action exactly once before outcome");Check(P(e,52).value==1,"Swap actual merge");e.Commit(power,false,true);Check(P(e,46).value==1,"failure does not recount action");
  var invalid=Attempt(41);invalid.eligible=false;invalid.lastAction=Action(1,2);e.Commit(invalid,true,false);Check(P(e,48).value==0,"development excluded");
  var hint=Attempt(41);hint.lastAction=Action(1,1);e.Commit(hint,false,false);Check(P(e,47).value==1&&P(e,55).value==0,"reveal differs from follow");hint.lastAction=Action(2);hint.lastAction.hintFollowed=true;e.Commit(hint,false,false);Check(P(e,55).value==1,"matched hint follow");
  var capture=Attempt(42);capture.lastAction=Action();e.Commit(capture,false,false);double captured=P(e,58).value;capture.lastAction=Action(2);e.Commit(capture,false,false);Check(P(e,58).value==captured,"revisit indices not farmable");var restart=Attempt(42);restart.lastAction=Action();e.Commit(restart,false,false);Check(P(e,58).value==captured,"restart cube credit dedupe");
  var score=Attempt(43);score.score=3000;score.lastAction=Action();e.Commit(score,false,false);Check(P(e,86).completed,"score peak unlocks without requiring victory");
  var quick=Attempt(44);quick.seconds=10;e.Commit(quick,true,false);Check(D(e,75).tiers.All(t=>e.Progress(D(e,75),t).completed),"decreasing thresholds unlock all");
  var improve=Attempt(45);improve.moves=18;improve.stars=1;improve.seconds=60;improve.score=2000;e.Commit(improve,true,false);var better=Attempt(45);better.moves=10;better.seconds=30;better.score=3000;e.Commit(better,true,false);Check(P(e,36).value==1&&P(e,67).value==1&&P(e,77).value==1&&P(e,89).value==1,"compare old records before updating");e.Commit(better,true,false);Check(P(e,67).value==1,"duplicate improvement suppressed");
  var isolated=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);for(int i=1;i<=3;i++){var a=Attempt(i);a.goalsAssigned=3;a.goalsEarned=3;a.assignedGoalIds=new[]{"a","b","c"};a.earnedGoalIds=a.assignedGoalIds;isolated.Commit(a,true,false);}Check(P(isolated,83).completed,"same-attempt perfect streak");isolated.Commit(Attempt(4),false,true);Check(P(isolated,83).completed&&P(isolated,83,1).current==0,"streak resets without revocation");
  var noGoals=Attempt(5);noGoals.goalsAssigned=0;isolated.Commit(noGoals,true,false);Check(P(isolated,43).value==3,"zero goals excluded");
  var seeded=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);var records=new List<AchievementLevelRecord>();for(int i=1;i<=35;i++)records.Add(new AchievementLevelRecord{key=AchievementEngine.NumberedKey(0,0,i),mode=0,difficulty=0,level=i,size=6,completed=true,stars=3,legacy=true});int historicalToasts=0;seeded.Unlocked+=u=>historicalToasts++;seeded.Import(records);seeded.Import(records);Check(P(seeded,7,2).value==35&&historicalToasts==0,"idempotent quiet retroactive import");Check(P(seeded,37).value==0,"no invented unassisted history");
  var serialized=JsonSerializer.Serialize(e.Data,Json);var restored=new AchievementEngine(Catalog(),JsonSerializer.Deserialize<AchievementSaveData>(serialized,Json),clock);restored.Commit(better,true,false);Check(P(restored,67).value==1,"Continue/restart roundtrip dedupe");Check(P(restored,75,2).completed,"completed tier retained");
  var timed=Attempt(60);e.ObserveTime(timed,15);double before=e.Data.activeSeconds;e.ObserveTime(timed,15);e.ObserveTime(timed,10);Check(e.Data.activeSeconds==before,"time replay/backward value not double credited");e.ObserveTime(timed,20);Check(e.Data.activeSeconds==before+5,"active time delta");
  Check(AchievementValidation.FilledStars(1,0)==0&&AchievementValidation.FilledStars(1,1)==3,"single-tier three stars");Check(AchievementValidation.FilledStars(2,1)==1&&AchievementValidation.FilledStars(2,2)==3,"two-tier stars");Check(AchievementValidation.FilledStars(3,2)==2,"three-tier stars");
  var def=D(e,1);var tier=def.tiers[0];Check(!e.TryClaim(def,tier,null,out _)&&P(e,1).claimedRewardIds.Count==0,"no fake reward claim");
  var bad=Catalog();bad.achievements[1].id=bad.achievements[0].id;Check(AchievementValidation.Validate(bad).Any(x=>x.Contains("duplicate/missing achievement")),"duplicate IDs detected");

  // Full mode/difficulty/size filter matrix, including supported custom setup combinations.
  int[] sizes={6,8,10,12,15,18,20};var matrix=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);int serial=0;
  for(int mode=0;mode<3;mode++)for(int difficulty=0;difficulty<4;difficulty++)foreach(int size in sizes)
  {var a=Attempt(++serial,mode,difficulty);a.size=size;matrix.Commit(a,true,false);Check(matrix.Level(a.key).mode==mode&&matrix.Level(a.key).difficulty==difficulty&&matrix.Level(a.key).size==size,"numbered mode/difficulty/size matrix");}
  for(int mode=0;mode<2;mode++)for(int difficulty=0;difficulty<4;difficulty++)foreach(int size in sizes)
  {var a=Attempt(0,mode,difficulty);a.key="custom:"+Guid.NewGuid().ToString("N");a.custom=true;a.size=size;matrix.Commit(a,true,false);Check(matrix.Level(a.key).custom,"custom mode/difficulty/size matrix");}
  Check(P(matrix,72).completed&&P(matrix,100).completed,"all seven numbered/custom sizes");Check(P(matrix,98,1).completed,"custom counts retain successful attempts");
  var exact=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);var catalogRecords=new List<AchievementLevelRecord>();
  for(int mode=0;mode<3;mode++)for(int difficulty=0;difficulty<4;difficulty++)for(int level=1;level<=100;level++)catalogRecords.Add(new AchievementLevelRecord{key=AchievementEngine.NumberedKey(mode,difficulty,level),mode=mode,difficulty=difficulty,level=level,size=sizes[Math.Min(6,(level-1)/15)],completed=true,stars=3,legacy=true});
  exact.Import(catalogRecords);Check(P(exact,4).completed&&P(exact,5,2).completed&&P(exact,6).completed,"all catalog/category completeness");
  for(int n=11;n<=14;n++)Check(P(exact,n).completed&&Math.Abs(P(exact,n).value-100)<.0001,"difficulty percentage denominator");
  for(int n=18;n<=20;n++)Check(P(exact,n).completed&&Math.Abs(P(exact,n).value-100)<.0001,"mode percentage denominator");
  for(int n=22;n<=33;n++)Check(P(exact,n).completed,"individual category filter");
  var color=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);for(int c=0;c<6;c++){var a=Attempt(c+1);a.finalColor=c;color.Commit(a,true,false);}Check(P(color,90).completed&&P(color,93).completed,"all final colors and distinct rotation");
  var failThenWin=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);for(int i=0;i<5;i++)failThenWin.Commit(Attempt(1),false,true);failThenWin.Commit(Attempt(1),true,false);Check(P(failThenWin,84).completed&&P(failThenWin,109).completed,"failed-attempt recovery and Redemption");
  var calendar=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);for(int day=0;day<7;day++){clock.UtcNow=new DateTime(2026,11,27,0,0,0,DateTimeKind.Utc).AddDays(day);calendar.Commit(Attempt(day+1),true,false);}Check(P(calendar,102).completed&&P(calendar,103,1).completed,"distinct and consecutive UTC completion dates");
  int dates=calendar.Data.activeDays.Count;clock.UtcNow=clock.UtcNow.AddDays(-4);calendar.Commit(Attempt(20),true,false);Check(calendar.Data.activeDays.Count==dates,"clock rollback cannot duplicate days");clock.UtcNow=clock.UtcNow.AddDays(12);calendar.Commit(Attempt(21),true,false);Check(P(calendar,105).completed,"seven-day return without requiring application restart");
  // Replay all unsaved actions from the bounded checkpoint outbox, then ignore repeated restores.
  var journal=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);var pending=Attempt(90);pending.pendingActions=new List<AchievementAction>{Action(1,0),Action(2,1),Action(3,2)};pending.lastAction=pending.pendingActions[2];journal.Commit(pending,false,false);Check(P(journal,46).value==1&&P(journal,47).value==1&&P(journal,48).value==1,"pending checkpoint recovers every action");journal.Commit(pending,false,false);Check(P(journal,49).value==3,"outbox replay is idempotent");
  var endless=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);var many=Attempt(91);for(int seq=1;seq<=60;seq++){many.lastAction=Action(seq,0);endless.Commit(many,false,false);}Check(endless.Statistics().powerUses[0]==60&&P(endless,46,2).completed,"lifetime counters continue after all original tiers complete");
  var chain=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);var series=Attempt(92);for(int seq=1;seq<=3;seq++){series.lastAction=Action(seq);series.lastAction.connected=10;chain.Commit(series,false,false);}Check(P(chain,62).completed,"configurable qualifying capture chain");series.lastAction=Action(4);series.lastAction.connected=0;chain.Commit(series,false,false);Check(P(chain,62,1).current==0,"capture chain breaks on nonqualifying move");
  var noBank=Catalog();var banked=noBank.achievements.Single(d=>d.id=="ff3d.046");banked.filters.requireWin=true;banked.filters.stars=3;banked.tiers[0].target=1;var conditional=new AchievementEngine(noBank,new AchievementSaveData(),clock);var eventThenWin=Attempt(93);eventThenWin.stars=0;eventThenWin.lastAction=Action(1,0);conditional.Commit(eventThenWin,false,false);Check(!P(conditional,46).completed,"win-required action remains provisional");eventThenWin.stars=3;conditional.Commit(eventThenWin,true,false);Check(P(conditional,46).completed,"same-attempt deferred action predicate banks on win");
  var legacy=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);var old=Attempt(94);old.historyComplete=false;old.assisted=true;legacy.Commit(old,true,false);Check(P(legacy,7).value==1&&P(legacy,56).value==0,"legacy Continue banks reliable catalog only");
  // Independent claims and a retry-safe future reward handler, with no production rewards configured.
  var claimsEngine=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);claimsEngine.Commit(Attempt(1),true,false);var claimDef=D(claimsEngine,1);var claimTier=claimDef.tiers[0];claimTier.rewards.Add(new AchievementReward{id="test.reward",type="test",identifier="isolated",quantity=1});var handler=new Rewards();handler.fail=true;
  Check(!claimsEngine.TryClaim(claimDef,claimTier,handler,out _)&&P(claimsEngine,1).claimedRewardIds.Count==0,"failed grant never marks claim");handler.fail=false;string beforeGrant=JsonSerializer.Serialize(claimsEngine.Data,Json);Check(claimsEngine.TryClaim(claimDef,claimTier,handler,out _)&&handler.grants==1,"future reward granted once");
  var retryClaims=new AchievementEngine(claimsEngine.Catalog,JsonSerializer.Deserialize<AchievementSaveData>(beforeGrant,Json),clock);Check(retryClaims.TryClaim(D(retryClaims,1),D(retryClaims,1).tiers[0],handler,out _)&&handler.grants==1,"idempotency token survives grant/receipt crash gap");

  var incremental=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);var one=Attempt(70);one.stars=1;incremental.Commit(one,true,false);var two=Attempt(70);two.stars=2;incremental.Commit(two,true,false);var three=Attempt(70);three.stars=3;incremental.Commit(three,true,false);Check(P(incremental,36).value==1,"one-to-three improvement includes intermediate two-star replay");
  var abandoned=Attempt(71);incremental.Commit(abandoned,false,false);incremental.AbandonById(abandoned.id,true);incremental.AbandonById(abandoned.id,true);Check(incremental.Statistics().abandons==1&&incremental.Statistics().restarts==1,"durable abandonment marker deduplicates restart and streak reset");
  var timeScope=Catalog();var hours=timeScope.achievements.Single(d=>d.id=="ff3d.104");hours.filters.difficulty=3;var scopedTime=new AchievementEngine(timeScope,new AchievementSaveData(),clock);var easyTime=Attempt(1,0,0);scopedTime.ObserveTime(easyTime,3600);var expertTime=Attempt(1,0,3);scopedTime.ObserveTime(expertTime,7200);Check(Math.Abs(P(scopedTime,104).value-2)<.001,"active-time difficulty scope uses actual eligible bucket");
  var goalCategory=new AchievementEngine(Catalog(),new AchievementSaveData(),clock);var recordsWithZero=new List<AchievementLevelRecord>();for(int level=1;level<=100;level++)recordsWithZero.Add(new AchievementLevelRecord{key=AchievementEngine.NumberedKey(0,0,level),mode=0,difficulty=0,level=level,completed=level<100,goalsKnown=true,assignedGoals=level<100?new List<string>{"g"}:new List<string>(),earnedGoals=level<100?new List<string>{"g"}:new List<string>()});goalCategory.Import(recordsWithZero);Check(P(goalCategory,45).completed,"zero-goal level does not block a category's actually available goals");
  Console.WriteLine("PASS: "+checks+" achievement assertions; 112 definitions / 242 tiers verified.");
 }
}
