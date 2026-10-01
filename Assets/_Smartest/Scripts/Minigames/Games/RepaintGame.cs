using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// A little picture of coloured cells shows for a moment, then the canvas is wiped. Pick a
    /// colour from the palette and paint it back: click or drag across the cells. Mistakes can
    /// be painted over; it's done the moment it matches. More colours and bigger canvases as
    /// the levels climb. Ranked by time.
    /// </summary>
    public class RepaintGame : MinigameView
    {
        private const float Spacing = 8f;
        private const float Limit = 16f;

        // No red paint: on kit cells red means wrong (it gets a cross), so red is kept for that.
        private static readonly Color[] Paints = { Palette.PaperHi, Palette.Gold, Palette.Blue, Palette.Ink };

        private Image[] _cells = new Image[0];
        private int[] _target = new int[0];
        private int[] _painted = new int[0];
        private Image[] _swatches = new Image[0];
        private Rect[] _swatchRects = new Rect[0];
        private Vector2 _gridCentre;
        private int _n;
        private int _colours;
        private float _cell;
        private float _showFor;
        private int _brush = 1;
        private bool _wiped;
        private TMP_Text _label;
        private int _demoBrush; // the colour the rule card's demo last took from the palette
        private int _demoMoves; // and how many clicks it has made

        protected override void Build()
        {
            var size = AreaSize;
            int marked;
            switch (Level)
            {
                case 1: _n = 3; _colours = 1; marked = 3; _showFor = 3.0f; break;
                case 2: _n = 3; _colours = 1; marked = 4; _showFor = 2.6f; break;
                case 3: _n = 4; _colours = 2; marked = 5; _showFor = 2.6f; break;
                case 4: _n = 4; _colours = 2; marked = 7; _showFor = 2.4f; break;
                default: _n = 5; _colours = 3; marked = Mathf.Min(12, 8 + (Level - 5)); _showFor = Mathf.Max(1.8f, 2.4f - (Level - 5) * 0.1f); break;
            }

            // The canvas on the left, the palette on the right.
            float available = Mathf.Min(size.y - 100f, 420f);
            _cell = UiKit.CellSize(_n, available, Spacing, 110f);
            _gridCentre = new Vector2(-110f, 20f);
            var canvas = UiKit.Node(Area, "Canvas", Vector2.zero, _gridCentre);
            _cells = UiKit.Grid(canvas, _n, _cell, Spacing, Palette.PaperHi, null);

            _target = new int[_n * _n];
            _painted = new int[_n * _n];
            foreach (int i in LevelRng.Distinct(Rng, marked, _n * _n)) _target[i] = 1 + RandomRange(0, _colours);
            for (int i = 0; i < _cells.Length; i++) _cells[i].color = Paints[_target[i]];

            // Swatches: the eraser (paper) and each colour in play.
            int swatchCount = _colours + 1;
            _swatches = new Image[swatchCount];
            _swatchRects = new Rect[swatchCount];
            var swatchSize = new Vector2(96f, 72f);
            for (int s = 0; s < swatchCount; s++)
            {
                var pos = new Vector2(330f, 20f + ((swatchCount - 1) * 0.5f - s) * (swatchSize.y + 16f));
                _swatches[s] = UiKit.Box(Area, "Swatch" + s, swatchSize, pos, Paints[(s + 1) % swatchCount == 0 ? 0 : s + 1]);
                _swatchRects[s] = new Rect(pos - swatchSize * 0.5f, swatchSize);
            }
            _label = UiKit.Label(Area, "Hint", "REMEMBER IT", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        /// <summary>Swatch index to paint index: the colours first, the eraser last.</summary>
        private int PaintOf(int swatch) => swatch == _swatches.Length - 1 ? 0 : swatch + 1;

        private int CellAt(Vector2 p)
        {
            float total = _n * _cell + (_n - 1) * Spacing;
            float x = p.x - _gridCentre.x + total * 0.5f, y = total * 0.5f - (p.y - _gridCentre.y);
            int c = Mathf.FloorToInt(x / (_cell + Spacing)), r = Mathf.FloorToInt(y / (_cell + Spacing));
            if (c < 0 || r < 0 || c >= _n || r >= _n) return -1;
            if (x - c * (_cell + Spacing) > _cell || y - r * (_cell + Spacing) > _cell) return -1;
            return r * _n + c;
        }

        private void MarkBrush()
        {
            for (int s = 0; s < _swatches.Length; s++)
                ((RectTransform)_swatches[s].transform).localScale = Vector3.one * (PaintOf(s) == _brush ? 1.15f : 0.9f);
        }

        protected override void OnTick(float dt)
        {
            if (!_wiped)
            {
                if (Elapsed < _showFor) return;
                _wiped = true;
                foreach (var c in _cells) c.color = Palette.PaperHi;
                _label.text = "PAINT IT BACK";
                MarkBrush();
            }

            if (Demo)
            {
                // The rule card's demo paints it back a colour at a time: that colour's swatch
                // first, then its cells, a move a beat.
                int cell = -1;
                for (int c = 1; c <= _colours && cell < 0; c++)
                    for (int i = 0; i < _target.Length && cell < 0; i++)
                        if (_target[i] == c && _painted[i] != c) cell = i;
                if (cell < 0) return;
                int colour = _target[cell];
                bool swatch = _demoBrush != colour;
                var at = swatch ? Where(_swatches[colour - 1]) : Where(_cells[cell]);
                float t = Elapsed - _showFor - _demoMoves * 0.5f;
                if (t >= 0.3f) PointAt(at);
                if (t < 0.6f) return;
                TapAt(at);
                _demoMoves++;
                if (swatch) { PickBrush(colour - 1); _demoBrush = colour; }
                else Paint(cell);
                return;
            }
            if (!Interactive)
            {
                // Someone watching sees it painted back, a cell at a time.
                int done = Mathf.FloorToInt((Elapsed - _showFor) / 0.25f);
                for (int i = 0, k = 0; i < _target.Length; i++)
                    if (_target[i] != 0 && k++ < done) _cells[i].color = Paints[_target[i]];
                return;
            }
            if (!CanAct) return;
            if (Elapsed > _showFor + Limit) { Fail("TOO SLOW"); return; }
            if (!KeyInput.MouseHeld() && !KeyInput.MousePressed()) return;
            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m)) return;

            if (KeyInput.MousePressed())
                for (int s = 0; s < _swatches.Length; s++)
                    if (_swatchRects[s].Contains(m)) { PickBrush(s); return; }
            Paint(CellAt(m));
        }

        /// <summary>A swatch clicked: by the player, or by the rule card's demo.</summary>
        private void PickBrush(int s)
        {
            if (!CanMove) return;
            _brush = PaintOf(s);
            MarkBrush();
        }

        /// <summary>The brush on a cell (-1 off the canvas): the player's mouse, or the demo's hand.</summary>
        private void Paint(int cell)
        {
            if (!CanMove || cell < 0 || _painted[cell] == _brush) return;
            _painted[cell] = _brush;
            _cells[cell].color = Paints[_brush];

            for (int i = 0; i < _target.Length; i++) if (_painted[i] != _target[i]) return;
            _label.text = "A PERFECT COPY";
            _label.color = Palette.Green;
            Finish(false, Ms(Elapsed - _showFor));
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            // The cells still wrong get the red cross.
            for (int i = 0; i < _cells.Length; i++) if (_painted[i] != _target[i]) _cells[i].color = Palette.Red;
            Finish(true, WorstMetric);
        }
    }
}
