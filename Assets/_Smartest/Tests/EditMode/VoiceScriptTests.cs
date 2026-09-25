using System.Collections.Generic;
using NUnit.Framework;
using Smartest.Core;
using Smartest.Minigames;

namespace Smartest.Tests
{
    /// <summary>
    /// The host script is content, not logic, so the only things worth testing are the ones
    /// that would quietly break it: a key nothing can ever play, a minigame with no intro, a
    /// line so long the moment has passed by the time it finishes.
    /// </summary>
    public class VoiceScriptTests
    {
        [Test]
        public void EveryEntryHasAKeyAndAtLeastOneLine()
        {
            foreach (var e in VoiceScript.All())
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.Key), "an entry has no key");
                Assert.IsNotNull(e.Texts, e.Key + " has no lines");
                Assert.Greater(e.Texts.Length, 0, e.Key + " has no lines");
                foreach (var t in e.Texts)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(t), e.Key + " has a blank line");
            }
        }

        [Test]
        public void KeysAreUnique()
        {
            var seen = new HashSet<string>();
            foreach (var e in VoiceScript.All())
                Assert.IsTrue(seen.Add(e.Key), "duplicate voice key " + e.Key);
        }

        [Test]
        public void LinesAreShortEnoughToLandBeforeTheMomentPasses()
        {
            foreach (var e in VoiceScript.All())
                foreach (var t in e.Texts)
                    Assert.Less(t.Length, 180, e.Key + ": \"" + t + "\" is too long to speak over a round change");
        }

        [Test]
        public void TheLinesYouHearMostHaveVariants()
        {
            // These fire several times a match; one recording each would get old fast.
            string[] frequent =
            {
                VoiceKeys.LevelUp, VoiceKeys.YouAreOut, VoiceKeys.LeadChange,
                VoiceKeys.MinigameWinner, VoiceKeys.RoundStart, VoiceKeys.LobbyPlayerJoined
            };
            foreach (var key in frequent)
            {
                var entry = Find(key);
                Assert.IsNotNull(entry, "missing voice key " + key);
                Assert.GreaterOrEqual(entry.Texts.Length, 3, key + " needs at least three variants");
            }
        }

        [Test]
        public void EveryMinigameHasItsOwnIntro()
        {
            foreach (var game in MinigameRegistry.All)
            {
                string key = VoiceKeys.Minigame(game.Id);
                Assert.IsNotNull(Find(key),
                    $"minigame '{game.Id}' has no intro line — add one to VoiceScript.MinigameIntros");
            }
        }

        [Test]
        public void NoIntroIsWrittenForAMinigameThatDoesNotExist()
        {
            foreach (var e in VoiceScript.All())
            {
                if (!e.Key.StartsWith("mg_")) continue;
                string id = e.Key.Substring(3);
                Assert.IsNotNull(MinigameRegistry.Get(id),
                    $"'{e.Key}' is an intro for a minigame that isn't registered");
            }
        }

        [Test]
        public void ChanceLinesActuallyHaveAChance()
        {
            foreach (var e in VoiceScript.All())
                if (e.Mode == VoiceLines.PlayMode.Chance)
                    Assert.IsTrue(e.Chance > 0f && e.Chance <= 1f, e.Key + " has an impossible chance");
        }

        [Test]
        public void TheScriptConvertsIntoPlayableLines()
        {
            var lines = VoiceLines.EntriesFromScript();
            Assert.AreEqual(VoiceScript.All().Count, lines.Count);
            foreach (var l in lines)
                Assert.AreEqual(l.texts.Length, l.clips.Length,
                    l.key + ": there must be one clip slot per written line");
        }

        [Test]
        public void TheScriptIsBigEnoughToStayFresh()
        {
            Assert.GreaterOrEqual(VoiceScript.ClipCount(), 100);
        }

        private static VoiceScript.Entry Find(string key)
        {
            foreach (var e in VoiceScript.All()) if (e.Key == key) return e;
            return null;
        }
    }
}
