using UnityEngine;

namespace FloodFill.ThreeD
{
    /// <summary>Fits the authored content frame inside its screen, preserving child RectTransforms.</summary>
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public sealed class FloodFillUIFit3D : MonoBehaviour
    {
        private void OnEnable() => Fit();
        private void LateUpdate() => Fit();
        private void Fit()
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null || rect.rect.width <= 0 || rect.rect.height <= 0) return;
            float scale = Mathf.Min(parent.rect.width / rect.rect.width, parent.rect.height / rect.rect.height);
            if (scale <= 0) return;
            Vector3 target = Vector3.one * scale;
            if ((rect.localScale - target).sqrMagnitude > .000001f) rect.localScale = target;
        }
    }
}
