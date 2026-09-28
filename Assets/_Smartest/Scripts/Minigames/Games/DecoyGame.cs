using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Chase with company. Your ball glides along its path while identical balls swing across
    /// it, passing right over it now and then; when they part, stay with the one that kept
    /// going your ball's way. On the warm-up levels your ball keeps a mark; after that you
    /// have only your eyes. Ranked by average distance from your ball's centre.
    /// </summary>
    public class DecoyGame : MinigameView
    {
        private const float GetOnBy = 3f;
        private const float Beat = 0.3f;
        private const float EaseIn = 0.8f;
        private const float WatchStart = 1f;

        private RectTransform _ball;
        private Image _ballImage;
        private RectTransform _mark;
        private RectTransform[] _decoys = new RectTransform[0];
        private Vector2[] _decoyDir = new Vector2[0];
        private float[] _decoyAmp = new float[0];
        private float[] _decoyFreq = new float[0];
        private TMP_Text _label;
        private float _radius;
        private float _hold;
        private bool _keepMark;
        private Vector2 _amp, _freq, _sign, _half;
        private bool _on;
        private float _startAt = -1f;
        private float _distSum, _timeSum;

        protected override void Build()
        {
            var size = AreaSize;
            float f;
            int decoys;
            switch (Level)
            {
                case 1: _radius = 60f; f = 0.26f; decoys = 1; _hold = 6f; break;
                case 2: _radius = 54f; f = 0.32f; decoys = 1; _hold = 6f; break;
                case 3: _radius = 48f; f = 0.36f; decoys = 2; _hold = 6f; break;
                case 4: _radius = 44f; f = 0.4f; decoys = 2; _hold = 7f; break;
                default:
                    _radius = Mathf.Max(34f, 40f - (Level - 5) * 1.5f);
                    f = Mathf.Min(0.6f, 0.44f + (Level - 5) * 0.03f);
                    decoys = Mathf.Min(4, 3 + (Level - 5) / 3);
                    _hold = 7f;
                    break;
            }
            _keepMark = Level <= 2;
            _half = new Vector2(size.x * 0.5f - _radius - 20f, size.y * 0.5f - _radius - 60f);
            _amp = new Vector2(Mathf.Min(380f, _half.x - 40f), Mathf.Max(50f, _half.y - 40f));
            _freq = new Vector2(RandomRange(f, f * 1.4f), RandomRange(f, f * 1.4f));
            _sign = new Vector2(RandomRange(0, 2) == 0 ? -1f : 1f, RandomRange(0, 2) == 0 ? -1f : 1f);

            _ballImage = UiKit.Dot(Area, "Ball", _radius * 2f, Vector2.zero, Palette.Accent);
            _ball = (RectTransform)_ballImage.transform;
            _mark = (RectTransform)UiKit.Dot(_ball, "Mark", 14f, Vector2.zero, Palette.Ink).transform;

            // Drawn after (over) your ball, so when one passes over it you really can't tell.
            _decoys = new RectTransform[decoys];
            _decoyDir = new Vector2[decoys];
            _decoyAmp = new float[decoys];
            _decoyFreq = new float[decoys];
            for (int i = 0; i < decoys; i++)
            {
                float a = RandomRange(0f, Mathf.PI * 2f);
                _decoyDir[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                _decoyAmp[i] = RandomRange(150f, 240f);
                _decoyFreq[i] = RandomRange(0.9f, 1.5f) * (1f + 0.05f * (Level - 1));
                _decoys[i] = (RectTransform)UiKit.Dot(Area, "Decoy" + i, _radius * 2f, Vector2.zero, Palette.Accent).transform;
            }

            _label = UiKit.Label(Area, "Hint", "GET ON THE MARKED BALL", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            PlaceAll(0f);
        }

        private Vector2 BallAt(float moving)
        {
            float ease = Mathf.Min(1f, moving / EaseIn);
            return new Vector2(_sign.x * _amp.x * ease * Mathf.Sin(_freq.x * moving),
                               _sign.y * _amp.y * ease * Mathf.Sin(_freq.y * moving));
        }

        /// <summary>A decoy swings across your ball's path, right over it whenever the cosine is zero.</summary>
        private Vector2 DecoyAt(int i, Vector2 ball, float moving)
        {
            var p = ball + _decoyDir[i] * (_decoyAmp[i] * Mathf.Cos(_decoyFreq[i] * moving));
            return new Vector2(Mathf.Clamp(p.x, -_half.x, _half.x), Mathf.Clamp(p.y, -_half.y, _half.y));
        }

        private Vector2 PlaceAll(float moving)
        {
            var c = BallAt(moving);
            _ball.anchoredPosition = c;
            for (int i = 0; i < _decoys.Length; i++) _decoys[i].anchoredPosition = DecoyAt(i, c, moving);
            return c;
        }

        protected override void OnTick(float dt)
        {
            if (!Interactive && _startAt < 0f) _startAt = WatchStart;
            float moving = _startAt < 0f ? 0f : Mathf.Max(0f, Elapsed - _startAt);
            var c = PlaceAll(moving);
            if (!_keepMark && moving > 0f && _mark.gameObject.activeSelf) _mark.gameObject.SetActive(false);

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
                    _label.text = "STAY ON YOURS";
                }
                else if (Elapsed > GetOnBy) { Fail("NEVER GOT ON"); return; }
                return;
            }

            if (d > _radius)
            {
                bool onDecoy = false;
                for (int i = 0; i < _decoys.Length; i++)
                    if (Vector2.Distance(local, _decoys[i].anchoredPosition) <= _radius) onDecoy = true;
                Fail(onDecoy ? "WRONG BALL" : "SLIPPED OFF");
                return;
            }
            if (moving > 0f)
            {
                _distSum += d * dt;
                _timeSum += dt;
            }
            if (moving >= _hold)
            {
                _label.text = "STAYED WITH IT";
                _label.color = Palette.Green;
                // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(_distSum / Mathf.Max(0.001f, _timeSum) * 10f));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_ballImage != null) _ballImage.color = Palette.Red;
            _mark.gameObject.SetActive(true); // show which one it was
            Finish(true, WorstMetric);
        }
    }
}
