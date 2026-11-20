using System;
using System.IO;
using System.Reflection;
using FloodFill.ThreeD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FloodFill.EditorTools
{
    public static class FloodCameraFramingVerification
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
        private static void Call(object target, string method) => target.GetType().GetMethod(method, Flags).Invoke(target, null);
        private static int checks;
        private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project copy.");
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            var authored = UnityEngine.Object.FindAnyObjectByType<OrbitCameraController3D>();
            var serialized = new SerializedObject(authored);
            Check(Mathf.Approximately(serialized.FindProperty("framePadding").floatValue, 1.05f), "Scene uses closer framing");
            Check(Mathf.Approximately(serialized.FindProperty("boardSurfaceClearance").floatValue, .75f), "Scene uses outside-surface clearance without fixed 13-unit offset");
            float smallestMargin = 1f;
            foreach (float aspect in new[] { 9f / 19.5f, 9f / 16f, 3f / 4f, 16f / 9f })
            {
                float referenceWidth = -1;
                foreach (int size in new[] { 6, 8, 10, 12, 15, 18, 20 })
                {
                    var rig = new GameObject("Framing verification");
                    var controller = rig.AddComponent<OrbitCameraController3D>();
                    var pivot = new GameObject("Pivot").transform; pivot.SetParent(rig.transform, false);
                    var camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(pivot, false);
                    camera.fieldOfView = 50; camera.aspect = aspect; camera.nearClipPlane = .3f;
                    controller.Configure(pivot, camera);
                    var bounds = new Bounds(new Vector3(7, -2, 3), Vector3.one * (size + (size - 1) * .01f));
                    controller.FrameBounds(bounds);
                    float width = ProjectedWidth(camera, bounds);
                    if (referenceWidth < 0) referenceWidth = width;
                    Check(Mathf.Abs(width - referenceWidth) < .0001f, "All board sizes have the same initial projected width");
                    if (aspect < .6f) Check(width > .79f && width < .85f, "Portrait starting board occupies about 80% of screen width");
                    var framed = controller.CapturePersistentState();
                    for (int yaw = -180; yaw < 180; yaw += 10)
                    for (int pitch = -75; pitch <= 75; pitch += 10)
                    {
                        Set(controller, "yaw", (float)yaw); Set(controller, "pitch", (float)pitch);
                        Call(controller, "ApplyCameraTransform");
                        foreach (Vector3 point in Corners(bounds))
                        {
                            Vector3 view = camera.WorldToViewportPoint(point);
                            float margin = Mathf.Min(view.x, 1 - view.x, view.y, 1 - view.y);
                            smallestMargin = Mathf.Min(smallestMargin, margin);
                            Check(margin > .02f && view.z > camera.nearClipPlane, "Rotation preserves edge margins and near-plane clearance");
                        }
                    }
                    Set(controller, "yaw", framed.yaw); Set(controller, "pitch", framed.pitch);
                    Set(controller, "distance", .01f); Call(controller, "ApplyCameraTransform");
                    Check(Vector3.Distance(camera.transform.position, bounds.center) >= bounds.extents.magnitude + .749f, "Zoom clamp keeps camera outside the board");
                    Set(controller, "zoomResetDuration", 0f); controller.ResetZoom();
                    Check(Mathf.Abs(controller.CapturePersistentState().distance - framed.defaultDistance) < .001f, "Reset returns to the closer initial framing");
                    UnityEngine.Object.DestroyImmediate(rig);
                }
            }
            string report = $"PASS: {checks} camera checks across 7 board sizes and 4 aspect ratios. Initial board widths match across sizes; portrait width is about 80%. Minimum orbit edge margin: {smallestMargin:P2}. Near-plane safety, outside-board zoom clamp and reset framing passed.\n";
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/FloodCameraFramingVerification.txt", report);Debug.Log(report);
        }

        private static Vector3[] Corners(Bounds bounds)
        {
            var result = new Vector3[8]; int i = 0;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                result[i++] = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
            return result;
        }
        private static float ProjectedWidth(Camera camera, Bounds bounds)
        {
            float min = 1, max = 0;
            foreach (Vector3 point in Corners(bounds)) { float x = camera.WorldToViewportPoint(point).x; min = Mathf.Min(min, x); max = Mathf.Max(max, x); }
            return max - min;
        }
    }
}
