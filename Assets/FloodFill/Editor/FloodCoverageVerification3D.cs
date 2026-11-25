using System;
using System.IO;
using FloodFill.ThreeD;
using UnityEditor;
using UnityEngine;
namespace FloodFill.EditorTools
{
    public static class FloodCoverageVerification3D
    {
        private static int checks;
        private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        public static void RunBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use an isolated /tmp project.");
            var root=new GameObject("Coverage fixture");var board=root.AddComponent<VoxelBoardManager3D>();
            var cells=new GameObject("Cells").transform;cells.SetParent(root.transform);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FloodFill/Prefabs/VoxelCell3D.prefab").GetComponent<VoxelCell3D>();
            var palette=new[]{Color.green,Color.magenta,Color.yellow,Color.red,Color.blue,Color.cyan};
            board.Configure(cells,prefab,8,8,8,1,.01f,VoxelBoardManager3D.VoxelVolumeMode.HollowCube);
            int[] colors={0,0,1,4,2,2,3,5};var initial=new VoxelBoardSaveData3D{width=8,height=8,depth=8,currentPlayerColor=0,voxels=new VoxelSaveData3D[8]};
            for(int x=0;x<8;x++)initial.voxels[x]=new VoxelSaveData3D{x=x,colorIndex=colors[x],captured=x<2,starting=x==0};
            try
            {
                Check(board.RestorePersistentState(initial,palette),"Legacy checkpoint loads");
                Check(board.CoveredVoxelCount==2,"Legacy coverage starts from captured cells");
                Check(board.RecolorConnectedRegion(board.GetCell(0,0,0),1),"First region move accepted");
                Check(board.CoveredVoxelCount==3&&board.CoveredPercentage==37.5f,"First capture accumulates");
                Check(board.RecolorConnectedRegion(board.GetCell(4,0,0),3),"Separate region move accepted");
                Check(board.CapturedVoxelCount==3&&board.CoveredVoxelCount==6&&board.CoveredPercentage==75,"Separate region retains previous coverage");
                Check(board.TryCapturePersistentState(out var saved),"Checkpoint captured");
                var copy=JsonUtility.FromJson<VoxelBoardSaveData3D>(JsonUtility.ToJson(saved));
                Check(board.RestorePersistentState(copy,palette)&&board.CoveredVoxelCount==6,"Coverage survives JSON save/restore");
                Check(board.RecolorConnectedRegion(board.GetCell(0,0,0),4)&&board.CoveredVoxelCount==7,"Revisiting covered cells counts only the new cell");
                Check(!board.RecolorConnectedRegion(board.GetCell(0,0,0),4)&&board.CoveredVoxelCount==7,"Invalid same-color move does not change coverage");
                Check(board.RecolorConnectedRegion(board.GetCell(4,0,0),5)&&board.CoveredVoxelCount==8,"All unique cells can be covered across separate regions");
                Check(!board.IsFullyCaptured&&board.CapturedVoxelCount==4,"Coverage never bypasses normal connected-board victory");
                Check(board.RecolorConnectedRegion(board.GetCell(0,0,0),5)&&board.IsFullyCaptured,"Final merge still completes the board normally");
                Check(board.RestorePersistentState(initial,palette)&&board.CoveredVoxelCount==2,"Restarting original definition resets coverage");
                board.ClearBoard();Check(board.CoveredVoxelCount==0&&board.CoveredPercentage==0,"Clearing removes coverage");
                File.WriteAllText("/tmp/FloodCoverageVerification.txt",checks+" checks passed: separate regions, no duplicates, invalid moves, legacy checkpoint, JSON round-trip, normal victory, restart and clear.\n");
            }
            finally{board.ClearPool();UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
