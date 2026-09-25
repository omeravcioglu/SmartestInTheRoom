using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Spatial reasoning with nothing hidden: the whole puzzle is on screen the entire
    /// time, so this one is decided purely by who works it out first.
    /// </summary>
    public class MirrorGame : MinigameView
    {
        private Image[] _cells;
        private TMP_Text _label;
        private int _n;
        private int _axis; // 0 vertical, 1 horizontal, 2 diagonal
        private readonly List<int> _targets = new List<int>();
        private readonly HashSet<int> _found = new HashSet<int>();

        protected override void Build()
        {
            var size = AreaSize;
            int litCount;
            switch (Level)
            {
                case 1: _n = 5; _axis = 0; litCount = 1; break;
                case 2: _n = 5; _axis = 1; litCount = 1; break;
                case 3: _n = 5; _axis = 2; litCount = 1; break;
                case 4: _n = 7; _axis = RandomRange(0, 2); litCount = 2; break;
                default: _n = 7; _axis = 2; litCount = Mathf.Min(4, 3 + (Level - 5)); break;
            }

            float available = Mathf.Min(size.x - 80f, size.y - 90f);
            float cell = UiKit.CellSize(_n, available, 8f);
            _cells = UiKit.Grid(Area, _n, cell, 8f, Palette.Neutral, OnCell);

            // The gold line itself: cells on the axis are tinted, never used as answers.
            for (int i = 0; i < _n * _n; i++)
                if (OnAxis(i)) _cells[i].color = Palette.AccentDim;

            var candidates = new List<int>();
            for (int i = 0; i < _n * _n; i++) if (!OnAxis(i)) candidates.Add(i);
            LevelRng.Shuffle(Rng, candidates);

            for (int i = 0; i < candidates.Count && _targets.Count < litCount; i++)
            {
                int lit = candidates[i];
                int mirror = Mirror(lit);
                if (_targets.Contains(mirror) || _targets.Contains(lit)) continue;
                _cells[lit].color = Palette.Accent;
                _targets.Add(mirror);
            }

            _label = UiKit.Label(Area, "Hint",
                _targets.Count > 1 ? $"CLICK ALL {_targets.Count} MIRRORS" : "CLICK THE MIRROR",
                28f, Palette.Text, new Vector2(size.x - 60f, 40f), new Vector2(0f, -(available * 0.5f + 34f)));
        }

        private bool OnAxis(int index)
        {
            int r = index / _n, c = index % _n;
            switch (_axis)
            {
                case 0: return c == _n / 2;
                case 1: return r == _n / 2;
                default: return r == c;
            }
        }

        private int Mirror(int index)
        {
            int r = index / _n, c = index % _n;
            switch (_axis)
            {
                case 0: return r * _n + (_n - 1 - c);
                case 1: return (_n - 1 - r) * _n + c;
                default: return c * _n + r;
            }
        }

        protected override void OnTick(float dt)
        {
            if (Elapsed > 8f) Fail("TOO SLOW");
        }

        private void OnCell(int index)
        {
            if (!CanAct || _found.Contains(index)) return;

            if (!_targets.Contains(index))
            {
                _cells[index].color = Palette.Red;
                Fail("WRONG BOX");
                return;
            }

            _found.Add(index);
            _cells[index].color = Palette.Green;
            if (_found.Count < _targets.Count) return;

            _label.text = "MIRRORED";
            _label.color = Palette.Green;
            Finish(false, Ms(Elapsed));
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            foreach (int t in _targets) if (!_found.Contains(t)) _cells[t].color = Palette.Green;
            Finish(true, WorstMetric);
        }
    }
}
