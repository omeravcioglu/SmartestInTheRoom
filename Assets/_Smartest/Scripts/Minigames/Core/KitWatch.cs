using Smartest.Core;
using Smartest.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Translates the colours a minigame sets into the print look, so games can keep saying
    /// "this cell is Palette.Red now" and never learn about stickers:
    ///  - cells: red gets a ✕, green a ✓ (state is never colour alone), the mirror line
    ///    (AccentDim) gets the gold hatch;
    ///  - text: gold, red and green words stay readable on paper — game words ("Hint") go on
    ///    a chip, everything else takes the text-safe shade.
    /// </summary>
    [AddComponentMenu("")]
    public class KitWatch : MonoBehaviour
    {
        private enum Kind { Cell, Text, Hint }

        private Kind _kind;
        private Image _cell;
        private TMP_Text _cellLabel;
        private Image _glyph;
        private Image _hatch;

        private TMP_Text _text;
        private Image _chip;
        private string _lastString;

        private Color _seen = new Color(-1f, -1f, -1f, -1f);   // what the game set last
        private Color _applied = new Color(-1f, -1f, -1f, -1f); // what we turned it into

        public static void ForCell(Image cell, TMP_Text label)
        {
            var w = cell.gameObject.AddComponent<KitWatch>();
            w._kind = Kind.Cell;
            w._cell = cell;
            w._cellLabel = label;
            w.LateUpdate();
        }

        public static void ForText(TMP_Text text, bool hint)
        {
            var w = text.gameObject.AddComponent<KitWatch>();
            w._kind = hint ? Kind.Hint : Kind.Text;
            w._text = text;
            w.LateUpdate();
        }

        private void LateUpdate()
        {
            if (_kind == Kind.Cell) WatchCell();
            else WatchText();
        }

        // ------------------------------------------------------------------

        private void WatchCell()
        {
            if (_cell == null) return;
            var c = _cell.color;
            if (c == _seen) return;
            _seen = c;

            bool red = Palette.Same(c, Palette.Red);
            bool green = Palette.Same(c, Palette.Green);
            bool mirror = Palette.Same(c, Palette.AccentDim);

            if (red || green)
            {
                if (_glyph == null) _glyph = MakeGlyph();
                _glyph.gameObject.SetActive(true);
                _glyph.sprite = red ? InkSprites.Cross : InkSprites.Check;
                _glyph.color = red ? Palette.OnRed : Palette.Ink;
                PlaceGlyph();
            }
            else if (_glyph != null) _glyph.gameObject.SetActive(false);

            if (mirror)
            {
                if (_hatch == null) _hatch = MakeHatch();
                _hatch.gameObject.SetActive(true);
            }
            else if (_hatch != null) _hatch.gameObject.SetActive(false);

            if (_cellLabel != null)
            {
                bool dark = red || Palette.Same(c, Palette.Ink);
                _cellLabel.color = dark ? Palette.OnRed : Palette.Ink;
            }
        }

        private Image MakeGlyph()
        {
            var img = Ink.Icon(_cell.transform, "State", InkSprites.Check, Palette.Ink);
            return img;
        }

        /// <summary>Centred on an empty cell; a corner badge when the cell shows a number.</summary>
        private void PlaceGlyph()
        {
            var size = _cell.rectTransform.rect.size;
            if (size.x < 1f) size = _cell.rectTransform.sizeDelta;
            bool numbered = _cellLabel != null && !string.IsNullOrEmpty(_cellLabel.text);
            var rt = _glyph.rectTransform;
            rt.anchorMin = rt.anchorMax = numbered ? new Vector2(1f, 1f) : new Vector2(0.5f, 0.5f);
            rt.pivot = numbered ? new Vector2(1f, 1f) : new Vector2(0.5f, 0.5f);
            float s = numbered ? Mathf.Clamp(size.x * 0.3f, 14f, 28f) : Mathf.Clamp(size.x * 0.45f, 14f, 60f);
            rt.sizeDelta = new Vector2(s, s);
            rt.anchoredPosition = numbered ? new Vector2(-5f, -5f) : Vector2.zero;
        }

        private Image MakeHatch()
        {
            var img = Ink.Icon(_cell.transform, "Hatch", InkSprites.Hatch, Color.white);
            img.preserveAspect = false;
            img.type = Image.Type.Tiled;
            img.rectTransform.Fill(3f, 3f, 3f, 3f);
            img.transform.SetAsFirstSibling();
            return img;
        }

        // ------------------------------------------------------------------

        private void WatchText()
        {
            if (_text == null) return;
            var c = _text.color;
            bool changed = c != _applied && c != _seen;
            bool textChanged = _kind == Kind.Hint && _chip != null && _chip.gameObject.activeSelf && _text.text != _lastString;
            if (!changed && !textChanged) return;
            if (changed) _seen = c;
            _lastString = _text.text;

            bool gold = Palette.Same(_seen, Palette.Gold);
            bool red = Palette.Same(_seen, Palette.Red);
            bool green = Palette.Same(_seen, Palette.Green);

            if (_kind == Kind.Hint && (gold || red || green))
            {
                if (_chip == null) _chip = MakeChip();
                _chip.gameObject.SetActive(true);
                _chip.color = gold ? Palette.Ink : red ? Palette.Red : Palette.Green;
                _applied = gold ? Palette.Gold : red ? Palette.OnRed : Palette.Ink;
                FitChip();
            }
            else
            {
                if (_chip != null) _chip.gameObject.SetActive(false);
                _applied = gold ? Palette.Ink : red ? Palette.RedText : green ? Palette.GreenText : _seen;
            }
            if (_text.color != _applied) _text.color = _applied;
        }

        /// <summary>A sticker behind the word, sized to the word, tilted a touch.</summary>
        private Image MakeChip()
        {
            var go = new GameObject("Chip", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_text.transform.parent, false);
            rt.SetSiblingIndex(_text.transform.GetSiblingIndex());
            var img = go.AddComponent<Image>();
            img.sprite = InkSprites.Box(3f);
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            return img;
        }

        private void FitChip()
        {
            if (_chip == null) return;
            _text.ForceMeshUpdate();
            var bounds = _text.textBounds;
            var trt = _text.rectTransform;
            var rt = _chip.rectTransform;
            rt.anchorMin = trt.anchorMin;
            rt.anchorMax = trt.anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            float w = bounds.size.x + 32f, h = bounds.size.y + 12f;
            if (bounds.size.x <= 0f) { w = 0f; h = 0f; }
            rt.sizeDelta = new Vector2(w, h);
            // textBounds are in the text's local space, around its pivot.
            Vector2 centre = (Vector2)bounds.center;
            rt.anchoredPosition = trt.anchoredPosition + centre;
            rt.localEulerAngles = new Vector3(0f, 0f, 2f);
        }
    }
}
