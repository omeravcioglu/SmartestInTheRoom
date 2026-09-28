using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Keepy-uppy with a mouse. Click the ball and it jumps; hit it off-centre and it heads
    /// the other way, like a real one. The physics runs in fixed steps, so a fast machine
    /// and a slow one throw the same ball. Ranked by how close it came to the floor.
    /// </summary>
    public class KeepyUppyGame : MinigameView
    {
        private const float PhysicsStep = 1f / 240f;
        private const float HitSlack = 12f;

        private RectTransform _ball;
        private Image _ballImage;
        private TMP_Text _label;
        private Vector2 _p;
        private Vector2 _v;
        private float _r;
        private float _g;
        private float _bounce;
        private float _wind;
        private float _hold;
        private float _floorLine;
        private float _ceiling;
        private float _halfW;
        private float _acc;
        private float _lowest = float.MaxValue;

        /// <summary>Bigger clearance is better, so the worst result is none at all.</summary>
        public override int WorstMetric => 0;

        protected override void Build()
        {
            var size = AreaSize;
            switch (Level)
            {
                case 1: _r = 64f; _g = 700f; _bounce = 640f; _hold = 5f; _wind = 0f; break;
                case 2: _r = 56f; _g = 850f; _bounce = 700f; _hold = 6f; _wind = 0f; break;
                case 3: _r = 50f; _g = 1000f; _bounce = 760f; _hold = 6f; _wind = 0f; break;
                case 4: _r = 44f; _g = 1150f; _bounce = 820f; _hold = 7f; _wind = 80f; break;
                default:
                    _r = Mathf.Max(32f, 40f - (Level - 5) * 2f);
                    _g = Mathf.Min(1600f, 1250f + (Level - 5) * 60f);
                    _bounce = Mathf.Min(940f, 860f + (Level - 5) * 15f);
                    _hold = 7f;
                    _wind = 100f + (Level - 5) * 10f;
                    break;
            }
            if (RandomRange(0, 2) == 0) _wind = -_wind;

            _halfW = Mathf.Min(520f, size.x * 0.5f - 40f);
            float floor = -size.y * 0.5f + 70f; // the hint lives below
            _ceiling = size.y * 0.5f - 10f;
            UiKit.Box(Area, "Arena", new Vector2(_halfW * 2f, _ceiling - floor),
                new Vector2(0f, (_ceiling + floor) * 0.5f), Palette.Panel);
            UiKit.Fill(Area, "Floor", new Vector2(_halfW * 2f - 8f, 12f), new Vector2(0f, floor + 10f), Palette.Red);
            _floorLine = floor + 16f;
            _ceiling -= 4f;

            // Tossed up from mid-height, so the first click comes a second or more after GO
            // rather than the instant the ball appears.
            _p = new Vector2(RandomRange(-_halfW * 0.4f, _halfW * 0.4f), 60f);
            _v = new Vector2(0f, Mathf.Sqrt(2f * _g * 90f));
            _ballImage = UiKit.Dot(Area, "Ball", _r * 2f, _p, Palette.Accent);
            _ball = (RectTransform)_ballImage.transform;
            _label = UiKit.Label(Area, "Hint", "CLICK THE BALL", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        protected override void OnTick(float dt)
        {
            if (CanAct && KeyInput.MousePressed() && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m))
            {
                var off = _p - m;
                if (off.magnitude <= _r + HitSlack)
                {
                    _v.y = _bounce;
                    _v.x = Mathf.Clamp(_v.x + off.x / _r * 260f, -420f, 420f);
                }
            }

            bool dropped = false;
            _acc += dt;
            while (_acc >= PhysicsStep)
            {
                _acc -= PhysicsStep;
                _v.y -= _g * PhysicsStep;
                _v.x += _wind * PhysicsStep;
                _p += _v * PhysicsStep;
                if (_p.x < -_halfW + _r) { _p.x = -_halfW + _r; _v.x = Mathf.Abs(_v.x) * 0.8f; }
                if (_p.x > _halfW - _r) { _p.x = _halfW - _r; _v.x = -Mathf.Abs(_v.x) * 0.8f; }
                if (_p.y > _ceiling - _r) { _p.y = _ceiling - _r; _v.y = -Mathf.Abs(_v.y) * 0.5f; }

                float gap = _p.y - _r - _floorLine;
                if (gap <= 0f)
                {
                    // Someone already out just watches it bounce.
                    if (!Interactive) { _p.y = _floorLine + _r; _v.y = _bounce; continue; }
                    _p.y = _floorLine + _r;
                    dropped = true;
                    break;
                }
                if (gap < _lowest) _lowest = gap;
            }
            if (_ball != null) _ball.anchoredPosition = _p;

            if (!CanAct) return;
            if (dropped)
            {
                _ballImage.color = Palette.Red;
                Fail("DROPPED");
                return;
            }
            if (Elapsed >= _hold)
            {
                _label.text = "KEPT IT UP";
                _label.color = Palette.Green;
                float lowest = _lowest == float.MaxValue ? 0f : _lowest;
                // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(Mathf.Max(0f, lowest) * 10f));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, 0);
        }
    }
}
