using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Pure reaction. The box turns green at a moment nobody can predict; jumping the gun
    /// is an instant exit, which is what stops everyone from just mashing space.
    /// </summary>
    public class GreenLightGame : MinigameView
    {
        private Image _box;
        private TMP_Text _label;
        private readonly List<float> _decoys = new List<float>();
        private float _greenAt;
        private float _window;
        private float _decoyUntil;
        private int _decoyIndex;
        private bool _green;

        protected override void Build()
        {
            var size = AreaSize;
            float w = Mathf.Min(560f, size.x - 60f);
            float h = Mathf.Min(220f, size.y * 0.5f);
            _box = UiKit.Box(Area, "Light", new Vector2(w, h), new Vector2(0f, 30f), Palette.Red);
            _label = UiKit.Label(Area, "Hint", "WAIT", 34f, Palette.TextDim,
                new Vector2(w, 60f), new Vector2(0f, -(h * 0.5f + 60f)));

            float minWait = Level >= 4 ? 0.5f : 1f;
            float maxWait = Level >= 4 ? 6f : (Level >= 2 ? 5f : 3f);
            _greenAt = RandomRange(minWait, maxWait);
            _window = Level >= 5 ? 0.6f : 2.5f;

            int decoyCount = Mathf.Clamp(Level - 2, 0, 4);
            _decoys.Clear();
            for (int i = 0; i < decoyCount; i++)
                _decoys.Add(RandomRange(0.3f, Mathf.Max(0.5f, _greenAt - 0.4f)));
            _decoys.Sort();
            _decoyIndex = 0;
            _green = false;
        }

        protected override void OnTick(float dt)
        {
            if (!_green)
            {
                while (_decoyIndex < _decoys.Count && Elapsed >= _decoys[_decoyIndex])
                {
                    _decoyUntil = Elapsed + 0.22f;
                    _decoyIndex++;
                }
                _box.color = Elapsed < _decoyUntil ? Palette.Accent : Palette.Red;
                if (Elapsed >= _greenAt)
                {
                    _green = true;
                    _box.color = Palette.Green;
                    _label.text = "NOW";
                    _label.color = Palette.Green;
                }
            }
            else if (Elapsed - _greenAt > _window)
            {
                Fail("TOO SLOW");
                return;
            }

            if (!CanAct) return;
            if (!KeyInput.SpacePressed()) return;

            if (!_green) Fail("TOO EARLY");
            else
            {
                _label.text = "GOOD";
                _label.color = Palette.Accent;
                Finish(false, Ms(Elapsed - _greenAt));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_box != null) _box.color = Palette.Red;
            Finish(true, WorstMetric);
        }
    }
}
