using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Memory again, but ordered — knowing which boxes flashed is worth nothing if you
    /// can't put them back in sequence.
    /// </summary>
    public class SimonGame : MinigameView
    {
        private Image[] _cells;
        private TMP_Text _label;
        private readonly List<int> _sequence = new List<int>();
        private float _step;
        private float _showUntil;
        private int _shown = -1;
        private int _index;
        private bool _playing;

        protected override void Build()
        {
            var size = AreaSize;
            int n = Level >= 5 ? 4 : 3;
            int len = Mathf.Min(9, 3 + (Level - 1));
            _step = Mathf.Max(0.26f, 0.55f - (Level - 1) * 0.04f);

            float available = Mathf.Min(size.x - 80f, size.y - 90f);
            float cell = UiKit.CellSize(n, available, 12f);
            _cells = UiKit.Grid(Area, n, cell, 12f, Palette.Neutral, OnCell);

            int last = -1;
            for (int i = 0; i < len; i++)
            {
                int pick;
                do { pick = RandomRange(0, n * n); } while (pick == last && n * n > 1);
                _sequence.Add(pick);
                last = pick;
            }

            _label = UiKit.Label(Area, "Hint", "WATCH", 28f, Palette.Accent,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(available * 0.5f + 34f)));
        }

        protected override void OnTick(float dt)
        {
            if (!_playing)
            {
                int step = Mathf.FloorToInt(Elapsed / _step);
                if (step >= _sequence.Count)
                {
                    _playing = true;
                    _showUntil = Elapsed;
                    if (_shown >= 0) _cells[_sequence[_shown]].color = Palette.Neutral;
                    _shown = -1;
                    if (_label != null) { _label.text = "REPEAT IT"; _label.color = Palette.Text; }
                    return;
                }
                if (step != _shown)
                {
                    if (_shown >= 0) _cells[_sequence[_shown]].color = Palette.Neutral;
                    _shown = step;
                    _cells[_sequence[_shown]].color = Palette.Accent;
                }
                // A short gap between two flashes so a repeat is still readable.
                else if (Elapsed - step * _step > _step * 0.7f)
                    _cells[_sequence[step]].color = Palette.PanelRaised;
                return;
            }

            if (Elapsed - _showUntil > 12f) Fail("TOO SLOW");
        }

        private void OnCell(int index)
        {
            if (!CanAct || !_playing) return;

            if (index != _sequence[_index])
            {
                _cells[index].color = Palette.Red;
                Fail("WRONG ORDER");
                return;
            }

            _cells[index].color = Palette.Green;
            _index++;
            if (_index < _sequence.Count) return;

            _label.text = "PERFECT";
            _label.color = Palette.Green;
            Finish(false, Ms(Elapsed - _showUntil));
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
