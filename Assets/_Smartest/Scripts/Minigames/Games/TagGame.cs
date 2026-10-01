using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Playground tag in a box: red dots chase your cursor, quick in a straight line but slow
    /// to turn, so you dodge round them. From level five there are two. They wait until you're
    /// in the box and start from the far corners. Ranked by the closest they got.
    /// </summary>
    public class TagGame : MinigameView
    {
        private const float PhysicsStep = 1f / 240f;
        private const float ChaserR = 22f;
        private const float GetInBy = 3f;
        private const float WatchStart = 1f;

        private RectTransform[] _views = new RectTransform[0];
        private Image[] _images = new Image[0];
        private Vector2[] _p = new Vector2[0];
        private Vector2[] _v = new Vector2[0];
        private Rect _box;
        private TMP_Text _label;
        private float _speed;
        private float _accel;
        private float _hold;
        private float _startAt = -1f;
        private float _acc;
        private float _closest = float.MaxValue;
        private Vector2 _phantomFreq;

        /// <summary>Bigger clearance is better, so the worst result is none at all.</summary>
        public override int WorstMetric => 0;

        protected override void Build()
        {
            var size = AreaSize;
            int chasers;
            switch (Level)
            {
                case 1: _speed = 200f; _accel = 900f; _hold = 6f; chasers = 1; break;
                case 2: _speed = 240f; _accel = 1000f; _hold = 6f; chasers = 1; break;
                case 3: _speed = 280f; _accel = 1100f; _hold = 6f; chasers = 1; break;
                case 4: _speed = 320f; _accel = 1200f; _hold = 7f; chasers = 1; break;
                default:
                    _speed = Mathf.Min(420f, 330f + (Level - 5) * 15f);
                    _accel = 1300f;
                    _hold = 7f;
                    chasers = 2;
                    break;
            }

            float w = Mathf.Min(1000f, size.x - 160f), h = size.y - 100f;
            var centre = new Vector2(0f, 20f);
            UiKit.Box(Area, "Playground", new Vector2(w, h), centre, Palette.PanelRaised);
            _box = new Rect(centre.x - w * 0.5f + 6f, centre.y - h * 0.5f + 6f, w - 12f, h - 12f);

            _views = new RectTransform[chasers];
            _images = new Image[chasers];
            _p = new Vector2[chasers];
            _v = new Vector2[chasers];
            for (int i = 0; i < chasers; i++)
            {
                _p[i] = Corner(i == 0 ? new Vector2(-1f, 1f) : new Vector2(1f, -1f));
                _images[i] = UiKit.Dot(Area, "Chaser" + i, ChaserR * 2f, _p[i], Palette.Red);
                _views[i] = (RectTransform)_images[i].transform;
            }
            _phantomFreq = new Vector2(RandomRange(0.5f, 0.8f), RandomRange(0.7f, 1.1f));
            _label = UiKit.Label(Area, "Hint", "GET IN THE BOX", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        private Vector2 Corner(Vector2 side) =>
            new Vector2(side.x < 0f ? _box.xMin + ChaserR : _box.xMax - ChaserR, side.y < 0f ? _box.yMin + ChaserR : _box.yMax - ChaserR);

        protected override void OnTick(float dt)
        {
            Vector2 target;
            if (!Interactive)
            {
                if (_startAt < 0f) _startAt = WatchStart;
                // Someone watching sees them chase a player who isn't there.
                target = _box.center + new Vector2(_box.width * 0.4f * Mathf.Sin(_phantomFreq.x * Elapsed), _box.height * 0.4f * Mathf.Sin(_phantomFreq.y * Elapsed));
            }
            else if (!CanAct || !UiKit.LocalPoint(Area, KeyInput.MousePosition(), out target)) return;

            if (_startAt < 0f)
            {
                if (!_box.Contains(target)) { if (Elapsed > GetInBy) Fail("NEVER GOT IN"); return; }
                _startAt = Elapsed;
                _label.text = "DON'T GET TAGGED";
                // They start from the corners furthest from you.
                for (int i = 0; i < _p.Length; i++)
                {
                    var side = new Vector2(target.x > _box.center.x ? -1f : 1f, target.y > _box.center.y ? -1f : 1f);
                    if (i == 1) side.y = -side.y;
                    _p[i] = Corner(side);
                }
            }
            if (Elapsed < _startAt) return;
            float t = Elapsed - _startAt;
            float speed = _speed * Mathf.Min(1.4f, 1f + 0.06f * t);

            _acc += dt;
            while (_acc >= PhysicsStep)
            {
                _acc -= PhysicsStep;
                for (int i = 0; i < _p.Length; i++)
                {
                    var to = target - _p[i];
                    var want = to.sqrMagnitude > 1f ? to.normalized * speed : Vector2.zero;
                    // Two chasers keep a little apart rather than stacking into one.
                    for (int j = 0; j < _p.Length; j++)
                    {
                        if (j == i) continue;
                        var apart = _p[i] - _p[j];
                        float d = apart.magnitude;
                        if (d < 120f && d > 0.01f) want += apart / d * (speed * 0.6f * (1f - d / 120f));
                    }
                    _v[i] = Vector2.MoveTowards(_v[i], want, _accel * PhysicsStep);
                    _p[i] += _v[i] * PhysicsStep;
                    _p[i] = new Vector2(Mathf.Clamp(_p[i].x, _box.xMin + ChaserR, _box.xMax - ChaserR),
                                        Mathf.Clamp(_p[i].y, _box.yMin + ChaserR, _box.yMax - ChaserR));
                }
            }
            for (int i = 0; i < _p.Length; i++) _views[i].anchoredPosition = _p[i];
            if (!CanAct) return;

            if (!_box.Contains(target)) { Fail("LEFT THE BOX"); return; }
            Progress("STAY FREE", t / _hold);
            for (int i = 0; i < _p.Length; i++)
            {
                float gap = Vector2.Distance(target, _p[i]) - ChaserR;
                if (gap < _closest) _closest = gap;
                if (gap <= 4f)
                {
                    _images[i].color = Palette.Ink;
                    Fail("TAGGED");
                    return;
                }
            }
            if (t >= _hold)
            {
                _label.text = "NEVER CAUGHT";
                _label.color = Palette.Green;
                // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(Mathf.Max(0f, _closest) * 10f));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, 0);
        }
    }
}
