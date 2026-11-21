using System;
using System.Threading;
using FloodFill.ThreeD.Goals;
using FloodFill.ThreeD.Solver;
using UnityEngine;
namespace FloodFill.ThreeD
{
    public sealed partial class FloodFillGameManager3D
    {
        [Header("Optional Secondary Goals")]
        [SerializeField] private FloodSecondaryGoalSettings3D secondaryGoalSettings;
        private FloodSecondaryGoalSettings3D fallbackGoalSettings;
        public FloodSecondaryGoalSettings3D GoalSettings
        {
            get
            {
                if(secondaryGoalSettings!=null)return secondaryGoalSettings;
                secondaryGoalSettings=Resources.Load<FloodSecondaryGoalSettings3D>("FloodSecondaryGoals");
                if(secondaryGoalSettings!=null)return secondaryGoalSettings;
                if(fallbackGoalSettings==null)fallbackGoalSettings=ScriptableObject.CreateInstance<FloodSecondaryGoalSettings3D>();
                return fallbackGoalSettings;
            }
        }
        public FloodNumberedLevelDefinition3D ActiveLevelDefinition { get; private set; }
        public FloodSecondaryGoalTracker3D SecondaryGoals { get; private set; }
        private SecondarySolutionTrace preparedGoalTrace;
        private static SolverPreparation PrepareGoalTrace(FloodFillRegionGraph3D graph,FloodFillSolverResult result,int goalDifficulty,int limit,CancellationToken token)
        {
            return new SolverPreparation { Result=result, GoalTrace=goalDifficulty<0?null:FloodSecondaryGoalGenerator3D.Trace(graph,result.BestRegionSequence,result.BestColorSequence,result.BudgetProfile.GetMoves((FloodFillDifficulty)goalDifficulty),limit,token) };
        }
        private bool PublishNumberedDefinition(FloodFillSolverResult result)
        {
            if(preparedGoalTrace==null||result.BestMoveCount>currentMoveBudget||!boardManager.TryCapturePersistentState(out var snapshot))
            { FailLoading("The level has no validated within-budget solution. Please retry.");return false; }
            try
            {
                SecondaryGoalDefinition[] goals; double milliseconds=0;
                try { goals=FloodSecondaryGoalGenerator3D.Generate(preparedGoalTrace,currentMoveBudget,NumberedMode,(int)difficulty,boardManager.LastGenerationSeed,GoalSettings,solverCancellation.Token,out milliseconds); }
                catch(OperationCanceledException){throw;}
                catch(Exception generationError){Debug.LogWarning("Optional goal generation returned no goals: "+generationError.Message);goals=Array.Empty<SecondaryGoalDefinition>();}
                var definition=new FloodNumberedLevelDefinition3D {id=FloodFillSessionPersistence3D.LevelId(NumberedMode,(int)difficulty,NumberedLevel),mode=NumberedMode,difficulty=(int)difficulty,level=NumberedLevel,seed=boardManager.LastGenerationSeed,moveBudget=currentMoveBudget,palette=(Color[])colors.Clone(),board=snapshot,budgetProfile=CurrentMoveBudgetProfile,witnessRegions=(int[])result.BestRegionSequence.Clone(),witnessColors=(int[])result.BestColorSequence.Clone(),goals=goals,regionCount=preparedGoalTrace.regions,initialCaptured=preparedGoalTrace.initialCaptured,goalGenerationMilliseconds=milliseconds,itemIdentifier=NumberedMode==2?new[]{"Apple","Pear","Orange","Banana"}[(int)((uint)boardManager.LastGenerationSeed%4)]:null,generationParameters=JsonUtility.ToJson(boardManager.ProceduralSettings)};
                if(!FloodFillSessionPersistence3D.SaveLevel(definition,out string error)){FailLoading(error);return false;}
                ActiveLevelDefinition=definition;SecondaryGoals=new FloodSecondaryGoalTracker3D(definition.id,goals,definition.initialCaptured,colors.Length);
                return true;
            }
            catch(Exception e){FailLoading("Goals could not be prepared: "+e.Message);return false;}
        }
        private bool RestoreNumberedDefinition(FloodNumberedLevelDefinition3D definition)
        {
            colors=(Color[])definition.palette.Clone();
            if(!boardManager.RestorePersistentState(definition.board,colors))return false;
            ActiveLevelDefinition=definition;LastSolverResult=definition.SolverResult();CurrentMoveBudgetProfile=definition.budgetProfile;currentMoveBudget=definition.moveBudget;
            SecondaryGoals=new FloodSecondaryGoalTracker3D(definition.id,definition.goals,definition.initialCaptured,colors.Length);
            State=GameState.Playing;
            if(boardManager.TryGetWorldBounds(out Bounds bounds))orbitCamera.FrameBounds(bounds);
            SaveSession();return true;
        }
        private void RestoreSecondaryAttempt(FloodFillSessionSaveData3D saved)
        {
            ActiveLevelDefinition=null;SecondaryGoals=null;
            if(NumberedLevel<=0)return;
            if(FloodFillSessionPersistence3D.TryLoadLevel(NumberedMode,(int)difficulty,NumberedLevel,out var definition,out var error))
            {
                ActiveLevelDefinition=definition;
                SecondaryGoals=new FloodSecondaryGoalTracker3D(definition.id,definition.goals,definition.initialCaptured,colors.Length,saved.secondaryGoalAttempt);
                // Older checkpoints cannot reconstruct historical goals, so don't grant partial-attempt rewards.
                if(saved.moveCount>0&&(saved.secondaryGoalAttempt==null||saved.secondaryGoalAttempt.lastMove!=saved.moveCount||!ReferenceEquals(SecondaryGoals.Attempt,saved.secondaryGoalAttempt)))SecondaryGoals=null;
            }
            else if(error!=null)Debug.LogWarning(error);
        }
    }
}
