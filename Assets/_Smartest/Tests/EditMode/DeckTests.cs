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

        [Test]
        public void Challenges_SurviveAnEmptyMinigameRegistry()
        {
            var deck = new ChallengeDeck(AllIds(), new List<int>(), InputOf, seed: 8, alternate: true);
            for (int i = 0; i < 10; i++) Assert.IsFalse(IsMinigame(deck.Draw()));
        }
    }
}
