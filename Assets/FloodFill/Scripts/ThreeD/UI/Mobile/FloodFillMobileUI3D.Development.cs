using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using FloodFill.ThreeD.PowerUps;
using FloodFill.ThreeD.Solver;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.Events;
#endif

namespace FloodFill.ThreeD
{
    public sealed partial class FloodFillMobileUI3D
    {
        [Header("Development Tools")]
        [SerializeField] private GameObject developmentTools;
        [SerializeField] private TMP_Text developmentStatus;
        [SerializeField] private Toggle developmentFps;
        private const string DevelopmentFpsKey = "FloodFill3D.ShowFPS";
        private Coroutine developmentSolver;
        private CancellationTokenSource developmentCancellation;
        private string developmentMessage = "Ready.";

        private void InitializeDevelopmentTools()
        {
            bool visible = PlayerPrefs.GetInt(DevelopmentFpsKey, 0) != 0;
            if (fpsLabel != null) fpsLabel.transform.parent.gameObject.SetActive(visible);
            if (developmentFps != null) developmentFps.SetIsOnWithoutNotify(visible);
        }
        public void OpenDevelopmentTools()
        {
            if (developmentTools == null) return;
            StopDevelopmentSolver();
            if (manager.State == FloodFillGameManager3D.GameState.Playing) manager.PauseGame();
            InitializeDevelopmentTools();
            Show(developmentTools);
            RefreshDevelopmentStatus();
        }
        public void CloseDevelopmentTools() { StopDevelopmentSolver(); Show(settings); }
        public void SetDevelopmentFps(bool visible)
        {
            PlayerPrefs.SetInt(DevelopmentFpsKey, visible ? 1 : 0); PlayerPrefs.Save();
            if (fpsLabel != null) fpsLabel.transform.parent.gameObject.SetActive(visible);
        }
        public void RefillDevelopmentPowerUps()
        {
            manager?.MarkAchievementAttemptIneligible("Development power-up refill");
            var config = Resources.Load<FloodPowerUpSettings3D>("FloodPowerUps");
            if (config == null) { developmentMessage = "Power-up settings are unavailable."; RefreshDevelopmentStatus(); return; }
            bool saved = true;
            for (int i = 0; i < 3; i++)
            {
                var kind = (FloodPowerUpKind)i;
                int amount = Mathf.Max(0, config.Definition(kind).maximumQuantity - FloodPowerUpInventory3D.Quantity(kind));
                if (amount > 0) saved &= FloodPowerUpInventory3D.Add(kind, amount);
            }
            developmentMessage = saved ? "Power-ups refilled." : "Refill could not be saved. Try again.";
            RefreshDevelopmentStatus();
        }
        public void StartDevelopmentSolver()
        {
            StopDevelopmentSolver();
            if (manager.IsLoading || manager.State != FloodFillGameManager3D.GameState.Playing || !manager.BoardManager.HasActiveBoard)
            { developmentMessage = "Start or Continue an unfinished level first."; RefreshDevelopmentStatus(); return; }
            manager.PowerUps?.CancelActivePowerUp();
            manager.MarkAchievementAttemptIneligible("Development auto solver");
            manager.BoardManager.SetBoardVisible(true);
            Show(hud);
            developmentSolver = StartCoroutine(RunDevelopmentSolver());
        }
        public void StopDevelopmentSolver()
        {
            if (developmentCancellation != null) { try { developmentCancellation.Cancel(); } catch (ObjectDisposedException) { } developmentCancellation = null; }
            if (developmentSolver != null) { StopCoroutine(developmentSolver); developmentSolver = null; developmentMessage = "Stopped."; }
            RefreshDevelopmentStatus();
        }
        private IEnumerator RunDevelopmentSolver()
        {
            developmentMessage = "Calculating…";
            while (manager.State == FloodFillGameManager3D.GameState.Playing && (manager.IsLoading || !manager.CanInteract || Time.time < manager.BoardManager.VisualsSettledAt)) yield return null;
            if (manager.State != FloodFillGameManager3D.GameState.Playing) { FinishDevelopmentSolver("Level is no longer playing."); yield break; }
            var board = manager.BoardManager;
            if (!board.TryCreateSolverSnapshot(out var snapshot)) { FinishDevelopmentSolver("Board could not be read."); yield break; }
            string attempt = manager.CurrentAchievementAttempt?.id;
            int moves = manager.MoveCount;
            long revision = board.PowerUpRevision;
            var cancellation = new CancellationTokenSource(); developmentCancellation = cancellation;
            var task = Task.Run(() =>
            {
                try
                {
                    if (!FloodFillRegionGraph3D.TryBuild(snapshot, out var graph, cancellation.Token)) return ((FloodFillRegionGraph3D)null, (FloodFillSolverResult)null);
                    return (graph, FloodFillMoveSolver3D.Solve(graph, new MoveSolverSettings(), true, cancellation.Token));
                }
                finally { cancellation.Dispose(); }
            });
            _ = task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            while (!task.IsCompleted) yield return null;
            developmentCancellation = null;
            if (task.IsFaulted) { Debug.LogException(task.Exception); FinishDevelopmentSolver("Solver failed. Try again."); yield break; }
            if (task.IsCanceled) { FinishDevelopmentSolver("Stopped."); yield break; }
            var (graph, result) = task.Result;
            if (graph == null || result == null || !result.SolutionValidated) { FinishDevelopmentSolver("No validated solution found."); yield break; }
            if (result.BestMoveCount > manager.CurrentMoveBudget - manager.MoveCount)
            { FinishDevelopmentSolver($"Needs {result.BestMoveCount} moves; only {manager.CurrentMoveBudget-manager.MoveCount} remain."); yield break; }
            for (int i = 0; i < result.BestMoveCount; i++)
            {
                while (!manager.IsLoading && manager.State == FloodFillGameManager3D.GameState.Playing &&
                    (!manager.CanInteract || Time.time < board.VisualsSettledAt)) yield return null;
                if (manager.CurrentAchievementAttempt?.id != attempt || manager.IsLoading || manager.State != FloodFillGameManager3D.GameState.Playing || manager.MoveCount != moves || board.PowerUpRevision != revision)
                { FinishDevelopmentSolver("Board changed. Start solver again."); yield break; }
                var cell = board.CellAtLogical(graph.RepresentativeLogicalIndices[result.BestRegionSequence[i]]);
                if (cell == null) { FinishDevelopmentSolver("Solver target is unavailable."); yield break; }
                developmentMessage = $"Solving {i+1} / {result.BestMoveCount}";
                manager.ExecuteHintMove(cell, result.BestColorSequence[i]);
                if (manager.MoveCount != moves + 1) { FinishDevelopmentSolver("Move was not accepted. Start solver again."); yield break; }
                moves = manager.MoveCount; revision = board.PowerUpRevision;
                yield return new WaitForSecondsRealtime(.4f);
            }
            FinishDevelopmentSolver(manager.State == FloodFillGameManager3D.GameState.Won ? "Level completed." : "Playback finished.");
        }
        private void FinishDevelopmentSolver(string message) { developmentMessage = message; developmentSolver = null; RefreshDevelopmentStatus(); }
        public void RestartDevelopmentLevel()
        {
            StopDevelopmentSolver();
            if (manager.IsLoading || !manager.BoardManager.HasActiveBoard)
            { developmentMessage = "Load a level first."; RefreshDevelopmentStatus(); return; }
            Restart();
        }
        public void NextDevelopmentLevel()
        {
            StopDevelopmentSolver();
            if (manager.NumberedLevel <= 0 || manager.NumberedLevel >= 100 || !FloodLevelProgression3D.Get(manager.NumberedMode, (int)manager.SelectedDifficulty, manager.NumberedLevel).completed)
            { developmentMessage = "Complete the current numbered level first. Level 100 has no next level."; RefreshDevelopmentStatus(); return; }
            started = true; manager.StartNumberedLevel(manager.NumberedMode, (int)manager.SelectedDifficulty, manager.NumberedLevel + 1); Show(loading);
        }
        private void RefreshDevelopmentStatus()
        {
            if (developmentStatus == null || manager == null) return;
            developmentStatus.text = (manager.NumberedLevel > 0 ? "LEVEL " + manager.NumberedLevel : "CUSTOM LEVEL") +
                "\nMoves: " + manager.MoveCount + " / " + manager.CurrentMoveBudget + "   Time: " + FloodFillGameManager3D.FormatElapsedTime(manager.ElapsedSeconds) +
                "\n" + developmentMessage + "\nSwap: " + FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap) +
                "   Hint: " + FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.SmartHint) + "   Blast: " + FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorBlast);
        }
