using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Smartest.Rounds
{
    /// <summary>
    /// Shared helpers. Conventions:
    ///  - RedGreen: 1 = red, 0 = green.  YesNo: 1 = yes, 0 = no.  Numbers: 0..10.
    ///  - No answer (-1) is the passive option (green / no / 0) and can never *win* a round.
    ///    Where Green is the bold move (The Door) or picking a side is the whole point (Sus),
    ///    a missing answer is simply left out instead.
    /// All math is integer and deterministic; nothing here touches UnityEngine.
    /// </summary>
    internal static class R
    {
        public static int Eff(int a) => a < 0 ? 0 : a;
        public static bool Answered(int a) => a >= 0;
        public static bool IsA(int a) => a == 1;      // red / yes
        public static bool IsB(int a) => a <= 0;      // green / no / none

        public static int CountA(IReadOnlyList<int> ans)
        {
            int c = 0;
            for (int i = 0; i < ans.Count; i++) if (IsA(ans[i])) c++;
            return c;
        }

        public static int Sum(IReadOnlyList<int> ans)
        {
            int s = 0;
            for (int i = 0; i < ans.Count; i++) s += Eff(ans[i]);
            return s;
        }

        public static string Names(RoundContext ctx, Func<int, bool> pick)
        {
            var list = new List<string>();
            for (int i = 0; i < ctx.PlayerCount; i++) if (pick(i)) list.Add(ctx.NameOf(i));
            return Join(list);
        }

        public static string Join(List<string> names)
        {
            if (names.Count == 0) return "Nobody";
            if (names.Count == 1) return names[0];
            if (names.Count == 2) return names[0] + " and " + names[1];
            var sb = new StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(i == names.Count - 1 ? " and " : ", ");
                sb.Append(names[i]);
            }
            return sb.ToString();
        }

        public static string Fmt(string template, params (string key, string value)[] subs)
        {
            foreach (var s in subs) template = template.Replace("{" + s.key + "}", s.value);
            return template;
        }

        public static RoundResult Make(RoundDefinition def, int[] deltas, string key, string runtime = null)
        {
            return new RoundResult { Deltas = deltas, OutcomeKey = key, RuntimeLine = runtime };
        }

        /// <summary>Template from the definition with {placeholders} substituted; empty if key missing.</summary>
        public static string Line(RoundDefinition def, string key, params (string key, string value)[] subs)
        {
            string t = def != null ? def.LineFor(key) : string.Empty;
            return string.IsNullOrEmpty(t) ? string.Empty : Fmt(t, subs);
        }
    }

    // ------------------------------------------------------------------
    // Red / Green
    // ------------------------------------------------------------------

    /// <summary>C01 — All green: +10 each. Any red: reds +15, greens −5. All red: everyone −10.</summary>
    public sealed class TheButtonResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, allGreenPts = def.Param(0, 10), redPts = def.Param(1, 15),
                greenPts = def.Param(2, -5), allRedPts = def.Param(3, -10);
            var d = new int[n];
            int reds = R.CountA(ctx.Answers);
            if (reds == 0) { for (int i = 0; i < n; i++) d[i] = allGreenPts; return R.Make(def, d, "allGreen"); }
            if (reds == n) { for (int i = 0; i < n; i++) d[i] = allRedPts; return R.Make(def, d, "allRed"); }
            for (int i = 0; i < n; i++) d[i] = R.IsA(ctx.Answers[i]) ? redPts : greenPts;
            return R.Make(def, d, "someRed");
        }
    }

    /// <summary>
    /// C02 — Green: +3 always. Red: +8 if half the room or fewer pick red, else 0.
    /// "Half or fewer" rather than "fewer than half": with two players a lone Red is exactly
    /// half, and a strict minority would make Red a guaranteed zero.
    /// </summary>
    public sealed class PickAPillResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, greenPts = def.Param(0, 3), redPts = def.Param(1, 8);
            int reds = R.CountA(ctx.Answers);
            bool redWins = reds * 2 <= n;
            var d = new int[n];
            for (int i = 0; i < n; i++) d[i] = R.IsA(ctx.Answers[i]) ? (redWins ? redPts : 0) : greenPts;
            string key = reds == 0 ? "noRed" : (redWins ? "redWins" : "redFails");
            return R.Make(def, d, key);
        }
    }

    /// <summary>
    /// C03 — Red vanishes, Green survives. Exactly floor(N/2) reds: greens +10, reds 0.
    /// Any other split: everyone −5.
    ///
    /// The old payoff (balanced: reds +10, greens −10; otherwise nothing) let Red win or
    /// break even whatever the room did, so nobody had a reason to press Green. Now the
    /// volunteers are the ones who vanish: somebody has to take the zero so the rest get
    /// paid, and missing the count costs everyone.
    /// </summary>
    public sealed class TheSnapResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, greenPts = def.Param(0, 10), redPts = def.Param(1, 0), missPts = def.Param(2, -5);
            int reds = R.CountA(ctx.Answers);
            var d = new int[n];
            bool balanced = reds >= 1 && reds == n / 2;
            if (!balanced)
            {
                for (int i = 0; i < n; i++) d[i] = missPts;
                return R.Make(def, d, "unbalanced", R.Line(def, "unbalanced", ("n", reds.ToString())));
            }
            for (int i = 0; i < n; i++) d[i] = R.IsA(ctx.Answers[i]) ? redPts : greenPts;
            return R.Make(def, d, "balanced");
        }
    }

    /// <summary>
    /// C04 — Green climbs: +15 if the only climber, −5 each if 2+. Red stays in the water: 0.
    /// Only a pressed Green climbs; a player who never answered stays in the water, so being
    /// away from the keyboard can't sink the one person who did climb.
    /// </summary>
    public sealed class TheDoorResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, onePts = def.Param(0, 15), manyPts = def.Param(1, -5);
            var d = new int[n];
            int climbers = 0;
            for (int i = 0; i < n; i++) if (Climbs(ctx.Answers[i])) climbers++;
            if (climbers == 0) return R.Make(def, d, "none");
            int pts = climbers == 1 ? onePts : manyPts;
            for (int i = 0; i < n; i++) if (Climbs(ctx.Answers[i])) d[i] = pts;
            return R.Make(def, d, climbers == 1 ? "one" : "many");
        }

        private static bool Climbs(int answer) => answer == 0;
    }

    /// <summary>C17 — Greens +5 unless everyone is green (then nobody). A lone red: +10.</summary>
    public sealed class OneUpResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, pts = def.Param(0, 5), soloPts = def.Param(1, 10);
            var d = new int[n];
            int reds = R.CountA(ctx.Answers);
            if (reds == 0) return R.Make(def, d, "allGreen");
            for (int i = 0; i < n; i++) d[i] = R.IsB(ctx.Answers[i]) ? pts : 0;
            if (reds != 1) return R.Make(def, d, "someRed");
            int solo = -1;
            for (int i = 0; i < n; i++) if (R.IsA(ctx.Answers[i])) { d[i] = soloPts; solo = i; }
            return R.Make(def, d, "soloRed", R.Line(def, "soloRed", ("name", ctx.NameOf(solo))));
        }
    }

    /// <summary>
    /// C22 — Minority color +10. Tie: nothing. Only pressed colours count: a player who
    /// never answered didn't pick the smaller side, so they can't be paid for it. And if the
    /// whole room picked one colour, nobody was outnumbered.
    /// </summary>
    public sealed class SusResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, pts = def.Param(0, 10);
            var d = new int[n];
            int reds = 0, greens = 0;
            for (int i = 0; i < n; i++)
            {
                if (ctx.Answers[i] == 1) reds++;
                else if (ctx.Answers[i] == 0) greens++;
            }
            if (reds == 0 || greens == 0) return R.Make(def, d, "same");
            if (reds == greens) return R.Make(def, d, "tie");
            int minority = reds < greens ? 1 : 0;
            for (int i = 0; i < n; i++)
                if (ctx.Answers[i] == minority) d[i] = pts;
            return R.Make(def, d, "reveal", R.Line(def, "reveal", ("color", minority == 1 ? "Red" : "Green")));
        }
    }

    /// <summary>
    /// C30 Gold Rush — Red digs: the diggers split 10. Green sells shovels: +6 for every digger.
    /// Digging alone is the best seat in the house, but a second digger halves the gold and
    /// doubles the shovel money, so the room has to settle who digs. A player who never
    /// answered neither digs nor sells.
    /// </summary>
    public sealed class GoldRushResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, gold = def.Param(0, 10), perDigger = def.Param(1, 6);
            var d = new int[n];
            int diggers = 0, sellers = 0, digger = -1;
            for (int i = 0; i < n; i++)
            {
                if (ctx.Answers[i] == 1) { diggers++; digger = i; }
                else if (ctx.Answers[i] == 0) sellers++;
            }
            if (diggers == 0) return R.Make(def, d, "none");
            for (int i = 0; i < n; i++)
            {
                if (ctx.Answers[i] == 1) d[i] = gold / diggers;
                else if (ctx.Answers[i] == 0) d[i] = perDigger * diggers;
            }
            if (diggers == 1) return R.Make(def, d, "one", R.Line(def, "one", ("name", ctx.NameOf(digger))));
            string key = sellers == 0 ? "all" : "rush";
            return R.Make(def, d, key, R.Line(def, key, ("n", diggers.ToString())));
        }
    }

    // ------------------------------------------------------------------
    // Yes / No
    // ------------------------------------------------------------------

    /// <summary>C13 — Exactly one Yes: hero +10, everyone else +5. No Yes: all −5. 2+ Yes: each Yes −5, others 0.</summary>
    public sealed class TakeTheHitResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, heroPts = def.Param(0, 10), roomPts = def.Param(1, 5), penalty = def.Param(2, -5);
            var d = new int[n];
            int yes = R.CountA(ctx.Answers);
            if (yes == 0) { for (int i = 0; i < n; i++) d[i] = penalty; return R.Make(def, d, "none"); }
            if (yes == 1)
            {
                int hero = -1;
                for (int i = 0; i < n; i++) { if (R.IsA(ctx.Answers[i])) { d[i] = heroPts; hero = i; } else d[i] = roomPts; }
                return R.Make(def, d, "one", R.Line(def, "one", ("name", ctx.NameOf(hero))));
            }
            for (int i = 0; i < n; i++) d[i] = R.IsA(ctx.Answers[i]) ? penalty : 0;
            return R.Make(def, d, "many");
        }
    }

    /// <summary>C16 — Yes: −5. No: 0. Nobody Yes: all −10.</summary>
    public sealed class TrolleyResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, yesPts = def.Param(0, -5), nonePts = def.Param(1, -10);
            var d = new int[n];
            int yes = R.CountA(ctx.Answers);
            if (yes == 0)
            {
                for (int i = 0; i < n; i++) d[i] = nonePts;
                // Everyone lost ten, so say how many that was — "five" only when it really is.
                string who = n == 5 ? "five" : n.ToString();
                return R.Make(def, d, "none", R.Line(def, "none", ("n", who)));
            }
            for (int i = 0; i < n; i++) d[i] = R.IsA(ctx.Answers[i]) ? yesPts : 0;
            return R.Make(def, d, "someYes", R.Line(def, "someYes", ("names", R.Names(ctx, i => R.IsA(ctx.Answers[i])))));
        }
    }

    /// <summary>
    /// C19 — Donors pay 3, last place collects it (ties split, floor). If at least half the
    /// room donates, every donor also gets +5 — so generosity pays once enough people join in.
    /// A donor who is tied for last gives to the others tied with them. That matters most at
    /// 0-0-0, when everyone is last place: otherwise every donation vanished into nobody.
    /// </summary>
    public sealed class CharityResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, cost = def.Param(0, -3), gift = def.Param(1, 3), bonus = def.Param(2, 5);
            var d = new int[n];
            int donors = R.CountA(ctx.Answers);
            if (donors == 0) return R.Make(def, d, "none");
            int low = int.MaxValue;
            for (int i = 0; i < n; i++) low = Math.Min(low, ctx.Scores[i]);
            var last = new List<int>();
            int outsideDonors = 0;
            for (int i = 0; i < n; i++)
            {
                if (ctx.Scores[i] == low) last.Add(i);
                else if (R.IsA(ctx.Answers[i])) outsideDonors++;
            }
            int share = gift * outsideDonors / last.Count;
            foreach (int i in last) d[i] += share;
            foreach (int i in last)
            {
                // Nobody gives to themselves: a lone last-place donor's 3 simply goes.
                if (!R.IsA(ctx.Answers[i]) || last.Count < 2) continue;
                int each = gift / (last.Count - 1);
                foreach (int j in last) if (j != i) d[j] += each;
            }
            bool enough = donors * 2 >= n;
            for (int i = 0; i < n; i++)
                if (R.IsA(ctx.Answers[i])) d[i] += cost + (enough ? bonus : 0);
            string key = enough ? "enough" : "few";
            return R.Make(def, d, key, R.Line(def, key, ("n", donors.ToString())));
        }
    }

    /// <summary>C27 — Givers pay 5. If at least half the room gives, EVERYONE gets +15 on top.</summary>
    public sealed class SacrificeResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, cost = def.Param(0, -5), reward = def.Param(1, 15);
            var d = new int[n];
            int givers = R.CountA(ctx.Answers);
            if (givers == 0) return R.Make(def, d, "none", R.Line(def, "none"));
            bool enough = givers * 2 >= n;
            for (int i = 0; i < n; i++)
            {
                if (R.IsA(ctx.Answers[i])) d[i] += cost;
                if (enough) d[i] += reward;
            }
            string key = enough ? "enough" : "notEnough";
            return R.Make(def, d, key, R.Line(def, key, ("n", givers.ToString())));
        }
    }

    // ------------------------------------------------------------------
    // Numbers
    // ------------------------------------------------------------------

    /// <summary>C07 — Lowest number chosen by exactly one player: +15. No unique number: nobody.</summary>
    public sealed class LowestUniqueResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, pts = def.Param(0, 15);
            var d = new int[n];
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < n; i++) if (R.Answered(ctx.Answers[i])) counts[ctx.Answers[i]] = counts.TryGetValue(ctx.Answers[i], out var c) ? c + 1 : 1;
            int best = int.MaxValue;
            foreach (var kv in counts) if (kv.Value == 1 && kv.Key < best) best = kv.Key;
            if (best == int.MaxValue) return R.Make(def, d, "none");
            int winner = -1;
            for (int i = 0; i < n; i++) if (ctx.Answers[i] == best) { d[i] = pts; winner = i; }
            return R.Make(def, d, "win", R.Line(def, "win", ("name", ctx.NameOf(winner)), ("n", best.ToString())));
        }
    }

    /// <summary>C08 — Closest to 2/3 of the average (non-answers count as 0 in the average, can't win): +10, ties split (floor).</summary>
    public sealed class TwoThirdsResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, pts = def.Param(0, 10);
            var d = new int[n];
            double avg = n > 0 ? (double)R.Sum(ctx.Answers) / n : 0.0;
            double target = avg * 2.0 / 3.0;
            double bestDist = double.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (!R.Answered(ctx.Answers[i])) continue;
                bestDist = Math.Min(bestDist, Math.Abs(ctx.Answers[i] - target));
            }
            var winners = new List<int>();
            for (int i = 0; i < n; i++)
                if (R.Answered(ctx.Answers[i]) && Math.Abs(Math.Abs(ctx.Answers[i] - target) - bestDist) < 1e-9) winners.Add(i);
            if (winners.Count == 0) return R.Make(def, d, "win", "Nobody answered. Target " + target.ToString("0.0", CultureInfo.InvariantCulture) + ".");
            int share = pts / winners.Count;
            foreach (var w in winners) d[w] = share;
            string names = R.Join(winners.ConvertAll(ctx.NameOf));
            return R.Make(def, d, "win", R.Line(def, "win", ("x", target.ToString("0.0", CultureInfo.InvariantCulture)), ("name", names)));
        }
    }

    /// <summary>
    /// C09 — Contribution = your answer. Pot ×2 split equally (floor). Delta = share − contribution.
    /// If the pot comes to less than 3 per player, everyone also loses 5.
    ///
    /// Without that floor, 0 was always the best answer — every point you put in comes back
    /// to you as 2/N of a point — so there was nothing to decide. The floor keeps the pull to
    /// free-ride on everybody else, but someone has to feed the pot, and how much depends on
    /// what the room does. (Contributions used to be capped at your current score as well,
    /// which the rule never said and which made the round do nothing at 0-0-0.)
    /// </summary>
    public sealed class ThePotResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, mult = def.Param(0, 2), perPlayer = def.Param(1, 3), starvedPts = def.Param(2, -5);
            var d = new int[n];
            var contrib = new int[n];
            int pot = 0;
            for (int i = 0; i < n; i++)
            {
                contrib[i] = R.Eff(ctx.Answers[i]);
                pot += contrib[i];
            }

            int share = n > 0 ? (pot * mult) / n : 0;
            bool starved = pot < perPlayer * n;
            bool even = true;
            int topGiver = 0, topGainer = 0;
            for (int i = 0; i < n; i++)
            {
                d[i] = share - contrib[i] + (starved ? starvedPts : 0);
                if (contrib[i] != contrib[0]) even = false;
                if (contrib[i] > contrib[topGiver]) topGiver = i;
                if (d[i] > d[topGainer]) topGainer = i;
            }
            if (pot == 0) return R.Make(def, d, "none");
            if (starved)
                return R.Make(def, d, "starved",
                    R.Line(def, "starved", ("pot", pot.ToString()), ("need", (perPlayer * n).ToString())));
            // Everyone gave the same: there is no "gave the most" to name.
            if (even) return R.Make(def, d, "same");
            return R.Make(def, d, "reveal", R.Line(def, "reveal", ("topGiver", ctx.NameOf(topGiver)), ("topGainer", ctx.NameOf(topGainer))));
        }
    }

    /// <summary>C10 — Highest bid (≥1) gets +20 (ties: +20/ties). Everyone pays their bid.</summary>
    public sealed class SilentAuctionResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, prize = def.Param(0, 20);
            var d = new int[n];
            int high = 0;
            for (int i = 0; i < n; i++) { d[i] = -R.Eff(ctx.Answers[i]); high = Math.Max(high, R.Eff(ctx.Answers[i])); }
            if (high <= 0) return R.Make(def, d, "none");
            var winners = new List<int>();
            for (int i = 0; i < n; i++) if (R.Eff(ctx.Answers[i]) == high) winners.Add(i);
            int share = prize / winners.Count;
            foreach (var w in winners) d[w] += share;
            return R.Make(def, d, "reveal", R.Line(def, "reveal", ("name", R.Join(winners.ConvertAll(ctx.NameOf))), ("n", high.ToString())));
        }
    }

    /// <summary>C11 — If the sum ≤ 6×N, everyone keeps their number. Else all 0.</summary>
    public sealed class GreedyResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, per = def.Param(0, 6);
            var d = new int[n];
            if (R.Sum(ctx.Answers) > per * n) return R.Make(def, d, "over");
            for (int i = 0; i < n; i++) d[i] = R.Eff(ctx.Answers[i]);
            return R.Make(def, d, "under");
        }
    }

    /// <summary>C20 — Prediction only; scored after the follow-up RedGreen round (see ScoreFollowUp).</summary>
    public sealed class PredictTheRoomResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            return RoundResult.Zero(ctx.PlayerCount, "predicted");
        }

        /// <summary>Exact prediction of the red count: +5. Off by one: +2. Returns the bonus deltas and the line.</summary>
        public static RoundResult ScoreFollowUp(RoundDefinition predictDef, IReadOnlyList<int> predictions, int redCount, RoundContext ctxForNames)
        {
            int n = predictions.Count, exact = predictDef.Param(0, 5), near = predictDef.Param(1, 2);
            var d = new int[n];
            var callers = new List<string>();
            for (int i = 0; i < n; i++)
            {
                if (predictions[i] < 0) continue;
                int diff = Math.Abs(predictions[i] - redCount);
                if (diff == 0) { d[i] = exact; callers.Add(ctxForNames.NameOf(i)); }
                else if (diff == 1) d[i] = near;
            }
            string key = callers.Count > 0 ? "followup" : "followupNone";
            return R.Make(predictDef, d, key, R.Line(predictDef, key, ("n", redCount.ToString()), ("names", R.Join(callers))));
        }
    }

    /// <summary>C21 — Unique lowest: +10. Shared lowest: each of them −10.</summary>
    public sealed class TheGodfatherResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, win = def.Param(0, 10), lose = def.Param(1, -10);
            var d = new int[n];
            int low = int.MaxValue;
            for (int i = 0; i < n; i++) if (R.Answered(ctx.Answers[i])) low = Math.Min(low, ctx.Answers[i]);
            if (low == int.MaxValue) return R.Make(def, d, "shared", "Nobody spoke. Family.");
            var lows = new List<int>();
            for (int i = 0; i < n; i++) if (ctx.Answers[i] == low) lows.Add(i);
            if (lows.Count == 1)
            {
                d[lows[0]] = win;
                return R.Make(def, d, "unique", R.Line(def, "unique", ("name", ctx.NameOf(lows[0])), ("n", low.ToString())));
            }
            foreach (var i in lows) d[i] = lose;
            return R.Make(def, d, "shared", R.Line(def, "shared", ("names", R.Join(lows.ConvertAll(ctx.NameOf)))));
        }
    }

    /// <summary>
    /// C29 Undercut — score your number, doubled if someone picked exactly one above you. If
    /// someone picked exactly one below you, you score nothing. Every deal the room can make
    /// has a betrayal that pays (if everyone takes 10, a 9 scores 18), so there's no safe
    /// number, only a read on the others. A player who never answered touches nobody.
    /// </summary>
    public sealed class UndercutResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, mult = def.Param(0, 2);
            var d = new int[n];
            // x - 1 and x + 1 are never x, so "someone else picked it" is just "it was picked".
            var picked = new HashSet<int>();
            for (int i = 0; i < n; i++) if (R.Answered(ctx.Answers[i])) picked.Add(ctx.Answers[i]);

            var victims = new List<int>();
            var hunters = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (!R.Answered(ctx.Answers[i])) continue;
                int x = ctx.Answers[i];
                if (picked.Contains(x - 1)) { victims.Add(i); continue; }
                if (picked.Contains(x + 1)) { d[i] = mult * x; hunters.Add(i); }
                else d[i] = x;
            }
            // The bottom of any chain is never undercut itself, so a victim always has a hunter.
            if (victims.Count == 0) return R.Make(def, d, "none");
            return R.Make(def, d, "undercut", R.Line(def, "undercut",
                ("victims", R.Join(victims.ConvertAll(ctx.NameOf))), ("hunters", R.Join(hunters.ConvertAll(ctx.NameOf)))));
        }
    }

    /// <summary>
    /// C31 Mirror Match — your mirror is 11 minus your number (3 and 8). Score your number if
    /// someone picked your mirror, unless someone else picked your number too. Every pair
    /// splits eleven unevenly (10 + 1, 6 + 5), so the room has to settle who takes the small
    /// end, and crowding a number wrecks it for everyone on it.
    /// </summary>
    public sealed class MirrorMatchResolver : IRoundResolver
    {
        // Odd on purpose: no number can be its own mirror.
        private const int MirrorSum = 11;

        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount;
            var d = new int[n];
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < n; i++)
            {
                if (!R.Answered(ctx.Answers[i])) continue;
                counts.TryGetValue(ctx.Answers[i], out int c);
                counts[ctx.Answers[i]] = c + 1;
            }
            var matched = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (!R.Answered(ctx.Answers[i])) continue;
                int x = ctx.Answers[i];
                counts.TryGetValue(MirrorSum - x, out int mirrors);
                if (mirrors > 0 && counts[x] == 1) { d[i] = x; matched.Add(i); }
            }
            if (matched.Count == 0) return R.Make(def, d, "none");
            return R.Make(def, d, "pairs", R.Line(def, "pairs", ("names", R.Join(matched.ConvertAll(ctx.NameOf)))));
        }
    }

    // ------------------------------------------------------------------
    // Registry
    // ------------------------------------------------------------------

    public static class ResolverRegistry
    {
        private static readonly Dictionary<ResolverType, IRoundResolver> s_map = new Dictionary<ResolverType, IRoundResolver>
        {
            { ResolverType.TheButton, new TheButtonResolver() },
            { ResolverType.PickAPill, new PickAPillResolver() },
            { ResolverType.TheSnap, new TheSnapResolver() },
            { ResolverType.TheDoor, new TheDoorResolver() },
            { ResolverType.LowestUnique, new LowestUniqueResolver() },
            { ResolverType.TwoThirds, new TwoThirdsResolver() },
            { ResolverType.ThePot, new ThePotResolver() },
            { ResolverType.SilentAuction, new SilentAuctionResolver() },
            { ResolverType.Greedy, new GreedyResolver() },
            { ResolverType.TakeTheHit, new TakeTheHitResolver() },
            { ResolverType.Trolley, new TrolleyResolver() },
            { ResolverType.OneUp, new OneUpResolver() },
            { ResolverType.Charity, new CharityResolver() },
            { ResolverType.PredictTheRoom, new PredictTheRoomResolver() },
            { ResolverType.TheGodfather, new TheGodfatherResolver() },
            { ResolverType.Sus, new SusResolver() },
            { ResolverType.Sacrifice, new SacrificeResolver() },
            { ResolverType.Undercut, new UndercutResolver() },
            { ResolverType.GoldRush, new GoldRushResolver() },
            { ResolverType.MirrorMatch, new MirrorMatchResolver() },
        };

        public static IRoundResolver Get(ResolverType type)
        {
            return s_map.TryGetValue(type, out var r) ? r : null;
        }

        public static RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            var r = Get(def.resolver);
            if (r == null) return RoundResult.Zero(ctx.PlayerCount, "missing", "No resolver for " + def.resolver);
            return r.Resolve(def, ctx);
        }
    }
}
