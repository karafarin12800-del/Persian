using UnityEngine;
using UnityEngine.UI;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Native Unity UI boot overlay. It does not depend on a Camera and is kept
    /// separate from the legacy IMGUI fallback in BootLoader.
    /// </summary>
    public sealed class BootOverlay : MonoBehaviour
    {
        private Canvas canvas;
        private Image panel;
        private Image topRule;
        private Image bottomRule;
        private Text title;
        private Text status;
        private Text percent;
        private Text footer;
        private Font builtinFont;

        public bool IsReady => canvas != null && title != null && status != null;

        public void Initialize()
        {
            if (IsReady) return;

            builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject canvasObject = new GameObject("BootOverlayCanvas");
            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panelObject = CreateRect("Panel", canvasObject.transform);
            panel = panelObject.AddComponent<Image>();
            panel.color = new Color(0.055f, 0.085f, 0.125f, 0.98f);
            SetCentered(panelObject.GetComponent<RectTransform>(), new Vector2(760f, 330f));

            topRule = CreateRule("TopRule", panelObject.transform);
            bottomRule = CreateRule("BottomRule", panelObject.transform);
            SetAnchored(topRule.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -5f), new Vector2(0f, 0f), new Vector2(0f, 5f));
            SetAnchored(bottomRule.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 5f), new Vector2(0f, 0f));

            title = CreateText("Title", panelObject.transform, 42, FontStyle.Bold);
            SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -145f), new Vector2(-20f, 0f), new Vector2(0f, -58f));
            title.alignment = TextAnchor.MiddleCenter;
            title.text = "PERSIA WAR";

            status = CreateText("Status", panelObject.transform, 20, FontStyle.Normal);
            SetAnchored(status.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(35f, -195f), new Vector2(-35f, -150f), new Vector2(0f, -34f));
            status.alignment = TextAnchor.MiddleCenter;

            Image barBackground = CreateImage("BarBackground", panelObject.transform, new Color(0.12f, 0.15f, 0.19f, 1f));
            SetAnchored(barBackground.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(60f, 0f), new Vector2(-60f, 0f), new Vector2(0f, -30f));

            Image barFill = CreateImage("BarFill", barBackground.transform, new Color(0.88f, 0.65f, 0.20f, 1f));
            RectTransform fillRect = barFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, 0f);

            GameObject stateObject = new GameObject("BootOverlayState");
            stateObject.transform.SetParent(panelObject.transform, false);
            stateObject.transform.SetSiblingIndex(panelObject.transform.childCount - 1);
            BootOverlayState state = stateObject.AddComponent<BootOverlayState>();
            state.BindFill(barFill, barBackground);

            percent = CreateText("Percent", panelObject.transform, 18, FontStyle.Bold);
            SetAnchored(percent.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -55f), new Vector2(0f, -20f), new Vector2(0f, -30f));
            percent.alignment = TextAnchor.MiddleCenter;

            footer = CreateText("Footer", panelObject.transform, 14, FontStyle.Normal);
            SetAnchored(footer.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(35f, 35f), new Vector2(-35f, 65f), new Vector2(0f, -30f));
            footer.alignment = TextAnchor.MiddleCenter;
            footer.text = "Preparing the battlefield • Please wait.";

            SetProgress(0f, "Starting Persia War...", false, string.Empty);
        }

        public void SetProgress(float value, string message, bool failed, string failureMessage)
        {
            if (!IsReady) return;

            value = Mathf.Clamp01(value);
            status.text = message ?? string.Empty;
            percent.text = Mathf.RoundToInt(value * 100f) + "%";
            footer.text = failed && !string.IsNullOrEmpty(failureMessage)
                ? failureMessage
                : "Preparing the battlefield • Please wait.";

            BootOverlayState state = GetComponentInChildren<BootOverlayState>(true);
            if (state != null)
                state.SetProgress(value, failed);
        }

        private Image CreateRule(string name, Transform parent)
        {
            return CreateImage(name, parent, new Color(0.88f, 0.65f, 0.20f, 0.90f));
        }

        private Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = CreateRect(name, parent);
            Image image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateText(string name, Transform parent, int fontSize, FontStyle fontStyle)
        {
            GameObject go = CreateRect(name, parent);
            Text text = go.AddComponent<Text>();
            text.font = builtinFont;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetCentered(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void SetAnchored(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            if (size != Vector2.zero)
                rect.sizeDelta = size;
        }
    }

    internal sealed class BootOverlayState : MonoBehaviour
    {
        private Image fill;
        private RectTransform fillRect;
        private RectTransform backgroundRect;

        public void BindFill(Image value, Image background)
        {
            fill = value;
            fillRect = value.rectTransform;
            backgroundRect = background.rectTransform;
            SetProgress(0f, false);
        }

        public void SetProgress(float value, bool failed)
        {
            if (fill == null || fillRect == null || backgroundRect == null) return;
            fill.color = failed
                ? new Color(0.70f, 0.18f, 0.14f, 1f)
                : new Color(0.88f, 0.65f, 0.20f, 1f);
            float width = backgroundRect.rect.width;
            fillRect.sizeDelta = new Vector2(Mathf.Max(0f, width) * Mathf.Clamp01(value), 0f);
        }
    }
}
