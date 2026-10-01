using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Three darts at a bull. Your aim sways on its own; hold the button to steady it (a held
    /// breath), let go to throw. Hold too long and you run out of breath and shake worse than
    /// before, so the throw is a little gamble on timing. Everyone's sway comes from the same
    /// seed. A dart off the board is out. Ranked by total distance from the bull.
    /// </summary>
    public class DartsGame : MinigameView
    {
        private const int Darts = 3;
        private const float BoardR = 190f;
        private const float SteadyIn = 0.35f;
        private const float Limit = 12f;

        private RectTransform _reticle;
        private Image _reticleImage;
        private TMP_Text _label;
        private float _sway;
        private float _breath;
        private Vector2 _f1, _f2;
        private Vector2 _p1, _p2;
        private Vector2 _board;
        private bool _holding;
        private float _heldAt;
        private int _thrown;
        private float _distSum;
        private float _nextAuto = 1.5f;

        protected override void Build()
        {
            var size = AreaSize;
            switch (Level)
            {
                case 1: _sway = 30f; _breath = 1.6f; break;
                case 2: _sway = 45f; _breath = 1.4f; break;
                case 3: _sway = 60f; _breath = 1.2f; break;
                case 4: _sway = 75f; _breath = 1.0f; break;
                default: _sway = Mathf.Min(120f, 85f + (Level - 5) * 8f); _breath = Mathf.Max(0.7f, 0.9f - (Level - 5) * 0.04f); break;
            }
            float speed = 1f + (Level - 1) * 0.12f;
            _f1 = new Vector2(RandomRange(1.1f, 1.6f), RandomRange(0.9f, 1.4f)) * speed;
            _f2 = new Vector2(RandomRange(2.3f, 3.1f), RandomRange(2.6f, 3.4f)) * speed;
            _p1 = new Vector2(RandomRange(0f, 6.3f), RandomRange(0f, 6.3f));
            _p2 = new Vector2(RandomRange(0f, 6.3f), RandomRange(0f, 6.3f));

            // The board: rings from the outside in, alternating, with a gold bull.
            _board = new Vector2(0f, 20f);
            Color[] rings = { Palette.Ink, Palette.PaperHi, Palette.Ink, Palette.PaperHi, Palette.Red, Palette.Accent };
            float[] radii = { BoardR, BoardR * 0.82f, BoardR * 0.64f, BoardR * 0.46f, BoardR * 0.2f, BoardR * 0.09f };
            for (int i = 0; i < rings.Length; i++) UiKit.Dot(Area, "Ring" + i, radii[i] * 2f, _board, rings[i]);

            _reticleImage = UiKit.Dot(Area, "Aim", 30f, _board, Palette.Blue);
            _reticle = (RectTransform)_reticleImage.transform;
            UiKit.Dot(_reticle, "Pip", 10f, Vector2.zero, Palette.PaperHi);
            _label = UiKit.Label(Area, "Hint", "HOLD TO STEADY, LET GO TO THROW", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            Tries("DARTS", Darts, Darts);
        }

        /// <summary>How much of the sway is left: steadies while held, then worse once the breath runs out.</summary>
        private float SwayNow()
        {
            if (!_holding) return 1f;
            float held = Elapsed - _heldAt;
            if (held < SteadyIn) return Mathf.Lerp(1f, 0.22f, held / SteadyIn);
            if (held < SteadyIn + _breath) return 0.22f;
            return Mathf.Min(1.8f, 0.22f + (held - SteadyIn - _breath) * 2.2f); // out of breath
        }

        private Vector2 Wobble(float t) =>
            new Vector2(Mathf.Sin(_f1.x * t + _p1.x) * 0.7f + Mathf.Sin(_f2.x * t + _p2.x) * 0.3f,
                        Mathf.Sin(_f1.y * t + _p1.y) * 0.7f + Mathf.Sin(_f2.y * t + _p2.y) * 0.3f) * _sway;

        protected override void OnTick(float dt)
        {
            if (_thrown >= Darts) return;

            Vector2 aim;
            if (CanAct && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m)) aim = m;
            else aim = _board; // someone watching: aiming dead centre, still swaying

            // Someone watching sees a steady half-second hold before each throw.
            bool held = CanAct ? KeyInput.MouseHeld() : !Interactive && Elapsed > _nextAuto - 0.5f && Elapsed < _nextAuto;
            if (held && !_holding) { _holding = true; _heldAt = Elapsed; }

            var at = aim + Wobble(Elapsed) * SwayNow();
            _reticle.anchoredPosition = at;
            _reticleImage.color = _holding && SwayNow() < 0.5f ? Palette.Green : Palette.Blue;

            if (CanAct && Elapsed > Limit) { Fail("TOO SLOW"); return; }
            if (!_holding || held) return;

            // Let go: the dart lands where the aim was.
            _holding = false;
            _thrown++;
            Tries("DARTS", Darts - _thrown, Darts);
            if (!Interactive) _nextAuto = Elapsed + 1.5f;
            // Blue shows on the ink rings and the paper ones alike.
            UiKit.Dot(Area, "Dart" + _thrown, 16f, at, Palette.Blue);
            _reticle.SetAsLastSibling();
            float d = Vector2.Distance(at, _board);
            if (d > BoardR && CanAct) { Fail("MISSED THE BOARD"); return; }
            _distSum += d;
            _label.text = Mathf.RoundToInt(d) + " FROM THE BULL";
            if (_thrown >= Darts && CanAct)
            {
                _label.color = Palette.Green;
                // Tenths of a pixel: whole pixels would tie, and a tie means a play-off.
                Finish(false, Mathf.RoundToInt(_distSum * 10f));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
