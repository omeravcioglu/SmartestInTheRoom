using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Pointing accuracy, not speed — though the target vanishes, so it is a little of
    /// both. Clicking anywhere outside the target is an exit.
    /// </summary>
    public class BullseyeGame : MinigameView
    {
        private RectTransform _target;
        private RectTransform _inner;
        private TMP_Text _label;
        private Vector2 _centre;
        private Vector2 _drift;
        private float _radius;
        private float _visibleFor;

        protected override void Build()
        {
            var size = AreaSize;
            switch (Level)
            {
                case 1: _radius = 100f; _visibleFor = 2.0f; break;
                case 2: _radius = 70f; _visibleFor = 1.5f; break;
                case 3: _radius = 50f; _visibleFor = 1.2f; break;
                case 4: _radius = 40f; _visibleFor = 1.2f; _drift = RandomDrift(); break;
                default:
                    _radius = Mathf.Max(20f, 30f - (Level - 5) * 2f);
                    _visibleFor = Mathf.Max(0.5f, 0.8f - (Level - 5) * 0.05f);
                    _drift = RandomDrift();
                    break;
            }

            float halfW = Mathf.Max(60f, size.x * 0.5f - _radius - 30f);
            float halfH = Mathf.Max(60f, size.y * 0.5f - _radius - 50f);
            _centre = new Vector2(RandomRange(-halfW, halfW), RandomRange(-halfH, halfH));

            _target = (RectTransform)UiKit.Dot(Area, "Target", _radius * 2f, _centre, Palette.Red).transform;
            _inner = (RectTransform)UiKit.Dot(Area, "Inner", _radius * 0.5f, _centre, Palette.Accent).transform;

            _label = UiKit.Label(Area, "Hint", "CLICK THE CENTRE", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        private Vector2 RandomDrift() => new Vector2(RandomRange(-70f, 70f), RandomRange(-50f, 50f));

        private Vector2 CentreNow()
        {
            if (_drift == Vector2.zero) return _centre;
            return _centre + _drift * Mathf.Sin(Elapsed * 1.6f);
        }

        protected override void OnTick(float dt)
        {
            var c = CentreNow();
            if (_target != null) _target.anchoredPosition = c;
            if (_inner != null) _inner.anchoredPosition = c;

            if (Elapsed >= _visibleFor)
            {
                if (_target != null) _target.gameObject.SetActive(false);
                if (_inner != null) _inner.gameObject.SetActive(false);
            }
            if (Elapsed > _visibleFor + 0.6f) { Fail("GONE"); return; }

            if (!CanAct || !KeyInput.MousePressed()) return;
            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var local)) return;

            float d = Vector2.Distance(local, c);
            if (d > _radius) { Fail("MISSED"); return; }

            _label.text = Mathf.RoundToInt(d) + " px off";
            _label.color = Palette.Green;
            Finish(false, Mathf.RoundToInt(d));
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
