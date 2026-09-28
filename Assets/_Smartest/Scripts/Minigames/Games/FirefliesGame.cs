using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Catching by touch: fireflies drift about the box and you sweep your cursor over each
    /// one to catch it, no clicking. Their paths come from the seed; from level six they
    /// also shy away when the cursor gets close. Ranked by time to catch them all.
    /// </summary>
    public class FirefliesGame : MinigameView
    {
        private const float Limit = 9f;
        private const float FleeRange = 130f;

        private struct Fly
        {
            public Vector2 Centre, Amp, Freq, Phase;
            public Vector2 Push;   // how far it has shied away from the cursor
            public bool Caught;
            public float CaughtAt;
            public RectTransform View;
            public Image Image;
        }

        private Fly[] _flies = new Fly[0];
        private float _radius;
        private float _fleeSpeed;
        private Rect _box;
        private int _caught;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int n;
            float speed;
            switch (Level)
            {
                case 1: n = 4; speed = 80f; _radius = 22f; break;
                case 2: n = 5; speed = 110f; _radius = 20f; break;
                case 3: n = 6; speed = 140f; _radius = 18f; break;
                case 4: n = 7; speed = 170f; _radius = 16f; break;
                default:
                    n = 8;
                    speed = Mathf.Min(260f, 190f + (Level - 5) * 10f);
                    _radius = Mathf.Max(12f, 15f - (Level - 5) * 0.5f);
                    break;
            }
            _fleeSpeed = Level >= 6 ? Mathf.Min(420f, 260f + (Level - 6) * 20f) : 0f;

            float w = Mathf.Min(1080f, size.x - 120f), h = size.y - 100f;
            var centre = new Vector2(0f, 20f);
            UiKit.Box(Area, "Night", new Vector2(w, h), centre, Palette.Panel);
            _box = new Rect(centre.x - w * 0.5f + _radius + 8f, centre.y - h * 0.5f + _radius + 8f,
                            w - 2f * (_radius + 8f), h - 2f * (_radius + 8f));

            _flies = new Fly[n];
            for (int i = 0; i < n; i++)
            {
                var amp = new Vector2(RandomRange(80f, _box.width * 0.3f), RandomRange(50f, _box.height * 0.3f));
                var c = new Vector2(RandomRange(_box.xMin + amp.x, _box.xMax - amp.x), RandomRange(_box.yMin + amp.y, _box.yMax - amp.y));
                // Frequencies that make the typical speed about `speed`.
                var freq = new Vector2(speed / amp.x * RandomRange(0.6f, 1f), speed / amp.y * RandomRange(0.3f, 0.6f));
                var phase = new Vector2(RandomRange(0f, Mathf.PI * 2f), RandomRange(0f, Mathf.PI * 2f));
                var img = UiKit.Dot(Area, "Fly" + i, _radius * 2f, c, Palette.Accent);
                _flies[i] = new Fly { Centre = c, Amp = amp, Freq = freq, Phase = phase, View = (RectTransform)img.transform, Image = img };
            }

            _label = UiKit.Label(Area, "Hint", "TOUCH THEM ALL", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        private Vector2 PathAt(in Fly f, float t) =>
            f.Centre + new Vector2(f.Amp.x * Mathf.Sin(f.Freq.x * t + f.Phase.x), f.Amp.y * Mathf.Sin(f.Freq.y * t + f.Phase.y));

        protected override void OnTick(float dt)
        {
            var cursor = Vector2.zero;
            bool haveCursor = CanAct && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out cursor);

            for (int i = 0; i < _flies.Length; i++)
            {
                ref var f = ref _flies[i];
                if (f.Caught)
                {
                    if (f.View.gameObject.activeSelf && Elapsed - f.CaughtAt > 0.12f) f.View.gameObject.SetActive(false);
                    continue;
                }

                var p = PathAt(f, Elapsed) + f.Push;
                if (_fleeSpeed > 0f && haveCursor)
                {
                    var away = p - cursor;
                    float d = away.magnitude;
                    if (d < FleeRange && d > 0.01f) f.Push += away / d * (_fleeSpeed * (1f - d / FleeRange) * dt);
                }
                f.Push *= Mathf.Max(0f, 1f - 0.6f * dt); // it drifts back to its path
                p = PathAt(f, Elapsed) + f.Push;
                // Kept inside the box; what the wall stops isn't kept as push.
                var clamped = new Vector2(Mathf.Clamp(p.x, _box.xMin, _box.xMax), Mathf.Clamp(p.y, _box.yMin, _box.yMax));
                f.Push += clamped - p;
                f.View.anchoredPosition = clamped;

                if (haveCursor && Vector2.Distance(cursor, clamped) <= _radius + 10f)
                {
                    f.Caught = true;
                    f.CaughtAt = Elapsed;
                    f.Image.color = Palette.Green;
                    _caught++;
                }
            }

            if (!CanAct) return;
            if (_caught == _flies.Length)
            {
                _label.text = "GOT THEM ALL";
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
    }
}
