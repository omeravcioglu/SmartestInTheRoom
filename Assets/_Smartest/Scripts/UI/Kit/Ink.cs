using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// Builders for the Tabloid pieces — boxes, stickers, stamps, keycaps, tokens, headlines —
    /// shared by SceneBuilder (the static screens) and the views that build per-player pieces
    /// at runtime. Coordinates follow the design sheets: left/top in pixels, y running down,
    /// inside a parent whose origin is its top-left corner.
    /// </summary>
    public static class Ink
    {
        // ------------------------------------------------------------------
        // Nodes and placement
        // ------------------------------------------------------------------

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>
        /// CSS-style absolute placement. The pivot sits in the middle so a rotation turns the
        /// box about its centre, the way a CSS transform does.
        /// </summary>
        public static RectTransform At(this RectTransform rt, float left, float top, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(left + width * 0.5f, -(top + height * 0.5f));
            return rt;
        }

        public static T At<T>(this T c, float left, float top, float width, float height) where T : Component
        {
            ((RectTransform)c.transform).At(left, top, width, height);
            return c;
        }

        /// <summary>
        /// Pin a point of the box to (x, y) — for things that size themselves (stickers, chips):
        /// pivot (0, 0.5) grows to the right of x, (1, 0.5) to the left, (0.5, 0.5) both ways.
        /// </summary>
        public static RectTransform Pin(this RectTransform rt, float x, float y, Vector2 pivot)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = pivot;
            rt.anchoredPosition = new Vector2(x, -y);
            return rt;
        }

        public static T Pin<T>(this T c, float x, float y, Vector2 pivot) where T : Component
        {
            ((RectTransform)c.transform).Pin(x, y, pivot);
            return c;
        }

        /// <summary>Stretch over the parent, inset by the given margins.</summary>
        public static RectTransform Fill(this RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static T Fill<T>(this T c, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f) where T : Component
        {
            ((RectTransform)c.transform).Fill(left, top, right, bottom);
            return c;
        }

        /// <summary>Rotate like CSS: positive degrees turn clockwise.</summary>
        public static T Tilt<T>(this T c, float cssDegrees) where T : Component
        {
            c.transform.localEulerAngles = new Vector3(0f, 0f, -cssDegrees);
            return c;
        }

        /// <summary>The top-left-origin position of a point inside the parent, for code that lays out rows.</summary>
        public static Vector2 Css(float left, float top) => new Vector2(left, -top);

        /// <summary>A row that lays its children out left to right and hugs them.</summary>
        public static RectTransform Row(Transform parent, string name, float spacing,
            TextAnchor align = TextAnchor.MiddleLeft, bool hug = true)
        {
            var rt = Node(parent, name);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            if (hug) Hug(rt.gameObject);
            return rt;
        }

        /// <summary>A column that stacks its children top to bottom and hugs them.</summary>
        public static RectTransform Column(Transform parent, string name, float spacing,
            TextAnchor align = TextAnchor.UpperLeft, bool hug = true)
        {
            var rt = Node(parent, name);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            if (hug) Hug(rt.gameObject);
            return rt;
        }

        private static void Hug(GameObject go)
        {
            var f = go.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>
        /// A chip that goes inside a row or column lets the layout size it: its own size fitter
        /// would fight the layout group, so it goes.
        /// </summary>
        public static void Unhug(Component c)
        {
            var f = c != null ? c.GetComponent<ContentSizeFitter>() : null;
            if (f == null) return;
            if (Application.isPlaying) Object.Destroy(f);
            else Object.DestroyImmediate(f);
        }

        /// <summary>Fixed layout size for something inside a row or column.</summary>
        public static LayoutElement Size(Component c, float width, float height)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f) { le.preferredWidth = width; le.minWidth = width; }
            if (height >= 0f) { le.preferredHeight = height; le.minHeight = height; }
            return le;
        }

        // ------------------------------------------------------------------
        // Surfaces
        // ------------------------------------------------------------------

        /// <summary>A box with an ink border baked into its sprite. Its colour is the fill.</summary>
        public static Image Box(Transform parent, string name, Color fill, float border = 3f, float radius = 0f,
            bool raycast = false)
        {
            var img = Node(parent, name).gameObject.AddComponent<Image>();
            img.sprite = InkSprites.Box(border, radius);
            img.type = Image.Type.Sliced;
            img.color = fill;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>A flat colour with no border (rules, bars, bands).</summary>
        public static Image Plain(Transform parent, string name, Color color, bool raycast = false)
            => Box(parent, name, color, 0f, 0f, raycast);

        /// <summary>A dashed border (open seats, the join-code frame, "nobody picked it").</summary>
        public static Image Dashed(Transform parent, string name, Color fill, float border = 3f)
        {
            var img = Node(parent, name).gameObject.AddComponent<Image>();
            img.sprite = InkSprites.Dashed(border);
            img.type = Image.Type.Tiled;
            img.color = fill;
            img.raycastTarget = false;
            return img;
        }

        public static Image Icon(Transform parent, string name, Sprite sprite, Color color)
        {
            var img = Node(parent, name).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A round token or dot. The ink ring is baked in; the colour fills the inside.</summary>
        public static Image Disc(Transform parent, string name, float diameter, float border, Color fill,
            bool halftone = false)
        {
            var img = Node(parent, name).gameObject.AddComponent<Image>();
            img.sprite = InkSprites.Disc(diameter, border, halftone);
            img.type = Image.Type.Simple;
            img.color = fill;
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            return img;
        }

        /// <summary>A player token: halftone disc with their two-letter monogram.</summary>
        public static TextMeshProUGUI Token(Transform parent, string name, float diameter, float border, Color fill,
            string monogram, float fontSize, bool halftone = true)
        {
            var disc = Disc(parent, name, diameter, border, fill, halftone);
            var t = Text(disc.transform, "Mono", monogram, TypeRole.Display, fontSize, Palette.Ink,
                TextAlignmentOptions.Center, caps: true);
            t.rectTransform.Fill();
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        public static HardShadow Shadow(Graphic g, float distance, Color? color = null)
            => HardShadow.On(g, distance, color);

        // ------------------------------------------------------------------
        // Type
        // ------------------------------------------------------------------

        public static TextMeshProUGUI Text(Transform parent, string name, string text, TypeRole role, float size,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool caps = false,
            float tracking = 0f, float lineHeight = 0f)
        {
            var t = Node(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            Typo.Apply(t, role, size, caps, tracking, lineHeight);
            t.text = text ?? string.Empty;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>Shrink to fit instead of overflowing — for text whose length the design doesn't control.</summary>
        public static T Fit<T>(this T t, float minSize) where T : TMP_Text
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = Mathf.Min(minSize, t.fontSize);
            return t;
        }

        public static T OneLine<T>(this T t) where T : TMP_Text
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        /// <summary>A headline: display face, caps, tight leading, a gold drop behind it.</summary>
        public static TextMeshProUGUI Headline(Transform parent, string name, string text, float size, float drop,
            float minSize = 0f)
        {
            var t = Text(parent, name, text, TypeRole.Display, size, Palette.Ink, TextAlignmentOptions.TopLeft,
                caps: true, lineHeight: 0.86f);
            if (minSize > 0f) t.Fit(minSize);
            if (drop > 0f) TextDrop.Add(t, drop);
            return t;
        }

        /// <summary>Small tracked caps: THE DEAL, THE RACE TO 100, JOIN CODE.</summary>
        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color? color = null,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, float tracking = 0.14f)
        {
            var t = Text(parent, name, text, TypeRole.Label, size, color ?? Palette.Ink, align, caps: true, tracking: tracking);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        // ------------------------------------------------------------------
        // Pieces that size themselves to their text
        // ------------------------------------------------------------------

        /// <summary>
        /// A box that hugs its text: kickers, stickers, stamps, keycaps, chips. The returned
        /// text's parent is the box; move and rotate the box.
        /// </summary>
        public static TextMeshProUGUI Chip(Transform parent, string name, string text, TypeRole role, float size,
            Color fill, Color ink, float border, RectOffset padding, bool caps = true, float tracking = 0f,
            float lineHeight = 1f)
        {
            var box = Box(parent, name, fill, border);
            var h = box.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = padding;
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            var fit = box.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var t = Text(box.transform, "Text", text, role, size, ink, TextAlignmentOptions.Center, caps, tracking);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = Mathf.Round(size * lineHeight);
            le.minHeight = le.preferredHeight;
            return t;
        }

        /// <summary>Gold-on-ink kicker above a headline, tilted −2°.</summary>
        public static TextMeshProUGUI Kicker(Transform parent, string name, string text, float size = 34f)
        {
            var t = Chip(parent, name, text, TypeRole.Display, size, Palette.Ink, Palette.Gold, 0f,
                new RectOffset(16, 16, 6, 4), caps: true);
            t.transform.parent.GetComponent<Image>().Tilt(-2f);
            return t;
        }

        /// <summary>A sticker: bordered, tilted, loud. "+15", "THAT'S YOU!", "OPENS IN 2 S".</summary>
        public static TextMeshProUGUI Sticker(Transform parent, string name, string text, float size, Color fill,
            Color ink, float tilt, float border = 3f)
        {
            var t = Chip(parent, name, text, TypeRole.Sticker, size, fill, ink, border,
                new RectOffset(10, 10, 4, 2), caps: true);
            t.transform.parent.GetComponent<Image>().Tilt(tilt);
            return t;
        }

        /// <summary>A rubber stamp: outline only, tracked caps, tilted. "LOCKED", "OUT".</summary>
        public static TextMeshProUGUI Stamp(Transform parent, string name, string text, float size, float tilt,
            float border = 3f, Color? ink = null, Color? fill = null)
        {
            var c = ink ?? Palette.Ink;
            var t = Chip(parent, name, text, TypeRole.Sticker, size, fill ?? c, c, border,
                new RectOffset(10, 10, 3, 2), caps: true, tracking: 0.1f);
            var img = t.transform.parent.GetComponent<Image>();
            if (fill == null)
            {
                // Outline only: the frame sprite is just the ring, coloured by the image.
                img.sprite = InkSprites.Frame(border);
                img.color = c;
            }
            img.Tilt(tilt);
            return t;
        }

        /// <summary>A keycap: ink cap, paper letters, a darker lip under it.</summary>
        public static TextMeshProUGUI Key(Transform parent, string name, string text, float size = 20f)
        {
            var t = Chip(parent, name, text, TypeRole.Sticker, size, Palette.Ink, Palette.PaperHi, 0f,
                new RectOffset(12, 12, 6, 4), caps: true, tracking: 0.08f);
            var cap = t.transform.parent.GetComponent<Image>();
            var lip = HardShadow.On(cap, 0f, Palette.Ink2);
            lip.Offset = new Vector2(0f, -4f);
            var le = cap.gameObject.AddComponent<LayoutElement>();
            le.minWidth = Mathf.Round(size * 2.2f);
            return t;
        }

        /// <summary>An outline with nothing inside (pips, open toggles). Its colour is the ring.</summary>
        public static Image Frame(Transform parent, string name, Color ring, float border = 3f)
        {
            var img = Node(parent, name).gameObject.AddComponent<Image>();
            img.sprite = InkSprites.Frame(border);
            img.type = Image.Type.Sliced;
            img.color = ring;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>The sticker's box, for moving or hiding it.</summary>
        public static RectTransform BoxOf(TMP_Text chipText) => (RectTransform)chipText.transform.parent;

        // ------------------------------------------------------------------
        // Buttons
        // ------------------------------------------------------------------

        /// <summary>
        /// A slab button: bordered box, hard shadow, big display word. Hover lifts it 3 px and
        /// the shadow grows to match, like the page is being pressed up at you.
        /// </summary>
        public static Button Slab(Transform parent, string name, string word, Color fill, Color ink, float wordSize,
            float border = 4f, float shadow = 10f, float radius = 0f)
        {
            var img = Box(parent, name, fill, border, radius, raycast: true);
            Shadow(img, shadow);
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            img.gameObject.AddComponent<SlabPress>();

            var t = Text(img.transform, "Label", word, TypeRole.Display, wordSize, ink, TextAlignmentOptions.Center,
                caps: true);
            t.rectTransform.Fill(12f, 4f, 12f, 4f);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.Fit(wordSize * 0.4f);
            return btn;
        }

        /// <summary>Underlined text button (QUIT).</summary>
        public static Button TextButton(Transform parent, string name, string word, float size = 22f)
        {
            var t = Text(parent, name, "<u>" + word + "</u>", TypeRole.Sticker, size, Palette.Ink,
                TextAlignmentOptions.Left, caps: true, tracking: 0.08f);
            t.raycastTarget = true;
            var btn = t.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = t;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.pressedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            btn.colors = colors;
            return btn;
        }
    }
}
