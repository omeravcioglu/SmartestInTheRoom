using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The only level with real movement. Every player faces exactly the same storm from
    /// the same seed, so surviving it by a wider margin than everyone else means something.
    /// </summary>
    public class DodgeGame : MinigameView
    {
        private struct Block
        {
            public Vector2 From, To;
            public float Start, Duration, Size;
        }

        private readonly List<Block> _blocks = new List<Block>();
        private readonly List<RectTransform> _views = new List<RectTransform>();
        private RectTransform _player;
        private TMP_Text _label;
        private Vector2 _pos;
        private Vector2 _half;
        private float _survive;
        private float _speed = 420f;
        private float _closest = float.MaxValue;
        private const float PlayerRadius = 14f;

        // The rule card's demo holds one of WASD at a time, in this order: none, W, D, S, A.
        private static readonly string[] Keys = { string.Empty, "W", "D", "S", "A" };
        private static readonly Vector2[] Ways = { Vector2.zero, Vector2.up, Vector2.right, Vector2.down, Vector2.left };
        private const float DemoComfort = 40f;
        private int _demoKey;
        private float _demoKeyAt;
        private float _demoTarget;
        private bool _demoDodging;

        /// <summary>Bigger margin is better, so the worst result is a margin of nothing.</summary>
        public override int WorstMetric => 0;

        protected override void Build()
        {
            var size = AreaSize;
            _half = new Vector2(Mathf.Min(760f, size.x - 60f), Mathf.Min(360f, size.y - 80f)) * 0.5f;
            var arena = UiKit.Box(Area, "Arena", _half * 2f, Vector2.zero, Palette.Panel);
            // Blocks come in from past the arena's edge and leave past the other: clipped to it,
            // they don't cross the hint under it on their way out.
            arena.gameObject.AddComponent<RectMask2D>();

            int count;
            float blockSpeed;
            int sides;
            switch (Level)
            {
                case 1: count = 4; blockSpeed = 0.9f; sides = 1; break;
                case 2: count = 6; blockSpeed = 1.0f; sides = 1; break;
                case 3: count = 8; blockSpeed = 1.25f; sides = 1; break;
                case 4: count = 10; blockSpeed = 1.35f; sides = 2; break;
                default: count = Mathf.Min(22, 14 + (Level - 5) * 2); blockSpeed = Mathf.Min(2f, 1.5f + (Level - 5) * 0.1f); sides = 4; break;
            }
            _survive = 8f;

            for (int i = 0; i < count; i++)
            {
                int side = sides == 1 ? 0 : RandomRange(0, sides);
                float s = RandomRange(34f, 58f);
                Vector2 from, to;
                switch (side)
                {
                    case 1: // from below
                        from = new Vector2(RandomRange(-_half.x, _half.x), -_half.y - s);
                        to = new Vector2(RandomRange(-_half.x, _half.x), _half.y + s);
                        break;
                    case 2: // from the left
                        from = new Vector2(-_half.x - s, RandomRange(-_half.y, _half.y));
                        to = new Vector2(_half.x + s, RandomRange(-_half.y, _half.y));
                        break;
                    case 3: // from the right
                        from = new Vector2(_half.x + s, RandomRange(-_half.y, _half.y));
                        to = new Vector2(-_half.x - s, RandomRange(-_half.y, _half.y));
                        break;
                    default: // from above
                        from = new Vector2(RandomRange(-_half.x, _half.x), _half.y + s);
                        to = new Vector2(RandomRange(-_half.x, _half.x), -_half.y - s);
                        break;
                }
                _blocks.Add(new Block
                {
                    From = from,
                    To = to,
                    Start = RandomRange(0f, _survive - 1.2f),
                    Duration = RandomRange(1.4f, 2.4f) / blockSpeed,
                    Size = s
                });
                var rt = (RectTransform)UiKit.Box(arena.transform, "Block" + i, new Vector2(s, s), from, Palette.Red).transform;
                rt.gameObject.SetActive(false);
                _views.Add(rt);
            }

            _pos = Vector2.zero;
            _player = (RectTransform)UiKit.Dot(Area, "You", PlayerRadius * 2f, Vector2.zero, Palette.Accent).transform;
            // Someone already knocked out watches the storm; they have no dot in it. The demo dodges one.
            if (!Interactive && !Demo) _player.gameObject.SetActive(false);
            _label = UiKit.Label(Area, "Hint", "WASD", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -_half.y - 30f));
        }

        protected override void OnTick(float dt)
        {
            if (CanMove)
            {
                var move = CanAct ? KeyInput.MoveAxis() : DemoSteer();
                if (move.sqrMagnitude > 1f) move = move.normalized;
                _pos += move * (_speed * dt);
                _pos.x = Mathf.Clamp(_pos.x, -_half.x + PlayerRadius, _half.x - PlayerRadius);
                _pos.y = Mathf.Clamp(_pos.y, -_half.y + PlayerRadius, _half.y - PlayerRadius);
                _player.anchoredPosition = _pos;
            }

            for (int i = 0; i < _blocks.Count; i++)
            {
                var b = _blocks[i];
                float t = (Elapsed - b.Start) / b.Duration;
                var view = _views[i];
                if (t < 0f || t > 1f) { if (view.gameObject.activeSelf) view.gameObject.SetActive(false); continue; }
                if (!view.gameObject.activeSelf) view.gameObject.SetActive(true);
                Vector2 p = Vector2.LerpUnclamped(b.From, b.To, t);
                view.anchoredPosition = p;
                if (!Interactive && !Demo) continue;

                // Box against circle, kept cheap: the gap between the dot and the square.
                float dx = Mathf.Max(0f, Mathf.Abs(p.x - _pos.x) - b.Size * 0.5f);
                float dy = Mathf.Max(0f, Mathf.Abs(p.y - _pos.y) - b.Size * 0.5f);
                float gap = Mathf.Sqrt(dx * dx + dy * dy) - PlayerRadius;
                if (gap < _closest) _closest = gap;
                if (gap <= 0f)
                {
                    if (_label != null) { _label.text = "HIT"; _label.color = Palette.Red; }
                    if (_player != null) _player.GetComponent<Image>().color = Palette.Red;
                    Finish(true, 0);
                    return;
                }
            }

            if (Elapsed >= _survive)
            {
                if (_label != null && (Interactive || Demo)) { _label.text = "SURVIVED"; _label.color = Palette.Green; }
                int margin = _closest == float.MaxValue ? 999 : Mathf.RoundToInt(Mathf.Max(0f, _closest));
                Finish(false, margin);
            }
        }

        /// <summary>
        /// The rule card's demo plays it like a careful player: it drops to the lower middle (more
        /// time to see what's falling), stands still while nothing it has seen will come close,
        /// steps along the row to a spot that stays clear of anything that will, and drifts back
        /// once the way is clear. One key at a time, so the keys it shows are the moves it makes.
        /// </summary>
        private Vector2 DemoSteer()
        {
            if (Elapsed < 0.4f) return Vector2.zero; // a moment to look before moving
            int want;
            if (_demoDodging || Clearance(_pos) < DemoComfort)
            {
                // Sideways is the natural dodge from things falling. It sticks with the spot it
                // picked (or with standing still) unless another is clearly better.
                if (!_demoDodging) _demoTarget = _pos.x;
                float best = Spot(_demoTarget) + 10f;
                for (float x = -_half.x + PlayerRadius; x <= _half.x - PlayerRadius; x += 30f)
                {
                    float s = Spot(x);
                    if (s > best) { best = s; _demoTarget = x; }
                }
                _demoDodging = true;
                // Within 12 px is there: a frame's move can be 14 px, and a tighter stop would see-saw.
                float go = _demoTarget - _pos.x;
                want = go > 12f ? 2 : go < -12f ? 4 : 0;
                // There and clear: let go, and give it a beat before heading back.
                if (want == 0 && Clearance(_pos) >= DemoComfort) _demoDodging = false;
            }
            else want = _demoKey != 0 || Elapsed - _demoKeyAt >= 0.3f ? HomeKey() : 0;

            if (want != _demoKey)
            {
                _demoKey = want;
                _demoKeyAt = Elapsed;
                if (want != 0) PressKey(Keys[want]);
            }
            return Ways[_demoKey];
        }

        /// <summary>How good a spot along the row is: clear enough first, then the nearer the better.</summary>
        private float Spot(float x) =>
            Mathf.Min(Clearance(new Vector2(x, _pos.y)), DemoComfort + 30f) - Mathf.Abs(x - _pos.x) * 0.05f;

        /// <summary>
        /// The key back towards the lower middle, if that way is clear: to the right height first,
        /// then across once well off to a side.
        /// </summary>
        private int HomeKey()
        {
            var off = new Vector2(0f, -_half.y * 0.5f) - _pos;
            int key = 0;
            if (Mathf.Abs(off.y) > 12f) key = off.y > 0f ? 1 : 3;
            else if (Mathf.Abs(off.x) > (_demoKey == 2 || _demoKey == 4 ? 12f : 150f)) key = off.x > 0f ? 2 : 4;
            if (key == 0) return 0;
            var to = key == 1 || key == 3 ? new Vector2(_pos.x, _pos.y + off.y) : new Vector2(0f, _pos.y);
            return Clearance(to) >= DemoComfort + 30f ? key : 0;
        }

        /// <summary>
        /// The smallest gap (as the hit test measures it) between the dot and any block the demo has
        /// seen, over the next second, if it heads straight for `spot` and stands there.
        /// </summary>
        private float Clearance(Vector2 spot)
        {
            float worst = 999f;
            for (float ahead = 0.025f; ahead <= 1f; ahead += 0.025f)
            {
                var p = Vector2.MoveTowards(_pos, spot, _speed * ahead);
                foreach (var b in _blocks)
                {
                    if (Elapsed < b.Start + 0.25f) continue; // not seen yet: a player needs a moment too
                    float t = (Elapsed + ahead - b.Start) / b.Duration;
                    if (t < 0f || t > 1f) continue;
                    var q = Vector2.LerpUnclamped(b.From, b.To, t);
                    float dx = Mathf.Max(0f, Mathf.Abs(q.x - p.x) - b.Size * 0.5f);
                    float dy = Mathf.Max(0f, Mathf.Abs(q.y - p.y) - b.Size * 0.5f);
                    worst = Mathf.Min(worst, Mathf.Sqrt(dx * dx + dy * dy) - PlayerRadius);
                }
            }
            return worst;
        }
    }
}
