using UnityEngine;
using UnityEngine.UI;

namespace FloodFill
{
    [RequireComponent(typeof(Button), typeof(Image))]
    public sealed class ColorButton : MonoBehaviour
    {
        [SerializeField] private int colorIndex;
        [SerializeField] private Button button;
        [SerializeField] private Image buttonImage;
        [SerializeField] private GameManager gameManager;

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

        private void Awake()
        {
            EnsureReferences();
            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }

        private void HandleClick()
        {
            if (gameManager == null)
            {
                Debug.LogError("Flood Fill color button has no GameManager reference.", this);
                return;
            }

            gameManager.SelectColor(colorIndex);
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureReferences();
        }
#endif
    }
}
