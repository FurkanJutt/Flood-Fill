using UnityEngine;
namespace FloodFill.ThreeD
{
    [CreateAssetMenu(menuName = "Flood Fill/Level Selection Rules")]
    public sealed class FloodLevelRules3D : ScriptableObject
    {
        [Range(0, 2)] public int defaultMode;
        [Range(0, 3)] public int defaultDifficulty;
        [Range(.01f, 1)] public float threeStarBudgetRatio = .7f;
        [Range(.01f, 1)] public float twoStarBudgetRatio = .9f;
        public Color progressColor = new Color(0, .85f, 1);
    }
}
