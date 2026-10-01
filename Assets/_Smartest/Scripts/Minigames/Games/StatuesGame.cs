using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Red light, green light. Holding the button runs your marker along the track at a
    /// steady pace; let go to freeze. When the light says STOP you get a moment to let go,
    /// and still holding after that puts you out. The lights come from the seed and the light
    /// says GO or STOP in words too, so the colour isn't the only cue. Ranked by time to the
    /// finish.
    /// </summary>
    public class StatuesGame : MinigameView
    {
        private const float Limit = 15f;
        private const float Stride = 0.13f;

        private readonly List<float> _switches = new List<float>(); // times the light changes; GO first
        private float _speed;
        private float _grace;
        private float _x;
        private float _startX, _finishX, _trackY;
        private RectTransform _runner;
        private Image _runnerImage;
        private Image _light;
        private TMP_Text _word;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            float goMin, goMax, stopMin, stopMax;
            switch (Level)
            {
                case 1: _speed = 260f; goMin = 1.2f; goMax = 2.0f; stopMin = 0.8f; stopMax = 1.3f; _grace = 0.45f; break;
                case 2: _speed = 290f; goMin = 1.0f; goMax = 1.8f; stopMin = 0.8f; stopMax = 1.3f; _grace = 0.4f; break;
                case 3: _speed = 320f; goMin = 0.8f; goMax = 1.6f; stopMin = 0.7f; stopMax = 1.2f; _grace = 0.35f; break;
                case 4: _speed = 350f; goMin = 0.6f; goMax = 1.4f; stopMin = 0.7f; stopMax = 1.2f; _grace = 0.3f; break;
                default:
                    _speed = Mathf.Min(420f, 370f + (Level - 5) * 10f);
                    goMin = 0.45f; goMax = 1.2f; stopMin = 0.6f; stopMax = 1.1f;
                    _grace = 0.25f;
                    break;
            }

            // The light starts on STOP, turns GO at 0.8 s, and alternates from there.
            float t = 0.8f;
            bool go = true;
            while (t < Limit + 2f)
            {
                _switches.Add(t);
                t += go ? RandomRange(goMin, goMax) : RandomRange(stopMin, stopMax);
                go = !go;
            }

            float w = Mathf.Min(1000f, size.x - 160f);
            _trackY = -40f;
            UiKit.Box(Area, "Track", new Vector2(w, 90f), new Vector2(0f, _trackY), Palette.PanelRaised);
            _startX = -w * 0.5f + 40f;
            _finishX = w * 0.5f - 40f;
            UiKit.FinishLine(Area, "Finish", 80f, new Vector2(_finishX, _trackY));
            _x = _startX;
            _runnerImage = UiKit.Art(Area, "Runner", "stand", new Vector2(44f, 56f) * 1.25f, new Vector2(_x, _trackY));
            _runner = (RectTransform)_runnerImage.transform;

            _light = UiKit.Dot(Area, "Light", 150f, new Vector2(0f, 140f), Palette.Red);
            _word = UiKit.Label(_light.transform, "Word", "STOP", 50f, Palette.OnRed, new Vector2(140f, 70f), Vector2.zero);
            _label = UiKit.Label(Area, "Hint", "HOLD TO RUN", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        /// <summary>Is the light GO at time t, and when did it last change?</summary>
        private bool GoAt(float t, out float since)
        {
            since = t;
            bool go = false;
            for (int i = 0; i < _switches.Count && _switches[i] <= t; i++)
            {
                go = i % 2 == 0;
                since = t - _switches[i];
            }
            return go;
        }

        protected override void OnTick(float dt)
        {
            bool go = GoAt(Elapsed, out float since);
            _light.color = go ? Palette.Green : Palette.Red;
            _word.text = go ? "GO" : "STOP";
            _word.color = go ? Palette.Ink : Palette.OnRed;

            // Someone watching sees a runner who stops a beat after the light does. The rule card's
            // demo also waits a beat for GO, and never holds before the first one.
            bool held = CanAct ? KeyInput.MouseHeld()
                : Demo ? Elapsed >= _switches[0] && (go ? since >= 0.25f : since < 0.25f)
                : !Interactive && (go || since < 0.2f);
            if (Demo) HoldAt(new Vector2(135f, 110f), held);
            // Before the first GO nothing counts: holding through the countdown isn't a false start.
            bool started = Elapsed >= _switches[0];
            bool running = held && started && _x < _finishX;
            if (running) _x = Mathf.Min(_finishX, _x + _speed * dt);
            _runner.anchoredPosition = new Vector2(_x, _trackY);
            // Two strides while running; stock still when not.
            UiKit.Dress(_runnerImage, !running ? "stand" : Mathf.FloorToInt(Elapsed / Stride) % 2 == 0 ? "run1" : "run2");
            if (!CanMove) return;

            if (held && started && !go && since > _grace) { Fail("MOVED ON RED"); return; }
            if (_x >= _finishX)
            {
                _label.text = "MADE IT";
                _label.color = Palette.Green;
                Finish(false, Ms(Elapsed));
            }
            else if (Elapsed > Limit) Fail("TOO SLOW");
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_runnerImage != null) _runnerImage.color = Palette.Red;
            Finish(true, WorstMetric);
        }
    }
}
