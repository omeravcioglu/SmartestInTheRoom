using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Three boxes light almost at once. At level five the gap is fifty milliseconds and
    /// there is no technique left — just whether your eyes are better than theirs.
    /// </summary>
    public class WhichWasFirstGame : MinigameView
    {
        private Image[] _boxes;
        private float[] _times;
        private TMP_Text _label;
        private int _first;
        private int _count;
        private float _startDelay;
        private bool _allLit;

        protected override void Build()
        {
            var size = AreaSize;
            float gap;
            switch (Level)
            {
                case 1: _count = 3; gap = 0.30f; break;
                case 2: _count = 3; gap = 0.20f; break;
                case 3: _count = 3; gap = 0.12f; break;
                case 4: _count = 3; gap = 0.08f; break;
                default: _count = 4; gap = Mathf.Max(0.03f, 0.05f - (Level - 5) * 0.005f); break;
            }

            float boxW = Mathf.Min(180f, (size.x - 80f - (_count - 1) * 24f) / _count);
            float total = _count * boxW + (_count - 1) * 24f;
            _boxes = new Image[_count];
            for (int i = 0; i < _count; i++)
            {
                float x = -total * 0.5f + boxW * 0.5f + i * (boxW + 24f);
                _boxes[i] = UiKit.Box(Area, "Box" + i, new Vector2(boxW, 180f), new Vector2(x, 30f), Palette.Neutral);
                UiKit.Label(_boxes[i].transform, "Num", (i + 1).ToString(), 44f, Palette.TextDim,
                    new Vector2(boxW, 60f), Vector2.zero);
            }

            _startDelay = RandomRange(0.6f, 2.2f);
            _first = RandomRange(0, _count);
            _times = new float[_count];
            var order = LevelRng.Distinct(Rng, _count, _count);
            // The chosen box lights first; the rest follow one gap apart in a shuffled order.
            int slot = 1;
            for (int i = 0; i < _count; i++)
            {
                int box = order[i];
                if (box == _first) continue;
                _times[box] = _startDelay + slot * gap;
                slot++;
            }
            _times[_first] = _startDelay;

            UiKit.Label(Area, "Keys", "Press the number of the FIRST box", 26f, Palette.Accent,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, 150f));
            _label = UiKit.Label(Area, "Hint", string.Empty, 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -100f));
        }

        protected override void OnTick(float dt)
        {
            for (int i = 0; i < _count; i++)
                if (Elapsed >= _times[i]) _boxes[i].color = Palette.Accent;
            if (!_allLit && Elapsed >= _startDelay) _allLit = true;

            if (_allLit && Elapsed - _startDelay > 4f) { Fail("TOO SLOW"); return; }
            if (!CanAct) return;

            int key = KeyInput.DigitPressed();
            if (key <= 0 || key > _count) return;
            if (!_allLit) { Fail("TOO EARLY"); return; }

            if (key - 1 == _first)
            {
                _boxes[_first].color = Palette.Green;
                Finish(false, Ms(Elapsed - _startDelay));
            }
            else Fail("WRONG BOX");
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_boxes != null && _first < _boxes.Length) _boxes[_first].color = Palette.Green;
            Finish(true, WorstMetric);
        }
    }
}
