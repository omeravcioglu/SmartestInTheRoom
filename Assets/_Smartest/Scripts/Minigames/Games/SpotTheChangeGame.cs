using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Change blindness, made competitive. The pattern blinks, one box has moved, and the
    /// eye is astonishingly bad at noticing which — until it isn't.
    /// </summary>
    public class SpotTheChangeGame : MinigameView
    {
        private Image[] _cells;
        private TMP_Text _label;
        private readonly List<int> _pattern = new List<int>();
        private float _lookFor;
        private float _blink = 0.18f;
        private int _answer;
        private int _removed;
        private int _stage; // 0 = first look, 1 = blink, 2 = answer

        protected override void Build()
        {
            var size = AreaSize;
            int n, k;
            switch (Level)
            {
                case 1: n = 4; k = 6; _lookFor = 2.0f; break;
                case 2: n = 5; k = 8; _lookFor = 1.6f; break;
                case 3: n = 5; k = 10; _lookFor = 1.2f; break;
                case 4: n = 6; k = 12; _lookFor = 1.0f; break;
                default: n = 6; k = Mathf.Min(18, 14 + (Level - 5)); _lookFor = Mathf.Max(0.5f, 0.8f - (Level - 5) * 0.05f); break;
            }

            float available = Mathf.Min(size.x - 80f, size.y - 90f);
            float cell = UiKit.CellSize(n, available, 8f);
            _cells = UiKit.Grid(Area, n, cell, 8f, Palette.Neutral, OnCell);

            int total = n * n;
            var lit = LevelRng.Distinct(Rng, k, total);
            _pattern.AddRange(lit);

            // One lit box moves to a dark one; the new position is the answer.
            _removed = _pattern[RandomRange(0, _pattern.Count)];
            var dark = new List<int>();
            for (int i = 0; i < total; i++) if (!_pattern.Contains(i)) dark.Add(i);
            _answer = dark.Count > 0 ? dark[RandomRange(0, dark.Count)] : _removed;

            foreach (int i in _pattern) _cells[i].color = Palette.Accent;

            _label = UiKit.Label(Area, "Hint", "LOOK", 28f, Palette.Accent,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(available * 0.5f + 34f)));
        }

        protected override void OnTick(float dt)
        {
            if (_stage == 0 && Elapsed >= _lookFor)
            {
                _stage = 1;
                foreach (int i in _pattern) _cells[i].color = Palette.Neutral;
                if (_label != null) _label.text = string.Empty;
            }
            else if (_stage == 1 && Elapsed >= _lookFor + _blink)
            {
                _stage = 2;
                foreach (int i in _pattern) if (i != _removed) _cells[i].color = Palette.Accent;
                _cells[_answer].color = Palette.Accent;
                if (_label != null) { _label.text = "WHICH MOVED?"; _label.color = Palette.Text; }
            }
            else if (_stage == 2 && Elapsed - _lookFor - _blink > 8f) Fail("TOO SLOW");
        }

        private void OnCell(int index)
        {
            if (!CanAct || _stage != 2) return;
            if (index != _answer)
            {
                _cells[index].color = Palette.Red;
                Fail("WRONG BOX");
                return;
            }
            _cells[index].color = Palette.Green;
            _label.text = "SPOTTED";
            _label.color = Palette.Green;
            Finish(false, Ms(Elapsed - _lookFor - _blink));
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_cells != null) _cells[_answer].color = Palette.Green;
            Finish(true, WorstMetric);
        }
    }
}
