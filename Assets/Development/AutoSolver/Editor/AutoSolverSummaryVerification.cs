using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace FloodFill.Development.AutoSolver
{
    public static class AutoSolverSummaryVerification
    {
        private static int checks;
        private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
        public static void VerifyStore()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            string path=Path.Combine(Path.GetDirectoryName(Application.dataPath),"UserSettings/SummaryVerification.json");
            if(File.Exists(path))File.Delete(path);
            var store=new AutoSolverSummaryStore(path);
            int index=0;
            for(int mode=0;mode<3;mode++)for(int difficulty=0;difficulty<4;difficulty++)
            {
                foreach(string outcome in new[]{"Lost","Won","Won"})
                {
                    store.Data.attempts.Add(new AutoSolverAttempt{id=(index++).ToString(),kind="Numbered",mode=mode,difficulty=difficulty,level=1,
                        seed=1000+mode*4+difficulty,startedUtc=DateTime.UtcNow.AddMinutes(index).ToString("O"),finishedUtc=DateTime.UtcNow.AddMinutes(index+1).ToString("O"),
                        outcome=outcome,moves=outcome=="Lost"?30:index%3==2?18:20,moveBudget=30});
                }
            }
            Check(AutoSolverSummaryWindow.LevelTime(new AutoSolverAttempt())=="—","Legacy rows do not fabricate level duration");
            Check(AutoSolverSummaryWindow.LevelTime(new AutoSolverAttempt{hasLevelElapsedTime=true,levelElapsedSeconds=125.25})=="00:02:05.2","Level duration formatting preserves tenths");
            store.Data.attempts[0].hasLevelElapsedTime=true;store.Data.attempts[0].levelElapsedSeconds=125.25;
            store.Save();store.Save();Check(File.Exists(path+".bak"),"Atomic saves keep backup");
            var restored=new AutoSolverSummaryStore(path);Check(restored.Data.attempts.Count==36,"All attempts survive reload");
            Check(restored.Data.attempts[0].hasLevelElapsedTime && restored.Data.attempts[0].levelElapsedSeconds==125.25,"New duration fields persist alongside legacy history");
            var rows=AutoSolverLevelSummary.Build(restored.Data.attempts).ToList();Check(rows.Count==12,"All mode/difficulty groups independent");
            foreach(var row in rows)
            {
                Check(row.TriesToWin==2&&row.Attempts.Count==3&&row.Wins==2,"First-win tries and replay counts");
                Check(row.BestMoves==18&&row.LatestWin.moves==20&&row.LatestWin.moveBudget==30,"Best and latest successful result stay distinct");
                Check(row.LatestWin.seed==1000+row.LatestWin.mode*4+row.LatestWin.difficulty,"Seeds retained per identity");
            }
            restored.Data.attempts.Add(new AutoSolverAttempt{id="interrupted",kind="Numbered",mode=0,difficulty=0,level=2,outcome="Running"});restored.Save();
            var afterRestart=new AutoSolverSummaryStore(path);Check(afterRestart.Data.attempts.Last().outcome=="Interrupted","Unfinished runs retain history after restart");
            var unfinished=AutoSolverLevelSummary.Build(afterRestart.Data.attempts).Single(a=>a.LatestAttempt.level==2);
            Check(!unfinished.Completed&&unfinished.LatestWin==null&&unfinished.LatestAttempt.outcome=="Interrupted","Unfinished level stays visible after reload");
            for(int retry=0;retry<3;retry++)afterRestart.Data.attempts.Add(new AutoSolverAttempt{id="failed20-"+retry,kind="Numbered",mode=0,difficulty=3,level=20,
                seed=1212414315,moves=17,moveBudget=17,voxels=296,capturedPercentage=99.66216f,outcome="Lost",startedUtc=DateTime.UtcNow.AddHours(1).AddSeconds(retry).ToString("O")});
            var failed=AutoSolverLevelSummary.Build(afterRestart.Data.attempts).Single(a=>a.LatestAttempt.level==20);
            Check(!failed.Completed&&failed.Attempts.Count==3&&failed.Losses==3&&failed.Wins==0,"Failed-only level 20 has its own row and retry counts");
            Check(failed.TriesToWin==0&&failed.BestMoves==0&&failed.LatestAttempt.moves==17&&failed.LatestAttempt.moveBudget==17,"No fabricated win statistics for failed level");
            afterRestart.Data.attempts.Add(new AutoSolverAttempt{id="playing20",kind="Numbered",mode=0,difficulty=3,level=20,
                seed=1212414315,moves=5,moveBudget=17,outcome="Running",startedUtc=DateTime.UtcNow.AddHours(2).ToString("O")});
            var playing=AutoSolverLevelSummary.Build(afterRestart.Data.attempts).Single(a=>a.LatestAttempt.level==20);
            Check(!playing.Completed&&playing.LatestAttempt.outcome=="Running"&&playing.LatestAttempt.moves==5&&playing.Attempts.Count==4&&playing.Losses==3,"New in-progress retry updates row without a win");
            afterRestart.Save();var resumed=new AutoSolverSummaryStore(path);
            Check(AutoSolverLevelSummary.Build(resumed.Data.attempts).Any(a=>a.LatestAttempt.level==20&&!a.Completed&&a.Attempts.Count==4),"All-level rows and failed tries survive reload");
            string existingPath=Path.Combine(Path.GetDirectoryName(Application.dataPath),"UserSettings/SummaryExistingHistory.json");
            if(File.Exists(existingPath))
            {
                var existing=new AutoSolverSummaryStore(existingPath);
                var actual=AutoSolverLevelSummary.Build(existing.Data.attempts).Single(a=>a.LatestAttempt.mode==0&&a.LatestAttempt.difficulty==3&&a.LatestAttempt.level==20);
                Check(actual.Attempts.Count==3&&actual.Losses==3&&!actual.Completed,"Existing user level 20 failures display without migration or rewriting history");
            }
            string damaged=path+".corrupt-test";File.WriteAllText(damaged,"{invalid");
            var invalid=new AutoSolverSummaryStore(damaged);invalid.Save();Check(File.ReadAllText(damaged)=="{invalid"&&!string.IsNullOrEmpty(invalid.Error),"Damaged history is preserved");
            File.WriteAllText("/tmp/FloodSolverSummaryStoreVerification.txt",checks+" store checks passed: all 12 sections, unfinished/failed level 20 visibility, live retries, existing history compatibility, grouping, retries, replay best/latest results, seeds, atomic backup, reload and interrupted runs, damaged-file preservation.\n");
            AutoSolverSummaryStore.Shared.Data.attempts.Clear(); AutoSolverSummaryStore.Shared.Save();
            AutoSolverWorkflowVerification.BeginBatch();
        }
        public static void VerifySeparateProcess()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            var data=AutoSolverSummaryStore.Shared.Data;
            Check(data.attempts.Count>=4,"History survives a separate Unity process");
            Check(data.attempts.Count(a=>a.Won&&a.kind=="Numbered"&&a.mode==2&&a.difficulty==0&&a.level<=2)==2,"Two real solver wins persist");
            Check(data.attempts.Where(a=>a.Won&&a.level<=2).All(a=>a.moves>0&&a.moveBudget>=a.moves&&a.solverMoves==a.moves&&a.searchMilliseconds>0),"Actual moves budget and solver metrics persist");
            Check(data.attempts.Where(a=>a.Won&&a.level<=2).All(a=>a.hasLevelElapsedTime&&a.levelElapsedSeconds>0),"Per-level completion time survives a fresh Unity process");
            File.WriteAllText("/tmp/FloodSolverSummaryRestartVerification.txt",checks+" separate-process solver history checks passed.\n");
        }
    }
}
