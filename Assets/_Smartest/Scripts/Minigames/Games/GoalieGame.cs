using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Shots fly in from the right at angles, and a glove on the goal line follows your mouse
    /// up and down. Stop them all. Everyone faces the same shots from the same seed; the
    /// ranking is how close to the middle of the glove you stopped them on average.
    /// </summary>
    public class GoalieGame : MinigameView
    {
        private const float ShotR = 18f;
        private const float GloveW = 26f;
        /// <summary>Faster than this, the glove couldn't get from one shot to the next.</summary>
        private const float ReachSpeed = 1200f;

        private struct Shot
        {
            public float Y0, Y1;   // where it starts, and where it would cross the goal line
            public float Start;
            public bool Judged;
            public bool Gone;
            public RectTransform View;
            public Image Image;
        }

        private Shot[] _shots = new Shot[0];
        private RectTransform _glove;
        private TMP_Text _label;
        private float _gloveH;
        private float _speed;
        private float _x0;       // where shots start
        private float _gloveX;   // the plane they're stopped on
        private float _yLimit;
        private float _centreY;
        private float _gloveY;
        private float _offSum;
        private int _stopped;

        protected override void Build()
        {
            var size = AreaSize;
            int n;
            float interval;
            switch (Level)
            {
                case 1: n = 5; _speed = 380f; interval = 1.2f; _gloveH = 150f; break;
                case 2: n = 7; _speed = 450f; interval = 1.0f; _gloveH = 135f; break;
                case 3: n = 8; _speed = 520f; interval = 0.9f; _gloveH = 120f; break;
                case 4: n = 10; _speed = 580f; interval = 0.8f; _gloveH = 110f; break;
                default:
                    n = Mathf.Min(15, 11 + (Level - 5));
                    _speed = Mathf.Min(800f, 640f + (Level - 5) * 25f);
                    interval = Mathf.Max(0.55f, 0.7f - (Level - 5) * 0.02f);
                    _gloveH = Mathf.Max(80f, 100f - (Level - 5) * 3f);
                    break;
            }

            float halfW = Mathf.Min(540f, size.x * 0.5f - 40f);
            float top = size.y * 0.5f - 10f, bottom = -size.y * 0.5f + 64f; // the hint lives below
            _centreY = (top + bottom) * 0.5f;
            UiKit.Box(Area, "Pitch", new Vector2(halfW * 2f, top - bottom), new Vector2(0f, _centreY), Palette.PanelRaised);
            float goalX = -halfW + 40f;
            // The net fills the strip behind the line.
            var net = UiKit.Art(Area, "Net", "net", new Vector2(goalX - 4f - (-halfW + 4f), top - bottom - 8f),
                new Vector2((goalX - 4f + (-halfW + 4f)) * 0.5f, _centreY));
            net.type = Image.Type.Tiled;
            UiKit.Fill(Area, "GoalLine", new Vector2(8f, top - bottom - 8f), new Vector2(goalX, _centreY), Palette.Ink);
            _gloveX = goalX + 34f;
            _x0 = halfW - 30f;
            _yLimit = (top - bottom) * 0.5f - 40f;

            _shots = new Shot[n];
            float lastY = 0f;
            for (int i = 0; i < n; i++)
            {
                // Never further from the last shot than the glove can travel in the time.
                float reach = Mathf.Max(120f, interval * ReachSpeed);
                float y1 = RandomRange(Mathf.Max(-_yLimit, lastY - reach), Mathf.Min(_yLimit, lastY + reach));
                lastY = y1;
                float y0 = RandomRange(-_yLimit, _yLimit);
                var img = UiKit.Art(Area, "Shot" + i, "ball", new Vector2(ShotR * 2f, ShotR * 2f), new Vector2(_x0, _centreY + y0), Palette.Accent);
                img.gameObject.SetActive(false);
                _shots[i] = new Shot { Y0 = y0, Y1 = y1, Start = 0.6f + i * interval, View = (RectTransform)img.transform, Image = img };
            }

            _gloveY = 0f;
            _glove = (RectTransform)UiKit.Art(Area, "Glove", "glove", new Vector2(GloveW + 4f, _gloveH), new Vector2(_gloveX, _centreY)).transform;
            _label = UiKit.Label(Area, "Hint", "STOP EVERY SHOT", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            Progress("SAVED", 0, n);
        }

        /// <summary>Where a shot is, `age` seconds after it was struck (it keeps going past the glove).</summary>
        private Vector2 ShotAt(in Shot s, float age)
        {
            float travel = _x0 - _gloveX;
            float u = age * _speed / travel;
            return new Vector2(_x0 - travel * u, _centreY + Mathf.LerpUnclamped(s.Y0, s.Y1, u));
        }

        protected override void OnTick(float dt)
        {
            MoveGlove(dt);

            bool allDone = true;
            for (int i = 0; i < _shots.Length; i++)
            {
                ref var s = ref _shots[i];
                if (s.Gone) continue;
                allDone = false;
                float age = Elapsed - s.Start;
                if (age < 0f) continue;
                if (!s.View.gameObject.activeSelf) s.View.gameObject.SetActive(true);
                var p = ShotAt(s, age);
                s.View.anchoredPosition = p;
                s.View.localRotation = Quaternion.Euler(0f, 0f, age * 400f); // struck with spin

                if (!s.Judged && p.x - ShotR <= _gloveX + GloveW * 0.5f)
                {
                    s.Judged = true;
                    float off = Mathf.Abs(p.y - (_centreY + _gloveY));
                    if (off <= _gloveH * 0.5f + ShotR * 0.5f)
                    {
                        s.Gone = true;
                        s.View.gameObject.SetActive(false);
                        _offSum += off;
                        _stopped++;
                        Progress("SAVED", _stopped, _shots.Length);
                        continue;
                    }
                    if (CanAct)
                    {
                        s.Image.color = Palette.Red;
                        Fail("GOAL");
                        return;
                    }
                }
                if (p.x < _gloveX - 60f) { s.Gone = true; s.View.gameObject.SetActive(false); }
            }

            if (allDone && CanMove)
            {
                _label.text = "CLEAN SHEET";
                _label.color = Palette.Green;
                // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(_offSum / Mathf.Max(1, _stopped) * 10f));
            }
        }

        private void MoveGlove(float dt)
        {
            float limit = _yLimit + 20f - _gloveH * 0.5f;
            if (CanAct)
            {
                if (UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m)) _gloveY = Mathf.Clamp(m.y - _centreY, -limit, limit);
            }
            else if (!Interactive)
            {
                // Someone watching sees the glove go for the next shot.
                for (int i = 0; i < _shots.Length; i++)
                {
                    if (_shots[i].Judged || Elapsed < _shots[i].Start) continue;
                    _gloveY = Mathf.MoveTowards(_gloveY, Mathf.Clamp(_shots[i].Y1, -limit, limit), 800f * dt);
                    break;
                }
            }
            _glove.anchoredPosition = new Vector2(_gloveX, _centreY + _gloveY);
            if (Demo) PointAt(_glove.anchoredPosition); // the glove goes where the mouse is: the demo's hand is on it
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
