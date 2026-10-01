using Smartest.Core;
using Smartest.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// A level's scoreline, as a tab on the edge of the game panel: a word and how far along
    /// you are. A few things are pips (3 of 5 caught), many are a number (7 / 16), and a run
    /// with nothing to count (a walk, a hold) is a bar. The ink tab is what you've done; the
    /// paper one is what you have left (shots, strokes).
    /// </summary>
    [AddComponentMenu("")]
    public class Scoreline : MonoBehaviour
    {
        public const int MaxPips = 10;
        private const float BarWidth = 160f;
        private const float PunchFor = 0.22f;

        [SerializeField] private TMP_Text word;
        [SerializeField] private RectTransform pipRow;
        [SerializeField] private Image[] pips = new Image[0];
        [SerializeField] private TMP_Text number;
        [SerializeField] private RectTransform bar;
        [SerializeField] private RectTransform barFill;
        [SerializeField] private bool onPaper;

        private MinigameView.Reading _shown;
        private Transform _punched;
        private float _punch;

        private Color On => onPaper ? Palette.Ink : Palette.Gold;
        private Color Off => onPaper ? Palette.Paper2 : Palette.Ink2;

        public void Hide()
        {
            _shown = default;
            gameObject.SetActive(false);
        }

        public void Show(MinigameView.Reading r)
        {
            if (!r.Shown) { Hide(); return; }
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            bool asBar = r.IsBar;
            bool asPips = !asBar && r.Total > 0 && r.Total <= MaxPips;
            if (word != null && word.text != r.Word) word.text = r.Word;
            if (pipRow != null) pipRow.gameObject.SetActive(asPips);
            if (number != null) number.gameObject.SetActive(!asBar && !asPips);
            if (bar != null) bar.gameObject.SetActive(asBar);

            if (asBar)
            {
                if (barFill != null) barFill.sizeDelta = new Vector2(BarWidth * r.Fill, 0f);
            }
            else if (asPips)
            {
                for (int i = 0; i < pips.Length; i++)
                {
                    if (pips[i] == null) continue;
                    pips[i].gameObject.SetActive(i < r.Total);
                    pips[i].color = i < r.Value ? On : Off;
                }
            }
            else if (number != null) number.text = $"{r.Value} / {r.Total}";

            // The pip that just changed (or the number) gives a little kick.
            bool sameCount = _shown.Shown && !asBar && _shown.Word == r.Word && _shown.Total == r.Total;
            if (sameCount && r.Value != _shown.Value)
            {
                int changed = Mathf.Min(r.Value, _shown.Value);
                Punch(asPips && changed < pips.Length && pips[changed] != null ? pips[changed].transform
                    : number != null ? number.transform : null);
            }
            _shown = r;
        }

        private void Punch(Transform t)
        {
            if (_punched != null) _punched.localScale = Vector3.one;
            _punched = t;
            _punch = t != null ? 1f : 0f;
        }

        private void Update()
        {
            if (_punched == null) return;
            _punch = Mathf.Max(0f, _punch - Time.unscaledDeltaTime / PunchFor);
            _punched.localScale = Vector3.one * (1f + 0.6f * _punch);
            if (_punch <= 0f) _punched = null;
        }

        private void OnDisable()
        {
            if (_punched != null) _punched.localScale = Vector3.one;
            _punched = null;
        }

        // ------------------------------------------------------------------
        // Construction (SceneBuilder)
        // ------------------------------------------------------------------

        public static Scoreline Create(Transform parent, string name, bool onPaper)
        {
            var box = Ink.Box(parent, name, onPaper ? Palette.PaperHi : Palette.Ink, onPaper ? 3f : 0f);
            var row = box.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(14, 14, 6, 5);
            row.spacing = 10f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var fit = box.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var s = box.gameObject.AddComponent<Scoreline>();
            s.onPaper = onPaper;
            Color ink = onPaper ? Palette.Ink : Palette.Gold;

            s.word = Ink.Text(box.transform, "Word", "CAUGHT", TypeRole.Sticker, 19f, ink,
                TextAlignmentOptions.MidlineLeft, caps: true, tracking: 0.08f).OneLine();
            Ink.Size(s.word, -1f, 26f);

            s.pipRow = Ink.Row(box.transform, "Pips", 6f, TextAnchor.MiddleLeft, hug: false);
            s.pips = new Image[MaxPips];
            for (int i = 0; i < MaxPips; i++)
            {
                s.pips[i] = Ink.Disc(s.pipRow, "Pip" + i, 18f, 2.5f, s.Off);
                Ink.Size(s.pips[i], 18f, 18f);
            }

            s.number = Ink.Text(box.transform, "Number", "0 / 12", TypeRole.Display, 30f, ink,
                TextAlignmentOptions.MidlineLeft, caps: true, lineHeight: 1f).OneLine();
            Ink.Size(s.number, -1f, 26f);

            var track = Ink.Plain(box.transform, "Bar", s.Off);
            Ink.Size(track, BarWidth, 10f);
            s.bar = track.rectTransform;
            var fill = Ink.Plain(track.transform, "Fill", ink);
            s.barFill = fill.rectTransform;
            s.barFill.anchorMin = new Vector2(0f, 0f);
            s.barFill.anchorMax = new Vector2(0f, 1f);
            s.barFill.pivot = new Vector2(0f, 0.5f);
            s.barFill.anchoredPosition = Vector2.zero;
            s.barFill.sizeDelta = Vector2.zero;
            return s;
        }
    }
}
