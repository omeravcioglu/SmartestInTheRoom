using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Balls fall and a basket follows your mouse along the bottom: catch them all. The rain
    /// comes from the seed, so everyone faces the same one, and the ranking is how close to
    /// the middle of the basket you caught them on average. Good Catch adds red balls that
    /// have to be let past.
    /// </summary>
    public class CatchGame : MinigameView
    {
        private const float BallR = 20f;
        private const float BasketH = 26f;
        /// <summary>Faster than this, the next ball couldn't be reached from the last one.</summary>
        private const float ReachSpeed = 1400f;

        private struct Drop
        {
            public float X;
            public float Spawn;
            public bool Red;
            public bool Judged;
            public bool Gone;
            public RectTransform View;
            public Image Image;
        }

        private Drop[] _drops = new Drop[0];
        private RectTransform _basket;
        private TMP_Text _label;
        private float _basketW;
        private float _fallSpeed;
        private float _spawnY;
        private float _catchY;
        private float _bottom;
        private float _halfW;
        private float _basketX;
        private float _offSum;
        private int _caught;
        private int _golds;

        /// <summary>Share of red balls: none here, some in Good Catch.</summary>
        protected virtual float RedShare => 0f;
        protected virtual string StartHint => "CATCH THEM ALL";

        protected override void Build()
        {
            var size = AreaSize;
            int n;
            float interval;
            switch (Level)
            {
                case 1: n = 5; _fallSpeed = 220f; interval = 1.2f; _basketW = 170f; break;
                case 2: n = 7; _fallSpeed = 260f; interval = 1.0f; _basketW = 150f; break;
                case 3: n = 8; _fallSpeed = 300f; interval = 0.85f; _basketW = 135f; break;
                case 4: n = 10; _fallSpeed = 340f; interval = 0.75f; _basketW = 120f; break;
                default:
                    n = Mathf.Min(16, 11 + (Level - 5));
                    _fallSpeed = Mathf.Min(520f, 380f + (Level - 5) * 20f);
                    interval = Mathf.Max(0.5f, 0.65f - (Level - 5) * 0.02f);
                    _basketW = Mathf.Max(90f, 110f - (Level - 5) * 4f);
                    break;
            }
            // Red balls are extra: the golds keep their rhythm and the reds come in between.
            if (RedShare > 0f) n += Mathf.RoundToInt(n * RedShare);

            _halfW = Mathf.Min(540f, size.x * 0.5f - 40f);
            float top = size.y * 0.5f - 10f;
            _bottom = -size.y * 0.5f + 64f; // the hint lives below
            UiKit.Box(Area, "Sky", new Vector2(_halfW * 2f, top - _bottom), new Vector2(0f, (top + _bottom) * 0.5f), Palette.PanelRaised);
            _spawnY = top - BallR - 8f;
            float basketY = _bottom + 26f;
            _catchY = basketY + BasketH * 0.5f;

            _drops = new Drop[n];
            float spacing = interval * (RedShare > 0f ? 0.8f : 1f);
            float lastX = 0f, lastGoldAt = 0f;
            int redRun = 0;
            for (int i = 0; i < n; i++)
            {
                float spawn = 0.6f + i * spacing;
                // Never more than two reds in a row, and the first ball is always gold.
                bool red = i > 0 && redRun < 2 && RandomRange(0f, 1f) < RedShare;
                redRun = red ? redRun + 1 : 0;
                float x;
                if (red)
                {
                    // Away from where the basket sits, so a red can always be let past.
                    int tries = 0;
                    do x = RandomRange(-_halfW + 50f, _halfW - 50f);
                    while (Mathf.Abs(x - lastX) < _basketW && ++tries < 12);
                }
                else
                {
                    // Never further from the last gold than a quick hand travels in the time.
                    float reach = Mathf.Max(200f, (spawn - lastGoldAt) * ReachSpeed);
                    x = RandomRange(Mathf.Max(-_halfW + 50f, lastX - reach), Mathf.Min(_halfW - 50f, lastX + reach));
                    lastX = x;
                    lastGoldAt = spawn;
                }

                var img = UiKit.Dot(Area, "Ball" + i, BallR * 2f, new Vector2(x, _spawnY), red ? Palette.Red : Palette.Accent);
                // Red is marked, so it isn't told from gold by colour alone.
                if (red) UiKit.Marker(img.transform, "Mark", UiKit.Mark.Diamond, BallR * 1.15f, Vector2.zero, Palette.OnRed);
                img.gameObject.SetActive(false);
                _drops[i] = new Drop { X = x, Red = red, Spawn = spawn, View = (RectTransform)img.transform, Image = img };
                if (!red) _golds++;
            }
            Progress("CAUGHT", 0, _golds);

            _basketX = 0f;
            // The basket's rim is the catching line.
            _basket = (RectTransform)UiKit.Art(Area, "Basket", "basket", new Vector2(_basketW + 12f, BasketH + 10f),
                new Vector2(0f, _catchY - (BasketH + 10f) * 0.5f + 2f)).transform;
            _label = UiKit.Label(Area, "Hint", StartHint, 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        protected override void OnTick(float dt)
        {
            MoveBasket(dt);

            bool allDone = true;
            for (int i = 0; i < _drops.Length; i++)
            {
                ref var d = ref _drops[i];
                if (d.Gone) continue;
                allDone = false;
                float age = Elapsed - d.Spawn;
                if (age < 0f) continue;
                if (!d.View.gameObject.activeSelf) d.View.gameObject.SetActive(true);
                float y = _spawnY - _fallSpeed * age;
                d.View.anchoredPosition = new Vector2(d.X, y);
                d.View.localScale = Vector3.one * Mathf.Min(1f, age / 0.1f);

                if (!d.Judged && y - BallR <= _catchY)
                {
                    d.Judged = true;
                    float off = Mathf.Abs(d.X - _basketX);
                    bool inBasket = off <= _basketW * 0.5f + BallR * 0.5f;
                    if (d.Red)
                    {
                        if (inBasket && CanAct) { Fail("CAUGHT A RED"); return; }
                    }
                    else if (inBasket)
                    {
                        d.Gone = true;
                        d.View.gameObject.SetActive(false);
                        _offSum += off;
                        _caught++;
                        Progress("CAUGHT", _caught, _golds);
                        continue;
                    }
                    else if (CanAct)
                    {
                        d.Image.color = Palette.Red;
                        Fail("DROPPED ONE");
                        return;
                    }
                }
                // Anything not caught falls on through and away.
                if (y < _bottom - BallR) { d.Gone = true; d.View.gameObject.SetActive(false); }
            }

            if (allDone && CanMove)
            {
                _label.text = "ALL CAUGHT";
                _label.color = Palette.Green;
                // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(_offSum / Mathf.Max(1, _caught) * 10f));
            }
        }

        private void MoveBasket(float dt)
        {
            float limit = _halfW - _basketW * 0.5f - 4f;
            if (CanAct)
            {
                if (UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m)) _basketX = Mathf.Clamp(m.x, -limit, limit);
            }
            else if (!Interactive)
            {
                // Someone watching sees the basket go after the next gold ball, and keep out from
                // under a red one that's about to land.
                float want = _basketX;
                for (int i = 0; i < _drops.Length; i++)
                {
                    if (_drops[i].Judged || _drops[i].Red || Elapsed < _drops[i].Spawn) continue;
                    want = Mathf.Clamp(_drops[i].X, -limit, limit);
                    break;
                }
                _basketX = Mathf.MoveTowards(_basketX, ClearOfReds(want, limit), 900f * dt);
            }
            _basket.anchoredPosition = new Vector2(_basketX, _basket.anchoredPosition.y);
            if (Demo) PointAt(_basket.anchoredPosition); // the basket goes where the mouse is: the demo's hand is on it
        }

        /// <summary>
        /// Where the basket can head without a red ball landing in it: for each red that's nearly
        /// down, it stays on its own side of it (the far side if its own side has no room).
        /// </summary>
        private float ClearOfReds(float want, float limit)
        {
            float clear = _basketW * 0.5f + BallR * 2f; // its middle a ball's width past the rim: a plain miss
            for (int i = 0; i < _drops.Length; i++)
            {
                var d = _drops[i];
                if (!d.Red || d.Judged || Elapsed < d.Spawn) continue;
                float landsIn = (_spawnY - _fallSpeed * (Elapsed - d.Spawn) - BallR - _catchY) / _fallSpeed;
                if (landsIn > 0.45f) continue; // far enough up to cross under it first
                bool left = _basketX < d.X;
                if (left && d.X - clear < -limit) left = false;
                else if (!left && d.X + clear > limit) left = true;
                want = left ? Mathf.Min(want, d.X - clear) : Mathf.Max(want, d.X + clear);
            }
            return Mathf.Clamp(want, -limit, limit);
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }

    /// <summary>Catch, with red balls mixed in that must fall past the basket.</summary>
    public class GoodCatchGame : CatchGame
    {
        protected override float RedShare => Level <= 2 ? 0.3f : 0.4f;
        protected override string StartHint => "GOLD IN, RED OUT";
    }
}
