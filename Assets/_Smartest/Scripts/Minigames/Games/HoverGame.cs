using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The fishing-rod minigame: hold the button and the dot climbs, let go and it sinks, and
    /// a gold zone wanders up and down the track. Control by throttle rather than by pointing,
    /// so it plays differently from everything else. Ranked by how close to the zone's middle
    /// you stayed on average.
    /// </summary>
    public class HoverGame : MinigameView
    {
        private const float PhysicsStep = 1f / 240f;
        private const float Accel = 1100f;
        private const float MaxSpeed = 500f;
        private const float TrackH = 420f;
        private const float TrackW = 130f;
        private const float DotR = 16f;
        /// <summary>Nothing moves until your first press, or until this long has passed.</summary>
        private const float SettleFor = 3f;
        /// <summary>For someone watching, the zone sets off after this long.</summary>
        private const float WatchStart = 1f;
        private RectTransform _zone;
        private Image _zoneImage;
        private RectTransform _dot;
        private TMP_Text _label;
        private float _trackY;
        private float _zoneH;
        private float _hold;
        private float _a1, _a2, _w1, _w2, _p1, _p2;
        private float _y;
        private float _v;
        private float _acc;
        private bool _started;
        private float _startedAt;
        private float _outFor;
        /// <summary>Out of the zone for this long and you're out; a brush with the edge isn't.</summary>
        private float _outAllowed;
        private float _distSum;
        private float _timeSum;

        protected override void Build()
        {
            var size = AreaSize;
            float f;
            switch (Level)
            {
                case 1: _zoneH = 160f; f = 0.45f; _hold = 6f; break;
                case 2: _zoneH = 135f; f = 0.6f; _hold = 6f; break;
                case 3: _zoneH = 115f; f = 0.75f; _hold = 7f; break;
                case 4: _zoneH = 100f; f = 0.9f; _hold = 7f; break;
                default:
                    // The dot tops out at 500 px/s; a zone much quicker than this can't be kept up with.
                    _zoneH = Mathf.Max(66f, 88f - (Level - 5) * 4f);
                    f = Mathf.Min(1.3f, 1.05f + (Level - 5) * 0.06f);
                    _hold = 7f;
                    break;
            }

            // Throttle control takes a moment to get the feel of; the warm-up forgives more.
            _outAllowed = Level == 1 ? 0.6f : Level == 2 ? 0.45f : 0.3f;
            _trackY = 20f;
            float range = (TrackH - _zoneH) * 0.5f - 8f;
            _a1 = range * 0.65f;
            _a2 = range * 0.35f;
            _w1 = f * RandomRange(0.9f, 1.2f);
            _w2 = f * RandomRange(1.6f, 2.2f);
            _p1 = RandomRange(0f, Mathf.PI * 2f);
            _p2 = RandomRange(0f, Mathf.PI * 2f);

            UiKit.Box(Area, "Track", new Vector2(TrackW, TrackH), new Vector2(0f, _trackY), Palette.Panel);
            _zoneImage = UiKit.Fill(Area, "Zone", new Vector2(TrackW - 16f, _zoneH), new Vector2(0f, _trackY + ZoneAt(0f)), Palette.Accent);
            _zone = (RectTransform)_zoneImage.transform;
            _y = ZoneAt(0f);
            _dot = (RectTransform)UiKit.Dot(Area, "Dot", DotR * 2f, new Vector2(0f, _trackY + _y), Palette.Ink).transform;
            // Someone already out watches the zone; they have no dot in it.
            if (!Interactive) _dot.gameObject.SetActive(false);

            _label = UiKit.Label(Area, "Hint", "HOLD TO RISE", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        /// <summary>The zone's centre, relative to the track's, t seconds after it starts moving.</summary>
        private float ZoneAt(float t) => _a1 * Mathf.Sin(_w1 * t + _p1) + _a2 * Mathf.Sin(_w2 * t + _p2);

        protected override void OnTick(float dt)
        {
            bool held = CanAct && KeyInput.MouseHeld();
            if (!_started && (held || Elapsed >= (Interactive ? SettleFor : WatchStart)))
            {
                _started = true;
                _startedAt = Elapsed;
            }
            float t = _started ? Elapsed - _startedAt : 0f;
            float zone = ZoneAt(t);
            _zone.anchoredPosition = new Vector2(0f, _trackY + zone);
            if (!CanAct || !_started) return;

            float limit = TrackH * 0.5f - DotR - 4f;
            _acc += dt;
            while (_acc >= PhysicsStep)
            {
                _acc -= PhysicsStep;
                _v = Mathf.Clamp(_v + (held ? Accel : -Accel) * PhysicsStep, -MaxSpeed, MaxSpeed);
                _y += _v * PhysicsStep;
                if (_y > limit) { _y = limit; _v = 0f; }
                if (_y < -limit) { _y = -limit; _v = 0f; }
            }
            _dot.anchoredPosition = new Vector2(0f, _trackY + _y);

            float off = Mathf.Abs(_y - zone);
            _distSum += off * dt;
            _timeSum += dt;
            if (off > _zoneH * 0.5f)
            {
                _outFor += dt;
                _zoneImage.color = Palette.AccentDim;
                if (_outFor > _outAllowed) { _zoneImage.color = Palette.Red; Fail("LOST IT"); return; }
            }
            else
            {
                _outFor = 0f;
                _zoneImage.color = Palette.Accent;
            }

            if (t >= _hold)
            {
                _label.text = "HELD IT";
                _label.color = Palette.Green;
                // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(_distSum / Mathf.Max(0.001f, _timeSum) * 10f));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
