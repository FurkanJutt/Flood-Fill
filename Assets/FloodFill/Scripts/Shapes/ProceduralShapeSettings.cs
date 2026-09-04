using System;
using UnityEngine;

namespace FloodFill.Shapes
{
    [Serializable]
    public sealed class ProceduralShapeSettings
    {
        [Header("Shape Size")]
        [Range(0.1f, 0.9f)] public float minFillPercent = 0.38f;
        [Range(0.1f, 0.9f)] public float maxFillPercent = 0.58f;
        [Min(0)] public int edgeMargin = 1;
        [Min(1)] public int minimumShapeWidth = 7;
        [Min(1)] public int minimumShapeHeight = 7;

        [Header("Growth")]
        [Min(0)] public int minBrushRadius = 0;
        [Min(0)] public int maxBrushRadius = 1;
        [Range(0f, 1f)] public float brushChance = 0.30f;
        [Range(0f, 1f)] public float branchChance = 0.30f;
        [Range(0f, 1f)] public float directionPersistence = 0.25f;
        [Range(0f, 1f)] public float centerBias = 0.15f;
        [Min(0)] public int seedOffsetRadius = 2;

        [Header("Shape Variety")]
        [Tooltip("How strongly each board can deviate from the base growth settings.")]
        [Range(0f, 1f)] public float shapeVariation = 0.85f;
        [Tooltip("Encourages growth toward several different areas to create lobes and asymmetry.")]
        [Range(0f, 1f)] public float lobeStrength = 0.75f;
        [Tooltip("Chance to carve connectivity-safe notches into the outside edge.")]
        [Range(0f, 1f)] public float edgeNotchChance = 0.70f;
        [Range(0, 20)] public int maxEdgeNotches = 8;
        [Tooltip("Chance to carve small enclosed holes into an otherwise solid area.")]
        [Range(0f, 1f)] public float cavityChance = 0.55f;
        [Range(0, 8)] public int maxCavities = 4;

        [Header("Cleanup and Validation")]
        [Range(0, 5)] public int cleanupIterations = 1;
        [Range(0f, 1f)] public float spikeRemovalChance = 0.35f;
        [Min(1)] public int maxGenerationAttempts = 20;

        [Header("Random Seed")]
        [Tooltip("Enabled creates a fresh procedural shape each restart. Disabled reuses Fixed Seed.")]
        public bool useRandomSeed = true;
        public int fixedSeed = 12345;

        [Header("Debug")]
        public bool logGeneratedMask;
    }
}
