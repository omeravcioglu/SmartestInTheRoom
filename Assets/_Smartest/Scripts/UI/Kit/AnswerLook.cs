using Smartest.Core;
using Smartest.Rounds;
using UnityEngine;

namespace Smartest.UI
{
    /// <summary>
    /// How one answer looks everywhere it appears — slab, seat band, reveal bucket — so RED is
    /// the same red diamond on every screen. Red carries a diamond and green a circle, so the
    /// two never depend on colour alone.
    /// </summary>
    public struct AnswerLook
    {
        public string Word;
        public Color Fill;
        public Color Ink;
        public Sprite Glyph;
        /// <summary>The colour of the word when it appears inside a sentence.</summary>
        public Color WordInText;

        public static AnswerLook For(RoundDefinition def, int answer)
        {
            var type = def != null ? def.inputType : InputType.Number1to10;
            switch (type)
            {
                case InputType.RedGreen:
                    return answer == 1
                        ? new AnswerLook { Word = "RED", Fill = Palette.Red, Ink = Palette.OnRed, Glyph = InkSprites.Diamond, WordInText = Palette.RedText }
                        : new AnswerLook { Word = "GREEN", Fill = Palette.Green, Ink = Palette.Ink, Glyph = InkSprites.Dot, WordInText = Palette.GreenText };
                case InputType.YesNo:
                    return answer == 1
                        ? new AnswerLook { Word = Name(def.buttonNameA, "YES"), Fill = Palette.Ink, Ink = Palette.OnRed, WordInText = Palette.Ink }
                        : new AnswerLook { Word = Name(def.buttonNameB, "NO"), Fill = Palette.Paper2, Ink = Palette.Ink, WordInText = Palette.Ink };
                default:
                    return new AnswerLook { Word = answer.ToString(), Fill = Palette.PaperHi, Ink = Palette.Ink, WordInText = Palette.Ink };
            }
        }

        /// <summary>The look of the slab you press, before anyone has answered.</summary>
        public static AnswerLook ForButton(RoundDefinition def, int answer)
        {
            var look = For(def, answer);
            if (def != null && def.inputType == InputType.YesNo)
            {
                // Named options are both plain paper slabs until the reveal splits them.
                look.Fill = Palette.PaperHi;
                look.Ink = Palette.Ink;
            }
            return look;
        }

        private static string Name(string custom, string fallback)
            => string.IsNullOrWhiteSpace(custom) ? fallback : custom.ToUpperInvariant();
    }
}
