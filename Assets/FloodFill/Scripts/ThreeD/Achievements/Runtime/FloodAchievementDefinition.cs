using UnityEngine;
namespace FloodFill.ThreeD.Achievements
{
    [CreateAssetMenu(menuName="Flood Fill/Achievements/Definition")]
    public sealed class FloodAchievementDefinition:ScriptableObject
    {
        public AchievementDefinition definition=new AchievementDefinition();
        public Sprite icon;
    }
}
