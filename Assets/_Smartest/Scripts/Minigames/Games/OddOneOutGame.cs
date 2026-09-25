using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Visual search. The difference starts obvious and ends at a few percent of one
    /// channel, which by level five is a genuine test of a monitor and a pair of eyes.
    /// </summary>
    public class OddOneOutGame : MinigameView
    {
        private Image[] _cells;
        private TMP_Text _label;
        private int _answer;

        protected override void Build()
        {
            var size = AreaSize;
            int n;
            float delta;
            switch (Level)
            {
                case 1: n = 3; delta = 0.85f; break;
                case 2: n = 4; delta = 0.30f; break;
                case 3: n = 5; delta = 0.20f; break;
                case 4: n = 6; delta = 0.12f; break;
                default: n = 7; delta = Mathf.Max(0.05f, 0.09f - (Level - 5) * 0.01f); break;
            }

            float available = Mathf.Min(size.x - 80f, size.y - 90f);
            float cell = UiKit.CellSize(n, available, 8f);

            // A colour from the palette family, so the grid still looks like this game.
            Color baseColor = Color.Lerp(Palette.Neutral, Palette.PanelRaised, RandomRange(0f, 1f));
            Color odd = Color.Lerp(baseColor, Palette.Accent, delta);

            _cells = UiKit.Grid(Area, n, cell, 8f, baseColor, OnCell);
            _answer = RandomRange(0, n * n);
            _cells[_answer].color = odd;

            _label = UiKit.Label(Area, "Hint", "ONE IS DIFFERENT", 28f, Palette.Text,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(available * 0.5f + 34f)));
        }

        protected override void OnTick(float dt)
        {
            if (Elapsed > 8f) Fail("TOO SLOW");
        }

        private void OnCell(int index)
        {
            if (!CanAct) return;
            if (index != _answer)
            {
                _cells[index].color = Palette.Red;
                Fail("WRONG BOX");
                return;
            }
            _cells[index].color = Palette.Green;
            _label.text = "FOUND IT";
            _label.color = Palette.Green;
            Finish(false, Ms(Elapsed));
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_cells != null) _cells[_answer].color = Palette.Green;
            Finish(true, WorstMetric);
        }
    }
}
