using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FloodFill
{
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public sealed class Cell : MonoBehaviour, IPointerClickHandler
    {
        private const float CaptureStartScale = 0.85f;
        private const float CaptureDuration = 0.16f;
        private const float SelectedScale = 0.82f;

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private BoxCollider2D cellCollider;

        private Vector3 restingScale = Vector3.one;
        private bool isSelected;

        public int X { get; private set; }
        public int Y { get; private set; }
        public int ColorIndex { get; private set; }
        public bool IsCaptured { get; private set; }
        public SpriteRenderer SpriteRenderer => spriteRenderer;

        public event Action<Cell> Clicked;

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

        public void SetSelected(bool selected)
        {
            if (isSelected == selected)
            {
                return;
            }

            isSelected = selected;
            transform.DOKill();
            transform.localScale = GetTargetScale();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke(this);
            }
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
