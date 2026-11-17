using System;
using System.Collections.Generic;

namespace FloodFill.ThreeD.Solver
{
    internal readonly struct FloodCandidate
    {
        internal FloodCandidate(FloodFillSolverMove3D move, double score) { Move = move; Score = score; }
        internal FloodFillSolverMove3D Move { get; }
        internal double Score { get; }
    }
    internal static class FloodHeuristics
    {
        // Each move can eliminate at most one globally present color. Valid for free flood.
        internal static int LowerBound(byte[] state, int palette)
        {
            var seen = new bool[palette]; int count = 0;
            for (int i = 0; i < state.Length; i++) if (!seen[state[i]]) { seen[state[i]] = true; count++; }
            return Math.Max(0, count - 1);
        }
        internal static List<FloodCandidate> Moves(FloodFillRegionGraph3D graph, byte[] state, int strategy, bool allLegal = false)
        {
            var a = new FloodComponentAnalysis(graph, state);
            var moves = new List<FloodCandidate>(a.Count * Math.Min(graph.ColorCount, 6));
            var mass = new int[graph.ColorCount]; var groups = new int[graph.ColorCount];
            for (int c = 0; c < a.Count; c++)
            {
                Array.Clear(mass, 0, mass.Length); Array.Clear(groups, 0, groups.Length);
                foreach (int neighbor in a.Adjacent[c]) { mass[a.Colors[neighbor]] += a.Mass[neighbor]; groups[a.Colors[neighbor]]++; }
                for (int color = 0; color < graph.ColorCount; color++)
                {
                    if (color == a.Colors[c] || (!allLegal && groups[color] == 0)) continue;
                    int merged = a.Mass[c] + mass[color];
                    int exposedMass = 0, exposedGroups = 0;
                    var frontier = new HashSet<int>();
                    foreach (int neighbor in a.Adjacent[c])
                    {
                        if (a.Colors[neighbor] != color) frontier.Add(neighbor);
                        else foreach (int beyond in a.Adjacent[neighbor])
                            if (beyond != c && a.Colors[beyond] != color) frontier.Add(beyond);
                    }
                    foreach (int f in frontier) { exposedMass += a.Mass[f]; exposedGroups++; }
                    double score;
                    switch (strategy % 6)
                    {
                        case 0: score = mass[color] * 10d + groups[color]; break;
                        case 1: score = groups[color] * 1000d + merged; break;
                        case 2: score = merged * 10d + groups[color] * 20d; break;
                        case 3: score = merged * 4d + exposedMass * 2d + exposedGroups * 5d; break;
                        case 4: score = groups[color] * 150d + merged * 3d + exposedMass; break;
                        default: score = merged * 1000d + groups[color] * 10d + exposedMass; break;
                    }
                    moves.Add(new FloodCandidate(new FloodFillSolverMove3D(a.Representatives[c], color), score));
                }
            }
            moves.Sort((x, y) => { int d = y.Score.CompareTo(x.Score); if (d != 0) return d; d = x.Move.RegionId.CompareTo(y.Move.RegionId); return d != 0 ? d : x.Move.ColorIndex.CompareTo(y.Move.ColorIndex); });
            return moves;
        }
        internal static double Estimate(FloodFillRegionGraph3D graph, byte[] state)
        {
            var a = new FloodComponentAnalysis(graph, state);
            // Ordering estimate only, never an exact-search pruning bound.
            return (a.Count - 1) + 0.95d * (graph.TotalVoxelCount - a.LargestMass) / graph.TotalVoxelCount;
        }
    }
}
