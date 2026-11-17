using System;

namespace FloodFill.ThreeD.Solver
{
    public static class FloodSolverValidator
    {
        public static bool Validate(FloodFillRegionGraph3D graph, FloodFillSolverMove3D[] moves, out int[] captured)
        {
            captured = Array.Empty<int>();
            if (graph == null || moves == null || !graph.IsConnected) return false;
            byte[] state = (byte[])graph.RegionColors.Clone();
            var simulator = new FloodMoveSimulator(graph);
            var counts = new int[moves.Length];
            try
            {
                for (int i = 0; i < moves.Length; i++)
                {
                    state = simulator.Apply(state, moves[i]);
                    counts[i] = simulator.CountComponent(state, moves[i].RegionId);
                }
            }
            catch (ArgumentException) { return false; }
            for (int i = 1; i < state.Length; i++) if (state[i] != state[0]) return false;
            captured = counts; return true;
        }
    }
}
