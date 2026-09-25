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

        /// <summary>Bigger margin is better, so the worst result is a margin of nothing.</summary>
        public override int WorstMetric => 0;

        protected override void Build()
        {
            var size = AreaSize;
            _half = new Vector2(Mathf.Min(760f, size.x - 60f), Mathf.Min(360f, size.y - 80f)) * 0.5f;
            UiKit.Box(Area, "Arena", _half * 2f, Vector2.zero, Palette.Panel);

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
                var rt = (RectTransform)UiKit.Box(Area, "Block" + i, new Vector2(s, s), from, Palette.Red).transform;
                rt.gameObject.SetActive(false);
                _views.Add(rt);
            }

            _pos = Vector2.zero;
            _player = (RectTransform)UiKit.Dot(Area, "You", PlayerRadius * 2f, Vector2.zero, Palette.Accent).transform;
            _label = UiKit.Label(Area, "Hint", "WASD", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -_half.y - 30f));
        }

        protected override void OnTick(float dt)
        {
            if (CanAct)
            {
                var move = KeyInput.MoveAxis();
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
                if (_label != null) { _label.text = "SURVIVED"; _label.color = Palette.Green; }
                int margin = _closest == float.MaxValue ? 999 : Mathf.RoundToInt(Mathf.Max(0f, _closest));
                Finish(false, margin);
            }
        }
    }
}
