using DG.Tweening;
using FloodFill.ThreeD.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.ThreeD
{
    public sealed class FloodLevelSelection3D : MonoBehaviour
    {
        public Button home, previous, next;
        public Button[] modes, difficulties, dots;
        public Image[] modeBackgrounds, difficultyBackgrounds, dotImages;
        public Sprite tabNormal, tabSelected, dotNormal, dotSelected;
        public TMP_Text completed, percentage;
        public Image fill;
        public FloodLevelButton3D levelPrefab;
        public RectTransform grid;
        public FloodLevelRules3D rules;
        public int Mode { get; private set; }
        public int Difficulty { get; private set; }
        public int Page { get; private set; }
        private FloodFillMobileUI3D owner;
        private readonly int[] pages = new int[12];
        private FloodLevelButton3D[] tiles;
        private void Awake()
        {
            owner = GetComponentInParent<FloodFillMobileUI3D>();
            for (int i = 0; i < pages.Length; i++) pages[i] = -1;
            Mode = rules != null ? rules.defaultMode : 0; Difficulty = rules != null ? rules.defaultDifficulty : 0;
            home.onClick.AddListener(() => owner.LevelSelectionHome());
            previous.onClick.AddListener(() => SetPage(Page - 1)); next.onClick.AddListener(() => SetPage(Page + 1));
            for (int i = 0; i < modes.Length; i++) { int index = i; modes[i].onClick.AddListener(() => Select(index, Difficulty)); }
            for (int i = 0; i < difficulties.Length; i++) { int index = i; difficulties[i].onClick.AddListener(() => Select(Mode, index)); }
            for (int i = 0; i < dots.Length; i++) { int index = i; dots[i].onClick.AddListener(() => SetPage(index)); }
            tiles = grid.GetComponentsInChildren<FloodLevelButton3D>(true);
            if (tiles.Length == 0) { tiles = new FloodLevelButton3D[30]; for (int i = 0; i < 30; i++) tiles[i] = Instantiate(levelPrefab, grid); }
        }
        private void OnEnable()
        {
            if (tiles == null) return;
            foreach (Button b in GetComponentsInChildren<Button>(true)) FloodAudioManager.Instance?.BindControl(b, b.name);
            Select(Mode, Difficulty);
        }
        public void Open(int mode, int difficulty)
        {
            if (tiles != null) Select(mode, difficulty);
        }
        public void Select(int mode, int difficulty)
        {
            Mode = Mathf.Clamp(mode,0,2); Difficulty = Mathf.Clamp(difficulty,0,3);
            int saved = pages[Mode * 4 + Difficulty];
            Page = saved >= 0 ? saved : (FloodLevelProgression3D.Next(Mode,Difficulty)-1)/30;
            Refresh();
        }
        public void SetPage(int page)
        {
            Page = Mathf.Clamp(page,0,3); pages[Mode*4+Difficulty] = Page; Refresh();
            grid.DOKill(); grid.localScale = Vector3.one * .98f; grid.DOScale(1,.16f).SetUpdate(true);
        }
        public void StartLevel(int level)
        {
            if (!FloodLevelProgression3D.Unlocked(Mode,Difficulty,level)) return;
            owner.StartSelectedLevel(Mode,Difficulty,level);
        }
        public void Refresh()
        {
            for (int i=0;i<3;i++) modeBackgrounds[i].sprite = i==Mode ? tabSelected : tabNormal;
            for (int i=0;i<4;i++) { difficultyBackgrounds[i].sprite = i==Difficulty ? tabSelected : tabNormal; dotImages[i].sprite = i==Page ? dotSelected : dotNormal; }
            int count = FloodLevelProgression3D.Completed(Mode,Difficulty);
            completed.text = count + " / 100 COMPLETED"; percentage.text = count + "%";
            fill.DOKill(); fill.color = rules != null ? rules.progressColor : Color.cyan;
            fill.DOFillAmount(count/100f,.3f).SetUpdate(true);
            previous.interactable = Page>0; next.interactable = Page<3;
            for (int i=0;i<30;i++) { int level=Page*30+i+1; tiles[i].gameObject.SetActive(level<=100); if(level<=100) tiles[i].Bind(this,level); }
        }
        private void OnDestroy() { fill.DOKill(); grid.DOKill(); }
    }
}
