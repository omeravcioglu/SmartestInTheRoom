using System;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The only way minigames are allowed to draw. Everything here uses the same rounded
    /// sprite, the same Palette colours and the same type sizes as the rest of the game,
    /// so a new minigame cannot accidentally look like it came from a different project.
    /// </summary>
    public static class UiKit
    {
        /// <summary>The 9-slice panel sprite. MinigameStage hands it over on Awake.</summary>
        public static Sprite RoundedSprite;

        private static Sprite s_circle;
        private static TMP_FontAsset s_font;

        /// <summary>Optional shared font, so runtime text matches the scene's text.</summary>
        public static TMP_FontAsset Font
        {
            get => s_font;
            set => s_font = value;
        }

        public static Sprite Circle
        {
            get
            {
                if (s_circle != null) return s_circle;
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                float r = size * 0.5f - 1f;
                var c = new Vector2(size * 0.5f, size * 0.5f);
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = Mathf.Clamp01(r - d); // 1px soft edge
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels(px);
                tex.Apply();
                s_circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect);
                s_circle.name = "SmartestCircle";
                return s_circle;
            }
        }

        // ------------------------------------------------------------------

        public static RectTransform Node(Transform parent, string name, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        /// <summary>A rounded filled box — the building block for every panel-like thing.</summary>
        public static Image Box(Transform parent, string name, Vector2 size, Vector2 pos, Color color,
            bool raycast = false)
        {
            var rt = Node(parent, name, size, pos);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundedSprite;
            if (RoundedSprite != null) img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Dot(Transform parent, string name, float diameter, Vector2 pos, Color color)
        {
            var rt = Node(parent, name, new Vector2(diameter, diameter), pos);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Circle;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TMP_Text Label(Transform parent, string name, string text, float fontSize, Color color,
            Vector2 size, Vector2 pos, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Node(parent, name, size, pos);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (s_font != null) t.font = s_font;
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = true;
            return t;
        }

        /// <summary>A clickable square with a centred label — grid cells, number tiles, answers.</summary>
        public static Image Cell(Transform parent, string name, Vector2 size, Vector2 pos, Color color,
            Action onClick, out TMP_Text label, string text = "", float fontSize = 30f)
        {
            var img = Box(parent, name, size, pos, color, raycast: true);
            label = Label(img.transform, name + "Label", text, fontSize, Palette.Text,
                new Vector2(size.x, size.y), Vector2.zero);
            if (onClick != null)
            {
                var btn = img.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => onClick());
            }
            return img;
        }

        /// <summary>Horizontal track with a fill that is driven by SetFill.</summary>
        public static Image Track(Transform parent, string name, Vector2 size, Vector2 pos,
            Color trackColor, Color fillColor, out Image fill)
        {
            var track = Box(parent, name, size, pos, trackColor);
            fill = Box(track.transform, name + "Fill", size, Vector2.zero, fillColor);
            var rt = (RectTransform)fill.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(size.x, 0f);
            rt.anchoredPosition = Vector2.zero;
            return track;
        }

        public static void SetFill(Image fill, float t, float fullWidth)
        {
            if (fill == null) return;
            var rt = (RectTransform)fill.transform;
            rt.sizeDelta = new Vector2(Mathf.Clamp01(t) * fullWidth, rt.sizeDelta.y);
        }

        /// <summary>A square grid of clickable cells, centred in the parent. Index = row * n + column.</summary>
        public static Image[] Grid(Transform parent, int n, float cellSize, float spacing, Color color, Action<int> onClick)
        {
            var cells = new Image[n * n];
            float total = n * cellSize + (n - 1) * spacing;
            float start = -total * 0.5f + cellSize * 0.5f;
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++)
                {
                    int index = r * n + c;
                    var pos = new Vector2(start + c * (cellSize + spacing), -(start + r * (cellSize + spacing)));
                    int captured = index;
                    cells[index] = Cell(parent, "Cell" + index, new Vector2(cellSize, cellSize), pos, color,
                        onClick != null ? () => onClick(captured) : (Action)null, out _, string.Empty, 26f);
                }
            }
            return cells;
        }

        /// <summary>Biggest cell that lets an n x n grid fit in the space available.</summary>
        public static float CellSize(int n, float available, float spacing, float max = 120f)
        {
            if (n <= 0) return max;
            float size = (available - (n - 1) * spacing) / n;
            return Mathf.Clamp(size, 24f, max);
        }

        /// <summary>A text field that takes focus immediately — the one game that needs typing.</summary>
        public static TMP_InputField Field(Transform parent, Vector2 size, Vector2 pos, float fontSize = 40f)
        {
            var back = Box(parent, "Field", size, pos, Palette.Neutral, raycast: true);

            var viewport = Node(back.transform, "TextArea", size, Vector2.zero);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(16f, 8f);
            viewport.offsetMax = new Vector2(-16f, -8f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var textRt = Node(viewport, "Text", size, Vector2.zero);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (s_font != null) text.font = s_font;
            text.fontSize = fontSize;
            text.color = Palette.Text;
            text.alignment = TextAlignmentOptions.Center;
            text.richText = false;

            var field = back.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = viewport;
            field.textComponent = text;
            field.fontAsset = s_font;
            field.pointSize = fontSize;
            field.caretWidth = 3;
            field.customCaretColor = true;
            field.caretColor = Palette.Accent;
            field.selectionColor = new Color(Palette.Accent.r, Palette.Accent.g, Palette.Accent.b, 0.35f);
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.richText = false;
            field.restoreOriginalTextOnEscape = false;
            field.onFocusSelectAll = false;
            field.text = string.Empty;
            return field;
        }

        /// <summary>Screen point to a point in this rect's local space. Canvas is Screen Space - Overlay.</summary>
        public static bool LocalPoint(RectTransform area, Vector2 screenPoint, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPoint, null, out local);
        }

        public static void Clear(RectTransform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(root.GetChild(i).gameObject);
        }
    }
}
