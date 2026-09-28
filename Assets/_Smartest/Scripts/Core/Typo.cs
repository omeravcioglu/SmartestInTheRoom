using TMPro;
using UnityEngine;

namespace Smartest.Core
{
    /// <summary>The six jobs text does in the Tabloid skin. Each maps to one font face.</summary>
    public enum TypeRole
    {
        /// <summary>Archivo ExtraCondensed Black: headlines, slabs, scores, big numbers, kickers.</summary>
        Display,
        /// <summary>Archivo Black: stickers, stamps, keycaps, ranks.</summary>
        Sticker,
        /// <summary>Archivo Condensed ExtraBold: player names, never upper-cased.</summary>
        Name,
        /// <summary>Archivo Expanded ExtraBold: small tracked caps like THE DEAL.</summary>
        Label,
        /// <summary>Atkinson Hyperlegible Next: rules, host lines, sentences.</summary>
        Body,
        /// <summary>Atkinson Hyperlegible Mono ExtraBold: join codes and typed words.</summary>
        Mono
    }

    /// <summary>
    /// Applies a type role to a TMP text: face, size, caps, tracking and the line height the
    /// design calls for. With the real fonts the faces carry their own weight; on the fallback
    /// font the heavy roles are set bold so the hierarchy still reads.
    /// </summary>
    public static class Typo
    {
        public const string ResourceName = "SmartestFonts";

        private static FontSet s_set;
        private static bool s_loaded;

        public static FontSet Set
        {
            get
            {
                if (!s_loaded)
                {
                    s_set = Resources.Load<FontSet>(ResourceName);
                    s_loaded = true;
                }
                return s_set;
            }
        }

        /// <summary>Forget the cached set (SceneBuilder calls this after rebuilding the fonts).</summary>
        public static void Reload()
        {
            s_set = null;
            s_loaded = false;
        }

        public static TMP_FontAsset Font(TypeRole role, out bool real)
        {
            var set = Set;
            var f = set != null ? set.For(role) : null;
            real = f != null;
            return real ? f : TMP_Settings.defaultFontAsset;
        }

        public static bool HasRealFont(TypeRole role)
        {
            Font(role, out bool real);
            return real;
        }

        /// <param name="tracking">Letter spacing in em, as the design gives it (0.14 = +0.14em).</param>
        /// <param name="lineHeight">Line height as a multiple of the size (CSS line-height). 0 = the font's own.</param>
        public static void Apply(TMP_Text t, TypeRole role, float size, bool caps = false, float tracking = 0f,
            float lineHeight = 0f)
        {
            if (t == null) return;
            var font = Font(role, out bool real);
            if (font != null) t.font = font;
            t.fontSize = size;

            var style = FontStyles.Normal;
            if (!real && role != TypeRole.Body) style |= FontStyles.Bold;
            if (caps) style |= FontStyles.UpperCase;
            t.fontStyle = style;

            t.characterSpacing = tracking * 100f; // TMP spacing is in em/100
            t.lineSpacing = lineHeight > 0f ? LineSpacingFor(t.font, lineHeight) : 0f;
        }

        /// <summary>
        /// TMP's lineSpacing is an adjustment (em/100) on top of the face's own line height;
        /// the design speaks CSS line-height. Convert one to the other for this face.
        /// </summary>
        private static float LineSpacingFor(TMP_FontAsset font, float cssLineHeight)
        {
            if (font == null) return 0f;
            var info = font.faceInfo;
            float natural = info.pointSize > 0 ? info.lineHeight / info.pointSize : 1.2f;
            return (cssLineHeight - natural) * 100f;
        }
    }
}
