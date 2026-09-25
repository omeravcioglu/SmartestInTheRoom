using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// One fuse, the same length for everyone, burning at the same moment. Nobody can see
    /// anyone else's nerve, which is the whole game: bank early and safe, or hold and hope
    /// the others blinked first.
    /// </summary>
    public class PressYourLuckGame : MinigameView
    {
        private TMP_Text _bankText;
        private TMP_Text _label;
        private Image _fill;
        private float _trackW;
        private float _bustAt;
        private int _bank;

        /// <summary>Higher is better here, so the worst possible result is nothing at all.</summary>
        public override int WorstMetric => 0;

        protected override void Build()
        {
            var size = AreaSize;
            float min, max;
            switch (Level)
            {
                case 1: min = 6f; max = 12f; break;
                case 2: min = 4f; max = 10f; break;
                case 3: min = 3f; max = 8f; break;
                case 4: min = 2f; max = 7f; break;
                default: min = Mathf.Max(1f, 1.5f - (Level - 5) * 0.1f); max = Mathf.Max(3f, 6f - (Level - 5) * 0.5f); break;
            }
            _bustAt = RandomRange(min, max);

            _bankText = UiKit.Label(Area, "Bank", "0", 96f, Palette.Accent,
                new Vector2(size.x - 60f, 130f), new Vector2(0f, 40f));
            _trackW = Mathf.Min(620f, size.x - 80f);
            UiKit.Track(Area, "Fuse", new Vector2(_trackW, 22f), new Vector2(0f, -60f),
                Palette.Neutral, Palette.Red, out _fill);
            UiKit.SetFill(_fill, 0f, _trackW);
            _label = UiKit.Label(Area, "Hint", "SPACE to bank it", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -110f));
        }

        protected override void OnTick(float dt)
        {
            _bank = Mathf.FloorToInt(Elapsed * 10f);
            if (_bankText != null) _bankText.text = _bank.ToString();
            // The bar shows how long you've held, not how close the bust is — that stays hidden.
            UiKit.SetFill(_fill, Mathf.Clamp01(Elapsed / 8f), _trackW);

            if (Elapsed >= _bustAt)
            {
                if (_bankText != null) { _bankText.text = "BUST"; _bankText.color = Palette.Red; }
                if (_label != null) { _label.text = "Nothing banked."; _label.color = Palette.Red; }
                Finish(true, 0);
                return;
            }

            if (!CanAct || !KeyInput.SpacePressed()) return;

            if (_label != null) { _label.text = "Banked " + _bank; _label.color = Palette.Green; }
            if (_bankText != null) _bankText.color = Palette.Green;
            Finish(false, _bank);
        }
    }
}
