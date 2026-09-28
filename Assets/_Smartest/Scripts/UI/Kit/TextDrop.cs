using Smartest.Core;
using TMPro;
using UnityEngine;

namespace Smartest.UI
{
    /// <summary>
    /// The gold drop behind a headline ("text-shadow: 6px 6px 0 gold"). TextMeshPro has no
    /// hard offset shadow, so this keeps a second copy of the text one sibling below, always
    /// gold (colour tags overridden), following the headline's text, rect and alpha.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMeshProUGUI))]
    [AddComponentMenu("Smartest/Text Drop")]
    public class TextDrop : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI copy;
        [SerializeField] private Vector2 offset = new Vector2(6f, -6f);
        [SerializeField] private Color color = new Color(1f, 0.761f, 0.102f, 1f);

        private TextMeshProUGUI _main;

        public TextMeshProUGUI Copy => copy;

        /// <summary>Give <paramref name="main"/> a drop of <paramref name="distance"/> px.</summary>
        public static TextDrop Add(TextMeshProUGUI main, float distance, Color? dropColor = null)
        {
            var drop = main.GetComponent<TextDrop>();
            if (drop == null) drop = main.gameObject.AddComponent<TextDrop>();

            if (drop.copy == null)
            {
                var go = new GameObject(main.name + "Drop", typeof(RectTransform));
                go.transform.SetParent(main.transform.parent, false);
                go.transform.SetSiblingIndex(main.transform.GetSiblingIndex()); // just behind
                drop.copy = go.AddComponent<TextMeshProUGUI>();
                // A layout group must not give the copy a slot of its own; it follows the headline.
                go.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            }

            drop.offset = new Vector2(distance, -distance);
            drop.color = dropColor ?? Palette.Gold;

            var c = drop.copy;
            c.font = main.font;
            c.fontStyle = main.fontStyle;
            c.fontSize = main.fontSize;
            c.enableAutoSizing = main.enableAutoSizing;
            c.fontSizeMin = main.fontSizeMin;
            c.fontSizeMax = main.fontSizeMax;
            c.characterSpacing = main.characterSpacing;
            c.lineSpacing = main.lineSpacing;
            c.alignment = main.alignment;
            c.textWrappingMode = main.textWrappingMode;
            c.overflowMode = main.overflowMode;
            c.richText = main.richText;
            c.overrideColorTags = true;
            c.raycastTarget = false;
            drop.Sync();
            return drop;
        }

        private void OnEnable()
        {
            if (copy != null) copy.gameObject.SetActive(true);
            Sync();
        }

        private void OnDisable()
        {
            if (copy != null) copy.gameObject.SetActive(false);
        }

        private void LateUpdate() => Sync();

        public void Sync()
        {
            if (_main == null) _main = GetComponent<TextMeshProUGUI>();
            if (_main == null || copy == null) return;

            if (copy.text != _main.text) copy.text = _main.text;
            if (!_main.enableAutoSizing && !Mathf.Approximately(copy.fontSize, _main.fontSize)) copy.fontSize = _main.fontSize;
            if (copy.fontStyle != _main.fontStyle) copy.fontStyle = _main.fontStyle;
            if (copy.alignment != _main.alignment) copy.alignment = _main.alignment;
            if (copy.textWrappingMode != _main.textWrappingMode) copy.textWrappingMode = _main.textWrappingMode;
            if (copy.overflowMode != _main.overflowMode) copy.overflowMode = _main.overflowMode;
            if (copy.enableAutoSizing != _main.enableAutoSizing) copy.enableAutoSizing = _main.enableAutoSizing;
            if (!Mathf.Approximately(copy.fontSizeMin, _main.fontSizeMin)) copy.fontSizeMin = _main.fontSizeMin;
            if (!Mathf.Approximately(copy.fontSizeMax, _main.fontSizeMax)) copy.fontSizeMax = _main.fontSizeMax;
            if (copy.font != _main.font) copy.font = _main.font;

            var target = new Color(color.r, color.g, color.b, color.a * _main.color.a);
            if (copy.color != target) copy.color = target;
            if (!Mathf.Approximately(copy.alpha, _main.alpha)) copy.alpha = _main.alpha * color.a;

            var a = _main.rectTransform;
            var b = copy.rectTransform;
            if (b.anchorMin != a.anchorMin) b.anchorMin = a.anchorMin;
            if (b.anchorMax != a.anchorMax) b.anchorMax = a.anchorMax;
            if (b.pivot != a.pivot) b.pivot = a.pivot;
            if (b.sizeDelta != a.sizeDelta) b.sizeDelta = a.sizeDelta;
            var pos = a.anchoredPosition + offset;
            if (b.anchoredPosition != pos) b.anchoredPosition = pos;
            if (b.localRotation != a.localRotation) b.localRotation = a.localRotation;
            if (b.localScale != a.localScale) b.localScale = a.localScale;
        }
    }
}
