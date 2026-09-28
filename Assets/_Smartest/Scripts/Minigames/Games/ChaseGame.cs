using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Steady Hand's restless cousin: the ball never stops, so holding still is no help. It
    /// glides along a path taken from the seed, so everyone chases the same ball, and the
    /// ranking is how close to its centre you stayed on average, not just whether you held on.
    /// The ball waits in the middle until you're on it, so nobody is out for a slow start.
    /// </summary>
    public class ChaseGame : MinigameView
    {
        /// <summary>You have this long to get on the ball.</summary>
        private const float GetOnBy = 3f;
        /// <summary>A beat between getting on and the ball setting off.</summary>
        private const float Beat = 0.3f;
        /// <summary>It eases into its path over this long instead of lurching off.</summary>
        private const float EaseIn = 0.8f;
        /// <summary>For someone watching, the ball sets off after this long.</summary>
        private const float WatchStart = 1f;

        private RectTransform _ball;
        private Image _ballImage;
        private TMP_Text _label;
        private float _radius;
        private float _hold;
        private Vector2 _amp;
        private Vector2 _freq;
        private Vector2 _sign;
        private bool _on;
        /// <summary>When the ball set off; negative until then.</summary>
        private float _startAt = -1f;
        private float _distSum;
        private float _timeSum;

        protected override void Build()
        {
            var size = AreaSize;
            float f; // radians per second; the path's speed
            switch (Level)
            {
                case 1: _radius = 75f; f = 0.28f; _hold = 5f; break;
                case 2: _radius = 62f; f = 0.36f; _hold = 5f; break;
                case 3: _radius = 52f; f = 0.44f; _hold = 6f; break;
                case 4: _radius = 46f; f = 0.52f; _hold = 6f; break;
                default:
                    // Tracking lags the ball by a tenth of a second or so; past this speed that
                    // lag alone is wider than the ball, and nobody could hold on.
                    _radius = Mathf.Max(34f, 42f - (Level - 5) * 2f);
                    f = Mathf.Min(0.7f, 0.56f + (Level - 5) * 0.03f);
                    _hold = 7f;
                    break;
            }

            _amp = new Vector2(Mathf.Min(420f, size.x * 0.5f - _radius - 40f),
                               Mathf.Max(60f, size.y * 0.5f - _radius - 70f));
            _freq = new Vector2(RandomRange(f, f * 1.5f), RandomRange(f, f * 1.5f));
            _sign = new Vector2(RandomRange(0, 2) == 0 ? -1f : 1f, RandomRange(0, 2) == 0 ? -1f : 1f);

            // Pale until you're on it; someone watching just sees the ball.
            _ballImage = UiKit.Dot(Area, "Ball", _radius * 2f, Vector2.zero, Interactive ? Palette.AccentDim : Palette.Accent);
            _ball = (RectTransform)_ballImage.transform;
            _label = UiKit.Label(Area, "Hint", "GET ON THE BALL", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        /// <summary>Where the ball is, `moving` seconds after it set off: a Lissajous path from the centre, eased in.</summary>
        private Vector2 BallAt(float moving)
        {
            float ease = Mathf.Min(1f, moving / EaseIn);
            return new Vector2(_sign.x * _amp.x * ease * Mathf.Sin(_freq.x * moving),
                               _sign.y * _amp.y * ease * Mathf.Sin(_freq.y * moving));
        }

        protected override void OnTick(float dt)
        {
            if (!Interactive && _startAt < 0f) _startAt = WatchStart;
            float moving = _startAt < 0f ? 0f : Mathf.Max(0f, Elapsed - _startAt);
            var c = BallAt(moving);
            if (_ball != null) _ball.anchoredPosition = c;

            if (!CanAct)
            {
                if (_startAt >= 0f && moving >= _hold) Finish(false, 0);
                return;
            }
            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var local)) return;
            float d = Vector2.Distance(local, c);

            if (!_on)
            {
                if (d <= _radius)
                {
                    _on = true;
                    _startAt = Elapsed + Beat;
                    _ballImage.color = Palette.Accent;
                    _label.text = "STAY ON IT";
                }
                else if (Elapsed > GetOnBy) { Fail("NEVER GOT ON"); return; }
                else return;
            }

            if (d > _radius) { Fail("SLIPPED OFF"); return; }
            if (moving > 0f)
            {
                _distSum += d * dt;
                _timeSum += dt;
            }

            if (moving >= _hold)
            {
                float avg = _timeSum > 0f ? _distSum / _timeSum : d;
                _label.text = "STUCK TO IT";
                _label.color = Palette.Green;
                // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(avg * 10f));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_ballImage != null) _ballImage.color = Palette.Red;
            Finish(true, WorstMetric);
        }
    }
}
