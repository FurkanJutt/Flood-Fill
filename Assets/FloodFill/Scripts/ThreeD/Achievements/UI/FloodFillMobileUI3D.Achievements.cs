using FloodFill.ThreeD.Achievements;
using UnityEngine;
namespace FloodFill.ThreeD
{
    public sealed partial class FloodFillMobileUI3D
    {
        [Header("Achievements")][SerializeField]private FloodAchievementScreen achievementScreen;
        [SerializeField]private FloodAchievementNotifications achievementNotifications;
        private void InitializeAchievementsUI()
        {
            var service=FloodAchievementService.Instance;
            foreach(var binding in GetComponentsInChildren<FloodFillUIAction3D>(true))if(binding.action==FloodFillUIAction.Achievements)binding.gameObject.SetActive(service!=null&&service.Ready);
            if(achievementScreen!=null)achievementScreen.Initialize(this);
            achievementNotifications?.Initialize();
        }
        public void OpenAchievements()
        {
            if(FloodAchievementService.Instance==null||!FloodAchievementService.Instance.Ready)return;
            if(achievementScreen==null)return;
            started=false;Show(achievementScreen.gameObject);
        }
        public void CloseAchievements(){if(achievementScreen!=null)achievementScreen.detailPanel.SetActive(false);Show(menu);}
    }
}
