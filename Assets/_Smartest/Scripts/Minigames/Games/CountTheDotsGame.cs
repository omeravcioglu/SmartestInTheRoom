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
        private const float DotSize = 26f;

        private readonly List<RectTransform> _dots = new List<RectTransform>();
        private readonly List<Vector2> _from = new List<Vector2>();
        private readonly List<Vector2> _velocity = new List<Vector2>();
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
                // Keep every dot clear of the others: two dots stacked on top of each other
                // make the right answer unknowable, which is a coin flip, not a skill.
                Vector2 pos = default;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    pos = new Vector2(RandomRange(-fieldW * 0.45f, fieldW * 0.45f),
                                      50f + RandomRange(-fieldH * 0.4f, fieldH * 0.4f));
                    if (ClearOfOthers(pos)) break;
                }
                // From level four the dots drift while they're up, which is what makes a
                // quick count harder than a glance at a still picture.
                var velocity = moving ? new Vector2(RandomRange(-70f, 70f), RandomRange(-40f, 40f)) : Vector2.zero;
                var rt = (RectTransform)UiKit.Dot(Area, "Dot" + i, DotSize, pos, Palette.Accent).transform;
                _dots.Add(rt);
                _from.Add(pos);
                _velocity.Add(velocity);
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

        private bool ClearOfOthers(Vector2 pos)
        {
            for (int i = 0; i < _from.Count; i++)
                if (Vector2.Distance(pos, _from[i]) < DotSize + 6f) return false;
            return true;
        }

        protected override void OnTick(float dt)
        {
            if (!_hidden)
                for (int i = 0; i < _dots.Count; i++)
                    if (_velocity[i] != Vector2.zero) _dots[i].anchoredPosition = _from[i] + _velocity[i] * Elapsed;

            if (!_hidden && Elapsed >= _flashFor)
            {
                _hidden = true;
                foreach (var d in _dots) d.gameObject.SetActive(false);
                for (int v = 1; v < _answers.Length; v++) _answers[v].color = Palette.Neutral;
                if (_label != null) { _label.text = "HOW MANY?"; _label.color = Palette.Text; }
            }
            if (_hidden && Elapsed - _flashFor > 8f) Fail("TOO SLOW");

            // The rule card's demo takes a beat once the dots have gone, then clicks the count.
            if (Demo && _hidden && !IsDone && Elapsed - _flashFor >= 0.35f)
            {
                PointAt(Where(_answers[_count]));
                if (Elapsed - _flashFor >= 0.7f)
                {
                    TapAt(Where(_answers[_count]));
                    OnAnswer(_count);
                }
            }
        }

        private void OnAnswer(int value)
        {
            if (!CanMove || !_hidden) return;
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
