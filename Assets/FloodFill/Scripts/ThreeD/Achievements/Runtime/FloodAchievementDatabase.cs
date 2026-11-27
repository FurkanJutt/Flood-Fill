using System.Collections.Generic;
using UnityEngine;
namespace FloodFill.ThreeD.Achievements
{
    [CreateAssetMenu(menuName="Flood Fill/Achievements/Database")]
    public sealed class FloodAchievementDatabase:ScriptableObject,IAchievementCatalogProvider
    {
        [System.Serializable]public sealed class IconEntry { public string key;public Sprite sprite; }
        public List<IconEntry> extraIcons=new List<IconEntry>();
        public int catalogVersion=1;
        public List<FloodAchievementDefinition> definitions=new List<FloodAchievementDefinition>();
        public AchievementCatalogData Load()
        {
            var result=new AchievementCatalogData{version=catalogVersion,levelsPerCategory=FloodLevelProgression3D.LevelsPerCategory};
            foreach(var asset in definitions)if(asset!=null)result.achievements.Add(asset.definition);
            result.achievements.Sort((a,b)=>a.order.CompareTo(b.order));return result;
        }
        public void SetIcon(string key,Sprite sprite){var entry=extraIcons.Find(e=>e.key==key);if(entry==null){entry=new IconEntry{key=key};extraIcons.Add(entry);}entry.sprite=sprite;}
        public Sprite Icon(string key){foreach(var a in definitions)if(a!=null&&a.definition.iconKey==key&&a.icon!=null)return a.icon;foreach(var entry in extraIcons)if(entry.key==key)return entry.sprite;return null;}
    }
}
