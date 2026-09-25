using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Four beats to lock onto, then silence you have to keep filling. Rhythm, not reflex —
    /// which makes it one of the few levels where being calm beats being fast.
    /// </summary>
    public class KeepTheBeatGame : MinigameView
    {
        private const int LeadBeats = 4;

        private Image _pulse;
        private TMP_Text _label;
        private float _interval;
        private float _tolerance;
        private int _silentBeats;
        private int _pressed;
        private float _error;
        private float _flashUntil;
        private int _pulsesPlayed;

        protected override void Build()
        {
            var size = AreaSize;
            int bpm;
            switch (Level)
            {
                case 1: bpm = 100; _silentBeats = 4; _tolerance = 0.15f; break;
                case 2: bpm = 120; _silentBeats = 4; _tolerance = 0.12f; break;
                case 3: bpm = 90; _silentBeats = 5; _tolerance = 0.12f; break;
                case 4: bpm = 140; _silentBeats = 4; _tolerance = 0.09f; break;
                default:
                    bpm = Mathf.Min(180, 150 + (Level - 5) * 6);
                    _silentBeats = Mathf.Min(8, 6 + (Level - 5));
                    _tolerance = Mathf.Max(0.05f, 0.07f - (Level - 5) * 0.005f);
                    break;
            }
            _interval = 60f / bpm;

            _pulse = UiKit.Box(Area, "Pulse", new Vector2(200f, 200f), new Vector2(0f, 20f), Palette.Neutral);
            UiKit.Label(Area, "Beats", $"{bpm} BPM  ·  {_silentBeats} SILENT BEATS", 26f, Palette.Accent,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, 150f));
            _label = UiKit.Label(Area, "Hint", "Listen…", 28f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -110f));
        }

        private float ExpectedAt(int pressIndex) => (LeadBeats + pressIndex) * _interval;

        protected override void OnTick(float dt)
        {
            // The four lead beats flash; after that the player is on their own.
            while (_pulsesPlayed < LeadBeats && Elapsed >= _pulsesPlayed * _interval)
            {
                _flashUntil = Elapsed + Mathf.Min(0.12f, _interval * 0.4f);
                _pulsesPlayed++;
                if (_pulsesPlayed == LeadBeats && _label != null) _label.text = "Keep going";
            }
            if (_pulse != null)
                _pulse.color = Elapsed < _flashUntil ? Palette.Accent : Palette.Neutral;

            float lastExpected = ExpectedAt(_silentBeats - 1);
            if (_pressed < _silentBeats && Elapsed > lastExpected + _tolerance + 0.3f) { Fail("MISSED A BEAT"); return; }

            if (!CanAct || !KeyInput.SpacePressed()) return;

            if (Elapsed < LeadBeats * _interval - _tolerance) { Fail("TOO EARLY"); return; }

            float expected = ExpectedAt(_pressed);
            float err = Mathf.Abs(Elapsed - expected);
            _flashUntil = Elapsed + 0.1f;
            if (err > _tolerance) { Fail("OFF BEAT"); return; }

            _error += err;
            _pressed++;
            if (_pressed >= _silentBeats)
            {
                _label.text = "IN TIME";
                _label.color = Palette.Green;
                Finish(false, Ms(_error));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
