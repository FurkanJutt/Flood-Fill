using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.ThreeD.Achievements
{
    [RequireComponent(typeof(Toggle))]
    public sealed class FloodAchievementDropdownItem : MonoBehaviour
    {
        public TMP_Text label;
        public Color normalColor = Color.white;
        public Color selectedColor = new Color(0f, .85f, 1f, 1f);
        private Toggle toggle;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(Refresh);
        }

        private void OnEnable() => Refresh(GetComponent<Toggle>().isOn);
        private void Refresh(bool selected)
        {
            if (label == null) return;
            label.color = selected ? selectedColor : normalColor;
            label.fontStyle = selected ? label.fontStyle | FontStyles.Bold : label.fontStyle & ~FontStyles.Bold;
        }

        private void OnDestroy()
        {
            if (toggle != null) toggle.onValueChanged.RemoveListener(Refresh);
        }
    }
}
