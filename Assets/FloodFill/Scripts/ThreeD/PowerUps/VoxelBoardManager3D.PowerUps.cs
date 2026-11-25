using System;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using FloodFill.ThreeD.PowerUps;
namespace FloodFill.ThreeD
{
    public sealed partial class VoxelBoardManager3D
    {
        public event Action PowerUpBoardChanged;
        public long PowerUpRevision {get;private set;}
        public float VisualsSettledAt {get;private set;}
        public Transform PowerUpBoardRoot=>boardRoot;
        public float PowerUpVoxelWidth=>voxelSize;
        public float PowerUpSpacing=>voxelSize+voxelGap;
        public IReadOnlyList<VoxelCell3D> ActiveCells=>allVoxels;
        public IReadOnlyList<VoxelCell3D> CapturedCells=>capturedVoxels;
        public Color PaletteColor(int color)=>palette[Mathf.Clamp(color,0,palette.Length-1)];
        public int PaletteCount=>palette.Length;
        public int LogicalIndex(VoxelCell3D cell)=>cell.X+width*(cell.Y+height*cell.Z);
        public VoxelCell3D CellAtLogical(int index)=>GetCell(index%width,index/width%height,index/(width*height));
        private readonly List<VoxelCell3D> powerEditCells=new List<VoxelCell3D>(7);
        private readonly Color[] powerOldColors=new Color[7];
        private void MarkPowerUpBoardChanged(){PowerUpRevision++;PowerUpBoardChanged?.Invoke();}
        public bool IsValidPowerTarget(VoxelCell3D cell)=>cell!=null&&cell.gameObject.activeInHierarchy&&GetCell(cell.X,cell.Y,cell.Z)==cell;
        public int GetPowerTargets(VoxelCell3D center,bool blast,List<VoxelCell3D> output)
        {
            output.Clear();if(!IsValidPowerTarget(center))return 0;output.Add(center);
            if(blast)foreach(var d in Directions){var next=GetCell(center.X+d.x,center.Y+d.y,center.Z+d.z);if(next!=null)output.Add(next);}
            return output.Count;
        }
        public bool TryPowerRecolor(VoxelCell3D center,int color,bool blast,FloodPowerUpSettings3D settings)
        {
            if(color<0||color>=palette.Length||GetPowerTargets(center,blast,powerEditCells)==0)return false;
            bool changed=false;
            for(int i=0;i<powerEditCells.Count;i++){var c=powerEditCells[i];powerOldColors[i]=palette[c.ColorIndex];changed|=c.ColorIndex!=color;}
            if(!changed)return false;
            // First commit every logical color, then rebuild the active component once. Splits are not assumed away.
            for(int i=0;i<powerEditCells.Count;i++)if(powerEditCells[i].ColorIndex!=color)powerEditCells[i].SetColor(color,palette[color]);
            LastRecolorAnimationDuration=0;
            RecalculateCapturedRegion(center,false);
            var definition=blast?settings.colorBlast:settings.colorSwap;
            for(int i=0;i<powerEditCells.Count;i++)
            {
                if(powerOldColors[i]==palette[color])continue;
                float delay=blast&&i>0?settings.neighborWaveDelay:0;
                float duration=blast&&i==0?settings.centerCubeTransitionDuration:definition.animationDuration-(blast?settings.neighborWaveDelay:0);
                duration=Mathf.Max(.05f,duration);
                powerEditCells[i].AnimatePowerVisual(powerOldColors[i],palette[color],duration,delay,definition.animationEasing,definition.enableScalePop,definition.scalePopIntensity);
                LastRecolorAnimationDuration=Mathf.Max(LastRecolorAnimationDuration,delay+duration);
            }
            VisualsSettledAt=Time.time+LastRecolorAnimationDuration;
            MarkPowerUpBoardChanged();return true;
        }
        public void CollectRegion(VoxelCell3D origin,List<VoxelCell3D> output)
        {
            output.Clear();if(!IsValidPowerTarget(origin))return;
            var visited=new HashSet<VoxelCell3D>();visited.Add(origin);output.Add(origin);
            for(int head=0;head<output.Count;head++)foreach(var d in Directions)
            {var c=output[head];var n=GetCell(c.X+d.x,c.Y+d.y,c.Z+d.z);if(n!=null&&n.ColorIndex==origin.ColorIndex&&visited.Add(n))output.Add(n);}
        }
        public void PreviewPowerVisual(VoxelCell3D center,bool blast,FloodPowerUpSettings3D settings)
        {
            if(GetPowerTargets(center,blast,powerEditCells)==0)return;
            var def=blast?settings.colorBlast:settings.colorSwap;
            for(int i=0;i<powerEditCells.Count;i++)
            {
                var c=powerEditCells[i];Color steady=palette[c.ColorIndex],preview=palette[(c.ColorIndex+1)%palette.Length];
                c.AnimatePowerPreview(steady,preview,def.animationDuration,blast&&i>0?settings.neighborWaveDelay:0,def.animationEasing,def.enableScalePop,def.scalePopIntensity);
            }
        }
        public void FinishPowerVisuals(){VisualsSettledAt=Time.time;}
        public void ResetPowerVisuals(){foreach(var c in allVoxels)c.SetColor(c.ColorIndex,palette[c.ColorIndex]);FinishPowerVisuals();}
    }
    public sealed partial class VoxelCell3D
    {
        public void AnimatePowerVisual(Color from,Color to,float duration,float delay,DG.Tweening.Ease easing,bool pop,float amount)
        {
            EnsureRenderers();colorWaveSequence?.Kill();
            if(visual!=null){visual.DOKill();visual.localScale=visualRestingScale;}
            ApplyDisplayColor(from);
            var sequence=DG.Tweening.DOTween.Sequence().AppendInterval(Mathf.Max(0,delay));
            sequence.Append(DG.Tweening.DOVirtual.Color(from,to,duration,ApplyDisplayColor).SetEase(easing));
            if(pop&&visual!=null)sequence.Join(visual.DOPunchScale(visualRestingScale*Mathf.Clamp(amount,0,.15f),duration,3,.5f));
            sequence.OnComplete(()=>{ApplySteadyColor(to);if(visual!=null)visual.localScale=visualRestingScale;colorWaveSequence=null;});
            colorWaveSequence=sequence;
        }
        public void AnimatePowerPreview(Color steady,Color preview,float duration,float delay,DG.Tweening.Ease easing,bool pop,float amount)
        {
            AnimatePowerVisual(steady,preview,duration*.5f,delay,easing,pop,amount);
            // Append a visual-only return; the logical ColorIndex never changes.
            colorWaveSequence.OnComplete(null).Append(DG.Tweening.DOVirtual.Color(preview,steady,duration*.5f,ApplyDisplayColor));
            colorWaveSequence.OnComplete(()=>{ApplySteadyColor(steady);if(visual!=null)visual.localScale=visualRestingScale;colorWaveSequence=null;});
        }
    }
}
