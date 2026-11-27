using FloodFill.ThreeD.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.ThreeD
{
    public sealed partial class FloodFillMobileUI3D
    {
        private void BindAuthoredControls()
        {
            var audio = FloodAudioManager.Instance;
            if (audio != null && audioSettingsOwner != audio)
            {
                UnbindAudioControls();
                audioSettingsOwner = audio;
                audio.SettingsChanged += RefreshAudioControls;
                foreach (var binding in GetComponentsInChildren<FloodFillUIAction3D>(true))
                {
                    var control = binding.GetComponent<Selectable>();
                    if (control != null) audio.BindControl(control, binding.audioLabel, binding.action == FloodFillUIAction.SfxEnabled || binding.action == FloodFillUIAction.Color);
                }
                RefreshAudioControls();
            }
            foreach (var binding in GetComponentsInChildren<FloodFillUIAction3D>(true))
                if (binding.action == FloodFillUIAction.Quit)
                    binding.gameObject.SetActive(Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer);
        }

        public void ExecuteAuthoredAction(FloodFillUIAction action, int index)
        {
            if (!built) return;
            switch (action)
            {
                case FloodFillUIAction.Levels: OpenLevelSelection(); break;
                case FloodFillUIAction.Play: PlayConfiguredGame(); break;
                case FloodFillUIAction.Continue: Continue(); break;
                case FloodFillUIAction.Setup: Show(setup); break;
                case FloodFillUIAction.Help: Show(howTo); break;
                case FloodFillUIAction.Settings: OpenSettings(); break;
                case FloodFillUIAction.Quit: Application.Quit(); break;
                case FloodFillUIAction.StartGame: StartGame(); break;
                case FloodFillUIAction.Menu: Show(menu); break;
                case FloodFillUIAction.Pause: OpenPause(); break;
                case FloodFillUIAction.Home: OpenHomeConfirmation(); break;
                case FloodFillUIAction.ConfirmHome: ConfirmHome(); break;
                case FloodFillUIAction.CancelHome: CancelHome(); break;
                case FloodFillUIAction.SettingsBack: CloseSettings(); break;
                case FloodFillUIAction.Resume: Resume(); break;
                case FloodFillUIAction.Restart: Restart(); break;
                case FloodFillUIAction.PromptSavedLevelRestart: PromptSavedLevelRestart(); break;
                case FloodFillUIAction.ConfirmSavedLevelRestart: ConfirmSavedLevelRestart(); break;
                case FloodFillUIAction.CancelSavedLevelRestart: CancelSavedLevelRestart(); break;
                case FloodFillUIAction.ReturnToMenu: BackToMenu(); break;
                case FloodFillUIAction.Result: OnResultAction(); break;
                case FloodFillUIAction.FruitDropdown: ToggleFruitDropdown(); break;
                case FloodFillUIAction.Mode: SetMode(index); break;
                case FloodFillUIAction.Size: SetSize(index); break;
                case FloodFillUIAction.Difficulty: SetDifficulty(index); break;
                case FloodFillUIAction.Fruit: SetFruit(index); break;
                case FloodFillUIAction.Color: manager.SelectColor(index); break;
            }
        }

        public void ExecuteAuthoredValue(FloodFillUIAction action, float value)
        {
            var audio = FloodAudioManager.Instance;
            if (audio == null) return;
            switch (action)
            {
                case FloodFillUIAction.MasterVolume: audio.SetMasterVolume(value); break;
                case FloodFillUIAction.MusicVolume: audio.SetMusicVolume(value); break;
                case FloodFillUIAction.SfxVolume: audio.SetSfxVolume(value); break;
            }
        }

        public void ExecuteAuthoredToggle(FloodFillUIAction action, bool value, Toggle control)
        {
            switch (action)
            {
                case FloodFillUIAction.ShowCurrentBlock: SetShowCurrentBlock(value); break;
                case FloodFillUIAction.MusicEnabled: FloodAudioManager.Instance?.SetMusicEnabled(value); break;
                case FloodFillUIAction.SfxEnabled: FloodAudioManager.Instance?.SetSfxEnabledFromUI(value); break;
            }
            if (control != null) SkinSwitch(control);
        }
    }
}
