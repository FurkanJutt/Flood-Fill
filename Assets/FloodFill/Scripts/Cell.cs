using DG.Tweening;
using UnityEngine;

namespace FloodFill
{
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public sealed class Cell : MonoBehaviour
    {
        private const float CaptureStartScale = 0.85f;
        private const float CaptureDuration = 0.16f;
        private const float SelectedScale = 0.82f;
        private const float WaveAnticipationDuration = 0.04f;
        private const float WaveFlashDuration = 0.09f;
        private const float WaveFillDuration = 0.16f;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private BoxCollider2D cellCollider;

        [Header("Pointer Pop Animation")]
        [SerializeField, Min(1f)] private float pointerPopScale = 1.12f;
        [SerializeField, Min(0.01f)] private float pointerPopUpDuration = 0.18f;
        [SerializeField, Min(0.01f)] private float pointerPopDownDuration = 0.30f;

        private Vector3 restingScale = Vector3.one;
        private bool isSelected;
        private Sequence colorWaveSequence;
        private Sequence pointerPopSequence;

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
            isSelected = false;
            restingScale = transform.localScale;
            EnsureRenderer();
            EnsureCollider();
            SetColor(colorIndex, color);
        }

        public void SetColor(int colorIndex, Color color)
        {
            ColorIndex = colorIndex;
            EnsureRenderer();

            if (spriteRenderer != null)
            {
                colorWaveSequence?.Kill();
                colorWaveSequence = null;
                spriteRenderer.color = color;
            }
        }

        public float AnimateColor(int colorIndex, Color color, float delay)
        {
            ColorIndex = colorIndex;
            EnsureRenderer();
            if (spriteRenderer == null)
            {
                return 0f;
            }

            float safeDelay = Mathf.Max(0f, delay);
            colorWaveSequence?.Kill();
            Color anticipationColor = Color.Lerp(spriteRenderer.color, Color.black, 0.16f);
            Color flashColor = Color.Lerp(color, Color.white, 0.52f);
            colorWaveSequence = DOTween.Sequence()
                .AppendInterval(safeDelay)
                .Append(spriteRenderer.DOColor(anticipationColor, WaveAnticipationDuration).SetEase(Ease.InQuad))
                .Append(spriteRenderer.DOColor(flashColor, WaveFlashDuration).SetEase(Ease.OutCubic))
                .Append(spriteRenderer.DOColor(color, WaveFillDuration).SetEase(Ease.OutSine))
                .OnComplete(() => colorWaveSequence = null);

            return safeDelay + WaveAnticipationDuration + WaveFlashDuration + WaveFillDuration;
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

        public void SetSelected(bool selected)
        {
            if (isSelected == selected)
            {
                return;
            }

            isSelected = selected;
            pointerPopSequence?.Kill();
            pointerPopSequence = null;
            transform.DOKill();
            transform.localScale = GetTargetScale();
        }

        public void StartPointerPopLoop()
        {
            if (pointerPopSequence != null && pointerPopSequence.IsActive())
            {
                return;
            }

            pointerPopSequence?.Kill();
            transform.DOKill();
            Vector3 targetScale = GetTargetScale();
            pointerPopSequence = DOTween.Sequence()
                .Append(transform.DOScale(targetScale * pointerPopScale, pointerPopUpDuration)
                    .SetEase(Ease.OutBack))
                .Append(transform.DOScale(targetScale, pointerPopDownDuration)
                    .SetEase(Ease.OutSine))
                .SetLoops(-1, LoopType.Restart);
        }

        public void StopPointerPop()
        {
            pointerPopSequence?.Kill();
            pointerPopSequence = null;
            transform.DOKill();
            transform.DOScale(GetTargetScale(), pointerPopDownDuration).SetEase(Ease.OutSine);
        }

        private void PlayCaptureAnimation()
        {
            transform.DOKill();
            Vector3 targetScale = GetTargetScale();
            transform.localScale = targetScale * CaptureStartScale;
            transform.DOScale(targetScale, CaptureDuration).SetEase(Ease.OutBack);
        }

        private Vector3 GetTargetScale()
        {
            return restingScale * (isSelected ? SelectedScale : 1f);
        }

        private void EnsureRenderer()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void EnsureCollider()
        {
            if (cellCollider == null)
            {
                cellCollider = GetComponent<BoxCollider2D>();
            }

            if (cellCollider != null)
            {
                cellCollider.size = Vector2.one;
                cellCollider.offset = Vector2.zero;
                cellCollider.enabled = true;
            }
        }

        private void OnDestroy()
        {
            colorWaveSequence?.Kill();
            pointerPopSequence?.Kill();
            if (spriteRenderer != null)
            {
                spriteRenderer.DOKill();
            }

            transform.DOKill();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureRenderer();
            EnsureCollider();
        }
#endif
    }
}
