using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.ThreeD
{
    public enum FloodFillUIAction
    {
        Play, Continue, Setup, Help, Settings, Quit, StartGame, Menu, Pause,
        Resume, Restart, ReturnToMenu, Result, FruitDropdown, Mode, Size,
        Difficulty, Fruit, Color, MasterVolume, MusicVolume, SfxVolume,
        MusicEnabled, SfxEnabled, ShowCurrentBlock, Levels,
        Home, ConfirmHome, CancelHome, SettingsBack,
        PromptSavedLevelRestart, ConfirmSavedLevelRestart, CancelSavedLevelRestart, Achievements
    }

    /// <summary>Persistent prefab event target. References survive renaming and reparenting.</summary>
    public sealed class FloodFillUIAction3D : MonoBehaviour
    {
        public FloodFillUIAction action;
        public int index;
        public string audioLabel;
        public void Invoke()
        {
            if (!Application.isPlaying) return;
            var owner = GetComponentInParent<FloodFillMobileUI3D>();
            if (owner == null) return;
            owner.ExecuteAuthoredAction(action, index);
            FloodTouchInput3D.PressFeedback(transform);
        }
        public void SetValue(float value)
        {
            if (Application.isPlaying) GetComponentInParent<FloodFillMobileUI3D>()?.ExecuteAuthoredValue(action, value);
        }
        public void SetEnabled(bool value)
        {
            if (Application.isPlaying) GetComponentInParent<FloodFillMobileUI3D>()?.ExecuteAuthoredToggle(action, value, GetComponent<Toggle>());
        }
    }
}
