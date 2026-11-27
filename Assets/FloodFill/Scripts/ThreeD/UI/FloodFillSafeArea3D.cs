using UnityEngine;

namespace FloodFill.ThreeD
{
    public sealed class FloodFillSafeArea3D : MonoBehaviour
    {
        private Rect last;

        private void Start() => Apply();
        private void OnRectTransformDimensionsChange() => Apply();

        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            if (area == last) return;
            last = area;
            RectTransform rect = transform as RectTransform;
            if (rect == null) return;
            Vector2 min = area.min;
            Vector2 max = area.max;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
