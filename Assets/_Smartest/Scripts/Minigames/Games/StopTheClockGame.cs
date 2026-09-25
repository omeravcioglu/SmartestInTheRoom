using System.Globalization;
using Smartest.Core;
using TMPro;
using UnityEngine;

namespace Smartest.Minigames
{
    /// <summary>
    /// An internal clock test. The numbers are taken away early, so after a second or two
    /// the only thing left to count with is you.
    /// </summary>
    public class StopTheClockGame : MinigameView
    {
        private TMP_Text _clock;
        private TMP_Text _label;
        private float _target;
        private float _tolerance;
        private float _visibleFor;

        protected override void Build()
        {
            var size = AreaSize;

            switch (Level)
            {
                case 4: _target = 7f; break;
                case 5: _target = 3.5f; break;
                default: _target = Level >= 6 ? (Level % 2 == 0 ? 6.5f : 4.5f) : 5f; break;
            }
            switch (Level)
            {
                case 1: _tolerance = 0.50f; _visibleFor = 2f; break;
                case 2: _tolerance = 0.35f; _visibleFor = 1f; break;
                case 3: _tolerance = 0.25f; _visibleFor = 0f; break;
                case 4: _tolerance = 0.20f; _visibleFor = 0f; break;
                default: _tolerance = Mathf.Max(0.08f, 0.14f - (Level - 5) * 0.01f); _visibleFor = 0f; break;
            }

            UiKit.Label(Area, "Target", "STOP AT " + _target.ToString("0.00", CultureInfo.InvariantCulture),
                30f, Palette.Accent, new Vector2(size.x - 60f, 50f), new Vector2(0f, 110f));
            _clock = UiKit.Label(Area, "Clock", "0.00", 96f, Palette.Text,
                new Vector2(size.x - 60f, 130f), new Vector2(0f, 10f));
            _label = UiKit.Label(Area, "Hint", "SPACE to stop", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -90f));
        }

        protected override void OnTick(float dt)
        {
            if (_clock != null)
                _clock.text = Elapsed < _visibleFor
                    ? Elapsed.ToString("0.00", CultureInfo.InvariantCulture)
                    : "?.??";

            if (Elapsed > _target + _tolerance + 1.5f) { Fail("TOO SLOW"); return; }
            if (!CanAct || !KeyInput.SpacePressed()) return;

            float error = Mathf.Abs(Elapsed - _target);
            if (_clock != null) _clock.text = Elapsed.ToString("0.00", CultureInfo.InvariantCulture);
            if (error > _tolerance) Fail("OFF BY " + error.ToString("0.00", CultureInfo.InvariantCulture));
            else
            {
                _label.text = "OFF BY " + error.ToString("0.00", CultureInfo.InvariantCulture);
                _label.color = Palette.Green;
                Finish(false, Ms(error));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
