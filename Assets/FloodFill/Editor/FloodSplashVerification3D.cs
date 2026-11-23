using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using FloodFill.ThreeD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FloodFill.EditorTools
{
    public static class FloodSplashVerification3D
    {
        private static readonly Color[] Palette={new Color(.25f,.78f,.38f),new Color(.62f,.32f,.86f),new Color(.98f,.82f,.20f),new Color(1,.52f,.16f),new Color(.20f,.48f,.95f),new Color(.30f,.82f,.80f)};
        private static readonly int[] Sizes={6,8,10,20};
        private static readonly VoxelBoardManager3D.VoxelVolumeMode[] Modes={VoxelBoardManager3D.VoxelVolumeMode.HollowCube,VoxelBoardManager3D.VoxelVolumeMode.Procedural,VoxelBoardManager3D.VoxelVolumeMode.Items};
        private static int stage,index,checks,peak,lastBurst;
        private static double ready,deadline;
        private static VoxelBoardManager3D board;
        private static FloodFillSplashVFX splash;
        private static GameObject fixture;
        private static Camera camera;
        private static string logicalResult;
        private static float duration;
        private static Quaternion initialRotation;
        private static int captureCount,colorIndex;
        private static double captureReady;
        private static readonly List<string> report=new List<string>();
        private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message+" (case "+index+", stage "+stage+")");}
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            var connected=UnityEngine.Object.FindObjectsByType<FloodFillSplashVFX>(FindObjectsInactive.Include);
            Check(connected.Length==1&&connected[0].GetComponentInParent<VoxelBoardManager3D>()!=null,"Demo scene has exactly one connected splash prefab");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            FloodSplashSetup3D.EnsureAssets();
            deadline=EditorApplication.timeSinceStartup+240;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Setup()
        {
            if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);
            fixture=new GameObject("Splash Verification");
            camera=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(fixture.transform);camera.tag="MainCamera";
            int size=Sizes[index%4];camera.transform.position=new Vector3(size*1.5f,size*1.15f,-size*1.8f);camera.transform.LookAt(Vector3.zero);camera.fieldOfView=45;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.04f,.07f);
            var light=new GameObject("Light",typeof(Light)).GetComponent<Light>();light.transform.SetParent(fixture.transform);light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(40,-30,0);
            var owner=new GameObject("Board",typeof(VoxelBoardManager3D));owner.transform.SetParent(fixture.transform);board=owner.GetComponent<VoxelBoardManager3D>();
            var root=new GameObject("Voxels").transform;root.SetParent(owner.transform);
            board.Configure(root,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FloodFill/Prefabs/VoxelCell3D.prefab").GetComponent<VoxelCell3D>(),size,size,size,1,.01f,Modes[index/4]);
            board.NumberedLevelSeed=1313;board.ProceduralSettings.useRandomSeed=false;
            typeof(VoxelBoardManager3D).GetField("waveStepDelay",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(board,index%2==0?.065f:.025f);
            Check(board.GenerateBoard(Palette),"Board generates");
            splash=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FloodSplashSetup3D.PrefabPath),root).GetComponent<FloodFillSplashVFX>();splash.viewCamera=camera;
            Check(splash.board==board&&splash.GetComponent<MeshRenderer>().sharedMaterial.shader.isSupported,"Prefab connects to board and URP shader is supported");
            Check(splash.GetComponentsInChildren<Collider>().Length==0,"Effect adds no input colliders");
            peak=lastBurst=0;Paint();
            Check(board.RecolorConnectedRegion(Origin(),1),"Actual flood recolor starts");
            Check(splash.EmittedBurstCount==0,"No splash before actual tween callbacks");
            Check(splash.PendingBurstCount>0&&splash.PendingBurstCount<=splash.maximumBurstsPerWave,"Sampled wavefront queue is nonempty and bounded");
            duration=board.LastRecolorAnimationDuration;board.TryCapturePersistentState(out var state);logicalResult=JsonUtility.ToJson(state);
            initialRotation=root.localRotation;
            ready=Time.time+duration+.5;stage=1;
        }
        private static void Paint()
        {
            for(int x=0;x<board.Width;x++)for(int y=0;y<board.Height;y++)for(int z=0;z<board.Depth;z++)
            {var cell=board.GetCell(x,y,z);if(cell!=null){int color=x<board.Width/2?0:1;cell.SetColor(color,Palette[color]);}}
        }
        private static VoxelCell3D Origin()
        {
            for(int x=0;x<board.Width/2;x++)for(int y=board.Height-1;y>=0;y--)for(int z=0;z<board.Depth;z++)
            {var cell=board.GetCell(x,y,z);if(cell!=null&&cell.ColorIndex==0)return cell;}
            throw new Exception("No test origin");
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Splash test timeout");
                if(stage==0){Setup();return;}
                peak=Math.Max(peak,splash.ActiveParticleCount);
                if(splash.EmittedBurstCount!=lastBurst)
                {
                    lastBurst=splash.EmittedBurstCount;
                    Check(splash.LastBurstColor==Palette[1]||stage>=3,"Wave inherits selected palette color");
                    Check(splash.LastBurstNormal.sqrMagnitude>.99f,"Splash has an exposed surface normal");
                    Check(splash.LastBurstPosition.magnitude>board.VoxelWidth*.5f,"Emission follows voxel surfaces, not board center");
                }
                if(splash.ActiveParticleCount>splash.maximumActiveParticles)throw new Exception("Active particle cap exceeded");
                if(stage==1)
                {
                    if(index==0&&splash.ActiveParticleCount>=8&&captureCount<3&&Time.time>=captureReady)
                    {
                        Capture("/tmp/FloodSplashWave"+captureCount+".png");
                        if(captureCount==0)
                        {
                            var image=CapturePixels();var renderer=splash.GetComponent<MeshRenderer>();renderer.enabled=false;
                            var baseline=CapturePixels();renderer.enabled=true;
                            Check(image.Where((pixel,i)=>!pixel.Equals(baseline[i])).Count()>5,"Live splash shader visibly renders over the actual board");
                        }
                        captureCount++;captureReady=Time.time+.10;
                    }
                    // Exercise camera motion and board-local particle coordinates during propagation.
                    camera.transform.RotateAround(Vector3.zero,Vector3.up,Time.unscaledDeltaTime*12);camera.transform.LookAt(Vector3.zero);
                    board.BoardTransform.localRotation=initialRotation*Quaternion.Euler(0,(float)(Math.Sin(Time.time)*3),0);
                    if(Time.time<ready)return;
                    Check(splash.EmittedBurstCount>0&&peak>0,"Actual recolor/capture tween wave emitted splashes");
                    Check(splash.PendingBurstCount==0&&splash.ActiveParticleCount==0,"All wave hooks and particles expire");
                    Check(splash.EmittedBurstCount<=splash.maximumBurstsPerWave,"Total wave burst cap respected");
                    Check(peak<=splash.maximumActiveParticles,"Per-frame particle budget checks passed");
                    splash.enabled=false;Check(board.GenerateBoard(Palette),"Disabled fixture regenerates");Paint();board.RecolorConnectedRegion(Origin(),1);
                    board.TryCapturePersistentState(out var state);
                    Check(JsonUtility.ToJson(state)==logicalResult&&Mathf.Approximately(board.LastRecolorAnimationDuration,duration),"Enabled/disabled effect produces identical board state and wave duration");
                    report.Add(Modes[index/4]+" "+Sizes[index%4]+" cubed: "+board.TotalVoxelCount+" voxels; peak "+peak+" particles; state/timing parity passed.");
                    if(++index<12){stage=0;return;}
                    splash.enabled=true;splash.splashIntensity=1;splash.wavefrontSamplingDensity=1;splash.spawnProbability=1;splash.streakProbability=1;splash.streakCount=new Vector2Int(2,2);
                    splash.dropletCount=new Vector2Int(6,6);splash.maximumActiveParticles=12;splash.ReloadVFXSettings();
                    Paint();board.RecolorConnectedRegion(Origin(),1);ready=Time.time+.25;stage=2;return;
                }
                if(stage==2)
                {
                    if(Time.time<ready)return;
                    Check(splash.ActiveParticleCount<=12,"Stress setting obeys lowered 12-particle pool");
                    board.RecolorConnectedRegion(board.StartingVoxel,2);
                    Check(splash.ActiveParticleCount==0&&splash.EmittedBurstCount==0,"Rapid next move immediately recycles previous particles");
                    Check(splash.PendingBurstCount<=splash.maximumBurstsPerWave,"Rapid next move cannot accumulate wave subscriptions");
                    splash.StopAllSplashEffects();Check(splash.PendingBurstCount==0&&splash.ActiveParticleCount==0,"Stop clears all particles and subscriptions");
                    splash.maximumActiveParticles=80;splash.ReloadVFXSettings();splash.PreviewFullWave();
                    board.TryCapturePersistentState(out var state);logicalResult=JsonUtility.ToJson(state);ready=Time.time+.7;stage=3;return;
                }
                if(stage==3)
                {
                    if(Time.time<ready)return;
                    board.TryCapturePersistentState(out var state);Check(JsonUtility.ToJson(state)==logicalResult,"Full-wave preview leaves gameplay state unchanged");
                    splash.StopAllSplashEffects();
                    splash.PreviewSplash();Check(splash.ActiveParticleCount>0,"Inspector preview emits while game is paused");
                    ready=Time.time+.06;stage=30;return;
                }
                if(stage==30)
                {
                    if(Time.time<ready)return;
                    Time.timeScale=0;ready=EditorApplication.timeSinceStartup+.25;stage=4;return;
                }
                if(stage==4)
                {
                    if(EditorApplication.timeSinceStartup<ready)return;
                    Check(splash.ActiveParticleCount>0,"Paused splash particles freeze with the wave");
                    Time.timeScale=1;splash.StopAllSplashEffects();splash.enableSplash=false;
                    splash.PreviewSplash();Check(splash.ActiveParticleCount==0,"Enable Splash toggle suppresses preview emissions");
                    splash.enableSplash=true;splash.PreviewFullWave();board.ClearBoard();
                    Check(splash.PendingBurstCount==0&&splash.ActiveParticleCount==0,"Clearing board cancels queued preview and active splash particles");
                    Check(!ShaderUtil.ShaderHasError(Shader.Find("FloodFill/Liquid Splash")),"Splash shader compiles without errors");
                    Check(board.GenerateBoard(Palette),"Palette test board generates");
                    colorIndex=0;
                    for(int x=0;x<board.Width;x++)for(int y=0;y<board.Height;y++)for(int z=0;z<board.Depth;z++){var cell=board.GetCell(x,y,z);if(cell!=null)cell.SetColor(1,Palette[1]);}
                    board.RecolorConnectedRegion(board.StartingVoxel,0);ready=Time.time+board.LastRecolorAnimationDuration+.4;stage=5;return;
                }
                if(stage==5)
                {
                    if(Time.time<ready)return;
                    Check(splash.EmittedBurstCount>0&&splash.LastBurstColor==Palette[colorIndex],"Selected color "+colorIndex+" drives splash RGB");
                    if(++colorIndex<6){board.RecolorConnectedRegion(board.StartingVoxel,colorIndex);ready=Time.time+board.LastRecolorAnimationDuration+.4;return;}
                    report.Insert(0,checks+" checks passed. Unity "+Application.unityVersion+"; GPU "+SystemInfo.graphicsDeviceName+". Isolated project; no original saves touched.");
                    report.Add("Default 80-particle budget; 12-particle stress pool; two wave speeds; camera orbit and board rotation; color, callback synchronization, rapid transitions, visual-only preview, pause, disable, reload and clear tested. Mobile hardware profiling remains a device check.");
                    File.WriteAllLines("/tmp/FloodSplashVerification.txt",report);EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Time.timeScale=1;Debug.LogException(e);File.WriteAllText("/tmp/FloodSplashVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
        private static void Capture(string path)
        {
            var rt=new RenderTexture(640,640,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
        }
        private static Color32[] CapturePixels()
        {
            var rt=new RenderTexture(640,640,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();var pixels=image.GetPixels32();
            RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);return pixels;
        }
    }
}