#if UNITY_EDITOR
        public void BuildDevelopmentToolsForEditor()
        {
            if (Application.isPlaying || developmentTools != null) return;
            uiSkin = Resources.Load<FloodUISkin>("FloodUISkin");
            var settingsContent = settings.transform.Find("Content") ?? settings.transform;
            var entry = AddButton(settingsContent, "DEVELOPMENT TOOLS", new Vector2(0,-410), new Vector2(650,78), Panel, () => { }, 28);
            UnityEventTools.AddPersistentListener(entry.onClick, OpenDevelopmentTools);
            foreach(var button in settingsContent.GetComponentsInChildren<Button>(true))
            {
                var text=button.GetComponentInChildren<TMP_Text>(true);
                if(text!=null&&text.text=="BACK")((RectTransform)button.transform).anchoredPosition=new Vector2(0,-530);
            }
            developmentTools = ModalScreen("DevelopmentTools");
            var content = MakeRect("Content", developmentTools.transform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero,new Vector2(1080,1920));
            content.AddComponent<FloodFillUIFit3D>();
            AddText(content.transform,"DEVELOPMENT TOOLS",44,FontStyles.Bold,new Vector2(0,650),new Vector2(950,80));
            developmentStatus=AddText(content.transform,"Ready.",28,FontStyles.Normal,new Vector2(0,455),new Vector2(900,220),Muted,true);
            DevelopmentEditorButton(content.transform,"START AUTO SOLVER",230,StartDevelopmentSolver);
            DevelopmentEditorButton(content.transform,"STOP SOLVER",120,StopDevelopmentSolver);
            DevelopmentEditorButton(content.transform,"RESTART LEVEL",10,RestartDevelopmentLevel);
            DevelopmentEditorButton(content.transform,"NEXT LEVEL",-100,NextDevelopmentLevel);
            DevelopmentEditorButton(content.transform,"REFILL POWER-UPS",-210,RefillDevelopmentPowerUps);
            var toggleRoot=MakeRect("ShowFPS",content.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-340),new Vector2(700,80));
            var background=MakeImage("Box",toggleRoot.transform,new Vector2(-260,0),new Vector2(60,60),Panel).GetComponent<Image>();
            var check=MakeImage("Check",background.transform,Vector2.zero,new Vector2(38,38),Accent).GetComponent<Image>();
            developmentFps=toggleRoot.AddComponent<Toggle>();developmentFps.targetGraphic=background;developmentFps.graphic=check;
            AddText(toggleRoot.transform,"SHOW FPS",30,FontStyles.Bold,new Vector2(50,0),new Vector2(500,80));
            UnityEventTools.AddPersistentListener(developmentFps.onValueChanged,SetDevelopmentFps);
            DevelopmentEditorButton(content.transform,"BACK",-500,CloseDevelopmentTools);
            developmentTools.SetActive(false);
        }
        private void DevelopmentEditorButton(Transform parent,string label,float y,UnityEngine.Events.UnityAction action)
        {
            var button=AddButton(parent,label,new Vector2(0,y),new Vector2(720,86),Panel,() => { },29);
            UnityEventTools.AddPersistentListener(button.onClick,action);
        }
#endif
    }
}
