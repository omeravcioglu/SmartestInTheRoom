using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Whack-a-mole on a three-by-three board. Gold moles pop up and duck again; whack each one
    /// while it's up. Red ones are bombs: leave them. The kit marks a red cell with a cross,
    /// which here says exactly the right thing. Ranked by total reaction time.
    /// </summary>
    public class WhackGame : MinigameView
    {
        private const int N = 3;
        private const float HitFlash = 0.15f;

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

            // Paint the board: a mole up is gold (or red); a hole just whacked flashes green.
            for (int h = 0; h < _holes.Length; h++)
            {
                int m = MoleIn(h);
                var c = Elapsed < _flashUntil[h] ? Palette.Green : m < 0 ? Palette.Neutral : _moles[m].Red ? Palette.Red : Palette.Accent;
                if (_holes[h].color != c) _holes[h].color = c;
            }

            if (allDone && CanAct)
            {
                _label.text = "ALL WHACKED";
                _label.color = Palette.Green;
                Finish(false, Ms(_reactionSum));
            }
        }

        private void OnHole(int hole)
        {
            if (!CanAct) return;
            int i = MoleIn(hole);
            if (i < 0) return; // an empty hole: no harm done
            if (_moles[i].Red) { Fail("THAT WAS A BOMB", hole); return; }
            _moles[i].Hit = true;
            _moles[i].Done = true;
            Progress("WHACKED", ++_whacked, _golds);
            _reactionSum += Elapsed - _moles[i].Up;
            _flashUntil[hole] = Elapsed + HitFlash;
            _holes[hole].color = Palette.Green;
        }

        private void Fail(string why, int hole)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            _holes[hole].color = Palette.Red;
            Finish(true, WorstMetric);
        }
    }
}
