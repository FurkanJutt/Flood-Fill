using System.Collections.Generic;
using UnityEngine;

namespace FloodFill.Shapes
{
    public static class ShapeBorderContourBuilder
    {
        private readonly struct BoundaryEdge
        {
            public BoundaryEdge(Vector2Int start, Vector2Int end)
            {
                Start = start;
                End = end;
            }

            public Vector2Int Start { get; }
            public Vector2Int End { get; }
            public Vector2Int Direction => End - Start;
        }

        public static IReadOnlyList<Vector3[]> BuildContours(
            bool[,] activeMask,
            ShapeBounds activeBounds,
            float cellSize,
            float cellSpacing)
        {
            var contours = new List<Vector3[]>();
            if (activeMask == null || !activeBounds.IsValid)
            {
                return contours;
            }

            List<BoundaryEdge> edges = BuildBoundaryEdges(activeMask);
            var outgoingEdges = new Dictionary<Vector2Int, List<int>>();
            for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                Vector2Int start = edges[edgeIndex].Start;
                if (!outgoingEdges.TryGetValue(start, out List<int> indices))
                {
                    indices = new List<int>();
                    outgoingEdges.Add(start, indices);
                }

                indices.Add(edgeIndex);
            }

            var usedEdges = new bool[edges.Count];
            float safeCellSize = Mathf.Max(0.05f, cellSize);
            float safeSpacing = Mathf.Max(0f, cellSpacing);
            float step = safeCellSize + safeSpacing;
            float shapeCenterX = (activeBounds.MinX + activeBounds.MaxX) * 0.5f;
            float shapeCenterY = (activeBounds.MinY + activeBounds.MaxY) * 0.5f;

            for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                if (usedEdges[edgeIndex])
                {
                    continue;
                }

                List<Vector2Int> latticeLoop = TraceLoop(
                    edgeIndex,
                    edges,
                    outgoingEdges,
                    usedEdges);
                if (latticeLoop.Count < 4)
                {
                    continue;
                }

                contours.Add(ConvertToCellEdgeContour(
                    latticeLoop,
                    step,
                    safeSpacing * 0.5f,
                    shapeCenterX,
                    shapeCenterY));
            }

            return contours;
        }

        private static List<BoundaryEdge> BuildBoundaryEdges(bool[,] mask)
        {
            var edges = new List<BoundaryEdge>();
            for (int x = 0; x < mask.GetLength(0); x++)
            {
                for (int y = 0; y < mask.GetLength(1); y++)
                {
                    if (!mask[x, y])
                    {
                        continue;
                    }

                    if (!IsActive(mask, x, y - 1))
                    {
                        edges.Add(new BoundaryEdge(
                            new Vector2Int(x, y),
                            new Vector2Int(x + 1, y)));
                    }

                    if (!IsActive(mask, x + 1, y))
                    {
                        edges.Add(new BoundaryEdge(
                            new Vector2Int(x + 1, y),
                            new Vector2Int(x + 1, y + 1)));
                    }

                    if (!IsActive(mask, x, y + 1))
                    {
                        edges.Add(new BoundaryEdge(
                            new Vector2Int(x + 1, y + 1),
                            new Vector2Int(x, y + 1)));
                    }

                    if (!IsActive(mask, x - 1, y))
                    {
                        edges.Add(new BoundaryEdge(
                            new Vector2Int(x, y + 1),
                            new Vector2Int(x, y)));
                    }
                }
            }

            return edges;
        }

        private static List<Vector2Int> TraceLoop(
            int startingEdgeIndex,
            IReadOnlyList<BoundaryEdge> edges,
            IReadOnlyDictionary<Vector2Int, List<int>> outgoingEdges,
            bool[] usedEdges)
        {
            var loop = new List<Vector2Int>();
            Vector2Int loopStart = edges[startingEdgeIndex].Start;
            int currentEdgeIndex = startingEdgeIndex;
            int safetyLimit = edges.Count + 1;
            while (safetyLimit-- > 0 && currentEdgeIndex >= 0)
            {
                BoundaryEdge currentEdge = edges[currentEdgeIndex];
                if (usedEdges[currentEdgeIndex])
                {
                    break;
                }

                usedEdges[currentEdgeIndex] = true;
                loop.Add(currentEdge.Start);
                if (currentEdge.End == loopStart)
                {
                    return loop;
                }

                currentEdgeIndex = SelectNextEdge(
                    currentEdge,
                    edges,
                    outgoingEdges,
                    usedEdges);
            }

            loop.Clear();
            return loop;
        }

        private static int SelectNextEdge(
            BoundaryEdge currentEdge,
            IReadOnlyList<BoundaryEdge> edges,
            IReadOnlyDictionary<Vector2Int, List<int>> outgoingEdges,
            bool[] usedEdges)
        {
            if (!outgoingEdges.TryGetValue(currentEdge.End, out List<int> candidates))
            {
                return -1;
            }

            int selectedIndex = -1;
            int selectedPriority = int.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                int candidateIndex = candidates[i];
                if (usedEdges[candidateIndex])
                {
                    continue;
                }

                int priority = GetTurnPriority(
                    currentEdge.Direction,
                    edges[candidateIndex].Direction);
                if (priority < selectedPriority)
                {
                    selectedPriority = priority;
                    selectedIndex = candidateIndex;
                }
            }

            return selectedIndex;
        }

        private static int GetTurnPriority(Vector2Int incoming, Vector2Int outgoing)
        {
            int cross = incoming.x * outgoing.y - incoming.y * outgoing.x;
            if (cross > 0)
            {
                return 0;
            }

            int dot = incoming.x * outgoing.x + incoming.y * outgoing.y;
            if (dot > 0)
            {
                return 1;
            }

            return cross < 0 ? 2 : 3;
        }

        private static Vector3[] ConvertToCellEdgeContour(
            IReadOnlyList<Vector2Int> latticeLoop,
            float step,
            float inwardOffset,
            float shapeCenterX,
            float shapeCenterY)
        {
            var positions = new Vector3[latticeLoop.Count];
            for (int i = 0; i < latticeLoop.Count; i++)
            {
                Vector2Int previous = latticeLoop[(i - 1 + latticeLoop.Count) % latticeLoop.Count];
                Vector2Int current = latticeLoop[i];
                Vector2Int next = latticeLoop[(i + 1) % latticeLoop.Count];
                Vector2Int incoming = current - previous;
                Vector2Int outgoing = next - current;
                Vector2 previousNormal = new Vector2(-incoming.y, incoming.x);
                Vector2 nextNormal = new Vector2(-outgoing.y, outgoing.x);
                Vector2 offset = incoming == outgoing
                    ? previousNormal * inwardOffset
                    : (previousNormal + nextNormal) * inwardOffset;
                positions[i] = new Vector3(
                    (current.x - 0.5f - shapeCenterX) * step + offset.x,
                    (current.y - 0.5f - shapeCenterY) * step + offset.y,
                    0f);
            }

            return positions;
        }

        private static bool IsActive(bool[,] mask, int x, int y)
        {
            return x >= 0 && x < mask.GetLength(0) &&
                y >= 0 && y < mask.GetLength(1) && mask[x, y];
        }
    }
}
