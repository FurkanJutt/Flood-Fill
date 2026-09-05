using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FloodFill.ThreeD
{
    [RequireComponent(typeof(Button), typeof(Image))]
    public sealed class ColorButton3D : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private int colorIndex;
        [SerializeField] private Button button;
        [SerializeField] private Image buttonImage;
        [SerializeField] private FloodFillGameManager3D gameManager;
        [SerializeField, Min(1f)] private float selectedScale = 1.12f;
        [SerializeField, Min(1f)] private float pressScale = 1.10f;

        private Vector3 restingScale = Vector3.one;
        private bool selected;
        private Sequence pressSequence;

        public int ColorIndex => colorIndex;

        public void Configure(int index, Color color, FloodFillGameManager3D manager)
        {
            colorIndex = index;
            gameManager = manager;
            EnsureReferences();
            buttonImage.color = color;
        }

        public void SetSelected(bool value)
        {
            selected = value;
            pressSequence?.Kill();
            transform.DOKill();
            transform.localScale = restingScale * (selected ? selectedScale : 1f);
        }

        public void SetInteractable(bool value)
        {
            EnsureReferences();
            button.interactable = value;
        }

        private void Awake()
        {
            restingScale = transform.localScale;
            EnsureReferences();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || button == null ||
                !button.interactable)
            {
                return;
            }

            gameManager?.SelectColor(colorIndex);
            PlayPressAnimation();
        }

        private void PlayPressAnimation()
        {
            pressSequence?.Kill();
            transform.DOKill();
            Vector3 target = restingScale * (selected ? selectedScale : 1f);
            pressSequence = DOTween.Sequence()
                .Append(transform.DOScale(target * pressScale, 0.10f).SetEase(Ease.OutBack))
                .Append(transform.DOScale(target, 0.18f).SetEase(Ease.OutSine))
                .OnComplete(() => pressSequence = null);
        }

        private void EnsureReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (buttonImage == null)
            {
                buttonImage = GetComponent<Image>();
            }
        }

        private void OnDestroy()
        {
            pressSequence?.Kill();
            transform.DOKill();
        }
    }
}
