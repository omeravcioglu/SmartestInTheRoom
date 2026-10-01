using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Crazy golf. Press on the ball, pull back to aim and set the power, let go. It rolls,
    /// slows, banks off walls and bumpers, and from level four a bar sweeps round the middle
    /// like a windmill. Too fast and it skips over the hole. Sink it within the strokes.
    /// Ranked by strokes, then time.
    /// </summary>
    public class PuttGame : MinigameView
    {
        private const float PhysicsStep = 1f / 240f;
        private const float BallR = 12f;
        private const float HoleR = 19f;
        private const float Friction = 420f;
        private const float MaxPull = 220f;
        private const float Power = 6.5f;
        private const float SinkSpeed = 650f;
        private const float Limit = 16f;
        private const float BarLen = 190f;
        private const float BarThick = 16f;

        private readonly List<Rect> _bumpers = new List<Rect>();
        private Rect _green;
        private Vector2 _hole;
        private Vector2 _p, _v;
        private bool _aiming, _rolling;
        private int _strokes, _maxStrokes;
        private Vector2 _barPivot;
        private float _barSpeed;     // degrees per second; 0 = no bar
        private RectTransform _bar;
        private RectTransform _ball;
        private Image _ballImage;
        private RectTransform _aim;
        private TMP_Text _label;
        private float _acc;
        private float _nextAuto = 1f;
        private float _demoFrom = -1f; // the demo: when it went for the ball

        protected override void Build()
        {
            var size = AreaSize;
            int bumpers;
            switch (Level)
            {
                case 1: bumpers = 0; _barSpeed = 0f; _maxStrokes = 3; break;
                case 2: bumpers = 1; _barSpeed = 0f; _maxStrokes = 3; break;
                case 3: bumpers = 2; _barSpeed = 0f; _maxStrokes = 3; break;
                case 4: bumpers = 1; _barSpeed = 70f; _maxStrokes = 3; break;
                default: bumpers = 2; _barSpeed = Mathf.Min(140f, 90f + (Level - 5) * 8f); _maxStrokes = 3; break;
            }

            float w = Mathf.Min(1060f, size.x - 140f), h = size.y - 100f;
            var centre = new Vector2(0f, 20f);
            UiKit.Box(Area, "Green", new Vector2(w, h), centre, Color.Lerp(Palette.PaperHi, Palette.Green, 0.22f));
            _green = new Rect(centre.x - w * 0.5f + 5f, centre.y - h * 0.5f + 5f, w - 10f, h - 10f);

            // Tee on the left, hole on the right, at heights from the seed.
            _p = new Vector2(_green.xMin + 70f, RandomRange(_green.yMin + 60f, _green.yMax - 60f));
            _hole = new Vector2(_green.xMax - 90f, RandomRange(_green.yMin + 60f, _green.yMax - 60f));

            // Bumpers stand between, each leaving a way round above or below.
            for (int i = 0; i < bumpers; i++)
            {
                float x = Mathf.Lerp(_green.xMin, _green.xMax, bumpers == 1 ? 0.5f : 0.36f + 0.3f * i);
                float gapAbove = RandomRange(0f, 1f) < 0.5f ? 1f : 0f;
                float len = _green.height * 0.62f;
                float y0 = gapAbove > 0f ? _green.yMin : _green.yMax - len;
                var r = new Rect(x - 12f, y0, 24f, len);
                _bumpers.Add(r);
                UiKit.Fill(Area, "Bumper" + i, r.size, r.center, Palette.Ink);
            }
            if (_barSpeed > 0f)
            {
                _barPivot = new Vector2(Mathf.Lerp(_green.xMin, _green.xMax, bumpers >= 2 ? 0.52f : 0.66f), _green.center.y);
                _bar = UiKit.Line(Area, "Windmill", _barPivot, _barPivot + Vector2.right, BarThick, Palette.Red);
                UiKit.Dot(Area, "Hub", 22f, _barPivot, Palette.Ink);
            }

            UiKit.Dot(Area, "Hole", HoleR * 2f, _hole, Palette.Ink);
            UiKit.Line(Area, "Pin", _hole, _hole + new Vector2(0f, 70f), 4f, Palette.Ink);
            UiKit.Art(Area, "Flag", "pennant", new Vector2(34f, 22f), _hole + new Vector2(17f, 59f));
            _aim = UiKit.Line(Area, "Aim", _p, _p + Vector2.right, 5f, Palette.Ink2);
            _aim.gameObject.SetActive(false);
            _ballImage = UiKit.Dot(Area, "Ball", BallR * 2f, _p, Palette.PaperHi);
            _ball = (RectTransform)_ballImage.transform;

            Tries("STROKES", _maxStrokes, _maxStrokes);
            _label = UiKit.Label(Area, "Hint", "PULL BACK FROM THE BALL", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            PlaceBar();
        }

        private float BarAngle => _barSpeed * Elapsed;

        private void PlaceBar()
        {
            if (_bar == null) return;
            float a = BarAngle * Mathf.Deg2Rad;
            var half = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * BarLen * 0.5f;
            UiKit.SetLine(_bar, _barPivot - half, _barPivot + half);
        }

        protected override void OnTick(float dt)
        {
            PlaceBar();
            if (!CanAct && Interactive) return;
            if (CanAct && Elapsed > Limit) { Fail("TOO SLOW"); return; }
            if (!_rolling)
            {
                if (Interactive) Aim();
                else if (Demo) DemoPutt();
                else AutoPutt();
                return;
            }
            Roll(dt);
        }

        /// <summary>For someone watching: straight at the cup, just hard enough to get there.</summary>
        private void AutoPutt()
        {
            if (Elapsed < _nextAuto || _strokes >= _maxStrokes) return;
            var to = _hole - _p;
            float d = to.magnitude;
            // Rolling to a stop takes v² = 2 · friction · distance; a little extra so it drops.
            _v = to / Mathf.Max(1f, d) * Mathf.Min(MaxPull * Power, Mathf.Sqrt(2f * Friction * (d + 40f)));
            _rolling = true;
            _strokes++;
            Tries("STROKES", _maxStrokes - _strokes, _maxStrokes);
        }

        /// <summary>
        /// The rule card's demo makes that putt the way a player does: on to the ball, press, draw
        /// back away from the hole for half a second while the aim line shows it, let go.
        /// </summary>
        private void DemoPutt()
        {
            if (_strokes >= _maxStrokes) return;
            const float press = 0.3f, pulled = 0.6f, letGo = 0.85f;
            if (_demoFrom < 0f) _demoFrom = Elapsed + (_strokes == 0 ? 0.4f : 0f);
            float t = Elapsed - _demoFrom;
            if (t < press)
            {
                // A look at the green first, then on to the ball.
                PointAt(t < 0f ? _p + new Vector2(160f, 0f) : _p);
                return;
            }
            var to = _hole - _p;
            float d = to.magnitude;
            float speed = Mathf.Min(MaxPull * Power, Mathf.Sqrt(2f * Friction * (d + 40f)));
            var pull = to / Mathf.Max(1f, d) * (speed / Power) * Mathf.SmoothStep(0f, 1f, (t - press) / (pulled - press));
            UiKit.SetLine(_aim, _p, _p + pull * 0.9f);
            _aim.gameObject.SetActive(pull.magnitude > 10f);
            HoldAt(_p - pull, t < letGo);
            if (t < letGo) return;

            // Let go.
            _aim.gameObject.SetActive(false);
            _v = pull * Power;
            _rolling = true;
            _strokes++;
            Tries("STROKES", _maxStrokes - _strokes, _maxStrokes);
            _demoFrom = -1f;
        }

        private void Aim()
        {
            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m)) return;
            if (!_aiming)
            {
                if (KeyInput.MousePressed() && Vector2.Distance(m, _p) <= 60f) _aiming = true;
                return;
            }
            var pull = Vector2.ClampMagnitude(_p - m, MaxPull);
            // The aim line points where it'll go; its length is the power.
            UiKit.SetLine(_aim, _p, _p + pull * 0.9f);
            _aim.gameObject.SetActive(pull.magnitude > 10f);
            if (KeyInput.MouseHeld()) return;

            _aiming = false;
            _aim.gameObject.SetActive(false);
            if (pull.magnitude < 12f) return;
            _v = pull * Power;
            _rolling = true;
            _strokes++;
            Tries("STROKES", _maxStrokes - _strokes, _maxStrokes);
        }

        private void Roll(float dt)
        {
            bool sunk = false;
            _acc += dt;
            while (_acc >= PhysicsStep && !sunk)
            {
                _acc -= PhysicsStep;
                float speed = _v.magnitude;
                float slowed = Mathf.Max(0f, speed - Friction * PhysicsStep);
                _v = speed > 0.001f ? _v * (slowed / speed) : Vector2.zero;
                _p += _v * PhysicsStep;

                // The walls round the green.
                if (_p.x < _green.xMin + BallR) { _p.x = _green.xMin + BallR; _v.x = Mathf.Abs(_v.x) * 0.8f; }
                if (_p.x > _green.xMax - BallR) { _p.x = _green.xMax - BallR; _v.x = -Mathf.Abs(_v.x) * 0.8f; }
                if (_p.y < _green.yMin + BallR) { _p.y = _green.yMin + BallR; _v.y = Mathf.Abs(_v.y) * 0.8f; }
                if (_p.y > _green.yMax - BallR) { _p.y = _green.yMax - BallR; _v.y = -Mathf.Abs(_v.y) * 0.8f; }
                foreach (var b in _bumpers) BounceOffRect(b);
                if (_bar != null) BounceOffBar();

                // In the cup, unless it's going too fast to drop.
                if (Vector2.Distance(_p, _hole) < HoleR - 3f)
                {
                    if (_v.magnitude < SinkSpeed) sunk = true;
                    else _v = Quaternion.Euler(0f, 0f, RandomRange(-8f, 8f)) * _v * 0.85f; // lips out
                }
            }
            _ball.anchoredPosition = _p;

            if (sunk)
            {
                _ball.anchoredPosition = _hole;
                _ballImage.color = Palette.Green;
                _label.text = _strokes == 1 ? "HOLE IN ONE" : "IN THE CUP";
                _label.color = Palette.Green;
                // Strokes first, then time: two good putts beat three quick ones.
                Finish(false, _strokes * 100000 + Mathf.Min(99999, Ms(Elapsed)));
                return;
            }
            if (_v.magnitude < 6f)
            {
                _v = Vector2.zero;
                _rolling = false;
                _nextAuto = Elapsed + 0.8f;
                if (_strokes >= _maxStrokes && CanAct) { Fail("OUT OF STROKES"); return; }
            }
        }

        private void BounceOffRect(Rect r)
        {
            var closest = new Vector2(Mathf.Clamp(_p.x, r.xMin, r.xMax), Mathf.Clamp(_p.y, r.yMin, r.yMax));
            var n = _p - closest;
            float d = n.magnitude;
            if (d >= BallR) return;
            if (d < 0.001f) n = Vector2.up; else n /= d;
            _p = closest + n * BallR;
            float into = Vector2.Dot(_v, n);
            if (into < 0f) _v -= 1.8f * into * n;
        }

        private void BounceOffBar()
        {
            float a = BarAngle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float along = Mathf.Clamp(Vector2.Dot(_p - _barPivot, dir), -BarLen * 0.5f, BarLen * 0.5f);
            var closest = _barPivot + dir * along;
            var n = _p - closest;
            float d = n.magnitude;
            if (d >= BallR + BarThick * 0.5f) return;
            if (d < 0.001f) n = new Vector2(-dir.y, dir.x); else n /= d;
            _p = closest + n * (BallR + BarThick * 0.5f);
            // The bar is moving: bounce off it, and take some of its sweep.
            var barVel = new Vector2(-dir.y, dir.x) * (_barSpeed * Mathf.Deg2Rad * along);
            var rel = _v - barVel;
            float into = Vector2.Dot(rel, n);
            if (into < 0f) _v = barVel + rel - 1.8f * into * n;
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
