using System;

namespace FloodFill.ThreeD.Audio
{
    /// <summary>Small, allocation-free integration surface. No audio is required for gameplay.</summary>
    public static class FloodAudioSignals
    {
        public readonly struct FloodWave
        {
            public readonly int AffectedCubes, NewlyCaptured;
            public readonly float Duration, LastWaveStart, WaveStep;
            public FloodWave(int affected, int captured, float duration, float lastWaveStart, float waveStep)
            { AffectedCubes=affected; NewlyCaptured=captured; Duration=duration; LastWaveStart=lastWaveStart; WaveStep=waveStep; }
        }
        public static event Action<FloodFillGameManager3D> ManagerAvailable;
        public static event Action<FloodFillGameManager3D> ManagerRemoved;
        public static event Action ColorAccepted;
        public static event Action<FloodWave> FloodStarted;
        public static event Action LevelReset;
        public static event Action RestartRequested;
        public static event Action<bool> PauseChanged;
        public static event Action<bool> ResultScheduled;
        public static event Action<bool> MenuContextChanged;
        public static event Action<bool> PopupChanged;
        public static void Register(FloodFillGameManager3D value) => ManagerAvailable?.Invoke(value);
        public static void Unregister(FloodFillGameManager3D value) => ManagerRemoved?.Invoke(value);
        public static void SelectColor() => ColorAccepted?.Invoke();
        public static void BeginFlood(FloodWave wave) => FloodStarted?.Invoke(wave);
        public static void ResetLevel() => LevelReset?.Invoke();
        public static void Restart() => RestartRequested?.Invoke();
        public static void Pause(bool paused) => PauseChanged?.Invoke(paused);
        public static void Result(bool won) => ResultScheduled?.Invoke(won);
        public static void Menu(bool menu) => MenuContextChanged?.Invoke(menu);
        public static void Popup(bool open) => PopupChanged?.Invoke(open);
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Clear()
        {
            ManagerAvailable=null;ManagerRemoved=null;ColorAccepted=null;FloodStarted=null;LevelReset=null;
            RestartRequested=null;PauseChanged=null;ResultScheduled=null;MenuContextChanged=null;PopupChanged=null;
        }
    }
}
