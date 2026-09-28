using System.Text.RegularExpressions;
using Smartest.Core;

namespace Smartest.UI
{
    /// <summary>
    /// Marks up a round's rule for THE DEAL box: points gained sit on gold, points lost on
    /// blue (as links a <see cref="TextMarks"/> paints behind), the words Red and Green take
    /// their colour, and the shouted words (EVERYONE, EXACTLY) go bold. Rules stay plain
    /// English in RoundCatalog; this is the only place that knows how they're dressed.
    /// </summary>
    public static class DealText
    {
        // One pass, so nothing inside an inserted tag can be matched again.
        private static readonly Regex Pattern = new Regex(
            @"(?<gain>\+\d+)" +
            @"|(?<verb>\b(?:lose|loses|costs you|costs everyone|costs|pays|pay)\s)(?<loss>\d+)" +
            @"|(?<neg>[−-]\d+)" +
            @"|(?<red>\b[Rr]eds?\b)" +
            @"|(?<green>\b[Gg]reens?\b)" +
            @"|(?<caps>\b[A-Z]{3,}\b)",
            RegexOptions.CultureInvariant);

        /// <summary>The rule with gains, losses, colours and emphasis marked up for TMP.</summary>
        public static string Format(string rule)
        {
            if (string.IsNullOrEmpty(rule)) return string.Empty;
            return Pattern.Replace(rule, m =>
            {
                if (m.Groups["gain"].Success) return Gain(m.Value);
                if (m.Groups["loss"].Success) return m.Groups["verb"].Value + Loss(m.Groups["loss"].Value);
                if (m.Groups["neg"].Success) return Loss("−" + m.Value.Substring(1));
                if (m.Groups["red"].Success) return Word(m.Value, Palette.RedText);
                if (m.Groups["green"].Success) return Word(m.Value, Palette.GreenText);
                if (m.Groups["caps"].Success) return "<b>" + m.Value + "</b>";
                return m.Value;
            });
        }

        /// <summary>Only the colour words — for headlines, which carry no point marks.</summary>
        public static string ColourWords(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return Regex.Replace(text, @"\b(?<red>[Rr]eds?|RED)\b|\b(?<green>[Gg]reens?|GREEN)\b", m =>
                m.Groups["red"].Success ? Colour(m.Value, Palette.RedText) : Colour(m.Value, Palette.GreenText));
        }

        // The mark is padded 0.2em each side, like CSS padding: the words make room for it.
        private const string Pad = "<space=0.2em>";

        /// <summary>A gain: TextMarks paints gold behind it.</summary>
        public static string Gain(string text) =>
            $"{Pad}<link=\"{TextMarks.Gain}\"><b>{text}</b></link>{Pad}";

        /// <summary>A loss: white on the blue TextMarks paints behind it.</summary>
        public static string Loss(string text) =>
            $"{Pad}<link=\"{TextMarks.Loss}\"><color={Palette.ToHex(Palette.OnBlue)}><b>{text}</b></color></link>{Pad}";

        private static string Word(string word, UnityEngine.Color c) => $"<color={Palette.ToHex(c)}><b>{word}</b></color>";

        private static string Colour(string word, UnityEngine.Color c) => $"<color={Palette.ToHex(c)}>{word}</color>";
    }
}
