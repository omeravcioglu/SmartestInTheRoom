using TMPro;
using UnityEngine;

namespace Smartest.Core
{
    /// <summary>
    /// The font faces, one per <see cref="TypeRole"/>. SceneBuilder fills
    /// Resources/SmartestFonts.asset from the TTFs in Assets/_Smartest/Fonts; any slot left
    /// empty falls back to TextMeshPro's default font, so the game always has readable text.
    /// </summary>
    [CreateAssetMenu(menuName = "Smartest/Font Set", fileName = "SmartestFonts")]
    public class FontSet : ScriptableObject
    {
        public TMP_FontAsset display;
        public TMP_FontAsset sticker;
        public TMP_FontAsset names;
        public TMP_FontAsset label;
        [Tooltip("Regular weight. Its weight table points <b> and <i> at the bold and italic faces.")]
        public TMP_FontAsset body;
        public TMP_FontAsset mono;

        public TMP_FontAsset For(TypeRole role)
        {
            switch (role)
            {
                case TypeRole.Display: return display;
                case TypeRole.Sticker: return sticker != null ? sticker : display;
                case TypeRole.Name: return names != null ? names : display;
                case TypeRole.Label: return label;
                case TypeRole.Body: return body;
                case TypeRole.Mono: return mono;
                default: return null;
            }
        }
    }
}
