using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Pairs, the card game, against the clock. Every card shows its number for a moment,
    /// then they all turn over and you flip two at a time to find the matches. A wrong pair
    /// costs only the second it takes to turn back. Ranked by time.
    /// </summary>
    public class PairsGame : MinigameView
    {
        private const float MismatchShow = 0.6f;

        private Image[] _cards = new Image[0];
        private TMP_Text[] _faces = new TMP_Text[0];
        private GameObject[] _backs = new GameObject[0];
        private int[] _valueOf = new int[0];
        private bool[] _matched = new bool[0];
        private float _preview;
        private float _limit;
        private int _first = -1;
        private int _second = -1;
        private float _turnBackAt = -1f;
        private int _pairs;
        private int _pairsLeft;
        private bool _hidden;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int pairs, cols;
            switch (Level)
            {
                case 1: pairs = 3; cols = 3; _preview = 1.5f; break;
                case 2: pairs = 4; cols = 4; _preview = 1.2f; break;
                case 3: pairs = 5; cols = 5; _preview = 1.0f; break;
                case 4: pairs = 6; cols = 4; _preview = 0.8f; break;
                default: pairs = 7; cols = 7; _preview = Mathf.Max(0.4f, 0.6f - (Level - 5) * 0.05f); break;
            }
            _limit = 18f;
            int n = pairs * 2, rows = Mathf.CeilToInt(n / (float)cols);
            _pairs = _pairsLeft = pairs;
            Progress("PAIRS", 0, pairs);

            var values = new int[n];
            for (int i = 0; i < n; i++) values[i] = i / 2 + 1;
            // Shuffle into the grid.
            for (int i = n - 1; i > 0; i--)
            {
                int j = RandomRange(0, i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }

            float gap = 14f;
            float w = Mathf.Min(140f, (size.x - 100f - (cols - 1) * gap) / cols);
            float h = Mathf.Min(150f, (size.y - 110f - (rows - 1) * gap) / rows);
            float totalW = cols * w + (cols - 1) * gap, totalH = rows * h + (rows - 1) * gap;
            var origin = new Vector2(-totalW * 0.5f + w * 0.5f, 20f + totalH * 0.5f - h * 0.5f);

            _cards = new Image[n];
            _faces = new TMP_Text[n];
            _backs = new GameObject[n];
            _valueOf = values;
            _matched = new bool[n];
            for (int i = 0; i < n; i++)
            {
                int c = i % cols, r = i / cols;
                int card = i;
                _cards[i] = UiKit.Cell(Area, "Card" + i, new Vector2(w, h), origin + new Vector2(c * (w + gap), -r * (h + gap)),
                    Palette.PanelRaised, () => OnCard(card), out _faces[i], values[i].ToString(), 40f);
                // The back of the card: a gold star on ink.
                _backs[i] = UiKit.Marker(_cards[i].transform, "Back", UiKit.Mark.Star, Mathf.Min(w, h) * 0.5f, Vector2.zero, Palette.Gold).gameObject;
                _backs[i].SetActive(false);
            }

            _label = UiKit.Label(Area, "Hint", "REMEMBER THEM", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        protected override void OnTick(float dt)
        {
            if (!_hidden && Elapsed >= _preview)
            {
                _hidden = true;
                for (int i = 0; i < _cards.Length; i++) FaceDown(i);
                _label.text = "FIND THE PAIRS";
            }
            if (_turnBackAt >= 0f && Elapsed >= _turnBackAt)
            {
                FaceDown(_first);
                FaceDown(_second);
                _first = _second = -1;
                _turnBackAt = -1f;
            }
            if (CanAct && Elapsed > _limit) Fail("TOO SLOW");

            // The rule card's demo remembers every card: the first one left, then its match, a card a beat.
            if (Demo && _hidden && !IsDone && Elapsed - _preview >= 0.35f)
            {
                int card = -1;
                for (int i = 0; i < _cards.Length && card < 0; i++)
                    if (!_matched[i] && i != _first && (_first < 0 || _valueOf[i] == _valueOf[_first])) card = i;
                if (card < 0) return;
                int turned = (_pairs - _pairsLeft) * 2 + (_first >= 0 ? 1 : 0);
                PointAt(Where(_cards[card]));
                if (Elapsed - _preview >= 0.7f + turned * 0.5f)
                {
                    TapAt(Where(_cards[card]));
                    OnCard(card);
                }
            }
        }

        private void FaceDown(int i)
        {
            _faces[i].text = string.Empty;
            _backs[i].SetActive(true);
            _cards[i].color = Palette.Ink; // a card back: nothing like a face-up card
        }

        private void FaceUp(int i)
        {
            _faces[i].text = _valueOf[i].ToString();
            _backs[i].SetActive(false);
            _cards[i].color = Palette.PanelRaised;
        }

        private void OnCard(int card)
        {
            // Not during the preview, not while a wrong pair is still showing, not a card
            // already turned up.
            if (!CanMove || !_hidden || _turnBackAt >= 0f || _matched[card] || card == _first) return;

            FaceUp(card);
            if (_first < 0) { _first = card; return; }

            _second = card;
            if (_valueOf[_first] == _valueOf[_second])
            {
                _matched[_first] = _matched[_second] = true;
                _cards[_first].color = Palette.Green;
                _cards[_second].color = Palette.Green;
                _first = _second = -1;
                Progress("PAIRS", _pairs - (_pairsLeft - 1), _pairs);
                if (--_pairsLeft == 0)
                {
                    _label.text = "ALL MATCHED";
                    _label.color = Palette.Green;
                    Finish(false, Ms(Elapsed));
                }
            }
            else
            {
                _cards[_first].color = Palette.Red;
                _cards[_second].color = Palette.Red;
                _turnBackAt = Elapsed + MismatchShow;
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            // Show every card, so the ones missed are plain to see.
            for (int i = 0; i < _cards.Length; i++) if (!_matched[i]) FaceUp(i);
            Finish(true, WorstMetric);
        }
    }
}
