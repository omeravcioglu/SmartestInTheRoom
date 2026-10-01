using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Keepy Uppy's big brother: two balls, then three, and they bounce off each other, so a
    /// good hit on one can knock the other off course. The second ball joins a beat after the
    /// first, so there's a moment to find the rhythm. Ranked by the closest any ball came to
    /// the floor.
    /// </summary>
    public class JuggleGame : MinigameView
    {
        private const float PhysicsStep = 1f / 240f;
        private const float HitSlack = 12f;
        private const float JoinEvery = 1.1f;

        private RectTransform[] _views = new RectTransform[0];
        private Image[] _images = new Image[0];
        private Vector2[] _p = new Vector2[0];
        private Vector2[] _v = new Vector2[0];
        private bool[] _live = new bool[0];
        private float _r, _g, _bounce, _wind, _hold;
        private float _floorLine, _ceiling, _halfW;
        private float _acc;
        private float _lowest = float.MaxValue;
        private TMP_Text _label;

        public override int WorstMetric => 0;

        protected override void Build()
        {
            var size = AreaSize;
            int balls;
            switch (Level)
            {
                case 1: balls = 2; _r = 54f; _g = 600f; _bounce = 600f; _hold = 6f; _wind = 0f; break;
                case 2: balls = 2; _r = 50f; _g = 700f; _bounce = 640f; _hold = 7f; _wind = 0f; break;
                case 3: balls = 2; _r = 46f; _g = 800f; _bounce = 680f; _hold = 7f; _wind = 60f; break;
                case 4: balls = 3; _r = 46f; _g = 800f; _bounce = 680f; _hold = 7f; _wind = 0f; break;
                default:
                    balls = 3;
                    _r = Mathf.Max(34f, 42f - (Level - 5) * 2f);
                    _g = Mathf.Min(1100f, 900f + (Level - 5) * 40f);
                    _bounce = Mathf.Min(820f, 720f + (Level - 5) * 15f);
                    _hold = 8f;
                    _wind = 70f + (Level - 5) * 8f;
                    break;
            }
            if (RandomRange(0, 2) == 0) _wind = -_wind;

            _halfW = Mathf.Min(540f, size.x * 0.5f - 40f);
            float floor = -size.y * 0.5f + 70f; // the hint lives below
            _ceiling = size.y * 0.5f - 14f;
            UiKit.Box(Area, "Arena", new Vector2(_halfW * 2f, _ceiling + 4f - floor), new Vector2(0f, (_ceiling + 4f + floor) * 0.5f), Palette.Panel);
            UiKit.Fill(Area, "Floor", new Vector2(_halfW * 2f - 8f, 12f), new Vector2(0f, floor + 10f), Palette.Red);
            _floorLine = floor + 16f;

            _views = new RectTransform[balls];
            _images = new Image[balls];
            _p = new Vector2[balls];
            _v = new Vector2[balls];
            _live = new bool[balls];
            float spread = _halfW * 0.8f / balls;
            for (int i = 0; i < balls; i++)
            {
                // Spread across the arena, each tossed up from mid-height when it joins.
                _p[i] = new Vector2((i - (balls - 1) * 0.5f) * spread * 2f + RandomRange(-30f, 30f), 40f);
                _images[i] = UiKit.Dot(Area, "Ball" + i, _r * 2f, _p[i], Palette.Accent);
                _views[i] = (RectTransform)_images[i].transform;
                _views[i].gameObject.SetActive(false);
            }
            _label = UiKit.Label(Area, "Hint", "KEEP THEM ALL UP", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        protected override void OnTick(float dt)
        {
            // Balls join one by one, tossed gently upward.
            for (int i = 0; i < _p.Length; i++)
            {
                if (_live[i] || Elapsed < i * JoinEvery) continue;
                _live[i] = true;
                _v[i] = new Vector2(0f, Mathf.Sqrt(2f * _g * 110f));
                _views[i].gameObject.SetActive(true);
            }

            if (CanAct && KeyInput.MousePressed() && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m))
            {
                // One click, one ball: the nearest under the cursor.
                int best = -1;
                float bestD = float.MaxValue;
                for (int i = 0; i < _p.Length; i++)
                {
                    if (!_live[i]) continue;
                    float d = Vector2.Distance(_p[i], m);
                    if (d <= _r + HitSlack && d < bestD) { best = i; bestD = d; }
                }
                if (best >= 0)
                {
                    var off = _p[best] - m;
                    _v[best].y = _bounce;
                    _v[best].x = Mathf.Clamp(_v[best].x + off.x / _r * 240f, -380f, 380f);
                }
            }

            int dropped = -1;
            _acc += dt;
            while (_acc >= PhysicsStep && dropped < 0)
            {
                _acc -= PhysicsStep;
                for (int i = 0; i < _p.Length; i++)
                {
                    if (!_live[i]) continue;
                    _v[i].y -= _g * PhysicsStep;
                    _v[i].x += _wind * PhysicsStep;
                    _p[i] += _v[i] * PhysicsStep;
                    if (_p[i].x < -_halfW + _r) { _p[i].x = -_halfW + _r; _v[i].x = Mathf.Abs(_v[i].x) * 0.8f; }
                    if (_p[i].x > _halfW - _r) { _p[i].x = _halfW - _r; _v[i].x = -Mathf.Abs(_v[i].x) * 0.8f; }
                    if (_p[i].y > _ceiling - _r) { _p[i].y = _ceiling - _r; _v[i].y = -Mathf.Abs(_v[i].y) * 0.5f; }
                }
                // Balls knock each other about: equal masses, swap the push along the line between them.
                for (int i = 0; i < _p.Length; i++)
                {
                    for (int j = i + 1; j < _p.Length; j++)
                    {
                        if (!_live[i] || !_live[j]) continue;
                        var n = _p[j] - _p[i];
                        float d = n.magnitude;
                        if (d >= 2f * _r || d < 0.001f) continue;
                        n /= d;
                        float overlap = 2f * _r - d;
                        _p[i] -= n * (overlap * 0.5f);
                        _p[j] += n * (overlap * 0.5f);
                        float vi = Vector2.Dot(_v[i], n), vj = Vector2.Dot(_v[j], n);
                        if (vi - vj <= 0f) continue; // already parting
                        _v[i] += n * (vj - vi) * 0.9f;
                        _v[j] += n * (vi - vj) * 0.9f;
                    }
                }
                for (int i = 0; i < _p.Length; i++)
                {
                    if (!_live[i]) continue;
                    float gap = _p[i].y - _r - _floorLine;
                    if (gap <= 0f)
                    {
                        if (!Interactive) { _p[i].y = _floorLine + _r; _v[i].y = _bounce; continue; }
                        _p[i].y = _floorLine + _r;
                        dropped = i;
                        break;
                    }
                    if (gap < _lowest) _lowest = gap;
                }
            }
            for (int i = 0; i < _p.Length; i++) if (_live[i]) _views[i].anchoredPosition = _p[i];

            if (!CanAct) return;
            if (dropped >= 0)
            {
                _images[dropped].color = Palette.Red;
                Fail("DROPPED ONE");
                return;
            }
            Progress("KEEP UP", Elapsed / (_hold + (_p.Length - 1) * JoinEvery));
            if (Elapsed >= _hold + (_p.Length - 1) * JoinEvery)
            {
                _label.text = "KEPT THEM UP";
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
