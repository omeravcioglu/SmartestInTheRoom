using System.Collections.Generic;
using NUnit.Framework;
using Smartest.Core;
using Smartest.Rounds;
using Smartest.UI;
using UnityEngine;

namespace Smartest.Tests
{
    /// <summary>The pure parts of the Tabloid skin: rule markup, monograms, headlines, generated art.</summary>
    public class TabloidTests
    {
        // ---- THE DEAL markup ----

        [Test]
        public void Gains_SitOnGold()
        {
            string s = DealText.Format("everyone gets +10.");
            StringAssert.Contains($"<link=\"{TextMarks.Gain}\"><b>+10</b></link>", s);
        }

        [Test]
        public void Losses_SitOnBlue_WhateverTheVerb()
        {
            foreach (string rule in new[] { "Greens lose 5.", "everyone loses 10.", "Pulling costs you 5.",
                         "Donating costs 3", "every attacker pays 3.", "−5 each if anyone joins", "-5 each" })
            {
                string s = DealText.Format(rule);
                StringAssert.Contains($"<link=\"{TextMarks.Loss}\">", s, rule);
            }
        }

        [Test]
        public void AGainAfterPays_IsStillAGain()
        {
            string s = DealText.Format("Red pays +8, but only if HALF of you pick Red.");
            StringAssert.Contains("<b>+8</b>", s);
            StringAssert.DoesNotContain(TextMarks.Loss, s);
        }

        [Test]
        public void ColourWords_AndShouting()
        {
            string s = DealText.Format("If EVERYONE picks Green, Reds get +15.");
            StringAssert.Contains($"<color={Palette.ToHex(Palette.GreenText)}><b>Green</b></color>", s);
            StringAssert.Contains($"<color={Palette.ToHex(Palette.RedText)}><b>Reds</b></color>", s);
            StringAssert.Contains("<b>EVERYONE</b>", s);
        }

        [Test]
        public void PlainNumbers_AreLeftAlone()
        {
            Assert.AreEqual("6 per player or less (0).", DealText.Format("6 per player or less (0)."));
        }

        [Test]
        public void EveryCatalogueRule_Formats()
        {
            foreach (var spec in RoundCatalog.AllChallenges())
            {
                string s = DealText.Format(spec.Rule);
                Assert.IsNotEmpty(s, spec.Title);
                // Every tag we open, we close.
                Assert.AreEqual(Count(s, "<link="), Count(s, "</link>"), spec.Title);
                Assert.AreEqual(Count(s, "<b>"), Count(s, "</b>"), spec.Title);
            }
        }

        private static int Count(string s, string what)
        {
            int n = 0, i = 0;
            while ((i = s.IndexOf(what, i, System.StringComparison.Ordinal)) >= 0) { n++; i += what.Length; }
            return n;
        }

        // ---- Monograms ----

        [Test]
        public void Monogram_IsTheFirstTwoLetters()
        {
            Assert.AreEqual("AY", Monogram.Base("Ayşe"));
            Assert.AreEqual("DE", Monogram.Base("deniz"));
            Assert.AreEqual("J2", Monogram.Base("  j2!"));
            Assert.AreEqual("?", Monogram.Base(""));
        }

        [Test]
        public void Monograms_NeverClash()
        {
            var names = new List<string> { "Can", "Canan", "Cansu", "Cem", "Cemre", "Ca", "Can" };
            var tags = Monogram.ForAll(names);
            Assert.AreEqual(names.Count, tags.Count);
            Assert.AreEqual(tags.Count, new HashSet<string>(tags).Count, string.Join(",", tags));
            Assert.AreEqual("CA", tags[0]);
        }

        // ---- Headlines and places ----

        [Test]
        public void SplitHeadline_CountsBothSides()
        {
            var def = ScriptableObject.CreateInstance<RoundDefinition>();
            def.inputType = InputType.RedGreen;
            var results = new List<PlayerRoundResult>
            {
                new PlayerRoundResult { Answer = 1 }, new PlayerRoundResult { Answer = 0 },
                new PlayerRoundResult { Answer = 0 }, new PlayerRoundResult { Answer = -1 }
            };
            string s = RevealPanel.SplitHeadline(def, results);
            StringAssert.StartsWith("1 ", s);
            StringAssert.Contains("Red.", s);
            StringAssert.Contains("2 ", s);
            StringAssert.Contains("Green.", s);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void SplitHeadline_NamedButtonsUseTheirWords()
        {
            var def = ScriptableObject.CreateInstance<RoundDefinition>();
            def.inputType = InputType.YesNo;
            def.buttonNameA = "volunteer";
            def.buttonNameB = "stay quiet";
            var results = new List<PlayerRoundResult> { new PlayerRoundResult { Answer = 1 }, new PlayerRoundResult { Answer = 0 } };
            string s = RevealPanel.SplitHeadline(def, results);
            StringAssert.Contains("Volunteer.", s);
            StringAssert.Contains("Stay quiet.", s);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void Ordinals()
        {
            Assert.AreEqual("1ST", RevealPanel.Ordinal(1));
            Assert.AreEqual("2ND", RevealPanel.Ordinal(2));
            Assert.AreEqual("3RD", RevealPanel.Ordinal(3));
            Assert.AreEqual("11TH", RevealPanel.Ordinal(11));
            Assert.AreEqual("22ND", RevealPanel.Ordinal(22));
        }

        [Test]
        public void Deltas_UseAProperMinus()
        {
            Assert.AreEqual("+15", SeatCard.Format(15));
            Assert.AreEqual("−5", SeatCard.Format(-5));
            Assert.AreEqual("0", SeatCard.Format(0));
            Assert.AreEqual(Palette.Gold, Palette.DeltaFill(3));
            Assert.AreEqual(Palette.Blue, Palette.DeltaFill(-3));
            Assert.AreEqual("−9", SeatCard.Score(-9));
            Assert.AreEqual("12", SeatCard.Score(12));
        }

        // ---- Generated art ----

        [Test]
        public void Catalogue_HasUniqueNamesAndSaneTextures()
        {
            var names = new HashSet<string>();
            foreach (var b in InkSprites.RenderCatalogue())
            {
                Assert.IsTrue(names.Add(b.Name), "duplicate sprite " + b.Name);
                Assert.Greater(b.Texture.width, 0, b.Name);
                Assert.LessOrEqual(b.Border.x * 2f, b.Texture.width, b.Name);
                Assert.LessOrEqual(b.Border.y * 2f, b.Texture.height, b.Name);
                Object.DestroyImmediate(b.Texture);
            }
            Assert.Greater(names.Count, 30);
        }

        [Test]
        public void Box_IsWhiteInsideInkOnTheRimClearOutside()
        {
            foreach (var b in InkSprites.RenderCatalogue())
            {
                if (b.Name != InkSprites.BoxName(3f, 0f)) { Object.DestroyImmediate(b.Texture); continue; }
                var t = b.Texture;
                int mid = t.width / 2;
                Color centre = t.GetPixel(mid, mid);
                Color corner = t.GetPixel(0, 0);
                // 1 px margin, then a 3 px rim, at 2 texels per pixel: rim texels are 2..7.
                Color rim = t.GetPixel(mid, 4);
                Assert.Greater(centre.r, 0.95f, "inside should be white so the image colour fills it");
                Assert.Less(corner.a, 0.05f, "the margin should be clear");
                Assert.Less(rim.r, 0.2f, "the rim should be ink");
                Assert.Greater(rim.a, 0.95f);
                Object.DestroyImmediate(t);
            }
        }
    }
}
