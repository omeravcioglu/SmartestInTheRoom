using System;
using System.Collections;
using Smartest.Core;
using TMPro;
using UnityEngine;

namespace Smartest.UI
{
    /// <summary>
    /// The host's speech bubble. Shows the exact words of the clip that's playing for as long
    /// as it plays, then fades. The reveal quip borrows the same bubble. Players can switch
    /// captions off in Sound settings; the choice is remembered on this machine.
    /// </summary>
    [AddComponentMenu("Smartest/Host Caption")]
    public class HostCaption : MonoBehaviour
    {
        public const string PrefKey = "smartest.captions";

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
            set
            {
                if (value == Enabled) return;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                EnabledChanged?.Invoke(value);
            }
        }

        public static event Action<bool> EnabledChanged;

        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform bubble;
        [SerializeField] private TMP_Text who;
        [SerializeField] private TMP_Text line;
        [SerializeField] private float padLeft = 22f;
        [SerializeField] private float padRight = 28f;
        [SerializeField] private float gap = 16f;
        [SerializeField] private float maxWidth = 1500f;
        [SerializeField] private float lineSize = 25f;
        [SerializeField] private float minHold = 2.2f;

        private Coroutine _routine;

        private void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        /// <summary>A pill-shaped paper bubble, "HOST:" then the line, and a tail pointing down-left.</summary>
        /// <param name="widest">How wide the bubble may grow before a line wraps.</param>
        public static HostCaption Create(Transform parent, float left, float top, float widest = 1500f)
        {
            var root = Ink.Node(parent, "HostCaption");
            root.At(left, top, widest, 82f);
            var cg = root.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = 0f;

            // Pinned by its bottom edge (62 px below the top), so a second line grows upward.
            var bubble = Ink.Box(root, "Bubble", Palette.PaperHi, 3f, 31f);
            var brt = bubble.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 0f);
            brt.anchoredPosition = new Vector2(0f, -62f);
            brt.sizeDelta = new Vector2(600f, 62f);

            var tail = Ink.Icon(root, "Tail", InkSprites.Tail, Palette.PaperHi);
            tail.preserveAspect = false;
            tail.rectTransform.At(34f, 56f, 26f, 26f);
            Ink.Plain(root, "TailJoin", Palette.PaperHi).rectTransform.At(37f, 55f, 20f, 5f);

            var who = Ink.Text(bubble.transform, "Who", "Host:", TypeRole.Display, 30f, Palette.Ink,
                TextAlignmentOptions.MidlineLeft, caps: true, lineHeight: 1f).OneLine();
            var line = Ink.Text(bubble.transform, "Line", "", TypeRole.Body, 25f, Palette.Ink,
                TextAlignmentOptions.MidlineLeft).OneLine();
            line.fontStyle |= FontStyles.Bold | FontStyles.Italic;

            var cap = root.gameObject.AddComponent<HostCaption>();
            cap.group = cg;
            cap.bubble = brt;
            cap.who = who;
            cap.line = line;
            cap.maxWidth = widest;
            return cap;
        }

        private void OnEnable()
        {
            VoiceLines.LineStarted += OnLine;
            EnabledChanged += OnToggled;
        }

        private void OnDisable()
        {
            VoiceLines.LineStarted -= OnLine;
            EnabledChanged -= OnToggled;
            if (group != null) group.alpha = 0f;
            _routine = null;
        }

        private void OnLine(string text, float seconds) => Say(text, seconds);

        private void OnToggled(bool on)
        {
            if (!on) Hide();
        }

        /// <summary>Show a line for about <paramref name="seconds"/>. Ignored while captions are off.</summary>
        public void Say(string text, float seconds)
        {
            if (!Enabled || string.IsNullOrEmpty(text) || !isActiveAndEnabled || group == null) return;
            if (line != null) line.text = "“" + text.Trim() + "”";
            Resize();
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(Mathf.Max(minHold, seconds) + 0.4f));
        }

        public void Hide()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            if (group != null) group.alpha = 0f;
        }

        private IEnumerator Run(float hold)
        {
            float from = group.alpha;
            yield return Tween.To(0.15f, Ease.OutQuad, t => group.alpha = Mathf.Lerp(from, 1f, t));
            yield return new WaitForSecondsRealtime(hold);
            yield return Tween.To(0.35f, Ease.InQuad, t => group.alpha = 1f - t);
            _routine = null;
        }

        /// <summary>The bubble hugs its line; a long line shrinks rather than running off the page.</summary>
        /// <summary>
        /// The bubble hugs its line. A line too long for one row wraps onto two and the bubble
        /// grows upward, away from the seats; anything longer than that shrinks to fit.
        /// </summary>
        private void Resize()
        {
            if (bubble == null || line == null) return;
            float whoWidth = who != null ? who.GetPreferredValues(who.text).x : 0f;

            line.enableAutoSizing = false;
            line.fontSize = lineSize;
            line.textWrappingMode = TextWrappingModes.NoWrap;
            float lineWidth = line.GetPreferredValues(line.text).x;
            float room = maxWidth - padLeft - padRight - gap - whoWidth;
            float height = 62f;
            if (lineWidth > room)
            {
                line.textWrappingMode = TextWrappingModes.Normal;
                line.fontSize = lineSize - 2f;
                float twoLines = line.GetPreferredValues(line.text, room, 0f).y;
                float limit = (lineSize - 2f) * 2.6f;
                if (twoLines > limit)
                {
                    line.enableAutoSizing = true;
                    line.fontSizeMax = lineSize - 2f;
                    line.fontSizeMin = 14f;
                    twoLines = limit;
                }
                lineWidth = room;
                height = Mathf.Max(62f, twoLines + 26f);
            }

            float width = padLeft + whoWidth + gap + lineWidth + padRight;
            bubble.sizeDelta = new Vector2(width, height);

            if (who != null)
            {
                var w = who.rectTransform;
                w.anchorMin = new Vector2(0f, 0f);
                w.anchorMax = new Vector2(0f, 1f);
                w.pivot = new Vector2(0f, 0.5f);
                w.sizeDelta = new Vector2(whoWidth + 2f, 0f);
                w.anchoredPosition = new Vector2(padLeft, 0f);
            }
            var l = line.rectTransform;
            l.anchorMin = new Vector2(0f, 0f);
            l.anchorMax = new Vector2(0f, 1f);
            l.pivot = new Vector2(0f, 0.5f);
            l.sizeDelta = new Vector2(lineWidth + 2f, 0f);
            l.anchoredPosition = new Vector2(padLeft + whoWidth + gap, 0f);
        }
    }
}
