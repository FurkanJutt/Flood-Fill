using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.ThreeD
{
    // Animate decoration, never the Button's raycast rectangle or interactable state.
    [DisallowMultipleComponent]
    public sealed class FloodButtonFeedback3D : MonoBehaviour
    {
        private readonly List<Transform> visuals=new List<Transform>();
        private readonly List<Vector3> scales=new List<Vector3>();
        private bool cached;
        public bool IsAnimating
        {
            get{foreach(var visual in visuals)if(visual!=null&&DOTween.IsTweening(visual))return true;return false;}
        }
        private void Cache()
        {
            if(cached)return;cached=true;
            for(int i=0;i<transform.childCount;i++)
            {
                var child=transform.GetChild(i);var graphic=child.GetComponent<Graphic>();
                if(graphic==null||graphic.raycastTarget||child.GetComponent<Selectable>()!=null)continue;
                visuals.Add(child);scales.Add(child.localScale);
            }
        }
        public void Play(float amount,float duration)
        {
            Cache();ResetVisuals();if(!isActiveAndEnabled)return;
            for(int i=0;i<visuals.Count;i++)if(visuals[i]!=null)
                visuals[i].DOPunchScale(scales[i]*amount,duration,4,.5f).SetUpdate(true);
        }
        private void ResetVisuals()
        {for(int i=0;i<visuals.Count;i++)if(visuals[i]!=null){visuals[i].DOKill();visuals[i].localScale=scales[i];}}
        private void OnDisable()=>ResetVisuals();
    }
}
