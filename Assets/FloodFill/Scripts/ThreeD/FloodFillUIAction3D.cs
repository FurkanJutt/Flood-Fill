using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.ThreeD
{
    public enum FloodFillUIAction
    {
        Play, Continue, Setup, Help, Settings, Quit, StartGame, Menu, Pause,
        Resume, Restart, ReturnToMenu, Result, FruitDropdown, Mode, Size,
        Difficulty, Fruit, Color, MasterVolume, MusicVolume, SfxVolume,
        MusicEnabled, SfxEnabled, ShowCurrentBlock, Levels
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
            transform.DOPunchScale(Vector3.one * .045f, .18f, 6);
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
