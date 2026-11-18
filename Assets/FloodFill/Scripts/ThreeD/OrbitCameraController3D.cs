using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FloodFill.ThreeD
{
    public sealed class OrbitCameraController3D : MonoBehaviour
    {
        private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>();
        private static EventSystem uiEventSystem;
        private static PointerEventData uiPointerData;

        [Header("Rig")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Camera targetCamera;

        [Header("Camera Fill Light")]
        [SerializeField] private bool useCameraFillLight = true;
        [SerializeField] private Light cameraFillLight;
        [SerializeField] private Color cameraFillColor = new Color(0.86f, 0.91f, 1f, 1f);
        [SerializeField, Range(0f, 3f)] private float cameraFillIntensity = 0.65f;

        [Header("Controls")]
        [SerializeField, Min(0.01f)] private float orbitSensitivity = 0.20f;
        [SerializeField, Min(0.01f)] private float panSensitivity = 1.25f;
        [SerializeField, Min(0.01f)] private float zoomSensitivity = 1.25f;
        [SerializeField] private float minimumPitch = -75f;
        [SerializeField] private float maximumPitch = 75f;
        [SerializeField, Min(0.1f)] private float minimumDistance = 4f;
        [SerializeField, Min(0.1f)] private float maximumDistance = 30f;
        [SerializeField, Min(1f)] private float framePadding = 1.22f;
        [SerializeField, Min(0f)] private float boardSurfaceClearance = 0.75f;

        [Header("Double Tap View Reset")]
        [SerializeField, Range(0.1f, 0.6f)] private float doubleTapMaximumDelay = 0.32f;
        [SerializeField, Min(0f)] private float tapMaximumMovement = 36f;
        [SerializeField, Min(0f)] private float doubleTapMaximumSeparation = 80f;
        [SerializeField, Min(0f)] private float zoomResetDuration = 0.45f;

        private Vector3 targetPosition;
        private float yaw = -40f;
        private float pitch = 28f;
        private float distance = 12f;
        private float defaultDistance = 12f;
        private float configuredMaximumDistance;
        private bool hasConfiguredMaximumDistance;
        private Vector3 framedBoundsCenter;
        private float framedBoundsRadius;
        private float zoomResetStartDistance;
        private Vector3 zoomResetStartPosition;
        private float zoomResetElapsed;
        private bool zoomResetAnimating;
        private bool inputEnabled = true;
        private bool orbitDragging;
        private bool panDragging;
        private bool mouseTapCandidate;
        private Vector2 mouseTapStartPosition;
        private bool touchTapCandidate;
        private Vector2 touchTapStartPosition;
        private float lastTapTime = -10f;
        private Vector2 lastTapPosition;
        private int activeTouchCount;
        private bool touchGestureBlocked;

        public void Configure(Transform pivot, Camera controlledCamera)
        {
            cameraPivot = pivot;
            targetCamera = controlledCamera;
            EnsureCameraFillLight();
            ApplyCameraTransform();
        }

        public Light EnsureCameraFillLight()
        {
            if (targetCamera == null)
            {
                return null;
            }

            if (cameraFillLight == null)
            {
                Transform lightTransform = targetCamera.transform.Find("Camera Fill Light");
                if (lightTransform == null)
                {
                    var lightObject = new GameObject("Camera Fill Light");
                    lightTransform = lightObject.transform;
                    lightTransform.SetParent(targetCamera.transform, false);
                }

                cameraFillLight = lightTransform.GetComponent<Light>();
                if (cameraFillLight == null)
                {
                    cameraFillLight = lightTransform.gameObject.AddComponent<Light>();
                }
            }

            cameraFillLight.transform.SetParent(targetCamera.transform, false);
            cameraFillLight.transform.localPosition = Vector3.zero;
            cameraFillLight.transform.localRotation = Quaternion.identity;
            ApplyCameraFillLightSettings();
            return cameraFillLight;
        }

        public void FrameBounds(Bounds bounds)
        {
            framedBoundsCenter = bounds.center;
            framedBoundsRadius = Mathf.Max(0f, bounds.extents.magnitude);
            targetPosition = bounds.center;
            yaw = -40f;
            pitch = 28f;
            float fieldOfView = targetCamera != null ? targetCamera.fieldOfView : 50f;
            float halfVerticalFov = Mathf.Max(1f, fieldOfView * 0.5f) * Mathf.Deg2Rad;
            float aspect = targetCamera != null ? Mathf.Max(0.1f, targetCamera.aspect) : 1f;
            float halfHorizontalFov = Mathf.Atan(Mathf.Tan(halfVerticalFov) * aspect);
            float limitingHalfFov = Mathf.Min(halfVerticalFov, halfHorizontalFov);
            float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
            distance = Mathf.Max(
                minimumDistance,
                radius / Mathf.Sin(limitingHalfFov) * framePadding);
            // Large boards and narrow portrait screens can need more distance than
            // the default zoom limit. Raise the limit to the fitted distance so the
            // board stays fully visible and the player can still zoom out farther.
            if (!hasConfiguredMaximumDistance)
            {
                configuredMaximumDistance = maximumDistance;
                hasConfiguredMaximumDistance = true;
            }
            maximumDistance = Mathf.Max(configuredMaximumDistance, distance);
            zoomResetAnimating = false;
            ApplyCameraTransform();
            // Clearance may push the fitted camera farther out. Reset to the actual
            // starting distance, not the uncorrected fit that was inside that limit.
            defaultDistance = distance;
        }

        public OrbitCameraSaveData3D CapturePersistentState()
        {
            return new OrbitCameraSaveData3D
            {
                targetPosition = targetPosition,
                yaw = yaw,
                pitch = pitch,
                distance = distance,
                defaultDistance = defaultDistance
            };
        }

        public bool RestorePersistentState(OrbitCameraSaveData3D state)
        {
            if (state == null || !IsFinite(state.targetPosition.x) ||
                !IsFinite(state.targetPosition.y) || !IsFinite(state.targetPosition.z) ||
                !IsFinite(state.yaw) || !IsFinite(state.pitch) ||
                !IsFinite(state.distance) || !IsFinite(state.defaultDistance))
            {
                return false;
            }

            targetPosition = state.targetPosition;
            yaw = state.yaw;
            pitch = Mathf.Clamp(state.pitch, minimumPitch, maximumPitch);
            distance = Mathf.Clamp(state.distance, minimumDistance, maximumDistance);
            defaultDistance = Mathf.Clamp(state.defaultDistance, minimumDistance, maximumDistance);
            if (framedBoundsRadius > 0f)
            {
                float clearance = Mathf.Max(boardSurfaceClearance, targetCamera != null ? targetCamera.nearClipPlane + .15f : .5f);
                defaultDistance = Mathf.Max(defaultDistance, framedBoundsRadius + clearance);
                maximumDistance = Mathf.Max(maximumDistance, defaultDistance);
            }
            zoomResetAnimating = false;
            ApplyCameraTransform();
            return true;
        }

        public void SetInputEnabled(bool value)
        {
            inputEnabled = value;
            if (value) return;
            orbitDragging = panDragging = mouseTapCandidate = touchTapCandidate = false;
            zoomResetAnimating = false;
            activeTouchCount = 0;
            touchGestureBlocked = false;
            lastTapTime = -10f;
        }

        public void ResetZoom()
        {
            if (!inputEnabled) return;
            float targetDistance = Mathf.Clamp(
                defaultDistance, minimumDistance, maximumDistance);
            if (zoomResetDuration <= 0f || (Mathf.Approximately(distance, targetDistance) && (targetPosition - framedBoundsCenter).sqrMagnitude < .000001f))
            {
                distance = targetDistance;
                targetPosition = framedBoundsCenter;
                zoomResetAnimating = false;
                ApplyCameraTransform();
                return;
            }

            zoomResetStartDistance = distance;
            zoomResetStartPosition = targetPosition;
            zoomResetElapsed = 0f;
            zoomResetAnimating = true;
        }

        private void Awake()
        {
            if (!hasConfiguredMaximumDistance)
            {
                configuredMaximumDistance = maximumDistance;
                hasConfiguredMaximumDistance = true;
            }
            EnsureCameraFillLight();
            ApplyCameraTransform();
        }

        private void Update()
        {
            if (!inputEnabled) return;
#if ENABLE_INPUT_SYSTEM
            HandleNewInputSystem();
#elif ENABLE_LEGACY_INPUT_MANAGER
            HandleLegacyInput();
#endif
            UpdateZoomReset(Time.unscaledDeltaTime);
            ApplyCameraTransform();
        }

#if ENABLE_INPUT_SYSTEM
        private void HandleNewInputSystem()
        {
            if (Touchscreen.current != null && HandleNewInputTouches(Touchscreen.current))
            {
                orbitDragging = false;
                panDragging = false;
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 pointerPosition = mouse.position.ReadValue();
            float scroll = mouse.scroll.ReadValue().y;
            bool panPressed = mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame;
            bool pointerOverUi = (mouse.leftButton.wasPressedThisFrame || panPressed || orbitDragging || panDragging || !Mathf.Approximately(scroll, 0f)) &&
                IsScreenPositionOverUI(pointerPosition);
            if (mouse.leftButton.wasPressedThisFrame)
            {
                orbitDragging = !pointerOverUi;
                BeginMouseTapCandidate(pointerPosition, pointerOverUi);
            }


            if (mouseTapCandidate && mouse.leftButton.isPressed &&
                Vector2.Distance(mouseTapStartPosition, pointerPosition) > tapMaximumMovement)
            {
                mouseTapCandidate = false;
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                orbitDragging = false;
                CompleteMouseTapCandidate(pointerPosition);
            }

            if (panPressed)
            {
                panDragging = !pointerOverUi;
            }

            if (!mouse.rightButton.isPressed && !mouse.middleButton.isPressed)
            {
                panDragging = false;
            }

            Vector2 delta = mouse.delta.ReadValue();
            if (panDragging && !pointerOverUi)
            {
                Pan(delta);
            }
            else if (orbitDragging && !pointerOverUi)
            {
                Orbit(delta);
            }

            if (!Mathf.Approximately(scroll, 0f) && !pointerOverUi)
            {
                Zoom(scroll / 120f);
            }
        }

        private bool HandleNewInputTouches(Touchscreen touchscreen)
        {
            var first = touchscreen.touches[0];
            var second = touchscreen.touches[1];
            Vector2 firstPosition = first.position.ReadValue();
            int touchCount = first.press.isPressed ? 1 : 0;
            if (second.press.isPressed)
            {
                touchCount++;
            }

            UpdateTouchTapCandidate(
                first.press.wasPressedThisFrame,
                first.press.wasReleasedThisFrame,
                first.press.isPressed,
                touchCount,
                firstPosition);

            if (touchCount == 0)
            {
                activeTouchCount = 0;
                touchGestureBlocked = false;
                return false;
            }

            if (touchCount != activeTouchCount)
            {
                touchGestureBlocked = IsScreenPositionOverUI(firstPosition) ||
                    touchCount > 1 && IsScreenPositionOverUI(second.position.ReadValue());
                activeTouchCount = touchCount;
            }

            if (touchGestureBlocked)
            {
                return true;
            }

            if (IsScreenPositionOverUI(firstPosition) ||
                touchCount > 1 && IsScreenPositionOverUI(second.position.ReadValue()))
            {
                return true;
            }

            if (touchCount == 1)
            {
                Orbit(first.delta.ReadValue());
            }
            else
            {
                Vector2 firstDelta = first.delta.ReadValue();
                Vector2 secondDelta = second.delta.ReadValue();
                Pan((firstDelta + secondDelta) * 0.5f);

                Vector2 secondPosition = second.position.ReadValue();
                float currentSeparation = Vector2.Distance(firstPosition, secondPosition);
                float previousSeparation = Vector2.Distance(
                    firstPosition - firstDelta,
                    secondPosition - secondDelta);
                float normalizedPinch = (currentSeparation - previousSeparation) /
                    Mathf.Max(1f, Screen.height) * 12f;
                Zoom(normalizedPinch);
            }

            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private void HandleLegacyInput()
        {
            if (Input.touchCount > 0)
            {
                HandleLegacyTouches();
                orbitDragging = false;
                panDragging = false;
                return;
            }

            Vector2 pointerPosition = Input.mousePosition;
            float scroll = Input.mouseScrollDelta.y;
            bool pointerOverUi = (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2) || orbitDragging || panDragging || !Mathf.Approximately(scroll, 0f)) &&
                IsScreenPositionOverUI(pointerPosition);
            if (Input.GetMouseButtonDown(0))
            {
                orbitDragging = !pointerOverUi;
                BeginMouseTapCandidate(pointerPosition, pointerOverUi);
            }

            if (mouseTapCandidate && Input.GetMouseButton(0) &&
                Vector2.Distance(mouseTapStartPosition, pointerPosition) > tapMaximumMovement)
            {
                mouseTapCandidate = false;
            }

            if (Input.GetMouseButtonUp(0))
            {
                orbitDragging = false;
                CompleteMouseTapCandidate(pointerPosition);
            }

            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                panDragging = !pointerOverUi;
            }

            if (!Input.GetMouseButton(1) && !Input.GetMouseButton(2))
            {
                panDragging = false;
            }

            Vector2 delta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 12f;
            if (panDragging && !pointerOverUi)
            {
                Pan(delta);
            }
            else if (orbitDragging && !pointerOverUi)
            {
                Orbit(delta);
            }

            if (!Mathf.Approximately(scroll, 0f) && !pointerOverUi)
            {
                Zoom(scroll);
            }
        }

        private void HandleLegacyTouches()
        {
            int touchCount = Mathf.Min(2, Input.touchCount);
            if (touchCount != activeTouchCount)
            {
                touchGestureBlocked = IsScreenPositionOverUI(Input.GetTouch(0).position) ||
                    touchCount > 1 && IsScreenPositionOverUI(Input.GetTouch(1).position);
                activeTouchCount = touchCount;
            }

            if (touchGestureBlocked)
            {
                return;
            }

            if (IsScreenPositionOverUI(Input.GetTouch(0).position) ||
                touchCount > 1 && IsScreenPositionOverUI(Input.GetTouch(1).position))
            {
                return;
            }

            Touch first = Input.GetTouch(0);
            UpdateTouchTapCandidate(
                first.phase == TouchPhase.Began,
                first.phase == TouchPhase.Ended,
                first.phase != TouchPhase.Ended && first.phase != TouchPhase.Canceled,
                touchCount,
                first.position);
            if (touchCount == 1)
            {
                Orbit(first.deltaPosition);
                return;
            }

            Touch second = Input.GetTouch(1);
            Pan((first.deltaPosition + second.deltaPosition) * 0.5f);
            float currentSeparation = Vector2.Distance(first.position, second.position);
            float previousSeparation = Vector2.Distance(
                first.position - first.deltaPosition,
                second.position - second.deltaPosition);
            Zoom((currentSeparation - previousSeparation) /
                Mathf.Max(1f, Screen.height) * 12f);
        }
#endif

        private void Orbit(Vector2 delta)
        {
            if (!inputEnabled) return;
            yaw += delta.x * orbitSensitivity;
            pitch = Mathf.Clamp(
                pitch - delta.y * orbitSensitivity,
                minimumPitch,
                maximumPitch);
        }

        private void Pan(Vector2 delta)
        {
            if (!inputEnabled) return;
            if (targetCamera == null)
            {
                return;
            }

            if (delta.sqrMagnitude > 0f) zoomResetAnimating = false;
            float scale = distance * panSensitivity / Mathf.Max(1f, Screen.height);
            targetPosition -= targetCamera.transform.right * delta.x * scale;
            targetPosition -= targetCamera.transform.up * delta.y * scale;
        }

        private void Zoom(float amount)
        {
            if (!inputEnabled) return;
            zoomResetAnimating = false;
            float outsideDistance = GetMinimumOutsideDistance();
            maximumDistance = Mathf.Max(maximumDistance, outsideDistance);
            distance = Mathf.Clamp(
                distance - amount * zoomSensitivity,
                outsideDistance,
                maximumDistance);
        }

        private float GetMinimumOutsideDistance()
        {
            float minimum = minimumDistance;
            if (framedBoundsRadius <= 0f) return minimum;

            float clearance = Mathf.Max(
                boardSurfaceClearance,
                targetCamera != null ? targetCamera.nearClipPlane + 0.15f : 0.5f);
            float safeRadius = framedBoundsRadius + clearance;
            Quaternion cameraRotation = Quaternion.Euler(0f, yaw, 0f) *
                                        Quaternion.Euler(pitch, 0f, 0f);
            Vector3 forward = cameraRotation * Vector3.forward;
            Vector3 targetOffset = targetPosition - framedBoundsCenter;
            float alongView = Vector3.Dot(targetOffset, forward);
            Vector3 perpendicularOffset = targetOffset - forward * alongView;
            float remainingRadiusSquared = safeRadius * safeRadius - perpendicularOffset.sqrMagnitude;
            if (remainingRadiusSquared <= 0f) return minimum;

            // Keep the camera on the far side of the board's bounding sphere, even
            // after the player pans or orbits before zooming in.
            float farSideDistance = alongView + Mathf.Sqrt(remainingRadiusSquared);
            return Mathf.Max(minimum, farSideDistance);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void UpdateZoomReset(float deltaTime)
        {
            if (!zoomResetAnimating)
            {
                return;
            }

            zoomResetElapsed += Mathf.Max(0f, deltaTime);
            float progress = zoomResetDuration > 0f
                ? Mathf.Clamp01(zoomResetElapsed / zoomResetDuration)
                : 1f;
            float easedProgress = progress * progress * (3f - 2f * progress);
            targetPosition = Vector3.LerpUnclamped(zoomResetStartPosition, framedBoundsCenter, easedProgress);
            distance = Mathf.LerpUnclamped(
                zoomResetStartDistance,
                Mathf.Clamp(defaultDistance, minimumDistance, maximumDistance),
                easedProgress);
            if (progress >= 1f)
            {
                distance = Mathf.Clamp(defaultDistance, minimumDistance, maximumDistance);
                targetPosition = framedBoundsCenter;
                zoomResetAnimating = false;
            }
        }

        private void BeginMouseTapCandidate(Vector2 position, bool pointerOverUi)
        {
            mouseTapCandidate = !pointerOverUi;
            mouseTapStartPosition = position;
        }

        private void CompleteMouseTapCandidate(Vector2 position)
        {
            if (mouseTapCandidate && !IsScreenPositionOverUI(position) &&
                Vector2.Distance(mouseTapStartPosition, position) <= tapMaximumMovement)
            {
                RegisterTap(position);
            }
            mouseTapCandidate = false;
        }

        private void UpdateTouchTapCandidate(
            bool pressedThisFrame,
            bool releasedThisFrame,
            bool isPressed,
            int touchCount,
            Vector2 position)
        {
            if (pressedThisFrame)
            {
                touchTapCandidate = touchCount == 1 && !IsScreenPositionOverUI(position);
                touchTapStartPosition = position;
            }
            if (touchCount > 1 || isPressed && touchTapCandidate &&
                Vector2.Distance(touchTapStartPosition, position) > tapMaximumMovement)
            {
                touchTapCandidate = false;
            }
            if (!releasedThisFrame)
            {
                return;
            }

            if (touchTapCandidate && !IsScreenPositionOverUI(position) &&
                Vector2.Distance(touchTapStartPosition, position) <= tapMaximumMovement)
            {
                RegisterTap(position);
            }
            touchTapCandidate = false;
        }

        private void RegisterTap(Vector2 position)
        {
            float now = Time.unscaledTime;
            bool isDoubleTap = now - lastTapTime <= doubleTapMaximumDelay &&
                Vector2.Distance(lastTapPosition, position) <= doubleTapMaximumSeparation;
            if (isDoubleTap)
            {
                ResetZoom();
                lastTapTime = -10f;
                return;
            }

            lastTapTime = now;
            lastTapPosition = position;
        }

        private void ApplyCameraTransform()
        {
            if (cameraPivot == null || targetCamera == null)
            {
                return;
            }

            float outsideDistance = GetMinimumOutsideDistance();
            if (distance < outsideDistance)
            {
                distance = outsideDistance;
                maximumDistance = Mathf.Max(maximumDistance, distance);
            }

            transform.position = targetPosition;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cameraPivot.localPosition = Vector3.zero;
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            targetCamera.transform.localPosition = new Vector3(0f, 0f, -distance);
            targetCamera.transform.localRotation = Quaternion.identity;
        }

        private void ApplyCameraFillLightSettings()
        {
            if (cameraFillLight == null)
            {
                return;
            }

            cameraFillLight.enabled = useCameraFillLight;
            cameraFillLight.type = LightType.Directional;
            cameraFillLight.color = cameraFillColor;
            cameraFillLight.intensity = Mathf.Max(0f, cameraFillIntensity);
            cameraFillLight.shadows = LightShadows.None;
            cameraFillLight.renderMode = LightRenderMode.Auto;
        }

        private static bool IsScreenPositionOverUI(Vector2 screenPosition)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            if (uiEventSystem != EventSystem.current || uiPointerData == null)
            {
                uiEventSystem = EventSystem.current;
                uiPointerData = new PointerEventData(uiEventSystem);
            }
            uiPointerData.Reset();
            uiPointerData.position = screenPosition;
            UiRaycastResults.Clear();
            EventSystem.current.RaycastAll(uiPointerData, UiRaycastResults);
            bool overUi = UiRaycastResults.Count > 0;
            UiRaycastResults.Clear();
            return overUi;
        }

        private void OnValidate()
        {
            cameraFillIntensity = Mathf.Max(0f, cameraFillIntensity);
            minimumDistance = Mathf.Max(0.1f, minimumDistance);
            maximumDistance = Mathf.Max(minimumDistance, maximumDistance);
            maximumPitch = Mathf.Max(minimumPitch, maximumPitch);
            framePadding = Mathf.Max(1f, framePadding);
            doubleTapMaximumDelay = Mathf.Clamp(doubleTapMaximumDelay, 0.1f, 0.6f);
            tapMaximumMovement = Mathf.Max(0f, tapMaximumMovement);
            doubleTapMaximumSeparation = Mathf.Max(0f, doubleTapMaximumSeparation);
            zoomResetDuration = Mathf.Max(0f, zoomResetDuration);
            defaultDistance = Mathf.Clamp(defaultDistance, minimumDistance, maximumDistance);
            ApplyCameraFillLightSettings();
        }
    }
}
