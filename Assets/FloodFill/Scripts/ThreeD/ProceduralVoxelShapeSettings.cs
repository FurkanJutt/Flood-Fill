using System;
using UnityEngine;

namespace FloodFill.ThreeD
{
    [Serializable]
    public sealed class ProceduralVoxelShapeSettings
    {
        [Header("Shape Size")]
        [Range(0.1f, 0.95f)] public float minSolidFillPercent = 0.38f;
        [Range(0.1f, 0.95f)] public float maxSolidFillPercent = 0.68f;
        [Min(0)] public int edgeMargin;
        [Min(1)] public int minimumSpanX = 4;
        [Min(1)] public int minimumSpanY = 4;
        [Min(1)] public int minimumSpanZ = 4;

        [Header("Growth")]
        [Range(0f, 1f)] public float brushChance = 0.32f;
        [Range(0f, 1f)] public float branchChance = 0.42f;
        [Range(0f, 1f)] public float directionPersistence = 0.32f;
        [Range(0f, 1f)] public float centerBias = 0.12f;
        [Min(0)] public int seedOffsetRadius = 1;

        [Header("Shape Variety")]
        [Tooltip("Pulls growth toward several random locations, producing lobes and asymmetry.")]
        [Range(0f, 1f)] public float lobeStrength = 0.78f;
        [Range(2, 7)] public int minimumLobes = 2;
        [Range(2, 7)] public int maximumLobes = 5;
        [Tooltip("Carves connectivity-safe notches from the exterior before creating the shell.")]
        [Range(0f, 1f)] public float edgeNotchChance = 0.28f;
        [Range(0, 4)] public int edgeNotchPasses = 2;

        [Header("Validation")]
        [Min(1)] public int maxGenerationAttempts = 30;

        [Header("Random Seed")]
        [Tooltip("Enabled creates a fresh 3D shape each restart. Disabled reuses Fixed Seed.")]
        public bool useRandomSeed = true;
        public int fixedSeed = 12345;

        [Header("Debug")]
        public bool logGenerationDetails;
    }
}
