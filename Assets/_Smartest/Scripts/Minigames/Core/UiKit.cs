using System;
using Smartest.Core;
using Smartest.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The only way minigames are allowed to draw. Everything here comes out in the Tabloid
    /// style — paper boxes with ink borders, gold dots with ink rings, display-face game words
    /// — so a new minigame cannot accidentally look like it came from a different project.
    ///
    /// Games keep speaking in Palette roles (Neutral, Accent, Red, Green). A small watcher on
    /// each piece turns those into the print look: a red cell gets a ✕, a green one a ✓, a
    /// gold word gets a chip so it stays readable on paper.
    /// </summary>
    public static class UiKit
    {
        /// <summary>Kept for older callers; the kit draws with InkSprites now.</summary>
        public static Sprite RoundedSprite;

        /// <summary>Optional override for runtime text. Null = the Tabloid faces.</summary>
        public static TMP_FontAsset Font { get; set; }

        public static Sprite Circle => InkSprites.Dot;

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

        /// <summary>
        /// A paper box with an ink border — the building block for every panel-like thing.
        /// Big frames (lights, clocks, arenas) get a heavier border and a hard shadow.
        /// </summary>
        public static Image Box(Transform parent, string name, Vector2 size, Vector2 pos, Color color,
            bool raycast = false)
        {
            var rt = Node(parent, name, size, pos);
            var img = rt.gameObject.AddComponent<Image>();
            bool big = Mathf.Max(size.x, size.y) >= 300f;
            bool thin = Mathf.Min(size.x, size.y) < 20f;
            img.sprite = InkSprites.Box(thin ? 2f : big ? 4f : 3f);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = raycast;
            if (big) HardShadow.On(img, 8f);
            return img;
        }

        /// <summary>
        /// A flat rectangle with no border, for shapes drawn in pieces that have to join
        /// without seams (a path made of segments, a band inside a track).
        /// </summary>
        public static Image Fill(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
        {
            var rt = Node(parent, name, size, pos);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = InkSprites.Box(0f);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A straight line from a to b: a thin flat bar, rotated. Move it with <see cref="SetLine"/>.</summary>
        public static RectTransform Line(Transform parent, string name, Vector2 a, Vector2 b, float thickness, Color color)
        {
            var rt = (RectTransform)Fill(parent, name, new Vector2(1f, thickness), Vector2.zero, color).transform;
            SetLine(rt, a, b);
            return rt;
        }

        public static void SetLine(RectTransform line, Vector2 a, Vector2 b)
        {
            if (line == null) return;
            var d = b - a;
            line.anchoredPosition = (a + b) * 0.5f;
            line.sizeDelta = new Vector2(d.magnitude, line.sizeDelta.y);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        /// <summary>A round piece with a 4 px ink ring (3 px on small dots).</summary>
        public static Image Dot(Transform parent, string name, float diameter, Vector2 pos, Color color)
        {
            var rt = Node(parent, name, new Vector2(diameter, diameter), pos);
            var img = rt.gameObject.AddComponent<Image>();
            float ring = diameter >= 40f ? 4f : diameter >= 20f ? 3f : 2f;
            img.sprite = InkSprites.Disc(Mathf.Round(diameter), ring);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// One of the illustrated pieces (a cup, a fish, a mole: InkSprites.Art). A tinted piece
        /// takes its colour from <paramref name="tint"/> the way a box does; the others carry
        /// their own colours and are left white.
        /// </summary>
        public static Image Art(Transform parent, string name, string piece, Vector2 size, Vector2 pos, Color? tint = null)
        {
            var rt = Node(parent, name, size, pos);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            Dress(img, piece);
            img.color = tint ?? Color.white;
            return img;
        }

        /// <summary>Draws an existing box or cell as an illustrated piece; its clicks and colour stay as they were.</summary>
        public static void Dress(Image img, string piece)
        {
            if (img == null) return;
            img.sprite = InkSprites.Art(piece);
            img.type = img.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }

        /// <summary>A chequered finish line: the flag repeated down its height (40 px a flag).</summary>
        public static Image FinishLine(Transform parent, string name, float height, Vector2 pos)
        {
            var rt = Node(parent, name, new Vector2(22f, height), pos);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = InkSprites.Checker;
            img.type = Image.Type.Tiled;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>The kit's small marks, for telling things apart by shape as well as colour.</summary>
        public enum Mark { Star, Diamond, Dot, Square, Cross, Check }

        /// <summary>A mark, coloured like a box: ink for a solid one, a light colour for one with an ink edge.</summary>
        public static Image Marker(Transform parent, string name, Mark mark, float size, Vector2 pos, Color color)
        {
            var rt = Node(parent, name, new Vector2(size, size), pos);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            switch (mark)
            {
                case Mark.Star: img.sprite = InkSprites.Star; break;
                case Mark.Diamond: img.sprite = InkSprites.Diamond; break;
                case Mark.Dot: img.sprite = InkSprites.Dot; break;
                case Mark.Cross: img.sprite = InkSprites.Cross; break;
                case Mark.Check: img.sprite = InkSprites.Check; break;
                default:
                    img.sprite = InkSprites.Box(3f);
                    img.type = Image.Type.Sliced;
                    rt.sizeDelta *= 0.8f; // a square looks bigger than a star of the same size
                    break;
            }
            img.color = color;
            return img;
        }

        /// <summary>
        /// Puts the pivot on a point of the piece (in its own pixels, y down from the top left), so
        /// that point is what anchoredPosition places and what it turns about.
        /// </summary>
        public static void PivotOn(Image img, string piece, Vector2 point)
        {
            var size = InkSprites.ArtSize(piece);
            if (img == null || size.x <= 0f) return;
            img.rectTransform.pivot = new Vector2(point.x / size.x, 1f - point.y / size.y);
        }

        /// <summary>
        /// Text. Words the game shouts ("MEMORISE", "HIT") are its "Hint" label and come out in
        /// the display face, on a chip when they're gold, red or green. Big numbers use the
        /// display face too; small instructions become tracked caps.
        /// </summary>
        public static TMP_Text Label(Transform parent, string name, string text, float fontSize, Color color,
            Vector2 size, Vector2 pos, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Node(parent, name, size, pos);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            bool hint = name == "Hint";
            if (hint) Typo.Apply(t, TypeRole.Display, Mathf.Max(40f, fontSize * 1.6f), caps: true, lineHeight: 1f);
            else if (fontSize >= 44f) Typo.Apply(t, TypeRole.Display, fontSize * 1.15f, lineHeight: 1f);
            else Typo.Apply(t, TypeRole.Label, Mathf.Clamp(fontSize * 0.66f, 14f, 20f), caps: true, tracking: 0.12f);
            if (Font != null) t.font = Font;
            t.text = text;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = Mathf.Min(12f, t.fontSize);
            KitWatch.ForText(t, hint);
            return t;
        }

        /// <summary>Typed words read best in the mono face (Type It's target word).</summary>
        public static void UseMono(TMP_Text t)
        {
            if (t == null) return;
            float size = t.fontSizeMax > 0f ? t.fontSizeMax : t.fontSize;
            Typo.Apply(t, TypeRole.Mono, size, tracking: 0.06f);
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
        }

        /// <summary>A clickable box with a centred label — grid cells, number tiles, answers.</summary>
        public static Image Cell(Transform parent, string name, Vector2 size, Vector2 pos, Color color,
            Action onClick, out TMP_Text label, string text = "", float fontSize = 30f)
        {
            var img = Box(parent, name, size, pos, color, raycast: true);
            var lrt = Node(img.transform, name + "Label", size, Vector2.zero);
            var t = lrt.gameObject.AddComponent<TextMeshProUGUI>();
            Typo.Apply(t, TypeRole.Display, fontSize * 1.25f, lineHeight: 1f);
            if (Font != null) t.font = Font;
            t.text = text;
            t.color = Palette.Ink;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = 12f;
            label = t;
            if (onClick != null)
            {
                var btn = img.gameObject.AddComponent<Button>();
                // The level is built while hidden (not interactable), so the new Button has
                // already faded its graphic to the grey "disabled" tint. No transitions, and
                // put the graphic back to its own colour.
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = null;
                img.canvasRenderer.SetColor(Color.white);
                btn.onClick.AddListener(() => onClick());
            }
            KitWatch.ForCell(img, t);
            return img;
        }

        /// <summary>Horizontal track with a fill that is driven by SetFill.</summary>
        public static Image Track(Transform parent, string name, Vector2 size, Vector2 pos,
            Color trackColor, Color fillColor, out Image fill)
        {
            var rt = Node(parent, name, size, pos);
            var track = rt.gameObject.AddComponent<Image>();
            track.sprite = InkSprites.Box(4f);
            track.type = Image.Type.Sliced;
            track.color = trackColor;
            track.raycastTarget = false;
            HardShadow.On(track, 6f);

            var frt = Node(track.transform, name + "Fill", size, Vector2.zero);
            fill = frt.gameObject.AddComponent<Image>();
            fill.sprite = InkSprites.Box(0f);
            fill.type = Image.Type.Sliced;
            fill.color = fillColor;
            fill.raycastTarget = false;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.offsetMin = new Vector2(4f, 4f);
            frt.offsetMax = new Vector2(4f, -4f);
            frt.sizeDelta = new Vector2(Mathf.Max(0f, size.x - 8f), -8f);
            frt.anchoredPosition = new Vector2(4f, 0f);
            return track;
        }

        public static void SetFill(Image fill, float t, float fullWidth)
        {
            if (fill == null) return;
            var rt = (RectTransform)fill.transform;
            // The fill sits inside the track's 4 px border.
            rt.sizeDelta = new Vector2(Mathf.Clamp01(t) * Mathf.Max(0f, fullWidth - 8f), rt.sizeDelta.y);
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
        public static float CellSize(int n, float available, float spacing, float max = 130f)
        {
            if (n <= 0) return max;
            float size = (available - (n - 1) * spacing) / n;
            return Mathf.Clamp(size, 24f, max);
        }

        /// <summary>A text field that takes focus immediately — the one game that needs typing.</summary>
        public static TMP_InputField Field(Transform parent, Vector2 size, Vector2 pos, float fontSize = 40f)
        {
            var back = Box(parent, "Field", size, pos, Palette.Paper2, raycast: true);
            back.sprite = InkSprites.Box(4f);

            var viewport = Node(back.transform, "TextArea", size, Vector2.zero);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(16f, 6f);
            viewport.offsetMax = new Vector2(-16f, -6f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var textRt = Node(viewport, "Text", size, Vector2.zero);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            float typed = Mathf.Min(fontSize * 1.3f, size.y * 0.7f);
            Typo.Apply(text, TypeRole.Mono, typed, tracking: 0.06f);
            if (Font != null) text.font = Font;
            text.color = Palette.Ink;
            text.alignment = TextAlignmentOptions.Center;
            text.richText = false;

            var field = back.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = viewport;
            field.textComponent = text;
            field.fontAsset = text.font;
            field.pointSize = typed;
            field.caretWidth = 4;
            field.customCaretColor = true;
            field.caretColor = Palette.Gold;
            field.selectionColor = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.45f);
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
