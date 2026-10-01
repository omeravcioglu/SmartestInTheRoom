using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// A dot that doesn't want to be caught: it runs from the cursor and slides along the
    /// walls, so the way to catch it is to drive it into a corner and click. It tires as the
    /// level goes on, so it can always be caught in the end. It has a worried face, and its
    /// eyes look the way it's running. Ranked by time.
    /// </summary>
    public class RunawayGame : MinigameView
    {
        private const float PhysicsStep = 1f / 240f;
        private const float Limit = 10f;
        private const float FleeRange = 240f;

        private RectTransform _dot;
        private Image _dotImage;
        private RectTransform _eyeL, _eyeR;
        private TMP_Text _label;
        private Rect _box;
        private Vector2 _p;
        private Vector2 _v;
        private float _r;
        private float _maxSpeed;
        private Vector2 _wanderFreq;
        private Vector2 _wanderPhase;
        private float _acc;
        private Vector2 _demoAt;       // the demo's mouse
        private Vector2 _demoCorner;   // the corner the demo drives it into
        private bool _demoClosing;     // it's pinned there: the demo closes in to click
        private float _demoStill;      // how long the demo has been on it

        protected override void Build()
        {
            var size = AreaSize;
            switch (Level)
            {
                case 1: _maxSpeed = 200f; _r = 28f; break;
                case 2: _maxSpeed = 250f; _r = 26f; break;
                case 3: _maxSpeed = 300f; _r = 24f; break;
                case 4: _maxSpeed = 350f; _r = 22f; break;
                default:
                    _maxSpeed = Mathf.Min(540f, 400f + (Level - 5) * 25f);
                    _r = 20f;
                    break;
            }

            float w = Mathf.Min(1000f, size.x - 160f), h = size.y - 100f;
            var centre = new Vector2(0f, 20f);
            UiKit.Box(Area, "Yard", new Vector2(w, h), centre, Palette.PanelRaised);
            _box = new Rect(centre.x - w * 0.5f + _r + 6f, centre.y - h * 0.5f + _r + 6f, w - 2f * (_r + 6f), h - 2f * (_r + 6f));

            _p = new Vector2(RandomRange(_box.xMin + 60f, _box.xMax - 60f), RandomRange(_box.yMin + 40f, _box.yMax - 40f));
            _wanderFreq = new Vector2(RandomRange(0.5f, 0.9f), RandomRange(0.6f, 1.1f));
            _wanderPhase = new Vector2(RandomRange(0f, 6.3f), RandomRange(0f, 6.3f));
            _dotImage = UiKit.Art(Area, "Runner", "fleer", new Vector2(_r * 2f, _r * 2f) * (50f / 44f), _p, Palette.Accent);
            _dot = (RectTransform)_dotImage.transform;
            _eyeL = (RectTransform)UiKit.Dot(_dot, "EyeL", _r * 0.3f, Vector2.zero, Palette.Ink).transform;
            _eyeR = (RectTransform)UiKit.Dot(_dot, "EyeR", _r * 0.3f, Vector2.zero, Palette.Ink).transform;
            _label = UiKit.Label(Area, "Hint", "CORNER IT, CLICK IT", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            // The demo's hand starts in the middle of the yard and drives it into the corner on its side.
            _demoAt = centre;
            _demoCorner = new Vector2(_p.x > centre.x ? _box.xMax : _box.xMin, _p.y > centre.y ? _box.yMax : _box.yMin);
            _demoClosing = false;
            _demoStill = 0f;
        }

        protected override void OnTick(float dt)
        {
            var cursor = Vector2.zero;
            bool haveCursor = CanAct && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out cursor);
            // The rule card's demo herds it with its own cursor, which it runs from just the same.
            if (Demo)
            {
                cursor = DemoHerd(dt);
                haveCursor = true;
            }
            // It tires: full speed at first, under half by the end.
            float speed = _maxSpeed * Mathf.Max(0.45f, 1f - 0.055f * Elapsed);

            _acc += dt;
            while (_acc >= PhysicsStep)
            {
                _acc -= PhysicsStep;
                Vector2 want;
                var away = _p - cursor;
                float d = away.magnitude;
                if (haveCursor && d < FleeRange && d > 0.01f)
                    want = away / d * (speed * Mathf.Sqrt(1f - d / FleeRange));
                else // left alone, it ambles about
                    want = new Vector2(Mathf.Sin(Elapsed * _wanderFreq.x + _wanderPhase.x), Mathf.Cos(Elapsed * _wanderFreq.y + _wanderPhase.y)) * (speed * 0.25f);
                _v = Vector2.MoveTowards(_v, want, 2400f * PhysicsStep);
                _p += _v * PhysicsStep;
                // Walls stop it, and it slides along them.
                if (_p.x < _box.xMin) { _p.x = _box.xMin; _v.x = Mathf.Max(0f, _v.x); }
                if (_p.x > _box.xMax) { _p.x = _box.xMax; _v.x = Mathf.Min(0f, _v.x); }
                if (_p.y < _box.yMin) { _p.y = _box.yMin; _v.y = Mathf.Max(0f, _v.y); }
                if (_p.y > _box.yMax) { _p.y = _box.yMax; _v.y = Mathf.Min(0f, _v.y); }
            }
            _dot.anchoredPosition = _p;
            var look = _v.sqrMagnitude > 100f ? _v.normalized : Vector2.zero;
            _eyeL.anchoredPosition = new Vector2(-_r * 0.32f, _r * 0.09f) + look * (_r * 0.12f);
            _eyeR.anchoredPosition = new Vector2(_r * 0.32f, _r * 0.09f) + look * (_r * 0.12f);

            if (Demo) DemoClick(dt);
            if (!CanAct) return;
            if (KeyInput.MousePressed() && haveCursor && Catch(cursor)) return;
            if (Elapsed > Limit)
            {
                _label.text = "IT GOT AWAY";
                _label.color = Palette.Red;
                Finish(true, WorstMetric);
            }
        }

        /// <summary>A click, the player's or the demo's: on the dot, it's caught.</summary>
        private bool Catch(Vector2 at)
        {
            if (!CanMove || Vector2.Distance(at, _p) > _r + 8f) return false;
            _dotImage.color = Palette.Green;
            _label.text = "GOT IT";
            _label.color = Palette.Green;
            Finish(false, Ms(Elapsed));
            return true;
        }

        /// <summary>
        /// The rule card's demo: a beat, then round behind it so it runs into the corner on its
        /// side of the yard; once it's pinned there, in close from the open side, where the only
        /// way it can run is into the walls.
        /// </summary>
        private Vector2 DemoHerd(float dt)
        {
            if (!_demoClosing && Vector2.Distance(_p, _demoCorner) < 40f) _demoClosing = true;
            if (Elapsed >= 0.3f)
            {
                var into = new Vector2(Mathf.Sign(_demoCorner.x - _box.center.x), Mathf.Sign(_demoCorner.y - _box.center.y)).normalized;
                var aim = _demoClosing ? _p - into * 12f : _p - (_demoCorner - _p).normalized * 110f;
                _demoAt = Vector2.MoveTowards(_demoAt, aim, (_demoClosing ? 450f : 420f) * dt);
            }
            PointAt(_demoAt);
            return _demoAt;
        }

        /// <summary>The demo clicks once it has been on the cornered dot for a moment.</summary>
        private void DemoClick(float dt)
        {
            if (!_demoClosing) return;
            _demoStill = Vector2.Distance(_demoAt, _p) <= 20f ? _demoStill + dt : 0f;
            if (_demoStill < 0.25f) return;
            _demoStill = 0f;
            TapAt(_demoAt);
            Catch(_demoAt);
        }
    }
}
