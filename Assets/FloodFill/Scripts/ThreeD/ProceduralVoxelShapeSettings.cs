using System;
using UnityEngine;

namespace FloodFill.ThreeD
{
    [Serializable]
    public sealed class ProceduralVoxelShapeSettings
    {
        [Header("Shape Size")]
        [Range(0.1f, 0.95f)] public float minSolidFillPercent = 0.42f;
        [Range(0.1f, 0.95f)] public float maxSolidFillPercent = 0.62f;
        [Min(0)] public int edgeMargin;
        [Min(1)] public int minimumSpanX = 4;
        [Min(1)] public int minimumSpanY = 4;
        [Min(1)] public int minimumSpanZ = 4;

        [Header("Growth")]
        [Range(0f, 1f)] public float brushChance = 0.32f;
        [Tooltip("Chance to choose any frontier voxel instead of tournament weighting.")]
        [Range(0f, 1f)] public float branchChance = 0.22f;
        [Tooltip("Number of random frontier candidates scored per weighted selection.")]
        [Range(1, 16)] public int weightedCandidateSamples = 6;
        [Range(0f, 1f)] public float directionPersistence = 0.22f;
        [Range(0f, 1f)] public float centerBias = 0.34f;
        [Min(0)] public int seedOffsetRadius = 1;

        [Header("Shape Variety")]
        [Tooltip("Pulls growth toward several random locations, producing lobes and asymmetry.")]
        [Range(0f, 1f)] public float lobeStrength = 0.50f;
        [Range(2, 7)] public int minimumLobes = 2;
        [Range(2, 7)] public int maximumLobes = 3;
        [Tooltip("Uses one conservative batch notch pass with one connectivity check.")]
        public bool enableNotches;
        [Range(0f, 1f)] public float edgeNotchChance = 0.12f;
        [Range(0, 2)] public int edgeNotchPasses = 1;
        [Tooltip("Notches only remove cells with at least this many solid face neighbors.")]
        [Range(2, 5)] public int minimumNotchSolidNeighbors = 4;

        [Header("Validation")]
        [Range(1, 3)] public int maxGenerationAttempts = 3;

        [Header("Random Seed")]
        [Tooltip("Enabled creates a fresh 3D shape each restart. Disabled reuses Fixed Seed.")]
        public bool useRandomSeed = true;
        public int fixedSeed = 12345;

        [Header("Debug")]
        public bool logGenerationDetails;
        public bool logGenerationPerformance;
    }
}
