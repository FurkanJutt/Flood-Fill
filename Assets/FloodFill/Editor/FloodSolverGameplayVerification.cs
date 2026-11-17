using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Solver;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FloodFill.EditorTools
{
    public static class FloodSolverGameplayVerification
    {
        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        [MenuItem("Tools/Flood Fill/Verify Solver Against Gameplay")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run this isolated verification outside Play Mode.");
            var report = new List<string>();
            Scene scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Solver verification (temporary)");
            SceneManager.MoveGameObjectToScene(root, scene);
            var boardRoot = new GameObject("Board"); boardRoot.transform.SetParent(root.transform);
            var prefabObject = new GameObject("Voxel prototype"); prefabObject.transform.SetParent(root.transform);
            var prefab = prefabObject.AddComponent<VoxelCell3D>();
            prefab.Configure(prefab.transform, Array.Empty<MeshRenderer>());
            var board = root.AddComponent<VoxelBoardManager3D>(); board.enabled = false;
            Set(board, "boardRoot", boardRoot.transform); Set(board, "voxelPrefab", prefab);
            var manager = root.AddComponent<FloodFillGameManager3D>(); manager.enabled = false;
            Set(manager, "boardManager", board);
            // No game session is started: verification never saves/deletes the player's session.
            Set(manager, "currentMoveBudget", 100000);
            var palette = new[] { Color.red, Color.green, Color.blue, Color.yellow, Color.magenta, Color.cyan };
            Set(manager, "colors", palette);
            var click = typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked", BindingFlags.Instance | BindingFlags.NonPublic);
            StackTraceLogType previousLogTrace = Application.GetStackTraceLogType(LogType.Log);
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            try
            {
                foreach (int size in new[] { 6, 8, 10, 12, 16 })
                foreach (int seed in new[] { 1103, 2207 })
                foreach (string shapeName in new[] { "shell", "irregular", "procedural" })
                {
                    bool irregular = shapeName == "irregular";
                    bool[,,] mask = null;
                    if (shapeName == "procedural")
                    {
                        Require(ProceduralVoxelShapeGenerator.TryGenerate(size,size,size,
                            new ProceduralVoxelShapeSettings {useRandomSeed=false,logGenerationDetails=false,logGenerationPerformance=false},
                            seed,out var shape), "Procedural shape generation");
                        mask=shape.Mask;
                    }
                    var random = new System.Random(seed); var voxels = new List<VoxelSaveData3D>();
                    for (int z = 0; z < size; z++) for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    {
                        if (mask != null ? !mask[x,y,z] : irregular ? y > 0 && (y > (x + z) % size || x % 3 == 1 && z % 3 == 1)
                            : x > 0 && y > 0 && z > 0 && x < size - 1 && y < size - 1 && z < size - 1) continue;
                        voxels.Add(new VoxelSaveData3D { x=x, y=y, z=z, colorIndex=random.Next(6), starting=voxels.Count==0, captured=voxels.Count==0 });
                    }
                    var saved = new VoxelBoardSaveData3D { width=size,height=size,depth=size,volumeMode=0,currentPlayerColor=voxels[0].colorIndex,voxels=voxels.ToArray() };
                    Require(board.RestorePersistentState(saved,palette), "Restore board");
                    Require(board.TryCreateSolverSnapshot(out var snapshot), "Capture snapshot");
                    Require(FloodFillRegionGraph3D.TryBuild(snapshot,out var graph), "Build graph");
                    var result = FloodFillMoveSolver3D.Solve(graph, new MoveSolverSettings(), true);
                    var simulator = new FloodMoveSimulator(graph); byte[] expected = (byte[])graph.RegionColors.Clone();
                    Set(manager,"<State>k__BackingField",FloodFillGameManager3D.GameState.Playing);
                    Set(manager,"<MoveCount>k__BackingField",0);
                    for (int m=0;m<result.BestMoveCount;m++)
                    {
                        int logical=graph.RepresentativeLogicalIndices[result.BestRegionSequence[m]];
                        var voxel=board.GetCell(logical%size,logical/size%size,logical/(size*size));
                        manager.SelectColor(result.BestColorSequence[m]);
                        click.Invoke(manager,new object[]{voxel});
                        Require(manager.MoveCount==m+1,"Exactly one gameplay move per solver move");
                        expected=simulator.Apply(expected,new FloodFillSolverMove3D(result.BestRegionSequence[m],result.BestColorSequence[m]));
                        int[] indices=snapshot.LogicalIndices;
                        for(int i=0;i<indices.Length;i++)
                        {
                            logical=indices[i];var actual=board.GetCell(logical%size,logical/size%size,logical/(size*size));
                            Require(actual.ColorIndex==expected[graph.VoxelRegionIds[i]],"Voxel color differs from simulator");
                        }
                        Require(board.CapturedVoxelCount==simulator.CountComponent(expected,result.BestRegionSequence[m]),"Captured mass differs from simulator");
                    }
                    Require(board.IsFullyCaptured,"Gameplay solution does not finish board");
                    Require(manager.CurrentMoveBudget==100000,"Playback changed existing move budget");
                    report.Add($"PASS {size} {shapeName} seed={seed}, voxels={graph.TotalVoxelCount}, regions={graph.RegionCount}, moves={result.BestMoveCount}: actual manager handler, every voxel and captured count match.");
                }
                // Exercise procedural shapes with the same shared snapshot/graph pipeline in the existing profiler.
                report.Add("Gameplay verification passed. Animation appearance/timing requires visual Play Mode inspection; these checks exercise actual logical handlers synchronously.");
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/FloodSolverGameplayVerification.txt",report);
                Debug.Log(string.Join("\n",report));
            }
            finally { Application.SetStackTraceLogType(LogType.Log,previousLogTrace); UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
