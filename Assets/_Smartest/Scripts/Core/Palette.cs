using UnityEngine;

namespace Smartest.Core
{
    /// <summary>
    /// Central color palette for the whole game. One dark ground, one accent,
    /// plus the red/green answer colors and score up/down colors.
    /// Change these in one place to reskin everything. All UI code should read
    /// colors from here rather than hard-coding hex values.
    /// </summary>
    public static class Palette
    {
        // Helper: hex -> Color (supports "#RRGGBB" or "#RRGGBBAA").
        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return Color.magenta; // obvious "you typed a bad hex" color
        }

        // --- Surfaces ---
        public static readonly Color Background  = Hex("#16171D"); // app background
        public static readonly Color Panel       = Hex("#21232E"); // default panel fill
        public static readonly Color PanelRaised  = Hex("#2A2D3A"); // buttons, rows, raised chips
        public static readonly Color Divider      = Hex("#33374A");

        // --- Text ---
        public static readonly Color Text     = Hex("#F5F6FA");
        public static readonly Color TextDim  = Hex("#9AA0B4");
        public static readonly Color TextOnAccent = Hex("#1A1400");

        // --- Accent (quiz-show gold) ---
        public static readonly Color Accent      = Hex("#F5C542");
        public static readonly Color AccentDim   = Hex("#8A7220");

        // --- Answer colors ---
        public static readonly Color Red     = Hex("#E5484D");
        public static readonly Color Green   = Hex("#30A46C");
        public static readonly Color Neutral = Hex("#3A3F52"); // yes/no buttons, number tiles

        // --- Score deltas ---
        public static readonly Color Positive = Hex("#30A46C");
        public static readonly Color Negative = Hex("#E5484D");
        public static readonly Color Zero     = Hex("#9AA0B4");

        /// <summary>"#RRGGBB" for TMP rich-text tags.</summary>
        public static string ToHex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // --- States ---
        public static readonly Color LocalHighlight = Hex("#F5C542");
        public static readonly Color LockedIn       = Hex("#30A46C");
        public static readonly Color Thinking       = Hex("#9AA0B4");
    }
}
