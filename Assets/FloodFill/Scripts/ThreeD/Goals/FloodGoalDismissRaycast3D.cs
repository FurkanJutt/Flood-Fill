using UnityEngine;
namespace FloodFill.ThreeD.Goals
{
    // Transparent only to the dismissal tap; never a permanent full-screen input blocker.
    public sealed class FloodGoalDismissRaycast3D : MonoBehaviour, ICanvasRaycastFilter
    {
        public bool IsRaycastLocationValid(Vector2 position,Camera eventCamera)=>FloodSecondaryGoalsUI3D.ConsumedPointerThisFrame;
    }
}
