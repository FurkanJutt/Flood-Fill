using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FloodFill.ThreeD.Audio;
using FloodFill.ThreeD.Solver;
using NaughtyAttributes;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace FloodFill.ThreeD.PowerUps
{
    [DisallowMultipleComponent]
    public sealed class FloodPowerUps3D : MonoBehaviour
    {
        public FloodPowerUpSettings3D settings;
        public FloodFillGameManager3D game;
        public FloodFillMobileUI3D ui;
        public FloodPowerUpHighlight3D highlights;
        public FloodPowerUpState State {get;private set;}
        public bool IsExecuting=>State==FloodPowerUpState.Executing;
        public bool IsTargeting=>State==FloodPowerUpState.SwapTargeting||State==FloodPowerUpState.BlastTargeting;
        public int TargetColor {get;private set;}=-1;
        public VoxelCell3D Target {get;private set;}
        public FloodPowerUpKind? SelectedKind {get;private set;}
        public FloodHintRecommendation3D CurrentHint {get;private set;}
        public event Action Changed;
        public bool Ready=>settings!=null&&settings.enablePowerUpSystem&&game!=null&&game.CanInteract&&Time.time>=game.BoardManager.VisualsSettledAt&&(settings.allowPowerUpsInCustomGames||game.NumberedLevel>0)&&ui!=null&&ui.PowerUpGameplayVisible;
        public bool CanSelect=>settings!=null&&settings.enablePowerUpSystem&&game!=null&&game.CanChooseColor&&(IsExecuting||Time.time>=game.BoardManager.VisualsSettledAt)&&(settings.allowPowerUpsInCustomGames||game.NumberedLevel>0)&&ui!=null&&ui.PowerUpGameplayVisible;
        private VoxelBoardManager3D board;
        private OrbitCameraController3D cameraRig;
        private CancellationTokenSource hintCancellation;
        private Task<FloodHintResult3D> hintTask;
        private long hintRevision;
        private int request;
        private FloodHintPlan3D cache;
        private FloodHintResult3D hintResult;
        private int hintCandidate;
        private VoxelCell3D representative;
        private FloodHintView3D hintView;
        private bool hintPaid,freeHint,focusInterrupted,committing,lastReady;
        private float executeAfter=-1,finishAt;
        private readonly List<VoxelCell3D> primary=new List<VoxelCell3D>(),secondary=new List<VoxelCell3D>(),directTargets=new List<VoxelCell3D>(7);
        private static readonly string[] ColorNames={"green","purple","yellow","orange","blue","cyan"};
        private void Awake()
        {
            if(game==null)game=GetComponent<FloodFillGameManager3D>();
            if(settings==null)settings=Resources.Load<FloodPowerUpSettings3D>("FloodPowerUps");
            if(settings==null)settings=ScriptableObject.CreateInstance<FloodPowerUpSettings3D>();
            FloodPowerUpInventory3D.Configure(settings);
        }
        private void Start()
        {
            if(game==null)return;
            board=game.BoardManager;cameraRig=game.PowerUpCamera;
            if(ui==null)ui=FindAnyObjectByType<FloodFillMobileUI3D>();
            if(highlights==null)
            {
                var root=new GameObject("PowerUpHighlights",typeof(MeshFilter),typeof(MeshRenderer),typeof(FloodPowerUpHighlight3D));root.transform.SetParent(board.PowerUpBoardRoot,false);highlights=root.GetComponent<FloodPowerUpHighlight3D>();
            }
            highlights.Initialize(settings.outlineMaterial);
            BindEvents();Refresh();
        }
        private void OnDestroy()
        {
            UnbindEvents();CancelActivePowerUp();
        }
        private void OnEnable()
        {
            if(!Application.isPlaying)return;
            if(game==null)game=GetComponent<FloodFillGameManager3D>();
            if(game==null)return;board=game.BoardManager;cameraRig=game.PowerUpCamera;
            if(settings==null||board==null||cameraRig==null)return;
            FloodPowerUpInventory3D.Configure(settings);if(highlights!=null)highlights.Initialize(settings.outlineMaterial);BindEvents();CancelActivePowerUp();
        }
        private void BindEvents()
        {
            UnbindEvents();if(board!=null)board.PowerUpBoardChanged+=OnBoardChanged;if(game!=null)game.UIChanged+=OnGameChanged;
            if(cameraRig!=null)cameraRig.HintFocusInterrupted+=OnManualFocusInterrupt;FloodPowerUpInventory3D.Changed+=Refresh;
        }
        private void UnbindEvents()
        {
            if(board!=null)board.PowerUpBoardChanged-=OnBoardChanged;if(game!=null)game.UIChanged-=OnGameChanged;
            if(cameraRig!=null)cameraRig.HintFocusInterrupted-=OnManualFocusInterrupt;FloodPowerUpInventory3D.Changed-=Refresh;
        }
        private void OnDisable(){UnbindEvents();CancelActivePowerUp();}
        public bool Available(FloodPowerUpKind kind)=>settings!=null&&settings.Definition(kind).enabled&&FloodPowerUpInventory3D.Available(kind)&&(kind==FloodPowerUpKind.SmartHint||game!=null&&settings.Definition(kind).moveCost<=game.CurrentMoveBudget-game.MoveCount);
        private void Trace(string text){if(settings!=null&&settings.debugLogging)Debug.Log("[Power-Ups] "+text,this);}
        private void Sound(AudioClip clip)=>FloodAudioManager.Instance?.PlayPowerUpClip(clip);
        private void Refresh(){Changed?.Invoke();}
        private void Message(string text){ui?.SetPowerUpInstruction(text);}
        public void Select(FloodPowerUpKind kind)
        {
            if(SelectedKind==kind){CancelActivePowerUp();return;}
            if(!CanSelect||!Available(kind)){Sound(settings.unavailableSound);return;}
            CancelActivePowerUp();SelectedKind=kind;TargetColor=-1;Trace("Selected "+kind);
            if(kind==FloodPowerUpKind.SmartHint){BeginHint(false);return;}
            State=kind==FloodPowerUpKind.ColorSwap?FloodPowerUpState.SwapTargeting:FloodPowerUpState.BlastTargeting;
            game.RefreshPowerUpUI();Sound(settings.selectionSound);Message(kind==FloodPowerUpKind.ColorSwap?"Select a block and a color.":"Select a block and a blast color.");Refresh();
        }
        public bool HandlePalette(int color)
        {
            if(!IsTargeting)return false;
            if(Goals.FloodSecondaryGoalsUI3D.ConsumedPointerThisFrame)return true;
            if(!Ready||color<0||color>=game.PowerUpColorCount)return true;
            TargetColor=color;game.SelectPowerUpColor(color);Refresh();
            if(Target!=null)ExecuteTarget();return true;
        }
        public bool HandleCube(VoxelCell3D cell)
        {
            if(!IsTargeting)return false;
            if(Goals.FloodSecondaryGoalsUI3D.ConsumedPointerThisFrame)return true;
            if(!Ready||!board.IsValidPowerTarget(cell)){Sound(settings.invalidActionSound);return true;}
            Target=cell;primary.Clear();secondary.Clear();primary.Add(cell);
            board.GetPowerTargets(cell,State==FloodPowerUpState.BlastTargeting,directTargets);
            for(int i=1;i<directTargets.Count;i++)secondary.Add(directTargets[i]);
            var def=settings.Definition(SelectedKind.Value);var tint=def.highlightColor;tint.a*=def.highlightIntensity;
            highlights.Show(board,primary,tint,secondary,settings.neighborHighlightColor,settings.highlightPulseSpeed);
            if(TargetColor>=0)ExecuteTarget();return true;
        }
        private void ExecuteTarget()
        {
            if(!Ready||!SelectedKind.HasValue)return;
            var kind=SelectedKind.Value;bool success;
            committing=true;cache=null;
            try{success=game.CommitPowerUpRecolor(kind,Target,TargetColor,settings);}
            finally{committing=false;}
            if(!success){Sound(settings.invalidActionSound);Message(string.IsNullOrEmpty(FloodPowerUpInventory3D.LastError)?"Choose a different color or a valid target.":FloodPowerUpInventory3D.LastError);Refresh();return;}
            Trace("Committed "+kind+" at board revision "+board.PowerUpRevision);Sound(settings.Definition(kind).activationSound);ui?.SetPowerUpPaletteHint(-1);
            Target=null;TargetColor=-1;State=FloodPowerUpState.Executing;finishAt=Time.time+board.LastRecolorAnimationDuration;
            Message(kind==FloodPowerUpKind.ColorSwap?"Color Swap":"Color Blast");game.RefreshPowerUpUI();Refresh();
        }
        private void OnBoardChanged()
        {
            if(committing)return;
            CancelActivePowerUp();
        }
        private void OnGameChanged()
        {
            if(!committing&&!IsExecuting&&!game.CanInteract)CancelActivePowerUp();
            Refresh();
        }
        private void OnManualFocusInterrupt(){focusInterrupted=true;executeAfter=-1;}
        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame&&State!=FloodPowerUpState.Idle)CancelActivePowerUp();
#endif
            if(board==null)return;
            bool ready=Ready;if(lastReady!=ready){lastReady=ready;Refresh();}
            if(IsExecuting&&Time.time>=finishAt){highlights.Clear();State=FloodPowerUpState.Idle;SelectedKind=null;ui?.RestorePowerUpInstruction();game.RefreshPowerUpUI();Refresh();}
            if(hintTask!=null&&hintTask.IsCompleted)ReceiveHint();
            if(State==FloodPowerUpState.FocusingHint&&!cameraRig.IsHintFocusMoving)RevealHint();
            if(State==FloodPowerUpState.HintDisplayed&&!hintPaid&&!freeHint&&cameraRig.IsHintSelectable(representative))PayForHint();
            if(executeAfter>=0&&Time.unscaledTime>=executeAfter)
            {
                executeAfter=-1;
                if(State==FloodPowerUpState.HintDisplayed&&hintRevision==board.PowerUpRevision&&Ready&&cameraRig.IsHintSelectable(representative))
                {var cell=representative;int color=CurrentHint.color;CancelActivePowerUp();game.ExecuteHintMove(cell,color);}
            }
        }
        private void BeginHint(bool development)
        {
            if(!Ready||!board.TryCreateSolverSnapshot(out var snapshot))return;
            freeHint=development;SelectedKind=FloodPowerUpKind.SmartHint;State=FloodPowerUpState.Calculating;hintRevision=board.PowerUpRevision;
            var captured=new int[board.CapturedCells.Count];for(int i=0;i<captured.Length;i++)captured[i]=board.LogicalIndex(board.CapturedCells[i]);
            int target=Mathf.Clamp(settings.calculationTimeBudgetMs,50,settings.maximumSearchBudgetMs);
            if(settings.enableExtendedSearch)target=settings.maximumSearchBudgetMs;
            int maximum=Mathf.Max(target,settings.maximumSearchBudgetMs);
            var options=new MoveSolverSettings {greedySearchTimeBudgetMs=Mathf.Max(10,target*2/5),beamSearchTimeBudgetMs=Mathf.Max(10,target/2),runtimeExactSearchTimeBudgetMs=Mathf.Max(5,target/10),exactMaxExpandedStates=settings.maximumSearchStates,maximumSearchMemoryMegabytes=settings.searchMemoryMegabytes};
            hintCancellation=new CancellationTokenSource();var userToken=hintCancellation.Token;var existing=cache;request++;
            hintTask=Task.Run(()=>
            {
                using(var bounded=CancellationTokenSource.CreateLinkedTokenSource(userToken))
                {
                    bounded.CancelAfter(maximum);
                    try{return FloodHintService3D.Calculate(snapshot,captured,options,existing,bounded.Token);}
                    catch(OperationCanceledException){if(userToken.IsCancellationRequested)return null;return FloodHintService3D.LegalFallback(snapshot,captured,userToken);}
                }
            });
            Sound(settings.hintActivationSound!=null?settings.hintActivationSound:settings.smartHint.activationSound);
            Message("Calculating a best-known move…");Refresh();
        }
        private void ReceiveHint()
        {
            var task=hintTask;hintTask=null;bool cancelled=hintCancellation==null||hintCancellation.IsCancellationRequested;
            hintCancellation?.Dispose();hintCancellation=null;
            if(cancelled||State!=FloodPowerUpState.Calculating||hintRevision!=board.PowerUpRevision||!Ready)return;
            if(task.IsFaulted||task.IsCanceled||task.Result==null){CancelActivePowerUp();Message("No useful hint was found. No hint was used.");return;}
            hintResult=task.Result;hintCandidate=0;
            if(!PrepareHintView()){CancelActivePowerUp();Message("No selectable hint was found. No hint was used.");}
        }
        private bool PrepareHintView()
        {
            while(hintResult!=null&&hintCandidate<hintResult.recommendations.Count)
            {
                var candidate=hintResult.recommendations[hintCandidate++];primary.Clear();secondary.Clear();
                foreach(int logical in candidate.primary){var c=board.CellAtLogical(logical);if(c!=null)primary.Add(c);}
                if(!cameraRig.TryFindHintView(board,primary,settings,out hintView))continue;
                CurrentHint=candidate;representative=hintView.representative;hintPaid=false;focusInterrupted=false;
                if(settings.previewConnectedNeighboringRegions)foreach(int logical in candidate.merging){var c=board.CellAtLogical(logical);if(c!=null)secondary.Add(c);}
                Color tint=settings.regionHighlightColor;tint.a*=settings.regionHighlightIntensity;Color merge=board.PaletteColor(candidate.color);merge.a=.35f;
                highlights.Show(board,primary,tint,secondary,merge,settings.highlightPulseSpeed);
                ui?.SetPowerUpPaletteHint(settings.highlightRecommendedPaletteColor?candidate.color:-1);
                State=FloodPowerUpState.FocusingHint;Message("Revealing the recommended region…");cameraRig.FocusHint(hintView,settings);Refresh();return true;
            }
            return false;
        }
        private void RevealHint()
        {
            if(hintRevision!=board.PowerUpRevision||!Ready){CancelActivePowerUp();return;}
            if(!cameraRig.IsHintSelectable(representative)&&!focusInterrupted)
            {if(!PrepareHintView()){CancelActivePowerUp();Message("The recommended cube could not be revealed. No hint was used.");}return;}
            State=FloodPowerUpState.HintDisplayed;
            string color=ColorNames[Mathf.Clamp(CurrentHint.color,0,ColorNames.Length-1)];
            string route=CurrentHint.winningContinuation?$" Best-known finish: {CurrentHint.solutionLength} moves; {Mathf.Max(0,game.CurrentMoveBudget-game.MoveCount)} left.":" Legal progress hint; a winning continuation was not verified.";
            Message($"Change the highlighted region to {color}. {CurrentHint.expectedCaptured} connected (+{CurrentHint.expectedNew} new)."+route);
            if(cameraRig.IsHintSelectable(representative))PayForHint();Refresh();
        }
        private void PayForHint()
        {
            if(hintPaid)return;
            if(!freeHint&&!game.CommitHintUse(settings)){CancelActivePowerUp();Message("The hint could not be saved. No hint was used.");return;}
            Trace("Displayed hint: "+CurrentHint.expectedCaptured+" connected; validated continuation="+CurrentHint.winningContinuation);hintPaid=true;cache=CurrentHint.plan;Sound(settings.hintRevealedSound);
            if(!freeHint&&settings.autoExecuteRecommendedMove&&!focusInterrupted)executeAfter=Time.unscaledTime+settings.automaticExecutionPreviewDuration;
            Refresh();
        }
        [Button("Cancel Active Power-Up")]
        public void CancelActivePowerUp()
        {
            bool active=State!=FloodPowerUpState.Idle;bool executing=IsExecuting;request++;
            hintCancellation?.Cancel();hintCancellation?.Dispose();hintCancellation=null;
            // Observe faults of an obsolete worker without keeping it attached to the UI.
            if(hintTask!=null){var obsolete=hintTask;_ = obsolete.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);hintTask=null;}
            if(IsExecuting&&board!=null)foreach(var cell in directTargets)if(board.IsValidPowerTarget(cell))cell.SetColor(cell.ColorIndex,board.PaletteColor(cell.ColorIndex));
            State=FloodPowerUpState.Idle;SelectedKind=null;Target=null;TargetColor=-1;CurrentHint=null;representative=null;hintResult=null;executeAfter=-1;hintPaid=false;freeHint=false;
            if(cameraRig!=null)cameraRig.CancelHintFocus();highlights?.Clear();ui?.SetPowerUpPaletteHint(-1);ui?.RestorePowerUpInstruction();
            if(executing&&board!=null)board.FinishPowerVisuals();
            if(active&&game!=null&&ui!=null&&ui.PowerUpGameplayVisible)game.RefreshPowerUpUI();
            if(active&&settings!=null)Sound(settings.cancellationSound);Refresh();
        }
        [Button("Add Color Swap")] public void AddColorSwap(){if(Application.isPlaying)FloodPowerUpInventory3D.Add(FloodPowerUpKind.ColorSwap,1);}
        [Button("Add Smart Hint")] public void AddSmartHint(){if(Application.isPlaying)FloodPowerUpInventory3D.Add(FloodPowerUpKind.SmartHint,1);}
        [Button("Add Color Blast")] public void AddColorBlast(){if(Application.isPlaying)FloodPowerUpInventory3D.Add(FloodPowerUpKind.ColorBlast,1);}
        [Button("Refill All Power-Ups")] public void RefillAll(){if(!Application.isPlaying)return;for(int i=0;i<3;i++){var k=(FloodPowerUpKind)i;FloodPowerUpInventory3D.Add(k,settings.Definition(k).startingQuantity-FloodPowerUpInventory3D.Quantity(k));}}
        [Button("Clear Power-Up Inventory")] public void ClearInventory(){if(!Application.isPlaying)return;for(int i=0;i<3;i++){var k=(FloodPowerUpKind)i;FloodPowerUpInventory3D.Add(k,-FloodPowerUpInventory3D.Quantity(k));}}
        [Button("Calculate Smart Hint")] public void CalculateHintPreview(){if(Application.isPlaying&&Ready){CancelActivePowerUp();BeginHint(true);}}
        [Button("Preview Smart Hint Camera")] public void PreviewHintCamera(){if(Application.isPlaying&&CurrentHint!=null&&cameraRig.TryFindHintView(board,primary,settings,out var view))cameraRig.FocusHint(view,settings);}
        [Button("Preview Color Swap")] public void PreviewSwap(){Preview(false);}
        [Button("Preview Color Blast")] public void PreviewBlast(){Preview(true);}
        private void Preview(bool blast){if(!Application.isPlaying||!Ready)return;CancelActivePowerUp();board.PreviewPowerVisual(board.StartingVoxel,blast,settings);}
        [Button("Reload Power-Up Settings")]
        public void ReloadSettings(){if(!Application.isPlaying)return;CancelActivePowerUp();FloodPowerUpInventory3D.Configure(settings);highlights?.Initialize(settings.outlineMaterial);Refresh();}
        [Button("Reset Visual States")]
        public void ResetVisualStates(){if(!Application.isPlaying)return;CancelActivePowerUp();board?.ResetPowerVisuals();}
    }
}
