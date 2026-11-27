using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.ThreeD
{
    /// <summary>Gives a padded button sprite a layout slot matching its visible body.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(LayoutElement))]
    public sealed class FloodFillResultButtonSlot3D : MonoBehaviour
    {
        public Image buttonImage;
        public Sprite measuredSprite;
        public Vector2 bodyPixelY;
        private LayoutElement element;
        public Vector2 BodyLocalY
        {
            get
            {
                if (buttonImage == null || buttonImage.sprite == null) return Vector2.zero;
                Rect rect = buttonImage.GetPixelAdjustedRect();
                float height = buttonImage.sprite.rect.height;
                Vector2 pixels = buttonImage.sprite == measuredSprite ? bodyPixelY : new Vector2(0, height);
                return new Vector2(rect.yMin + MapY(pixels.x, rect.height), rect.yMin + MapY(pixels.y, rect.height));
            }
        }
        private float MapY(float pixel, float height)
        {
            Sprite sprite = buttonImage.sprite;
            float pixels = sprite.rect.height;
            if (buttonImage.type != Image.Type.Sliced) return pixel / pixels * height;
            Vector4 border = sprite.border;
            float ppu = Mathf.Max(.001f, buttonImage.pixelsPerUnit * buttonImage.pixelsPerUnitMultiplier);
            float bottom = border.y / ppu, top = border.w / ppu;
            float scale = bottom + top > height ? height / (bottom + top) : 1;
            bottom *= scale; top *= scale;
            if (border.y > 0 && pixel <= border.y) return pixel / border.y * bottom;
            if (border.w > 0 && pixel >= pixels - border.w) return height - (pixels - pixel) / border.w * top;
            return bottom + (pixel - border.y) / Mathf.Max(.001f, pixels - border.y - border.w) * Mathf.Max(0, height - bottom - top);
        }
        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (buttonImage == null || buttonImage.sprite == null) return;
            if (element == null) element = GetComponent<LayoutElement>();
            RectTransform button = buttonImage.rectTransform;
            Vector2 center = new Vector2(.5f,.5f);
            if (button.anchorMin != center) button.anchorMin = center;
            if (button.anchorMax != center) button.anchorMax = center;
            if (button.pivot != center) button.pivot = center;
            Vector2 body = BodyLocalY;
            float height = Mathf.Max(1, body.y - body.x);
            if (!Mathf.Approximately(element.preferredHeight,height)) element.preferredHeight = height;
            if (!Mathf.Approximately(element.minHeight,height)) element.minHeight = height;
            element.flexibleHeight = 0; element.layoutPriority = 2;
            Vector2 offset = new Vector2(0,-(body.x + body.y)*.5f);
            if ((button.anchoredPosition-offset).sqrMagnitude>.00001f) button.anchoredPosition=offset;
        }
        public void SetVisible(bool visible)
        {
            if (buttonImage != null) buttonImage.gameObject.SetActive(visible);
            gameObject.SetActive(visible);
        }
    }
}
