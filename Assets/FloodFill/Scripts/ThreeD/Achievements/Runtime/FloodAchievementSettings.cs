using UnityEngine;
namespace FloodFill.ThreeD.Achievements
{
    [CreateAssetMenu(menuName="Flood Fill/Achievements/Settings")]
    public sealed class FloodAchievementSettings:ScriptableObject
    {
        [Header("General")]
        public bool enableAchievementSystem=true;
        public FloodAchievementDatabase database;
        public bool enableRetroactiveInitialization=true,enableNotifications=true,enableHiddenAchievements=true,enableRewardUI=true,debugLogging;
        [Header("Achievement screen")]
        public GameObject screenPrefab,cardPrefab,detailPrefab,notificationPrefab;
        public Sprite filledStar,emptyStar,defaultIcon,hiddenIcon,completedIndicator;
        [Header("Notifications")]
        [Min(.5f)]public float notificationDuration=3;
        [Min(.05f)]public float notificationAnimationDuration=.22f;
        [Range(1,30)]public int maximumQueuedNotifications=8;
        public AudioClip achievementUnlockClip,tierCompletionClip;
        [Header("Persistence")]
        public string saveFileName="flood-fill-achievements-v1.json";
        [Min(.1f)]public float saveDebounceDuration=2f;
        public bool saveOnCompletion=true,saveOnPause=true,saveOnExit=true;
    }
}
