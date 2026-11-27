using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.ThreeD
{
    /// <summary>Authored state visuals with optional per-state dimensions.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class FloodFillUIState3D : MonoBehaviour
    {
        public Sprite normalSprite;
        public bool normalOverrideSize;
        public Vector2 normalSize = new Vector2(100, 100);
        public Sprite selectedSprite;
        public bool selectedOverrideSize;
        public Vector2 selectedSize = new Vector2(100, 100);
        public Color normalColor = Color.white;
        public Color selectedColor = Color.white;
        public Sprite disabledSprite;
        public Color disabledColor = Color.white;
        private bool selectedState;
        private bool sizeOverridden;
        private Vector2 originalSizeDelta;

        private void Reset()
        {
            var rect = (RectTransform)transform;
            normalSize = selectedSize = rect.rect.size;
        }

        public void SwapSprite()
        {
            var image = GetComponent<Image>();
            bool selected = normalSprite != selectedSprite ? image.sprite == selectedSprite : selectedState;
            Apply(!selected);
        }

        public void Apply(bool selected) => ApplyState(selected, false);

        public void ApplyState(bool selected, bool disabled)
        {
            if (disabled) selected = false;
            selectedState = selected;
            Image image = GetComponent<Image>();
            image.sprite = disabled && disabledSprite != null ? disabledSprite : selected ? selectedSprite : normalSprite;
            image.color = disabled ? disabledColor : selected ? selectedColor : normalColor;
            var rect = image.rectTransform;
            if (selected ? selectedOverrideSize : normalOverrideSize)
            {
                if (!sizeOverridden) originalSizeDelta = rect.sizeDelta;
                sizeOverridden = true;
                Vector2 size = selected ? selectedSize : normalSize;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0, size.x));
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0, size.y));
            }
            else if (sizeOverridden)
            {
                rect.sizeDelta = originalSizeDelta;
                sizeOverridden = false;
            }
        }
    }
}
