using DG.Tweening;
using UnityEngine;

namespace FloodFill
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Cell : MonoBehaviour
    {
        private const float CaptureStartScale = 0.85f;
        private const float CaptureDuration = 0.16f;

        [SerializeField] private SpriteRenderer spriteRenderer;

        private Vector3 restingScale = Vector3.one;

        public int X { get; private set; }
        public int Y { get; private set; }
        public int ColorIndex { get; private set; }
        public bool IsCaptured { get; private set; }
        public SpriteRenderer SpriteRenderer => spriteRenderer;

        public void Initialize(int x, int y, int colorIndex, Color color)
        {
            X = x;
            Y = y;
            IsCaptured = false;
            restingScale = transform.localScale;
            EnsureRenderer();
            SetColor(colorIndex, color);
        }

        public void SetColor(int colorIndex, Color color)
        {
            ColorIndex = colorIndex;
            EnsureRenderer();

            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }

        public void SetCaptured(bool captured, bool animate = true)
        {
            if (IsCaptured == captured)
            {
                return;
            }

            IsCaptured = captured;
            if (captured && animate)
            {
                PlayCaptureAnimation();
            }
        }

        private void PlayCaptureAnimation()
        {
            transform.DOKill();
            transform.localScale = restingScale * CaptureStartScale;
            transform.DOScale(restingScale, CaptureDuration).SetEase(Ease.OutBack);
        }

        private void EnsureRenderer()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureRenderer();
        }
#endif
    }
}
