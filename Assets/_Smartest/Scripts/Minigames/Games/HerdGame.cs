using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// You're the sheepdog. Sheep run from the cursor, drift back towards each other and
    /// wander when left alone; get every one of them through the gap in the fence and into
    /// the pen. Come in too fast and the flock scatters. From level five a pond sits in the
    /// way. Everyone gets the same field and the same flock. Ranked by time.
    /// </summary>
    public class HerdGame : MinigameView
    {
        private const float PhysicsStep = 1f / 120f;
        private const float SheepR = 17f;
        private const float FleeR = 170f;
        private const float Wall = 10f;
        private const float Limit = 17f;

        private readonly List<Vector2> _p = new List<Vector2>();
        private readonly List<Vector2> _v = new List<Vector2>();
        private readonly List<bool> _penned = new List<bool>();
        private readonly List<RectTransform> _views = new List<RectTransform>();
        private readonly List<Vector2> _wander = new List<Vector2>(); // per-sheep phase
        private Rect _field, _pen;
        private float _gapLow, _gapHigh;
        private Vector2 _pond;
        private float _pondR;
        private float _flee, _maxSpeed, _jitter;
        private Vector2 _dog;
        private RectTransform _dogView;
        private int _in;
        private float _acc;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int sheep;
            float gap;
            switch (Level)
            {
                case 1: sheep = 3; gap = 150f; _flee = 2300f; _maxSpeed = 330f; _jitter = 40f; break;
                case 2: sheep = 4; gap = 140f; _flee = 2400f; _maxSpeed = 340f; _jitter = 50f; break;
                case 3: sheep = 5; gap = 130f; _flee = 2500f; _maxSpeed = 350f; _jitter = 60f; break;
                case 4: sheep = 6; gap = 120f; _flee = 2600f; _maxSpeed = 360f; _jitter = 70f; break;
                default:
                    sheep = Mathf.Min(8, 6 + (Level - 5) / 2);
                    gap = Mathf.Max(96f, 116f - (Level - 5) * 4f);
                    _flee = 2700f; _maxSpeed = Mathf.Min(420f, 370f + (Level - 5) * 8f); _jitter = 80f;
                    break;
            }

            float top = size.y * 0.5f - 12f, bottom = -size.y * 0.5f + 64f; // the hint lives below
            float halfW = Mathf.Min(570f, size.x * 0.5f - 20f);
            _field = Rect.MinMaxRect(-halfW, bottom, halfW, top);
            UiKit.Box(Area, "Field", _field.size + new Vector2(8f, 8f), _field.center, Palette.Panel);

            // The pen on the right, open through a gap in its left fence.
            float penW = 220f, penH = Mathf.Min(300f, _field.height - 60f);
            float penY = RandomRange(_field.yMin + penH * 0.5f + 20f, _field.yMax - penH * 0.5f - 20f);
            _pen = new Rect(_field.xMax - penW, penY - penH * 0.5f, penW, penH);
            float gapMid = RandomRange(_pen.yMin + gap * 0.5f + 30f, _pen.yMax - gap * 0.5f - 30f);
            _gapLow = gapMid - gap * 0.5f;
            _gapHigh = gapMid + gap * 0.5f;
            UiKit.Fill(Area, "PenFloor", _pen.size, _pen.center, Palette.AccentDim);
            Fence(new Vector2(_pen.xMin, _pen.yMax), new Vector2(_pen.xMax, _pen.yMax));
            Fence(new Vector2(_pen.xMin, _pen.yMin), new Vector2(_pen.xMax, _pen.yMin));
            Fence(new Vector2(_pen.xMin, _pen.yMax), new Vector2(_pen.xMin, _gapHigh));
            Fence(new Vector2(_pen.xMin, _gapLow), new Vector2(_pen.xMin, _pen.yMin));
            UiKit.Dot(Area, "GateHigh", 22f, new Vector2(_pen.xMin, _gapHigh), Palette.Accent);
            UiKit.Dot(Area, "GateLow", 22f, new Vector2(_pen.xMin, _gapLow), Palette.Accent);

            if (Level >= 5)
            {
                _pondR = 64f;
                _pond = new Vector2(RandomRange(-40f, 120f), RandomRange(_field.yMin + 110f, _field.yMax - 110f));
                UiKit.Dot(Area, "Pond", _pondR * 2f, _pond, Color.Lerp(Palette.PaperHi, Palette.Blue, 0.45f));
            }

            // The flock starts on the far side of the field.
            for (int i = 0; i < sheep; i++)
            {
                Vector2 p;
                int tries = 0;
                do p = new Vector2(RandomRange(_field.xMin + 60f, -160f), RandomRange(_field.yMin + 40f, _field.yMax - 40f));
                while (Crowded(p) && ++tries < 40);
                _p.Add(p);
                _v.Add(Vector2.zero);
                _penned.Add(false);
                _wander.Add(new Vector2(RandomRange(0f, 6.3f), RandomRange(0.7f, 1.3f)));
                // From above, nose first; the wool is about the size of the body that's simulated.
                var body = UiKit.Art(Area, "Sheep" + i, "sheep", new Vector2(50f, 40f) * (SheepR * 2f / 36f), p, Palette.PaperHi);
                _views.Add((RectTransform)body.transform);
            }
            _dog = new Vector2(_field.xMin + 30f, _field.center.y);
            // The dog: black and white, so it's never mistaken for a sheep or the pond.
            _dogView = (RectTransform)UiKit.Art(Area, "Dog", "dog", new Vector2(42f, 42f), _dog).transform;

            Progress("PENNED", 0, sheep);
            _label = UiKit.Label(Area, "Hint", "HERD THEM INTO THE PEN", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        private bool Crowded(Vector2 p)
        {
            foreach (var q in _p) if (Vector2.Distance(p, q) < SheepR * 3f) return true;
            return false;
        }

        private void Fence(Vector2 a, Vector2 b)
        {
            var d = (b - a).normalized * (Wall * 0.5f);
            UiKit.Line(Area, "Fence", a - d, b + d, Wall, Palette.Ink);
        }

        /// <summary>For someone watching: work round behind the sheep nearest the gate and push it in.</summary>
        private Vector2 AutoDog()
        {
            var gate = new Vector2(_pen.xMin, (_gapLow + _gapHigh) * 0.5f);
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < _p.Count; i++)
            {
                if (_penned[i]) continue;
                float d = Vector2.Distance(_p[i], gate);
                if (d < bestD) { best = i; bestD = d; }
            }
            if (best < 0) return _dog;
            return _p[best] + (_p[best] - gate).normalized * 120f;
        }

        protected override void OnTick(float dt)
        {
            Vector2 want;
            if (CanAct && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m)) want = m;
            else if (!Interactive) want = Vector2.MoveTowards(_dog, AutoDog(), 520f * dt);
            else want = _dog;
            _dog = new Vector2(Mathf.Clamp(want.x, _field.xMin, _field.xMax), Mathf.Clamp(want.y, _field.yMin, _field.yMax));
            _dogView.anchoredPosition = _dog;
            if (!Interactive) PointAt(_dog); // the dog goes where the mouse is: the demo's hand is on it

            _acc += dt;
            while (_acc >= PhysicsStep)
            {
                _acc -= PhysicsStep;
                Simulate(PhysicsStep);
            }
            for (int i = 0; i < _p.Count; i++)
            {
                _views[i].anchoredPosition = _p[i];
                // Face the way they're going.
                if (_v[i].sqrMagnitude > 400f) _views[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_v[i].y, _v[i].x) * Mathf.Rad2Deg);
            }

            if (!CanAct) return;
            if (_in >= _p.Count)
            {
                _label.text = "ALL PENNED";
                _label.color = Palette.Green;
                Finish(false, Ms(Elapsed));
            }
            else if (Elapsed > Limit)
            {
                _label.text = "TIME'S UP";
                _label.color = Palette.Red;
                Finish(true, WorstMetric);
            }
        }

        private void Simulate(float h)
        {
            float time = Elapsed;
            var centre = Vector2.zero;
            int loose = 0;
            for (int i = 0; i < _p.Count; i++) if (!_penned[i]) { centre += _p[i]; loose++; }
            if (loose > 0) centre /= loose;

            for (int i = 0; i < _p.Count; i++)
            {
                var a = Vector2.zero;
                if (!_penned[i])
                {
                    // Run from the dog, harder the closer it is.
                    var away = _p[i] - _dog;
                    float d = away.magnitude;
                    if (d < FleeR && d > 0.01f) a += away / d * (_flee * (1f - d / FleeR));
                    // Drift back to the flock, and wander a little.
                    var toFlock = centre - _p[i];
                    if (toFlock.magnitude > 70f) a += toFlock.normalized * 90f;
                    var w = _wander[i];
                    a += new Vector2(Mathf.Sin(time * w.y + w.x), Mathf.Cos(time * w.y * 0.8f + w.x * 1.7f)) * _jitter;
                }
                // Personal space.
                for (int j = 0; j < _p.Count; j++)
                {
                    if (j == i) continue;
                    var apart = _p[i] - _p[j];
                    float d = apart.magnitude;
                    if (d < SheepR * 2.4f && d > 0.01f) a += apart / d * (900f * (1f - d / (SheepR * 2.4f)));
                }
                _v[i] = (_v[i] + a * h) * Mathf.Exp(-(_penned[i] ? 6f : 2.2f) * h);
                _v[i] = Vector2.ClampMagnitude(_v[i], _maxSpeed);
                _p[i] += _v[i] * h;
                Collide(i);

                if (!_penned[i] && _p[i].x - SheepR > _pen.xMin + Wall * 0.5f && _pen.Contains(_p[i]))
                {
                    _penned[i] = true;
                    _in++;
                    Progress("PENNED", _in, _p.Count);
                    _views[i].GetComponent<Image>().color = Palette.Accent;
                }
            }
        }

        private void Collide(int i)
        {
            var p = _p[i];
            var v = _v[i];
            // The field's edges.
            if (p.x < _field.xMin + SheepR) { p.x = _field.xMin + SheepR; v.x = Mathf.Abs(v.x) * 0.4f; }
            if (p.x > _field.xMax - SheepR) { p.x = _field.xMax - SheepR; v.x = -Mathf.Abs(v.x) * 0.4f; }
            if (p.y < _field.yMin + SheepR) { p.y = _field.yMin + SheepR; v.y = Mathf.Abs(v.y) * 0.4f; }
            if (p.y > _field.yMax - SheepR) { p.y = _field.yMax - SheepR; v.y = -Mathf.Abs(v.y) * 0.4f; }
            // The fences, with the gate posts as the ends of the two left pieces.
            Segment(ref p, ref v, new Vector2(_pen.xMin, _pen.yMax), new Vector2(_pen.xMax, _pen.yMax));
            Segment(ref p, ref v, new Vector2(_pen.xMin, _pen.yMin), new Vector2(_pen.xMax, _pen.yMin));
            Segment(ref p, ref v, new Vector2(_pen.xMin, _gapHigh), new Vector2(_pen.xMin, _pen.yMax));
            Segment(ref p, ref v, new Vector2(_pen.xMin, _pen.yMin), new Vector2(_pen.xMin, _gapLow));
            // A penned sheep stays penned.
            if (_penned[i] && p.x < _pen.xMin + Wall * 0.5f + SheepR) { p.x = _pen.xMin + Wall * 0.5f + SheepR; v.x = Mathf.Abs(v.x); }
            if (_pondR > 0f)
            {
                var off = p - _pond;
                float d = off.magnitude;
                if (d < _pondR + SheepR && d > 0.01f)
                {
                    var n = off / d;
                    p = _pond + n * (_pondR + SheepR);
                    float into = Vector2.Dot(v, n);
                    if (into < 0f) v -= n * into;
                }
            }
            _p[i] = p;
            _v[i] = v;
        }

        /// <summary>Keep a sheep out of a fence: push it off the nearest point and kill the speed going in.</summary>
        private static void Segment(ref Vector2 p, ref Vector2 v, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            var closest = a + ab * t;
            var off = p - closest;
            float d = off.magnitude;
            float min = SheepR + Wall * 0.5f;
            if (d >= min) return;
            var n = d > 0.01f ? off / d : new Vector2(-ab.y, ab.x).normalized;
            p = closest + n * min;
            float into = Vector2.Dot(v, n);
            if (into < 0f) v -= n * into * 1.3f;
        }
    }
}
