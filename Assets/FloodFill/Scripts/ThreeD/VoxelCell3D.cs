using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace FloodFill.ThreeD
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class VoxelCell3D : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FlatKitShadedColorId = Shader.PropertyToID("_ColorDim");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock sharedPropertyBlock;

        [SerializeField] private Transform visual;
        [SerializeField] private MeshRenderer[] meshRenderers;
        [SerializeField] private BoxCollider voxelCollider;
        [SerializeField, Min(1f)] private float capturedScale = 1.025f;
        [SerializeField, Range(0.1f, 1f)] private float captureStartScale = 0.82f;
        [SerializeField, Min(0.01f)] private float captureDuration = 0.28f;

        [Header("Recolor Wave")]
        [SerializeField, Range(0f, 1f)] private float waveAnticipationDarken = 0.24f;
        [SerializeField, Range(0f, 1f)] private float waveFlashBrightness = 0.72f;
        [SerializeField, Range(0.1f, 1f)] private float waveAnticipationScale = 0.88f;
        [SerializeField, Min(1f)] private float wavePopScale = 1.18f;
        [SerializeField, Min(0.01f)] private float waveAnticipationDuration = 0.07f;
        [SerializeField, Min(0.01f)] private float waveFlashDuration = 0.13f;
        [SerializeField, Min(0.01f)] private float waveSettleDuration = 0.24f;

        private Vector3 restingScale = Vector3.one;
        private Vector3 visualRestingScale = Vector3.one;
        private Color displayedColor = Color.white;
        private Tween captureTween;
        private Sequence colorWaveSequence;

        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }
        public int ColorIndex { get; private set; }
        public bool IsCaptured { get; private set; }
        public IReadOnlyList<MeshRenderer> MeshRenderers => meshRenderers;

        public void Configure(Transform visualRoot, MeshRenderer[] renderers)
        {
            visual = visualRoot;
            meshRenderers = renderers;
        }

        public void Initialize(int x, int y, int z, int colorIndex, Color color)
        {
            X = x;
            Y = y;
            Z = z;
            IsCaptured = false;
            restingScale = transform.localScale;
            EnsureRenderers();
            EnsureCollider();
            if (visual != null)
            {
                visualRestingScale = visual.localScale;
            }
            SetColor(colorIndex, color);
        }

        public void SetColor(int colorIndex, Color color)
        {
            ColorIndex = colorIndex;
            EnsureRenderers();
            colorWaveSequence?.Kill();
            colorWaveSequence = null;
            if (visual != null)
            {
                visual.DOKill();
                visual.localScale = visualRestingScale;
            }

            displayedColor = color;
            ApplyDisplayColor(color);
        }

        public float AnimateColor(int colorIndex, Color color, float delay)
        {
            ColorIndex = colorIndex;
            EnsureRenderers();
            float safeDelay = Mathf.Max(0f, delay);
            Color startColor = displayedColor;
            Color anticipationColor = Color.Lerp(startColor, Color.black, waveAnticipationDarken);
            Color flashColor = Color.Lerp(color, Color.white, waveFlashBrightness);

            colorWaveSequence?.Kill();
            if (visual != null)
            {
                visual.DOKill();
                visual.localScale = visualRestingScale;
            }

            colorWaveSequence = DOTween.Sequence().AppendInterval(safeDelay);
            colorWaveSequence.Append(
                DOVirtual.Color(
                    startColor,
                    anticipationColor,
                    waveAnticipationDuration,
                    ApplyDisplayColor)
                .SetEase(Ease.InQuad));
            if (visual != null)
            {
                colorWaveSequence.Join(
                    visual.DOScale(
                            visualRestingScale * waveAnticipationScale,
                            waveAnticipationDuration)
                        .SetEase(Ease.InQuad));
            }

            colorWaveSequence.Append(
                DOVirtual.Color(
                    anticipationColor,
                    flashColor,
                    waveFlashDuration,
                    ApplyDisplayColor)
                .SetEase(Ease.OutCubic));
            if (visual != null)
            {
                colorWaveSequence.Join(
                    visual.DOScale(visualRestingScale * wavePopScale, waveFlashDuration)
                        .SetEase(Ease.OutBack));
            }

            colorWaveSequence.Append(
                DOVirtual.Color(
                    flashColor,
                    color,
                    waveSettleDuration,
                    ApplyDisplayColor)
                .SetEase(Ease.OutSine));
            if (visual != null)
            {
                colorWaveSequence.Join(
                    visual.DOScale(visualRestingScale, waveSettleDuration)
                        .SetEase(Ease.OutSine));
            }

            colorWaveSequence.OnComplete(() =>
            {
                displayedColor = color;
                ApplyDisplayColor(color);
                colorWaveSequence = null;
            });

            return safeDelay + waveAnticipationDuration + waveFlashDuration + waveSettleDuration;
        }

        private void ApplyDisplayColor(Color color)
        {
            displayedColor = color;
            if (sharedPropertyBlock == null)
            {
                sharedPropertyBlock = new MaterialPropertyBlock();
            }

            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshRenderer targetRenderer = meshRenderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(sharedPropertyBlock);
                Material material = targetRenderer.sharedMaterial;
                if (material != null && material.HasProperty(BaseColorId))
                {
                    sharedPropertyBlock.SetColor(BaseColorId, color);
                    if (material.HasProperty(FlatKitShadedColorId))
                    {
                        sharedPropertyBlock.SetColor(
                            FlatKitShadedColorId,
                            GetFlatKitShadedColor(color));
                    }
                }
                else
                {
                    sharedPropertyBlock.SetColor(ColorId, color);
                }

                targetRenderer.SetPropertyBlock(sharedPropertyBlock);
                sharedPropertyBlock.Clear();
            }
        }

        private static Color GetFlatKitShadedColor(Color baseColor)
        {
            // Flat Kit's single-step cel mode blends from _ColorDim to _BaseColor.
            // Keep the shadow tied to the gameplay hue while giving it a subtle cool bias.
            return new Color(
                baseColor.r * 0.55f,
                baseColor.g * 0.58f,
                baseColor.b * 0.68f,
                baseColor.a);
        }

        public float SetCaptured(bool captured, bool animate, float delay = 0f)
        {
            if (IsCaptured == captured)
            {
                return 0f;
            }

            IsCaptured = captured;
            captureTween?.Kill();
            Vector3 targetScale = restingScale * (captured ? capturedScale : 1f);
            if (captured && animate)
            {
                float safeDelay = Mathf.Max(0f, delay);
                captureTween = DOTween.Sequence()
                    .AppendInterval(safeDelay)
                    .AppendCallback(() => transform.localScale = targetScale * captureStartScale)
                    .Append(transform.DOScale(targetScale, captureDuration)
                        .SetEase(Ease.OutBack))
                    .OnComplete(() => captureTween = null);
                return safeDelay + captureDuration;
            }

            transform.localScale = targetScale;
            return 0f;
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            EnsureRenderers();
            bounds = default;
            bool foundRenderer = false;
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshRenderer targetRenderer = meshRenderers[i];
                if (targetRenderer == null || !targetRenderer.enabled)
                {
                    continue;
                }

                if (!foundRenderer)
                {
                    bounds = targetRenderer.bounds;
                    foundRenderer = true;
                }
                else
                {
                    bounds.Encapsulate(targetRenderer.bounds);
                }
            }

            return foundRenderer;
        }

        private void EnsureRenderers()
        {
            if (meshRenderers == null || meshRenderers.Length == 0)
            {
                Transform searchRoot = visual != null ? visual : transform;
                meshRenderers = searchRoot.GetComponentsInChildren<MeshRenderer>(true);
            }
        }

        private void EnsureCollider()
        {
            if (voxelCollider == null)
            {
                voxelCollider = GetComponent<BoxCollider>();
            }

            if (voxelCollider == null)
            {
                voxelCollider = gameObject.AddComponent<BoxCollider>();
            }

            voxelCollider.center = Vector3.zero;
            voxelCollider.size = Vector3.one;
            voxelCollider.enabled = true;
        }

        private void OnDestroy()
        {
            captureTween?.Kill();
            colorWaveSequence?.Kill();
            if (visual != null)
            {
                visual.DOKill();
            }
            transform.DOKill();
        }
    }
}
