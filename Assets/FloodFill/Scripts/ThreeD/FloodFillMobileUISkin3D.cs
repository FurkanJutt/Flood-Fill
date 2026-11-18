using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.ThreeD
{
    public sealed partial class FloodFillMobileUI3D
    {
        private FloodUISkin uiSkin;
        private GameObject victoryCrown;

        private bool SkinImage(Image image, string key, bool sliced = true)
        {
            Sprite sprite = uiSkin != null ? uiSkin.Find(key) : null;
            if (sprite == null) return false;
            image.sprite = sprite;
            image.color = Color.white;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = !sliced;
            // Match border thickness to control height instead of source-sheet pixels.
            float height = image.rectTransform.sizeDelta.y;
            image.pixelsPerUnitMultiplier = Mathf.Max(1f, sprite.rect.height / Mathf.Max(1f, height));
            if (height > 300f)
                image.pixelsPerUnitMultiplier = Mathf.Max(image.pixelsPerUnitMultiplier, sprite.rect.width / Mathf.Max(1f, image.rectTransform.sizeDelta.x));
            return true;
        }

        private void SkinDecoration(string name, Transform parent, string key, Vector2 position, Vector2 size)
        {
            if (uiSkin == null || uiSkin.Find(key) == null) return;
            Image image = MakeImage(name, parent, position, size, Color.white).GetComponent<Image>();
            SkinImage(image, key, false);
            image.raycastTarget = false;
        }

        private void SkinButton(Button button, string label, Color originalColor)
        {
            string parent = button.transform.parent.name;
            string key = originalColor == Accent ? "Primary" : "Secondary";
            if (parent == "PauseMenu") key = originalColor == Accent ? "PausePrimary" : "PauseSecondary";
            if (parent == "GameResults") key = originalColor == Accent ? "ResultPrimary" : "ResultSecondary";
            if (parent == "GameplayHUD") key = "HudButton";
            SkinImage(button.image, key);
            string icon = null;
            switch (label)
            {
                case "PLAY": case "START GAME": case "NEXT LEVEL": icon = "Play"; break;
                case "CONTINUE": icon = "Continue"; break;
                case "CUSTOM": icon = "Custom"; break;
                case "HOW TO PLAY": icon = "Help"; break;
                case "SETTINGS": icon = "Settings"; break;
                case "QUIT": icon = "Quit"; break;
                case "RESUME": icon = "Resume"; break;
                case "RESTART": case "PLAY AGAIN": case "TRY AGAIN": icon = "Restart"; break;
                case "BACK": icon = "Back"; break;
                case "HOME": case "MAIN MENU": icon = "Home"; break;
                case "PAUSE": icon = "Pause"; break;
            }
            if (icon == null || uiSkin == null || uiSkin.Find(icon) == null) return;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>();
            Vector2 size = button.GetComponent<RectTransform>().sizeDelta;
            float iconSize = Mathf.Min(42f, size.y * .43f);
            if (parent == "GameplayHUD")
            {
                text.enabled = false;
                SkinDecoration("Icon", button.transform, icon, Vector2.zero, Vector2.one * iconSize);
            }
            else
            {
                // Center icon and live label as one group, including narrower menu layouts.
                var group = MakeRect("ButtonContent", button.transform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, size);
                text.transform.SetParent(group.transform, false);
                var layout = group.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 16f;
                layout.childControlWidth = true; layout.childControlHeight = true;
                layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
                SkinDecoration("Icon", group.transform, icon, Vector2.zero, Vector2.one * iconSize);
                var image = group.transform.Find("Icon");
                image.SetAsFirstSibling();
                var element = image.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = iconSize; element.preferredHeight = iconSize;
                var labelLayout = text.gameObject.AddComponent<LayoutElement>();
                labelLayout.preferredHeight = size.y - 12;
                labelLayout.flexibleWidth = 0;
            }
        }

        private void SkinChoice(Button button, bool selected)
        {
            string key = sizeButtons.Contains(button) ? (selected ? "SelectedTile" : "Tile") : (selected ? "Primary" : "Secondary");
            if (!SkinImage(button.image, key))
                button.image.color = selected ? Accent : Panel;
        }

        private void SkinSwitch(Toggle toggle)
        {
            if (uiSkin == null) return;
            Image image = toggle.targetGraphic as Image;
            if (image == null || !SkinImage(image, toggle.isOn ? "SwitchOn" : "SwitchOff", false)) return;
            image.rectTransform.sizeDelta = new Vector2(104, 57);
            if (toggle.graphic != null) toggle.graphic.gameObject.SetActive(false);
            toggle.graphic = null;
            // SetIsOnWithoutNotify is used when opening settings; refresh those explicitly too.
        }
    }
}
