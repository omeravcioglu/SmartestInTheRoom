using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// A winding road scrolls towards you and your car follows the mouse sideways; keep it on
    /// the road. You can see the bends coming, so it's steering, not reflex. The road waits
    /// until your car is on it. Ranked by how close to the middle you drove on average.
    /// </summary>
    public class RoadGame : MinigameView
    {
        /// <summary>Thin slices: at 24 px a bend read as a staircase.</summary>
        private const float SliceH = 8f;
        /// <summary>Each slice overlaps its neighbours by this much, so their soft edges don't show as seams.</summary>
        private const float Overlap = 3f;
        private const float CarR = 14f;
        private const float GetOnBy = 3f;
        private const float WatchStart = 1f;

        private RectTransform[] _edges = new RectTransform[0];
        private RectTransform[] _lanes = new RectTransform[0];
        private RectTransform[] _dashes = new RectTransform[0];
        private float[] _sliceY = new float[0];
        private RectTransform _car;
        private Image _carImage;
        private TMP_Text _label;
        private float _width;
        private float _scroll;
        private float _carY;
        private float _halfW;
        private float _hold;
        private float _a1, _a2, _w1, _w2, _p1, _p2;
        private float _startAt = -1f;
        private float _distSum, _timeSum;
        private float _demoX; // the demo's mouse, across

        protected override void Build()
        {
            var size = AreaSize;
            float f, reach;
            switch (Level)
            {
                case 1: _width = 200f; _scroll = 220f; f = 0.5f; reach = 0.6f; _hold = 6f; break;
                case 2: _width = 175f; _scroll = 250f; f = 0.6f; reach = 0.7f; _hold = 6f; break;
                case 3: _width = 150f; _scroll = 280f; f = 0.7f; reach = 0.8f; _hold = 6f; break;
                case 4: _width = 130f; _scroll = 310f; f = 0.8f; reach = 0.9f; _hold = 7f; break;
                default:
                    _width = Mathf.Max(84f, 112f - (Level - 5) * 5f);
                    _scroll = Mathf.Min(420f, 330f + (Level - 5) * 15f);
                    f = Mathf.Min(1.1f, 0.85f + (Level - 5) * 0.05f);
                    reach = 1f;
                    _hold = 7f;
                    break;
            }

            float w = Mathf.Min(1080f, size.x - 120f);
            float top = size.y * 0.5f - 10f, bottom = -size.y * 0.5f + 64f; // the hint lives below
            _halfW = w * 0.5f;
            UiKit.Box(Area, "Grass", new Vector2(w, top - bottom), new Vector2(0f, (top + bottom) * 0.5f), Palette.Panel);
            _carY = bottom + 60f;

            float swing = (_halfW - _width * 0.5f - 24f) * reach;
            _a1 = swing * 0.7f;
            _a2 = swing * 0.3f;
            _w1 = f * RandomRange(0.9f, 1.2f);
            _w2 = f * RandomRange(1.7f, 2.3f);
            _p1 = RandomRange(0f, Mathf.PI * 2f);
            _p2 = RandomRange(0f, Mathf.PI * 2f);

            // The road in slices from just below the car to the top, drawn in two passes (all
            // the edges, then all the tarmac) so the slices join into one road.
            int n = Mathf.FloorToInt((top - 8f - (_carY - 40f)) / SliceH);
            _sliceY = new float[n];
            _edges = new RectTransform[n];
            _lanes = new RectTransform[n];
            for (int i = 0; i < n; i++) _sliceY[i] = _carY - 40f + SliceH * (i + 0.5f);
            for (int i = 0; i < n; i++)
                _edges[i] = (RectTransform)UiKit.Fill(Area, "Edge" + i, new Vector2(_width + 10f, SliceH + Overlap), new Vector2(0f, _sliceY[i]), Palette.Ink).transform;
            for (int i = 0; i < n; i++)
                _lanes[i] = (RectTransform)UiKit.Fill(Area, "Road" + i, new Vector2(_width, SliceH + Overlap), new Vector2(0f, _sliceY[i]), Palette.PanelRaised).transform;
            // The centre line, in dashes that run past as you drive.
            _dashes = new RectTransform[n];
            for (int i = 0; i < n; i++)
                _dashes[i] = (RectTransform)UiKit.Fill(Area, "Dash" + i, new Vector2(4f, SliceH + Overlap), new Vector2(0f, _sliceY[i]), Palette.Ink2).transform;

            _carImage = UiKit.Dot(Area, "Car", CarR * 2f, new Vector2(0f, _carY), Palette.Accent);
            _car = (RectTransform)_carImage.transform;
            // Someone already out has no car on the road; the demo drives one.
            if (!Interactive && !Demo) _car.gameObject.SetActive(false);
            _label = UiKit.Label(Area, "Hint", "GET ON THE ROAD", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            Place(0f);
            // The demo's car starts off to one side, so it has to steer onto the road.
            float roadX = RoadX(0f);
            _demoX = roadX + (roadX > 0f ? -230f : 230f);
        }

        private float RoadX(float tau) => _a1 * Mathf.Sin(_w1 * tau + _p1) + _a2 * Mathf.Sin(_w2 * tau + _p2);

        private void Place(float t)
        {
            for (int i = 0; i < _sliceY.Length; i++)
            {
                // Further up the screen is further down the road.
                float x = RoadX(t + (_sliceY[i] - _carY) / _scroll);
                _edges[i].anchoredPosition = new Vector2(x, _sliceY[i]);
                _lanes[i].anchoredPosition = new Vector2(x, _sliceY[i]);
                _dashes[i].anchoredPosition = new Vector2(x, _sliceY[i]);
                bool dash = Mathf.Repeat(_scroll * t + _sliceY[i], 48f) < 26f;
                if (_dashes[i].gameObject.activeSelf != dash) _dashes[i].gameObject.SetActive(dash);
            }
        }

        protected override void OnTick(float dt)
        {
            // The demo waits for its own car to get on, like a player.
            if (!Interactive && !Demo && _startAt < 0f) _startAt = WatchStart;
            float t = _startAt < 0f ? 0f : Mathf.Max(0f, Elapsed - _startAt);
            Place(t);

            Vector2 local;
            if (Demo) local = DemoSteer(t, dt);
            else if (!CanAct || !UiKit.LocalPoint(Area, KeyInput.MousePosition(), out local)) return;
            float carX = Mathf.Clamp(local.x, -_halfW + CarR, _halfW - CarR);
            _car.anchoredPosition = new Vector2(carX, _carY);
            float off = Mathf.Abs(carX - RoadX(t));

            if (_startAt < 0f)
            {
                if (off <= _width * 0.5f)
                {
                    _startAt = Elapsed;
                    _label.text = "STAY ON THE ROAD";
                }
                else if (Elapsed > GetOnBy) Fail("NEVER GOT ON");
                return;
            }

            if (off > _width * 0.5f) { Fail("OFF THE ROAD"); return; }
            Progress("DISTANCE", t / _hold);
            _distSum += off * dt;
            _timeSum += dt;
            if (t >= _hold)
            {
                _label.text = "ARRIVED";
                _label.color = Palette.Green;
                // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(_distSum / Mathf.Max(0.001f, _timeSum) * 10f));
            }
        }

        /// <summary>The rule card's demo: a beat, onto the road, then steering round its bends, the hand on the car.</summary>
        private Vector2 DemoSteer(float t, float dt)
        {
            if (Elapsed >= 0.3f) _demoX = Mathf.MoveTowards(_demoX, RoadX(t) + 6f * Mathf.Sin(Elapsed * 1.4f), 500f * dt);
            var at = new Vector2(_demoX, _carY);
            PointAt(at);
            return at;
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_carImage != null) _carImage.color = Palette.Red;
            Finish(true, WorstMetric);
        }
    }
}
