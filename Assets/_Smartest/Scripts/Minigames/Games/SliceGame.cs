using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Fruit Ninja with dots: gold is thrown up in volleys and you slash through it with the
    /// button held down, before it lands. Red ones are bombs: don't touch them. A slash is the
    /// whole stroke since the last frame, so a fast swipe can't skip over anything. Ranked by
    /// total reaction time.
    /// </summary>
    public class SliceGame : MinigameView
    {
        private const float Gravity = 900f;

        private struct Throw
        {
            public float X0, VX, VY, Start;
            public bool Bomb;
            public bool Cut;
            public float CutAt;
            public bool Gone;
            public RectTransform View;
            public Image Image;
        }

        private readonly List<Throw> _throws = new List<Throw>();
        private float _r;
        private float _floorY;
        private float _reactionSum;
        private int _golds;
        private int _cut;
        private Vector2 _last;
        private bool _wasHeld;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int golds, maxGroup;
            float interval, bombShare;
            switch (Level)
            {
                case 1: golds = 5; interval = 1.4f; _r = 36f; maxGroup = 1; bombShare = 0f; break;
                case 2: golds = 7; interval = 1.3f; _r = 34f; maxGroup = 2; bombShare = 0.15f; break;
                case 3: golds = 9; interval = 1.2f; _r = 32f; maxGroup = 3; bombShare = 0.2f; break;
                case 4: golds = 11; interval = 1.1f; _r = 30f; maxGroup = 3; bombShare = 0.25f; break;
                default:
                    golds = Mathf.Min(18, 13 + (Level - 5));
                    interval = Mathf.Max(0.85f, 1.0f - (Level - 5) * 0.02f);
                    _r = Mathf.Max(24f, 28f - (Level - 5) * 0.5f);
                    maxGroup = 4;
                    bombShare = 0.25f;
                    break;
            }

            float halfW = Mathf.Min(540f, size.x * 0.5f - 40f);
            float top = size.y * 0.5f - 10f;
            _floorY = -size.y * 0.5f + 70f; // the hint lives below
            UiKit.Box(Area, "Sky", new Vector2(halfW * 2f, top - _floorY + 20f), new Vector2(0f, (top + _floorY - 20f) * 0.5f), Palette.PanelRaised);
            UiKit.Fill(Area, "Floor", new Vector2(halfW * 2f - 8f, 8f), new Vector2(0f, _floorY), Palette.Ink);
            float maxApex = top - _floorY - _r - 16f;

            float t = 0.5f;
            int made = 0;
            while (made < golds)
            {
                int group = Mathf.Min(golds - made, RandomRange(1, maxGroup + 1));
                for (int g = 0; g < group; g++, made++) Add(t, halfW, maxApex, false);
                // A bomb rides along with some volleys.
                if (RandomRange(0f, 1f) < bombShare) Add(t, halfW, maxApex, true);
                t += interval * (group > 1 ? 1.3f : 1f);
            }
            _golds = golds;

            _label = UiKit.Label(Area, "Hint", "HOLD AND SLASH", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            Progress("SLICED", 0, _golds);
        }

        private void Add(float start, float halfW, float maxApex, bool bomb)
        {
            float apex = RandomRange(maxApex * 0.6f, maxApex);
            float vy = Mathf.Sqrt(2f * Gravity * apex);
            float x0 = RandomRange(-halfW + 80f, halfW - 80f);
            float vx = RandomRange(0f, 120f) * (x0 > 0f ? -1f : 1f);
            var img = UiKit.Dot(Area, (bomb ? "Bomb" : "Gold") + _throws.Count, _r * 2f, new Vector2(x0, _floorY), bomb ? Palette.Red : Palette.Accent);
            img.gameObject.SetActive(false);
            _throws.Add(new Throw { X0 = x0, VX = vx, VY = vy, Start = start + (bomb ? 0.15f : 0f), Bomb = bomb, View = (RectTransform)img.transform, Image = img });
        }

        private Vector2 PositionAt(in Throw k, float age) =>
            new Vector2(k.X0 + k.VX * age, _floorY + k.VY * age - 0.5f * Gravity * age * age);

        /// <summary>Distance from p to the segment a-b.</summary>
        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len = ab.sqrMagnitude;
            float u = len < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len);
            return Vector2.Distance(p, a + ab * u);
        }

        protected override void OnTick(float dt)
        {
            var cursor = Vector2.zero;
            bool haveCursor = CanAct && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out cursor);
            bool held = haveCursor && KeyInput.MouseHeld();
            // The stroke this frame; the first frame of a press is a point, not a line from
            // wherever the button was last let go.
            var from = held && _wasHeld ? _last : cursor;

            for (int i = 0; i < _throws.Count; i++)
            {
                var k = _throws[i];
                if (k.Gone) continue;
                if (k.Cut)
                {
                    if (Elapsed - k.CutAt > 0.12f) { k.Gone = true; k.View.gameObject.SetActive(false); _throws[i] = k; }
                    continue;
                }
                float age = Elapsed - k.Start;
                if (age < 0f) continue;
                if (!k.View.gameObject.activeSelf) k.View.gameObject.SetActive(true);
                var p = PositionAt(k, age);
                k.View.anchoredPosition = p;

                if (held && SegmentDistance(p, from, cursor) <= _r)
                {
                    if (k.Bomb)
                    {
                        _throws[i] = k;
                        Fail("SLICED A BOMB");
                        return;
                    }
                    k.Cut = true;
                    k.CutAt = Elapsed;
                    k.Image.color = Palette.Green;
                    _reactionSum += age;
                    _cut++;
                    Progress("SLICED", _cut, _golds);
                    _throws[i] = k;
                    continue;
                }

                if (age > 2f * k.VY / Gravity)
                {
                    k.Gone = true;
                    _throws[i] = k;
                    if (!k.Bomb && CanAct)
                    {
                        k.Image.color = Palette.Red;
                        Fail("ONE LANDED");
                        return;
                    }
                    k.View.gameObject.SetActive(false);
                }
            }
            _last = cursor;
            _wasHeld = held;

            if (CanAct && _cut == _golds)
            {
                _label.text = "CLEAN CUTS";
                _label.color = Palette.Green;
                Finish(false, Ms(_reactionSum));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
