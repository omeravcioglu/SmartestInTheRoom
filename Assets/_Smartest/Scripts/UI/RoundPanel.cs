using System.Collections.Generic;
using Smartest.Core;
using Smartest.Net;
using Smartest.Rounds;
using TMPro;
using UnityEngine;

namespace Smartest.UI
{
    /// <summary>
    /// The centre panel for social rounds: title, question, ONE short rule under it, the
    /// input for this round's type, and the countdown. Sends the local player's answer
    /// once, then locks. The rule never goes on a button — a button says RED, and that's all.
    /// </summary>
    public class RoundPanel : Panel
    {
        [Header("Round")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text promptText;
        [Tooltip("The one-line rule. Sits under the question, NOT on the buttons.")]
        [SerializeField] private TMP_Text rulesText;
        [SerializeField] private TMP_Text subLineText;
        [SerializeField] private TMP_Text tieText;
        [SerializeField] private TMP_Text lockedHint;

        [Header("Answers")]
        [SerializeField] private AnswerButton buttonA;   // red / yes
        [SerializeField] private AnswerButton buttonB;   // green / no
        [SerializeField] private RectTransform numberGrid;
        [SerializeField] private AnswerButton[] numberButtons = new AnswerButton[11]; // index = value 0..10
        [SerializeField] private CountdownBar countdown;

        private RoundDefinition _def;
        private bool _answering;
        private bool _locked;
        private readonly List<AnswerButton> _active = new List<AnswerButton>();

        protected override void Awake()
        {
            base.Awake();
            if (buttonA != null) buttonA.Clicked += OnAnswerClicked;
            if (buttonB != null) buttonB.Clicked += OnAnswerClicked;
            for (int i = 0; i < numberButtons.Length; i++)
                if (numberButtons[i] != null) numberButtons[i].Clicked += OnAnswerClicked;
        }

        public void ShowRound(RoundDefinition def, int subRound, bool tieBreak)
        {
            _def = def;
            _answering = false;
            _locked = false;
            _active.Clear();

            if (titleText != null) titleText.text = def != null ? def.title.ToUpperInvariant() : string.Empty;
            if (promptText != null) promptText.text = def != null ? def.prompt : string.Empty;

            string sub = def != null ? def.subLine : string.Empty;
            if (def != null && def.subRounds > 1)
                sub = $"Pull {subRound} of {def.subRounds} — pot is {def.Param(0, 5) * subRound}"
                      + (string.IsNullOrEmpty(def.subLine) ? string.Empty : "\n" + def.subLine);
            if (subLineText != null)
            {
                subLineText.text = sub;
                subLineText.gameObject.SetActive(!string.IsNullOrEmpty(sub));
            }
            if (tieText != null) tieText.gameObject.SetActive(tieBreak);
            if (lockedHint != null) lockedHint.gameObject.SetActive(false);

            bool numbers = def != null && def.inputType == InputType.Number1to10;
            bool twoButton = def != null && (def.inputType == InputType.RedGreen || def.inputType == InputType.YesNo);

            if (buttonA != null) buttonA.gameObject.SetActive(twoButton);
            if (buttonB != null) buttonB.gameObject.SetActive(twoButton);
            if (numberGrid != null) numberGrid.gameObject.SetActive(numbers);

            // The rule lives here, under the question — never on the buttons.
            if (rulesText != null)
            {
                string rule = Colourise(def != null ? def.ruleText : string.Empty);
                rulesText.text = rule;
                rulesText.gameObject.SetActive(!string.IsNullOrEmpty(rule));
            }

            if (def == null) return;

            if (def.inputType == InputType.RedGreen)
            {
                buttonA?.Setup(1, "RED", Palette.Red);
                buttonB?.Setup(0, "GREEN", Palette.Green);
                _active.Add(buttonA); _active.Add(buttonB);
            }
            else if (def.inputType == InputType.YesNo)
            {
                buttonA?.Setup(1, NameOf(def.buttonNameA, "YES"), Palette.Neutral);
                buttonB?.Setup(0, NameOf(def.buttonNameB, "NO"), Palette.Neutral);
                _active.Add(buttonA); _active.Add(buttonB);
            }
            else if (numbers)
            {
                for (int v = 0; v < numberButtons.Length; v++)
                {
                    var b = numberButtons[v];
                    if (b == null) continue;
                    bool show = v > 0 || def.allowZero;
                    b.gameObject.SetActive(show);
                    if (show)
                    {
                        b.Setup(v, v.ToString(), Palette.Neutral);
                        _active.Add(b);
                    }
                }
            }

            foreach (var b in _active) if (b != null) b.SetInteractable(false);
            if (countdown != null) countdown.SetVisible(false);
        }

        private static string NameOf(string custom, string fallback)
            => string.IsNullOrWhiteSpace(custom) ? fallback : custom.ToUpperInvariant();

        /// <summary>
        /// Tints the words Red and Green inside the rule so the sentence and the buttons
        /// read as one thing. The rule itself is authored as plain English in RoundCatalog.
        /// </summary>
        private static string Colourise(string rule)
        {
            if (string.IsNullOrEmpty(rule)) return string.Empty;
            var sb = new System.Text.StringBuilder(rule.Length + 96);
            int i = 0;
            while (i < rule.Length)
            {
                if (!char.IsLetter(rule[i])) { sb.Append(rule[i]); i++; continue; }
                int start = i;
                while (i < rule.Length && char.IsLetter(rule[i])) i++;
                string word = rule.Substring(start, i - start);
                switch (word.ToLowerInvariant())
                {
                    case "red":
                    case "reds": sb.Append(Wrap(word, Palette.Red)); break;
                    case "green":
                    case "greens": sb.Append(Wrap(word, Palette.Green)); break;
                    default: sb.Append(word); break;
                }
            }
            return sb.ToString();
        }

        private static string Wrap(string word, Color color)
            => $"<color={Palette.ToHex(color)}><b>{word}</b></color>";

        public void SetAnswering(bool on)
        {
            _answering = on;
            if (countdown != null) countdown.SetVisible(on);
            if (!_locked)
                foreach (var b in _active) if (b != null) b.SetInteractable(on);
        }

        public void Tick(float remaining, float duration)
        {
            if (countdown != null && _answering) countdown.Set(remaining, duration);
        }

        private void OnAnswerClicked(AnswerButton btn)
        {
            if (!_answering || _locked || btn == null || _def == null) return;
            if (!_def.IsValidAnswer(btn.Value)) return;
            _locked = true;
            Sounds.Play(Sounds.Kind.LockIn);
            foreach (var b in _active) if (b != null) b.SetLocked(b == btn);
            if (lockedHint != null) lockedHint.gameObject.SetActive(true);
            var local = PlayerData.Local;
            if (local != null) local.SubmitAnswerRpc(btn.Value);
        }
    }
}
