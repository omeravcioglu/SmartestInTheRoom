using Smartest.Core;
using TMPro;
using UnityEngine;

namespace Smartest.Minigames
{
    /// <summary>
    /// The word fights the colour. Reading is automatic and that's the trap — this level
    /// punishes the fast reader and rewards whoever can switch the habit off.
    ///
    /// Red and BLACK ink, not red and green: about one man in twelve can't tell red from
    /// green, which made this the one minigame some players could never win. Red against
    /// the page's own black ink differs in brightness too, so it reads for everyone.
    /// </summary>
    public class StroopGame : MinigameView
    {
        private TMP_Text _word;
        private TMP_Text _progress;
        private TMP_Text _label;

        private string[] _words;
        private int[] _inks;      // 1 = red, 2 = black
        private int _index;
        private float _perPrompt;
        private float _promptStart;

        // Level 1's words are all in their own colour, which hides the trap: the demo shows level 2's.
        public override int DemoLevel => 2;

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
                    // Naming the ink of a mismatched word takes most people 600-700 ms before
                    // they even press a key; a 0.5 s limit meant everybody failed, forever.
                    // Past level four it gets longer, not impossibly faster.
                    count = Mathf.Min(12, 10 + (Level - 5));
                    _perPrompt = Mathf.Max(0.7f, 0.8f - (Level - 5) * 0.025f);
                    mismatch = true; neutral = true; break;
            }

            _words = new string[count];
            _inks = new int[count];
            for (int i = 0; i < count; i++)
            {
                int ink = RandomRange(0, 2) == 0 ? 1 : 2;
                _inks[i] = ink;
                if (!mismatch) _words[i] = ink == 1 ? "RED" : "BLACK";
                else if (neutral && RandomRange(0, 5) == 0) _words[i] = "GOLD";
                else _words[i] = RandomRange(0, 2) == 0 ? "RED" : "BLACK";
            }

            UiKit.Label(Area, "Keys", "1 = RED INK      2 = BLACK INK", 26f, Palette.Accent,
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
            _word.color = _inks[_index] == 1 ? Palette.Red : Palette.Ink;
            _progress.text = $"{_index + 1} / {_words.Length}";
            _promptStart = Elapsed;
        }

        protected override void OnTick(float dt)
        {
            if (_index >= _words.Length) return;
            if (Elapsed - _promptStart > _perPrompt) { Fail("TOO SLOW"); return; }

            // The rule card's demo (never a spectator: it gives the answers away) reads the ink
            // and answers after a reader's pause.
            int key = CanAct ? KeyInput.DigitPressed()
                : (Demo && Elapsed - _promptStart >= 0.9f && DemoAnswer(_inks[_index].ToString()) ? _inks[_index] : 0);
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
