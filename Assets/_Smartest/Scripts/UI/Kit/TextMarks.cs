using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// Solid highlighter marks behind words in a TMP text — gold under points gained, blue
    /// under points lost. TMP's own &lt;mark&gt; draws half-transparent over the words, so the
    /// text says <c>&lt;link="gain"&gt;+10&lt;/link&gt;</c> instead and this paints a box behind each
    /// link, on a layer that sits just under the text and follows it around.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    [AddComponentMenu("Smartest/Text Marks")]
    public class TextMarks : MonoBehaviour
    {
        public const string Gain = "gain";
        public const string Loss = "loss";

        [SerializeField] private RectTransform layer;
        [Tooltip("How far the mark reaches past the word, in em — DealText leaves this much room.")]
        [SerializeField] private float padEm = 0.2f;

        private TextMeshProUGUI _text;
        private string _drawn;
        private Vector2 _drawnSize = new Vector2(-1f, -1f);
        private readonly List<Image> _marks = new List<Image>();

        public static TextMarks Add(TextMeshProUGUI text)
        {
            var marks = text.GetComponent<TextMarks>();
            if (marks == null) marks = text.gameObject.AddComponent<TextMarks>();
            if (marks.layer == null)
            {
                var go = new GameObject(text.name + "Marks", typeof(RectTransform));
                go.transform.SetParent(text.transform.parent, false);
                go.transform.SetSiblingIndex(text.transform.GetSiblingIndex()); // just behind the words
                go.AddComponent<LayoutElement>().ignoreLayout = true;
                marks.layer = (RectTransform)go.transform;
            }
            return marks;
        }

        private void OnEnable()
        {
            if (layer != null) layer.gameObject.SetActive(true);
            _drawn = null;
        }

        private void OnDisable()
        {
            if (layer != null) layer.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_text == null) _text = GetComponent<TextMeshProUGUI>();
            if (_text == null || layer == null) return;

            // Follow the text's rect exactly, so text-local coordinates work on the layer too.
            var a = _text.rectTransform;
            if (layer.anchorMin != a.anchorMin) layer.anchorMin = a.anchorMin;
            if (layer.anchorMax != a.anchorMax) layer.anchorMax = a.anchorMax;
            if (layer.pivot != a.pivot) layer.pivot = a.pivot;
            if (layer.sizeDelta != a.sizeDelta) layer.sizeDelta = a.sizeDelta;
            if (layer.anchoredPosition != a.anchoredPosition) layer.anchoredPosition = a.anchoredPosition;
            if (layer.localRotation != a.localRotation) layer.localRotation = a.localRotation;

            var size = a.rect.size;
            if (_text.text == _drawn && size == _drawnSize) return;
            _drawn = _text.text;
            _drawnSize = size;
            Paint();
        }

        private void Paint()
        {
            _text.ForceMeshUpdate();
            var info = _text.textInfo;
            int used = 0;

            for (int l = 0; l < info.linkCount; l++)
            {
                var link = info.linkInfo[l];
                string id = link.GetLinkID();
                Color fill = id == Loss ? Palette.Blue : Palette.Gold;

                // One box per line the link runs across.
                int first = link.linkTextfirstCharacterIndex;
                int last = first + link.linkTextLength - 1;
                int i = first;
                while (i <= last && i < info.characterCount)
                {
                    int line = info.characterInfo[i].lineNumber;
                    float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
                    while (i <= last && i < info.characterCount && info.characterInfo[i].lineNumber == line)
                    {
                        var c = info.characterInfo[i];
                        if (c.isVisible || c.character == ' ')
                        {
                            xMin = Mathf.Min(xMin, c.origin);
                            xMax = Mathf.Max(xMax, c.xAdvance);
                            yMin = Mathf.Min(yMin, c.descender);
                            yMax = Mathf.Max(yMax, c.ascender);
                        }
                        i++;
                    }
                    if (xMax <= xMin) continue;

                    float padX = padEm * _text.fontSize;
                    var mark = Mark(used++);
                    mark.color = fill;
                    var rt = mark.rectTransform;
                    rt.anchorMin = rt.anchorMax = _text.rectTransform.pivot;
                    rt.pivot = new Vector2(0f, 0f);
                    rt.anchoredPosition = new Vector2(xMin - padX, yMin);
                    rt.sizeDelta = new Vector2(xMax - xMin + padX * 2f, yMax - yMin);
                }
            }

            for (int k = used; k < _marks.Count; k++)
                if (_marks[k] != null) _marks[k].gameObject.SetActive(false);
        }

        private Image Mark(int index)
        {
            while (_marks.Count <= index)
            {
                var img = Ink.Plain(layer, "Mark" + _marks.Count, Palette.Gold);
                _marks.Add(img);
            }
            var m = _marks[index];
            m.gameObject.SetActive(true);
            return m;
        }
    }
}
