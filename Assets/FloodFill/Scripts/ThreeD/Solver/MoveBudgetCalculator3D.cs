using System;

namespace FloodFill.ThreeD.Solver
{
    public static class MoveBudgetCalculator3D
    {
        public static MoveBudgetProfile Calculate(
            int bestSolutionMoves,
            int regionCount,
            bool provenOptimal,
            MoveSolverSettings settings)
        {
            int solution = Math.Max(1, bestSolutionMoves);
            float multiplier = GetComplexityMultiplier(regionCount, settings);
            int hardExtra = ScaleExtra(Math.Max(1, Round(solution * 0.08)), multiplier);
            int normalExtra = ScaleExtra(Math.Max(3, Round(solution * 0.23)), multiplier);
            int easyExtra = ScaleExtra(Math.Max(5, Round(solution * 0.38)), multiplier);
            int configuredMaximum = Math.Max(4, settings?.maximumMoveBudget ?? 1000000);
            int maximum = Math.Max(configuredMaximum, solution + 3);

            int perfect = solution;
            int hard = Math.Min(maximum - 2, Math.Max(perfect + 1, solution + hardExtra));
            int normal = Math.Min(maximum - 1, Math.Max(hard + 1, solution + normalExtra));
            int easy = Math.Min(maximum, Math.Max(normal + 1, solution + easyExtra));
            return new MoveBudgetProfile
            {
                bestSolutionMoves = solution,
                perfectMoves = perfect,
                hardMoves = hard,
                normalMoves = normal,
                easyMoves = easy,
                isProvenOptimal = provenOptimal,
                complexityMultiplier = multiplier
            };
        }

        public static float GetComplexityMultiplier(int regionCount, MoveSolverSettings settings)
        {
            if (settings == null)
            {
                return 1f;
            }

            if (regionCount >= settings.complexityThreshold4)
            {
                return Math.Max(1f, settings.complexityMultiplier4);
            }
            if (regionCount >= settings.complexityThreshold3)
            {
                return Math.Max(1f, settings.complexityMultiplier3);
            }
            if (regionCount >= settings.complexityThreshold2)
            {
                return Math.Max(1f, settings.complexityMultiplier2);
            }
            if (regionCount >= settings.complexityThreshold1)
            {
                return Math.Max(1f, settings.complexityMultiplier1);
            }
            return 1f;
        }

        private static int ScaleExtra(int extra, float multiplier)
        {
            return Math.Max(1, Round(extra * multiplier));
        }

        private static int Round(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }
}
