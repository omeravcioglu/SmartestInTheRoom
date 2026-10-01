using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Kim's game. A tray of objects (shapes in colours) shows for a moment, blinks away, and
    /// comes back one short; pick the one that went from four choices. From level four the
    /// ones left also get shuffled about, so it's the set you have to remember, not the
    /// places. Wrong pick and you're out. Ranked by time.
    /// </summary>
    public class WhatsMissingGame : MinigameView
    {
        private const float Blink = 0.5f;
        private const float AnswerFor = 7f;
        private const int Choices = 4;

        // Shapes: 0 dot, 1 square, 2 tall bar, 3 ring. Colours: gold, blue, ink, red.
        private static readonly Color[] Colours = { Palette.Gold, Palette.Blue, Palette.Ink, Palette.Red };

        private readonly List<int> _items = new List<int>();          // item types on the tray
        private readonly List<RectTransform> _views = new List<RectTransform>();
        private readonly List<Vector2> _spots = new List<Vector2>();
        private readonly List<Vector2> _shuffled = new List<Vector2>();
        private int _missing;
        private int[] _options = new int[0];
        private Rect[] _optionRects = new Rect[0];
        private RectTransform[] _optionViews = new RectTransform[0];
        private float _showFor;
        private bool _shuffle;
        private int _stage;         // 0 showing, 1 blink, 2 asking
        private float _askedAt;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int k;
            switch (Level)
            {
                case 1: k = 4; _showFor = 2.5f; _shuffle = false; break;
                case 2: k = 5; _showFor = 2.3f; _shuffle = false; break;
                case 3: k = 6; _showFor = 2.1f; _shuffle = false; break;
                case 4: k = 7; _showFor = 2.0f; _shuffle = true; break;
                default: k = Mathf.Min(10, 8 + (Level - 5) / 2); _showFor = Mathf.Max(1.4f, 1.8f - (Level - 5) * 0.08f); _shuffle = true; break;
            }

            // The tray, in the top part of the area; the choices go along the bottom.
            var tray = new Rect(-size.x * 0.5f + 90f, -30f, size.x - 180f, size.y * 0.5f - 10f + 30f - 10f);
            UiKit.Box(Area, "Tray", tray.size + new Vector2(40f, 30f), tray.center, Palette.Panel);

            foreach (int t in LevelRng.Distinct(Rng, k, 16)) _items.Add(t);
            for (int i = 0; i < k; i++)
            {
                Vector2 p;
                int tries = 0;
                do p = new Vector2(RandomRange(tray.xMin + 50f, tray.xMax - 50f), RandomRange(tray.yMin + 50f, tray.yMax - 50f));
                while (TooClose(p, _spots) && ++tries < 40);
                _spots.Add(p);
                _views.Add(MakeItem(Area, "Item" + i, _items[i], p, 1f, Palette.Panel));
            }
            // Where everything moves to on a shuffle.
            _shuffled.AddRange(_spots);
            if (_shuffle) LevelRng.Shuffle(Rng, _shuffled);

            _missing = RandomRange(0, k);
            // The choices: the missing one and three that are still there, in a shuffled row.
            var picks = new List<int> { _items[_missing] };
            var others = new List<int>(_items);
            others.RemoveAt(_missing);
            LevelRng.Shuffle(Rng, others);
            for (int i = 0; i < Choices - 1 && i < others.Count; i++) picks.Add(others[i]);
            LevelRng.Shuffle(Rng, picks);
            _options = picks.ToArray();
            _optionRects = new Rect[_options.Length];
            _optionViews = new RectTransform[_options.Length];
            var tile = new Vector2(120f, 100f);
            for (int i = 0; i < _options.Length; i++)
            {
                var pos = new Vector2((i - (_options.Length - 1) * 0.5f) * (tile.x + 24f), -size.y * 0.5f + 118f);
                var box = UiKit.Box(Area, "Choice" + i, tile, pos, Palette.PaperHi);
                MakeItem(box.transform, "Shape", _options[i], Vector2.zero, 0.7f, Palette.PaperHi);
                _optionRects[i] = new Rect(pos - tile * 0.5f, tile);
                _optionViews[i] = (RectTransform)box.transform;
                box.gameObject.SetActive(false);
            }

            _label = UiKit.Label(Area, "Hint", "REMEMBER THEM ALL", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        private static bool TooClose(Vector2 p, List<Vector2> taken)
        {
            foreach (var q in taken) if (Vector2.Distance(p, q) < 110f) return true;
            return false;
        }

        /// <summary>One object: a shape in a colour, drawn from the kit's own pieces. A ring's hole shows what's behind.</summary>
        private static RectTransform MakeItem(Transform parent, string name, int type, Vector2 pos, float scale, Color behind)
        {
            int shape = type / 4;
            var colour = Colours[type % 4];
            var root = UiKit.Node(parent, name, Vector2.one * 80f, pos);
            switch (shape)
            {
                case 0: UiKit.Dot(root, "Dot", 68f, Vector2.zero, colour); break;
                case 1: UiKit.Box(root, "Square", new Vector2(62f, 62f), Vector2.zero, colour); break;
                case 2: UiKit.Box(root, "Bar", new Vector2(30f, 76f), Vector2.zero, colour); break;
                default:
                    UiKit.Dot(root, "Ring", 72f, Vector2.zero, colour);
                    UiKit.Dot(root, "Hole", 32f, Vector2.zero, behind);
                    break;
            }
            root.localScale = Vector3.one * scale;
            return root;
        }

        protected override void OnTick(float dt)
        {
            if (_stage == 0 && Elapsed >= _showFor)
            {
                _stage = 1;
                foreach (var v in _views) v.gameObject.SetActive(false);
                _label.text = "";
            }
            else if (_stage == 1 && Elapsed >= _showFor + Blink)
            {
                _stage = 2;
                _askedAt = Elapsed;
                for (int i = 0; i < _views.Count; i++)
                {
                    if (i == _missing) continue;
                    _views[i].gameObject.SetActive(true);
                    _views[i].anchoredPosition = _shuffled[i];
                }
                foreach (var o in _optionViews) o.gameObject.SetActive(true);
                _label.text = "WHAT'S MISSING?";
            }
            if (_stage < 2) return;

            if (!Interactive)
            {
                if (Elapsed - _askedAt > 1.2f) Reveal(-1);
                return;
            }
            if (!CanAct) return;
            if (Elapsed - _askedAt > AnswerFor) { Reveal(-1); Fail("TOO SLOW"); return; }
            if (!KeyInput.MousePressed() || !UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m)) return;

            for (int i = 0; i < _optionRects.Length; i++)
            {
                if (!_optionRects[i].Contains(m)) continue;
                Reveal(i);
                if (_options[i] == _items[_missing])
                {
                    _label.text = "SPOT ON";
                    _label.color = Palette.Green;
                    Finish(false, Ms(Elapsed - _askedAt));
                }
                else Fail("THAT ONE'S STILL THERE");
                return;
            }
        }

        /// <summary>Mark the right choice (and a wrong pick).</summary>
        private void Reveal(int picked)
        {
            for (int i = 0; i < _options.Length; i++)
            {
                var box = _optionViews[i].GetComponent<Image>();
                if (_options[i] == _items[_missing]) box.color = Palette.Green;
                else if (i == picked) box.color = Palette.Red;
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
