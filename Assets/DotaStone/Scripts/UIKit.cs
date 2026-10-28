using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DotaStone
{
    // Наведение мыши — для показа увеличенной карты
    public class HoverHandler : MonoBehaviour, IPointerEnterHandler
    {
        public System.Action Enter;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Enter != null) Enter();
        }
    }

    // Маленькие помощники, чтобы собирать интерфейс из кода без префабов
    public static class UIKit
    {
        public static Font Font { get; private set; }

        static Sprite circle;
        static readonly Dictionary<string, Texture2D> art = new Dictionary<string, Texture2D>();

        public static void Init()
        {
            if (Font == null) Font = LoadFont();
        }

        static Font LoadFont()
        {
            // В Unity 2022.2+ встроенный шрифт называется LegacyRuntime.ttf, раньше — Arial.ttf
            string[] parts = Application.unityVersion.Split('.');
            int major = 0, minor = 0;
            int.TryParse(parts[0], out major);
            if (parts.Length > 1) int.TryParse(parts[1], out minor);
            bool legacyName = major > 2022 || (major == 2022 && minor >= 2);

            Font f = Resources.GetBuiltinResource<Font>(legacyName ? "LegacyRuntime.ttf" : "Arial.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
            return f;
        }

        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                Color32[] px = new Color32[size * size];
                float r = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - r;
                        float dy = y + 0.5f - r;
                        float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return circle;
            }
        }

        // Картинка из Assets/DotaStone/Resources/Art/<id>.png (если есть)
        public static Texture2D Art(string id)
        {
            Texture2D tex;
            if (!art.TryGetValue(id, out tex))
            {
                tex = Resources.Load<Texture2D>("Art/" + id);
                art[id] = tex;
            }
            return tex;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // Растянуть по долям родителя (0..1)
        public static void Anchor(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Stretch(RectTransform rt)
        {
            Anchor(rt, 0, 0, 1, 1);
        }

        public static void Inset(RectTransform rt, float px)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(px, px);
            rt.offsetMax = new Vector2(-px, -px);
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            RectTransform rt = Rect(parent, name);
            Stretch(rt);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
            TextAnchor align = TextAnchor.MiddleCenter, bool bold = false, bool fit = false)
        {
            RectTransform rt = Rect(parent, "Text");
            Stretch(rt);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = fit ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = fit;
            t.resizeTextMinSize = 8;
            t.resizeTextMaxSize = size;
            t.supportRichText = true;
            t.raycastTarget = false;
            Shadow s = rt.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, 0.85f);
            s.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        public static Button MakeButton(Transform parent, string label, Color color, System.Action onClick, int fontSize = 26)
        {
            RectTransform rt = Rect(parent, "Button: " + label);
            Stretch(rt);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            Button b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            Label(rt, label, fontSize, Color.white, TextAnchor.MiddleCenter, true);
            return b;
        }

        // Элемент фиксированного размера для горизонтального ряда
        public static RectTransform Fixed(Transform parent, string name, float w, float h)
        {
            RectTransform rt = Rect(parent, name);
            rt.sizeDelta = new Vector2(w, h);
            LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;
            return rt;
        }

        public static HorizontalLayoutGroup Row(RectTransform rt, float spacing)
        {
            HorizontalLayoutGroup h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = spacing;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        public static void Clear(Transform t)
        {
            foreach (Transform child in t)
            {
                // Неактивные дети сразу выпадают из раскладки, а Destroy сработает в конце кадра
                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
            }
        }

        // Круглый значок с числом (мана, атака, здоровье)
        public static Text Badge(Transform parent, Vector2 anchor, Vector2 offset, float size, Color color, string text, int fontSize)
        {
            RectTransform o = Rect(parent, "Badge");
            o.anchorMin = anchor;
            o.anchorMax = anchor;
            o.pivot = new Vector2(0.5f, 0.5f);
            o.anchoredPosition = offset;
            o.sizeDelta = new Vector2(size, size);
            Image border = o.gameObject.AddComponent<Image>();
            border.sprite = Circle;
            border.color = new Color(0, 0, 0, 0.85f);
            border.raycastTarget = false;
            Image fill = Panel(o, "Fill", color);
            fill.sprite = Circle;
            Inset(fill.rectTransform, 3);
            return Label(o, text, fontSize, Color.white, TextAnchor.MiddleCenter, true);
        }

        // Картинка карты или заглушка с первой буквой имени
        public static void ArtBox(Transform parent, string artId, string title, Color tint,
            float x0, float y0, float x1, float y1)
        {
            Texture2D tex = Art(artId);
            if (tex != null)
            {
                RectTransform rt = Rect(parent, "Art");
                Anchor(rt, x0, y0, x1, y1);
                RawImage ri = rt.gameObject.AddComponent<RawImage>();
                ri.texture = tex;
                ri.raycastTarget = false;
                return;
            }
            Image bg = Panel(parent, "Art", Color.Lerp(tint, Color.black, 0.45f));
            Anchor(bg.rectTransform, x0, y0, x1, y1);
            string letter = string.IsNullOrEmpty(title) ? "?" : title.Substring(0, 1);
            Label(bg.transform, letter, 46, new Color(1, 1, 1, 0.3f), TextAnchor.MiddleCenter, true);
        }
    }
}
