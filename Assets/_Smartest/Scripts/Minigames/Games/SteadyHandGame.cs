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
        private RectTransform _circle;
        private Image _circleImage;
        private TMP_Text _label;
        private Vector2 _centre;
        private Vector2 _jumpFrom;
        private Vector2 _drift;
        private float _radius;
        private float _startRadius;
        private float _hold = 5f;
        private float _jumpEvery;
        private float _nextJump;
        private float _closest = float.MaxValue;
        private bool _inside;

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
                    _startRadius = Mathf.Max(30f, 40f - (Level - 5) * 3f);
                    _drift = Vector2.zero;
                    _jumpEvery = Mathf.Max(0.5f, 0.9f - (Level - 5) * 0.05f);
                    break;
            }
            _radius = _startRadius;
            _centre = Vector2.zero;
            _jumpFrom = Vector2.zero;
            _nextJump = _jumpEvery;

            _circleImage = UiKit.Dot(Area, "Zone", _radius * 2f, _centre, Palette.Accent);
            _circle = (RectTransform)_circleImage.transform;
            _label = UiKit.Label(Area, "Hint", "KEEP THE CURSOR INSIDE", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        private Vector2 CentreNow()
        {
            if (_drift != Vector2.zero) return _jumpFrom + _drift * Mathf.Sin(Elapsed * 1.1f);
            return _jumpFrom;
        }

        protected override void OnTick(float dt)
        {
            var size = AreaSize;

            if (_jumpEvery > 0f && Elapsed >= _nextJump)
            {
                _nextJump += _jumpEvery;
                float halfW = Mathf.Max(40f, size.x * 0.5f - _radius - 30f);
                float halfH = Mathf.Max(40f, size.y * 0.5f - _radius - 50f);
                _jumpFrom = new Vector2(RandomRange(-halfW, halfW), RandomRange(-halfH, halfH));
            }

            // Level four shrinks the circle under you rather than moving it.
            if (Level == 4) _radius = Mathf.Lerp(_startRadius, _startRadius * 0.55f, Elapsed / _hold);

            var c = CentreNow();
            if (_circle != null)
            {
                _circle.anchoredPosition = c;
                _circle.sizeDelta = new Vector2(_radius * 2f, _radius * 2f);
            }

            if (!CanAct)
            {
                if (Elapsed >= _hold) Finish(false, 0);
                return;
            }

            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var local)) return;
            float margin = _radius - Vector2.Distance(local, c);

            // The first moment is a grace period: the cursor may be anywhere when GO lands.
            if (!_inside)
            {
                if (margin > 0f) _inside = true;
                else if (Elapsed > 1f) { Fail("GET IN THE CIRCLE"); return; }
                else { _circleImage.color = Palette.AccentDim; return; }
            }

            _circleImage.color = Palette.Accent;
            if (margin < _closest) _closest = margin;

            if (margin <= 0f) { Fail("OUT"); return; }

            if (Elapsed >= _hold)
            {
                _label.text = "STEADY";
                _label.color = Palette.Green;
                Finish(false, Mathf.RoundToInt(Mathf.Max(0f, _closest)));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_circleImage != null) _circleImage.color = Palette.Red;
            Finish(true, 0);
        }
    }
}
