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
        [SerializeField, Min(1f)] private float pressScale = 1.10f;

        private Vector3 restingScale = Vector3.one;

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
            transform.DOKill();
            transform.localScale = restingScale;
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
            transform.DOKill();transform.localScale=restingScale;
            FloodTouchInput3D.PressFeedback(transform,Mathf.Max(0,pressScale-1f),.28f);
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
            transform.DOKill();
        }
    }
}
