using UnityEngine;
using UnityEngine.UI;

namespace HillbillyTaxi.Interaction
{
    /// <summary>
    /// Temporary code-generated interaction prompt. It keeps this first interaction
    /// milestone independent from the final HUD design and can be replaced later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private string keyboardKeyLabel = "E";

        [Header("Layout")]
        [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, -140f);
        [SerializeField] private Vector2 panelSize = new Vector2(420f, 54f);

        private CanvasGroup _canvasGroup;
        private Text _promptText;

        public void Show(string prompt)
        {
            EnsureBuilt();

            _promptText.text = $"[{keyboardKeyLabel}]  {prompt}";
            _canvasGroup.alpha = 1f;
        }

        public void Hide()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
        }

        private void EnsureBuilt()
        {
            if (_canvasGroup != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject(
                "Interaction Prompt Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup));

            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            GameObject panelObject = new GameObject(
                "Prompt Background",
                typeof(RectTransform),
                typeof(Image));

            panelObject.transform.SetParent(canvasObject.transform, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = anchoredPosition;
            panelRect.sizeDelta = panelSize;

            Image background = panelObject.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.68f);
            background.raycastTarget = false;

            GameObject textObject = new GameObject(
                "Prompt Text",
                typeof(RectTransform),
                typeof(Text));

            textObject.transform.SetParent(panelObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 6f);
            textRect.offsetMax = new Vector2(-18f, -6f);

            _promptText = textObject.GetComponent<Text>();
            _promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _promptText.fontSize = 24;
            _promptText.alignment = TextAnchor.MiddleCenter;
            _promptText.color = Color.white;
            _promptText.raycastTarget = false;
            _promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _promptText.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }
}
