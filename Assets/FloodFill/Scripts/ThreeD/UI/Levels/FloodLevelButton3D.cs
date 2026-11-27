using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.ThreeD
{
    public sealed class FloodLevelButton3D : MonoBehaviour
    {
        public Button button;
        public Image background, lockIcon;
        public TMP_Text number;
        public Image[] stars, goalDiamonds;
        public Sprite available, current, locked, goldStar, grayStar, filledGoalDiamond, unfilledGoalDiamond;
        private FloodLevelSelection3D owner;
        private int level;
        private void Awake() => button.onClick.AddListener(Select);
        private void Select() => owner.StartLevel(level);
        public void Bind(FloodLevelSelection3D controller, int value)
        {
            owner = controller; level = value;
            var result = FloodLevelProgression3D.Get(owner.Mode, owner.Difficulty, value);
            bool unlocked = FloodLevelProgression3D.Unlocked(owner.Mode, owner.Difficulty, value);
            bool next = value == FloodLevelProgression3D.Next(owner.Mode, owner.Difficulty);
            number.text = value.ToString(); number.color = unlocked ? Color.white : new Color(.65f,.71f,.81f);
            button.interactable = unlocked;
            background.sprite = !unlocked ? locked : next ? current : result.completed ? available : locked;
            background.rectTransform.anchorMin = Vector2.zero; background.rectTransform.anchorMax = Vector2.one;
            float glow = next && unlocked ? 15 : 0;
            background.rectTransform.offsetMin = Vector2.one * -glow; background.rectTransform.offsetMax = Vector2.one * glow;
            lockIcon.gameObject.SetActive(!unlocked);
            int secondaryCount = FloodLevelProgression3D.SecondaryStars(owner.Mode, owner.Difficulty, value);
            for (int i = 0; i < stars.Length; i++)
            {
                bool earned = i < result.stars;
                stars[i].gameObject.SetActive(unlocked);
                stars[i].sprite = earned ? goldStar : grayStar;
                stars[i].color = Color.white;
                stars[i].rectTransform.sizeDelta = Vector2.one * (earned ? 41.954f : 30f);
            }
            if (goalDiamonds != null)
                for (int i = 0; i < goalDiamonds.Length; i++)
                {
                    goalDiamonds[i].gameObject.SetActive(unlocked);
                    goalDiamonds[i].sprite = i < secondaryCount ? filledGoalDiamond : unfilledGoalDiamond;
                    goalDiamonds[i].color = i < secondaryCount ? Color.white : new Color32(255, 255, 255, 165);
                }
        }
    }
}
