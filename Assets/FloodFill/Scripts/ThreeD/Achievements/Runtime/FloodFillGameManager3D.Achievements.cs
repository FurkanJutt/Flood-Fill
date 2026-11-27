using System;
using System.Collections.Generic;
using FloodFill.ThreeD.Achievements;
using UnityEngine;
namespace FloodFill.ThreeD
{
    public sealed partial class FloodFillGameManager3D
    {
        private AchievementAttempt achievementAttempt;
        private int achievementLastMultiplier;
        private bool achievementRestartRequested;
        private string pendingAchievementAbandonId,pendingAchievementAbandonKey;
        private bool pendingAchievementRestart;
        private float nextAchievementTimeSample;
        public AchievementAttempt CurrentAchievementAttempt=>achievementAttempt;
        private static readonly Vector3Int[] AchievementDirections={Vector3Int.left,Vector3Int.right,Vector3Int.up,Vector3Int.down,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};
        private sealed class AchievementProbe
        {
            public int direct,color,score,sourceLogical,rawConnected,rawMerged;
            public bool followed;
            public readonly List<List<int>> targetComponents=new List<List<int>>();
        }
        private readonly AchievementProbe reusableAchievementProbe=new AchievementProbe();
        private readonly List<VoxelCell3D> achievementEdited=new List<VoxelCell3D>(),achievementQueue=new List<VoxelCell3D>();
        private readonly HashSet<VoxelCell3D> achievementVisited=new HashSet<VoxelCell3D>();
        private readonly HashSet<int> achievementRoots=new HashSet<int>(),achievementRawRegions=new HashSet<int>();
        private readonly List<List<int>> achievementComponentPool=new List<List<int>>();
        private readonly List<int> achievementFreshIds=new List<int>();
        private int[] achievementConnections,achievementLogicalToDense;
        private void BuildAchievementConnectionLookup()
        {
            achievementLogicalToDense=new int[boardManager.Width*boardManager.Height*boardManager.Depth];
            for(int i=0;i<achievementLogicalToDense.Length;i++)achievementLogicalToDense[i]=-1;
            achievementConnections=new int[boardManager.TotalVoxelCount];
            for(int i=0;i<boardManager.ActiveCells.Count;i++){achievementLogicalToDense[boardManager.LogicalIndex(boardManager.ActiveCells[i])]=i;achievementConnections[i]=i;}
        }
        private int AchievementConnectionRoot(int dense)
        {
            int root=dense;while(achievementConnections[root]!=root)root=achievementConnections[root];
            while(achievementConnections[dense]!=dense){int next=achievementConnections[dense];achievementConnections[dense]=root;dense=next;}return root;
        }
        private int AchievementLogicalRoot(int logical)=>AchievementConnectionRoot(achievementLogicalToDense[logical]);
        private void JoinAchievementConnections(int a,int b)
        {
            int left=AchievementConnectionRoot(a),right=AchievementConnectionRoot(b);if(left!=right)achievementConnections[Math.Max(left,right)]=Math.Min(left,right);
        }
        private VoxelCell3D AchievementNeighbor(VoxelCell3D cell,Vector3Int direction)
        {
            int x=cell.X+direction.x,y=cell.Y+direction.y,z=cell.Z+direction.z;
            return x<0||y<0||z<0||x>=boardManager.Width||y>=boardManager.Height||z>=boardManager.Depth?null:boardManager.CellAtLogical(x+boardManager.Width*(y+boardManager.Height*z));
        }
        private void PackAchievementConnections()
        {
            var bytes=new byte[achievementConnections.Length*4];Buffer.BlockCopy(achievementConnections,0,bytes,0,bytes.Length);achievementAttempt.connectionHistory=Convert.ToBase64String(bytes);
        }
        private void InitializeAchievementConnections()
        {
            BuildAchievementConnectionLookup();
            foreach(var cell in boardManager.ActiveCells)for(int i=0;i<AchievementDirections.Length;i+=2)
            {
                var n=AchievementNeighbor(cell,AchievementDirections[i]);if(n!=null&&n.ColorIndex==cell.ColorIndex)JoinAchievementConnections(achievementLogicalToDense[boardManager.LogicalIndex(cell)],achievementLogicalToDense[boardManager.LogicalIndex(n)]);
            }
            PackAchievementConnections();
        }
        private void RestoreAchievementConnections()
        {
            if(achievementAttempt==null)return;BuildAchievementConnectionLookup();var bytes=AchievementEngine.DecodeBits(achievementAttempt.connectionHistory);
            bool valid=bytes.Length==achievementConnections.Length*4;
            if(valid)
            {
                Buffer.BlockCopy(bytes,0,achievementConnections,0,bytes.Length);
                for(int i=0;i<achievementConnections.Length;i++)if(achievementConnections[i]<0||achievementConnections[i]>i){valid=false;break;}
            }
            if(!valid){InitializeAchievementConnections();achievementAttempt.historyComplete=false;achievementAttempt.assisted=true;achievementAttempt.ineligibleReason="Missing or invalid connection history; whole-attempt restrictions cannot be certified.";}
        }
        private void CollectAchievementComponent(VoxelCell3D origin,List<VoxelCell3D> output)
        {
            output.Clear();achievementVisited.Add(origin);output.Add(origin);int color=origin.ColorIndex;
            for(int head=0;head<output.Count;head++)foreach(var direction in AchievementDirections)
            {var neighbor=AchievementNeighbor(output[head],direction);if(neighbor!=null&&neighbor.ColorIndex==color&&achievementVisited.Add(neighbor))output.Add(neighbor);}
        }
        private void BeginAchievementAttempt(bool checkpoint=true)
        {
            if(achievementAttempt!=null)return;
            var service=FloodAchievementService.Instance;if(service==null||!service.Ready)return;
            if(!string.IsNullOrEmpty(pendingAchievementAbandonId))service.Engine.AbandonById(pendingAchievementAbandonId,pendingAchievementRestart);
            pendingAchievementAbandonId=pendingAchievementAbandonKey=null;pendingAchievementRestart=false;
            var board=boardManager;string hash="";
            if(board.TryCapturePersistentState(out var initial))hash=FloodFillSessionPersistence3D.BoardHash(initial);
            if(NumberedLevel>0&&ActiveLevelDefinition!=null)hash=ActiveLevelDefinition.boardHash;
            bool custom=NumberedLevel==0&&!IsCampaignGame;
            string key=NumberedLevel>0?AchievementEngine.NumberedKey(NumberedMode,(int)difficulty,NumberedLevel):(custom?"custom:":"campaign:")+hash;
            achievementAttempt=new AchievementAttempt{id=Guid.NewGuid().ToString("N"),key=key,puzzleHash=hash,custom=custom,
                mode=NumberedLevel>0?NumberedMode:SelectedBoardMode==VoxelBoardManager3D.VoxelVolumeMode.HollowCube?0:SelectedBoardMode==VoxelBoardManager3D.VoxelVolumeMode.Items?2:1,
                difficulty=(int)difficulty,level=NumberedLevel,size=board.Width,occupied=board.TotalVoxelCount,budget=currentMoveBudget,seed=board.LastGenerationSeed};
            var record=service.Engine.Level(key);achievementAttempt.wasCompleted=record.completed;achievementAttempt.previousFailures=record.failures;
            if(ActiveLevelDefinition!=null)
            {
                achievementAttempt.goalsAssigned=ActiveLevelDefinition.goals.Length;
                var ids=new string[achievementAttempt.goalsAssigned];for(int i=0;i<ids.Length;i++)ids[i]=ActiveLevelDefinition.goals[i].id;achievementAttempt.assignedGoalIds=ids;
            }
            var bits=new byte[(board.Width*board.Height*board.Depth+7)/8];
            foreach(var cell in board.CapturedCells){int id=board.LogicalIndex(cell);bits[id>>3]|=(byte)(1<<(id&7));}
            achievementAttempt.capturedBits=Convert.ToBase64String(bits);
            InitializeAchievementConnections();
            if(Application.isBatchMode)MarkAchievementAttemptIneligible("Automated batch verification");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(PowerUps!=null&&PowerUps.settings!=null&&PowerUps.settings.Unlimited)MarkAchievementAttemptIneligible("Unlimited development inventory");
#endif
            service.Engine.RegisterLevelMetadata(achievementAttempt,ActiveLevelDefinition!=null);
            if(checkpoint)SaveSession();
        }
        private void RestoreAchievementAttempt(FloodFillSessionSaveData3D saved)
        {
            if(!string.IsNullOrEmpty(pendingAchievementAbandonId))
            {
                string restoredKey=saved.achievementAttempt?.key;
                if(restoredKey==null&&saved.numberedLevel>0)restoredKey=AchievementEngine.NumberedKey(saved.numberedMode,saved.difficulty,saved.numberedLevel);
                if(restoredKey==null&&boardManager.TryCapturePersistentState(out var restoredBoard))restoredKey=(saved.isCampaignGame?"campaign:":"custom:")+FloodFillSessionPersistence3D.BoardHash(restoredBoard);
                if(restoredKey!=pendingAchievementAbandonKey)FloodAchievementService.Instance?.Engine?.AbandonById(pendingAchievementAbandonId,pendingAchievementRestart);
                pendingAchievementAbandonId=pendingAchievementAbandonKey=null;pendingAchievementRestart=false;
            }
            achievementAttempt=saved.achievementAttempt;
            if(achievementAttempt!=null)RestoreAchievementConnections();
            if(achievementAttempt==null)
            {
                // Existing unfinished games still contribute reliable catalog results, never invented move history.
                BeginAchievementAttempt(false);
                if(achievementAttempt!=null){achievementAttempt.historyComplete=false;achievementAttempt.assisted=true;achievementAttempt.ineligibleReason="Legacy checkpoint has no achievement action history.";
                    var bits=AchievementEngine.DecodeBits(achievementAttempt.capturedBits);foreach(var voxel in saved.board.voxels)if(voxel.covered||voxel.captured){int id=voxel.x+saved.board.width*(voxel.y+saved.board.height*voxel.z);bits[id>>3]|=(byte)(1<<(id&7));}achievementAttempt.capturedBits=Convert.ToBase64String(bits);
                    FloodAchievementService.Instance?.Engine?.ResumeBaseline(achievementAttempt,saved.elapsedSeconds);}
            }
        }
        private void AbandonAchievementAttempt()
        {
            if(achievementAttempt!=null&&achievementAttempt.eligible&&State==GameState.Playing){pendingAchievementAbandonId=achievementAttempt.id;pendingAchievementAbandonKey=achievementAttempt.key;pendingAchievementRestart=achievementRestartRequested;}
            achievementRestartRequested=false;achievementAttempt=null;achievementConnections=null;achievementLogicalToDense=null;
        }
        public void MarkAchievementAttemptIneligible(string reason)
        {
            if(achievementAttempt==null)return;achievementAttempt.eligible=false;achievementAttempt.ineligibleReason=reason;
        }
        private AchievementProbe BeforeAchievementAction(VoxelCell3D target,int color,bool blast)=>ProbeAchievementAction(target,color,blast?2:-1);
        private AchievementProbe BeforeDirectAchievementAction(VoxelCell3D target,int color,bool blast)=>ProbeAchievementAction(target,color,blast?2:0);
        private AchievementProbe ProbeAchievementAction(VoxelCell3D target,int color,int kind)
        {
            achievementLastMultiplier=0;var a=achievementAttempt;
            if(a==null||!a.eligible||target==null||color<0||color>=boardManager.PaletteCount||kind==-1&&target.ColorIndex==color)return null;
            if(achievementConnections==null)RestoreAchievementConnections();
            var probe=reusableAchievementProbe;probe.color=color;probe.score=Score;probe.direct=0;probe.followed=false;probe.targetComponents.Clear();probe.sourceLogical=boardManager.LogicalIndex(target);
            if(a.hintPaid&&a.hintColor==color&&a.hintRevision==boardManager.PowerUpRevision)probe.followed=Array.IndexOf(a.hintPrimary,probe.sourceLogical)>=0;
            achievementEdited.Clear();achievementVisited.Clear();
            if(kind==-1)CollectAchievementComponent(target,achievementEdited);else if(kind==2)boardManager.GetPowerTargets(target,true,achievementEdited);else achievementEdited.Add(target);
            foreach(var cell in achievementEdited)if(cell.ColorIndex!=color)probe.direct++;
            achievementVisited.Clear();achievementRawRegions.Clear();
            if(kind==2)foreach(var original in achievementEdited)if(!achievementVisited.Contains(original))
            {
                CollectAchievementComponent(original,achievementQueue);int label=int.MaxValue;foreach(var c in achievementQueue)label=Math.Min(label,boardManager.LogicalIndex(c));achievementRawRegions.Add(label);
            }
            achievementVisited.Clear();
            if(target.ColorIndex==color)CollectAchievementComponent(target,achievementQueue);
            foreach(var cell in achievementEdited)foreach(var direction in AchievementDirections)
            {
                var next=AchievementNeighbor(cell,direction);if(next==null||next.ColorIndex!=color||achievementVisited.Contains(next))continue;
                CollectAchievementComponent(next,achievementQueue);
                int slot=probe.targetComponents.Count;if(slot==achievementComponentPool.Count)achievementComponentPool.Add(new List<int>());
                var ids=achievementComponentPool[slot];ids.Clear();foreach(var c in achievementQueue)ids.Add(boardManager.LogicalIndex(c));probe.targetComponents.Add(ids);
            }
            probe.rawConnected=0;foreach(var group in probe.targetComponents)
            {
                probe.rawConnected+=group.Count;if(kind==2){int label=int.MaxValue;foreach(int id in group)label=Math.Min(label,id);achievementRawRegions.Add(label);}
            }
            probe.rawMerged=kind==2?Math.Max(0,achievementRawRegions.Count-1):probe.targetComponents.Count;
            return probe;
        }
        private void RecordAchievementAction(AchievementProbe probe,int kind)
        {
            if(probe==null||achievementAttempt==null)return;var a=achievementAttempt;
            var bits=AchievementEngine.DecodeBits(a.capturedBits);int required=(boardManager.Width*boardManager.Height*boardManager.Depth+7)/8;if(bits.Length<required)Array.Resize(ref bits,required);
            int connected=0,merged=0;int previousRoot=AchievementLogicalRoot(probe.sourceLogical);achievementRoots.Clear();
            foreach(var component in probe.targetComponents)
            {
                int root=AchievementLogicalRoot(component[0]);if(root==previousRoot)continue;
                connected+=component.Count;if(achievementRoots.Add(root))merged++;
            }
            achievementFreshIds.Clear();int sourceDense=achievementLogicalToDense[probe.sourceLogical];
            foreach(var cell in boardManager.CapturedCells)
            {
                int id=boardManager.LogicalIndex(cell);byte mask=(byte)(1<<(id&7));if((bits[id>>3]&mask)==0){achievementFreshIds.Add(id);bits[id>>3]|=mask;}
                JoinAchievementConnections(sourceDense,achievementLogicalToDense[id]);
            }
            PackAchievementConnections();
            a.capturedBits=Convert.ToBase64String(bits);a.targetMask|=1<<probe.color;
            if(kind>=0){a.powerUses[kind]++;a.powerMask|=1<<kind;a.assisted=true;a.captureChain=a.multiplierChain=0;}
            else
            {
                if(connected==0)a.zeroMoves++;
                a.captureChain=connected>=10?a.captureChain+1:0;
                a.multiplierChain=achievementLastMultiplier==7?a.multiplierChain+1:0;
            }
            a.lastAction=new AchievementAction{sequence=++a.sequence,kind=kind,direct=probe.direct,connected=connected,merged=merged,
                rawConnected=probe.rawConnected,rawMerged=probe.rawMerged,resultingComponent=boardManager.CapturedVoxelCount,revisitedCubes=boardManager.CapturedVoxelCount-achievementFreshIds.Count,
                capturePercent=boardManager.TotalVoxelCount>0?connected*100.0/boardManager.TotalVoxelCount:0,
                multiplier=achievementLastMultiplier,scoreDelta=Math.Max(0,Score-probe.score),scoreAfter=Score,targetColor=probe.color,hintFollowed=kind==-1&&probe.followed,freshCubeIds=achievementFreshIds.ToArray()};
            QueueAchievementAction(a);
            a.peakCapture=Math.Max(a.peakCapture,connected);a.peakMerge=Math.Max(a.peakMerge,merged);
            a.hintPaid=false;a.hintPrimary=Array.Empty<int>();a.hintColor=-1;
        }
        private void RecordAchievementHint()
        {
            var a=achievementAttempt;if(a==null||!a.eligible)return;
            a.assisted=true;a.powerMask|=1<<1;a.powerUses[1]++;
            a.lastAction=new AchievementAction{sequence=++a.sequence,kind=1,scoreAfter=Score};
            QueueAchievementAction(a);
            var hint=PowerUps?.CurrentHint;
            if(hint!=null){a.hintPaid=true;a.hintColor=hint.color;a.hintRevision=boardManager.PowerUpRevision;a.hintPrimary=(int[])hint.primary.Clone();}
        }
        private void QueueAchievementAction(AchievementAttempt a)
        {
            if(a.pendingActions==null)a.pendingActions=new List<AchievementAction>();
            int durable=FloodAchievementService.Instance?.DurableSequence(a.id)??0;
            a.pendingActions.RemoveAll(action=>action.sequence<=durable);a.pendingActions.Add(a.lastAction);
        }
        private AchievementAttempt AchievementCheckpoint()
        {
            if(achievementAttempt==null)return null;
            achievementAttempt.moves=MoveCount;achievementAttempt.score=Score;achievementAttempt.seconds=ElapsedSeconds;
            achievementAttempt.finalColor=CurrentPlayerColor;return achievementAttempt;
        }
        private void SampleAchievementTime()
        {
            if(achievementAttempt==null||!elapsedTimerActive||elapsedTimerSuspended||audioPaused||State!=GameState.Playing||Time.unscaledTime<nextAchievementTimeSample)return;
            nextAchievementTimeSample=Time.unscaledTime+15;FloodAchievementService.Instance?.Engine?.ObserveTime(achievementAttempt,ElapsedSeconds);
        }
    }
}
