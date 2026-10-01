using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// A real maze, carved fresh from the seed (so everyone gets the same one): junctions, dead
    /// ends, one way through. Take the cursor from START to the gold exit without touching a
    /// wall. From level six the lights go out and your cursor carries a torch. The whole
    /// stroke since the last frame is checked, so a flick can't jump a wall. Ranked by time.
    /// </summary>
    public class MazeGame : MinigameView
    {
        private const float Limit = 16f;

        private int _cols, _rows;
        private float _c, _t;
        private bool[,] _right, _down;     // walls: right of / below each cell
        private int _inRow, _outRow;
        private float _x0, _y0;            // the maze's top-left corner
        private Rect _startPad, _endPad;
        private bool _armed;
        private float _armedAt;
        private Vector2 _last;
        private RectTransform _torch, _world;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            float torchR = 0f;
            switch (Level)
            {
                case 1: _cols = 6; _rows = 3; _c = 92f; _t = 14f; break;
                case 2: _cols = 7; _rows = 4; _c = 80f; _t = 13f; break;
                case 3: _cols = 9; _rows = 4; _c = 74f; _t = 12f; break;
                case 4: _cols = 10; _rows = 5; _c = 66f; _t = 11f; break;
                default:
                    _cols = 11; _rows = 5; _c = 62f; _t = 10f;
                    // From level six the lights are out: you see what the torch shows.
                    if (Level >= 6) torchR = Mathf.Max(110f, 150f - (Level - 6) * 8f);
                    break;
            }
            Carve();

            float w = _cols * _c, h = _rows * _c;
            var centre = new Vector2(0f, 20f);
            _x0 = centre.x - w * 0.5f;
            _y0 = centre.y + h * 0.5f;

            // In the dark, everything but the pads is drawn inside a round mask that follows
            // the cursor (the torch); the scene inside is held still by moving it the other way.
            Transform scene = Area;
            if (torchR > 0f)
            {
                UiKit.Box(Area, "Dark", new Vector2(w + _c * 2f + 40f, h + 40f), centre, Palette.Ink);
                _torch = UiKit.Node(Area, "Torch", Vector2.one * (torchR * 2f), centre);
                var disc = _torch.gameObject.AddComponent<Image>();
                disc.sprite = UiKit.Circle;
                disc.raycastTarget = false;
                _torch.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                _world = UiKit.Node(_torch, "Scene", Vector2.zero, -centre);
                scene = _world;
            }
            UiKit.Fill(scene, "Floor", new Vector2(w, h), centre, Palette.PaperHi);
            DrawWalls(scene);

            _startPad = new Rect(_x0 - _c, _y0 - (_inRow + 1) * _c + _t * 0.5f, _c, _c - _t);
            _endPad = new Rect(_x0 + w, _y0 - (_outRow + 1) * _c + _t * 0.5f, _c, _c - _t);
            UiKit.Fill(Area, "Start", _startPad.size, _startPad.center, Palette.AccentDim);
            UiKit.Fill(Area, "Exit", _endPad.size, _endPad.center, Palette.Accent);
            UiKit.Label(Area, "StartText", "START", 20f, torchR > 0f ? Palette.PaperHi : Palette.Ink, new Vector2(90f, 30f),
                _startPad.center + new Vector2(0f, _startPad.height * 0.5f + 16f));

            _label = UiKit.Label(Area, "Hint", Interactive ? "GO TO START" : "FIND THE GOLD EXIT", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        /// <summary>A perfect maze by randomised depth-first search: every cell reachable, one way between any two.</summary>
        private void Carve()
        {
            _right = new bool[_cols, _rows];
            _down = new bool[_cols, _rows];
            for (int c = 0; c < _cols; c++) for (int r = 0; r < _rows; r++) { _right[c, r] = true; _down[c, r] = true; }
            var seen = new bool[_cols, _rows];
            var stack = new Stack<Vector2Int>();
            var start = new Vector2Int(0, RandomRange(0, _rows));
            seen[start.x, start.y] = true;
            stack.Push(start);
            var options = new List<Vector2Int>(4);
            while (stack.Count > 0)
            {
                var at = stack.Peek();
                options.Clear();
                if (at.x > 0 && !seen[at.x - 1, at.y]) options.Add(new Vector2Int(at.x - 1, at.y));
                if (at.x < _cols - 1 && !seen[at.x + 1, at.y]) options.Add(new Vector2Int(at.x + 1, at.y));
                if (at.y > 0 && !seen[at.x, at.y - 1]) options.Add(new Vector2Int(at.x, at.y - 1));
                if (at.y < _rows - 1 && !seen[at.x, at.y + 1]) options.Add(new Vector2Int(at.x, at.y + 1));
                if (options.Count == 0) { stack.Pop(); continue; }
                var next = options[RandomRange(0, options.Count)];
                if (next.x != at.x) _right[Mathf.Min(at.x, next.x), at.y] = false;
                else _down[at.x, Mathf.Min(at.y, next.y)] = false;
                seen[next.x, next.y] = true;
                stack.Push(next);
            }
            _inRow = start.y;
            _outRow = RandomRange(0, _rows);
        }

        private void DrawWalls(Transform scene)
        {
            float w = _cols * _c, h = _rows * _c;
            int k = 0;
            void Wall(Vector2 a, Vector2 b)
            {
                // Each wall runs half a thickness past its ends, which fills in the corners.
                var d = (b - a).normalized * (_t * 0.5f);
                UiKit.Line(scene, "Wall" + k++, a - d, b + d, _t, Palette.Ink);
            }
            Wall(new Vector2(_x0, _y0), new Vector2(_x0 + w, _y0));
            Wall(new Vector2(_x0, _y0 - h), new Vector2(_x0 + w, _y0 - h));
            for (int r = 0; r < _rows; r++)
            {
                float top = _y0 - r * _c, bottom = _y0 - (r + 1) * _c;
                if (r != _inRow) Wall(new Vector2(_x0, top), new Vector2(_x0, bottom));
                if (r != _outRow) Wall(new Vector2(_x0 + w, top), new Vector2(_x0 + w, bottom));
                for (int c = 0; c < _cols; c++)
                {
                    float x = _x0 + (c + 1) * _c;
                    if (c < _cols - 1 && _right[c, r]) Wall(new Vector2(x, top), new Vector2(x, bottom));
                    if (r < _rows - 1 && _down[c, r]) Wall(new Vector2(_x0 + c * _c, bottom), new Vector2(x, bottom));
                }
            }
        }

        /// <summary>Is this point in the open: a corridor, or one of the two pads?</summary>
        private bool Open(Vector2 p)
        {
            if (_startPad.Contains(p) || _endPad.Contains(p)) return true;
            float lx = p.x - _x0, ly = _y0 - p.y;
            if (lx < 0f || ly < 0f || lx >= _cols * _c || ly >= _rows * _c) return false;
            int c = Mathf.FloorToInt(lx / _c), r = Mathf.FloorToInt(ly / _c);
            float ox = lx - c * _c, oy = ly - r * _c, half = _t * 0.5f;
            bool nearLeft = ox < half, nearRight = ox > _c - half, nearTop = oy < half, nearBottom = oy > _c - half;
            if ((nearLeft || nearRight) && (nearTop || nearBottom)) return false; // the posts at the corners
            if (nearLeft && (c == 0 ? r != _inRow : _right[c - 1, r])) return false;
            if (nearRight && (c == _cols - 1 ? r != _outRow : _right[c, r])) return false;
            if (nearTop && (r == 0 || _down[c, r - 1])) return false;
            if (nearBottom && (r == _rows - 1 || _down[c, r])) return false;
            return true;
        }

        protected override void OnTick(float dt)
        {
            // Someone watching sees a torch sweeping to and fro across the maze.
            var p = new Vector2(_x0 + _cols * _c * 0.5f * (1f + Mathf.Sin(Elapsed * 0.5f)), _y0 - _rows * _c * 0.5f);
            if (CanAct && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m)) p = m;
            if (_torch != null)
            {
                _torch.anchoredPosition = p;
                _world.anchoredPosition = -p;
            }
            if (!CanAct) return;

            if (!_armed)
            {
                if (_startPad.Contains(p))
                {
                    _armed = true;
                    _armedAt = Elapsed;
                    _last = p;
                    _label.text = "FIND THE GOLD EXIT";
                }
                else if (Elapsed > Limit) Fail("NEVER STARTED");
                return;
            }

            float travelled = Vector2.Distance(_last, p);
            int steps = Mathf.Max(1, Mathf.CeilToInt(travelled / 3f));
            for (int s = 1; s <= steps; s++)
            {
                var q = Vector2.Lerp(_last, p, s / (float)steps);
                if (Open(q)) continue;
                UiKit.Dot(Area, "Touch", 16f, q, Palette.Red);
                Fail("TOUCHED A WALL");
                return;
            }
            _last = p;

            if (_endPad.Contains(p))
            {
                _label.text = "OUT OF THE MAZE";
                _label.color = Palette.Green;
                Finish(false, Ms(Elapsed - _armedAt));
            }
            else if (Elapsed > Limit) Fail("LOST IN THERE");
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
