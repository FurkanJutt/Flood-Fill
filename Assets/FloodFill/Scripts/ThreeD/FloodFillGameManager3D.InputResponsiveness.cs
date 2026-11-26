using System;
using System.IO;
using UnityEngine;
namespace FloodFill.ThreeD
{
    public sealed partial class FloodFillGameManager3D
    {
        private bool selectionSavePending;
        private float selectionSaveAt;
        private bool continueCacheReady,cachedContinueAvailable;
        private long continueCacheStamp,continueCacheSize;
        private string continueCachePath;
        private FloodFillSessionSaveData3D cachedContinueSession;
        // Selecting paint changes no board colors. Coalesce rapid selection-only saves;
        // actual moves, power-ups, navigation and app lifecycle still save immediately.
        private void QueueSelectionSave(){selectionSavePending=true;selectionSaveAt=Time.unscaledTime+.15f;}
        private void FlushSelectionSave()
        {if(selectionSavePending&&Time.unscaledTime>=selectionSaveAt)SaveSession();}
        private bool ReadSessionStamp(out string path,out long stamp,out long size)
        {
            path=FloodFillSessionPersistence3D.SavePath;stamp=size=0;
            try{var file=new FileInfo(path);if(!file.Exists)return false;stamp=file.LastWriteTimeUtc.Ticks;size=file.Length;return true;}
            catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
        }
        private bool TryCachedContinue()
        {
            if(!ReadSessionStamp(out var path,out var stamp,out var size))return false;
            if(!continueCacheReady||path!=continueCachePath||stamp!=continueCacheStamp||size!=continueCacheSize)return false;
            HasSavedSession=cachedContinueAvailable;availableSession=cachedContinueSession;return true;
        }
        private void CacheContinue(FloodFillSessionSaveData3D saved,bool available)
        {
            continueCacheReady=ReadSessionStamp(out continueCachePath,out continueCacheStamp,out continueCacheSize);
            cachedContinueAvailable=available;cachedContinueSession=available?saved:null;
        }
    }
}
