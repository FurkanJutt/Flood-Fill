using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace FloodFill.ThreeD
{
    public sealed class VoxelCell3D : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FlatKitShadedColorId = Shader.PropertyToID("_ColorDim");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock sharedPropertyBlock;

        [SerializeField] private Transform visual;
        [SerializeField] private MeshRenderer[] meshRenderers;
        [SerializeField, Min(1f)] private float capturedScale = 1.025f;
        [SerializeField, Range(0.1f, 1f)] private float captureStartScale = 0.82f;
        [SerializeField, Min(0.01f)] private float captureDuration = 0.28f;

        private Vector3 restingScale = Vector3.one;
        private Tween captureTween;

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
            SetColor(colorIndex, color);
        }

        public void SetColor(int colorIndex, Color color)
        {
            ColorIndex = colorIndex;
            EnsureRenderers();
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

        public void SetCaptured(bool captured, bool animate)
        {
            if (IsCaptured == captured)
            {
                return;
            }

            IsCaptured = captured;
            captureTween?.Kill();
            Vector3 targetScale = restingScale * (captured ? capturedScale : 1f);
            if (captured && animate)
            {
                transform.localScale = targetScale * captureStartScale;
                captureTween = transform.DOScale(targetScale, captureDuration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() => captureTween = null);
            }
            else
            {
                transform.localScale = targetScale;
            }
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

        private void OnDestroy()
        {
            captureTween?.Kill();
            transform.DOKill();
        }
    }
}
