using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FloodFill.Development.AutoSolver
{
    internal sealed class AutoSolverVisualTuner
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private struct FieldBackup
        {
            public object Target;
            public FieldInfo Field;
            public object Value;
        }

        private readonly List<FieldBackup> backups = new List<FieldBackup>();
        private float originalTimeScale = 1f;
        private bool active;

        public void Begin(
            AutoSolverAdapter adapter,
            AutoSolverBoardSnapshot snapshot,
            float animationDuration,
            bool enableCaptureAnimation,
            bool enableColorTransition,
            bool enableCubePop)
        {
            Restore();
            if (adapter == null || snapshot == null || !adapter.IsAlive) return;
            active = true;
            originalTimeScale = Time.timeScale;

            int span = Math.Max(1, snapshot.Width + snapshot.Height + snapshot.Depth);
            float waveStep = enableCaptureAnimation || enableColorTransition
                ? Mathf.Max(0f, animationDuration * 0.1f / span)
                : 0f;
            SetField(adapter.Board, "waveStepDelay", waveStep);

            float colorTotal = enableColorTransition ? Mathf.Max(0.01f, animationDuration) : 0.001f;
            float captureTotal = enableCaptureAnimation ? Mathf.Max(0.01f, animationDuration) : 0.001f;
            for (int i = 0; i < snapshot.Voxels.Length; i++)
            {
                Component voxel = snapshot.Voxels[i];
                if (voxel == null) continue;
                SetField(voxel, "captureDuration", captureTotal);
                SetField(voxel, "captureStartScale", !enableCaptureAnimation ? 1f : enableCubePop ? 0.96f : 0.995f);
                SetField(voxel, "capturedScale", !enableCaptureAnimation ? 1f : enableCubePop ? 1.025f : 1.005f);
                SetField(voxel, "waveAnticipationDuration", colorTotal * 0.25f);
                SetField(voxel, "waveFlashDuration", colorTotal * 0.25f);
                SetField(voxel, "waveSettleDuration", colorTotal * 0.5f);
                SetField(voxel, "waveAnticipationDarken", enableColorTransition ? 0.18f : 0f);
                SetField(voxel, "waveFlashBrightness", enableColorTransition ? 0.45f : 0f);
                SetField(voxel, "waveAnticipationScale", enableColorTransition && enableCubePop ? 0.97f : 1f);
                SetField(voxel, "wavePopScale", enableColorTransition && enableCubePop ? 1.05f : 1f);
            }
        }

        public void SetPlaybackSpeed(float speed)
        {
            if (!active) return;
            Time.timeScale = originalTimeScale * Mathf.Clamp(speed, 0.1f, 8f);
        }

        public void PauseTime() { if (active) Time.timeScale = 0f; }

        public void CompleteVoxelAnimations(AutoSolverBoardSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Voxels == null) return;
            for (int i = 0; i < snapshot.Voxels.Length; i++)
            {
                Component voxel = snapshot.Voxels[i];
                if (voxel == null) continue;
                CompleteTween(GetField(voxel, "colorWaveSequence"));
                CompleteTween(GetField(voxel, "captureTween"));
            }
        }

        public void Restore()
        {
            if (!active && backups.Count == 0) return;
            for (int i = backups.Count - 1; i >= 0; i--)
            {
                FieldBackup backup = backups[i];
                if (backup.Target is UnityEngine.Object unityObject && unityObject == null) continue;
                try { backup.Field.SetValue(backup.Target, backup.Value); }
                catch { /* A scene object may have been destroyed while closing the window. */ }
            }
            backups.Clear();
            Time.timeScale = originalTimeScale;
            active = false;
        }

        private void SetField(object target, string fieldName, object value)
        {
            if (target == null) return;
            FieldInfo field = target.GetType().GetField(fieldName, Flags);
            if (field == null || field.FieldType != value.GetType()) return;
            backups.Add(new FieldBackup { Target = target, Field = field, Value = field.GetValue(target) });
            field.SetValue(target, value);
        }

        private static object GetField(object target, string name) =>
            target != null ? target.GetType().GetField(name, Flags)?.GetValue(target) : null;

        private static void CompleteTween(object tween)
        {
            if (tween == null) return;
            MethodInfo[] methods = tween.GetType().GetMethods(Flags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "Complete") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0) { method.Invoke(tween, null); return; }
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
                {
                    method.Invoke(tween, new object[] { true });
                    return;
                }
            }
        }
    }
}
