using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The duel. Nothing, nothing, nothing, then a target somewhere, and you click it. Click
    /// with nothing on screen and you're out for drawing early. From level four a red target
    /// sometimes shows first: hold your fire. Ranked by total reaction time.
    /// </summary>
    public class QuickDrawGame : MinigameView
    {
        private const float RedShow = 0.7f;

        private struct Draw
        {
            public float Delay;
            public Vector2 Pos;
            public bool Red;
        }

        private Draw[] _draws = new Draw[0];
        private int _index;
        private float _appearAt;
        private bool _showing;
        private float _shownAt;
        private float _window;
        private float _r;
        private float _reactionSum;
        private int _golds;
        private int _hits;
        private RectTransform _target;
        private Image _targetImage;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int golds, reds;
            float minDelay, maxDelay;
            switch (Level)
            {
                case 1: golds = 3; reds = 0; minDelay = 1.0f; maxDelay = 2.2f; _window = 1.5f; _r = 50f; break;
                case 2: golds = 4; reds = 0; minDelay = 0.9f; maxDelay = 2.6f; _window = 1.2f; _r = 44f; break;
                case 3: golds = 5; reds = 0; minDelay = 0.8f; maxDelay = 2.6f; _window = 1.0f; _r = 38f; break;
                // Waits shorten as reds are added, so the slowest possible run still fits the level.
                case 4: golds = 5; reds = 1; minDelay = 0.8f; maxDelay = 2.4f; _window = 0.9f; _r = 34f; break;
                default:
                    golds = 5; reds = 2; minDelay = 0.6f; maxDelay = 2.0f;
                    _window = Mathf.Max(0.6f, 0.8f - (Level - 5) * 0.03f);
                    _r = Mathf.Max(24f, 30f - (Level - 5));
                    break;
            }

            float halfW = size.x * 0.5f - _r - 40f, yMin = -size.y * 0.5f + 70f + _r, yMax = size.y * 0.5f - _r - 10f;
            _draws = new Draw[golds + reds];
            // Reds go before golds at random, never last and never first.
            var redAt = new bool[_draws.Length];
            for (int k = 0; k < reds; k++)
            {
                int slot;
                int tries = 0;
                do slot = RandomRange(1, _draws.Length - 1); while (redAt[slot] && ++tries < 10);
                redAt[slot] = true;
            }
            for (int i = 0; i < _draws.Length; i++)
                _draws[i] = new Draw
                {
                    Delay = RandomRange(minDelay, maxDelay),
                    Pos = new Vector2(RandomRange(-halfW, halfW), RandomRange(yMin, yMax)),
                    Red = redAt[i]
                };

            _targetImage = UiKit.Dot(Area, "Target", _r * 2f, Vector2.zero, Palette.Accent);
            _target = (RectTransform)_targetImage.transform;
            _target.gameObject.SetActive(false);
            _appearAt = _draws[0].Delay;
            _label = UiKit.Label(Area, "Hint", "WAIT FOR IT", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            _golds = golds;
            Progress("HIT", 0, _golds);
        }

        private void Next()
        {
            _showing = false;
            _target.gameObject.SetActive(false);
            _label.text = "WAIT FOR IT";
            _index++;
            if (_index < _draws.Length) _appearAt = Elapsed + _draws[_index].Delay;
        }

        protected override void OnTick(float dt)
        {
            if (_index >= _draws.Length) return;
            var d = _draws[_index];

            if (!_showing && Elapsed >= _appearAt)
            {
                _showing = true;
                _shownAt = Elapsed;
                _target.anchoredPosition = d.Pos;
                _targetImage.color = d.Red ? Palette.Red : Palette.Accent;
                _target.gameObject.SetActive(true);
                _label.text = d.Red ? "HOLD YOUR FIRE" : "DRAW!";
            }
            if (_showing && d.Red && Elapsed - _shownAt >= RedShow) { Next(); return; }

            if (!CanAct)
            {
                // Someone watching sees each gold one taken after a fair reaction.
                if (_showing && !d.Red && Elapsed - _shownAt >= 0.35f) Next();
                return;
            }
            if (_showing && !d.Red && Elapsed - _shownAt > _window) { Fail("TOO SLOW"); return; }
            if (!KeyInput.MousePressed()) return;

            if (!_showing) { Fail("TOO EARLY"); return; }
            if (d.Red) { Fail("SHOT THE RED ONE"); return; }
            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var click)) return;
            if (Vector2.Distance(click, d.Pos) > _r + 8f) return; // a miss: fire again

            _reactionSum += Elapsed - _shownAt;
            Progress("HIT", ++_hits, _golds);
            Next();
            if (_index >= _draws.Length)
            {
                _label.text = "FASTEST GUN";
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
