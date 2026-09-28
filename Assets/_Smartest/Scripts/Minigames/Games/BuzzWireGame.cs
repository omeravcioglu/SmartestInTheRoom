using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The fairground buzz wire, with the cursor for the loop. Everyone gets the same winding
    /// path. The clock starts when you reach START, so where your mouse happened to be at GO
    /// doesn't matter. The whole stroke since the last frame is checked, so a flick can't
    /// jump a wall.
    /// </summary>
    public class BuzzWireGame : MinigameView
    {
        /// <summary>Must finish by then; the level's own deadline is a little later.</summary>
        private const float Limit = 14f;

        private readonly List<Rect> _path = new List<Rect>();
        private Rect _start;
        private Rect _end;
        private TMP_Text _label;
        private bool _armed;
        private float _armedAt;
        private Vector2 _last;

        protected override void Build()
        {
            var size = AreaSize;
            int turns;
            float w;
            switch (Level)
            {
                case 1: turns = 2; w = 96f; break;
                case 2: turns = 3; w = 84f; break;
                case 3: turns = 4; w = 72f; break;
                case 4: turns = 5; w = 62f; break;
                default:
                    turns = Mathf.Min(8, 6 + (Level - 5) / 2);
                    w = Mathf.Max(40f, 54f - (Level - 5) * 3f);
                    break;
            }

            float halfW = size.x * 0.5f - 60f;
            float top = size.y * 0.5f - 24f - w * 0.5f;
            float bottom = -size.y * 0.5f + 74f + w * 0.5f; // the hint lives below

            // Horizontal runs split the width; vertical runs join them at new heights.
            int runs = turns + 1;
            float x0 = -halfW + w * 0.5f, x1 = halfW - w * 0.5f;
            var xs = new float[runs + 1];
            xs[0] = x0;
            xs[runs] = x1;
            float runLength = (x1 - x0) / runs;
            for (int i = 1; i < runs; i++)
                xs[i] = x0 + i * runLength + RandomRange(-runLength * 0.25f, runLength * 0.25f);

            var ys = new float[runs];
            ys[0] = RandomRange(bottom, top);
            for (int i = 1; i < runs; i++)
            {
                float y;
                int tries = 0;
                do y = RandomRange(bottom, top);
                while (Mathf.Abs(y - ys[i - 1]) < w * 1.6f && ++tries < 20);
                ys[i] = y;
            }

            for (int i = 0; i < runs; i++)
            {
                AddSegment(new Vector2(xs[i], ys[i]), new Vector2(xs[i + 1], ys[i]), w);
                if (i + 1 < runs) AddSegment(new Vector2(xs[i + 1], ys[i]), new Vector2(xs[i + 1], ys[i + 1]), w);
            }
            _start = Square(new Vector2(xs[0], ys[0]), w);
            _end = Square(new Vector2(xs[runs], ys[runs - 1]), w);

            // Drawn in two passes: every edge first, then every fill on top, so the pieces
            // join into one path and only its outline shows.
            UiKit.Box(Area, "Board", new Vector2(halfW * 2f + 40f, top - bottom + w + 36f),
                new Vector2(0f, (top + bottom) * 0.5f), Palette.Panel);
            for (int i = 0; i < _path.Count; i++)
                UiKit.Fill(Area, "Edge" + i, _path[i].size + new Vector2(8f, 8f), _path[i].center, Palette.Ink);
            for (int i = 0; i < _path.Count; i++)
                UiKit.Fill(Area, "Path" + i, _path[i].size, _path[i].center, Palette.PanelRaised);
            UiKit.Fill(Area, "Start", _start.size, _start.center, Palette.AccentDim);
            UiKit.Fill(Area, "End", _end.size, _end.center, Palette.Accent);
            // The word sits beside the pad, not in it (a 40 px pad can't hold it), on the side
            // with more room.
            float wordY = _start.center.y > (top + bottom) * 0.5f ? _start.yMin - 18f : _start.yMax + 18f;
            UiKit.Label(Area, "StartText", "START", 20f, Palette.Ink, new Vector2(80f, 30f),
                new Vector2(_start.xMin + 40f, wordY), TextAlignmentOptions.Left);

            _label = UiKit.Label(Area, "Hint", "GO TO START", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        private void AddSegment(Vector2 a, Vector2 b, float w)
        {
            float h = w * 0.5f;
            _path.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x) - h, Mathf.Min(a.y, b.y) - h,
                                      Mathf.Max(a.x, b.x) + h, Mathf.Max(a.y, b.y) + h));
        }

        private static Rect Square(Vector2 centre, float w) => new Rect(centre.x - w * 0.5f, centre.y - w * 0.5f, w, w);

        private bool OnPath(Vector2 p)
        {
            for (int i = 0; i < _path.Count; i++) if (_path[i].Contains(p)) return true;
            return false;
        }

        protected override void OnTick(float dt)
        {
            if (!CanAct) return;
            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var p)) return;

            if (!_armed)
            {
                if (_start.Contains(p))
                {
                    _armed = true;
                    _armedAt = Elapsed;
                    _last = p;
                    _label.text = "TO THE GOLD END";
                }
                else if (Elapsed > Limit) Fail("NEVER STARTED");
                return;
            }

            // Every point of the stroke since the last frame has to be on the path.
            float travelled = Vector2.Distance(_last, p);
            int steps = Mathf.Max(1, Mathf.CeilToInt(travelled / 3f));
            for (int s = 1; s <= steps; s++)
            {
                var q = Vector2.Lerp(_last, p, s / (float)steps);
                if (OnPath(q)) continue;
                UiKit.Dot(Area, "Touch", 18f, q, Palette.Red);
                Fail("TOUCHED THE EDGE");
                return;
            }
            _last = p;

            if (_end.Contains(p))
            {
                _label.text = "MADE IT";
                _label.color = Palette.Green;
                Finish(false, Ms(Elapsed - _armedAt));
            }
            else if (Elapsed > Limit) Fail("TOO SLOW");
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
