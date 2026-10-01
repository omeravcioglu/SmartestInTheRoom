using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The chimpanzee memory test. Numbers sit on a grid; the moment you click the 1, the
    /// rest turn blank and you click them in order from memory. You may look as long as you
    /// like before that first click, but the clock is already running. Ranked by time.
    /// </summary>
    public class ChimpGame : MinigameView
    {
        private const int Cols = 8;
        private const int Rows = 4;
        private const float Limit = 15f;

        private Image[] _tiles = new Image[0];
        private TMP_Text[] _numbers = new TMP_Text[0];
        private int[] _valueOf = new int[0];
        private int _next = 1;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int n = Level switch
            {
                1 => 4,
                2 => 5,
                3 => 6,
                4 => 7,
                _ => Mathf.Min(12, 8 + (Level - 5)),
            };

            float gap = 12f;
            float cell = Mathf.Min((size.x - 80f - (Cols - 1) * gap) / Cols, (size.y - 110f - (Rows - 1) * gap) / Rows);
            float totalW = Cols * cell + (Cols - 1) * gap, totalH = Rows * cell + (Rows - 1) * gap;
            var origin = new Vector2(-totalW * 0.5f + cell * 0.5f, 20f + totalH * 0.5f - cell * 0.5f);

            var cells = LevelRng.Distinct(Rng, n, Cols * Rows);
            _tiles = new Image[n];
            _numbers = new TMP_Text[n];
            _valueOf = new int[n];
            for (int i = 0; i < n; i++)
            {
                int c = cells[i] % Cols, r = cells[i] / Cols;
                var pos = origin + new Vector2(c * (cell + gap), -r * (cell + gap));
                int tile = i;
                _valueOf[i] = i + 1;
                _tiles[i] = UiKit.Cell(Area, "Tile" + i, new Vector2(cell, cell), pos, Palette.PanelRaised,
                    () => OnTile(tile), out _numbers[i], (i + 1).ToString(), 34f);
            }

            _label = UiKit.Label(Area, "Hint", "START AT 1", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            Progress("FOUND", 0, n);
        }

        protected override void OnTick(float dt)
        {
            if (CanAct && Elapsed > Limit) Fail("TOO SLOW");

            // The rule card's demo takes a look, clicks the 1, then the rest from memory, a tile a beat.
            if (Demo && !IsDone && Elapsed >= 0.6f)
            {
                int tile = _next - 1; // tile i holds i + 1
                PointAt(Where(_tiles[tile]));
                if (Elapsed >= 1.2f + tile * 0.5f)
                {
                    TapAt(Where(_tiles[tile]));
                    OnTile(tile);
                }
            }
        }

        private void OnTile(int tile)
        {
            if (!CanMove) return;
            if (_valueOf[tile] != _next)
            {
                _tiles[tile].color = Palette.Red;
                // Show where they all were.
                for (int i = 0; i < _numbers.Length; i++) _numbers[i].text = _valueOf[i].ToString();
                Fail("WRONG ORDER");
                return;
            }

            if (_next == 1)
            {
                // The rest go blank: from here on it's memory.
                for (int i = 0; i < _numbers.Length; i++) _numbers[i].text = string.Empty;
                _label.text = "NOW FROM MEMORY";
            }
            _tiles[tile].gameObject.SetActive(false);
            _next++;
            Progress("FOUND", _next - 1, _tiles.Length);
            if (_next > _tiles.Length)
            {
                _label.text = "PERFECT";
                _label.color = Palette.Green;
                Finish(false, Ms(Elapsed));
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
