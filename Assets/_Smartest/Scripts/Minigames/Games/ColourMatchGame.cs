using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// A colour shows in the middle; click the pad of the same colour, as fast as you can,
    /// again and again. The four colours differ in brightness as well as hue, so they stay
    /// apart for colour-blind players too (no red against green). From level four the pads
    /// swap places every few prompts. Ranked by total reaction time.
    /// </summary>
    public class ColourMatchGame : MinigameView
    {
        private const float Gap = 0.35f;

        private static readonly Color[] Colours = { Palette.Gold, Palette.Blue, Palette.Red, Palette.Ink };

        private Image[] _pads = new Image[4];
        private Rect[] _rects = new Rect[4];
        private Vector2[] _spots = new Vector2[4];
        private int[] _padColour = { 0, 1, 2, 3 };  // which colour each pad shows
        private int[] _prompts = new int[0];
        private int[][] _layouts = new int[0][];     // pad colours from each prompt on
        private float _window;
        private int _index;
        private float _showAt;
        private bool _showing;
        private float _reactionSum;
        private Image _swatch;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int n;
            bool swaps;
            switch (Level)
            {
                case 1: n = 6; _window = 2.0f; swaps = false; break;
                case 2: n = 8; _window = 1.6f; swaps = false; break;
                case 3: n = 10; _window = 1.3f; swaps = false; break;
                case 4: n = 12; _window = 1.1f; swaps = true; break;
                default: n = 12; _window = Mathf.Max(0.8f, 1.0f - (Level - 5) * 0.03f); swaps = true; break;
            }

            var padSize = new Vector2(220f, 130f);
            float x = size.x * 0.5f - padSize.x * 0.5f - 90f, y = size.y * 0.5f - padSize.y * 0.5f - 20f;
            _spots = new[] { new Vector2(-x, y), new Vector2(x, y), new Vector2(-x, -y + 50f), new Vector2(x, -y + 50f) };
            for (int i = 0; i < 4; i++)
                _pads[i] = UiKit.Box(Area, "Pad" + i, padSize, _spots[i], Colours[i]);
            for (int i = 0; i < 4; i++) _rects[i] = new Rect(_spots[i] - padSize * 0.5f, padSize);
            _swatch = UiKit.Dot(Area, "Swatch", 170f, new Vector2(0f, 25f), Palette.PanelRaised);

            // Never the same colour twice running: a repeat can look like nothing happened.
            _prompts = new int[n];
            _layouts = new int[n][];
            var layout = new[] { 0, 1, 2, 3 };
            for (int i = 0; i < n; i++)
            {
                int c;
                do c = RandomRange(0, 4); while (i > 0 && c == _prompts[i - 1]);
                _prompts[i] = c;
                if (swaps && i > 0 && i % 3 == 0)
                    for (int s = 3; s > 0; s--)
                    {
                        int j = RandomRange(0, s + 1);
                        (layout[s], layout[j]) = (layout[j], layout[s]);
                    }
                _layouts[i] = (int[])layout.Clone();
            }

            _showAt = 0.5f;
            _label = UiKit.Label(Area, "Hint", "MATCH THE COLOUR", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            Progress("MATCHED", 0, n);
        }

        private void Next()
        {
            _showing = false;
            _swatch.color = Palette.PanelRaised;
            _index++;
            Progress("MATCHED", _index, _prompts.Length);
            _showAt = Elapsed + Gap;
        }

        protected override void OnTick(float dt)
        {
            if (_index >= _prompts.Length) return;
            if (!_showing && Elapsed >= _showAt)
            {
                _showing = true;
                _showAt = Elapsed;
                _padColour = _layouts[_index];
                for (int i = 0; i < 4; i++) _pads[i].color = Colours[_padColour[i]];
                _swatch.color = Colours[_prompts[_index]];
            }
            // The rule card's demo waits just under the swatch for the first colour.
            if (Demo && !DemoHand.Shown) PointAt(new Vector2(0f, -110f));
            if (!_showing) return;

            if (!CanAct)
            {
                // The demo takes a beat to see the colour, moves to its pad and clicks it.
                if (Demo)
                {
                    int match = 0;
                    for (int i = 0; i < 4; i++) if (_padColour[i] == _prompts[_index]) match = i;
                    if (Elapsed - _showAt >= 0.3f) PointAt(_spots[match]);
                    if (Elapsed - _showAt < 0.55f) return;
                    TapAt(_spots[match]);
                    _reactionSum += Elapsed - _showAt;
                    Next();
                    if (_index >= _prompts.Length)
                    {
                        _label.text = "SHARP EYES";
                        _label.color = Palette.Green;
                        Finish(false, Ms(_reactionSum));
                    }
                    return;
                }
                if (Elapsed - _showAt >= 0.45f) Next(); // someone watching sees a steady pace
                return;
            }
            if (Elapsed - _showAt > _window) { Fail("TOO SLOW"); return; }
            if (!KeyInput.MousePressed() || !UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var click)) return;

            int pad = -1;
            for (int i = 0; i < 4; i++) if (_rects[i].Contains(click)) pad = i;
            if (pad < 0) return;
            if (_padColour[pad] != _prompts[_index]) { Fail("WRONG COLOUR"); return; }

            _reactionSum += Elapsed - _showAt;
            Next();
            if (_index >= _prompts.Length)
            {
                _label.text = "SHARP EYES";
                _label.color = Palette.Green;
                Finish(false, Ms(_reactionSum));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
