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

    /// <summary>C02 — Green: +3 always. Red: +8 if fewer than half pick red, else 0.</summary>
    public sealed class PickAPillResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, greenPts = def.Param(0, 3), redPts = def.Param(1, 8);
            int reds = R.CountA(ctx.Answers);
            bool redWins = reds * 2 < n;
            var d = new int[n];
            for (int i = 0; i < n; i++) d[i] = R.IsA(ctx.Answers[i]) ? (redWins ? redPts : 0) : greenPts;
            string key = reds == 0 ? "noRed" : (redWins ? "redWins" : "redFails");
            return R.Make(def, d, key);
        }
    }

    /// <summary>C03 — Exactly floor(N/2) reds (and at least one): reds +10, greens −10. Otherwise nothing.</summary>
    public sealed class TheSnapResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, redPts = def.Param(0, 10), greenPts = def.Param(1, -10);
            int reds = R.CountA(ctx.Answers);
            var d = new int[n];
            bool balanced = reds >= 1 && reds == n / 2;
            if (!balanced) return R.Make(def, d, "unbalanced");
            for (int i = 0; i < n; i++) d[i] = R.IsA(ctx.Answers[i]) ? redPts : greenPts;
            return R.Make(def, d, "balanced");
        }
    }

    /// <summary>C04 — Green: +15 if the only green, −5 if 2+ greens. Red: 0.</summary>
    public sealed class TheDoorResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, onePts = def.Param(0, 15), manyPts = def.Param(1, -5);
            var d = new int[n];
            int greens = 0;
            for (int i = 0; i < n; i++) if (R.Answered(ctx.Answers[i]) && R.IsB(ctx.Answers[i])) greens++;
            // Non-answers count as green for payoff purposes (passive), but can't be "the one".
            int greensIncl = 0;
            for (int i = 0; i < n; i++) if (R.IsB(ctx.Answers[i])) greensIncl++;
            if (greensIncl == 0) return R.Make(def, d, "none");
            if (greensIncl == 1)
            {
                for (int i = 0; i < n; i++) if (R.IsB(ctx.Answers[i])) d[i] = R.Answered(ctx.Answers[i]) ? onePts : 0;
                return R.Make(def, d, greens == 1 ? "one" : "none");
            }
            for (int i = 0; i < n; i++) if (R.IsB(ctx.Answers[i])) d[i] = manyPts;
            return R.Make(def, d, "many");
        }
    }

    /// <summary>C14 — Any red: every leader −15, every red −3 (a red leader takes both). No red: nothing.</summary>
    public sealed class AttackTheLeaderResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, leaderPts = def.Param(0, -15), attackerPts = def.Param(1, -3);
            var d = new int[n];
            if (R.CountA(ctx.Answers) == 0) return R.Make(def, d, "none");
            int top = int.MinValue;
            for (int i = 0; i < n; i++) top = Math.Max(top, ctx.Scores[i]);
            for (int i = 0; i < n; i++)
            {
                if (ctx.Scores[i] == top) d[i] += leaderPts;
                if (R.IsA(ctx.Answers[i])) d[i] += attackerPts;
            }
            return R.Make(def, d, "attack");
        }
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

    /// <summary>C22 — Minority color +10. Tie: nothing.</summary>
    public sealed class SusResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, pts = def.Param(0, 10);
            var d = new int[n];
            int reds = R.CountA(ctx.Answers), greens = n - reds;
            if (reds == greens) return R.Make(def, d, "tie");
            bool redMinority = reds < greens;
            for (int i = 0; i < n; i++)
                if (R.IsA(ctx.Answers[i]) == redMinority) d[i] = pts;
            return R.Make(def, d, "reveal", R.Line(def, "reveal", ("color", redMinority ? "Red" : "Green")));
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

    /// <summary>C15 — Sub-round k: any Yes → the Yes players split 5k (floor), round ends. No Yes by k=5: all +20.</summary>
    public sealed class TheLeverResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, potPer = def.Param(0, 5), never = def.Param(1, 20);
            int k = Math.Max(1, ctx.SubRound);
            int last = Math.Max(1, def.subRounds);
            var d = new int[n];
            int yes = R.CountA(ctx.Answers);
            if (yes > 0)
            {
                int share = (potPer * k) / yes;
                for (int i = 0; i < n; i++) if (R.IsA(ctx.Answers[i])) d[i] = share;
                string names = R.Names(ctx, i => R.IsA(ctx.Answers[i]));
                return R.Make(def, d, "pulled", R.Line(def, "pulled", ("k", k.ToString()), ("names", names), ("pts", (potPer * k).ToString())));
            }
            if (k < last)
            {
                var r = R.Make(def, d, "held", R.Line(def, "held") + $" Next pot: {potPer * (k + 1)}.");
                r.ContinueSubRounds = true;
                return r;
            }
            for (int i = 0; i < n; i++) d[i] = never;
            return R.Make(def, d, "never");
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
                string who = n < 5 ? n.ToString() : "five";
                return R.Make(def, d, "none", R.Line(def, "none", ("n", who)));
            }
            for (int i = 0; i < n; i++) d[i] = R.IsA(ctx.Answers[i]) ? yesPts : 0;
            return R.Make(def, d, "someYes", R.Line(def, "someYes", ("names", R.Names(ctx, i => R.IsA(ctx.Answers[i])))));
        }
    }

    /// <summary>C18 — Majority +3, minority −3, tie all −1.</summary>
    public sealed class IsThisADreamResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, maj = def.Param(0, 3), min = def.Param(1, -3), tie = def.Param(2, -1);
            var d = new int[n];
            int yes = R.CountA(ctx.Answers), no = n - yes;
            if (yes == no) { for (int i = 0; i < n; i++) d[i] = tie; return R.Make(def, d, "tie"); }
            bool yesWins = yes > no;
            for (int i = 0; i < n; i++) d[i] = R.IsA(ctx.Answers[i]) == yesWins ? maj : min;
            return R.Make(def, d, "reveal", R.Line(def, "reveal", ("x", yesWins ? "yes" : "no")));
        }
    }

    /// <summary>
    /// C19 — Donors pay 3, last place collects it (ties split, floor). If at least half the
    /// room donates, every donor also gets +5 — so generosity pays once enough people join in.
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
            int lastCount = 0, outsideDonors = 0;
            for (int i = 0; i < n; i++)
            {
                if (ctx.Scores[i] == low) lastCount++;
                else if (R.IsA(ctx.Answers[i])) outsideDonors++;
            }
            int pool = gift * outsideDonors;
            int share = lastCount > 0 ? pool / lastCount : 0;
            bool enough = donors * 2 >= n;
            for (int i = 0; i < n; i++)
            {
                if (R.IsA(ctx.Answers[i])) d[i] += cost + (enough ? bonus : 0);
                if (ctx.Scores[i] == low) d[i] += share;
            }
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

    /// <summary>C05 — All answered numbers distinct: +5 each (to those who answered). Any duplicate: everyone 0.</summary>
    public sealed class RuleOneResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, pts = def.Param(0, 5);
            var d = new int[n];
            var seen = new HashSet<int>();
            int answered = 0;
            for (int i = 0; i < n; i++)
            {
                if (!R.Answered(ctx.Answers[i])) continue;
                answered++;
                if (!seen.Add(ctx.Answers[i])) return R.Make(def, d, "dup");
            }
            if (answered == 0) return R.Make(def, d, "dup");
            for (int i = 0; i < n; i++) if (R.Answered(ctx.Answers[i])) d[i] = pts;
            return R.Make(def, d, "unique");
        }
    }

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

    /// <summary>C09 — Contribution = min(answer, max(score,0)). Pot ×2 split equally (floor). Delta = share − contribution.</summary>
    public sealed class ThePotResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, mult = def.Param(0, 2);
            var d = new int[n];
            var contrib = new int[n];
            int pot = 0;
            for (int i = 0; i < n; i++)
            {
                contrib[i] = Math.Min(R.Eff(ctx.Answers[i]), Math.Max(ctx.Scores[i], 0));
                pot += contrib[i];
            }
            int share = n > 0 ? (pot * mult) / n : 0;
            int topGiver = 0, topGainer = 0;
            for (int i = 0; i < n; i++)
            {
                d[i] = share - contrib[i];
                if (contrib[i] > contrib[topGiver]) topGiver = i;
                if (d[i] > d[topGainer]) topGainer = i;
            }
            string key = topGiver == topGainer ? "same" : "reveal";
            return R.Make(def, d, key, R.Line(def, key, ("topGiver", ctx.NameOf(topGiver)), ("topGainer", ctx.NameOf(topGainer))));
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

    /// <summary>
    /// C12 — The average has to land on EXACTLY 7 for anyone to score. One honest rating,
    /// or one person overcompensating, and the whole room gets nothing.
    /// </summary>
    public sealed class RateThisGameResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, target = def.Param(0, 7), pts = def.Param(1, 10);
            var d = new int[n];
            int sum = R.Sum(ctx.Answers);
            bool exact = n > 0 && sum == target * n; // average == target, integer-exact
            if (exact)
            {
                for (int i = 0; i < n; i++) d[i] = pts;
                return R.Make(def, d, "exact");
            }
            string avg = n > 0 ? ((double)sum / n).ToString("0.#", CultureInfo.InvariantCulture) : "0";
            return R.Make(def, d, "miss", R.Line(def, "miss", ("x", avg)));
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
    /// C26 Pairs — +10 for a number exactly two players picked. Being alone pays nothing and
    /// so does being in a crowd, so the room has to split into couples without talking.
    /// </summary>
    public sealed class PairsResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, pts = def.Param(0, 10);
            var d = new int[n];
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < n; i++)
            {
                if (!R.Answered(ctx.Answers[i])) continue;
                counts.TryGetValue(ctx.Answers[i], out int c);
                counts[ctx.Answers[i]] = c + 1;
            }
            var paired = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (!R.Answered(ctx.Answers[i])) continue;
                if (counts[ctx.Answers[i]] == 2) { d[i] = pts; paired.Add(i); }
            }
            if (paired.Count == 0) return R.Make(def, d, "none", R.Line(def, "none"));
            return R.Make(def, d, "pairs",
                R.Line(def, "pairs", ("names", R.Join(paired.ConvertAll(ctx.NameOf))), ("n", paired.Count.ToString())));
        }
    }

    /// <summary>
    /// C28 Bandwagon — the biggest group of matching numbers scores; a unanimous room scores
    /// double. If nobody matched anybody, the round pays nothing at all.
    /// </summary>
    public sealed class BandwagonResolver : IRoundResolver
    {
        public RoundResult Resolve(RoundDefinition def, RoundContext ctx)
        {
            int n = ctx.PlayerCount, pts = def.Param(0, 5), unanimousPts = def.Param(1, 10);
            var d = new int[n];
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < n; i++)
            {
                if (!R.Answered(ctx.Answers[i])) continue;
                counts.TryGetValue(ctx.Answers[i], out int c);
                counts[ctx.Answers[i]] = c + 1;
            }
            int best = 0, bestNumber = 0;
            foreach (var kv in counts)
                if (kv.Value > best || (kv.Value == best && kv.Key < bestNumber)) { best = kv.Value; bestNumber = kv.Key; }

            if (best < 2) return R.Make(def, d, "none", R.Line(def, "none"));

            bool unanimous = best == n;
            int pay = unanimous ? unanimousPts : pts;
            var riders = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (!R.Answered(ctx.Answers[i])) continue;
                if (counts[ctx.Answers[i]] == best) { d[i] = pay; riders.Add(i); }
            }
            string key = unanimous ? "unanimous" : "crowd";
            return R.Make(def, d, key,
                R.Line(def, key, ("n", bestNumber.ToString()), ("names", R.Join(riders.ConvertAll(ctx.NameOf)))));
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
            { ResolverType.RuleOne, new RuleOneResolver() },
            { ResolverType.LowestUnique, new LowestUniqueResolver() },
            { ResolverType.TwoThirds, new TwoThirdsResolver() },
            { ResolverType.ThePot, new ThePotResolver() },
            { ResolverType.SilentAuction, new SilentAuctionResolver() },
            { ResolverType.Greedy, new GreedyResolver() },
            { ResolverType.RateThisGame, new RateThisGameResolver() },
            { ResolverType.TakeTheHit, new TakeTheHitResolver() },
            { ResolverType.AttackTheLeader, new AttackTheLeaderResolver() },
            { ResolverType.TheLever, new TheLeverResolver() },
            { ResolverType.Trolley, new TrolleyResolver() },
            { ResolverType.OneUp, new OneUpResolver() },
            { ResolverType.IsThisADream, new IsThisADreamResolver() },
            { ResolverType.Charity, new CharityResolver() },
            { ResolverType.PredictTheRoom, new PredictTheRoomResolver() },
            { ResolverType.TheGodfather, new TheGodfatherResolver() },
            { ResolverType.Sus, new SusResolver() },
            { ResolverType.Pairs, new PairsResolver() },
            { ResolverType.Sacrifice, new SacrificeResolver() },
            { ResolverType.Bandwagon, new BandwagonResolver() },
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
