using System;
using UnityEngine;
namespace FloodFill.ThreeD.Goals
{
    [CreateAssetMenu(menuName = "Flood Fill/Secondary Goal Settings")]
    public sealed class FloodSecondaryGoalSettings3D : ScriptableObject
    {
        [Serializable] public sealed class Tuning
        {
            [Range(.1f, 1)] public float captureFraction = .65f;
            [Range(0, .95f)] public float moveSlackFraction = .5f;
            [Range(.1f,.6f)] public float earlyMoveFraction = .3f;
            [Range(.5f,.9f)] public float sprintStartFraction = .7f;
            [Min(1)] public float humanSecondsPerMove = 7;
        }
        [Serializable] public sealed class Pool { public float[] weights = new float[10]; }
        public Tuning[] difficulties = { new Tuning { captureFraction=.45f,moveSlackFraction=.65f,earlyMoveFraction=.35f,humanSecondsPerMove=10 }, new Tuning(), new Tuning { captureFraction=.8f,moveSlackFraction=.2f,earlyMoveFraction=.25f,humanSecondsPerMove=6 }, new Tuning { captureFraction=.95f,moveSlackFraction=0,earlyMoveFraction=.2f,humanSecondsPerMove=5 } };
        [Tooltip("Mode * 4 + difficulty; weights follow SecondaryGoalType enum order.")]
        public Pool[] pools = DefaultPools();
        [Min(1)] public int minimumNewCapture = 2, minimumMoveSavings = 1, minimumComboMoves = 2;
        [Range(.01f,.5f)] public float minimumCaptureBoardFraction = .02f;
        [Min(1)] public int candidateLimit = 30, validationMoveLimit = 2048;
        [Min(1)] public float generationBudgetMilliseconds = 30;
        [Min(1)] public float minimumSpeedSeconds = 60, cameraSecondsPerAxis = 8, regionInspectionSeconds = .12f;
        [Min(1)] public float randomShapeTimeMultiplier = 1.25f, itemsTimeMultiplier = 1.15f;
        [Tooltip("Optional description overrides in goal enum order. Tokens: {color}, {target}, {deadline}, {count}, {seconds}.")]
        public string[] descriptionFormats = new string[10];
        public string[] colorNames = { "green", "purple", "yellow", "orange", "blue", "cyan" };
        public Tuning ForDifficulty(int d) => difficulties[Mathf.Clamp(d,0,3)];
        public float Weight(int mode,int d,SecondaryGoalType type) => Mathf.Max(0,pools[mode*4+d].weights[(int)type]);
        private static Pool[] DefaultPools()
        {
            // Move, final color, capture, quick, combo, speed, color limit, sprint, expansion, compound.
            return new[] {
                P(3,2,3,2,0,1,0,0,0,0), P(3,2,3,2,2,1,0,0,0,0), P(3,0,3,0,3,1,2,0,3,0), P(3,0,3,0,3,0,2,3,0,3),
                P(2,3,3,2,0,1,0,0,0,0), P(2,2,3,0,3,1,0,0,2,0), P(3,0,3,0,3,0,2,2,3,0), P(3,0,3,0,3,0,2,3,0,3),
                P(2,3,3,2,0,1,0,0,0,0), P(2,3,3,0,2,1,0,0,0,0), P(3,2,3,0,0,0,2,0,3,0), P(3,0,3,0,3,0,2,3,0,3) };
        }
        private static Pool P(params float[] w) => new Pool {weights=w};
    }
}
