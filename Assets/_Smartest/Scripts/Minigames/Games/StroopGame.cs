using Smartest.Core;
using TMPro;
using UnityEngine;

namespace Smartest.Minigames
{
    /// <summary>
    /// The word fights the colour. Reading is automatic and that's the trap — this level
    /// punishes the fast reader and rewards whoever can switch the habit off.
    /// </summary>
    public class StroopGame : MinigameView
    {
        private TMP_Text _word;
        private TMP_Text _progress;
        private TMP_Text _label;

        private string[] _words;
        private int[] _inks;      // 1 = red, 2 = green
        private int _index;
        private float _perPrompt;
        private float _promptStart;

        protected override void Build()
        {
            var size = AreaSize;
            int count;
            bool mismatch;
            bool neutral = false;
            switch (Level)
            {
                case 1: count = 5; _perPrompt = 2.0f; mismatch = false; break;
                case 2: count = 5; _perPrompt = 1.6f; mismatch = true; break;
                case 3: count = 6; _perPrompt = 1.2f; mismatch = true; break;
                case 4: count = 8; _perPrompt = 0.9f; mismatch = true; break;
                default:
                    count = Mathf.Min(12, 10 + (Level - 5));
                    _perPrompt = Mathf.Max(0.5f, 0.7f - (Level - 5) * 0.05f);
                    mismatch = true; neutral = true; break;
            }

            _words = new string[count];
            _inks = new int[count];
            for (int i = 0; i < count; i++)
            {
                int ink = RandomRange(0, 2) == 0 ? 1 : 2;
                _inks[i] = ink;
                if (!mismatch) _words[i] = ink == 1 ? "RED" : "GREEN";
                else if (neutral && RandomRange(0, 5) == 0) _words[i] = "GOLD";
                else _words[i] = RandomRange(0, 2) == 0 ? "RED" : "GREEN";
            }

            UiKit.Label(Area, "Keys", "1 = RED INK      2 = GREEN INK", 26f, Palette.Accent,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, 140f));
            _word = UiKit.Label(Area, "Word", string.Empty, 96f, Palette.Text,
                new Vector2(size.x - 60f, 140f), new Vector2(0f, 20f));
            _progress = UiKit.Label(Area, "Progress", string.Empty, 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -80f));
            _label = UiKit.Label(Area, "Hint", string.Empty, 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -120f));
        }

        protected override void OnBegin()
        {
            _index = 0;
            _promptStart = 0f;
            ShowPrompt();
        }

        private void ShowPrompt()
        {
            if (_index >= _words.Length) return;
            _word.text = _words[_index];
            _word.color = _inks[_index] == 1 ? Palette.Red : Palette.Green;
            _progress.text = $"{_index + 1} / {_words.Length}";
            _promptStart = Elapsed;
        }

        protected override void OnTick(float dt)
        {
            if (_index >= _words.Length) return;
            if (Elapsed - _promptStart > _perPrompt) { Fail("TOO SLOW"); return; }
            if (!CanAct) return;

            int key = KeyInput.DigitPressed();
            if (key != 1 && key != 2) return;
            if (key != _inks[_index]) { Fail("WRONG"); return; }

            _index++;
            if (_index >= _words.Length)
            {
                _word.text = "DONE";
                _word.color = Palette.Accent;
                Finish(false, Ms(Elapsed));
            }
            else ShowPrompt();
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
