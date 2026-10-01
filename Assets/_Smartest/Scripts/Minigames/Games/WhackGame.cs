using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Whack-a-mole on a three-by-three board. Moles pop up and duck again; whack each one
    /// while it's up. Some holes throw up a bomb instead: leave it. A whack that lands flashes
    /// the hole green, a mistake turns it red and crossed. Ranked by total reaction time.
    /// </summary>
    public class WhackGame : MinigameView
    {
        private const int N = 3;
        private const float HitFlash = 0.15f;
        private const float PopFor = 0.09f;

        private struct Mole
        {
            public int Hole;
            public float Up;       // when it pops up
            public bool Red;
            public bool Hit;
            public bool Done;
        }

        private Mole[] _moles = new Mole[0];
        private Image[] _holes = new Image[0];
        private RectTransform[] _moleViews = new RectTransform[0];
        private RectTransform[] _bombViews = new RectTransform[0];
        private float _popBase;
        private float[] _flashUntil = new float[N * N];
        private float _upFor;
        private float _reactionSum;
        private int _golds;
        private int _whacked;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int n;
            float spacing, redShare;
            switch (Level)
            {
                case 1: n = 6; _upFor = 1.3f; spacing = 0.95f; redShare = 0f; break;
                case 2: n = 8; _upFor = 1.1f; spacing = 0.8f; redShare = 0.12f; break;
                case 3: n = 10; _upFor = 0.95f; spacing = 0.7f; redShare = 0.2f; break;
                case 4: n = 12; _upFor = 0.85f; spacing = 0.6f; redShare = 0.25f; break;
                default:
                    n = Mathf.Min(16, 13 + (Level - 5));
                    _upFor = Mathf.Max(0.6f, 0.75f - (Level - 5) * 0.02f);
                    spacing = Mathf.Max(0.45f, 0.55f - (Level - 5) * 0.015f);
                    redShare = 0.3f;
                    break;
            }

            float available = Mathf.Min(size.x - 80f, size.y - 100f);
            float cell = UiKit.CellSize(N, available, 14f);
            var board = UiKit.Node(Area, "Board", Vector2.zero, new Vector2(0f, 20f));
            _holes = UiKit.Grid(board, N, cell, 14f, Palette.Neutral, OnHole);

            // Each cell has a hole, and a window above it that whatever pops up rises through.
            _moleViews = new RectTransform[_holes.Length];
            _bombViews = new RectTransform[_holes.Length];
            float holeY = -cell * 0.24f, popH = cell * 0.7f, mole = cell * 0.66f;
            _popBase = -popH * 0.5f;
            for (int h = 0; h < _holes.Length; h++)
            {
                var spot = _holes[h].transform;
                UiKit.Art(spot, "Hole", "hole", new Vector2(cell * 0.78f, cell * 0.24f), new Vector2(0f, holeY));
                var pop = UiKit.Node(spot, "Pop", new Vector2(cell - 8f, popH), new Vector2(0f, holeY + popH * 0.5f));
                pop.gameObject.AddComponent<RectMask2D>();
                _moleViews[h] = UiKit.Art(pop, "Mole", "mole", new Vector2(mole, mole), Vector2.zero).rectTransform;
                _bombViews[h] = UiKit.Art(pop, "Bomb", "bomb", new Vector2(80f, 84f) * (mole * 0.8f / 80f), Vector2.zero).rectTransform;
                foreach (var v in new[] { _moleViews[h], _bombViews[h] })
                {
                    v.pivot = new Vector2(0.5f, 0f);
                    v.gameObject.SetActive(false);
                }
            }

            // Never two moles in one hole at once, and the first one is always gold.
            _moles = new Mole[n];
            var busyUntil = new float[N * N];
            for (int i = 0; i < n; i++)
            {
                float up = 0.6f + i * spacing;
                int hole;
                int tries = 0;
                do hole = RandomRange(0, N * N); while (busyUntil[hole] > up && ++tries < 20);
                busyUntil[hole] = up + _upFor + 0.2f;
                _moles[i] = new Mole { Hole = hole, Up = up, Red = i > 0 && RandomRange(0f, 1f) < redShare };
                if (!_moles[i].Red) _golds++;
            }
            Progress("WHACKED", 0, _golds);

            _label = UiKit.Label(Area, "Hint", "WHACK THE GOLD", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        /// <summary>The mole up in this hole right now, or -1.</summary>
        private int MoleIn(int hole)
        {
            for (int i = 0; i < _moles.Length; i++)
            {
                var m = _moles[i];
                if (m.Hole == hole && !m.Done && Elapsed >= m.Up && Elapsed < m.Up + _upFor) return i;
            }
            return -1;
        }

        protected override void OnTick(float dt)
        {
            bool allDone = true;
            for (int i = 0; i < _moles.Length; i++)
            {
                ref var m = ref _moles[i];
                if (m.Done) continue;
                allDone = false;
                if (Elapsed >= m.Up + _upFor)
                {
                    m.Done = true;
                    if (!m.Red && CanAct) { Fail("ONE GOT AWAY", m.Hole); return; }
                }
            }

            if (Demo) PlayDemo();

            // Up they come and down they go; a hole just whacked flashes green.
            for (int h = 0; h < _holes.Length; h++)
            {
                int m = MoleIn(h);
                ShowPopUp(h, m);
                var c = Elapsed < _flashUntil[h] ? Palette.Green : Palette.Neutral;
                if (_holes[h].color != c) _holes[h].color = c;
            }

            if (allDone && CanMove)
            {
                _label.text = "ALL WHACKED";
                _label.color = Palette.Green;
                Finish(false, Ms(_reactionSum));
            }
        }

        /// <summary>The rule card's demo: the hand goes to each gold one as it pops up and whacks it a beat later. Bombs it leaves.</summary>
        private void PlayDemo()
        {
            for (int i = 0; i < _moles.Length; i++)
            {
                var m = _moles[i];
                if (m.Done || m.Red || Elapsed < m.Up + 0.05f) continue;
                var at = Where(_holes[m.Hole]);
                PointAt(at);
                if (Elapsed < m.Up + 0.32f) return;
                TapAt(at);
                OnHole(m.Hole);
                return;
            }
        }

        /// <summary>The mole (or bomb) in this hole, risen as far as it has got; nothing if the hole is empty.</summary>
        private void ShowPopUp(int hole, int m)
        {
            var mole = _moleViews[hole];
            var bomb = _bombViews[hole];
            bool up = m >= 0;
            if (mole.gameObject.activeSelf != (up && !_moles[m].Red)) mole.gameObject.SetActive(up && !_moles[m].Red);
            if (bomb.gameObject.activeSelf != (up && _moles[m].Red)) bomb.gameObject.SetActive(up && _moles[m].Red);
            if (!up) return;

            var view = _moles[m].Red ? bomb : mole;
            float t = Elapsed - _moles[m].Up;
            float risen = Mathf.Clamp01(Mathf.Min(t, _upFor - t) / PopFor);
            risen = risen * risen * (3f - 2f * risen);
            // A bomb sits a little lower: it's round at the bottom, a mole is cut off by the hole.
            float rest = _popBase - (_moles[m].Red ? view.sizeDelta.y * 0.12f : 0f);
            view.anchoredPosition = new Vector2(0f, rest - (1f - risen) * view.sizeDelta.y);
        }

        private void OnHole(int hole)
        {
            if (!CanMove) return;
            int i = MoleIn(hole);
            if (i < 0) return; // an empty hole: no harm done
            if (_moles[i].Red) { Fail("THAT WAS A BOMB", hole); return; }
            _moles[i].Hit = true;
            _moles[i].Done = true;
            Progress("WHACKED", ++_whacked, _golds);
            _reactionSum += Elapsed - _moles[i].Up;
            _flashUntil[hole] = Elapsed + HitFlash;
            _holes[hole].color = Palette.Green;
            ShowPopUp(hole, -1);
        }

        private void Fail(string why, int hole)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            _holes[hole].color = Palette.Red;
            Finish(true, WorstMetric);
        }
    }
}
