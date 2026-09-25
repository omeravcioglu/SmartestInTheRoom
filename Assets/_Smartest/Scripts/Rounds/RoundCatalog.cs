using System.Collections.Generic;
using Smartest.Minigames;

namespace Smartest.Rounds
{
    /// <summary>
    /// All social rounds as plain data. SceneBuilder turns these into RoundDefinition assets;
    /// tests use the same numbers. Minigame definitions come from MinigameRegistry and are
    /// turned into Specs by MinigameSpecs() so both kinds live in one library.
    ///
    /// Writing rules for this file:
    ///  - Prompt is the QUESTION, 12 words max. People don't read.
    ///  - Rule is ONE short sentence (two at most, ~25 words total). A player has to get it
    ///    in 2-4 seconds. It is shown under the question, never on a button.
    ///  - Every round must be multiplayer: what the others do has to change your result,
    ///    and no option may be best no matter what the room does.
    /// </summary>
    public static class RoundCatalog
    {
        public sealed class Spec
        {
            public int Id;
            public string Title;
            public string Prompt;
            public string SubLine;
            public RoundKind Kind = RoundKind.Social;
            public string MinigameId;
            public string Rule;
            public InputType Input;
            public string NameA;
            public string NameB;
            public bool AllowZero;
            public int AnswerSeconds = 30;
            public ResolverType Resolver;
            public int[] Params = new int[0];
            public List<RevealLine> Lines = new List<RevealLine>();
            public int SubRounds = 1;
            public int FollowUpId = -1;

            public Spec L(string key, string text) { Lines.Add(new RevealLine(key, text)); return this; }

            public void ApplyTo(RoundDefinition d)
            {
                d.id = Id; d.title = Title; d.prompt = Prompt; d.subLine = SubLine;
                d.kind = Kind; d.minigameId = MinigameId; d.ruleText = Rule;
                d.inputType = Input; d.buttonNameA = NameA; d.buttonNameB = NameB;
                d.allowZero = AllowZero; d.answerSeconds = AnswerSeconds;
                d.resolver = Resolver; d.resolverParams = (int[])Params.Clone();
                d.revealLines = new List<RevealLine>();
                foreach (var l in Lines) d.revealLines.Add(new RevealLine(l.outcomeKey, l.text));
                d.subRounds = SubRounds; d.followUpId = FollowUpId;
            }
        }

        public static RoundDefinition CreateDefinition(Spec s)
        {
            var d = UnityEngine.ScriptableObject.CreateInstance<RoundDefinition>();
            s.ApplyTo(d);
            d.name = s.Kind == RoundKind.Minigame ? $"M{s.Id:000} {s.Title}" : $"C{s.Id:00} {s.Title}";
            return d;
        }

        public static Spec Get(int id)
        {
            foreach (var s in All) if (s.Id == id) return s;
            foreach (var s in MinigameSpecs()) if (s.Id == id) return s;
            return null;
        }

        /// <summary>
        /// One Spec per registered minigame. The rule text and the timer live in the registry
        /// next to the game itself, so adding a game never means editing this file.
        /// </summary>
        public static List<Spec> MinigameSpecs()
        {
            var list = new List<Spec>();
            foreach (var e in MinigameRegistry.All)
            {
                list.Add(new Spec
                {
                    Id = e.RoundId,
                    Title = e.Title,
                    Prompt = e.Prompt,
                    Kind = RoundKind.Minigame,
                    MinigameId = e.Id,
                    Rule = e.Rule,
                    AnswerSeconds = 20
                });
            }
            return list;
        }

        /// <summary>Social rounds plus minigames, in one list.</summary>
        public static List<Spec> AllChallenges()
        {
            var list = new List<Spec>(All);
            list.AddRange(MinigameSpecs());
            return list;
        }

