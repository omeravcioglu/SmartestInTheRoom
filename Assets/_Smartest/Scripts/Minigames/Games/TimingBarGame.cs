using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Visual timing rather than reaction: the marker's path is completely visible, so
    /// missing is on you. The gold zone narrows, speeds up and eventually drifts.
    /// </summary>
    public class TimingBarGame : MinigameView
    {
        private RectTransform _zone;
        private RectTransform _marker;
        private TMP_Text _label;
        private float _trackW;
        private float _zoneWidth;
        private float _zoneCentre;
        private float _speed;
        private float _drift;

        protected override void Build()
        {
            var size = AreaSize;
            _trackW = Mathf.Min(780f, size.x - 80f);
            var track = UiKit.Box(Area, "Track", new Vector2(_trackW, 56f), new Vector2(0f, 20f), Palette.Neutral);

            switch (Level)
            {
                case 1: _zoneWidth = 0.30f; break;
                case 2: _zoneWidth = 0.20f; break;
                case 3: _zoneWidth = 0.14f; break;
                case 4: _zoneWidth = 0.11f; break;
                default: _zoneWidth = Mathf.Max(0.05f, 0.09f - (Level - 5) * 0.01f); break;
            }
            _zoneCentre = RandomRange(0.25f, 0.75f);
            _speed = Level <= 2 ? 0.35f : (Level == 3 ? 0.55f : Mathf.Min(1.3f, 0.7f + (Level - 4) * 0.12f));
            _drift = Level >= 4 ? RandomRange(0.04f, 0.10f) : 0f;

            var zone = UiKit.Box(track.transform, "Zone", new Vector2(_trackW * _zoneWidth, 56f), Vector2.zero, Palette.Accent);
            _zone = (RectTransform)zone.transform;
            var marker = UiKit.Box(track.transform, "Marker", new Vector2(10f, 84f), Vector2.zero, Palette.Text);
            _marker = (RectTransform)marker.transform;

            _label = UiKit.Label(Area, "Hint", "SPACE inside the gold", 26f, Palette.TextDim,
                new Vector2(_trackW, 40f), new Vector2(0f, -70f));
            Place(0f);
        }

        private float CentreNow()
        {
            float c = _zoneCentre + Mathf.Sin(Elapsed * 1.3f) * _drift;
            float half = _zoneWidth * 0.5f;
            return Mathf.Clamp(c, half, 1f - half);
        }

        private float MarkerNow() => Mathf.PingPong(Elapsed * _speed * 2f, 1f);

        private void Place(float t)
        {
            float c = CentreNow();
            if (_zone != null) _zone.anchoredPosition = new Vector2((c - 0.5f) * _trackW, 0f);
            if (_marker != null) _marker.anchoredPosition = new Vector2((t - 0.5f) * _trackW, 0f);
        }

        protected override void OnTick(float dt)
        {
            float pos = MarkerNow();
            Place(pos);

            if (Elapsed > 8f) { Fail("TOO SLOW"); return; }
            if (!CanAct || !KeyInput.SpacePressed()) return;

            float d = Mathf.Abs(pos - CentreNow());
            if (d <= _zoneWidth * 0.5f)
            {
                _label.text = "IN";
                _label.color = Palette.Green;
                Finish(false, Mathf.RoundToInt(d * 1000f));
            }
            else Fail("MISSED");
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
