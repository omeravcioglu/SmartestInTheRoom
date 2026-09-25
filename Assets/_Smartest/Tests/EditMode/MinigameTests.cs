using System.Collections.Generic;
using NUnit.Framework;
using Smartest.Minigames;

namespace Smartest.Tests
{
    /// <summary>
    /// The elimination ladder and the payout table decide who wins every minigame and what
    /// it's worth, and neither touches Unity — so they can be pinned down exactly here
    /// rather than discovered during a game night.
    /// </summary>
    public class LadderTests
    {
        private const ulong A = 1, B = 2, C = 3, D = 4;

        private static List<LevelReport> Reports(params (ulong id, bool failed, int metric)[] rows)
        {
            var list = new List<LevelReport>();
            foreach (var r in rows) list.Add(new LevelReport(r.id, r.failed, r.metric));
            return list;
        }

        private static EliminationLadder Four()
            => new EliminationLadder(new[] { A, B, C, D }, MetricOrder.LowerIsBetter);

        [Test]
        public void FailingALevelPutsYouOut()
        {
            var ladder = Four();
            var step = ladder.Submit(Reports((A, false, 100), (B, false, 200), (C, true, 0), (D, false, 300)));
            CollectionAssert.AreEqual(new[] { C }, step.Eliminated);
            CollectionAssert.AreEquivalent(new[] { A, B, D }, ladder.Alive);
            Assert.IsFalse(step.Finished);
        }

        [Test]
        public void WhenNobodyFailsTheWorstResultGoesOut()
        {
            var ladder = Four();
            var step = ladder.Submit(Reports((A, false, 100), (B, false, 200), (C, false, 150), (D, false, 400)));
            CollectionAssert.AreEqual(new[] { D }, step.Eliminated);
            Assert.IsFalse(step.RepeatHarder);
        }

        [Test]
        public void HigherIsBetterFlipsWhichResultIsWorst()
        {
            var ladder = new EliminationLadder(new[] { A, B, C }, MetricOrder.HigherIsBetter);
            var step = ladder.Submit(Reports((A, false, 30), (B, false, 10), (C, false, 20)));
            CollectionAssert.AreEqual(new[] { B }, step.Eliminated);
        }

        [Test]
        public void WhenEveryoneFailsNobodyGoesOutAndTheLevelGetsHarder()
        {
            var ladder = Four();
            int before = ladder.Level;
            var step = ladder.Submit(Reports((A, true, 0), (B, true, 0), (C, true, 0), (D, true, 0)));
            Assert.IsTrue(step.RepeatHarder);
            Assert.AreEqual(0, step.Eliminated.Count);
            Assert.AreEqual(4, ladder.Alive.Count);
            Assert.AreEqual(before + 1, ladder.Level, "the replay has to be harder than the level nobody cleared");
        }

        [Test]
        public void ADeadHeatIsPlayedOffRatherThanGuessed()
        {
            var ladder = new EliminationLadder(new[] { A, B, C }, MetricOrder.LowerIsBetter);
            var step = ladder.Submit(Reports((A, false, 100), (B, false, 200), (C, false, 200)));

            Assert.IsTrue(step.TieBreak);
            CollectionAssert.AreEquivalent(new[] { B, C }, step.TiedPlayers);
            Assert.AreEqual(0, step.Eliminated.Count, "a tie must not knock anyone out on its own");
            CollectionAssert.AreEquivalent(new[] { B, C }, ladder.Participants);
            CollectionAssert.AreEquivalent(new[] { A, B, C }, ladder.Alive);
            Assert.IsTrue(ladder.InTieBreak);

            // Only the tied players play the next level; the loser of it goes out.
            var second = ladder.Submit(Reports((B, false, 50), (C, false, 80)));
            CollectionAssert.AreEqual(new[] { C }, second.Eliminated);
            Assert.IsFalse(ladder.InTieBreak);
            CollectionAssert.AreEquivalent(new[] { A, B }, ladder.Participants);
        }

        [Test]
        public void ATieThatStaysTiedIsPlayedAgain()
        {
            var ladder = new EliminationLadder(new[] { A, B, C }, MetricOrder.LowerIsBetter);
            ladder.Submit(Reports((A, false, 100), (B, false, 200), (C, false, 200)));
            var again = ladder.Submit(Reports((B, false, 70), (C, false, 70)));
            Assert.IsTrue(again.RepeatHarder);
            Assert.AreEqual(0, again.Eliminated.Count);
            CollectionAssert.AreEquivalent(new[] { B, C }, ladder.Participants);
        }

