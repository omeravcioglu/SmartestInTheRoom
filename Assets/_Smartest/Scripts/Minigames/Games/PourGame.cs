using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Filling a glass to the line: hold the button to pour, let go to stop. One pour, no
    /// topping up. Later levels pour in surges, and the drink keeps settling a little after you
    /// let go, so you stop early on purpose. Spill over the top and you're out; otherwise
    /// ranked by how close to the line you finished.
    /// </summary>
    public class PourGame : MinigameView
    {
        private const float GlassW = 170f;
        private const float GlassH = 380f;
        private const float StartBy = 4f;
        private const float SettleFor = 0.35f;

        private RectTransform _drink;
        private Image _drinkImage;
        private TMP_Text _label;
        private float _glassBottom;
        private float _inner;       // inside height of the glass
        private float _line;        // target height, measured from the inside bottom
        private float _rate;
        private float _surge;
        private float _surgeFreq;
        private float _surgePhase;
        private float _settleShare; // how much more rises after letting go, as seconds of flow
        private float _tolerance;
        private float _level;
        private bool _pouring;
        private float _pourStart;
        private float _releasedAt = -1f;
        private float _settleTotal;
        private float _settleLeft;

        protected override void Build()
        {
            var size = AreaSize;
            switch (Level)
            {
                case 1: _rate = 110f; _tolerance = 40f; _surge = 0f; _settleShare = 0f; break;
                case 2: _rate = 140f; _tolerance = 30f; _surge = 0f; _settleShare = 0f; break;
                case 3: _rate = 175f; _tolerance = 24f; _surge = 0.25f; _settleShare = 0f; break;
                case 4: _rate = 210f; _tolerance = 18f; _surge = 0.35f; _settleShare = 0.12f; break;
                default:
                    _rate = Mathf.Min(340f, 240f + (Level - 5) * 15f);
                    _tolerance = Mathf.Max(8f, 14f - (Level - 5));
                    _surge = 0.45f;
                    _settleShare = 0.18f;
                    break;
            }
            _surgeFreq = RandomRange(2.5f, 4f);
            _surgePhase = RandomRange(0f, Mathf.PI * 2f);

            var centre = new Vector2(0f, 10f);
            UiKit.Box(Area, "Glass", new Vector2(GlassW, GlassH), centre, Palette.PanelRaised);
            _glassBottom = centre.y - GlassH * 0.5f + 4f;
            _inner = GlassH - 8f;
            _line = _inner * RandomRange(0.45f, 0.75f);

            _drinkImage = UiKit.Fill(Area, "Drink", new Vector2(GlassW - 8f, 0f), new Vector2(0f, _glassBottom), Palette.Accent);
            _drink = (RectTransform)_drinkImage.transform;
            _drink.pivot = new Vector2(0.5f, 0f);
            _drink.anchoredPosition = new Vector2(0f, _glassBottom);
            // The line runs past both sides of the glass so it reads at a glance.
            UiKit.Fill(Area, "Line", new Vector2(GlassW + 60f, 6f), new Vector2(0f, _glassBottom + _line), Palette.Ink);

            _label = UiKit.Label(Area, "Hint", "HOLD TO POUR", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        private float FlowAt(float t) => _rate * (1f + _surge * Mathf.Sin(_surgeFreq * t + _surgePhase));

        protected override void OnTick(float dt)
        {
            if (!Interactive)
            {
                // Someone watching sees a tidy pour to the line.
                if (Elapsed > 1f && _level < _line) _level = Mathf.Min(_line, _level + _rate * dt);
                _drink.sizeDelta = new Vector2(GlassW - 8f, _level);
                return;
            }
            if (!CanAct) return;
            bool held = KeyInput.MouseHeld();

            if (_releasedAt < 0f)
            {
                if (!_pouring)
                {
                    if (held) { _pouring = true; _pourStart = Elapsed; _label.text = "LET GO ON THE LINE"; }
                    else if (Elapsed > StartBy) { Fail("NEVER POURED"); return; }
                }
                if (_pouring)
                {
                    if (held) _level += FlowAt(Elapsed - _pourStart) * dt;
                    else
                    {
                        _releasedAt = Elapsed;
                        _settleTotal = _settleLeft = FlowAt(Elapsed - _pourStart) * _settleShare;
                    }
                }
            }
            else if (_settleLeft > 0f)
            {
                // The pour settles: a last little rise after letting go, spread over SettleFor.
                float add = Mathf.Min(_settleLeft, _settleTotal * dt / SettleFor);
                _level += add;
                _settleLeft -= add;
            }

            _drink.sizeDelta = new Vector2(GlassW - 8f, Mathf.Min(_level, _inner));
            if (_level >= _inner) { Fail("SPILLED"); return; }

            if (_releasedAt >= 0f && (_settleLeft <= 0f || Elapsed - _releasedAt >= SettleFor))
            {
                _level += _settleLeft;
                _settleLeft = 0f;
                _drink.sizeDelta = new Vector2(GlassW - 8f, Mathf.Min(_level, _inner));
                if (_level >= _inner) { Fail("SPILLED"); return; }
                float off = Mathf.Abs(_level - _line);
                if (off > _tolerance) { Fail("OFF BY " + Mathf.RoundToInt(off) + " PX"); return; }
                _label.text = "OFF BY " + Mathf.RoundToInt(off) + " PX";
                _label.color = Palette.Green;
                // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(off * 10f));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_drinkImage != null) _drinkImage.color = Palette.Red;
            Finish(true, WorstMetric);
        }
    }
}
