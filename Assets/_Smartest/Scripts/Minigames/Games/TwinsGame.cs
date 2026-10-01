using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Snap, the card game, on your own: cards flip one after another, and when one matches
    /// the card before it you click before the next flip. Click on a card that isn't a match
    /// and you're out. Each number has its own colour on the early levels; from level four
    /// the colours are shuffled, so only the number counts. Ranked by total reaction time.
    /// </summary>
    public class TwinsGame : MinigameView
    {
        private const float FirstFlip = 0.6f;
        private const float FlipTime = 0.1f;

        private static readonly Color[] Inks = { Palette.Gold, Palette.Blue, Palette.Ink, Palette.Red, Palette.Green, Palette.PaperHi };

        private int[] _values = new int[0];
        private int[] _colours = new int[0];
        private float _interval;
        private int _shown = -1;
        private bool _answered;
        private float _reactionSum;
        private RectTransform _card;
        private Image _cardImage;
        private TMP_Text _number;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int n;
            bool shuffleColours;
            switch (Level)
            {
                case 1: n = 8; _interval = 1.3f; shuffleColours = false; break;
                case 2: n = 10; _interval = 1.1f; shuffleColours = false; break;
                case 3: n = 12; _interval = 0.95f; shuffleColours = false; break;
                case 4: n = 14; _interval = 0.85f; shuffleColours = true; break;
                default:
                    n = 16;
                    _interval = Mathf.Max(0.55f, 0.75f - (Level - 5) * 0.03f);
                    shuffleColours = true;
                    break;
            }

            // About three flips in ten are twins; never three the same in a row, and at least two
            // twins a level.
            _values = new int[n];
            _colours = new int[n];
            int twins = 0;
            for (int i = 0; i < n; i++)
            {
                bool twin = i > 0 && !(i > 1 && _values[i - 1] == _values[i - 2]) && RandomRange(0f, 1f) < 0.3f;
                if (!twin && i >= n - 3 && twins < 2 && i > 0 && !(i > 1 && _values[i - 1] == _values[i - 2])) twin = true;
                int v;
                if (twin) v = _values[i - 1];
                else
                {
                    do v = RandomRange(1, 7); while (i > 0 && v == _values[i - 1]);
                }
                if (twin) twins++;
                _values[i] = v;
                _colours[i] = shuffleColours ? RandomRange(0, Inks.Length) : v - 1;
            }

            // The rule card cuts its demo off at seven seconds, on the fifth card. If this deck's
            // first pair comes later, the demo deals one sooner (never three in a row), so it gets
            // to show a click.
            if (Demo)
            {
                int first = 1;
                while (first < n && !IsTwin(first)) first++;
                for (int i = 2; first > 4 && i <= 4; i++)
                {
                    if (_values[i + 1] == _values[i - 1]) continue;
                    _values[i] = _values[i - 1];
                    if (!shuffleColours) _colours[i] = _values[i] - 1;
                    break;
                }
            }

            // A plain box, not a kit cell: a red or green card here is just a colour, and a cell
            // would stamp it with a cross or a tick.
            _cardImage = UiKit.Box(Area, "Card", new Vector2(220f, 280f), new Vector2(0f, 30f), Palette.PanelRaised);
            _card = (RectTransform)_cardImage.transform;
            _number = UiKit.Label(_card, "Number", string.Empty, 110f, Palette.Ink, new Vector2(200f, 240f), Vector2.zero);
            _label = UiKit.Label(Area, "Hint", "CLICK ON A PAIR", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        private bool IsTwin(int i) => i > 0 && _values[i] == _values[i - 1];

        protected override void OnTick(float dt)
        {
            int due = Mathf.FloorToInt((Elapsed - FirstFlip) / _interval);
            if (due > _shown)
            {
                // The card we're leaving was a pair nobody clicked.
                if (CanMove && _shown >= 0 && IsTwin(_shown) && !_answered) { Fail("MISSED A PAIR"); return; }
                if (due >= _values.Length)
                {
                    if (CanMove)
                    {
                        _label.text = "SHARP";
                        _label.color = Palette.Green;
                        Finish(false, Ms(_reactionSum));
                    }
                    return;
                }
                _shown = due;
                _answered = false;
                Progress("CARDS", _shown + 1, _values.Length);
                var ink = Inks[_colours[_shown]];
                _cardImage.color = ink;
                _number.text = _values[_shown].ToString();
                // Light numbers on the dark cards.
                _number.color = ink.grayscale < 0.45f ? Palette.PaperHi : Palette.Ink;
                _label.text = "CLICK ON A PAIR";
                _label.color = Palette.TextDim;
            }

            // A quick squash on every flip, so a repeat still reads as a new card.
            float since = Elapsed - FirstFlip - _shown * _interval;
            _card.localScale = new Vector3(_shown < 0 ? 1f : Mathf.Clamp01(since / FlipTime), 1f, 1f);

            // The rule card's demo rests the pointer on the card, under the number, and clicks a
            // beat after a pair turns up.
            if (Demo && _shown >= 0)
            {
                var spot = Where(_card) + new Vector2(30f, -85f);
                PointAt(spot);
                if (IsTwin(_shown) && !_answered && since >= 0.4f)
                {
                    TapAt(spot);
                    Snap(since);
                }
                return;
            }

            if (!CanAct || _shown < 0 || !KeyInput.MousePressed() || _answered) return;
            Snap(since);
        }

        /// <summary>A click while a card shows: the player's, or the rule card's demo's.</summary>
        private void Snap(float since)
        {
            if (!CanMove || _shown < 0 || _answered) return;
            if (!IsTwin(_shown)) { Fail("NOT A PAIR"); return; }
            _answered = true;
            _reactionSum += since;
            _label.text = "PAIR!";
            _label.color = Palette.Green;
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
