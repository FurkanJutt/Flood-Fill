using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using FloodFill.ThreeD.PowerUps;
namespace FloodFill.ThreeD
{
    public struct FloodHintView3D
    {
        public float yaw,pitch,distance,angle,visibleFraction;
        public Vector3 center;
        public VoxelCell3D representative;
    }
    public sealed partial class OrbitCameraController3D
    {
        public Camera HintCamera=>targetCamera;
        public bool IsHintFocusMoving {get;private set;}
        public event Action HintFocusInterrupted;
        private FloodHintView3D hintView;
        private float hintStartYaw,hintStartPitch,hintStartDistance,hintElapsed,hintDuration,hintSpeed;
        private Vector3 hintStartCenter;
        private Ease hintEase;
        public void CancelHintFocus()
        {if(!IsHintFocusMoving)return;IsHintFocusMoving=false;HintFocusInterrupted?.Invoke();}
        public void FocusHint(FloodHintView3D view,FloodPowerUpSettings3D settings)
        {
            CancelHintFocus();hintView=view;
            if(!settings.enableAutomaticHintRotation)return;
            hintStartYaw=yaw;hintStartPitch=pitch;hintStartDistance=distance;hintStartCenter=targetPosition;
            hintDuration=Mathf.Max(settings.rotationDuration,view.angle/Mathf.Max(30,settings.maximumRotationSpeed));hintElapsed=0;hintEase=settings.rotationEasing;hintSpeed=Mathf.Max(30,settings.maximumRotationSpeed);
            IsHintFocusMoving=view.angle>settings.minimumRotationThreshold||(view.center-targetPosition).sqrMagnitude>.01f||Mathf.Abs(view.distance-distance)>.01f;
        }
        private void TickHintFocus(float dt)
        {
            if(!IsHintFocusMoving)return;
            hintElapsed+=dt;float t=Mathf.Clamp01(hintElapsed/Mathf.Max(.1f,hintDuration));float e=DOVirtual.EasedValue(0,1,t,hintEase);
            float desiredYaw=Mathf.LerpAngle(hintStartYaw,hintView.yaw,e),desiredPitch=Mathf.Clamp(Mathf.Lerp(hintStartPitch,hintView.pitch,e),minimumPitch,maximumPitch);
            Vector2 delta=new Vector2(Mathf.DeltaAngle(yaw,desiredYaw),desiredPitch-pitch);float limit=hintSpeed*dt;
            if(delta.magnitude>limit&&delta.sqrMagnitude>0)delta=delta.normalized*limit;
            yaw+=delta.x;pitch=Mathf.Clamp(pitch+delta.y,minimumPitch,maximumPitch);
            targetPosition=Vector3.LerpUnclamped(hintStartCenter,hintView.center,e);distance=Mathf.Lerp(hintStartDistance,hintView.distance,e);
            if(t>=1&&Mathf.Abs(Mathf.DeltaAngle(yaw,hintView.yaw))<.1f&&Mathf.Abs(pitch-hintView.pitch)<.1f)IsHintFocusMoving=false;
        }
        private struct FaceSample {public VoxelCell3D cell;public Vector3 point,normal;}
        private readonly List<FaceSample> hintSamples=new List<FaceSample>(96);
        private static readonly Vector3Int[] HintFaces={Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};
        public bool TryFindHintView(VoxelBoardManager3D board,IReadOnlyList<VoxelCell3D> region,FloodPowerUpSettings3D settings,out FloodHintView3D view)
        {
            view=default;if(targetCamera==null||region.Count==0)return false;
            hintSamples.Clear();int stride=Mathf.Max(1,region.Count/16);
            for(int i=0;i<region.Count&&hintSamples.Count<96;i+=stride)
            {
                var cell=region[i];
                foreach(var face in HintFaces)
                {
                    if(board.IsActiveCoordinate(cell.X+face.x,cell.Y+face.y,cell.Z+face.z))continue;
                    Vector3 normal=board.PowerUpBoardRoot.TransformDirection((Vector3)face).normalized;
                    hintSamples.Add(new FaceSample {cell=cell,point=cell.transform.position+normal*board.PowerUpVoxelWidth*board.PowerUpBoardRoot.lossyScale.x*.48f,normal=normal});
                }
            }
            if(hintSamples.Count==0)return false;
            float bestScore=float.NegativeInfinity;bool found=false;FloodHintView3D bestView=default;
            Evaluate(yaw,pitch,targetPosition,distance,true);
            if(found&&bestView.visibleFraction>=settings.visibilityThreshold){view=bestView;return true;}
            if(!settings.enableAutomaticHintRotation){view=bestView;return found;}
            int centers=settings.allowCameraPanAdjustment?2:1;
            for(int center=0;center<centers;center++)
            {
                Vector3 point=center==0?targetPosition:framedBoundsCenter;
                float candidateDistance=distance;
                if(settings.allowCameraZoomAdjustment)candidateDistance=Mathf.Max(candidateDistance,defaultDistance);
                for(int p=0;p<3;p++)for(int y=0;y<8;y++)Evaluate(y*45,Mathf.Clamp(p==0?0:p==1?70:-70,minimumPitch,maximumPitch),point,candidateDistance,false);
            }
            view=bestView;return found;
            void Evaluate(float candidateYaw,float candidatePitch,Vector3 center,float dist,bool current)
            {
                Quaternion rotation=Quaternion.Euler(0,candidateYaw,0)*Quaternion.Euler(candidatePitch,0,0);
                Vector3 origin=center+rotation*Vector3.back*dist;
                float halfY=Mathf.Tan(targetCamera.fieldOfView*.5f*Mathf.Deg2Rad),halfX=halfY*targetCamera.aspect;
                int visible=0;VoxelCell3D representative=null;float closest=float.PositiveInfinity;
                foreach(var sample in hintSamples)
                {
                    if(Vector3.Dot(sample.normal,(origin-sample.point).normalized)<.12f)continue;
                    Vector3 relative=Quaternion.Inverse(rotation)*(sample.point-origin);
                    if(relative.z<=targetCamera.nearClipPlane)continue;
                    Vector2 screen=new Vector2(.5f+relative.x/(relative.z*halfX)*.5f,.5f+relative.y/(relative.z*halfY)*.5f);
                    // Keep the selectable representative between the top stats/goals and bottom power-up/palette strip.
                    if(screen.x<.1f||screen.x>.9f||screen.y<.27f||screen.y>.73f)continue;
                    if(!HintLineClear(board,origin,sample.cell.transform.position,sample.cell))continue;
                    visible++;float off=(screen-settings.targetScreenPosition).sqrMagnitude;
                    if(off<closest){closest=off;representative=sample.cell;}
                }
                if(representative==null)return;
                float angle=Mathf.Sqrt(Mathf.Pow(Mathf.DeltaAngle(yaw,candidateYaw),2)+Mathf.Pow(candidatePitch-pitch,2));
                float fraction=visible/(float)Mathf.Max(1,hintSamples.Count);
                float score=visible-angle*.025f-(center-targetPosition).magnitude*.1f-closest*3;
                if(score<=bestScore)return;
                bestScore=score;found=true;
                bestView=new FloodHintView3D {yaw=candidateYaw,pitch=candidatePitch,center=center,distance=dist,representative=representative,angle=angle,visibleFraction=fraction};
            }
        }
        // Grid traversal is bounded by the ray crossing count, not the number of cubes on the board.
        private static bool HintLineClear(VoxelBoardManager3D board,Vector3 origin,Vector3 target,VoxelCell3D intended)
        {
            var root=board.PowerUpBoardRoot;float spacing=board.PowerUpSpacing;
            Vector3 offset=board.ActiveBounds.Center;
            Vector3 from=root.InverseTransformPoint(origin)/spacing+offset+Vector3.one*.5f;
            Vector3 to=root.InverseTransformPoint(target)/spacing+offset+Vector3.one*.5f;
            Vector3 dir=to-from;
            float low=0,high=1;
            for(int axis=0;axis<3;axis++)
            {
                float extent=axis==0?board.Width:axis==1?board.Height:board.Depth;
                if(Mathf.Abs(dir[axis])<.00001f){if(from[axis]<0||from[axis]>extent)return false;continue;}
                float a=(0-from[axis])/dir[axis],b=(extent-from[axis])/dir[axis];if(a>b){float swap=a;a=b;b=swap;}
                low=Mathf.Max(low,a);high=Mathf.Min(high,b);if(low>high)return false;
            }
            Vector3 point=from+dir*(low+.00001f);var cell=new Vector3Int(Mathf.FloorToInt(point.x),Mathf.FloorToInt(point.y),Mathf.FloorToInt(point.z));
            for(int steps=0;steps<board.Width+board.Height+board.Depth+8;steps++)
            {
                var occupied=board.GetCell(cell.x,cell.y,cell.z);
                if(occupied!=null)return occupied==intended;
                float next=float.PositiveInfinity;int selected=-1;
                for(int axis=0;axis<3;axis++)
                {
                    if(Mathf.Abs(dir[axis])<.00001f)continue;
                    float boundary=dir[axis]>0?cell[axis]+1:cell[axis];float t=(boundary-from[axis])/dir[axis];
                    if(t<next){next=t;selected=axis;}
                }
                if(selected<0||next>high+.0001f)return false;
                cell[selected]+=dir[selected]>0?1:-1;
            }
            return false;
        }
        public bool IsHintSelectable(VoxelCell3D cell)
        {
            if(cell==null||targetCamera==null)return false;
            Vector3 viewport=targetCamera.WorldToViewportPoint(cell.transform.position);
            if(viewport.z<=0||viewport.x<.05f||viewport.x>.95f||viewport.y<.23f||viewport.y>.78f)return false;
            var ray=targetCamera.ViewportPointToRay(viewport);
            return Physics.Raycast(ray,out var hit,10000)&&hit.collider.GetComponentInParent<VoxelCell3D>()==cell;
        }
    }
}
