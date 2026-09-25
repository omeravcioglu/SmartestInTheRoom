using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// A sequence you can see all of at once, so it's pure execution speed — until the
    /// gold arrows arrive and every one of them has to be inverted on the way past.
    /// </summary>
    public class ArrowRushGame : MinigameView
    {
        private static readonly string[] Glyphs = { string.Empty, "▲", "▶", "▼", "◀" };

        private int[] _dirs;      // 1 up, 2 right, 3 down, 4 left
        private bool[] _gold;
        private TMP_Text[] _labels;
        private Image[] _slots;
        private TMP_Text _label;
        private int _index;

        protected override void Build()
        {
            var size = AreaSize;
            int count;
            int golds;
            switch (Level)
            {
                case 1: count = 4; golds = 0; break;
                case 2: count = 6; golds = 0; break;
                case 3: count = 6; golds = 1; break;
                case 4: count = 8; golds = 2; break;
                default: count = Mathf.Min(12, 10 + (Level - 5)); golds = Mathf.Min(count, 4 + (Level - 5)); break;
            }

            _dirs = new int[count];
            _gold = new bool[count];
            for (int i = 0; i < count; i++) _dirs[i] = RandomRange(1, 5);
            foreach (int i in LevelRng.Distinct(Rng, golds, count)) _gold[i] = true;

            float cell = Mathf.Min(90f, (size.x - 80f - (count - 1) * 10f) / count);
            float total = count * cell + (count - 1) * 10f;
            _slots = new Image[count];
            _labels = new TMP_Text[count];
            for (int i = 0; i < count; i++)
            {
                float x = -total * 0.5f + cell * 0.5f + i * (cell + 10f);
                _slots[i] = UiKit.Box(Area, "Slot" + i, new Vector2(cell, cell), new Vector2(x, 20f), Palette.Neutral);
                _labels[i] = UiKit.Label(_slots[i].transform, "Arrow", Glyphs[_dirs[i]], cell * 0.55f,
                    _gold[i] ? Palette.Accent : Palette.Text, new Vector2(cell, cell), Vector2.zero);
            }

            UiKit.Label(Area, "Keys", "WASD  ·  GOLD = press the OPPOSITE way", 26f, Palette.Accent,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, 130f));
            _label = UiKit.Label(Area, "Hint", string.Empty, 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -100f));
            Highlight();
        }

        private static int Opposite(int dir) => dir == 1 ? 3 : dir == 3 ? 1 : dir == 2 ? 4 : 2;

        private void Highlight()
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i].color = i < _index ? Palette.Green : (i == _index ? Palette.PanelRaised : Palette.Neutral);
        }

        protected override void OnTick(float dt)
        {
            if (Elapsed > 10f) { Fail("TOO SLOW"); return; }
            if (!CanAct || _index >= _dirs.Length) return;

            int key = KeyInput.DirectionPressed();
            if (key == 0) return;

            int want = _gold[_index] ? Opposite(_dirs[_index]) : _dirs[_index];
            if (key != want) { Fail("WRONG WAY"); return; }

            _index++;
            Highlight();
            if (_index >= _dirs.Length)
            {
                _label.text = "CLEAR";
                _label.color = Palette.Green;
                Finish(false, Ms(Elapsed));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_index < _slots.Length) _slots[_index].color = Palette.Red;
            Finish(true, WorstMetric);
        }
    }
}
