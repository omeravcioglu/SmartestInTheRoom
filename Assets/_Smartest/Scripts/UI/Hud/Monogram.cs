using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Smartest.UI
{
    /// <summary>
    /// Two-letter tags for the tokens and the race to 100: "Ayşe" → AY, "Deniz" → DE.
    /// Players whose first two letters clash get the first and last letter instead, then a
    /// digit, so every tag on screen is unique. Deterministic: every client computes the same
    /// tags from the same roster.
    /// </summary>
    public static class Monogram
    {
        public static string Base(string name)
        {
            var letters = new StringBuilder(2);
            if (!string.IsNullOrEmpty(name))
            {
                foreach (char c in name)
                {
                    if (!char.IsLetterOrDigit(c)) continue;
                    letters.Append(char.ToUpper(c, CultureInfo.InvariantCulture));
                    if (letters.Length == 2) break;
                }
            }
            if (letters.Length == 0) return "?";
            return letters.ToString();
        }

        /// <summary>Tags for a list of names, in the same order, all distinct.</summary>
        public static List<string> ForAll(IReadOnlyList<string> names)
        {
            var result = new List<string>(names.Count);
            var taken = new HashSet<string>();
            for (int i = 0; i < names.Count; i++)
            {
                string tag = Base(names[i]);
                if (taken.Contains(tag)) tag = FirstAndLast(names[i]);
                int n = 2;
                string stem = tag.Substring(0, 1);
                while (taken.Contains(tag)) tag = stem + n++;
                taken.Add(tag);
                result.Add(tag);
            }
            return result;
        }

        private static string FirstAndLast(string name)
        {
            char first = '\0', last = '\0';
            if (!string.IsNullOrEmpty(name))
            {
                foreach (char c in name)
                {
                    if (!char.IsLetterOrDigit(c)) continue;
                    if (first == '\0') first = c;
                    else last = c;
                }
            }
            if (first == '\0') return "?";
            if (last == '\0') return char.ToUpper(first, CultureInfo.InvariantCulture).ToString();
            return char.ToUpper(first, CultureInfo.InvariantCulture).ToString() + char.ToUpper(last, CultureInfo.InvariantCulture);
        }
    }
}
