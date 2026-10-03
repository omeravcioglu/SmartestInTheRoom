using System.Collections.Generic;
using NUnit.Framework;
using Smartest.Minigames;
using Smartest.Rounds;
using Smartest.UI;
using UnityEngine;

namespace Smartest.Tests
{
    /// <summary>The host's match options (length, which games) and the window sizes the settings offer.</summary>
    public class HostOptionsTests
    {
        private static List<MinigameEntry> Games(params string[] ids)
        {
            var list = new List<MinigameEntry>();
            foreach (var id in ids) list.Add(new MinigameEntry { Id = id, Title = id });
            return list;
        }

        private static readonly List<MinigameEntry> Five = Games("a", "b", "c", "d", "e");

        [Test]
        public void ANewHostPlaysTheStandardLengthWithEveryGame()
        {
            var o = new HostOptions();
            Assert.AreEqual(HostOptions.StandardLength, o.LengthIndex);
            Assert.AreEqual(100, o.Target);
            Assert.AreEqual(5, o.CountOn(Five));
        }

        [Test]
        public void TheLengthsAreShortStandardLong()
        {
            CollectionAssert.AreEqual(new[] { 50, 100, 150 }, System.Array.ConvertAll(HostOptions.Lengths, l => l.Target));
            var o = new HostOptions();
            o.SetLength(0);
            Assert.AreEqual(50, o.Target);
            o.SetLength(99);
            Assert.AreEqual(150, o.Target, "out of range clamps to the longest");
            o.SetLength(-3);
            Assert.AreEqual(50, o.Target, "and the shortest");
        }

        [Test]
        public void GamesGoOutAndComeBack()
        {
            var o = new HostOptions();
            Assert.IsTrue(o.SetOn("b", false, Five));
            Assert.IsFalse(o.IsOn("b"));
            Assert.AreEqual(4, o.CountOn(Five));
            Assert.IsFalse(o.SetOn("b", false, Five), "already out: nothing changes");
            Assert.IsTrue(o.SetOn("b", true, Five));
            Assert.AreEqual(5, o.CountOn(Five));
        }

        [Test]
        public void TheLastFewGamesCantBeTakenOut()
        {
            var o = new HostOptions();
            Assert.IsTrue(o.SetOn("a", false, Five));
            Assert.IsTrue(o.SetOn("b", false, Five));
            Assert.AreEqual(HostOptions.MinGamesOn, o.CountOn(Five));
            Assert.IsFalse(o.SetOn("c", false, Five), $"{HostOptions.MinGamesOn} have to stay in");
            Assert.IsTrue(o.IsOn("c"));
            o.AllOn();
            Assert.AreEqual(5, o.CountOn(Five));
        }

        [Test]
        public void OptionsSurviveSavingAndLoading()
        {
            var o = new HostOptions();
            o.SetLength(2);
            o.SetOn("d", false, Five);
            o.SetOn("a", false, Five);
            string saved = o.Serialize();
            Assert.AreEqual("2|a,d", saved, "the length, then the games that are out, in order");

            var back = HostOptions.Parse(saved, Five);
            Assert.AreEqual(2, back.LengthIndex);
            Assert.IsFalse(back.IsOn("a"));
            Assert.IsFalse(back.IsOn("d"));
            Assert.IsTrue(back.IsOn("b"));
        }

        [Test]
        public void JunkOrAStaleSaveFallsBackSensibly()
        {
            foreach (var junk in new[] { null, "", "abc", "|", "x|y", "7" })
            {
                var o = HostOptions.Parse(junk, Five);
                Assert.AreEqual(5, o.CountOn(Five), $"'{junk}' keeps every game");
            }
            Assert.AreEqual(2, HostOptions.Parse("7", Five).LengthIndex, "a length out of range clamps");

            var stale = HostOptions.Parse("0|gone,b", Five);
            Assert.AreEqual(0, stale.LengthIndex);
            Assert.IsFalse(stale.IsOn("b"));
            Assert.AreEqual("0|b", stale.Serialize(), "a game removed in an update is forgotten");

            var tooFew = HostOptions.Parse("1|a,b,c,d", Five);
            Assert.AreEqual(5, tooFew.CountOn(Five), "a save that leaves fewer than the minimum turns everything back on");
        }

        [Test]
        public void WindowSizesFitTheScreenWithRoomForTheTitleBar()
        {
            CollectionAssert.AreEqual(new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900) },
                SettingsPanel.WindowSizes(1920, 1080), "a 1080p screen can't hold a 1080p window");
            CollectionAssert.AreEqual(new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) },
                SettingsPanel.WindowSizes(2560, 1440));
            Assert.AreEqual(5, SettingsPanel.WindowSizes(3840, 2160).Count);
            CollectionAssert.AreEqual(new[] { new Vector2Int(1280, 720) }, SettingsPanel.WindowSizes(1366, 768),
                "never an empty list, however small the screen");
        }

        [Test]
        public void TheWindowSizeShownIsTheNearestOnOffer()
        {
            var sizes = SettingsPanel.WindowSizes(2560, 1440);
            Assert.AreEqual(new Vector2Int(1600, 900), SettingsPanel.Nearest(sizes, new Vector2Int(1700, 950)));
            Assert.AreEqual(new Vector2Int(1920, 1080), SettingsPanel.Nearest(sizes, new Vector2Int(2560, 1440)));
            Assert.AreEqual(new Vector2Int(1280, 720), SettingsPanel.Nearest(sizes, new Vector2Int(800, 600)));
        }
    }
}
