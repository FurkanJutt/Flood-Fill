#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using FloodFill.ThreeD.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

namespace FloodFill.EditorTools
{
    /// <summary>Editor-only asset authoring. Internal mixer authoring APIs never enter a player.</summary>
    public static class FloodAudioSetup
    {
        public const string MixerPath="Assets/FloodFill/Audio/FloodFillAudio.mixer";
        public const string PrefabPath="Assets/FloodFill/Audio/FloodAudioManager.prefab";
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static Type EditorType(string name)
        {
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {var type=assembly.GetType(name);if(type!=null)return type;}
            throw new InvalidOperationException("Unity editor audio authoring API unavailable: "+name);
        }
        private static object Get(object target,string name)=>target.GetType().GetProperty(name,Flags).GetValue(target);
        private static void Set(object target,string name,object value)=>target.GetType().GetProperty(name,Flags).SetValue(target,value);
        private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
        [MenuItem("Tools/Flood Fill/Audio/Create or Repair Audio Assets")]
        public static void CreateOrRepair()
        {
            Directory.CreateDirectory("Assets/FloodFill/Audio");AssetDatabase.Refresh();
            AudioMixer mixer=AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if(mixer==null)
            {
                var create=EditorType("UnityEditor.Audio.AudioMixerController").GetMethod("CreateMixerControllerAtPath",Flags);
                mixer=(AudioMixer)create.Invoke(null,new object[]{MixerPath});
            }
            var master=Get(mixer,"masterGroup");
            var groups=mixer.FindMatchingGroups("Music");
            var music=groups.Length>0?groups[0]:(AudioMixerGroup)Call(mixer,"CreateNewGroup","Music",true);
            groups=mixer.FindMatchingGroups("SFX");
            var effects=groups.Length>0?groups[0]:(AudioMixerGroup)Call(mixer,"CreateNewGroup","SFX",true);
            var children=Array.CreateInstance(master.GetType(),2);children.SetValue(music,0);children.SetValue(effects,1);Set(master,"children",children);
            // Keep the group volume controls exposed, using their actual native parameter GUIDs.
            Type parameterType=EditorType("UnityEditor.Audio.ExposedAudioParameter");
            var exposed=Array.CreateInstance(parameterType,3);
            object[] volumeGroups={master,music,effects};string[] names={"MasterVolume","MusicVolume","SfxVolume"};
            for(int i=0;i<3;i++)
            {
                object parameter=Activator.CreateInstance(parameterType);
                parameterType.GetField("guid",Flags).SetValue(parameter,Call(volumeGroups[i],"GetGUIDForVolume"));
                parameterType.GetField("name",Flags).SetValue(parameter,names[i]);exposed.SetValue(parameter,i);
            }
            Set(mixer,"exposedParameters",exposed);
            var snapshot=Get(mixer,"TargetSnapshot");
            foreach(object group in volumeGroups)Call(group,"SetValueForVolume",mixer,snapshot,0f);
            AddCompressor(mixer,master,snapshot,-3f);
            AddCompressor(mixer,effects,snapshot,-10f);
            mixer.updateMode=AudioMixerUpdateMode.UnscaledTime;
            Array views=(Array)Get(mixer,"views");
            if(views.Length==0)
            {
                Type viewType=EditorType("UnityEditor.Audio.MixerGroupView");
                object view=Activator.CreateInstance(viewType);viewType.GetField("name",Flags).SetValue(view,"All");
                var guidArray=Array.CreateInstance(Call(master,"GetGUIDForVolume").GetType(),3);
                for(int i=0;i<3;i++)guidArray.SetValue(Get(volumeGroups[i],"groupID"),i);
                viewType.GetField("guids",Flags).SetValue(view,guidArray);
                var allViews=Array.CreateInstance(viewType,1);allViews.SetValue(view,0);Set(mixer,"views",allViews);Set(mixer,"currentViewIndex",0);
            }
            EditorUtility.SetDirty(mixer);EditorUtility.SetDirty((UnityEngine.Object)master);EditorUtility.SetDirty(music);EditorUtility.SetDirty(effects);
            bool newPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null;
            GameObject root=newPrefab?new GameObject("Flood Audio Manager"):PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var audio=root.GetComponent<FloodAudioManager>();if(audio==null)audio=root.AddComponent<FloodAudioManager>();
                var serialized=new SerializedObject(audio);serialized.FindProperty("mixer").objectReferenceValue=mixer;
                serialized.FindProperty("musicGroup").objectReferenceValue=music;serialized.FindProperty("effectsGroup").objectReferenceValue=effects;serialized.FindProperty("feedbackGroup").objectReferenceValue=(AudioMixerGroup)master;serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally {if(newPrefab)UnityEngine.Object.DestroyImmediate(root);else PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("Audio assets ready: Master > Music / SFX, three exposed volume parameters, two compressors, and the Inspector-configured persistent prefab.");
        }
        private static void AddCompressor(AudioMixer mixer,object group,object snapshot,float threshold)
        {
            Array existing=(Array)Get(group,"effects");
            for(int i=0;i<existing.Length;i++)if((string)Get(existing.GetValue(i),"effectName")=="Compressor")return;
            Type type=EditorType("UnityEditor.Audio.AudioMixerEffectController");
            var effect=(UnityEngine.Object)Activator.CreateInstance(type,Flags,null,new object[]{"Compressor"},null);
            Call(effect,"PreallocateGUIDs");AssetDatabase.AddObjectToAsset(effect,mixer);
            var appended=Array.CreateInstance(type,existing.Length+1);Array.Copy(existing,appended,existing.Length);appended.SetValue(effect,existing.Length);Set(group,"effects",appended);
            Call(effect,"SetValueForParameter",mixer,snapshot,"Threshold",threshold);
            Call(effect,"SetValueForParameter",mixer,snapshot,"Make up gain",0f);
            Call(effect,"SetValueForParameter",mixer,snapshot,"Attack",5f);
            Call(effect,"SetValueForParameter",mixer,snapshot,"Release",100f);
            EditorUtility.SetDirty(effect);
        }
        [MenuItem("Tools/Flood Fill/Audio/Install Audio Prefab In Open Scene")]
        public static void InstallInOpenScene()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Install outside Play Mode.");
            CreateOrRepair();
            if(UnityEngine.Object.FindAnyObjectByType<FloodAudioManager>()!=null)return;
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        public static void CompileMobilePlayers()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Run compilation checks in an isolated batch project.");
            var report=new System.Collections.Generic.List<string>();
            foreach(var target in new[]{BuildTarget.Android,BuildTarget.iOS})
            {
                string output="Temp/FloodAudioPlayerCompilation/"+target;Directory.CreateDirectory(output);
                var settings=new UnityEditor.Build.Player.ScriptCompilationSettings
                {
                    target=target,
                    group=target==BuildTarget.Android?BuildTargetGroup.Android:BuildTargetGroup.iOS,
                    options=UnityEditor.Build.Player.ScriptCompilationOptions.None
                };
                var result=UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(settings,output);
                if(result.assemblies==null||result.assemblies.Count==0)throw new InvalidOperationException(target+" player script compilation returned no assemblies.");
                report.Add("PASS "+target+": "+result.assemblies.Count+" player assemblies compiled; Editor folder excluded by Unity's player compilation pipeline.");
            }
            Directory.CreateDirectory("Logs");File.WriteAllLines("Logs/FloodAudioPlayerCompilation.txt",report);Debug.Log(string.Join("\n",report));
        }
        public static void InstallDemoBatch()
        {
            CreateOrRepair();
            var scene=EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            InstallInOpenScene();EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
    }
}
#endif
