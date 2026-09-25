using System.Collections;
using System.Collections.Generic;
using Smartest.Core;
using Smartest.Net;
using Smartest.Rounds;
using TMPro;
using UnityEngine;

namespace Smartest.UI
{
    /// <summary>
    /// Replaces the round panel after the timer: one row per player (staggered in),
    /// then the host reaction line, then (in Scoring) the deltas count up.
    /// </summary>
    public class RevealPanel : Panel
    {
        [Header("Reveal")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform rowsContainer;
        [SerializeField] private RevealRowView rowPrefab;
        [SerializeField] private TMP_Text reactionText;
        [SerializeField] private float rowHeight = 60f;
        [SerializeField] private float rowSpacing = 8f;

        private readonly List<RevealRowView> _rows = new List<RevealRowView>();
        private Coroutine _sequence;

        public void ShowResults(RoundDefinition def, IReadOnlyList<PlayerRoundResult> results, string reaction,
            float stagger)
        {
            if (_sequence != null) StopCoroutine(_sequence);
            foreach (var r in _rows) if (r != null) Destroy(r.gameObject);
            _rows.Clear();

            if (titleText != null) titleText.text = def != null ? def.title.ToUpperInvariant() : "REVEAL";
            if (reactionText != null)
            {
                reactionText.text = reaction ?? string.Empty;
                reactionText.alpha = 0f;
            }

            if (rowsContainer != null && rowPrefab != null && results != null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    var res = results[i];
                    var row = Instantiate(rowPrefab, rowsContainer);
                    var rt = (RectTransform)row.transform;
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.sizeDelta = new Vector2(0f, rowHeight);
                    rt.anchoredPosition = new Vector2(0f, -i * (rowHeight + rowSpacing));

                    var p = PlayerData.Get(res.ClientId);
                    string name = p != null ? p.DisplayName : "Player";
                    bool local = p != null && p.IsOwner;
                    AnswerLabel(def, res.Answer, res.Place, out string label, out Color color);
                    string note = res.Place == 1 && def != null && def.IsMinigame ? "WINNER" : string.Empty;
                    row.Set(name, label, color, res.Delta, note, local);
                    _rows.Add(row);
                }
            }

            _sequence = StartCoroutine(Sequence(stagger));
        }

        private IEnumerator Sequence(float stagger)
        {
            // Rows stagger in 120 ms apart.
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null) StartCoroutine(_rows[i].Appear(0.25f));
                yield return new WaitForSecondsRealtime(stagger);
            }
            yield return new WaitForSecondsRealtime(0.15f);
            if (reactionText != null)
                yield return Tween.To(0.35f, Ease.OutQuad, t => reactionText.alpha = t);
            _sequence = null;
        }

        /// <summary>Scoring phase: count each row's delta up from 0.</summary>
        public void AnimateDeltas(float duration)
        {
            foreach (var r in _rows) if (r != null) StartCoroutine(r.CountDelta(duration));
        }

        /// <summary>
        /// The chip at the left of a row: what they answered on a social round, or where
        /// they finished on a minigame.
        /// </summary>
        public static void AnswerLabel(RoundDefinition def, int answer, int place, out string label, out Color color)
        {
            color = Palette.Neutral;

            if (def != null && def.IsMinigame)
            {
                label = Ordinal(place);
                color = place == 1 ? Palette.Accent : place == 2 ? Palette.Green : Palette.Neutral;
                return;
            }

            if (answer < 0) { label = "—"; color = Palette.Divider; return; }
            if (def == null) { label = answer.ToString(); return; }
            switch (def.inputType)
            {
                case InputType.RedGreen:
                    label = answer == 1 ? "RED" : "GREEN";
                    color = answer == 1 ? Palette.Red : Palette.Green;
                    break;
                case InputType.YesNo:
                    label = answer == 1 ? "YES" : "NO";
                    break;
                default:
                    label = answer.ToString();
                    break;
            }
        }

        public static string Ordinal(int place)
        {
            if (place <= 0) return "—";
            int mod100 = place % 100;
            if (mod100 >= 11 && mod100 <= 13) return place + "TH";
            switch (place % 10)
            {
                case 1: return place + "ST";
                case 2: return place + "ND";
                case 3: return place + "RD";
                default: return place + "TH";
            }
        }
    }
}
