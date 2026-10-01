using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The original Simon: four coloured pads, a sequence of flashes, and you play it back.
    /// Where Simon asks you to remember places on a grid, this one is a string of colours, and
    /// it gets long. The pads are plain boxes rather than kit cells, so red and green here are
    /// just colours, not right and wrong; each carries its own mark (star, diamond, dot,
    /// square) so the sequence can be followed by shape too. Ranked by how fast you play it back.
    /// </summary>
    public class EchoGame : MinigameView
    {
        private const float AnswerFor = 10f;
        private const float ClickFlash = 0.15f;

        private Image[] _pads = new Image[4];
        private Rect[] _padRects = new Rect[4];
        private Color[] _lit = new Color[4];
        private Color[] _dim = new Color[4];
        private readonly List<int> _sequence = new List<int>();
        private float _step;
        private float _showEnd;
        private int _index;
        private int _flashPad = -1;
        private float _flashUntil;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int len;
            switch (Level)
            {
                case 1: len = 3; _step = 0.65f; break;
                case 2: len = 4; _step = 0.6f; break;
                case 3: len = 5; _step = 0.55f; break;
                case 4: len = 6; _step = 0.5f; break;
                default:
                    len = Mathf.Min(12, 7 + (Level - 5));
                    _step = Mathf.Max(0.32f, 0.45f - (Level - 5) * 0.02f);
                    break;
            }

            _lit = new[] { Palette.Gold, Palette.Red, Palette.Green, Palette.Blue };
            var marks = new[] { UiKit.Mark.Star, UiKit.Mark.Diamond, UiKit.Mark.Dot, UiKit.Mark.Square };
            var padSize = new Vector2(190f, 150f);
            float gap = 18f;
            for (int i = 0; i < 4; i++)
            {
                _dim[i] = Color.Lerp(_lit[i], Palette.Paper, 0.72f);
                var pos = new Vector2((i % 2 == 0 ? -1f : 1f) * (padSize.x + gap) * 0.5f,
                                      20f + (i < 2 ? 1f : -1f) * (padSize.y + gap) * 0.5f);
                _pads[i] = UiKit.Box(Area, "Pad" + i, padSize, pos, _dim[i]);
                UiKit.Marker(_pads[i].transform, "Mark", marks[i], 46f, Vector2.zero, Palette.Ink);
                _padRects[i] = new Rect(pos - padSize * 0.5f, padSize);
            }

            for (int i = 0; i < len; i++) _sequence.Add(RandomRange(0, 4));
            _showEnd = 0.4f + len * _step;

            _label = UiKit.Label(Area, "Hint", "WATCH", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        protected override void OnTick(float dt)
        {
            // The show: each flash lights for most of its step, with a gap so repeats read as two.
            if (Elapsed < _showEnd)
            {
                float t = Elapsed - 0.4f;
                int step = t < 0f ? -1 : Mathf.FloorToInt(t / _step);
                bool on = step >= 0 && t - step * _step < _step * 0.7f;
                for (int i = 0; i < 4; i++) _pads[i].color = on && _sequence[step] == i ? _lit[i] : _dim[i];
                return;
            }
            if (_label.text == "WATCH")
            {
                for (int i = 0; i < 4; i++) _pads[i].color = _dim[i];
                _label.text = "YOUR TURN";
                Progress("PLAYED", 0, _sequence.Count);
            }

            if (_flashPad >= 0 && Elapsed >= _flashUntil) { _pads[_flashPad].color = _dim[_flashPad]; _flashPad = -1; }

            // The rule card's demo plays it back like a sure player, a pad a beat.
            if (Demo)
            {
                if (IsDone || Elapsed - _showEnd < 0.3f) return;
                int next = _sequence[_index];
                PointAt(Where(_pads[next]));
                if (Elapsed - _showEnd >= 0.65f + _index * 0.55f)
                {
                    TapAt(Where(_pads[next]));
                    Press(next);
                }
                return;
            }

            if (!CanAct) return;
            if (Elapsed - _showEnd > AnswerFor) { Fail("TOO SLOW"); return; }
            if (!KeyInput.MousePressed() || !UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var click)) return;

            int pad = -1;
            for (int i = 0; i < 4; i++) if (_padRects[i].Contains(click)) pad = i;
            if (pad >= 0) Press(pad);
        }

        /// <summary>A pad clicked: by the player, or by the rule card's demo.</summary>
        private void Press(int pad)
        {
            if (!CanMove) return;
            if (_flashPad >= 0) _pads[_flashPad].color = _dim[_flashPad];
            _pads[pad].color = _lit[pad];
            _flashPad = pad;
            _flashUntil = Elapsed + ClickFlash;

            if (pad != _sequence[_index])
            {
                // Show which one it should have been.
                _pads[_sequence[_index]].color = _lit[_sequence[_index]];
                Fail("WRONG COLOUR");
                return;
            }
            Progress("PLAYED", ++_index, _sequence.Count);
            if (_index < _sequence.Count) return;

            _label.text = "PERFECT";
            _label.color = Palette.Green;
            Finish(false, Ms(Elapsed - _showEnd));
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
