#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text.RegularExpressions;
using FloodFill.ThreeD.Audio;
using TMPro;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.ThreeD
{
    public sealed partial class FloodFillMobileUI3D
    {
        public GameObject[] EditorScreens => new[] { menu, setup, howTo, settings, hud, pause, results, loading, homeConfirmation, victoryPopup != null ? victoryPopup.gameObject : null, levelSelection != null ? levelSelection.gameObject : null }.Where(screen => screen != null).ToArray();

        /// <summary>Explicit one-time editor construction. Runtime only uses the saved references.</summary>
        public void BuildEditableLayoutForEditor()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Generate UI outside Play Mode.");
            if (safeRoot != null) throw new InvalidOperationException("This Canvas already has an authored UI. Edit its prefabs directly.");
            if (canvasRoot == null) canvasRoot = transform;
            uiSkin = Resources.Load<FloodUISkin>("FloodUISkin");
            safeRoot = MakeRect("SafeArea", canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).transform;
            Stretch((RectTransform)safeRoot);
            safeRoot.gameObject.AddComponent<FloodFillSafeArea3D>();
            BuildScreens();
            BuildFpsCounter();
            fpsLabel.transform.parent.gameObject.SetActive(false);
            ConfigurePersistentActions();
            ConfigureStateVisuals();
            UpdateModeSections();
            masterAudioSlider.SetValueWithoutNotify(1f);
            musicAudioSlider.SetValueWithoutNotify(.65f);
            sfxAudioSlider.SetValueWithoutNotify(.8f);
            masterAudioValue.text = "100%"; musicAudioValue.text = "65%"; sfxAudioValue.text = "80%";
            progress.fillAmount = 0f;
            foreach (Transform item in GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 5;
            foreach (GameObject screen in EditorScreens)
            {
                int childCount = screen.transform.childCount;
                var content = MakeRect("Content", screen.transform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(1080,1920));
                for (int i = 0; i < childCount; i++) screen.transform.GetChild(0).SetParent(content.transform, false);
                content.AddComponent<FloodFillUIFit3D>();
            }
            // Give labels meaningful names for Hierarchy editing, without using names for bindings.
            foreach (TMP_Text label in GetComponentsInChildren<TMP_Text>(true))
            {
                string title = Regex.Replace(label.text, "<.*?>", string.Empty).Split('\n')[0];
                label.name = title.Length == 0 ? "DynamicText" : title.Substring(0, Math.Min(40, title.Length));
            }
            HideAll(); menu.SetActive(true);
            manager = null; // The prefab resolves its scene game manager at runtime.
            Canvas.ForceUpdateCanvases();
        }

        private void ConfigurePersistentActions()
        {
            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                var binding = button.gameObject.AddComponent<FloodFillUIAction3D>();
                string label = button.name.EndsWith("Button") ? button.name.Substring(0, button.name.Length - 6) : button.name;
                binding.audioLabel = label;
                if (modeButtons.Contains(button)) { binding.action = FloodFillUIAction.Mode; binding.index = modeButtons.IndexOf(button); }
                else if (sizeButtons.Contains(button)) { binding.action = FloodFillUIAction.Size; binding.index = sizeButtons.IndexOf(button); }
                else if (difficultyButtons.Contains(button)) { binding.action = FloodFillUIAction.Difficulty; binding.index = difficultyButtons.IndexOf(button); }
                else if (colorButtons.Contains(button)) { binding.action = FloodFillUIAction.Color; binding.index = colorButtons.IndexOf(button); }
                else if (button == fruitDropdownButton) binding.action = FloodFillUIAction.FruitDropdown;
                else if (button.transform.IsChildOf(fruitDropdownMenu.transform)) { binding.action = FloodFillUIAction.Fruit; binding.index = Array.IndexOf(FruitNames, label); }
                else
                {
                    switch (label)
                    {
                        case "PLAY": binding.action = FloodFillUIAction.Play; break;
                        case "CONTINUE": binding.action = FloodFillUIAction.Continue; break;
                        case "CUSTOM": binding.action = FloodFillUIAction.Setup; break;
                        case "HOW TO PLAY": binding.action = FloodFillUIAction.Help; break;
                        case "SETTINGS": binding.action = FloodFillUIAction.Settings; break;
                        case "QUIT": binding.action = FloodFillUIAction.Quit; break;
                        case "START GAME": binding.action = FloodFillUIAction.StartGame; break;
                        case "HOME": binding.action = FloodFillUIAction.Home; break;
                        case "PAUSE": binding.action = FloodFillUIAction.Pause; break;
                        case "CONFIRM": binding.action = FloodFillUIAction.ConfirmHome; break;
                        case "RESUME": binding.action = FloodFillUIAction.Resume; break;
                        case "RESTART": case "TRY AGAIN": binding.action = FloodFillUIAction.Restart; break;
                        case "MAIN MENU": binding.action = button.transform.IsChildOf(pause.transform) ? FloodFillUIAction.Home : FloodFillUIAction.ReturnToMenu; break;
                        case "PLAY AGAIN": binding.action = FloodFillUIAction.Result; break;
                        case "BACK": binding.action = button.transform.IsChildOf(homeConfirmation.transform) ? FloodFillUIAction.CancelHome : button.transform.IsChildOf(settings.transform) ? FloodFillUIAction.SettingsBack : button.transform.IsChildOf(loading.transform) ? FloodFillUIAction.ReturnToMenu : FloodFillUIAction.Menu; break;
                        default: throw new InvalidOperationException("Unmapped UI control: " + label);
                    }
                }
                button.onClick.RemoveAllListeners();
                UnityEventTools.AddPersistentListener(button.onClick, binding.Invoke);
            }
            BindEditorSlider(masterAudioSlider, FloodFillUIAction.MasterVolume, "MASTER VOLUME");
            BindEditorSlider(musicAudioSlider, FloodFillUIAction.MusicVolume, "MUSIC VOLUME");
            BindEditorSlider(sfxAudioSlider, FloodFillUIAction.SfxVolume, "SOUND EFFECTS");
            BindEditorToggle(showCurrentBlockToggle, FloodFillUIAction.ShowCurrentBlock, "SHOW CURRENT BLOCK");
            BindEditorToggle(musicAudioToggle, FloodFillUIAction.MusicEnabled, "MUSIC ENABLED");
            BindEditorToggle(sfxAudioToggle, FloodFillUIAction.SfxEnabled, "SOUND EFFECTS ENABLED");
            var feedback = sfxAudioToggle.gameObject.AddComponent<FloodAudioUIOverride>();
            feedback.silent = true; // SFX mute emits its own shared toggle feedback through Master.
        }

        private void BindEditorSlider(Slider slider, FloodFillUIAction action, string label)
        {
            var binding = slider.gameObject.AddComponent<FloodFillUIAction3D>();
            binding.action = action; binding.audioLabel = label;
            slider.onValueChanged.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(slider.onValueChanged, binding.SetValue);
        }

        private void BindEditorToggle(Toggle toggle, FloodFillUIAction action, string label)
        {
            var binding = toggle.gameObject.AddComponent<FloodFillUIAction3D>();
            binding.action = action; binding.audioLabel = label;
            toggle.onValueChanged.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(toggle.onValueChanged, binding.SetEnabled);
        }

        private void ConfigureStateVisuals()
        {
            Transform icon = resultActionLabel.transform.parent.Find("Icon");
            if (icon != null)
            {
                var state = icon.gameObject.AddComponent<FloodFillUIState3D>();
                state.normalSprite = uiSkin.Find("Restart"); state.selectedSprite = uiSkin.Find("Play");
                state.Apply(false);
            }
            foreach (var buttons in new[] { modeButtons, sizeButtons, difficultyButtons })
                for (int i = 0; i < buttons.Count; i++)
                {
                    Button button = buttons[i];
                    var state = button.gameObject.AddComponent<FloodFillUIState3D>();
                    state.normalSprite = uiSkin.Find(buttons == sizeButtons ? "Tile" : "Secondary");
                    state.selectedSprite = uiSkin.Find(buttons == sizeButtons ? "SelectedTile" : "Primary");
                    state.Apply(i == (buttons == difficultyButtons ? 1 : 0));
                }
            foreach (Toggle toggle in new[] { showCurrentBlockToggle, musicAudioToggle, sfxAudioToggle })
            {
                var state = toggle.gameObject.AddComponent<FloodFillUIState3D>();
                state.normalSprite = uiSkin.Find("SwitchOff"); state.selectedSprite = uiSkin.Find("SwitchOn");
                Image image = (Image)toggle.targetGraphic;
                image.type = Image.Type.Simple; image.preserveAspect = true;
                image.rectTransform.sizeDelta = new Vector2(104,57);
                if (toggle.graphic != null) toggle.graphic.gameObject.SetActive(false);
                toggle.graphic = null;
                state.Apply(toggle.isOn);
            }
        }
    }
}
#endif
