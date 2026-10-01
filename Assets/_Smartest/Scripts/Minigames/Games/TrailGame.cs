using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Simon drawn as a snake: a path crawls across the grid, cell by cell, leaving a fading
    /// trail, then goes dark. Draw it back: hold the button and sweep through the cells in
    /// order, or click them one by one. A path is easier to hold than scattered flashes, so
    /// the paths get long. One wrong cell and you're out. Ranked by time.
    /// </summary>
    public class TrailGame : MinigameView
    {
        private const float Spacing = 10f;
        private const float AnswerFor = 10f;

        private Image[] _cells = new Image[0];
        private readonly List<int> _path = new List<int>();
        private int _n;
        private float _cell;
        private float _step;
        private float _showEnd;
        private int _index;
        private int _last = -1;
        private bool _dark;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int len;
            switch (Level)
            {
                case 1: _n = 4; len = 4; _step = 0.45f; break;
                case 2: _n = 4; len = 5; _step = 0.4f; break;
                case 3: _n = 5; len = 7; _step = 0.35f; break;
                case 4: _n = 5; len = 8; _step = 0.3f; break;
                default: _n = 6; len = Mathf.Min(14, 9 + (Level - 5)); _step = Mathf.Max(0.2f, 0.26f - (Level - 5) * 0.01f); break;
            }

            float available = Mathf.Min(size.x - 80f, size.y - 110f); // clear of the hint below
            _cell = UiKit.CellSize(_n, available, Spacing);
            _cells = UiKit.Grid(Area, _n, _cell, Spacing, Palette.Neutral, null);

            // A path that never crosses itself: a random walk that restarts if it traps itself.
            for (int attempt = 0; attempt < 60 && _path.Count < len; attempt++)
            {
                _path.Clear();
                int at = RandomRange(0, _n * _n);
                _path.Add(at);
                while (_path.Count < len)
                {
                    var next = new List<int>();
                    int r = at / _n, c = at % _n;
                    if (r > 0 && !_path.Contains(at - _n)) next.Add(at - _n);
                    if (r < _n - 1 && !_path.Contains(at + _n)) next.Add(at + _n);
                    if (c > 0 && !_path.Contains(at - 1)) next.Add(at - 1);
                    if (c < _n - 1 && !_path.Contains(at + 1)) next.Add(at + 1);
                    if (next.Count == 0) break;
                    at = next[RandomRange(0, next.Count)];
                    _path.Add(at);
                }
            }
            _showEnd = 0.3f + _path.Count * _step + 0.4f;

            _label = UiKit.Label(Area, "Hint", "WATCH THE PATH", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        /// <summary>The cell under a point, from the same layout UiKit.Grid used; -1 between cells.</summary>
        private int CellAt(Vector2 p)
        {
            float total = _n * _cell + (_n - 1) * Spacing;
            float x = p.x + total * 0.5f, y = total * 0.5f - p.y;
            int c = Mathf.FloorToInt(x / (_cell + Spacing)), r = Mathf.FloorToInt(y / (_cell + Spacing));
            if (c < 0 || r < 0 || c >= _n || r >= _n) return -1;
            if (x - c * (_cell + Spacing) > _cell || y - r * (_cell + Spacing) > _cell) return -1;
            return r * _n + c;
        }

        protected override void OnTick(float dt)
        {
            // The show: the head lights gold, the trail behind it stays pale.
            if (Elapsed < _showEnd)
            {
                int head = Mathf.FloorToInt((Elapsed - 0.3f) / _step);
                for (int i = 0; i < _path.Count; i++)
                    _cells[_path[i]].color = i == head ? Palette.Accent : i < head ? Palette.AccentDim : Palette.Neutral;
                return;
            }
            if (!_dark)
            {
                _dark = true;
                foreach (var cell in _cells) cell.color = Palette.Neutral;
                _label.text = "DRAW IT BACK";
                Progress("DRAWN", 0, _path.Count);
            }

            if (!Interactive)
            {
                // Someone watching sees it traced at a steady pace.
                int shown = Mathf.Min(_path.Count, Mathf.FloorToInt((Elapsed - _showEnd) / 0.3f));
                for (int i = 0; i < shown; i++) _cells[_path[i]].color = Palette.Green;
                return;
            }
            if (!CanAct) return;
            if (Elapsed - _showEnd > AnswerFor) { Fail("TOO SLOW", -1); return; }
            if (!KeyInput.MouseHeld() && !KeyInput.MousePressed()) { _last = -1; return; }
            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m)) return;

            int cellHere = CellAt(m);
            if (cellHere < 0 || cellHere == _last) return;
            _last = cellHere;
            // Sweeping back over a cell already drawn is fine; it just doesn't count again.
            if (_index > 0 && _path.IndexOf(cellHere) >= 0 && _path.IndexOf(cellHere) < _index) return;
            if (cellHere != _path[_index]) { Fail("WRONG WAY", cellHere); return; }

            _cells[cellHere].color = Palette.Green;
            _index++;
            Progress("DRAWN", _index, _path.Count);
            if (_index >= _path.Count)
            {
                _label.text = "PERFECT PATH";
                _label.color = Palette.Green;
                Finish(false, Ms(Elapsed - _showEnd));
            }
        }

        private void Fail(string why, int cell)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (cell >= 0) _cells[cell].color = Palette.Red;
            // Show the way it went.
            for (int i = _index; i < _path.Count; i++) if (_path[i] != cell) _cells[_path[i]].color = Palette.AccentDim;
            Finish(true, WorstMetric);
        }
    }
}
