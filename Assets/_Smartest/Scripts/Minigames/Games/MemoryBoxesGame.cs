using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Short-term memory, with a clock on it. Everyone sees the same boxes for the same
    /// time, so the only variable left is how well you hold a pattern in your head.
    /// </summary>
    public class MemoryBoxesGame : MinigameView
    {
        private Image[] _cells;
        private TMP_Text _label;
        private readonly HashSet<int> _lit = new HashSet<int>();
        private readonly HashSet<int> _found = new HashSet<int>();
        private float _showFor;
        private bool _hidden;

        protected override void Build()
        {
            var size = AreaSize;
            int n, k;
            switch (Level)
            {
                case 1: n = 4; k = 3; _showFor = 2.5f; break;
                case 2: n = 4; k = 4; _showFor = 2.0f; break;
                case 3: n = 5; k = 5; _showFor = 2.0f; break;
                case 4: n = 5; k = 6; _showFor = 1.5f; break;
                case 5: n = 5; k = 7; _showFor = 1.2f; break;
                default:
                    n = 5;
                    k = Mathf.Min(10, 7 + (Level - 5));
                    _showFor = Mathf.Max(0.6f, 1.2f - (Level - 5) * 0.1f);
                    break;
            }

            float available = Mathf.Min(size.x - 80f, size.y - 90f);
            float cell = UiKit.CellSize(n, available, 10f);
            _cells = UiKit.Grid(Area, n, cell, 10f, Palette.Neutral, OnCell);

            foreach (int i in LevelRng.Distinct(Rng, k, n * n)) _lit.Add(i);
            foreach (int i in _lit) _cells[i].color = Palette.Accent;

            _label = UiKit.Label(Area, "Hint", "MEMORISE", 28f, Palette.Accent,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(available * 0.5f + 34f)));
        }

        protected override void OnTick(float dt)
        {
            if (!_hidden && Elapsed >= _showFor)
            {
                _hidden = true;
                foreach (int i in _lit) _cells[i].color = Palette.Neutral;
                if (_label != null) { _label.text = "CLICK THEM"; _label.color = Palette.Text; }
            }
            if (_hidden && Elapsed - _showFor > 10f) Fail("TOO SLOW");
        }

        private void OnCell(int index)
        {
            if (!CanAct || !_hidden || _found.Contains(index)) return;

            if (!_lit.Contains(index))
            {
                _cells[index].color = Palette.Red;
                Fail("WRONG BOX");
                return;
            }

            _found.Add(index);
            _cells[index].color = Palette.Green;
            if (_found.Count < _lit.Count) return;

            _label.text = "PERFECT";
            _label.color = Palette.Green;
            Finish(false, Ms(Elapsed - _showFor));
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            foreach (int i in _lit) if (!_found.Contains(i)) _cells[i].color = Palette.Accent;
            Finish(true, WorstMetric);
        }
    }
}
