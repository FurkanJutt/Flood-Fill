using System;
using System.IO;
using System.Reflection;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Solver;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FloodFill.EditorTools
{
    public static class FloodSplashGameplayVerification3D
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static FloodFillMobileUI3D ui;
        private static FloodFillGameManager3D game;
        private static FloodFillSplashVFX splash;
        private static int stage,checks,moves,peak;
        private static float ready;
        private static double deadline;
        private static bool captured;
        private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            PlayerSettings.companyName="FloodSplashGameplayVerification";PlayerSettings.productName="FloodSplashGameplayVerification";
            PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Gameplay splash timeout at stage "+stage);
                ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();
                if(ui==null||!ui.IsBuilt)return;
                if(stage==0){Screen.SetResolution(420,900,false);ready=Time.time+.25f;stage=1;return;}
                if(stage==1){if(Time.time<ready)return;ui.StartSelectedLevel(0,0,1);stage=2;return;}
                if(stage==2)
                {
                    if(!game.CanInteract)return;
                    splash=game.BoardManager.BoardTransform.GetComponentInChildren<FloodFillSplashVFX>();
                    Check(splash!=null&&splash.board==game.BoardManager,"Live scene splash stays bound after main-menu preview reparenting");
                    Check(splash.splashIntensity==.25f&&splash.maximumActiveParticles==80,"Visible defaults retain baseline intensity and fixed pool");
                    Move();stage=3;return;
                }
                if(stage==3)
                {
                    peak=Math.Max(peak,splash.ActiveParticleCount);
                    if(!captured&&splash.ActiveParticleCount>=4)
                    {
                        // Let the actual runtime mesh have a nonzero size before checking portrait visibility.
                        ready=Time.time+.12f;stage=4;return;
                    }
                    if(Time.time<ready)return;
                    if(moves<3&&game.CanInteract){Move();return;}
                    Check(peak>0,"Normal color-button/cube-click gameplay emits visible particles");
                    splash.enabled=false;
                    typeof(FloodFillSplashVFX).GetField("random",Flags).SetValue(splash,null);
                    splash.enabled=true;
                    Check(typeof(FloodFillSplashVFX).GetField("random",Flags).GetValue(splash)!=null,"Re-enable recovers missing transient resources after script reload");
                    Move();stage=5;return;
                }
                if(stage==4)
                {
                    peak=Math.Max(peak,splash.ActiveParticleCount);
                    if(Time.time<ready)return;
                    Time.timeScale=0;
                    var renderer=splash.GetComponent<MeshRenderer>();Check(renderer.enabled&&renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount>0,"Actual scene renders splash mesh");
                    var camera=Camera.main;var with=Capture(camera,"/tmp/FloodSplashGameplay.png");renderer.enabled=false;var without=Capture(camera,"/tmp/FloodSplashGameplayWithout.png");renderer.enabled=true;
                    int changed=0;for(int i=0;i<with.Length;i++)if(Math.Abs(with[i].r-without[i].r)+Math.Abs(with[i].g-without[i].g)+Math.Abs(with[i].b-without[i].b)>12)changed++;
                    Check(changed>=120,"Splash changes at least 120 clearly colored pixels at 420x900 gameplay framing (actual "+changed+")");
                    Time.timeScale=1;captured=true;ready=Time.time+game.BoardManager.LastRecolorAnimationDuration+.45f;stage=3;return;
                }
                if(stage==5)
                {
                    peak=Math.Max(peak,splash.ActiveParticleCount);
                    if(Time.time<ready)return;
                    Check(splash.EmittedBurstCount>0,"Gameplay splashes continue after transient-resource recovery");Finish();
                }
            }
            catch(Exception e){Time.timeScale=1;Debug.LogException(e);File.WriteAllText("/tmp/FloodSplashGameplayVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
        private static void Move()
        {
            var board=game.BoardManager;board.TryCreateSolverSnapshot(out var snapshot);FloodFillRegionGraph3D.TryBuild(snapshot,out var graph);
            // Use the real selected-color/click path with an exposed front voxel, as a player would.
            VoxelCell3D cell=board.GetCell(board.Width/2,board.Height-1,board.Depth/2);
            for(int x=0;x<board.Width&&cell==null;x++)for(int y=board.Height-1;y>=0&&cell==null;y--)for(int z=0;z<board.Depth;z++)
            {var candidate=board.GetCell(x,y,z);if(candidate!=null&&z==0){cell=candidate;break;}}
            if(cell==null)cell=board.StartingVoxel;
            int color=(cell.ColorIndex+1)%6;int before=game.MoveCount;
            game.SelectColor(color);typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Flags).Invoke(game,new object[]{cell});
            Check(game.MoveCount==before+1,"Actual gameplay accepts color/cube input");moves++;
            ready=Time.time+board.LastRecolorAnimationDuration+.45f;
        }
        private static Color32[] Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(420,900,24);var old=camera.targetTexture;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(420,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,420,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());var pixels=image.GetPixels32();
            RenderTexture.active=null;camera.targetTexture=old;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);return pixels;
        }
        private static void Finish()
        {
            Check(captured&&peak<=80,"Live gameplay visibility checked within 80-particle cap");
            File.WriteAllText("/tmp/FloodSplashGameplayVerification.txt",checks+" checks passed in Unity "+Application.unityVersion+". Actual demo main menu to Level 1, four selected-color/cube-click moves, stable board subscription after menu preview, emitted particles, transient-resource recovery and 420x900 GPU pixel comparison. Peak "+peak+" particles. Isolated player-save identity.\n");EditorApplication.Exit(0);
        }
    }
}
