using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Yowol
{
    /// <summary>Small helpers to build uGUI hierarchies from code.</summary>
    public static class UIBuild
    {
        public static Canvas Canvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;
            return c;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                                         Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, Vector2 anchor, Vector2 pivot,
                                  Vector2 pos, Vector2 size, bool preserveAspect = false)
        {
            var rt = Rect(name, parent, anchor, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
                                           TextAlignmentOptions align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box,
                                           bool title = false)
        {
            var rt = Rect(name, parent, anchor, anchor, pivot, pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var a = GameAssets.I;
            if (a != null) t.font = title && a.titleFont != null ? a.titleFont : a.bodyFont;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public static Button Button(string name, Transform parent, string label, Vector2 pos, Vector2 size, System.Action onClick)
        {
            var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.08f, 0.1f, 0.16f, 0.85f);
            var b = rt.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(0.35f, 0.55f, 0.85f, 1f);
            colors.pressedColor = new Color(0.25f, 0.4f, 0.7f, 1f);
            colors.selectedColor = colors.highlightedColor;
            b.colors = colors;
            b.onClick.AddListener(() =>
            {
                AudioHub.Sfx(GameAssets.I.sfxUiClick, 0.8f, 0f);
                onClick?.Invoke();
            });
            var t = Text("Label", rt, label, 34, Color.white, TextAlignmentOptions.Center,
                         new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            t.raycastTarget = false;
            return b;
        }
    }
}
