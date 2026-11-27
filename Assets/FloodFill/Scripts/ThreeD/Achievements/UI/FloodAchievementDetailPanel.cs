using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.ThreeD.Achievements
{
    /// <summary>Authored detail-popup references shared by the prefab and screen.</summary>
    public sealed class FloodAchievementDetailPanel:MonoBehaviour
    {
        public TMP_Text title,body;
        public Image icon;
        public Button close;
        public Button[] claims=new Button[3];
        public TMP_Text[] claimLabels=new TMP_Text[3];
    }
}
