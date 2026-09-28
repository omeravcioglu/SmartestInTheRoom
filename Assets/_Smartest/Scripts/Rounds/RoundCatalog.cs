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
    ///  - No team rounds (the room agrees and everyone scores the same), no move that only
    ///    hurts someone else, and no step where one answer is always best.
    ///  - If a round only holds up from three players, say so with MinPlayers = 3: with two,
    ///    Sus can never pay, Lowest Unique and Two Thirds are solved by picking 1, your own
    ///    points in The Pot come straight back to you, and one donor is already "half" in Charity.
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
            /// <summary>Fewest players the round makes sense with; smaller matches never draw it.</summary>
            public int MinPlayers = 1;

            public Spec L(string key, string text) { Lines.Add(new RevealLine(key, text)); return this; }

            public void ApplyTo(RoundDefinition d)
            {
                d.id = Id; d.title = Title; d.prompt = Prompt; d.subLine = SubLine;
                d.kind = Kind; d.minigameId = MinigameId; d.minPlayers = MinPlayers; d.ruleText = Rule;
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
                Rule = "Red pays +8, but only if HALF of you or fewer pick Red. Green pays +3, guaranteed.",
                Resolver = ResolverType.PickAPill, Params = new[] { 3, 8 } }
                .L("redWins", "The rabbit hole goes eight points deep.")
                .L("redFails", "Truth is worth zero when everyone has it.")
                .L("noRed", "Everyone stayed asleep. Three each. Comfortable."),

            new Spec { Id = 3, Title = "The Snap", Prompt = "Half of you must vanish.", Input = InputType.RedGreen,
                SubLine = "With {players} of you, that's exactly {half} Red.",
                Rule = "If EXACTLY half of you pick Red, the Reds vanish (0) and the Greens get +10. Any other split: everyone loses 5.",
                Resolver = ResolverType.TheSnap, Params = new[] { 10, 0, -5 } }
                .L("balanced", "Perfectly balanced. The ones who vanished paid for it.")
                .L("unbalanced", "{n} vanished. Wrong number. Everybody pays."),

            new Spec { Id = 4, Title = "The Door", Prompt = "There's room on the door. For one.", Input = InputType.RedGreen,
                Rule = "Green climbs on the door: +15 if you're the only one, −5 each if anyone joins you. Red stays in the water.",
                Resolver = ResolverType.TheDoor, Params = new[] { 15, -5 } }
                .L("one", "One survivor. They'll talk about you for 97 years.")
                .L("many", "Physics doesn't care. Everybody's wet.")
                .L("none", "There was always room."),

            new Spec { Id = 7, Title = "Lowest Unique", Prompt = "Lowest number nobody else picked wins.", Input = InputType.Number1to10, MinPlayers = 3,
                Rule = "The lowest number that EXACTLY ONE player picked wins +15. Match someone and yours is worthless.",
                Resolver = ResolverType.LowestUnique, Params = new[] { 15 } }
                .L("win", "{name} wins with {n}. The rest matched or got scared.")
                .L("none", "Everybody matched. Nobody wins. Cowards."),

            new Spec { Id = 8, Title = "Two Thirds", Prompt = "Guess two-thirds of the average guess.", Input = InputType.Number1to10, MinPlayers = 3,
                Rule = "Closest to two-thirds of the room's average wins +10. Ties split it.",
                Resolver = ResolverType.TwoThirds, Params = new[] { 10 } }
                .L("win", "Target {x}. {name} wins. Whoever picked ten dragged the average and lost."),

            new Spec { Id = 9, Title = "The Pot", Prompt = "Put points in the pot. It doubles.", Input = InputType.Number1to10, MinPlayers = 3,
                Rule = "Everything put in is doubled and split between ALL players. If the pot comes to less than 3 per player, everyone also loses 5.",
                AllowZero = true,
                Resolver = ResolverType.ThePot, Params = new[] { 2, 3, -5 } }
                .L("reveal", "{topGiver} gave the most. {topGainer} gained the most. Not the same person.")
                .L("same", "Everyone put in the same. Perfectly fair. Deeply boring.")
                .L("starved", "{pot} in the pot, {need} needed. Everybody pays five.")
                .L("none", "Nobody put anything in. Everybody pays for it."),

            // 0 is a real answer here: the reveal line and the tests both expect a room that
            // doesn't bid, but without a 0 button the only way to stay out was to not answer.
            new Spec { Id = 10, Title = "Silent Auction", Prompt = "Bid for twenty points.", Input = InputType.Number1to10,
                Rule = "Highest bid wins +20, split on a tie. Everyone pays their own bid — winners and losers. Bid 0 to sit out.",
                AllowZero = true,
                Resolver = ResolverType.SilentAuction, Params = new[] { 20 } }
                .L("reveal", "{name} paid {n} for twenty. Everyone else paid for nothing.")
                .L("none", "Nobody bid. The lot goes home. Cheap."),

            new Spec { Id = 11, Title = "Greedy", Prompt = "Take what you want. Don't be greedy.", Input = InputType.Number1to10,
                Rule = "If everyone's numbers add up to 6 per player or less, you each keep what you asked for. One over: everyone gets nothing.",
                Resolver = ResolverType.Greedy, Params = new[] { 6 } }
                .L("under", "Restraint. Suspicious.")
                .L("over", "Whoever picked ten is anonymous. They're not. Names are on the left."),

            new Spec { Id = 13, Title = "Take the Hit", Prompt = "Someone has to take the hit.", Input = InputType.YesNo,
                NameA = "VOLUNTEER", NameB = "STAY QUIET",
                Rule = "Exactly one volunteer: they get +10 and everyone else +5. Two or more: each volunteer loses 5. Nobody: everyone loses 5.",
                Resolver = ResolverType.TakeTheHit, Params = new[] { 10, 5, -5 } }
                .L("one", "{name} took it. And got paid for it. Rare.")
                .L("none", "Democracy.")
                .L("many", "Heroes cancel out."),

            new Spec { Id = 16, Title = "Trolley", Prompt = "Pull the trolley lever?", Input = InputType.YesNo,
                NameA = "PULL", NameB = "DON'T",
                Rule = "Pulling costs you 5. If NOBODY pulls, everyone loses 10.",
                Resolver = ResolverType.Trolley, Params = new[] { -5, -10 } }
                .L("someYes", "{names}. Brave. Deceased.")
                .L("none", "Five died. Coincidentally, all {n} of you lost ten."),

            new Spec { Id = 17, Title = "1-Up", Prompt = "Grab the extra life?", Input = InputType.RedGreen,
                Rule = "Green reaches for it: +5, unless EVERYONE reaches, then nobody gets it. Red holds back: +10 if you're the only Red.",
                Resolver = ResolverType.OneUp, Params = new[] { 5, 10 } }
                .L("soloRed", "{name} was the only one who didn't reach. Ten points for standing still.")
                .L("someRed", "Several of you held back. Nobody got the bonus for it.")
                .L("allGreen", "Always one short. That's the design."),

            new Spec { Id = 19, Title = "Charity", Prompt = "Give three points to last place?", Input = InputType.YesNo, MinPlayers = 3,
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

            new Spec { Id = 22, Title = "Sus", Prompt = "Pick the colour fewer people pick.", Input = InputType.RedGreen, MinPlayers = 3,
                Rule = "Whichever colour FEWER players pick gains +10 each. An even split pays nobody.",
                Resolver = ResolverType.Sus, Params = new[] { 10 } }
                .L("reveal", "{color} was outnumbered. Outnumbered wins.")
                .L("same", "One colour for everybody. Nobody was outnumbered.")
                .L("tie", "Even split. Nobody's sus. Everybody's sus."),

            // ---------------- Added in the redesign ----------------

            new Spec { Id = 27, Title = "Sacrifice", Prompt = "Give up five points?", Input = InputType.YesNo,
                NameA = "GIVE", NameB = "KEEP",
                Rule = "Giving costs you 5. If at least HALF of you give, EVERYONE gets +15.",
                Resolver = ResolverType.Sacrifice, Params = new[] { -5, 15 } }
                .L("enough", "{n} gave. Everyone profits. Especially the ones who didn't.")
                .L("notEnough", "{n} gave. Not enough. They paid for the lesson.")
                .L("none", "Nobody gave. Nothing happened. Efficient."),

            // ---------------- Added 27 Sep 2026 ----------------
            // They replace Rule One, Rate This Game, Is This a Dream?, Pairs and Bandwagon (team
            // rounds: the room agrees on Discord and everybody scores the same), Attack the
            // Leader (attacking never helped your own score) and The Lever (its last pull always
            // said pull). None of these three has an agreement the room can't be betrayed out of.

            new Spec { Id = 29, Title = "Undercut", Prompt = "Pick high. Mind the number just below yours.", Input = InputType.Number1to10,
                Rule = "Score your number, doubled if someone picked exactly one above you. If someone picked exactly one below you, you score nothing.",
                Resolver = ResolverType.Undercut, Params = new[] { 2 } }
                .L("undercut", "{victims} got undercut. {hunters} scored double.")
                .L("none", "Nobody undercut anybody. Everyone keeps their number."),

            new Spec { Id = 30, Title = "Gold Rush", Prompt = "Dig for gold, or sell the shovels?", Input = InputType.RedGreen,
                Rule = "Red digs: the diggers split 10 points. Green sells shovels: +6 for every digger.",
                Resolver = ResolverType.GoldRush, Params = new[] { 10, 6 } }
                .L("one", "{name} dug alone and struck gold. The shovel shop did fine too.")
                .L("rush", "{n} diggers, one hill of gold. The shovel sellers did better.")
                .L("all", "Everybody dug. Nobody sold a shovel. Ten points, split {n} ways.")
                .L("none", "Nobody dug. Nobody needed a shovel. Zero all round."),

            new Spec { Id = 31, Title = "Mirror Match", Prompt = "Find someone to pick your mirror.", Input = InputType.Number1to10,
                SubLine = "Mirrors: 1↔10 · 2↔9 · 3↔8 · 4↔7 · 5↔6",
                Rule = "Your mirror is 11 minus your number. Score your number if someone picked your mirror — unless someone else picked yours too.",
                Resolver = ResolverType.MirrorMatch }
                .L("pairs", "{names} found their mirror.")
                .L("none", "No mirrors anywhere. Everybody looked alone."),
        };
    }
}
