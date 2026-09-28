using System;
using System.Collections.Generic;

namespace Smartest.Rounds
{
    public enum InputType
    {
        RedGreen,     // answer: Red = 1, Green = 0
        YesNo,        // answer: Yes = 1, No = 0
        Number1to10   // answer: 1..10 (0 allowed when RoundDefinition.allowZero)
    }

    /// <summary>
    /// A challenge is either a social round (everyone answers, a resolver pays out)
    /// or a minigame (everyone plays, the elimination ladder ranks, the payout table pays).
    /// </summary>
    public enum RoundKind
    {
        Social = 0,
        Minigame = 1
    }

    /// <summary>One entry per payoff rule. The registry maps these to resolver classes.</summary>
    public enum ResolverType
    {
        TheButton = 1,
        PickAPill = 2,
        TheSnap = 3,
        TheDoor = 4,
        LowestUnique = 7,
        TwoThirds = 8,
        ThePot = 9,
        SilentAuction = 10,
        Greedy = 11,
        TakeTheHit = 13,
        Trolley = 16,
        OneUp = 17,
        Charity = 19,
        PredictTheRoom = 20,
        TheGodfather = 21,
        Sus = 22,

        // --- Added in the multiplayer-first redesign ---
        Sacrifice = 27,  // enough givers and everybody profits

        // --- Added 27 Sep 2026, replacing the team rounds (Rule One 5, Rate This Game 12,
        //     Is This a Dream 18, Pairs 26, Bandwagon 28), Attack the Leader 14 and The Lever 15.
        //     Those ids stay retired so an old asset can never be read as a new round. ---
        Undercut = 29,     // one below you steals your number, and doubles theirs
        GoldRush = 30,     // dig and split the gold, or sell shovels to whoever digs
        MirrorMatch = 31   // you only score if someone picks your mirror number
    }

    [Serializable]
    public class RevealLine
    {
        public string outcomeKey;
        public string text;

        public RevealLine() { }
        public RevealLine(string key, string text) { outcomeKey = key; this.text = text; }
    }

    /// <summary>
    /// Everything a resolver may need. Plain data so resolvers stay unit-testable
    /// without any networking. Indices are player indices 0..PlayerCount-1.
    /// </summary>
    public sealed class RoundContext
    {
        public int PlayerCount;
        /// <summary>Raw answers, -1 = no answer.</summary>
        public IReadOnlyList<int> Answers;
        /// <summary>Scores before this round is applied.</summary>
        public IReadOnlyList<int> Scores;
        /// <summary>Display names (for runtime reveal lines). May be null in tests.</summary>
        public IReadOnlyList<string> Names;
        /// <summary>1-based sub-round for multi-step rounds (The Lever). 1 otherwise.</summary>
        public int SubRound = 1;

        public string NameOf(int i)
        {
            if (Names != null && i >= 0 && i < Names.Count && !string.IsNullOrEmpty(Names[i])) return Names[i];
            return "Player " + (i + 1);
        }
    }

    /// <summary>Output of a resolver.</summary>
    public struct RoundResult
    {
        /// <summary>Per-player score delta.</summary>
        public int[] Deltas;
        /// <summary>Picks the RevealLine on the RoundDefinition.</summary>
        public string OutcomeKey;
        /// <summary>Optional fully-formatted line with names; overrides the RevealLine text when set.</summary>
        public string RuntimeLine;
        /// <summary>Multi-step rounds: true means "run another sub-round" (no scoring yet).</summary>
        public bool ContinueSubRounds;

        public static RoundResult Zero(int n, string key, string runtimeLine = null)
        {
            return new RoundResult { Deltas = new int[n], OutcomeKey = key, RuntimeLine = runtimeLine };
        }
    }

    public interface IRoundResolver
    {
        RoundResult Resolve(RoundDefinition def, RoundContext ctx);
    }
}
