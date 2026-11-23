using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

namespace FloodFill.ThreeD
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class VoxelCell3D : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FlatKitShadedColorId = Shader.PropertyToID("_ColorDim");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock sharedPropertyBlock;
        private static readonly Dictionary<(Material, Color), Material> ColorMaterials = new Dictionary<(Material, Color), Material>();
        private struct RendererColorBinding
        {
            public Material[] SourceMaterials, ColoredMaterials;
            public int ColorProperty;
            public bool HasShadedColor;
        }
        private RendererColorBinding[] rendererColorBindings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetColorMaterials()
        {
            foreach (Material material in ColorMaterials.Values)
                if (material != null) Destroy(material);
            ColorMaterials.Clear();
            sharedPropertyBlock = null;
        }

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
        private bool hasVisualRestingScale;
        private Color displayedColor = Color.white;
        private Tween captureTween;
        private Sequence colorWaveSequence;

        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }
        public int ColorIndex { get; private set; }
        public bool IsCaptured { get; private set; }
        // Visual-only notification at the actual tween phase, rather than a second VFX clock.
        public event System.Action<VoxelCell3D, bool> WavefrontReached;
        public IReadOnlyList<MeshRenderer> MeshRenderers => meshRenderers;

        public void Configure(Transform visualRoot, MeshRenderer[] renderers)
        {
            visual = visualRoot;
            meshRenderers = renderers;
            rendererColorBindings = null;
        }

        public void Initialize(int x, int y, int z, int colorIndex, Color color)
        {
            PrepareForReuse(x, y, z);
            SetColor(colorIndex, color);
        }

        public void PrepareForReuse(int x, int y, int z)
        {
            StopAnimations();
            X = x;
            Y = y;
            Z = z;
            IsCaptured = false;
            restingScale = transform.localScale;
            EnsureRenderers();
            EnsureCollider();
            if (visual != null)
            {
                if (!hasVisualRestingScale)
                {
                    visualRestingScale = visual.localScale;
                    hasVisualRestingScale = true;
                }
                else
                {
                    visual.localScale = visualRestingScale;
                }
            }
        }

        public void ResetForPool()
        {
            StopAnimations();
            EnsureRenderers();
            for (int i = 0; i < meshRenderers.Length; i++)
                if (meshRenderers[i] != null) meshRenderers[i].SetPropertyBlock(null);
            IsCaptured = false;
            if (visual != null && hasVisualRestingScale)
            {
                visual.localScale = visualRestingScale;
            }

            if (voxelCollider != null)
            {
                voxelCollider.enabled = false;
            }
        }

        public void SetShadowCasting(bool castShadows)
        {
            EnsureRenderers();
            ShadowCastingMode mode = castShadows
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if (meshRenderers[i] != null)
                {
                    meshRenderers[i].shadowCastingMode = mode;
                }
            }
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

            ApplySteadyColor(color);
        }

        public float ColorWaveStartOffset => Mathf.Max(0f, waveAnticipationDuration);

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

            if (WavefrontReached != null)
                colorWaveSequence.AppendCallback(() => WavefrontReached?.Invoke(this, true));
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
                ApplySteadyColor(color);
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

                RendererColorBinding binding = rendererColorBindings[i];
                sharedPropertyBlock.SetColor(binding.ColorProperty, color);
                if (binding.HasShadedColor)
                    sharedPropertyBlock.SetColor(FlatKitShadedColorId, GetFlatKitShadedColor(color));

                targetRenderer.SetPropertyBlock(sharedPropertyBlock);
                sharedPropertyBlock.Clear();
            }
        }

        private void ApplySteadyColor(Color color)
        {
            displayedColor = color;
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshRenderer renderer = meshRenderers[i];
                if (renderer == null) continue;
                RendererColorBinding binding = rendererColorBindings[i];
                for (int slot = 0; slot < binding.SourceMaterials.Length; slot++)
                    binding.ColoredMaterials[slot] = GetColorMaterial(binding.SourceMaterials[slot], color);
                if (binding.ColoredMaterials.Length == 1)
                    renderer.sharedMaterial = binding.ColoredMaterials[0];
                else
                    renderer.sharedMaterials = binding.ColoredMaterials;
                // Persistent color overrides disable batching with the Flat Kit shader.
                // Use a shared palette material at rest and overrides only while tweening.
                renderer.SetPropertyBlock(null);
            }
        }

        private static Material GetColorMaterial(Material source, Color color)
        {
            if (source == null) return null;
            if (!source.HasProperty(BaseColorId) && !source.HasProperty(ColorId)) return source;
            var key = (source, color);
            if (ColorMaterials.TryGetValue(key, out Material material) && material != null) return material;
            material = new Material(source) { name = source.name + " (Voxel Palette)", hideFlags = HideFlags.DontSave };
            material.SetColor(source.HasProperty(BaseColorId) ? BaseColorId : ColorId, color);
            if (source.HasProperty(FlatKitShadedColorId))
                material.SetColor(FlatKitShadedColorId, GetFlatKitShadedColor(color));
            ColorMaterials[key] = material;
            return material;
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
                    .AppendCallback(() =>
                    {
                        transform.localScale = targetScale * captureStartScale;
                        WavefrontReached?.Invoke(this, false);
                    })
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
            if (rendererColorBindings != null && rendererColorBindings.Length == meshRenderers.Length) return;
            rendererColorBindings = new RendererColorBinding[meshRenderers.Length];
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if (meshRenderers[i] == null) continue;
                Material[] materials = meshRenderers[i].sharedMaterials;
                Material primary = materials.Length > 0 ? materials[0] : null;
                rendererColorBindings[i] = new RendererColorBinding
                {
                    SourceMaterials = materials,
                    ColoredMaterials = new Material[materials.Length],
                    ColorProperty = primary != null && primary.HasProperty(BaseColorId) ? BaseColorId : ColorId,
                    HasShadedColor = primary != null && primary.HasProperty(FlatKitShadedColorId)
                };
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

        private void StopAnimations()
        {
            captureTween?.Kill();
            captureTween = null;
            colorWaveSequence?.Kill();
            colorWaveSequence = null;
            if (visual != null)
            {
                visual.DOKill();
            }
            transform.DOKill();
        }

        private void OnDestroy()
        {
            StopAnimations();
        }
    }
}
