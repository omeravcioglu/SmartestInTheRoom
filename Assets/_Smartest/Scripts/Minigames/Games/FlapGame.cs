using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The one everyone already knows: click to flap, thread the gaps. The walls come from the
    /// seed, so everyone flies the same course, and nothing moves until your first flap, so
    /// the reaction to GO costs nobody anything. Ranked by the narrowest scrape.
    /// </summary>
    public class FlapGame : MinigameView
    {
        private const float PhysicsStep = 1f / 240f;
        private const float BirdX = -400f;
        private const float BirdR = 16f;
        private const float WallW = 70f;
        private const float Spacing = 360f;
        private const float FirstWall = 220f;
        private const float MaxFall = 650f;
        private const float TakeOffBy = 3f;

        private float[] _gapY = new float[0];
        private RectTransform[] _tops = new RectTransform[0];
        private RectTransform[] _bottoms = new RectTransform[0];
        private float _gapH;
        private float _speed;
        private float _gravity;
        private float _flap;
        private float _top;
        private float _bottom;
        private RectTransform _bird;
        private Image _birdImage;
        private TMP_Text _label;
        private float _y;
        private float _vy;
        private bool _flying;
        private float _startedAt;
        private float _acc;
        private float _closest = float.MaxValue;

        /// <summary>Bigger clearance is better, so the worst result is none at all.</summary>
        public override int WorstMetric => 0;

        protected override void Build()
        {
            var size = AreaSize;
            int n;
            switch (Level)
            {
                // The first two levels fall slower and flap softer: the warm-up is for
                // finding the rhythm.
                case 1: n = 4; _gapH = 230f; _speed = 240f; _gravity = 1000f; _flap = 380f; break;
                case 2: n = 5; _gapH = 205f; _speed = 260f; _gravity = 1100f; _flap = 400f; break;
                case 3: n = 6; _gapH = 185f; _speed = 285f; _gravity = 1300f; _flap = 440f; break;
                case 4: n = 7; _gapH = 168f; _speed = 305f; _gravity = 1300f; _flap = 440f; break;
                default:
                    n = Mathf.Min(10, 8 + (Level - 5) / 2);
                    _gapH = Mathf.Max(128f, 152f - (Level - 5) * 4f);
                    _speed = Mathf.Min(400f, 330f + (Level - 5) * 12f);
                    _gravity = 1300f;
                    _flap = 440f;
                    break;
            }

            float halfW = Mathf.Min(560f, size.x * 0.5f - 30f);
            _top = size.y * 0.5f - 10f;
            _bottom = -size.y * 0.5f + 64f; // the hint lives below
            var sky = UiKit.Box(Area, "Sky", new Vector2(halfW * 2f, _top - _bottom),
                new Vector2(0f, (_top + _bottom) * 0.5f), Palette.PanelRaised);
            // The walls slide in and out at the sky's edges instead of popping into view, and
            // never over the sky's own ink edge.
            sky.gameObject.AddComponent<RectMask2D>().padding = new Vector4(4f, 4f, 4f, 4f);
            var world = UiKit.Node(sky.transform, "World", Vector2.zero, new Vector2(0f, -(_top + _bottom) * 0.5f));

            _top -= 4f;    // inside the sky's ink border
            _bottom += 4f;

            _gapY = new float[n];
            _tops = new RectTransform[n];
            _bottoms = new RectTransform[n];
            float lo = _bottom + _gapH * 0.5f + 30f, hi = _top - _gapH * 0.5f - 30f;
            for (int i = 0; i < n; i++)
            {
                // Never a climb or a dive too big to make between two walls.
                float prev = i == 0 ? 0f : _gapY[i - 1];
                _gapY[i] = Mathf.Clamp(prev + RandomRange(-170f, 170f), lo, hi);
                if (i == 0) _gapY[i] = RandomRange(lo * 0.5f, hi * 0.5f);

                float gapTop = _gapY[i] + _gapH * 0.5f, gapBottom = _gapY[i] - _gapH * 0.5f;
                float x = WallX(i, 0f);
                _tops[i] = Pillar(world, "WallTop" + i, x, (_top + 20f + gapTop) * 0.5f, _top - gapTop + 20f);
                _bottoms[i] = Pillar(world, "WallBottom" + i, x, (gapBottom + _bottom - 20f) * 0.5f, gapBottom - _bottom + 20f);
            }

            _y = 0f;
            _birdImage = UiKit.Dot(world, "Bird", BirdR * 2f, new Vector2(BirdX, _y), Palette.Accent);
            _bird = (RectTransform)_birdImage.transform;
            // Someone already out watches the course; they have no bird in it.
            if (!Interactive) _bird.gameObject.SetActive(false);

            _label = UiKit.Label(Area, "Hint", "CLICK TO FLAP", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        private float WallX(int i, float t) => FirstWall + i * Spacing - _speed * t;

        /// <summary>A red pillar with an ink edge. Flat, so tall and short ones look alike.</summary>
        private static RectTransform Pillar(Transform parent, string name, float x, float y, float height)
        {
            var root = UiKit.Node(parent, name, new Vector2(WallW, height), new Vector2(x, y));
            UiKit.Fill(root, "Edge", new Vector2(WallW, height), Vector2.zero, Palette.Ink);
            UiKit.Fill(root, "Paint", new Vector2(WallW - 8f, height - 8f), Vector2.zero, Palette.Red);
            return root;
        }

        protected override void OnTick(float dt)
        {
            if (!Interactive && !_flying) { _flying = true; _startedAt = 0f; }

            if (CanAct && KeyInput.MousePressed())
            {
                if (!_flying) { _flying = true; _startedAt = Elapsed; }
                _vy = _flap;
            }

            if (!_flying)
            {
                // Hovering, waiting for the first flap.
                _bird.anchoredPosition = new Vector2(BirdX, 10f * Mathf.Sin(Elapsed * 5f));
                if (CanAct && Elapsed > TakeOffBy) Fail("NEVER TOOK OFF");
                return;
            }

            float t = Elapsed - _startedAt;
            for (int i = 0; i < _gapY.Length; i++)
            {
                float x = WallX(i, t);
                _tops[i].anchoredPosition = new Vector2(x, _tops[i].anchoredPosition.y);
                _bottoms[i].anchoredPosition = new Vector2(x, _bottoms[i].anchoredPosition.y);
            }
            if (!CanAct) return;

            _acc += dt;
            while (_acc >= PhysicsStep)
            {
                _acc -= PhysicsStep;
                _vy = Mathf.Max(-MaxFall, _vy - _gravity * PhysicsStep);
                _y += _vy * PhysicsStep;
            }
            _bird.anchoredPosition = new Vector2(BirdX, _y);

            if (_y + BirdR > _top || _y - BirdR < _bottom) { Crash(); return; }

            // The bird as a slightly forgiving square against each wall it overlaps.
            const float Body = BirdR * 0.9f;
            for (int i = 0; i < _gapY.Length; i++)
            {
                float x = WallX(i, t);
                if (Mathf.Abs(x - BirdX) >= WallW * 0.5f + Body) continue;
                float clear = Mathf.Min(_gapY[i] + _gapH * 0.5f - (_y + BirdR), (_y - BirdR) - (_gapY[i] - _gapH * 0.5f));
                if (clear < _closest) _closest = clear;
                if (_y + Body > _gapY[i] + _gapH * 0.5f || _y - Body < _gapY[i] - _gapH * 0.5f) { Crash(); return; }
            }

            if (WallX(_gapY.Length - 1, t) + WallW * 0.5f < BirdX - BirdR)
            {
                _label.text = "MADE IT";
                _label.color = Palette.Green;
                float closest = _closest == float.MaxValue ? _gapH * 0.5f : _closest;
                // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(Mathf.Max(0f, closest) * 10f));
            }
        }

        private void Crash()
        {
            _birdImage.color = Palette.Red;
            Fail("CRASHED");
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, 0);
        }
    }
}
