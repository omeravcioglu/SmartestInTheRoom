using System.Collections.Generic;
using NUnit.Framework;
using Smartest.Rounds;

namespace Smartest.Tests
{
    public class DeckTests
    {
        private static InputType? InputOf(int id)
        {
            var s = RoundCatalog.Get(id);
            return s != null ? s.Input : (InputType?)null;
        }

        private static List<int> AllIds()
        {
            var ids = new List<int>();
            foreach (var s in RoundCatalog.All) ids.Add(s.Id);
            return ids;
        }

        [Test]
        public void Deck_NoRepeatsUntilExhausted()
        {
            var ids = AllIds();
            var deck = new RoundDeck(ids, InputOf, seed: 42);
            var seen = new HashSet<int>();
            for (int i = 0; i < ids.Count; i++)
                Assert.IsTrue(seen.Add(deck.Draw()), "round repeated before the deck was exhausted");
            Assert.AreEqual(ids.Count, seen.Count);
        }

        [Test]
        public void Deck_ReshufflesAfterExhaustion()
        {
            var ids = AllIds();
            var deck = new RoundDeck(ids, InputOf, seed: 7);
            for (int i = 0; i < ids.Count * 3; i++)
                Assert.IsTrue(ids.Contains(deck.Draw()));
        }

        [Test]
        public void Deck_ForcedInputTypeIsHonoured()
        {
            var deck = new RoundDeck(AllIds(), InputOf, seed: 3);
            for (int i = 0; i < 40; i++)
            {
                int id = deck.Draw(InputType.RedGreen);
                Assert.AreEqual(InputType.RedGreen, InputOf(id), $"draw {i} returned a non-RedGreen round {id}");
            }
        }

        [Test]
        public void Deck_IsDeterministicForSeed()
        {
            var a = new RoundDeck(AllIds(), InputOf, seed: 99);
            var b = new RoundDeck(AllIds(), InputOf, seed: 99);
            for (int i = 0; i < 30; i++) Assert.AreEqual(a.Draw(), b.Draw());
        }

        // ------------------------------------------------------------------
        // ChallengeDeck: a question, then a minigame, then a question.
        // ------------------------------------------------------------------

        private static List<int> MinigameIds()
        {
            var ids = new List<int>();
            foreach (var e in Smartest.Minigames.MinigameRegistry.All) ids.Add(e.RoundId);
            return ids;
        }

        private static bool IsMinigame(int id) => MinigameIds().Contains(id);

        [Test]
        public void Challenges_AlternateQuestionsAndMinigames()
        {
            var deck = new ChallengeDeck(AllIds(), MinigameIds(), InputOf, seed: 11, alternate: true);
            bool expectMinigame = false;
            for (int i = 0; i < 24; i++)
            {
                int id = deck.Draw();
                Assert.AreEqual(expectMinigame, IsMinigame(id), $"draw {i} broke the alternation");
                expectMinigame = !expectMinigame;
            }
        }

        [Test]
        public void Challenges_CanDealTwoMinigamesPerQuestion()
        {
            var deck = new ChallengeDeck(AllIds(), MinigameIds(), InputOf, seed: 17, alternate: true,
                minigamesPerQuestion: 2);
            for (int i = 0; i < 30; i++)
                Assert.AreEqual(i % 3 != 0, IsMinigame(deck.Draw()), $"draw {i}: expected question, minigame, minigame…");

            // A forced follow-up question still doesn't cost the minigames that were due.
            var forced = new ChallengeDeck(AllIds(), MinigameIds(), InputOf, seed: 18, alternate: true,
                minigamesPerQuestion: 2);
            Assert.IsFalse(IsMinigame(forced.Draw()));
            Assert.IsFalse(IsMinigame(forced.Draw(InputType.RedGreen)));
            Assert.IsTrue(IsMinigame(forced.Draw()));
            Assert.IsTrue(IsMinigame(forced.Draw()));
            Assert.IsFalse(IsMinigame(forced.Draw()));
        }

        [Test]
        public void Challenges_ForcedRedGreenAlwaysComesFromTheSocialDeck()
        {
            var deck = new ChallengeDeck(AllIds(), MinigameIds(), InputOf, seed: 5, alternate: true);
            for (int i = 0; i < 20; i++)
            {
                int id = deck.Draw(InputType.RedGreen);
                Assert.IsFalse(IsMinigame(id), "a forced follow-up must be a question, not a minigame");
                Assert.AreEqual(InputType.RedGreen, InputOf(id));
            }
        }

        [Test]
        public void Challenges_ForcedDrawDoesNotDisturbTheAlternation()
        {
            var deck = new ChallengeDeck(AllIds(), MinigameIds(), InputOf, seed: 21, alternate: true);
            Assert.IsFalse(IsMinigame(deck.Draw()), "the match opens with a question");
            deck.Draw(InputType.RedGreen);
            Assert.IsTrue(IsMinigame(deck.Draw()), "a minigame was still due after the forced question");
        }