        public static readonly List<Spec> All = new List<Spec>
        {
            new Spec { Id = 1, Title = "The Button", Prompt = "Everyone presses green?", Input = InputType.RedGreen,
                Rule = "If EVERYONE picks Green, everyone gets +10. If anyone picks Red, Reds get +15 and Greens lose 5. All Red: everyone loses 10.",
                Resolver = ResolverType.TheButton, Params = new[] { 10, 15, -5, -10 } }
                .L("allGreen", "Ten each. Boring. Correct.")
                .L("someRed", "Reds win. Greens trusted.")
                .L("allRed", "Minus ten. You built this."),

            new Spec { Id = 2, Title = "Pick a Pill", Prompt = "Red pill or green pill?", Input = InputType.RedGreen,
                Rule = "Red pays +8, but only if fewer than half of you pick Red. Green pays +3, guaranteed.",
                Resolver = ResolverType.PickAPill, Params = new[] { 3, 8 } }
                .L("redWins", "The rabbit hole goes eight points deep.")
                .L("redFails", "Truth is worth zero when everyone has it.")
                .L("noRed", "Everyone stayed asleep. Three each. Comfortable."),

            new Spec { Id = 3, Title = "The Snap", Prompt = "Half of you must vanish.", Input = InputType.RedGreen,
                Rule = "If EXACTLY half of you pick Red, Reds get +10 and Greens lose 10. Any other split: nothing happens.",
                Resolver = ResolverType.TheSnap, Params = new[] { 10, -10 } }
                .L("balanced", "Perfectly balanced.")
                .L("unbalanced", "Unbalanced. Nothing happens."),

            new Spec { Id = 4, Title = "The Door", Prompt = "There's room on the door. For one.", Input = InputType.RedGreen,
                Rule = "Green climbs on the door: +15 if you're the only one, −5 each if anyone joins you. Red stays in the water.",
                Resolver = ResolverType.TheDoor, Params = new[] { 15, -5 } }
                .L("one", "One survivor. They'll talk about you for 97 years.")
                .L("many", "Physics doesn't care. Everybody's wet.")
                .L("none", "There was always room."),

            new Spec { Id = 5, Title = "Rule One", Prompt = "Pick a number. Don't match anyone.", Input = InputType.Number1to10,
                Rule = "If every player picks a different number, everyone gets +5. If any two match, nobody gets anything.",
                Resolver = ResolverType.RuleOne, Params = new[] { 5 } }
                .L("unique", "You talked. Rule one is a suggestion.")
                .L("dup", "Someone lied, or nobody talked. Both fine."),

            new Spec { Id = 7, Title = "Lowest Unique", Prompt = "Lowest number nobody else picked wins.", Input = InputType.Number1to10,
                Rule = "The lowest number that EXACTLY ONE player picked wins +15. Match someone and yours is worthless.",
                Resolver = ResolverType.LowestUnique, Params = new[] { 15 } }
                .L("win", "{name} wins with {n}. The rest matched or got scared.")
                .L("none", "Everybody matched. Nobody wins. Cowards."),

            new Spec { Id = 8, Title = "Two Thirds", Prompt = "Guess two-thirds of the average guess.", Input = InputType.Number1to10,
                Rule = "Closest to two-thirds of the room's average wins +10. Ties split it.",
                Resolver = ResolverType.TwoThirds, Params = new[] { 10 } }
                .L("win", "Target {x}. {name} wins. Whoever picked ten dragged the average and lost."),

            new Spec { Id = 9, Title = "The Pot", Prompt = "Put points in the pot. It doubles.", Input = InputType.Number1to10,
                Rule = "Everything put in is doubled and split equally between ALL players — including the ones who gave nothing.",
                AllowZero = true,
                Resolver = ResolverType.ThePot, Params = new[] { 2 } }
                .L("reveal", "{topGiver} gave the most. {topGainer} gained the most. Not the same person.")
                .L("same", "{topGiver} gave the most and gained the most. Suspicious."),

            new Spec { Id = 10, Title = "Silent Auction", Prompt = "Bid for twenty points.", Input = InputType.Number1to10,
                Rule = "Highest bid wins +20, split on a tie. Everyone pays their own bid — winners and losers.",
                Resolver = ResolverType.SilentAuction, Params = new[] { 20 } }
                .L("reveal", "{name} paid {n} for twenty. Everyone else paid for nothing.")
                .L("none", "Nobody bid. The lot goes home. Cheap."),

            new Spec { Id = 11, Title = "Greedy", Prompt = "Take what you want. Don't be greedy.", Input = InputType.Number1to10,
                Rule = "If everyone's numbers add up to 6 per player or less, you each keep what you asked for. One over: everyone gets nothing.",
                Resolver = ResolverType.Greedy, Params = new[] { 6 } }
                .L("under", "Restraint. Suspicious.")
                .L("over", "Whoever picked ten is anonymous. They're not. Names are on the left."),

            new Spec { Id = 12, Title = "Rate This Game", Prompt = "Rate this game.", Input = InputType.Number1to10,
                Rule = "If the average of all ratings is EXACTLY 7, everyone gets +10. Anything else: nothing.",
                Resolver = ResolverType.RateThisGame, Params = new[] { 7, 10 } }
                .L("exact", "Seven. Thank you for the coordinated lie.")
                .L("miss", "Average {x}. Somebody was honest. It cost everyone."),

            new Spec { Id = 13, Title = "Take the Hit", Prompt = "Someone has to take the hit.", Input = InputType.YesNo,
                NameA = "VOLUNTEER", NameB = "STAY QUIET",
                Rule = "Exactly one volunteer: they get +10 and everyone else +5. Two or more: each volunteer loses 5. Nobody: everyone loses 5.",
                Resolver = ResolverType.TakeTheHit, Params = new[] { 10, 5, -5 } }
                .L("one", "{name} took it. And got paid for it. Rare.")
                .L("none", "Democracy.")
                .L("many", "Heroes cancel out."),

            new Spec { Id = 14, Title = "Attack the Leader", Prompt = "Attack the leader?", Input = InputType.RedGreen,
                Rule = "If anyone picks Red, the leader loses 15 — and every attacker pays 3. Green: nothing happens.",
                Resolver = ResolverType.AttackTheLeader, Params = new[] { -15, -3 } }
                .L("attack", "Revolutions are cheap when you split the bill.")
                .L("none", "Respect or fear. The leader can't tell either."),

            new Spec { Id = 15, Title = "The Lever", Prompt = "Pull the lever?", Input = InputType.YesNo,
                NameA = "PULL", NameB = "WAIT",
                Rule = "Pull and split the pot with everyone else pulling now. Wait and it grows +5. Nobody pulls in five rounds: everyone +20.",
                AnswerSeconds = 20, SubRounds = 5,
                Resolver = ResolverType.TheLever, Params = new[] { 5, 20 } }
                .L("pulled", "Round {k}. {names} split {pts}.")
                .L("held", "Nobody pulled. The lever gets heavier.")
                .L("never", "Twenty each. I've never seen that."),

            new Spec { Id = 16, Title = "Trolley", Prompt = "Pull the trolley lever?", Input = InputType.YesNo,
                NameA = "PULL", NameB = "DON'T",
                Rule = "Pulling costs you 5. If NOBODY pulls, everyone loses 10.",
                Resolver = ResolverType.Trolley, Params = new[] { -5, -10 } }
                .L("someYes", "{names}. Brave. Deceased.")
                .L("none", "Five died. Coincidentally, {n} of you lost ten."),

            new Spec { Id = 17, Title = "1-Up", Prompt = "Grab the extra life?", Input = InputType.RedGreen,
                Rule = "Green reaches for it: +5, unless EVERYONE reaches, then nobody gets it. Red holds back: +10 if you're the only Red.",
                Resolver = ResolverType.OneUp, Params = new[] { 5, 10 } }
                .L("soloRed", "{name} was the only one who didn't reach. Ten points for standing still.")
                .L("someRed", "Several of you held back. Nobody got the bonus for it.")
                .L("allGreen", "Always one short. That's the design."),

            new Spec { Id = 18, Title = "Is This a Dream?", Prompt = "Is this a dream?", Input = InputType.YesNo,
                Rule = "The bigger side gets +3, the smaller side loses 3. An exact split costs everyone 1.",
                Resolver = ResolverType.IsThisADream, Params = new[] { 3, -3, -1 } }
                .L("reveal", "Majority said {x}. So it is. Reality is a vote.")
                .L("tie", "Split. Reality undecided. Everyone loses one."),

            new Spec { Id = 19, Title = "Charity", Prompt = "Give three points to last place?", Input = InputType.YesNo,
                NameA = "DONATE", NameB = "KEEP",
                Rule = "Donating costs 3 and goes to last place. If HALF of you or more donate, every donor also gets +5.",
                Resolver = ResolverType.Charity, Params = new[] { -3, 3, 5 } }
                .L("enough", "{n} donated. Enough of you to make kindness profitable.")
                .L("few", "{n} donated. Charity's easy when it's someone else's problem.")
                .L("none", "Nobody donated. Last place noticed."),

            new Spec { Id = 20, Title = "Predict the Room", Prompt = "How many will press RED next round?", Input = InputType.Number1to10,
                Rule = "Guess how many players press Red NEXT round. Exactly right: +5. Off by one: +2.",
                AllowZero = true,
                Resolver = ResolverType.PredictTheRoom, Params = new[] { 5, 2 }, FollowUpId = 0 }
                .L("predicted", "Predictions locked. Next round decides.")
                .L("followup", "{n} pressed red. {names} called it. They know you.")
                .L("followupNone", "{n} pressed red. Nobody called it. You don't know each other."),

            new Spec { Id = 21, Title = "The Godfather", Prompt = "Lowest number wins. Unless it's shared.", Input = InputType.Number1to10,
                Rule = "The lowest number wins +10 — but only if ONE player picked it. Share the lowest and you all lose 10.",
                Resolver = ResolverType.TheGodfather, Params = new[] { 10, -10 } }
                .L("unique", "{name} picked {n}. Nobody dared.")
                .L("shared", "{names}. Minus ten. One of you lied. Family."),

            new Spec { Id = 22, Title = "Sus", Prompt = "Pick the colour fewer people pick.", Input = InputType.RedGreen,
                Rule = "Whichever colour FEWER players pick gains +10 each. An even split pays nobody.",
                Resolver = ResolverType.Sus, Params = new[] { 10 } }
                .L("reveal", "{color} was outnumbered. Outnumbered wins.")
                .L("tie", "Even split. Nobody's sus. Everybody's sus."),

            // ---------------- Added in the redesign ----------------

            new Spec { Id = 26, Title = "Pairs", Prompt = "Find your twin. Just one.", Input = InputType.Number1to10,
                Rule = "You get +10 if EXACTLY one other player picked your number. Alone or in a crowd: nothing.",
                Resolver = ResolverType.Pairs, Params = new[] { 10 } }
                .L("pairs", "{names} found each other. Everyone else guessed alone.")
                .L("none", "No pairs. Ten people, ten islands."),

            new Spec { Id = 27, Title = "Sacrifice", Prompt = "Give up five points?", Input = InputType.YesNo,
                NameA = "GIVE", NameB = "KEEP",
                Rule = "Giving costs you 5. If at least HALF of you give, EVERYONE gets +15.",
                Resolver = ResolverType.Sacrifice, Params = new[] { -5, 15 } }
                .L("enough", "{n} gave. Everyone profits. Especially the ones who didn't.")
                .L("notEnough", "{n} gave. Not enough. They paid for the lesson.")
                .L("none", "Nobody gave. Nothing happened. Efficient."),

            new Spec { Id = 28, Title = "Bandwagon", Prompt = "Pick the number everyone else picks.", Input = InputType.Number1to10,
                Rule = "Everyone on the MOST popular number gets +5. If all of you pick the same one, +10 each. No matches: nothing.",
                Resolver = ResolverType.Bandwagon, Params = new[] { 5, 10 } }
                .L("unanimous", "All of you on {n}. Ten each. Slightly terrifying.")
                .L("crowd", "{names} matched. Everyone else guessed alone.")
                .L("none", "Everyone picked differently. No bandwagon to jump on."),
        };
    }
}
