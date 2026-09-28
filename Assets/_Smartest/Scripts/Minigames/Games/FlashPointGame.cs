using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// A memory for places. A dot flashes on an empty board and you point at where it was.
    /// The flash gets shorter until it's gone before you can move; from level six two dots
    /// flash in turn and you point at both, in order. Ranked by how far off you were.
    /// </summary>
    public class FlashPointGame : MinigameView
    {
        private const float AnswerFor = 3f;
        private const float Between = 0.35f;

        private Vector2[] _spots = new Vector2[0];
        private float[] _flashAt = new float[0];
        private Image[] _dots = new Image[0];
        private float _flash;
        private float _tolerance;
        private float _radius;
        private int _clicked;
        private float _errorSum;
        private TMP_Text _label;
        private bool _revealed;

        private float LastFlashEnds => _flashAt[_flashAt.Length - 1] + _flash;

        protected override void Build()
        {
            var size = AreaSize;
            int count = 1;
            switch (Level)
            {
                case 1: _radius = 34f; _flash = 0.9f; _tolerance = 70f; break;
                case 2: _radius = 30f; _flash = 0.6f; _tolerance = 58f; break;
                case 3: _radius = 26f; _flash = 0.4f; _tolerance = 48f; break;
                case 4: _radius = 22f; _flash = 0.25f; _tolerance = 40f; break;
                case 5: _radius = 20f; _flash = 0.2f; _tolerance = 36f; break;
                default:
                    count = 2;
                    _radius = 20f;
                    _flash = Mathf.Max(0.15f, 0.25f - (Level - 6) * 0.01f);
                    _tolerance = Mathf.Max(30f, 40f - (Level - 6) * 1.5f);
                    break;
            }

            // An empty board: nothing on it to remember the spot by.
            float boardW = size.x - 80f, boardH = size.y - 100f;
            var boardCentre = new Vector2(0f, 20f);
            UiKit.Box(Area, "Board", new Vector2(boardW, boardH), boardCentre, Palette.Panel);

            float halfW = boardW * 0.5f - _radius - 30f, halfH = boardH * 0.5f - _radius - 30f;
            _spots = new Vector2[count];
            _flashAt = new float[count];
            _dots = new Image[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 p;
                int tries = 0;
                do p = boardCentre + new Vector2(RandomRange(-halfW, halfW), RandomRange(-halfH, halfH));
                while (i > 0 && Vector2.Distance(p, _spots[i - 1]) < 220f && ++tries < 20);
                _spots[i] = p;
                _flashAt[i] = i == 0 ? RandomRange(0.5f, 1.2f) : _flashAt[i - 1] + _flash + Between;
                _dots[i] = UiKit.Dot(Area, "Flash" + i, _radius * 2f, p, Palette.Accent);
                _dots[i].gameObject.SetActive(false);
            }

            _label = UiKit.Label(Area, "Hint", count > 1 ? "WATCH BOTH" : "WATCH", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        protected override void OnTick(float dt)
        {
            for (int i = 0; i < _dots.Length; i++)
            {
                bool lit = Elapsed >= _flashAt[i] && Elapsed < _flashAt[i] + _flash;
                if (!_revealed && _dots[i].gameObject.activeSelf != lit) _dots[i].gameObject.SetActive(lit);
            }
            if (Elapsed >= LastFlashEnds && _clicked == 0 && _label.text.StartsWith("WATCH"))
                _label.text = _spots.Length > 1 ? "CLICK BOTH, IN ORDER" : "WHERE WAS IT?";

            if (!CanAct)
            {
                if (!_revealed && Elapsed > LastFlashEnds + 1.5f) Reveal();
                return;
            }
            if (Elapsed > LastFlashEnds + AnswerFor) { Reveal(); Fail("TOO SLOW"); return; }
            // A click before its dot has even flashed isn't an answer yet.
            if (Elapsed < _flashAt[_clicked] || !KeyInput.MousePressed()) return;
            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var local)) return;

            // Clicks answer the dots in the order they flashed.
            int i0 = _clicked;
            float error = Vector2.Distance(local, _spots[i0]);
            UiKit.Dot(Area, "Click" + i0, 14f, local, Palette.Ink);
            _clicked++;
            if (error > _tolerance)
            {
                Reveal();
                Fail("OFF BY " + Mathf.RoundToInt(error) + " PX");
                return;
            }
            _errorSum += error;
            if (_clicked < _spots.Length) return;

            Reveal();
            _label.text = Mathf.RoundToInt(_errorSum) + " PX OFF";
            _label.color = Palette.Green;
            // Hundredths of a pixel: whole pixels tie constantly, and every tie is a play-off.
            Finish(false, Mathf.RoundToInt(_errorSum * 100f));
        }

        /// <summary>Show where the dots really were.</summary>
        private void Reveal()
        {
            _revealed = true;
            foreach (var d in _dots) d.gameObject.SetActive(true);
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