        [Test]
        public void Challenges_WithoutAlternationEverythingIsOnePool()
        {
            var deck = new ChallengeDeck(AllIds(), MinigameIds(), InputOf, seed: 3, alternate: false);
            int minigames = 0;
            int total = AllIds().Count + MinigameIds().Count;
            for (int i = 0; i < total; i++) if (IsMinigame(deck.Draw())) minigames++;
            Assert.AreEqual(MinigameIds().Count, minigames, "one shuffled pool should deal every challenge once");
        }

        // ------------------------------------------------------------------
        // Which questions a match is dealt
        // ------------------------------------------------------------------

        [Test]
        public void TwoPlayerMatchesNeverDealRoundsThatNeedThree()
        {
            var library = UnityEngine.ScriptableObject.CreateInstance<RoundLibrary>();
            foreach (var spec in RoundCatalog.All) library.rounds.Add(RoundCatalog.CreateDefinition(spec));

            var forTwo = library.SocialIds(2);
            foreach (var spec in RoundCatalog.All)
                Assert.AreEqual(spec.MinPlayers <= 2, forTwo.Contains(spec.Id), spec.Title + " with two players");
            foreach (int id in new[] { 7, 8, 9, 19, 22 })
                Assert.IsFalse(forTwo.Contains(id), $"round {id} doesn't work with two players");
            Assert.AreEqual(RoundCatalog.All.Count, library.SocialIds(3).Count, "three players can be dealt every question");
            Assert.AreEqual(RoundCatalog.All.Count, library.SocialIds().Count, "no player count: everything");

            foreach (var d in library.rounds) UnityEngine.Object.DestroyImmediate(d);
            UnityEngine.Object.DestroyImmediate(library);
        }

        // ------------------------------------------------------------------
        // Someone leaves mid-match
        // ------------------------------------------------------------------

        private static readonly HashSet<int> NeedThree = new HashSet<int> { 7, 8, 9, 19, 22 };

        [Test]
        public void QuestionsTakenOutWhenSomeoneLeavesAreNeverDealtAgain()
        {
            var deck = new ChallengeDeck(AllIds(), MinigameIds(), InputOf, seed: 13, alternate: true);
            deck.Draw();
            Assert.AreEqual(NeedThree.Count, deck.RemoveSocial(NeedThree.Contains));
            for (int i = 0; i < 200; i++)
            {
                int id = i % 5 == 0 ? deck.Draw(InputType.RedGreen) : deck.Draw();
                CollectionAssert.DoesNotContain(NeedThree, id, $"draw {i} dealt a question that was taken out");
            }
        }

        [Test]
        public void TakingQuestionsOutKeepsEveryMinigameAndNeverEmptiesTheDeck()
        {
            // One shuffled pool, and a careless filter that also matches every minigame.
            var mixed = new ChallengeDeck(AllIds(), MinigameIds(), InputOf, seed: 4, alternate: false);
            mixed.RemoveSocial(id => IsMinigame(id) || NeedThree.Contains(id));
            var expected = new List<int>(MinigameIds());
            foreach (int id in AllIds()) if (!NeedThree.Contains(id)) expected.Add(id);
            var dealt = new List<int>();
            for (int i = 0; i < expected.Count; i++) dealt.Add(mixed.Draw());
            CollectionAssert.AreEquivalent(expected, dealt, "every minigame and every question that still works, once each");

            var deck = new RoundDeck(new List<int> { 1, 2 }, InputOf, seed: 1);
            Assert.AreEqual(0, deck.Remove(id => true), "a deck is never emptied");
            CollectionAssert.Contains(new[] { 1, 2 }, deck.Draw());
        }

        [Test]
        public void AMatchEndsWhenEveryoneElseLeaves()
        {
            Assert.IsTrue(GameState.IsAbandoned(2, 1, GamePhase.Answering), "the last of two players has nobody to play");
            Assert.IsTrue(GameState.IsAbandoned(8, 1, GamePhase.Play), "mid-minigame too");
            Assert.IsFalse(GameState.IsAbandoned(3, 2, GamePhase.Answering), "two can still play");
            Assert.IsFalse(GameState.IsAbandoned(1, 1, GamePhase.Answering), "a match started solo is a test run and plays on");
            Assert.IsFalse(GameState.IsAbandoned(4, 1, GamePhase.Winner), "a finished match keeps its winner screen");
        }

        [Test]
        public void WithQuestionRoundsOffAMatchIsAllMinigames()
        {
            // What GameState builds when GameConfig.questionRounds is off: no questions at all.
            var deck = new ChallengeDeck(new List<int>(), MinigameIds(), InputOf, seed: 9, alternate: true, minigamesPerQuestion: 2);
            for (int i = 0; i < 60; i++)
            {
                // Even a forced red/green follow-up (there can't be one) must not conjure a question.
                int id = i % 7 == 0 ? deck.Draw(InputType.RedGreen) : deck.Draw();
                Assert.IsTrue(IsMinigame(id), $"draw {i} dealt a question");
            }
        }

        [Test]
        public void Challenges_SurviveAnEmptyMinigameRegistry()
        {
            var deck = new ChallengeDeck(AllIds(), new List<int>(), InputOf, seed: 8, alternate: true);
            for (int i = 0; i < 10; i++) Assert.IsFalse(IsMinigame(deck.Draw()));
        }
    }
}
