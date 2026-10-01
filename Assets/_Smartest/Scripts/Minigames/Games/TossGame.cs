using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Things are thrown up from the floor in arcs, sometimes two or three at once, and you
    /// click each one before it lands. The throws come from the seed; they're slowest at the
    /// top of the arc, which is where the calm players take them. Ranked by total reaction.
    /// </summary>
    public class TossGame : MinigameView
    {
        private const float Gravity = 900f;

        private struct Throw
        {
            public float X0, VX, VY, Start;
            public bool Hit;
            public float HitAt;
            public bool Gone;
            public RectTransform View;
            public Image Image;
        }

        private readonly List<Throw> _throws = new List<Throw>();
        private float _r;
        private float _floorY;
        private float _halfW;
        private float _reactionSum;
        private int _hits;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int n, maxGroup;
            float interval;
            switch (Level)
            {
                case 1: n = 5; interval = 1.4f; _r = 36f; maxGroup = 1; break;
                case 2: n = 7; interval = 1.1f; _r = 32f; maxGroup = 1; break;
                case 3: n = 8; interval = 1.0f; _r = 30f; maxGroup = 2; break;
                case 4: n = 10; interval = 0.9f; _r = 28f; maxGroup = 2; break;
                default:
                    n = Mathf.Min(16, 11 + (Level - 5));
                    interval = Mathf.Max(0.7f, 0.85f - (Level - 5) * 0.02f);
                    _r = Mathf.Max(22f, 26f - (Level - 5) * 0.5f);
                    maxGroup = 3;
                    break;
            }

            _halfW = Mathf.Min(540f, size.x * 0.5f - 40f);
            float top = size.y * 0.5f - 10f;
            _floorY = -size.y * 0.5f + 70f; // the hint lives below
            UiKit.Box(Area, "Sky", new Vector2(_halfW * 2f, top - _floorY + 20f), new Vector2(0f, (top + _floorY - 20f) * 0.5f), Palette.PanelRaised);
            UiKit.Fill(Area, "Floor", new Vector2(_halfW * 2f - 8f, 8f), new Vector2(0f, _floorY), Palette.Ink);
            float maxApex = top - _floorY - _r - 16f;

            // Groups of one to maxGroup, launched together, one group per interval.
            float t = 0.5f;
            int made = 0;
            while (made < n)
            {
                int group = Mathf.Min(n - made, RandomRange(1, maxGroup + 1));
                for (int g = 0; g < group; g++, made++)
                {
                    float apex = RandomRange(maxApex * 0.6f, maxApex);
                    float vy = Mathf.Sqrt(2f * Gravity * apex);
                    float x0 = RandomRange(-_halfW + 80f, _halfW - 80f);
                    // Drifts toward the middle rather than out of the box.
                    float vx = RandomRange(0f, 120f) * (x0 > 0f ? -1f : 1f);
                    var img = UiKit.Art(Area, "Throw" + made, "coin", new Vector2(_r * 2f, _r * 2f), new Vector2(x0, _floorY), Palette.Accent);
                    img.gameObject.SetActive(false);
                    _throws.Add(new Throw { X0 = x0, VX = vx, VY = vy, Start = t, View = (RectTransform)img.transform, Image = img });
                }
                t += interval * (group > 1 ? 1.4f : 1f);
            }

            _label = UiKit.Label(Area, "Hint", "CLICK THEM IN THE AIR", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            Progress("HIT", 0, n);
        }

        private Vector2 PositionAt(in Throw k, float age) =>
            new Vector2(k.X0 + k.VX * age, _floorY + k.VY * age - 0.5f * Gravity * age * age);

        protected override void OnTick(float dt)
        {
            for (int i = 0; i < _throws.Count; i++)
            {
                var k = _throws[i];
                if (k.Gone) continue;
                if (k.Hit)
                {
                    if (Elapsed - k.HitAt > 0.12f) { k.Gone = true; k.View.gameObject.SetActive(false); _throws[i] = k; }
                    continue;
                }
                float age = Elapsed - k.Start;
                if (age < 0f) continue;
                if (!k.View.gameObject.activeSelf) k.View.gameObject.SetActive(true);
                k.View.anchoredPosition = PositionAt(k, age);
                // Flipping end over end on the way up and down.
                k.View.localScale = new Vector3(Mathf.Max(0.15f, Mathf.Abs(Mathf.Cos(age * 7f + i))), 1f, 1f);

                // Back down on the floor without being caught.
                if (age > 2f * k.VY / Gravity)
                {
                    k.Gone = true;
                    _throws[i] = k;
                    if (CanAct)
                    {
                        k.Image.color = Palette.Red;
                        Fail("ONE LANDED");
                        return;
                    }
                    k.View.gameObject.SetActive(false);
                }
            }

            if (Demo) PlayDemo();

            if (!CanAct || !KeyInput.MousePressed()) return;
            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var click)) return;
            Hit(click);
        }

        /// <summary>
        /// The rule card's demo: the hand picks up each throw on its way up, the one landing
        /// first first, follows it, and clicks it as it slows towards the top.
        /// </summary>
        private void PlayDemo()
        {
            int next = -1;
            float lands = float.MaxValue;
            for (int i = 0; i < _throws.Count; i++)
            {
                var k = _throws[i];
                if (k.Hit || k.Gone || Elapsed < k.Start) continue;
                float down = k.Start + 2f * k.VY / Gravity;
                if (down < lands) { lands = down; next = i; }
            }
            if (next < 0) return;
            float age = Elapsed - _throws[next].Start;
            if (age < 0.3f) return;
            var at = PositionAt(_throws[next], age);
            PointAt(at);
            if (age < 0.6f) return;
            TapAt(at);
            Hit(at);
        }

        /// <summary>A click, the player's or the demo's: it hits the nearest throw in the air under it.</summary>
        private void Hit(Vector2 click)
        {
            if (!CanMove) return;
            // The nearest one in the air under the click.
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < _throws.Count; i++)
            {
                var k = _throws[i];
                if (k.Hit || k.Gone || Elapsed < k.Start) continue;
                float d = Vector2.Distance(click, PositionAt(k, Elapsed - k.Start));
                if (d <= _r + 8f && d < bestD) { best = i; bestD = d; }
            }
            if (best < 0) return;

            var hit = _throws[best];
            hit.Hit = true;
            hit.HitAt = Elapsed;
            hit.Image.color = Palette.Green;
            hit.View.localScale = Vector3.one * 1.15f; // caught face on
            _throws[best] = hit;
            _reactionSum += Elapsed - hit.Start;
            _hits++;
            Progress("HIT", _hits, _throws.Count);
            if (_hits == _throws.Count)
            {
                _label.text = "NOTHING LANDED";
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
