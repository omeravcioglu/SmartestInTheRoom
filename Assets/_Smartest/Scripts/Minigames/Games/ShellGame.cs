using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The street hustle, played straight: one ball, a few cups, a shuffle everyone sees
    /// identically. Nothing to read and nothing to remember except where your eyes went.
    /// </summary>
    public class ShellGame : MinigameView
    {
        private const float ShowFor = 1.3f;   // cup up, ball visible
        private const float LowerFor = 0.3f;  // cup comes down over it
        private const float LiftHeight = 120f;
        private const float ArcHeight = 64f;
        private const float AnswerFor = 5f;

        private struct Swap { public int SlotA, SlotB; }

        private Image[] _cups = new Image[0];
        private float[] _slotX = new float[0];
        private int[] _slotOf = new int[0]; // cup -> slot, as of the swaps applied so far
        private Swap[] _swaps = new Swap[0];
        private int _applied;
        private int _ballCup;
        private Image _ball;
        private float _swapTime;
        private float _cupY;
        private Vector2 _cupSize;
        private float _shuffleEnd = -1f;
        private bool _revealed;
        private TMP_Text _label;

        private float ShuffleStart => ShowFor + LowerFor;

        protected override void Build()
        {
            var size = AreaSize;
            int k, m;
            switch (Level)
            {
                case 1: k = 3; m = 3; _swapTime = 0.62f; break;
                case 2: k = 3; m = 5; _swapTime = 0.5f; break;
                case 3: k = 4; m = 6; _swapTime = 0.44f; break;
                case 4: k = 4; m = 8; _swapTime = 0.38f; break;
                default:
                    k = 5;
                    m = Mathf.Min(14, 9 + (Level - 5));
                    _swapTime = Mathf.Max(0.26f, 0.33f - (Level - 5) * 0.01f);
                    break;
            }

            float spacing = Mathf.Min(250f, (size.x - 200f) / k);
            _cupSize = new Vector2(Mathf.Min(150f, spacing - 50f), 150f);
            _cupY = -10f;
            _slotX = new float[k];
            for (int s = 0; s < k; s++) _slotX[s] = (s - (k - 1) * 0.5f) * spacing;
            _slotOf = new int[k];
            for (int c = 0; c < k; c++) _slotOf[c] = c;
            _ballCup = RandomRange(0, k);

            // The ball's cup is in most swaps; otherwise following it would be free.
            _swaps = new Swap[m];
            var sim = (int[])_slotOf.Clone();
            for (int i = 0; i < m; i++)
            {
                int a = RandomRange(0f, 1f) < 0.6f ? sim[_ballCup] : RandomRange(0, k);
                int b;
                do b = RandomRange(0, k); while (b == a);
                _swaps[i] = new Swap { SlotA = a, SlotB = b };
                Exchange(sim, a, b);
            }

            // The ball first, so the cups draw over it.
            _ball = UiKit.Dot(Area, "Ball", 54f, BallSpot(), Palette.Accent);
            _cups = new Image[k];
            for (int c = 0; c < k; c++)
            {
                int cup = c;
                _cups[c] = UiKit.Cell(Area, "Cup" + c, _cupSize, new Vector2(_slotX[c], _cupY), Palette.Neutral,
                    () => OnCup(cup), out _, string.Empty, 30f);
            }
            SetCup(_ballCup, _slotX[_ballCup], _cupY + LiftHeight);

            _label = UiKit.Label(Area, "Hint", "WATCH THE BALL", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        private static void Exchange(int[] slotOf, int slotA, int slotB)
        {
            for (int c = 0; c < slotOf.Length; c++)
            {
                if (slotOf[c] == slotA) slotOf[c] = slotB;
                else if (slotOf[c] == slotB) slotOf[c] = slotA;
            }
        }

        private int CupIn(int slot)
        {
            for (int c = 0; c < _slotOf.Length; c++) if (_slotOf[c] == slot) return c;
            return -1;
        }

        private Vector2 BallSpot() =>
            new Vector2(_slotX[_slotOf[_ballCup]], _cupY - _cupSize.y * 0.5f + 30f);

        private void SetCup(int cup, float x, float y) =>
            ((RectTransform)_cups[cup].transform).anchoredPosition = new Vector2(x, y);

        protected override void OnTick(float dt)
        {
            float t = Elapsed;

            // 1. The cup is up and the ball is plain to see, then the cup comes down on it.
            if (t < ShuffleStart)
            {
                float up = t < ShowFor ? 1f : 1f - (t - ShowFor) / LowerFor;
                SetCup(_ballCup, _slotX[_ballCup], _cupY + LiftHeight * Smooth(up));
                return;
            }
            if (_ball.gameObject.activeSelf && !_revealed)
            {
                _ball.gameObject.SetActive(false);
                SetCup(_ballCup, _slotX[_ballCup], _cupY);
                _label.text = "FOLLOW THE CUP";
            }

            // 2. The shuffle: one swap at a time, one cup over the top and one underneath.
            int i = Mathf.FloorToInt((t - ShuffleStart) / _swapTime);
            while (_applied < Mathf.Min(i, _swaps.Length))
            {
                var done = _swaps[_applied++];
                Exchange(_slotOf, done.SlotA, done.SlotB);
                SetCup(CupIn(done.SlotA), _slotX[done.SlotA], _cupY);
                SetCup(CupIn(done.SlotB), _slotX[done.SlotB], _cupY);
            }
            if (i < _swaps.Length)
            {
                var sw = _swaps[i];
                int ca = CupIn(sw.SlotA), cb = CupIn(sw.SlotB);
                float u = Smooth(((t - ShuffleStart) - i * _swapTime) / _swapTime);
                float lift = Mathf.Sin(u * Mathf.PI) * ArcHeight;
                SetCup(ca, Mathf.Lerp(_slotX[sw.SlotA], _slotX[sw.SlotB], u), _cupY + lift);
                SetCup(cb, Mathf.Lerp(_slotX[sw.SlotB], _slotX[sw.SlotA], u), _cupY - lift);
                return;
            }

            // 3. Which one?
            if (_shuffleEnd < 0f)
            {
                _shuffleEnd = t;
                for (int c = 0; c < _cups.Length; c++) SetCup(c, _slotX[_slotOf[c]], _cupY);
                _label.text = "WHICH CUP?";
            }
            if (!Interactive && !_revealed && t - _shuffleEnd > 1.5f) Reveal();
            if (CanAct && t - _shuffleEnd > AnswerFor)
            {
                Reveal();
                Fail("TOO SLOW");
            }
        }

        private static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        private void OnCup(int cup)
        {
            if (!CanAct || _shuffleEnd < 0f) return;
            Reveal();
            if (cup == _ballCup)
            {
                _cups[cup].color = Palette.Green;
                _label.text = "FOUND IT";
                _label.color = Palette.Green;
                Finish(false, Ms(Elapsed - _shuffleEnd));
            }
            else
            {
                _cups[cup].color = Palette.Red;
                Fail("WRONG CUP");
            }
        }

        /// <summary>Lift the right cup and show the ball under it.</summary>
        private void Reveal()
        {
            if (_revealed) return;
            _revealed = true;
            _ball.rectTransform.anchoredPosition = BallSpot();
            _ball.gameObject.SetActive(true);
            SetCup(_ballCup, _slotX[_slotOf[_ballCup]], _cupY + LiftHeight);
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
