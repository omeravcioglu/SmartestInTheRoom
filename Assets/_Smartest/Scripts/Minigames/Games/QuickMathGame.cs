using Smartest.Core;
using TMPro;
using UnityEngine;

namespace Smartest.Minigames
{
    /// <summary>
    /// Comparison, not calculation: you never have to say what the sums are, only which
    /// one is bigger. Fast enough to be a race, simple enough that nobody needs paper.
    /// </summary>
    public class QuickMathGame : MinigameView
    {
        private TMP_Text _left;
        private TMP_Text _right;
        private TMP_Text _progress;
        private TMP_Text _label;

        private string[] _leftText;
        private string[] _rightText;
        private int[] _answer;   // 1 = left, 2 = right
        private int _index;
        private float _perPrompt;
        private float _promptStart;

        protected override void Build()
        {
            var size = AreaSize;
            int count;
            int mode; // 0 = single-digit +, 1 = + and -, 2 = x, 3 = two-digit, 4 = mixed
            switch (Level)
            {
                case 1: count = 3; mode = 0; _perPrompt = 5f; break;
                case 2: count = 4; mode = 1; _perPrompt = 5f; break;
                case 3: count = 4; mode = 2; _perPrompt = 5f; break;
                case 4: count = 5; mode = 3; _perPrompt = 4f; break;
                default: count = 5; mode = 4; _perPrompt = Mathf.Max(2f, 3f - (Level - 5) * 0.2f); break;
            }

            _leftText = new string[count];
            _rightText = new string[count];
            _answer = new int[count];
            for (int i = 0; i < count; i++)
            {
                int a, b;
                do
                {
                    _leftText[i] = Expression(mode, out a);
                    _rightText[i] = Expression(mode, out b);
                } while (a == b);
                _answer[i] = a > b ? 1 : 2;
            }

            UiKit.Label(Area, "Keys", "1 = LEFT IS BIGGER      2 = RIGHT IS BIGGER", 26f, Palette.Accent,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, 150f));

            float half = Mathf.Min(320f, (size.x - 120f) * 0.5f);
            var leftBox = UiKit.Box(Area, "Left", new Vector2(half, 150f), new Vector2(-(half * 0.5f + 20f), 20f), Palette.Neutral);
            var rightBox = UiKit.Box(Area, "Right", new Vector2(half, 150f), new Vector2(half * 0.5f + 20f, 20f), Palette.Neutral);
            _left = UiKit.Label(leftBox.transform, "L", string.Empty, 52f, Palette.Text, new Vector2(half, 90f), Vector2.zero);
            _right = UiKit.Label(rightBox.transform, "R", string.Empty, 52f, Palette.Text, new Vector2(half, 90f), Vector2.zero);

            _progress = UiKit.Label(Area, "Progress", string.Empty, 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -90f));
            _label = UiKit.Label(Area, "Hint", string.Empty, 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -130f));
        }

        private string Expression(int mode, out int value)
        {
            switch (mode)
            {
                case 0:
                {
                    int a = RandomRange(1, 10), b = RandomRange(1, 10);
                    value = a + b;
                    return $"{a} + {b}";
                }
                case 1:
                {
                    int a = RandomRange(5, 20), b = RandomRange(1, 10);
                    bool plus = RandomRange(0, 2) == 0;
                    value = plus ? a + b : a - b;
                    return plus ? $"{a} + {b}" : $"{a} − {b}";
                }
                case 2:
                {
                    int a = RandomRange(2, 10), b = RandomRange(2, 10);
                    value = a * b;
                    return $"{a} × {b}";
                }
                case 3:
                {
                    int a = RandomRange(11, 60), b = RandomRange(11, 60);
                    value = a + b;
                    return $"{a} + {b}";
                }
                default:
                {
                    int pick = RandomRange(0, 3);
                    if (pick == 0) { int a = RandomRange(11, 40), b = RandomRange(2, 9); value = a * b; return $"{a} × {b}"; }
                    if (pick == 1) { int a = RandomRange(40, 99), b = RandomRange(11, 39); value = a - b; return $"{a} − {b}"; }
                    { int a = RandomRange(20, 80), b = RandomRange(20, 80); value = a + b; return $"{a} + {b}"; }
                }
            }
        }

        protected override void OnBegin()
        {
            _index = 0;
            ShowPrompt();
        }

        private void ShowPrompt()
        {
            if (_index >= _answer.Length) return;
            _left.text = _leftText[_index];
            _right.text = _rightText[_index];
            _progress.text = $"{_index + 1} / {_answer.Length}";
            _promptStart = Elapsed;
        }

        protected override void OnTick(float dt)
        {
            if (_index >= _answer.Length) return;
            if (Elapsed - _promptStart > _perPrompt) { Fail("TOO SLOW"); return; }
            if (!CanAct) return;

            int key = KeyInput.DigitPressed();
            if (key != 1 && key != 2) return;
            if (key != _answer[_index]) { Fail("WRONG"); return; }

            _index++;
            if (_index >= _answer.Length)
            {
                _left.text = "✓"; _right.text = "✓";
                _left.color = Palette.Green; _right.color = Palette.Green;
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