        [Test]
        public void TheLastPlayerStandingWinsAndEveryoneElseIsRankedByHowLongTheyLasted()
        {
            var ladder = Four();
            ladder.Submit(Reports((A, false, 100), (B, false, 200), (C, true, 0), (D, false, 300)));
            ladder.Submit(Reports((A, false, 100), (B, false, 200), (D, false, 300)));
            var last = ladder.Submit(Reports((A, false, 100), (B, false, 150)));

            Assert.IsTrue(last.Finished);
            CollectionAssert.AreEqual(new[] { A, B, D, C }, last.Ranking);
            Assert.AreEqual(1, last.Places[A]);
            Assert.AreEqual(2, last.Places[B]);
            Assert.AreEqual(3, last.Places[D]);
            Assert.AreEqual(4, last.Places[C]);
        }

        [Test]
        public void TwoPlayersKnockedOutTogetherWithTheSameResultSharePlace()
        {
            var ladder = Four();
            ladder.Submit(Reports((A, false, 100), (B, true, 500), (C, true, 500), (D, false, 200)));
            var last = ladder.Submit(Reports((A, false, 100), (D, false, 200)));

            Assert.IsTrue(last.Finished);
            Assert.AreEqual(1, last.Places[A]);
            Assert.AreEqual(2, last.Places[D]);
            Assert.AreEqual(3, last.Places[B]);
            Assert.AreEqual(3, last.Places[C], "a genuine dead heat shares a place instead of being broken randomly");
        }

        [Test]
        public void APlayerWhoNeverReportsCountsAsAFail()
        {
            var ladder = Four();
            var step = ladder.Submit(Reports((A, false, 100), (B, false, 200), (C, false, 150)));
            CollectionAssert.AreEqual(new[] { D }, step.Eliminated);
        }

        [Test]
        public void LeavingTheGameEndsItWhenOnlyOnePlayerIsLeft()
        {
            var ladder = new EliminationLadder(new[] { A, B }, MetricOrder.LowerIsBetter);
            ladder.Remove(B);
            Assert.IsTrue(ladder.Finished);
            CollectionAssert.AreEqual(new[] { A }, ladder.Alive);
            Assert.AreEqual(1, ladder.FinalPlaces()[A]);
        }

        [Test]
        public void AMinigameAlwaysEndsWithinAReasonableNumberOfLevels()
        {
            // Eight players, nobody ever failing: one goes out per level, so seven levels.
            var ids = new List<ulong>();
            for (ulong i = 1; i <= 8; i++) ids.Add(i);
            var ladder = new EliminationLadder(ids, MetricOrder.LowerIsBetter);

            int levels = 0;
            while (!ladder.Finished && levels < 50)
            {
                var reports = new List<LevelReport>();
                int metric = 10;
                foreach (var id in ladder.Participants) reports.Add(new LevelReport(id, false, metric += 10));
                ladder.Submit(reports);
                levels++;
            }
            Assert.IsTrue(ladder.Finished);
            Assert.AreEqual(7, levels);
            Assert.AreEqual(1, ladder.Alive.Count);
        }
    }

    public class PayoutTableTests
    {
        private static readonly int[] Points = { 20, 10, 5 };
        private const int Last = -5;

        private static Dictionary<ulong, int> Places(params int[] places)
        {
            var d = new Dictionary<ulong, int>();
            for (int i = 0; i < places.Length; i++) d[(ulong)(i + 1)] = places[i];
            return d;
        }

        private static int[] Deltas(params int[] places)
        {
            var result = PayoutTable.Deltas(Places(places), Points, Last);
            var flat = new int[places.Length];
            for (int i = 0; i < places.Length; i++) flat[i] = result[(ulong)(i + 1)];
            return flat;
        }

        [Test] public void TwoPlayers() => CollectionAssert.AreEqual(new[] { 20, -5 }, Deltas(1, 2));
        [Test] public void ThreePlayers() => CollectionAssert.AreEqual(new[] { 20, 10, -5 }, Deltas(1, 2, 3));
        [Test] public void FourPlayers() => CollectionAssert.AreEqual(new[] { 20, 10, 5, -5 }, Deltas(1, 2, 3, 4));
        [Test] public void FivePlayers() => CollectionAssert.AreEqual(new[] { 20, 10, 5, 0, -5 }, Deltas(1, 2, 3, 4, 5));

        [Test]
        public void EightPlayersPayTopThreeAndPunishOnlyLast()
            => CollectionAssert.AreEqual(new[] { 20, 10, 5, 0, 0, 0, 0, -5 }, Deltas(1, 2, 3, 4, 5, 6, 7, 8));

