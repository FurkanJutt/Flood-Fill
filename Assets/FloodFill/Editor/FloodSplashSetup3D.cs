using FloodFill.ThreeD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace FloodFill.EditorTools
{
    public static class FloodSplashSetup3D
    {
        public const string PrefabPath="Assets/FloodFill/Prefabs/VFX/FloodFillSplash.prefab";
        private const string MaterialPath="Assets/FloodFill/Art/VFX/FloodLiquidSplash.mat";
        [InitializeOnLoadMethod]
        private static void ConnectLoadedDemo()
        {
            // A scene already open when the files change may still lack the new prefab instance.
            EditorApplication.delayCall+=RepairLoadedDemo;
            EditorApplication.playModeStateChanged+=state=>
            {if(state==PlayModeStateChange.EnteredEditMode||state==PlayModeStateChange.ExitingEditMode)RepairLoadedDemo();};
        }
        private static void RepairLoadedDemo()
        {
            const string key="FloodFill.SplashVisibilityRepair.v2";
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||SessionState.GetBool(key,false)||scene.path!="Assets/FloodFill/Scenes/FloodFill3DDemo.unity")return;
            // Respect deliberate removal: only repair stale in-memory scenes whose saved version includes the prefab.
            string guid=AssetDatabase.AssetPathToGUID(PrefabPath);
            if(string.IsNullOrEmpty(guid)||!System.IO.File.ReadAllText(scene.path).Contains(guid))return;
            Install();SessionState.SetBool(key,true);
        }
        public static GameObject EnsureAssets()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(material==null)
            {
                var shader=Shader.Find("FloodFill/Liquid Splash");
                if(shader==null)throw new System.Exception("Flood splash shader not imported.");
                material=new Material(shader);AssetDatabase.CreateAsset(material,MaterialPath);
            }
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(prefab!=null)return prefab;
            var root=new GameObject("FloodFillSplash",typeof(MeshFilter),typeof(MeshRenderer),typeof(FloodFillSplashVFX));
            try
            {
                var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                return PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [MenuItem("Tools/Flood Fill/VFX/Install Splash In Open Scene")]
        public static void Install()
        {
            var prefab=EnsureAssets();
            foreach(var board in Object.FindObjectsByType<VoxelBoardManager3D>(FindObjectsInactive.Include))
            {
                var existing=board.GetComponentInChildren<FloodFillSplashVFX>(true);
                if(existing!=null)
                {
                    if(existing.board==null){existing.board=board;PrefabUtility.RecordPrefabInstancePropertyModifications(existing);EditorSceneManager.MarkSceneDirty(board.gameObject.scene);}
                    continue;
                }
                if(board.BoardTransform==null)continue;
                var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,board.BoardTransform);
                root.GetComponent<FloodFillSplashVFX>().board=board;
                PrefabUtility.RecordPrefabInstancePropertyModifications(root.GetComponent<FloodFillSplashVFX>());
                Undo.RegisterCreatedObjectUndo(root,"Install Flood Splash");
                EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
        }
        public static void InstallBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new System.Exception("Use isolated /tmp project.");
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            Install();Install();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
    }
}
