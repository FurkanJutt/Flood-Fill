using System;
using System.Collections.Generic;
using FloodFill.ThreeD.PowerUps;
using UnityEngine;
namespace FloodFill.ThreeD
{
    public sealed partial class FloodFillGameManager3D
    {
        private FloodPowerUps3D powerUps;
        public FloodPowerUps3D PowerUps=>powerUps!=null?powerUps:(powerUps=GetComponent<FloodPowerUps3D>());
        public OrbitCameraController3D PowerUpCamera=>orbitCamera;
        public int PowerUpColorCount=>colors.Length;
        public void RefreshPowerUpUI()=>RefreshUI();
        private FloodPowerUpAttempt3D powerUpAttempt=new FloodPowerUpAttempt3D();
        private readonly HashSet<int> powerRewardLedger=new HashSet<int>();
        public int PowerUpsUsed(FloodPowerUpKind kind)=>powerUpAttempt.used[(int)kind];
        private void ResetPowerUpAttempt(){PowerUps?.CancelActivePowerUp();powerUpAttempt=new FloodPowerUpAttempt3D();powerRewardLedger.Clear();}
        private FloodPowerUpAttempt3D CapturePowerUpAttempt()
        {
            powerUpAttempt.rewardedCubeIndices=new int[powerRewardLedger.Count];powerRewardLedger.CopyTo(powerUpAttempt.rewardedCubeIndices);return powerUpAttempt;
        }
        private void RestorePowerUpAttempt(FloodPowerUpAttempt3D saved)
        {
            powerUpAttempt=saved??new FloodPowerUpAttempt3D();if(powerUpAttempt.used?.Length!=3)powerUpAttempt.used=new int[3];
            powerRewardLedger.Clear();if(powerUpAttempt.rewardedCubeIndices!=null)foreach(int i in powerUpAttempt.rewardedCubeIndices)powerRewardLedger.Add(i);
        }
        private int PowerUpScoreCount(int originalNewCount)
        {
            int fresh=0;var cells=boardManager.CapturedCells;for(int i=0;i<cells.Count;i++)if(powerRewardLedger.Add(boardManager.LogicalIndex(cells[i])))fresh++;
            return powerUpAttempt.scoreGuardActive?fresh:originalNewCount;
        }
        public void SelectPowerUpColor(int color)
        {
            if(color<0||color>=colors.Length||!CanInteract)return;
            if(color!=SelectedColorIndex){SelectedColorIndex=color;SecondaryGoals?.SelectColor(color);FloodFill.ThreeD.Audio.FloodAudioSignals.SelectColor();}
            RefreshUI();QueueSelectionSave();
        }
        public bool CommitPowerUpRecolor(FloodPowerUpKind kind,VoxelCell3D target,int color,FloodPowerUpSettings3D settings)
        {
            if(settings==null||!settings.enablePowerUpSystem||kind==FloodPowerUpKind.SmartHint||!settings.Definition(kind).enabled)return false;
            if(!CanInteract||State!=GameState.Playing||Time.time<boardManager.VisualsSettledAt||!FloodPowerUpInventory3D.Available(kind))return false;
            var definition=settings.Definition(kind);int cost=Mathf.Max(0,definition.moveCost);
            if(cost>currentMoveBudget-MoveCount)return false;
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(settings.Unlimited)MarkAchievementAttemptIneligible("Unlimited development inventory");
#endif
            var before=CapturePowerUpCheckpoint();if(before==null)return false;
            // Clone only at a paid transaction boundary, not on every frame or every cube.
            var rollback=JsonUtility.FromJson<FloodFillSessionSaveData3D>(JsonUtility.ToJson(before));var randomState=UnityEngine.Random.state;var previousSolver=LastSolverResult;
            var achievementProbe=BeforeDirectAchievementAction(target,color,kind==FloodPowerUpKind.ColorBlast);
            if(!boardManager.TryPowerRecolor(target,color,kind==FloodPowerUpKind.ColorBlast,settings))return false;
            MoveCount+=cost;powerUpAttempt.scoreGuardActive=true;
            int fresh=PowerUpScoreCount(boardManager.LastNewlyCapturedCount);
            if(settings.awardScoreForPowerUpCaptures)AwardScore(fresh);
            if(settings.trackPowerUpUsage)powerUpAttempt.used[(int)kind]++;
            SecondaryGoals?.PowerUpChange(MoveCount,boardManager.LastNewlyCapturedCount,boardManager.CapturedVoxelCount,settings.allowCompatibleGoalProgressFromPowerUps);
            RecordAchievementAction(achievementProbe,(int)kind);
            LastSolverResult=null;var after=CapturePowerUpCheckpoint();
            if(boardManager.IsFullyCaptured)after.gameState=(int)GameState.Won;
            else if(MoveCount>=currentMoveBudget)after.gameState=(int)GameState.Lost;
            if(!FloodPowerUpInventory3D.Commit(kind,after))
            {
                boardManager.RestorePersistentState(rollback.board,colors);MoveCount=rollback.moveCount;Score=rollback.score;RestorePowerUpAttempt(rollback.powerUpAttempt);RestoreSecondaryAttempt(rollback);achievementAttempt=rollback.achievementAttempt;RestoreAchievementConnections();UnityEngine.Random.state=randomState;LastSolverResult=previousSolver;
                RefreshUI();return false;
            }
            if(boardManager.IsFullyCaptured)ScheduleResult(GameState.Won);
            else if(MoveCount>=currentMoveBudget)ScheduleResult(GameState.Lost);
            else {RefreshUI();SaveSession();}
            return true;
        }
        public bool CommitHintUse(FloodPowerUpSettings3D settings)
        {
            if(!CanInteract||State!=GameState.Playing)return false;
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(settings.Unlimited)MarkAchievementAttemptIneligible("Unlimited development inventory");
#endif
            var previousAchievement=achievementAttempt!=null?JsonUtility.FromJson<Achievements.AchievementAttempt>(JsonUtility.ToJson(achievementAttempt)):null;
            RecordAchievementHint();
            if(settings.trackPowerUpUsage)powerUpAttempt.used[(int)FloodPowerUpKind.SmartHint]++;
            if(FloodPowerUpInventory3D.Commit(FloodPowerUpKind.SmartHint,CapturePowerUpCheckpoint()))return true;
            achievementAttempt=previousAchievement;
            if(settings.trackPowerUpUsage)powerUpAttempt.used[(int)FloodPowerUpKind.SmartHint]--;return false;
        }
        public void ExecuteHintMove(VoxelCell3D cube,int color){SelectColor(color);HandleVoxelClicked(cube);}
    }
}
