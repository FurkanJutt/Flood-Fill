using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.ThreeD
{
    public sealed partial class FloodFillMobileUI3D
    {
        [SerializeField,HideInInspector] private GameObject savedLevelRestartButton,savedLevelRestartConfirmation;
        [SerializeField,HideInInspector] private RectTransform savedLevelBackRect;
        [SerializeField,HideInInspector] private Vector2 savedLevelBackPosition,savedLevelButtonStep;
        private int restartSavedMode,restartSavedDifficulty,restartSavedLevel;
        private void EnsureSavedLevelRecoveryUI()
        {
            if(savedLevelRestartButton!=null||loadingRetry==null||homeConfirmation==null)return;
            foreach(var binding in loading.GetComponentsInChildren<FloodFillUIAction3D>(true))
                if(binding.action==FloodFillUIAction.ReturnToMenu)
                {savedLevelBackRect=binding.GetComponent<RectTransform>();break;}
            if(savedLevelBackRect==null)return;
            savedLevelBackPosition=savedLevelBackRect.anchoredPosition;
            savedLevelButtonStep=savedLevelBackPosition-loadingRetry.GetComponent<RectTransform>().anchoredPosition;
            savedLevelRestartButton=Instantiate(loadingRetry,loadingRetry.transform.parent);
            savedLevelRestartButton.name="RestartSavedLevelButton";
            savedLevelRestartButton.GetComponent<RectTransform>().anchoredPosition=savedLevelBackPosition;
            foreach(var text in savedLevelRestartButton.GetComponentsInChildren<TMP_Text>(true))
                if(text.text.Contains("TRY AGAIN"))text.text="RESTART LEVEL";
            var action=savedLevelRestartButton.GetComponent<FloodFillUIAction3D>();
            action.action=FloodFillUIAction.PromptSavedLevelRestart;action.audioLabel="RESTART";
            savedLevelRestartButton.SetActive(false);
            savedLevelRestartConfirmation=Instantiate(homeConfirmation,loading.transform);
            savedLevelRestartConfirmation.name="SavedLevelRestartConfirmation";
            foreach(var text in savedLevelRestartConfirmation.GetComponentsInChildren<TMP_Text>(true))
            {
                if(text.text.Contains("Are you sure"))text.text="This will overwrite the previous save. do you want to start over?";
                else if(text.text.Contains("QUIT TO HOME"))text.text="RESTART LEVEL?";
            }
            foreach(var binding in savedLevelRestartConfirmation.GetComponentsInChildren<FloodFillUIAction3D>(true))
            {
                if(binding.action==FloodFillUIAction.ConfirmHome)binding.action=FloodFillUIAction.ConfirmSavedLevelRestart;
                if(binding.action==FloodFillUIAction.CancelHome)binding.action=FloodFillUIAction.CancelSavedLevelRestart;
            }
            savedLevelRestartConfirmation.SetActive(false);
        }
        private void RefreshSavedLevelRecoveryUI()
        {
            if(savedLevelRestartButton==null)return;
            bool available=manager.CanRestartUnreadableSavedLevel;
            savedLevelRestartButton.SetActive(available);
            savedLevelBackRect.anchoredPosition=savedLevelBackPosition+(available?savedLevelButtonStep:Vector2.zero);
            if(!available)savedLevelRestartConfirmation.SetActive(false);
        }
        private void PromptSavedLevelRestart()
        {
            if(!manager.CanRestartUnreadableSavedLevel||!loading.activeInHierarchy||savedLevelRestartConfirmation==null)return;
            restartSavedMode=manager.NumberedMode;restartSavedDifficulty=(int)manager.SelectedDifficulty;restartSavedLevel=manager.NumberedLevel;
            savedLevelRestartConfirmation.SetActive(true);savedLevelRestartConfirmation.transform.SetAsLastSibling();
        }
        private void CancelSavedLevelRestart()
        {if(savedLevelRestartConfirmation!=null)savedLevelRestartConfirmation.SetActive(false);}
        private void ConfirmSavedLevelRestart()
        {
            if(savedLevelRestartConfirmation==null||!savedLevelRestartConfirmation.activeInHierarchy)return;
            if(restartSavedMode!=manager.NumberedMode||restartSavedDifficulty!=(int)manager.SelectedDifficulty||restartSavedLevel!=manager.NumberedLevel)
            {CancelSavedLevelRestart();return;}
            CancelSavedLevelRestart();
            if(manager.RestartUnreadableSavedLevel()){started=true;Show(hud);}
        }
    }
}
