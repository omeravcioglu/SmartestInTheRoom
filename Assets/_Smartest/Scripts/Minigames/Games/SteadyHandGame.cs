using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The opposite of every other level: doing nothing, precisely, for five seconds.
    /// Ranked by the closest the cursor ever came to the edge, so nerve beats luck.
    /// </summary>
    public class SteadyHandGame : MinigameView
    {
        /// <summary>Right after entering the circle the margin is ~0 by definition; don't rank on that.</summary>
        private const float SettleSeconds = 0.15f;

        private RectTransform _circle;
        private Image _circleImage;
        private TMP_Text _label;
        private Vector2 _centre;
        private Vector2 _jumpFrom;
        private Vector2 _drift;
        private Vector2[] _jumps = new Vector2[0];
        private float _radius;
        private float _startRadius;
        private float _hold = 5f;
        private float _jumpEvery;
        private float _jumpGrace;
        private float _nextJump;
        private int _jumpIndex;
        private float _graceUntil = 1f;
        private float _settledAt;
        private float _closest = float.MaxValue;
        private bool _inside;
        private Vector2 _demoAt; // the demo's mouse

        /// <summary>Bigger margin is better, so the worst result is nothing at all.</summary>
        public override int WorstMetric => 0;

        protected override void Build()
        {
            var size = AreaSize;
            switch (Level)
            {
                case 1: _startRadius = 110f; _drift = Vector2.zero; _jumpEvery = 0f; break;
                case 2: _startRadius = 80f; _drift = new Vector2(50f, 30f); _jumpEvery = 0f; break;
                case 3: _startRadius = 60f; _drift = new Vector2(90f, 55f); _jumpEvery = 0f; break;
                case 4: _startRadius = 50f; _drift = new Vector2(70f, 45f); _jumpEvery = 0f; break;
                default:
                    // From level five the circle jumps and you chase it. Each jump comes with a
                    // moment to get back in; without one the cursor is simply left outside and
                    // the level can't be cleared at all.
                    _startRadius = Mathf.Max(30f, 40f - (Level - 5) * 3f);
                    _drift = Vector2.zero;
                    _jumpEvery = Mathf.Max(0.8f, 1.2f - (Level - 5) * 0.05f);
                    _jumpGrace = Mathf.Max(0.4f, 0.6f - (Level - 5) * 0.025f);
                    break;
            }
            _radius = _startRadius;
            _centre = Vector2.zero;
            _jumpFrom = Vector2.zero;
            _nextJump = _jumpEvery;

            // Every jump is decided here, from the level's seed, so every player chases the
            // same circle no matter what their frame rate did.
            if (_jumpEvery > 0f)
            {
                float halfW = Mathf.Max(40f, size.x * 0.5f - _radius - 30f);
                float halfH = Mathf.Max(40f, size.y * 0.5f - _radius - 50f);
                _jumps = new Vector2[Mathf.CeilToInt(_hold / _jumpEvery) + 1];
                for (int i = 0; i < _jumps.Length; i++)
                    _jumps[i] = new Vector2(RandomRange(-halfW, halfW), RandomRange(-halfH, halfH));
            }

            _circleImage = UiKit.Dot(Area, "Zone", _radius * 2f, _centre, Palette.Accent);
            _circle = (RectTransform)_circleImage.transform;
            _label = UiKit.Label(Area, "Hint", "KEEP THE CURSOR INSIDE", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));

            // The demo's hand starts outside, low on the right, and has the first second to get in.
            _demoAt = new Vector2(260f, -150f);
        }

        private Vector2 CentreNow()
        {
            if (_drift != Vector2.zero) return _jumpFrom + _drift * Mathf.Sin(Elapsed * 1.1f);
            return _jumpFrom;
        }

        protected override void OnTick(float dt)
        {
            // No jump so late that there'd be no time left to get back in before the end.
            if (_jumpEvery > 0f && Elapsed >= _nextJump && _nextJump < _hold - _jumpGrace && _jumpIndex < _jumps.Length)
            {
                _nextJump += _jumpEvery;
                _jumpFrom = _jumps[_jumpIndex++];
                _inside = false;
                _graceUntil = Elapsed + _jumpGrace;
            }

            // Level four shrinks the circle under you rather than moving it.
            if (Level == 4) _radius = Mathf.Lerp(_startRadius, _startRadius * 0.55f, Elapsed / _hold);

            var c = CentreNow();
            if (_circle != null)
            {
                _circle.anchoredPosition = c;
                _circle.sizeDelta = new Vector2(_radius * 2f, _radius * 2f);
            }

            Vector2 local;
            if (Demo) local = DemoMouse(c, dt);
            else if (!CanAct)
            {
                if (Elapsed >= _hold) Finish(false, 0);
                return;
            }
            else if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out local)) return;
            float margin = _radius - Vector2.Distance(local, c);

            // Grace periods: the first second (the cursor may be anywhere when GO lands) and
            // the moment after every jump.
            if (!_inside)
            {
                if (margin > 0f) { _inside = true; _settledAt = Elapsed + SettleSeconds; }
                else if (Elapsed > _graceUntil) { Fail("GET IN THE CIRCLE"); return; }
                else { _circleImage.color = Palette.AccentDim; return; }
            }

            _circleImage.color = Palette.Accent;
            if (Elapsed >= _settledAt && margin < _closest) _closest = margin;

            if (margin <= 0f) { Fail("OUT"); return; }

            if (Elapsed >= _hold)
            {
                _label.text = "STEADY";
                _label.color = Palette.Green;
                float closest = _closest == float.MaxValue ? margin : _closest;
                // Tenths of a pixel: whole pixels tie far too often, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(Mathf.Max(0f, closest) * 10f));
            }
        }

        /// <summary>
        /// The rule card's demo plays it as a player: a beat, into the circle (after it, if it
        /// moves), then as still as a real hand manages, which is a few pixels of tremor.
        /// </summary>
        private Vector2 DemoMouse(Vector2 c, float dt)
        {
            var rest = c + new Vector2(12f + 3f * Mathf.Sin(Elapsed * 2.3f), -8f + 3f * Mathf.Sin(Elapsed * 1.7f + 1f));
            if (Elapsed >= 0.3f) _demoAt = Vector2.MoveTowards(_demoAt, rest, 500f * dt);
            PointAt(_demoAt);
            return _demoAt;
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_circleImage != null) _circleImage.color = Palette.Red;
            Finish(true, 0);
        }
    }
}
