using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Audio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.EditorTools
{
    /// <summary>Run in an isolated copy: round-trip authored overrides, then exercise their persistent events.</summary>
    public static class FloodFillUIPrefabVerification
    {
        private const string ClonePath = "Assets/FloodFill/Generated/UIAuthoringVerification.prefab";
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int checks, stage, buttonCount;
        private static double deadline;
        private static byte[] original;
        private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Flags).GetValue(owner);

        public static void BeginBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use batch mode in an isolated project copy.");
            checks=0;stage=0;
            original=File.ReadAllBytes(FloodFillUIPrefabSetup.CanvasPath);
            Check(AssetDatabase.FindAssets("t:Prefab", new[]{FloodFillUIPrefabSetup.Folder}).Length==9, "Canvas and eight screen prefabs exist");
            GameObject root=PrefabUtility.LoadPrefabContents(FloodFillUIPrefabSetup.CanvasPath);
            try
            {
                var ui=root.GetComponent<FloodFillMobileUI3D>();
                Check(Read<FloodFillGameManager3D>(ui,"manager")==null,"Reusable Canvas has no external scene dependency");
                foreach(var screen in ui.EditorScreens)
                    Check(screen!=null&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(screen)==FloodFillUIPrefabSetup.Folder+"/"+screen.name+".prefab","Screen has nested prefab connection: "+screen.name);
                foreach(var button in root.GetComponentsInChildren<Button>(true))
                    Check(button.onClick.GetPersistentEventCount()==1&&button.onClick.GetPersistentTarget(0)==button.GetComponent<FloodFillUIAction3D>(),"Persistent button event: "+button.name);
                var play=root.GetComponentsInChildren<FloodFillUIAction3D>(true).Single(value=>value.action==FloodFillUIAction.Play).GetComponent<Button>();
                play.name="RenamedPlayControl";
                play.GetComponent<RectTransform>().sizeDelta=new Vector2(837,177);
                play.GetComponentInChildren<TMP_Text>(true).text="MY CUSTOM PLAY";
                play.GetComponentInChildren<TMP_Text>(true).fontSize=53;
                var toggle=Read<Toggle>(ui,"showCurrentBlockToggle");toggle.name="RenamedBlockToggle";
                toggle.GetComponent<RectTransform>().sizeDelta=new Vector2(123,67);
                toggle.transform.parent.GetComponent<RectTransform>().anchoredPosition=new Vector2(28,253);
                var choice=Read<System.Collections.Generic.List<Button>>(ui,"difficultyButtons")[1];
                var visual=choice.GetComponent<FloodFillUIState3D>();visual.selectedColor=Color.green;
                Directory.CreateDirectory("Assets/FloodFill/Generated");AssetDatabase.Refresh();
                PrefabUtility.SaveAsPrefabAsset(root,ClonePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            var originalUi=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();
            UnityEngine.Object.DestroyImmediate(originalUi.gameObject);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ClonePath));
            PlayerSettings.companyName="FloodUIPrefabVerification";PlayerSettings.productName="FloodUIPrefabVerification";
            foreach(string key in new[]{"FloodFill3D.SetupDifficulty","FloodFill3D.SetupMode","FloodFill3D.SetupSize"})PlayerPrefs.DeleteKey(key);
            deadline=EditorApplication.timeSinceStartup+90;
            EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("UI authoring verification timed out");
                var ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();
                if(ui==null||!ui.IsBuilt)return;
                var manager=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();
                if(stage==0)
                {
                    Check(UnityEngine.Object.FindObjectsByType<FloodFillMobileUI3D>(FindObjectsInactive.Include).Length==1,"One prefab controller, no bootstrap duplicates");
                    var play=ui.GetComponentsInChildren<FloodFillUIAction3D>(true).Single(value=>value.action==FloodFillUIAction.Play).GetComponent<Button>();
                    Check(play.name=="RenamedPlayControl"&&play.GetComponent<RectTransform>().sizeDelta==new Vector2(837,177),"Renamed button and edited size survive prefab round-trip and startup");
                    var text=play.GetComponentInChildren<TMP_Text>(true);
                    Check(text.text=="MY CUSTOM PLAY"&&text.fontSize==53,"Authored caption and font size survive startup");
                    var toggle=Read<Toggle>(ui,"showCurrentBlockToggle");
                    Check(toggle.name=="RenamedBlockToggle"&&toggle.GetComponent<RectTransform>().sizeDelta==new Vector2(123,67),"Switch layout is not restyled at runtime");
                    Check(toggle.transform.parent.GetComponent<RectTransform>().anchoredPosition==new Vector2(28,253),"Authored switch row position survives startup");
                    ui.ExecuteAuthoredAction(FloodFillUIAction.Settings,0);
                    toggle.isOn=!toggle.isOn;
                    Check(toggle.GetComponent<RectTransform>().sizeDelta==new Vector2(123,67),"Switch changes preserve manual size");
                    Check(Read<System.Collections.Generic.List<Button>>(ui,"difficultyButtons")[1].image.color==Color.green,"Authored selection color is used");
                    buttonCount=ui.GetComponentsInChildren<Button>(true).Length;
                    play.onClick.Invoke();stage=1;
                }
                else if(!manager.IsLoading&&!manager.BoardManager.IsStartAnimationPlaying&&manager.State==FloodFillGameManager3D.GameState.Playing)
                {
                    Check(manager.CanInteract,"Renamed Play button launches a complete playable level");
                    var palette=ui.GetComponentsInChildren<FloodFillUIAction3D>(true).First(value=>value.action==FloodFillUIAction.Color&&value.index==0);
                    palette.name="RenamedPaletteControl";
                    var audio=FloodAudioManager.Instance;
                    audio.colorSelected.clip=AudioClip.Create("Prefab color cue",1024,1,22050,false);
                    audio.buttonClick.clip=AudioClip.Create("Prefab click cue",1024,1,22050,false);
                    int selected=audio.SoundStartCount(FloodSound.ColorSelected), clicks=audio.SoundStartCount(FloodSound.ButtonClick);
                    palette.GetComponent<Button>().onClick.Invoke();
                    Check(manager.SelectedColorIndex==0,"Renamed palette control keeps its color action");
                    Check(audio.SoundStartCount(FloodSound.ColorSelected)==selected+1&&audio.SoundStartCount(FloodSound.ButtonClick)==clicks,"Renamed palette control emits one color cue without an extra generic click");
                    Check(ui.GetComponentsInChildren<Button>(true).Length==buttonCount,"Opening screens and launching does not regenerate controls");
                    Check(File.ReadAllBytes(FloodFillUIPrefabSetup.CanvasPath).SequenceEqual(original),"Verification leaves the original authored prefab intact");
                    Finish(null);
                }
            }
            catch(Exception error){Finish(error);}
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update-=Tick;
            string result=error==null?$"PASS: {checks} prefab checks, including nested screens, persistent button events, renamed controls, saved layout/text/state edits, single initialization and playable launch.":"FAILED: "+error;
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/FloodUIPrefabVerification.txt",result+"\n");
            Debug.Log(result);EditorApplication.Exit(error==null?0:1);
        }
    }
}
