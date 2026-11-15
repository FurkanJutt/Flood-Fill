using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.ThreeD
{
    internal static class FloodFillMobileUIBootstrap3D
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToExistingDemoScene()
        {
            FloodFillGameManager3D manager = Object.FindAnyObjectByType<FloodFillGameManager3D>();
            if (manager == null || manager.BoardManager == null || manager.BoardManager.VoxelPrefab == null)
                return;

            FloodFillMobileUI3D ui = manager.GetComponent<FloodFillMobileUI3D>();
            if (ui == null) ui = manager.gameObject.AddComponent<FloodFillMobileUI3D>();
            if (ui.IsBuilt) return;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;
            ui.Initialize(manager, canvas.transform, manager.BoardManager.VoxelPrefab);
        }
    }
}
