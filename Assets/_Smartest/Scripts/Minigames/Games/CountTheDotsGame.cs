using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Estimation. Nobody actually counts twelve dots in four tenths of a second — what
    /// this really measures is how good your guess is when counting isn't an option.
    /// </summary>
    public class CountTheDotsGame : MinigameView
    {
        private readonly List<RectTransform> _dots = new List<RectTransform>();
        private Image[] _answers;
        private TMP_Text _label;
        private int _count;
        private float _flashFor;
        private bool _hidden;

        protected override void Build()
        {
            var size = AreaSize;
            int min, max;
            bool moving = false;
            switch (Level)
            {
                case 1: min = 3; max = 6; _flashFor = 1.5f; break;
                case 2: min = 4; max = 8; _flashFor = 1.0f; break;
                case 3: min = 5; max = 10; _flashFor = 0.8f; break;
                case 4: min = 5; max = 10; _flashFor = 0.6f; moving = true; break;
                default: min = 4; max = 10; _flashFor = Mathf.Max(0.25f, 0.45f - (Level - 5) * 0.05f); moving = true; break;
            }
            _count = RandomRange(min, max + 1);

            float fieldW = Mathf.Min(720f, size.x - 80f);
            float fieldH = Mathf.Min(240f, size.y - 180f);
            UiKit.Box(Area, "Field", new Vector2(fieldW, fieldH), new Vector2(0f, 50f), Palette.Panel);

            for (int i = 0; i < _count; i++)
            {
                var pos = new Vector2(RandomRange(-fieldW * 0.45f, fieldW * 0.45f),
                                      50f + RandomRange(-fieldH * 0.4f, fieldH * 0.4f));
                var rt = (RectTransform)UiKit.Dot(Area, "Dot" + i, 26f, pos, Palette.Accent).transform;
                if (moving) rt.localRotation = Quaternion.identity;
                _dots.Add(rt);
            }

            // Answer tiles appear straight away but do nothing until the dots are gone.
            int options = 10;
            float tile = Mathf.Min(66f, (size.x - 80f - (options - 1) * 8f) / options);
            float total = options * tile + (options - 1) * 8f;
            _answers = new Image[options + 1];
            for (int v = 1; v <= options; v++)
            {
                float x = -total * 0.5f + tile * 0.5f + (v - 1) * (tile + 8f);
                int captured = v;
                _answers[v] = UiKit.Cell(Area, "Ans" + v, new Vector2(tile, tile),
                    new Vector2(x, -(fieldH * 0.5f + 20f)), Palette.Neutral,
                    () => OnAnswer(captured), out _, v.ToString(), tile * 0.42f);
                _answers[v].color = Palette.Panel;
            }

            _label = UiKit.Label(Area, "Hint", "COUNT", 28f, Palette.Accent,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(fieldH * 0.5f + 80f)));
        }

        protected override void OnTick(float dt)
        {
            if (!_hidden && Elapsed >= _flashFor)
            {
                _hidden = true;
                foreach (var d in _dots) d.gameObject.SetActive(false);
                for (int v = 1; v < _answers.Length; v++) _answers[v].color = Palette.Neutral;
                if (_label != null) { _label.text = "HOW MANY?"; _label.color = Palette.Text; }
            }
            if (_hidden && Elapsed - _flashFor > 8f) Fail("TOO SLOW");
        }

        private void OnAnswer(int value)
        {
            if (!CanAct || !_hidden) return;
            if (value != _count)
            {
                _answers[value].color = Palette.Red;
                Fail("IT WAS " + _count);
                return;
            }
            _answers[value].color = Palette.Green;
            _label.text = "CORRECT";
            _label.color = Palette.Green;
            Finish(false, Ms(Elapsed - _flashFor));
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            if (_answers != null && _count < _answers.Length && _answers[_count] != null)
                _answers[_count].color = Palette.Green;
            Finish(true, WorstMetric);
        }
    }
}
