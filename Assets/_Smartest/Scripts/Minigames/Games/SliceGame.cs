using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Fruit Ninja: oranges are thrown up in volleys and you slash through them with the
    /// button held down, before they land. Bombs ride along: don't touch them. A slash is the
    /// whole stroke since the last frame, so a fast swipe can't skip over anything. Ranked by
    /// total reaction time.
    /// </summary>
    public class SliceGame : MinigameView
    {
        private const float Gravity = 900f;
        private const float HalvesFor = 0.45f;
        private const float BladeFor = 0.1f;
        private const int BladeBits = 8;

        private struct Throw
        {
            public float X0, VX, VY, Start;
            public bool Bomb;
            public float Spin;
            public bool Cut;
            public float CutAt;
            public Vector2 CutPos, CutVel, Apart;
            public RectTransform HalfA, HalfB;
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
        private readonly RectTransform[] _blade = new RectTransform[BladeBits];
        private readonly Vector2[] _trailAt = new Vector2[BladeBits + 1];
        private readonly float[] _trailTime = new float[BladeBits + 1];
        private int _trail;
        // The rule card's demo: the slash it's in the middle of, if any.
        private Vector2 _swipeFrom, _swipeTo;
        private float _swipeAt = -1f;
        private int _swipes;

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

            // The blade's trail, over the fruit: newest piece thickest.
            for (int i = 0; i < BladeBits; i++)
            {
                _blade[i] = UiKit.Line(Area, "Blade" + i, Vector2.zero, Vector2.right, Mathf.Lerp(10f, 2f, i / (BladeBits - 1f)), Palette.Ink);
                _blade[i].gameObject.SetActive(false);
            }

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
            // Drawn so the round part is the size that gets hit.
            string piece = bomb ? "bomb" : "orange";
            var img = UiKit.Art(Area, (bomb ? "Bomb" : "Gold") + _throws.Count, piece, new Vector2(80f, 84f) * (_r / (bomb ? 32f : 34f)), new Vector2(x0, _floorY));
            UiKit.PivotOn(img, piece, bomb ? new Vector2(38f, 49f) : new Vector2(40f, 46f));
            img.gameObject.SetActive(false);
            _throws.Add(new Throw
            {
                X0 = x0, VX = vx, VY = vy, Start = start + (bomb ? 0.15f : 0f), Bomb = bomb,
                Spin = (vx >= 0f ? -1f : 1f) * (70f + Mathf.Abs(vx)), View = (RectTransform)img.transform, Image = img
            });
        }

        /// <summary>One half of a cut orange, its flat side along the slash.</summary>
        private RectTransform Half(Vector2 at, float angle)
        {
            var img = UiKit.Art(Area, "Half", "orange_half", new Vector2(80f, 44f) * (_r / 34f), at);
            UiKit.PivotOn(img, "orange_half", new Vector2(40f, 40f));
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            return img.rectTransform;
        }

        /// <summary>The halves carry on the way the orange was going, falling, and drift apart.</summary>
        private void MoveHalves(in Throw k, float dt)
        {
            float t = Elapsed - k.CutAt;
            var drift = k.CutPos + k.CutVel * t + new Vector2(0f, -0.5f * Gravity * t * t);
            k.HalfA.anchoredPosition = drift + k.Apart * (110f * t);
            k.HalfB.anchoredPosition = drift - k.Apart * (110f * t);
            k.HalfA.localRotation *= Quaternion.Euler(0f, 0f, 120f * dt);
            k.HalfB.localRotation *= Quaternion.Euler(0f, 0f, -120f * dt);
        }

        /// <summary>The trail behind the cursor while the button is down, shrinking away behind it.</summary>
        private void DrawBlade(bool held, bool pressed, Vector2 cursor)
        {
            if (pressed) _trail = 0;
            if (held)
            {
                if (_trail > 0 && (cursor - _trailAt[0]).sqrMagnitude < 4f) _trailTime[0] = Elapsed;
                else
                {
                    for (int j = Mathf.Min(_trail, BladeBits); j > 0; j--) { _trailAt[j] = _trailAt[j - 1]; _trailTime[j] = _trailTime[j - 1]; }
                    _trailAt[0] = cursor;
                    _trailTime[0] = Elapsed;
                    _trail = Mathf.Min(_trail + 1, BladeBits + 1);
                }
            }
            while (_trail > 0 && Elapsed - _trailTime[_trail - 1] > BladeFor) _trail--;
            for (int i = 0; i < BladeBits; i++)
            {
                bool on = i + 1 < _trail;
                if (_blade[i].gameObject.activeSelf != on) _blade[i].gameObject.SetActive(on);
                if (on) UiKit.SetLine(_blade[i], _trailAt[i], _trailAt[i + 1]);
            }
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
            // The rule card's demo swings a blade of its own, through the same trail and cuts.
            if (Demo)
            {
                held = Swipe(out cursor);
                HoldAt(cursor, held);
            }
            // The stroke this frame; the first frame of a press is a point, not a line from
            // wherever the button was last let go.
            var from = held && _wasHeld ? _last : cursor;
            DrawBlade(held, held && !_wasHeld, cursor);

            for (int i = 0; i < _throws.Count; i++)
            {
                var k = _throws[i];
                if (k.Gone) continue;
                if (k.Cut)
                {
                    if (Elapsed - k.CutAt < HalvesFor) MoveHalves(k, dt);
                    else
                    {
                        k.Gone = true;
                        k.HalfA.gameObject.SetActive(false);
                        k.HalfB.gameObject.SetActive(false);
                        _throws[i] = k;
                    }
                    continue;
                }
                float age = Elapsed - k.Start;
                if (age < 0f) continue;
                if (!k.View.gameObject.activeSelf) k.View.gameObject.SetActive(true);
                var p = PositionAt(k, age);
                k.View.anchoredPosition = p;
                k.View.localRotation = Quaternion.Euler(0f, 0f, k.Spin * age);

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
                    // Two halves, split along the stroke, take over from the whole one.
                    var stroke = cursor - from;
                    float angle = stroke.sqrMagnitude > 1f ? Mathf.Atan2(stroke.y, stroke.x) * Mathf.Rad2Deg : 0f;
                    k.CutPos = p;
                    k.CutVel = new Vector2(k.VX, k.VY - Gravity * age);
                    k.Apart = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                    k.HalfA = Half(p, angle);
                    k.HalfB = Half(p, angle + 180f);
                    k.View.gameObject.SetActive(false);
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

            if (CanMove && _cut == _golds)
            {
                _label.text = "CLEAN CUTS";
                _label.color = Palette.Green;
                Finish(false, Ms(_reactionSum));
            }
        }

        /// <summary>
        /// The rule card's demo: a quick slash through each orange as it slows near the top, the
        /// one landing first first. The hand goes to where the slash starts, presses, sweeps
        /// through and lets go. Returns whether the button is down, with the blade at `at`.
        /// </summary>
        private bool Swipe(out Vector2 at)
        {
            const float SwipeFor = 0.2f, Reach = 115f;
            at = DemoHand.At;
            if (_swipeAt >= 0f)
            {
                float u = (Elapsed - _swipeAt) / SwipeFor;
                at = Vector2.Lerp(_swipeFrom, _swipeTo, u);
                if (u >= 1f) { _swipeAt = -1f; return false; }
                // Never through a bomb: with one in the way, lift the blade.
                for (int i = 0; i < _throws.Count; i++)
                {
                    var b = _throws[i];
                    if (!b.Bomb || b.Gone || Elapsed < b.Start) continue;
                    if (SegmentDistance(PositionAt(b, Elapsed - b.Start), _last, at) <= _r + 24f) { _swipeAt = -1f; return false; }
                }
                return true;
            }

            int next = -1;
            float lands = float.MaxValue;
            for (int i = 0; i < _throws.Count; i++)
            {
                var k = _throws[i];
                if (k.Bomb || k.Cut || k.Gone || Elapsed < k.Start) continue;
                float down = k.Start + 2f * k.VY / Gravity;
                if (down < lands) { lands = down; next = i; }
            }
            if (next < 0 || Elapsed < _throws[next].Start + 0.25f) return false;

            // Slashed down and across, one way then the other, through where it will be mid-slash.
            var t = _throws[next];
            float mid = Mathf.Max(t.Start + 0.6f, Elapsed + SwipeFor * 0.5f);
            var across = new Vector2(_swipes % 2 == 0 ? 1f : -1f, -0.5f).normalized * Reach;
            var through = PositionAt(t, mid - t.Start);
            at = through - across;
            if (Elapsed < mid - SwipeFor * 0.5f) return false;
            _swipeFrom = at;
            _swipeTo = through + across;
            _swipeAt = Elapsed;
            _swipes++;
            return true;
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