        [Test]
        public void ADeadHeatForLastMeansBothTakeThePenalty()
            => CollectionAssert.AreEqual(new[] { 20, 10, -5, -5 }, Deltas(1, 2, 3, 3));

        [Test]
        public void ThePayoutCurveIsJustThreeNumbers()
        {
            var custom = new[] { 30, 18, 12, 6 };
            var result = PayoutTable.Deltas(Places(1, 2, 3, 4, 5), custom, -8);
            Assert.AreEqual(30, result[1]);
            Assert.AreEqual(18, result[2]);
            Assert.AreEqual(12, result[3]);
            Assert.AreEqual(6, result[4]);
            Assert.AreEqual(-8, result[5]);
        }
    }

    public class LevelRngTests
    {
        [Test]
        public void TheSameSeedAndLevelGiveEveryClientTheSameContent()
        {
            var a = LevelRng.For("memory_boxes", 12345, 3);
            var b = LevelRng.For("memory_boxes", 12345, 3);
            for (int i = 0; i < 25; i++) Assert.AreEqual(a.Next(1000), b.Next(1000));
        }

        [Test]
        public void DifferentLevelsAndGamesGiveDifferentContent()
        {
            Assert.AreNotEqual(LevelRng.Hash("memory_boxes", 1, 1), LevelRng.Hash("memory_boxes", 1, 2));
            Assert.AreNotEqual(LevelRng.Hash("memory_boxes", 1, 1), LevelRng.Hash("simon", 1, 1));
            Assert.AreNotEqual(LevelRng.Hash("memory_boxes", 1, 1), LevelRng.Hash("memory_boxes", 2, 1));
        }

        [Test]
        public void HashIsAlwaysAUsableSeed()
        {
            for (int seed = -50; seed < 50; seed++)
                for (int level = 1; level <= 20; level++)
                    Assert.Greater(LevelRng.Hash("dodge", seed, level), 0);
        }

        [Test]
        public void DistinctPicksAreActuallyDistinct()
        {
            var rng = LevelRng.For("memory_boxes", 99, 1);
            var picked = LevelRng.Distinct(rng, 7, 25);
            Assert.AreEqual(7, picked.Count);
            CollectionAssert.AllItemsAreUnique(picked);
            foreach (int i in picked) Assert.IsTrue(i >= 0 && i < 25);
        }

        [Test]
        public void AskingForMoreThanExistsReturnsEverything()
        {
            var rng = LevelRng.For("simon", 5, 1);
            Assert.AreEqual(9, LevelRng.Distinct(rng, 20, 9).Count);
        }
    }

    public class MinigameRegistryTests
    {
        [Test]
        public void EveryEntryIsUsableByTheStage()
        {
            foreach (var e in MinigameRegistry.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.Id), "a minigame has no id");
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.Title), e.Id + " has no title");
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.Rule), e.Id + " has no rule");
                Assert.Less(e.Rule.Length, 130, e.Id + ": the rule has to land in 2-4 seconds");
                Assert.IsNotNull(e.ViewType, e.Id + " has no view");
                Assert.IsTrue(typeof(MinigameView).IsAssignableFrom(e.ViewType),
                    e.Id + " view must derive from MinigameView");
                Assert.IsTrue(e.LevelSeconds >= 5f && e.LevelSeconds <= 20f,
                    e.Id + ": a level has to be between 5 and 20 seconds");
            }
        }

        [Test]
        public void IdsAndRoundIdsAreUnique()
        {
            var ids = new HashSet<string>();
            var roundIds = new HashSet<int>();
            foreach (var e in MinigameRegistry.All)
            {
                Assert.IsTrue(ids.Add(e.Id), "duplicate minigame id " + e.Id);
                Assert.IsTrue(roundIds.Add(e.RoundId), "duplicate round id " + e.RoundId);
            }
        }

        [Test]
        public void LookupWorksBothWays()
        {
            foreach (var e in MinigameRegistry.All)
            {
                Assert.AreSame(e, MinigameRegistry.Get(e.Id));
                Assert.AreSame(e, MinigameRegistry.ByRoundId(e.RoundId));
            }
            Assert.IsNull(MinigameRegistry.Get("not_a_game"));
        }

        [Test]
        public void ThereAreEnoughMinigamesToKeepAMatchVaried()
            => Assert.GreaterOrEqual(MinigameRegistry.All.Count, 15);
    }
}
