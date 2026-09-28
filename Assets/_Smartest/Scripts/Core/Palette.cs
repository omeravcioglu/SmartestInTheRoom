using UnityEngine;

namespace Smartest.Core
{
    /// <summary>
    /// The Tabloid palette: newsprint paper, one ink, one gold. Red and green only ever mean
    /// an answer (or right/wrong inside a minigame); points gained are gold and points lost
    /// are blue, so a score can never be mistaken for an answer.
    ///
    /// The second block keeps the role names the first skin used (Panel, Accent, Neutral…).
    /// The minigames draw with those, so they pick up the new look without being touched.
    /// </summary>
    public static class Palette
    {
        // Helper: hex -> Color (supports "#RRGGBB" or "#RRGGBBAA").
        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return Color.magenta; // obvious "you typed a bad hex" color
        }

        // --- Paper and ink ---
        public static readonly Color Paper    = Hex("#F3EDE0"); // page ground behind everything
        public static readonly Color PaperHi  = Hex("#FFFCF4"); // cards, boxes, bubbles, panels
        public static readonly Color Paper2   = Hex("#E9E1CF"); // tokens, idle grid cells, neutral bands
        public static readonly Color Ink      = Hex("#1A1916"); // text, borders, rules, hard shadows
        public static readonly Color Ink2     = Hex("#4A463E"); // secondary text, "thinking…"

        // --- Gold: you, points gained, timers, gold zones ---
        public static readonly Color Gold      = Hex("#FFC21A");
        public static readonly Color GoldSoft  = Hex("#FFE9A8"); // your row inside lists
        public static readonly Color GoldHatch = Hex("#F3D77E"); // second stripe of the hatch

        // --- Answers ---
        public static readonly Color Red       = Hex("#D7263D");
        public static readonly Color RedText   = Hex("#C41E36"); // the word "Red" inside sentences
        public static readonly Color OnRed     = Hex("#FFF7EE"); // text on a red fill
        public static readonly Color Green     = Hex("#1DB86C");
        public static readonly Color GreenText = Hex("#0E7A45"); // the word "Green" inside sentences

        // --- Points lost ---
        public static readonly Color Blue   = Hex("#1F5FD1");
        public static readonly Color OnBlue = Hex("#FFFFFF");

        /// <summary>Behind modals.</summary>
        public static readonly Color Scrim = new Color(Ink.r, Ink.g, Ink.b, 0.62f);

        // --- Roles from the first skin, mapped onto the paper ---
        public static readonly Color Background   = Paper;
        public static readonly Color Panel        = Paper2;  // arenas and fields inside a game
        public static readonly Color PanelRaised  = PaperHi; // "raised" is lighter paper now
        public static readonly Color Divider      = Ink;
        public static readonly Color Text         = Ink;
        public static readonly Color TextDim      = Ink2;
        public static readonly Color TextOnAccent = Ink;
        public static readonly Color Accent       = Gold;
        public static readonly Color AccentDim    = GoldSoft;
        public static readonly Color Neutral      = Paper2;  // idle cells, number tiles

        // --- Score deltas ---
        public static readonly Color Positive = Gold;
        public static readonly Color Negative = Blue;
        public static readonly Color Zero     = Ink2;

        // --- States ---
        public static readonly Color LocalHighlight = Gold;
        public static readonly Color LockedIn       = Ink;
        public static readonly Color Thinking       = Ink2;

        /// <summary>"#RRGGBB" for TMP rich-text tags.</summary>
        public static string ToHex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Colour equality that survives the float round-trip through a Graphic.</summary>
        public static bool Same(Color a, Color b)
        {
            const float e = 0.004f;
            return Mathf.Abs(a.r - b.r) < e && Mathf.Abs(a.g - b.g) < e && Mathf.Abs(a.b - b.b) < e;
        }

        /// <summary>The fill a delta sticker sits on: gold for gained, blue for lost, paper for nothing.</summary>
        public static Color DeltaFill(int delta) => delta > 0 ? Gold : delta < 0 ? Blue : PaperHi;

        /// <summary>The text colour that goes with <see cref="DeltaFill"/>.</summary>
        public static Color DeltaText(int delta) => delta < 0 ? OnBlue : Ink;
    }
}
