using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Ordering under time pressure. Nothing is hidden and nothing is random once it's on
    /// screen — it just turns out that sorting seven numbers with people watching is hard.
    /// </summary>
    public class SortItGame : MinigameView
    {
        private Image[] _tiles;
        private int[] _values;
        private int[] _order;
        private TMP_Text _label;
        private int _index;

        protected override void Build()
        {
            var size = AreaSize;
            int count;
            int min, max;
            switch (Level)
            {
                case 1: count = 4; min = 1; max = 20; break;
                case 2: count = 5; min = 1; max = 20; break;
                case 3: count = 6; min = 1; max = 99; break;
                case 4: count = 6; min = -40; max = 60; break;
                default: count = Mathf.Min(9, 7 + (Level - 5)); min = -99; max = 99; break;
            }

            // Distinct values, so the order is never ambiguous.
            var used = new HashSet<int>();
            _values = new int[count];
            for (int i = 0; i < count; i++)
            {
                int v;
                do { v = RandomRange(min, max + 1); } while (!used.Add(v));
                _values[i] = v;
            }

            var sorted = new List<int>();
            for (int i = 0; i < count; i++) sorted.Add(i);
            sorted.Sort((a, b) => _values[a].CompareTo(_values[b]));
            _order = sorted.ToArray();

            float tile = Mathf.Min(120f, (size.x - 80f - (count - 1) * 14f) / count);
            float total = count * tile + (count - 1) * 14f;
            _tiles = new Image[count];
            for (int i = 0; i < count; i++)
            {
                float x = -total * 0.5f + tile * 0.5f + i * (tile + 14f);
                int captured = i;
                _tiles[i] = UiKit.Cell(Area, "Tile" + i, new Vector2(tile, tile), new Vector2(x, 20f),
                    Palette.Neutral, () => OnTile(captured), out _, _values[i].ToString(), tile * 0.3f);
            }

            _label = UiKit.Label(Area, "Hint", "SMALLEST FIRST", 28f, Palette.Text,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(tile * 0.5f + 60f)));
        }

        protected override void OnTick(float dt)
        {
            if (Elapsed > 12f) Fail("TOO SLOW");
        }

        private void OnTile(int index)
        {
            if (!CanAct) return;
            if (index != _order[_index])
            {
                _tiles[index].color = Palette.Red;
                Fail("OUT OF ORDER");
                return;
            }

            _tiles[index].color = Palette.Green;
            _index++;
            if (_index < _order.Length) return;

            _label.text = "SORTED";
            _label.color = Palette.Green;
            Finish(false, Ms(Elapsed));
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_index < _order.Length) _tiles[_order[_index]].color = Palette.Accent;
            Finish(true, WorstMetric);
        }
    }
}
