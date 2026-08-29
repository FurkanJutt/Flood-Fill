using System.Collections.Generic;
using UnityEngine;

namespace FloodFill.Shapes
{
    public static class ShapeBorderMeshBuilder
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        public static Mesh Build(
            bool[,] activeMask,
            ShapeBounds activeBounds,
            float cellSize,
            float cellSpacing,
            float borderWidth,
            Color borderColor)
        {
            if (activeMask == null || !activeBounds.IsValid)
            {
                return null;
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var uv = new List<Vector2>();
            float safeCellSize = Mathf.Max(0.05f, cellSize);
            float safeSpacing = Mathf.Max(0f, cellSpacing);
            float safeBorderWidth = Mathf.Max(0.005f, borderWidth);
            float step = safeCellSize + safeSpacing;
            float outlineWidth = Mathf.Max(safeBorderWidth, safeSpacing * 0.5f);
            float halfCell = safeCellSize * 0.5f;
            float halfLength = halfCell + outlineWidth;
            float shapeCenterX = (activeBounds.MinX + activeBounds.MaxX) * 0.5f;
            float shapeCenterY = (activeBounds.MinY + activeBounds.MaxY) * 0.5f;

            for (int x = 0; x < activeMask.GetLength(0); x++)
            {
                for (int y = 0; y < activeMask.GetLength(1); y++)
                {
                    if (!activeMask[x, y])
                    {
                        continue;
                    }

                    Vector2 center = new Vector2(
                        (x - shapeCenterX) * step,
                        (y - shapeCenterY) * step);
                    for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
                    {
                        Vector2Int direction = Directions[directionIndex];
                        if (IsActive(activeMask, x + direction.x, y + direction.y))
                        {
                            continue;
                        }

                        if (direction == Vector2Int.up)
                        {
                            float edgeY = center.y + halfCell;
                            AddQuad(
                                center.x - halfLength,
                                edgeY - outlineWidth,
                                center.x + halfLength,
                                edgeY + outlineWidth,
                                borderColor,
                                vertices,
                                triangles,
                                colors,
                                uv);
                        }
                        else if (direction == Vector2Int.down)
                        {
                            float edgeY = center.y - halfCell;
                            AddQuad(
                                center.x - halfLength,
                                edgeY - outlineWidth,
                                center.x + halfLength,
                                edgeY + outlineWidth,
                                borderColor,
                                vertices,
                                triangles,
                                colors,
                                uv);
                        }
                        else if (direction == Vector2Int.left)
                        {
                            float edgeX = center.x - halfCell;
                            AddQuad(
                                edgeX - outlineWidth,
                                center.y - halfLength,
                                edgeX + outlineWidth,
                                center.y + halfLength,
                                borderColor,
                                vertices,
                                triangles,
                                colors,
                                uv);
                        }
                        else
                        {
                            float edgeX = center.x + halfCell;
                            AddQuad(
                                edgeX - outlineWidth,
                                center.y - halfLength,
                                edgeX + outlineWidth,
                                center.y + halfLength,
                                borderColor,
                                vertices,
                                triangles,
                                colors,
                                uv);
                        }
                    }
                }
            }

            if (vertices.Count == 0)
            {
                return null;
            }

            var mesh = new Mesh
            {
                name = "Flood Fill Shape Border"
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool IsActive(bool[,] mask, int x, int y)
        {
            return x >= 0 && x < mask.GetLength(0) &&
                y >= 0 && y < mask.GetLength(1) && mask[x, y];
        }

        private static void AddQuad(
            float minX,
            float minY,
            float maxX,
            float maxY,
            Color color,
            List<Vector3> vertices,
            List<int> triangles,
            List<Color> colors,
            List<Vector2> uv)
        {
            int startIndex = vertices.Count;
            vertices.Add(new Vector3(minX, minY, 0f));
            vertices.Add(new Vector3(minX, maxY, 0f));
            vertices.Add(new Vector3(maxX, maxY, 0f));
            vertices.Add(new Vector3(maxX, minY, 0f));
            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
            for (int i = 0; i < 4; i++)
            {
                colors.Add(color);
                uv.Add(new Vector2(0.5f, 0.5f));
            }
        }
    }
}
