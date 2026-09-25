using Smartest.Core;
using TMPro;
using UnityEngine;

namespace Smartest.Minigames
{
    /// <summary>
    /// Typing under pressure. Backspace is allowed — it just costs you time — so the
    /// question is whether you trust your fingers enough not to check.
    /// </summary>
    public class TypeItGame : MinigameView
    {
        private static readonly string[] Four = { "lamp", "gold", "rise", "kite", "wolf", "mint", "clay", "dusk" };
        private static readonly string[] Six = { "bridge", "candle", "orbits", "velvet", "marble", "puzzle", "tunnel", "silver" };
        private static readonly string[] Eight = { "lanterns", "quantity", "birthday", "notebook", "sandwich", "elephant", "keyboard", "mountain" };

        private TMP_InputField _field;
        private TMP_Text _target;
        private TMP_Text _label;
        private string _word;
        private bool _submitted;

        protected override void Build()
        {
            var size = AreaSize;
            switch (Level)
            {
                case 1: _word = Pick(Four); break;
                case 2: _word = Pick(Six); break;
                case 3: _word = Pick(Eight); break;
                case 4: _word = Pick(Six) + " " + Pick(Four); break;
                default: _word = Pick(Six) + " " + Pick(Four) + " " + Pick(Level >= 6 ? Eight : Four); break;
            }

            UiKit.Label(Area, "Caption", "TYPE THIS, THEN ENTER", 26f, Palette.Accent,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, 140f));
            _target = UiKit.Label(Area, "Word", _word, 60f, Palette.Text,
                new Vector2(size.x - 60f, 90f), new Vector2(0f, 70f));

            _field = UiKit.Field(Area, new Vector2(Mathf.Min(620f, size.x - 80f), 80f), new Vector2(0f, -30f));
            _field.onSubmit.AddListener(OnSubmit);
            _field.interactable = false;

            _label = UiKit.Label(Area, "Hint", string.Empty, 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -100f));
        }

        private string Pick(string[] pool) => pool[RandomRange(0, pool.Length)];

        protected override void OnBegin()
        {
            if (_field == null) return;
            _field.interactable = Interactive;
            if (Interactive) _field.ActivateInputField();
        }

        private void OnSubmit(string text)
        {
            if (_submitted || !CanAct) return;
            _submitted = true;
            if (string.Equals(text.Trim(), _word, System.StringComparison.Ordinal))
            {
                _label.text = "CLEAN";
                _label.color = Palette.Green;
                Finish(false, Ms(Elapsed));
            }
            else Fail("TYPO");
        }

        protected override void OnTick(float dt)
        {
            if (Elapsed > 10f && !IsDone) Fail("TOO SLOW");
        }

        protected override void OnTeardown()
        {
            if (_field != null) _field.onSubmit.RemoveListener(OnSubmit);
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_target != null) _target.color = Palette.Red;
            if (_field != null) _field.interactable = false;
            Finish(true, WorstMetric);
        }
    }
}
