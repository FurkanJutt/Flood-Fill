using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FloodFill
{
    [RequireComponent(typeof(Button), typeof(Image))]
    public sealed class ColorButton : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private int colorIndex;
        [SerializeField] private Button button;
        [SerializeField] private Image buttonImage;
        [SerializeField] private GameManager gameManager;

        [Header("Press Animation")]
        [SerializeField, Min(1f)] private float selectedScale = 1.12f;
        [SerializeField, Min(1f)] private float pressPopScale = 1.10f;
        [SerializeField, Min(0.01f)] private float pressPopUpDuration = 0.10f;
        [SerializeField, Min(0.01f)] private float pressPopDownDuration = 0.18f;

        private Vector3 restingScale = Vector3.one;
        private bool isSelected;
        private Sequence pressSequence;

        public int ColorIndex => colorIndex;

        public void Configure(int index, Color color, GameManager manager)
        {
            colorIndex = index;
            gameManager = manager;
            EnsureReferences();

            if (buttonImage != null)
            {
                buttonImage.color = color;
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        public void SetVisualColor(Color color)
        {
            if (buttonImage != null)
            {
                buttonImage.color = color;
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            pressSequence?.Kill();
            pressSequence = null;
            transform.DOKill();
            transform.localScale = GetTargetScale();
        }

        private void Awake()
        {
            restingScale = transform.localScale;
            EnsureReferences();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            SelectColorImmediately();
            PlayPressAnimation();
        }

        private void SelectColorImmediately()
        {
            if (gameManager == null)
            {
                Debug.LogError("Flood Fill color button has no GameManager reference.", this);
                return;
            }

            gameManager.SelectColor(colorIndex);
        }

        private void PlayPressAnimation()
        {
            pressSequence?.Kill();
            transform.DOKill();
            Vector3 targetScale = GetTargetScale();
            pressSequence = DOTween.Sequence()
                .Append(transform.DOScale(targetScale * pressPopScale, pressPopUpDuration)
                    .SetEase(Ease.OutBack))
                .Append(transform.DOScale(targetScale, pressPopDownDuration)
                    .SetEase(Ease.OutSine))
                .OnComplete(() => pressSequence = null);
        }

        private Vector3 GetTargetScale()
        {
            return restingScale * (isSelected ? selectedScale : 1f);
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureReferences();
        }
#endif
    }
}
