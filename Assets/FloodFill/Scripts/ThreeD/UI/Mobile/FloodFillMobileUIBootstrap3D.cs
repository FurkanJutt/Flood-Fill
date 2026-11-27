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

            FloodFillMobileUI3D ui = Object.FindAnyObjectByType<FloodFillMobileUI3D>();
            if (ui == null)
            {
                Debug.LogError("The scene needs its authored FloodFillCanvas prefab. Use Tools > Flood Fill > UI > Install Editable UI in Demo Scene.");
                return;
            }
            if (ui.IsBuilt) return;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;
            ui.Initialize(manager, canvas.transform, manager.BoardManager.VoxelPrefab);
        }
    }
}
