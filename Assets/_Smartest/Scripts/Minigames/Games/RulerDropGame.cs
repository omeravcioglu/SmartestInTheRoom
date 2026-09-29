using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The school-lab reaction test: a ruler hangs there, drops at a moment nobody can guess,
    /// and you click to catch it. How far it fell is your reaction time, made visible. Click
    /// before it drops and you're out; from level four it sometimes twitches first to bait
    /// you. Ranked by the total distance fallen.
    /// </summary>
    public class RulerDropGame : MinigameView
    {
        private const float RulerW = 64f;
        private const float RulerH = 200f;
        /// <summary>Past this it has fallen through; kept inside the play area, which isn't clipped.</summary>
        private const float MaxFall = 220f;
        private const float Pause = 0.9f;
        private const float TwitchFor = 0.18f;

        private float[] _delays = new float[0];
        private bool[] _twitch = new bool[0];
        private int _drop;
        private float _dropAt;     // when this drop lets go
        private float _caughtAt = -1f;
        private float _gravity;
        private float _topY;
        private float _fallen;
        private float _totalFallen;
        private RectTransform _ruler;
        private Image _rulerImage;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int drops;
            switch (Level)
            {
                case 1: drops = 3; _gravity = 1350f; break;
                case 2: drops = 3; _gravity = 1750f; break;
                case 3: drops = 4; _gravity = 2150f; break;
                case 4: drops = 4; _gravity = 2550f; break;
                default: drops = 4; _gravity = Mathf.Min(3200f, 2650f + (Level - 5) * 130f); break;
            }
            // Four drops of up to 2.6 s wait each is the most a level has room for.
            // How long a drop takes to fall right through: 0.57 s at level one, never under
            // 0.37 s, which leaves a normal reaction time to spare.

            _delays = new float[drops];
            _twitch = new bool[drops];
            for (int i = 0; i < drops; i++)
            {
                _delays[i] = RandomRange(1.0f, 2.6f);
                _twitch[i] = Level >= 4 && RandomRange(0f, 1f) < 0.5f;
            }

            _topY = size.y * 0.5f - 20f - RulerH * 0.5f;
            _rulerImage = UiKit.Box(Area, "Ruler", new Vector2(RulerW, RulerH), new Vector2(0f, _topY), Palette.Accent);
            _ruler = (RectTransform)_rulerImage.transform;
            for (int k = 1; k < 10; k++)
                UiKit.Fill(_ruler, "Tick" + k, new Vector2(k % 5 == 0 ? 34f : 18f, 4f),
                    new Vector2(-RulerW * 0.5f + (k % 5 == 0 ? 21f : 13f), RulerH * 0.5f - k * RulerH / 10f), Palette.Ink);
            // The hand it falls through, at the ruler's foot and drawn over it.
            UiKit.Fill(Area, "Hand", new Vector2(RulerW + 80f, 12f), new Vector2(0f, _topY - RulerH * 0.5f + 4f), Palette.Ink);

            _dropAt = _delays[0];
            _label = UiKit.Label(Area, "Hint", "CATCH IT WHEN IT DROPS", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        protected override void OnTick(float dt)
        {
            if (_drop >= _delays.Length) return;

            // Between drops: the caught ruler rests where it stopped, then goes back up.
            if (_caughtAt >= 0f)
            {
                if (Elapsed - _caughtAt < Pause) return;
                _caughtAt = -1f;
                _fallen = 0f;
                _ruler.anchoredPosition = new Vector2(0f, _topY);
                _dropAt = Elapsed + _delays[_drop];
                _label.text = "CATCH IT WHEN IT DROPS";
                _label.color = Palette.TextDim;
            }

            float t = Elapsed - _dropAt;
            if (t < 0f)
            {
                // A bait twitch just before some drops.
                bool twitching = _twitch[_drop] && t > -0.6f && t < -0.6f + TwitchFor;
                _ruler.anchoredPosition = new Vector2(twitching ? 5f * Mathf.Sin(Elapsed * 90f) : 0f, _topY);
                if (CanAct && KeyInput.MousePressed()) Fail("TOO EARLY");
                return;
            }

            _fallen = 0.5f * _gravity * t * t;
            _ruler.anchoredPosition = new Vector2(0f, _topY - Mathf.Min(_fallen, MaxFall));

            bool catchIt = CanAct ? KeyInput.MousePressed() : (!Interactive && t >= 0.24f);
            if (catchIt)
            {
                _totalFallen += _fallen;
                _caughtAt = Elapsed;
                _label.text = Mathf.RoundToInt(_fallen) + " PX";
                _label.color = Palette.Green;
                _drop++;
                if (_drop >= _delays.Length && CanAct)
                {
                    // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                    Finish(false, Mathf.RoundToInt(_totalFallen * 10f));
                }
                return;
            }
            if (_fallen > MaxFall && CanAct) Fail("DROPPED IT");
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_rulerImage != null) _rulerImage.color = Palette.Red;
            Finish(true, WorstMetric);
        }
    }
}
