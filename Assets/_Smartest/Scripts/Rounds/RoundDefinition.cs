using System.Collections.Generic;
using UnityEngine;

namespace Smartest.Rounds
{
    /// <summary>
    /// One challenge, authored as data — either a social round (resolver pays out) or a
    /// minigame (the ladder ranks, the payout table pays). Add one by adding an entry to
    /// RoundCatalog (social) or MinigameRegistry (minigame); no engine code changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Smartest/Round Definition", fileName = "Round")]
    public class RoundDefinition : ScriptableObject
    {
        [Header("Identity")]
        public int id;
        public string title;
        [TextArea(2, 4)] public string prompt;
        [Tooltip("Optional smaller line under the prompt.")]
        public string subLine;

        [Header("Kind")]
        public RoundKind kind = RoundKind.Social;
        [Tooltip("Minigames only: which entry of MinigameRegistry this round plays.")]
        public string minigameId;

        [Header("Rule")]
        [Tooltip("THE rule. One or two short sentences — a player must get it in 2-4 seconds. " +
                 "Shown under the question, never on a button. The words Red/Green are coloured automatically.")]
        [TextArea(2, 3)] public string ruleText;

        [Header("Input (social rounds)")]
        public InputType inputType = InputType.RedGreen;
        [Tooltip("Word on button A. Blank = RED / YES.")]
        public string buttonNameA;
        [Tooltip("Word on button B. Blank = GREEN / NO.")]
        public string buttonNameB;
        [Tooltip("Number rounds: also offer a 0 button.")]
        public bool allowZero;
        public int answerSeconds = 30;

        [Header("Payoff")]
        public ResolverType resolver;
        public int[] resolverParams = new int[0];
        public List<RevealLine> revealLines = new List<RevealLine>();
        [Tooltip("1 normally; 5 for The Lever.")]
        public int subRounds = 1;
        [Tooltip("-1 normally. Predict the Room uses 0 = 'any RedGreen round'.")]
        public int followUpId = -1;

        [Header("Audio (optional)")]
        public AudioClip revealClip;

        public bool IsMinigame => kind == RoundKind.Minigame;

        public int Param(int index, int fallback)
        {
            return resolverParams != null && index < resolverParams.Length ? resolverParams[index] : fallback;
        }

        public string LineFor(string outcomeKey)
        {
            if (revealLines != null)
            {
                for (int i = 0; i < revealLines.Count; i++)
                    if (revealLines[i] != null && revealLines[i].outcomeKey == outcomeKey) return revealLines[i].text;
            }
            return string.Empty;
        }

        /// <summary>Highest answer value accepted for this round's input type.</summary>
        public int MaxAnswer => inputType == InputType.Number1to10 ? 10 : 1;
        public int MinAnswer => inputType == InputType.Number1to10 ? (allowZero ? 0 : 1) : 0;

        public bool IsValidAnswer(int a)
        {
            if (IsMinigame) return false; // minigames report results, not answers
            return a >= MinAnswer && a <= MaxAnswer;
        }
    }
}
