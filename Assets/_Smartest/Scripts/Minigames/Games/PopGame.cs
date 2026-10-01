using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Hand and eye, nothing else. Dots appear one after another, the same dots in the same
    /// places at the same moments for everyone, and each shrinks away unless you click it.
    /// Ranked by total reaction time, so speed and aim both count.
    /// </summary>
    public class PopGame : MinigameView
    {
        private const float PopFlash = 0.12f;

        private struct Target
        {
            public Vector2 Pos;
            public float Spawn;
            public bool Popped;
            public float PoppedAt;
            public bool Gone;
            public RectTransform View;
            public Image Image;
        }

        private Target[] _targets = new Target[0];
        private float _radius;
        private float _life;
        private int _popped;
        private float _reactionSum;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int n;
            float interval;
            switch (Level)
            {
                case 1: n = 5; interval = 1.3f; _life = 1.9f; _radius = 46f; break;
                case 2: n = 7; interval = 1.05f; _life = 1.7f; _radius = 42f; break;
                case 3: n = 8; interval = 0.9f; _life = 1.55f; _radius = 38f; break;
                case 4: n = 10; interval = 0.8f; _life = 1.45f; _radius = 34f; break;
                default:
                    // Reaching a dot across the screen takes most people 0.8-1 s, so it never
                    // lives less than that, and they never arrive faster than you can keep up.
                    n = Mathf.Min(16, 11 + (Level - 5));
                    interval = Mathf.Max(0.62f, 0.72f - (Level - 5) * 0.02f);
                    _life = Mathf.Max(1.2f, 1.35f - (Level - 5) * 0.03f);
                    _radius = Mathf.Max(26f, 31f - (Level - 5));
                    break;
            }

            float halfW = size.x * 0.5f - _radius - 30f;
            float yMin = -size.y * 0.5f + 70f + _radius; // the hint lives below
            float yMax = size.y * 0.5f - _radius - 10f;

            _targets = new Target[n];
            Vector2 last = new Vector2(float.MaxValue, float.MaxValue);
            for (int i = 0; i < n; i++)
            {
                // Never right on top of the previous dot: two in one place read as one.
                Vector2 p;
                int tries = 0;
                do p = new Vector2(RandomRange(-halfW, halfW), RandomRange(yMin, yMax));
                while (Vector2.Distance(p, last) < _radius * 3f && ++tries < 12);
                last = p;

                var img = UiKit.Dot(Area, "Dot" + i, _radius * 2f, p, Palette.Accent);
                img.gameObject.SetActive(false);
                _targets[i] = new Target
                {
                    Pos = p,
                    Spawn = 0.3f + i * interval + RandomRange(-0.08f, 0.08f),
                    View = (RectTransform)img.transform,
                    Image = img
                };
            }

            _label = UiKit.Label(Area, "Hint", "CLICK THE DOTS", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            Progress("POPPED", 0, n);
        }

        protected override void OnTick(float dt)
        {
            for (int i = 0; i < _targets.Length; i++)
            {
                ref var t = ref _targets[i];
                if (t.Gone) continue;
                if (t.Popped)
                {
                    if (Elapsed - t.PoppedAt >= PopFlash) { t.Gone = true; t.View.gameObject.SetActive(false); }
                    continue;
                }
                float age = Elapsed - t.Spawn;
                if (age < 0f) continue;
                if (!t.View.gameObject.activeSelf) t.View.gameObject.SetActive(true);

                // Pops in, then shrinks as its time runs out.
                float scale = age < 0.08f ? age / 0.08f : 1f - 0.4f * (age / _life);
                t.View.localScale = Vector3.one * Mathf.Max(0.05f, scale);

                if (age >= _life)
                {
                    t.Gone = true;
                    if (CanAct)
                    {
                        t.Image.color = Palette.Red;
                        t.View.localScale = Vector3.one * 0.6f;
                        Fail("ONE GOT AWAY");
                        return;
                    }
                    t.View.gameObject.SetActive(false);
                }
            }

            if (Demo) PlayDemo();

            if (!CanAct || !KeyInput.MousePressed()) return;
            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var local)) return;
            Click(local);
        }

        /// <summary>The rule card's demo: the hand goes to each dot as it comes up and clicks it a beat later.</summary>
        private void PlayDemo()
        {
            for (int i = 0; i < _targets.Length; i++)
            {
                var t = _targets[i];
                if (t.Popped || t.Gone) continue;
                float age = Elapsed - t.Spawn;
                if (age < 0.1f) return; // nothing up yet, or only just: nobody's that quick
                PointAt(t.Pos);
                if (age >= 0.35f)
                {
                    TapAt(t.Pos);
                    Click(t.Pos);
                }
                return;
            }
        }

        /// <summary>A click, the player's or the demo's: it pops the oldest live dot under it.</summary>
        private void Click(Vector2 local)
        {
            if (!CanMove) return;
            // The oldest live dot under the cursor. A click on nothing costs nothing.
            for (int i = 0; i < _targets.Length; i++)
            {
                ref var t = ref _targets[i];
                if (t.Popped || t.Gone) continue;
                float age = Elapsed - t.Spawn;
                if (age < 0f) break; // spawn times are in order: nothing later is up yet
                float hit = _radius * Mathf.Max(0.6f, t.View.localScale.x) + 6f;
                if (Vector2.Distance(local, t.Pos) > hit) continue;

                t.Popped = true;
                t.PoppedAt = Elapsed;
                t.Image.color = Palette.Green;
                _reactionSum += age;
                _popped++;
                Progress("POPPED", _popped, _targets.Length);
                if (_popped == _targets.Length)
                {
                    _label.text = "ALL POPPED";
                    _label.color = Palette.Green;
                    Finish(false, Ms(_reactionSum));
                }
                return;
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
