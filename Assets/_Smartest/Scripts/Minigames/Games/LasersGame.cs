using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Red beams sweep the box, each with one gap, and your cursor has to be in that gap when
    /// the beam passes. Everyone faces the same beams from the same seed. Both the cursor's
    /// stroke and the beam's own move since the last frame are checked, so neither a flick
    /// nor a fast beam slips through. Ranked by the narrowest escape. The beams wait until
    /// your cursor is in the box, so nobody is out for where their mouse was at GO.
    /// </summary>
    public class LasersGame : MinigameView
    {
        private const float Thickness = 26f;
        /// <summary>You have this long to get into the box.</summary>
        private const float GetInBy = 3f;

        private struct Beam
        {
            public bool Vertical;   // a vertical bar that sweeps sideways
            public float Start;
            public float From, To;  // along the axis it sweeps
            public float GapCentre;
            public RectTransform A, B;
        }

        private Beam[] _beams = new Beam[0];
        private float _duration;
        private float _gapW;
        private Rect _box;
        private TMP_Text _label;
        private Vector2 _last;
        /// <summary>When the beams set off; negative until the cursor first gets into the box.</summary>
        private float _armedAt = -1f;
        private float _closest = float.MaxValue;

        /// <summary>Bigger clearance is better, so the worst result is none at all.</summary>
        public override int WorstMetric => 0;

        protected override void Build()
        {
            var size = AreaSize;
            int n;
            float interval;
            int kinds; // 0 horizontal only, 1 alternate, 2 random
            switch (Level)
            {
                case 1: n = 3; _duration = 2.4f; interval = 2.0f; _gapW = 220f; kinds = 0; break;
                case 2: n = 4; _duration = 2.2f; interval = 1.8f; _gapW = 200f; kinds = 1; break;
                case 3: n = 5; _duration = 2.0f; interval = 1.6f; _gapW = 180f; kinds = 2; break;
                case 4: n = 6; _duration = 1.85f; interval = 1.4f; _gapW = 165f; kinds = 2; break;
                default:
                    n = Mathf.Min(10, 7 + (Level - 5));
                    _duration = Mathf.Max(1.3f, 1.7f - (Level - 5) * 0.05f);
                    interval = Mathf.Max(0.9f, 1.25f - (Level - 5) * 0.04f);
                    _gapW = Mathf.Max(110f, 150f - (Level - 5) * 5f);
                    kinds = 2;
                    break;
            }

            float w = Mathf.Min(1000f, size.x - 160f), h = Mathf.Min(400f, size.y - 110f);
            var centre = new Vector2(0f, 20f);
            var arena = UiKit.Box(Area, "Box", new Vector2(w, h), centre, Palette.PanelRaised);
            // Beams slide in and out at the edges, and never over the box's own ink edge.
            arena.gameObject.AddComponent<RectMask2D>().padding = new Vector4(4f, 4f, 4f, 4f);
            var world = UiKit.Node(arena.transform, "World", Vector2.zero, -centre);
            _box = new Rect(centre.x - w * 0.5f + 4f, centre.y - h * 0.5f + 4f, w - 8f, h - 8f);

            _beams = new Beam[n];
            for (int i = 0; i < n; i++)
            {
                bool vertical = kinds == 1 ? i % 2 == 1 : kinds == 2 && RandomRange(0, 2) == 0;
                bool reverse = RandomRange(0, 2) == 0;
                var b = new Beam { Vertical = vertical, Start = 0.8f + i * interval };
                float lo, hi, gapLo, gapHi;
                if (vertical)
                {
                    lo = _box.xMin - Thickness; hi = _box.xMax + Thickness;
                    gapLo = _box.yMin + _gapW * 0.5f + 20f; gapHi = _box.yMax - _gapW * 0.5f - 20f;
                }
                else
                {
                    lo = _box.yMin - Thickness; hi = _box.yMax + Thickness;
                    gapLo = _box.xMin + _gapW * 0.5f + 20f; gapHi = _box.xMax - _gapW * 0.5f - 20f;
                }
                b.From = reverse ? hi : lo;
                b.To = reverse ? lo : hi;
                b.GapCentre = RandomRange(gapLo, gapHi);
                BuildBeam(world, i, ref b);
                _beams[i] = b;
            }

            _label = UiKit.Label(Area, "Hint", Interactive ? "GET IN THE BOX" : "MIND THE BEAMS", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            Progress("BEAMS", 0, n);
        }

        /// <summary>Two red bars with the gap between them, parked just outside the box.</summary>
        private void BuildBeam(Transform parent, int i, ref Beam b)
        {
            float g0 = b.GapCentre - _gapW * 0.5f, g1 = b.GapCentre + _gapW * 0.5f;
            if (b.Vertical)
            {
                float y0 = _box.yMin - 30f, y1 = _box.yMax + 30f;
                b.A = Bar(parent, "Beam" + i + "A", new Vector2(Thickness, g0 - y0), new Vector2(b.From, (y0 + g0) * 0.5f));
                b.B = Bar(parent, "Beam" + i + "B", new Vector2(Thickness, y1 - g1), new Vector2(b.From, (g1 + y1) * 0.5f));
            }
            else
            {
                float x0 = _box.xMin - 30f, x1 = _box.xMax + 30f;
                b.A = Bar(parent, "Beam" + i + "A", new Vector2(g0 - x0, Thickness), new Vector2((x0 + g0) * 0.5f, b.From));
                b.B = Bar(parent, "Beam" + i + "B", new Vector2(x1 - g1, Thickness), new Vector2((g1 + x1) * 0.5f, b.From));
            }
        }

        private static RectTransform Bar(Transform parent, string name, Vector2 size, Vector2 pos)
        {
            var root = UiKit.Node(parent, name, size, pos);
            UiKit.Fill(root, "Edge", size, Vector2.zero, Palette.Ink);
            UiKit.Fill(root, "Paint", size - new Vector2(8f, 8f), Vector2.zero, Palette.Red);
            return root;
        }

        private float PositionAt(in Beam b, float t) => Mathf.Lerp(b.From, b.To, Mathf.Clamp01((t - b.Start) / _duration));

        protected override void OnTick(float dt)
        {
            if (!Interactive && _armedAt < 0f) _armedAt = 0f; // someone watching sees them straight away
            float t = _armedAt < 0f ? 0f : Elapsed - _armedAt;
            for (int i = 0; i < _beams.Length; i++)
            {
                var b = _beams[i];
                float p = PositionAt(b, t);
                if (b.Vertical)
                {
                    b.A.anchoredPosition = new Vector2(p, b.A.anchoredPosition.y);
                    b.B.anchoredPosition = new Vector2(p, b.B.anchoredPosition.y);
                }
                else
                {
                    b.A.anchoredPosition = new Vector2(b.A.anchoredPosition.x, p);
                    b.B.anchoredPosition = new Vector2(b.B.anchoredPosition.x, p);
                }
            }
            if (!CanAct) return;
            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var cursor)) return;

            if (_armedAt < 0f)
            {
                if (_box.Contains(cursor))
                {
                    _armedAt = Elapsed;
                    _last = cursor;
                    _label.text = "MIND THE BEAMS";
                }
                else if (Elapsed > GetInBy) Fail("NEVER GOT IN");
                return;
            }
            if (!_box.Contains(cursor)) { Fail("LEFT THE BOX"); return; }

            // Every point of the cursor's stroke against the ground each beam covered this frame.
            float stroke = Vector2.Distance(_last, cursor);
            int steps = Mathf.Max(1, Mathf.CeilToInt(stroke / 4f));
            for (int i = 0; i < _beams.Length; i++)
            {
                var b = _beams[i];
                if (t < b.Start || t - dt > b.Start + _duration) continue;
                float now = PositionAt(b, t), before = PositionAt(b, t - dt);
                float bandLo = Mathf.Min(now, before) - Thickness * 0.5f, bandHi = Mathf.Max(now, before) + Thickness * 0.5f;
                for (int s = 1; s <= steps; s++)
                {
                    var q = Vector2.Lerp(_last, cursor, s / (float)steps);
                    float along = b.Vertical ? q.x : q.y, across = b.Vertical ? q.y : q.x;
                    bool inGap = Mathf.Abs(across - b.GapCentre) < _gapW * 0.5f;
                    if (along >= bandLo && along <= bandHi && !inGap)
                    {
                        UiKit.Dot(Area, "Zap", 18f, q, Palette.Accent);
                        Fail("ZAPPED");
                        return;
                    }
                }

                // How close did it come? The distance to the nearer solid part: off the bar's
                // line, plus (only when lined up with the gap) off the gap's nearer edge.
                float offLine = Mathf.Max(0f, Mathf.Abs((b.Vertical ? cursor.x : cursor.y) - now) - Thickness * 0.5f);
                float fromGapCentre = Mathf.Abs((b.Vertical ? cursor.y : cursor.x) - b.GapCentre);
                float offEdge = Mathf.Max(0f, _gapW * 0.5f - fromGapCentre);
                float clearance = Mathf.Sqrt(offLine * offLine + offEdge * offEdge);
                if (clearance < _closest) _closest = clearance;
            }
            _last = cursor;

            int passed = 0;
            for (int i = 0; i < _beams.Length; i++) if (t > _beams[i].Start + _duration) passed++;
            Progress("BEAMS", passed, _beams.Length);

            var last = _beams[_beams.Length - 1];
            if (t > last.Start + _duration)
            {
                _label.text = "UNTOUCHED";
                _label.color = Palette.Green;
                float closest = _closest == float.MaxValue ? 0f : _closest;
                // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(Mathf.Max(0f, closest) * 10f));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, 0);
        }
    }
}
