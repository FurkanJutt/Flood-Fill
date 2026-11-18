#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FloodFill.ThreeD;
using DG.Tweening;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace FloodFill.EditorTools
{
public static class FloodRotationPerformanceVerification
{
    // Run only in an isolated project copy: this replaces the open scene.
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use batch mode in an isolated project copy.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new Exception("A graphics device is required.");
        var camera=new GameObject("Benchmark camera").AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.045f,.09f);camera.fieldOfView=50;
        var light=new GameObject("Benchmark light").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-30,0);light.shadows=LightShadows.None;
        var target=new RenderTexture(512,512,24);target.Create();
        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FloodFill/Prefabs/VoxelCell3D.prefab");
        var palette=new[]{Color.red,Color.green,Color.blue,Color.yellow,Color.magenta,Color.cyan};
        VerifyPaletteMaterials(prefab, palette);
        var original=prefab.GetComponent<VoxelCell3D>().MeshRenderers[0].sharedMaterial;
        var lines=new List<string>{"GPU: "+SystemInfo.graphicsDeviceName,"512x512 synchronous URP camera render + GPU readback wait, 16 warmup / 32 rotating measured frames per case; not player FPS."};
        foreach(int size in new[]{12,20})
        {
            var root=new GameObject("Benchmark board");var cells=new List<VoxelCell3D>();
            for(int x=0;x<size;x++)for(int y=0;y<size;y++)for(int z=0;z<size;z++)
            {
                var cell=UnityEngine.Object.Instantiate(prefab,root.transform).GetComponent<VoxelCell3D>();
                cell.transform.position=new Vector3(x,y,z)*1.06f-Vector3.one*(size-1)*.53f;
                cell.Initialize(x,y,z,(x+y+z)%6,palette[(x+y+z)%6]);cell.SetShadowCasting(false);cells.Add(cell);
            }
            var block=new MaterialPropertyBlock();
            Color32[] baselinePixels=null;
            foreach(bool shared in new[]{false,true})
            {
                foreach(var cell in cells)
                {
                    if(shared) cell.SetColor(cell.ColorIndex,palette[cell.ColorIndex]);
                    else foreach(var renderer in cell.MeshRenderers)
                    {
                        renderer.sharedMaterial=original;
                        var color=palette[cell.ColorIndex];
                        block.SetColor("_BaseColor",color);
                        block.SetColor("_ColorDim",new Color(color.r*.55f,color.g*.58f,color.b*.68f,color.a));
                        renderer.SetPropertyBlock(block);block.Clear();
                    }
                }
                var sharedMaterials=new HashSet<Material>();
                foreach(var cell in cells)foreach(var renderer in cell.MeshRenderers)
                {
                    sharedMaterials.Add(renderer.sharedMaterial);
                    if(shared&&renderer.HasPropertyBlock())throw new Exception("Steady cubes retain a color override.");
                }
                if(shared&&sharedMaterials.Count!=palette.Length)throw new Exception("Palette materials are not shared across cubes.");
                var samples=new List<double>();
                for(int frame=0;frame<48;frame++)
                {
                    camera.transform.position=Quaternion.Euler(25,frame*13,0)*new Vector3(0,0,-size*3.2f);camera.transform.LookAt(Vector3.zero);
                    var watch=System.Diagnostics.Stopwatch.StartNew();
                    RenderPipeline.SubmitRenderRequest(camera,request);
                    var read=AsyncGPUReadback.Request(target);read.WaitForCompletion();
                    watch.Stop();if(read.hasError)throw new Exception("GPU readback failed");if(frame>=16)samples.Add(watch.Elapsed.TotalMilliseconds);
                    if(frame==47)
                    {
                        var pixels=read.GetData<Color32>().ToArray();
                        if(!shared)baselinePixels=pixels;
                        else
                        {
                            long difference=0;
                            for(int pixel=0;pixel<pixels.Length;pixel++)
                                difference+=Math.Abs(pixels[pixel].r-baselinePixels[pixel].r)+Math.Abs(pixels[pixel].g-baselinePixels[pixel].g)+Math.Abs(pixels[pixel].b-baselinePixels[pixel].b);
                            double meanDifference=difference/(pixels.Length*3.0);
                            if(meanDifference>1)throw new Exception("Palette rendering differs visibly from the original: "+meanDifference);
                            lines.Add($"{size}^3 visual comparison: mean RGB difference {meanDifference:0.000} / 255.");
                        }
                    }
                }
                samples.Sort();lines.Add($"{size}^3 ({cells.Count} cubes), {(shared?"shared palette materials":"per-cube color blocks")}: median {samples[16]:0.00} ms, p95 {samples[30]:0.00} ms, mean {samples.Average():0.00} ms; shared materials={sharedMaterials.Count}.");
            }
            UnityEngine.Object.DestroyImmediate(root);
        }
        Directory.CreateDirectory("Logs");File.WriteAllLines("Logs/FloodRotationPerformance.txt",lines);Debug.Log(string.Join("\n",lines));target.Release();UnityEngine.Object.DestroyImmediate(target);
    }
    private static void VerifyPaletteMaterials(GameObject prefab,Color[] palette)
    {
        var cells=new List<VoxelCell3D>();
        var source=prefab.GetComponent<VoxelCell3D>().MeshRenderers[0].sharedMaterial;
        var originalColor=source.GetColor("_BaseColor");
        for(int i=0;i<12;i++)
        {
            var cell=UnityEngine.Object.Instantiate(prefab).GetComponent<VoxelCell3D>();
            cell.Initialize(i,0,0,i%6,palette[i%6]);cells.Add(cell);
        }
        for(int i=0;i<6;i++)
            if(cells[i].MeshRenderers[0].sharedMaterial!=cells[i+6].MeshRenderers[0].sharedMaterial)
                throw new Exception("Identical colors must share the same palette material.");
        if(source.GetColor("_BaseColor")!=originalColor)throw new Exception("Authored material was modified.");
        var first=cells[0];var renderer=first.MeshRenderers[0];
        int materialCount=((System.Collections.IDictionary)typeof(VoxelCell3D).GetField("ColorMaterials",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)).Count;
        first.AnimateColor(1,palette[1],.1f);
        var sequence=(Sequence)typeof(VoxelCell3D).GetField("colorWaveSequence",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(first);
        sequence.SetUpdate(UpdateType.Manual);DOTween.ManualUpdate(.15f,.15f);
        if(!renderer.HasPropertyBlock())throw new Exception("The wave must still animate its color.");
        DOTween.ManualUpdate(1,1);
        if(renderer.HasPropertyBlock()||renderer.sharedMaterial!=cells[1].MeshRenderers[0].sharedMaterial)
            throw new Exception("Completed wave did not restore the shared target palette material.");
        if(((System.Collections.IDictionary)typeof(VoxelCell3D).GetField("ColorMaterials",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)).Count!=materialCount)
            throw new Exception("Animation created intermediate color materials.");
        first.AnimateColor(2,palette[2],0);first.SetColor(3,palette[3]);
        if(renderer.HasPropertyBlock()||renderer.sharedMaterial!=cells[3].MeshRenderers[0].sharedMaterial)
            throw new Exception("Interrupted wave did not restore steady batching.");
        first.AnimateColor(2,palette[2],0);DOTween.ManualUpdate(.1f,.1f);first.ResetForPool();first.Initialize(0,0,0,0,palette[0]);
        if(renderer.HasPropertyBlock()||renderer.sharedMaterial!=cells[6].MeshRenderers[0].sharedMaterial)
            throw new Exception("Pooled voxel did not reuse its original palette material.");
        foreach(var cell in cells)UnityEngine.Object.DestroyImmediate(cell.gameObject);
        Debug.Log("PASS shared palette colors, authored material preservation, temporary animation overrides, completion/interruption cleanup, no transient materials, and pooled reuse.");
    }
}
}
#endif
