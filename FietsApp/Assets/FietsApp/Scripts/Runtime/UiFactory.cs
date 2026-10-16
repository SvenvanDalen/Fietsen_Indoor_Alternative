using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>Bouwstenen voor UI in code (uGUI met LegacyRuntime.ttf): canvas, tekst en knop.</summary>
    public static class UiFactory
    {
        /// <summary>Knoppen werken alleen met een EventSystem; met het nieuwe Input System via InputSystemUIInputModule.</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        /// <summary>Overlay-canvas dat meeschaalt met het scherm (ontworpen op 1920×1080).</summary>
        public static Transform Canvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return go.transform;
        }

        /// <summary>Tekstregel over de volle breedte van <paramref name="parent"/>, <paramref name="y"/> vanaf de bovenkant.</summary>
        public static Text Text(RectTransform parent, string name, int size, float y, float height)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var rt = text.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(16f, y - height);
            rt.offsetMax = new Vector2(-16f, y);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Blauwe knop met een label, linksboven op <paramref name="topLeft"/>.</summary>
        public static (Button button, Text label) Button(RectTransform parent, string name, Vector2 topLeft, float width,
            float height, UnityEngine.Events.UnityAction onClick)
        {
            var rt = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = new Vector2(width, height);
            rt.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.8f, 1f);
            var button = rt.GetComponent<Button>();
            if (onClick != null) button.onClick.AddListener(onClick);

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.rectTransform.SetParent(rt, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            return (button, label);
        }
    }
}
