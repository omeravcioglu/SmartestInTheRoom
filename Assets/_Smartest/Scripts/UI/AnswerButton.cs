using System;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// One answer: a big slab (RED, GREEN, PULL…) or a number tile. The slab carries only the
    /// word you're choosing — the rule lives in THE DEAL. Your pick gets a stamp (slab) or
    /// turns ink with a gold check (tile); the others fade to 35%; nothing is clickable after.
    /// </summary>
    public class AnswerButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image glyph;
        [SerializeField] private TMP_Text keyCap;
        [SerializeField] private TMP_Text stamp;
        [SerializeField] private Image badge;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private HardShadow shadow;
        [SerializeField] private bool tile;
        [SerializeField] private float wordSize = 160f;

        public int Value { get; private set; }
        public event Action<AnswerButton> Clicked;

        private Color _fill = Color.white;
        private Color _ink = Color.black;

        private const float GlyphSize = 58f;
        private const float GlyphGap = 26f;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(this));
            if (stamp != null) Ink.BoxOf(stamp).gameObject.SetActive(false);
            if (badge != null) badge.gameObject.SetActive(false);
        }

        /// <summary>A keyboard shortcut: behaves exactly like a click, and only when a click would.</summary>
        public void Press()
        {
            if (button != null && button.IsInteractable() && isActiveAndEnabled) Clicked?.Invoke(this);
        }

        public void Setup(int value, string text, AnswerLook look, string key = null)
        {
            Value = value;
            _fill = look.Fill;
            _ink = look.Ink;
            if (label != null)
            {
                label.text = text;
                label.color = _ink;
            }
            if (glyph != null)
            {
                glyph.gameObject.SetActive(look.Glyph != null && !tile);
                if (look.Glyph != null)
                {
                    glyph.sprite = look.Glyph;
                    glyph.color = look.Ink;
                }
            }
            if (keyCap != null)
            {
                Ink.BoxOf(keyCap).gameObject.SetActive(!string.IsNullOrEmpty(key));
                keyCap.text = key ?? string.Empty;
            }
            if (stamp != null)
            {
                // The stamp is drawn in whatever reads on this slab.
                var box = Ink.BoxOf(stamp).GetComponent<Image>();
                bool onPaper = Palette.Same(_fill, Palette.PaperHi);
                if (onPaper)
                {
                    box.sprite = InkSprites.Box(3f);
                    box.color = Palette.Gold;
                    stamp.color = Palette.Ink;
                }
                else
                {
                    box.sprite = InkSprites.Frame(3f);
                    box.color = _ink;
                    stamp.color = _ink;
                }
            }
            ResetState();
            LayoutRow();
        }

        public void ResetState()
        {
            if (button != null) button.interactable = true;
            if (group != null) group.alpha = 1f;
            if (stamp != null) Ink.BoxOf(stamp).gameObject.SetActive(false);
            if (badge != null) badge.gameObject.SetActive(false);
            if (background != null) background.color = _fill;
            if (label != null) label.color = _ink;
            if (shadow != null) shadow.Set(tile ? 8f : 10f, Palette.Ink);
            transform.localScale = Vector3.one;
        }

        public void SetInteractable(bool on)
        {
            if (button != null) button.interactable = on;
        }

        /// <summary>Before the answering window opens: visible, readable, not yet pressable.</summary>
        public void SetIntro(bool on)
        {
            if (group != null) group.alpha = on ? 0.45f : 1f;
            if (on) SetInteractable(false);
        }

        public void SetLocked(bool chosen)
        {
            if (button != null) button.interactable = false;
            if (group != null) group.alpha = chosen ? 1f : 0.35f;
            if (!chosen) return;

            if (tile)
            {
                if (background != null) background.color = Palette.Ink;
                if (label != null) label.color = Palette.PaperHi;
                if (shadow != null) shadow.Set(8f, Palette.Gold);
                if (badge != null) badge.gameObject.SetActive(true);
            }
            else if (stamp != null)
            {
                Ink.BoxOf(stamp).gameObject.SetActive(true);
            }
            if (isActiveAndEnabled) StartCoroutine(Tween.ScaleUniform(transform, 1.03f, 0.18f, Ease.OutBack));
        }

        /// <summary>Glyph and word sit centred together; a long word shrinks rather than spilling.</summary>
        private void LayoutRow()
        {
            if (label == null) return;
            var size = ((RectTransform)transform).rect.size;
            if (size.x < 10f) size = ((RectTransform)transform).sizeDelta;

            label.enableAutoSizing = false;
            label.fontSize = wordSize;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            if (tile)
            {
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.Fill();
                return;
            }

            bool hasGlyph = glyph != null && glyph.gameObject.activeSelf;
            float glyphSpace = hasGlyph ? GlyphSize + GlyphGap : 0f;
            float room = size.x - 80f - glyphSpace;
            float w = label.GetPreferredValues(label.text).x;
            if (w > room && w > 0f)
            {
                label.fontSize = wordSize * room / w;
                w = room;
            }
            float x = (size.x - glyphSpace - w) * 0.5f;
            if (hasGlyph) glyph.rectTransform.At(x, (size.y - GlyphSize) * 0.5f, GlyphSize, GlyphSize);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.At(x + glyphSpace, 0f, w + 6f, size.y);
        }

        // ------------------------------------------------------------------
        // Construction (SceneBuilder)
        // ------------------------------------------------------------------

        /// <summary>An 888 × 210 slab: word, optional glyph, keycap bottom-left, LOCKED IN stamp bottom-right.</summary>
        public static AnswerButton CreateSlab(Transform parent, string name, Vector2 size)
        {
            var bg = Ink.Box(parent, name, Palette.PaperHi, 4f, 6f, raycast: true);
            bg.rectTransform.sizeDelta = size;
            var sh = Ink.Shadow(bg, 10f);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            bg.gameObject.AddComponent<SlabPress>().DimWhenDisabled = false;
            var cg = bg.gameObject.AddComponent<CanvasGroup>();

            var g = Ink.Icon(bg.transform, "Glyph", InkSprites.Diamond, Palette.OnRed);
            var word = Ink.Text(bg.transform, "Word", "RED", TypeRole.Display, 160f, Palette.OnRed,
                TextAlignmentOptions.MidlineLeft, caps: true, lineHeight: 0.8f);
            word.textWrappingMode = TextWrappingModes.NoWrap;

            var key = Ink.Chip(bg.transform, "Key", "1", TypeRole.Sticker, 20f, Palette.Ink, Palette.PaperHi, 0f,
                new RectOffset(10, 10, 3, 2), caps: true);
            Ink.BoxOf(key).Pin(18f, size.y - 16f, new Vector2(0f, 0f));

            var st = Ink.Stamp(bg.transform, "Stamp", "LOCKED IN", 22f, -6f, 3f, Palette.OnRed);
            Ink.BoxOf(st).Pin(size.x - 22f, size.y - 18f, new Vector2(1f, 0f));

            var ab = bg.gameObject.AddComponent<AnswerButton>();
            ab.button = btn;
            ab.background = bg;
            ab.label = word;
            ab.glyph = g;
            ab.keyCap = key;
            ab.stamp = st;
            ab.group = cg;
            ab.shadow = sh;
            ab.tile = false;
            ab.wordSize = 160f;
            return ab;
        }

        /// <summary>A 160 × 150 number tile with the gold check badge for your pick.</summary>
        public static AnswerButton CreateTile(Transform parent, string name, Vector2 size)
        {
            var bg = Ink.Box(parent, name, Palette.PaperHi, 4f, 6f, raycast: true);
            bg.rectTransform.sizeDelta = size;
            var sh = Ink.Shadow(bg, 8f);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            bg.gameObject.AddComponent<SlabPress>().DimWhenDisabled = false;
            var cg = bg.gameObject.AddComponent<CanvasGroup>();

            var word = Ink.Text(bg.transform, "Number", "1", TypeRole.Display, 110f, Palette.Ink,
                TextAlignmentOptions.Center, lineHeight: 1f);
            word.rectTransform.Fill();
            word.textWrappingMode = TextWrappingModes.NoWrap;

            var badgeDisc = Ink.Disc(bg.transform, "Badge", 46f, 3f, Palette.Gold);
            badgeDisc.rectTransform.At(size.x + 16f - 46f, -16f, 46f, 46f);
            var check = Ink.Icon(badgeDisc.transform, "Check", InkSprites.Check, Palette.Ink);
            check.rectTransform.At(12f, 12f, 22f, 22f);

            var ab = bg.gameObject.AddComponent<AnswerButton>();
            ab.button = btn;
            ab.background = bg;
            ab.label = word;
            ab.badge = badgeDisc;
            ab.group = cg;
            ab.shadow = sh;
            ab.tile = true;
            ab.wordSize = 110f;
            return ab;
        }
    }
}
