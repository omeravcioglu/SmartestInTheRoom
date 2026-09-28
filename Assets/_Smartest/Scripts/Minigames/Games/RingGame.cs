using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Keep the cursor in a ring that breathes: it swells and shrinks, and later drifts too,
    /// so you move in and out rather than chase. Anywhere round the ring will do. Ranked by
    /// how close to the middle of the band you stayed on average.
    /// </summary>
    public class RingGame : MinigameView
    {
        private const float GetInBy = 3f;
        private const float WatchStart = 1f;

        private RectTransform _outer;
        private RectTransform _inner;
        private Image _outerImage;
        private TMP_Text _label;
        private float _half;       // half the band's width
        private float _base;
        private float _a1, _a2, _w1, _w2, _p1, _p2;
        private Vector2 _drift, _driftFreq;
        private float _hold;
        private float _startAt = -1f;
        private float _distSum, _timeSum;

        protected override void Build()
        {
            var size = AreaSize;
            float f;
            switch (Level)
            {
                case 1: _half = 40f; f = 0.6f; _hold = 6f; break;
                case 2: _half = 34f; f = 0.75f; _hold = 6f; break;
                case 3: _half = 28f; f = 0.9f; _hold = 6f; break;
                case 4: _half = 24f; f = 1.0f; _hold = 7f; break;
                default:
                    _half = Mathf.Max(15f, 20f - (Level - 5));
                    f = Mathf.Min(1.4f, 1.1f + (Level - 5) * 0.05f);
                    _hold = 7f;
                    break;
            }
            float room = size.y * 0.5f - 70f; // how far out the ring may reach
            _drift = Level >= 4 ? new Vector2(RandomRange(80f, 160f), RandomRange(20f, 40f)) : Vector2.zero;
            _driftFreq = new Vector2(RandomRange(0.4f, 0.7f), RandomRange(0.5f, 0.8f));
            float maxR = room - _drift.y - _half;
            float minR = 30f + _half;
            _base = (maxR + minR) * 0.5f;
            float swing = (maxR - minR) * 0.5f;
            _a1 = swing * 0.7f;
            _a2 = swing * 0.3f;
            _w1 = f * RandomRange(0.9f, 1.2f);
            _w2 = f * RandomRange(1.7f, 2.3f);
            _p1 = RandomRange(0f, Mathf.PI * 2f);
            _p2 = RandomRange(0f, Mathf.PI * 2f);

            // Two discs make the ring: gold, with a hole in the stage's own colour on top. They
            // are drawn at the ring's middle size; resizing scales the ink edge a little.
            _outerImage = UiKit.Dot(Area, "Band", 2f * (_base + _half), Vector2.zero, Palette.AccentDim);
            _outer = (RectTransform)_outerImage.transform;
            _inner = (RectTransform)UiKit.Dot(Area, "Hole", 2f * (_base - _half), Vector2.zero, Palette.PanelRaised).transform;
            _label = UiKit.Label(Area, "Hint", "GET IN THE RING", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            Place(0f);
        }

        private float RadiusAt(float t) => _base + _a1 * Mathf.Sin(_w1 * t + _p1) + _a2 * Mathf.Sin(_w2 * t + _p2);

        private Vector2 CentreAt(float t) =>
            new Vector2(0f, 20f) + new Vector2(_drift.x * Mathf.Sin(_driftFreq.x * t), _drift.y * Mathf.Sin(_driftFreq.y * t));

        private (Vector2 c, float r) Place(float t)
        {
            var c = CentreAt(t);
            float r = RadiusAt(t);
            _outer.anchoredPosition = c;
            _outer.sizeDelta = Vector2.one * (2f * (r + _half));
            _inner.anchoredPosition = c;
            _inner.sizeDelta = Vector2.one * (2f * (r - _half));
            return (c, r);
        }

        protected override void OnTick(float dt)
        {
            if (!Interactive && _startAt < 0f) { _startAt = WatchStart; _outerImage.color = Palette.Accent; }
            float t = _startAt < 0f ? 0f : Mathf.Max(0f, Elapsed - _startAt);
            var (c, r) = Place(t);

            if (!CanAct) return;
            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var local)) return;
            float off = Mathf.Abs(Vector2.Distance(local, c) - r);

            if (_startAt < 0f)
            {
                if (off <= _half)
                {
                    _startAt = Elapsed;
                    _outerImage.color = Palette.Accent;
                    _label.text = "STAY IN THE RING";
                }
                else if (Elapsed > GetInBy) Fail("NEVER GOT IN");
                return;
            }

            if (off > _half) { Fail(Vector2.Distance(local, c) < r ? "FELL INSIDE" : "SLIPPED OUT"); return; }
            _distSum += off * dt;
            _timeSum += dt;
            if (t >= _hold)
            {
                _label.text = "IN THE RING";
                _label.color = Palette.Green;
                // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(_distSum / Mathf.Max(0.001f, _timeSum) * 10f));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_outerImage != null) _outerImage.color = Palette.Red;
            Finish(true, WorstMetric);
        }
    }
}
