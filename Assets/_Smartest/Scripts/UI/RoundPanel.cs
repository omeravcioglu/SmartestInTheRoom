using System.Collections.Generic;
using Smartest.Core;
using Smartest.Net;
using Smartest.Rounds;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// A social round, laid out like a front page: the round's name as a kicker, the question
    /// as the headline, the rule in THE DEAL box, then the answers — two slabs or a row of
    /// number tiles. The timer is a burst between them. One click locks you in; 1 and 2 on
    /// the keyboard press the slabs. The rule never goes on a button.
    /// </summary>
    public class RoundPanel : Panel
    {
        [Header("Head")]
        [SerializeField] private RectTransform head;
        [SerializeField] private TMP_Text kicker;
        [SerializeField] private TMP_Text stepText;
        [SerializeField] private Image[] stepPips = new Image[0];
        [SerializeField] private TMP_Text potText;
        [SerializeField] private TMP_Text headline;

        [Header("The deal")]
        [SerializeField] private RectTransform dealColumn;
        [SerializeField] private TMP_Text dealText;
        [SerializeField] private TMP_Text pickedText;
        [SerializeField] private Image[] pickedPips = new Image[0];

        [Header("Answers")]
        [SerializeField] private RectTransform slabRow;
        [SerializeField] private AnswerButton buttonA;   // red / yes
        [SerializeField] private AnswerButton buttonB;   // green / no
        [SerializeField] private RectTransform tileRow;
        [SerializeField] private AnswerButton[] numberButtons = new AnswerButton[11]; // index = value 0..10
        [SerializeField] private Burst burst;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private TMP_Text opensIn;

        [Header("Tie-break")]
        [SerializeField] private GameObject tieBanner;
        [SerializeField] private TMP_Text tieSentence;

        private RoundDefinition _def;
        private int _shownOpensIn = -1;
        private int _shownLocked = -1;
        private int _shownTotal = -1;
        private bool _answering;
        private bool _locked;
        private bool _numbers;
        private bool _tie;
        private int _lockedValue = -1;
        private readonly List<AnswerButton> _active = new List<AnswerButton>();

        public const float Width = 1824f;
        public const float Height = 612f;

        protected override void Awake()
        {
            base.Awake();
            if (buttonA != null) buttonA.Clicked += OnAnswerClicked;
            if (buttonB != null) buttonB.Clicked += OnAnswerClicked;
            for (int i = 0; i < numberButtons.Length; i++)
                if (numberButtons[i] != null) numberButtons[i].Clicked += OnAnswerClicked;
        }

        private void Update()
        {
            if (!_answering || _locked || _numbers || _def == null) return;
            int digit = KeyInput.DigitPressed();
            if (digit == 1 && buttonA != null) buttonA.Press();
            else if (digit == 2 && buttonB != null) buttonB.Press();
        }

        // ------------------------------------------------------------------

        /// <param name="tieLine">Who is tied on what, when the last round ended level at the top.</param>
        public void ShowRound(RoundDefinition def, int subRound, bool tieBreak, string tieLine = null)
        {
            _def = def;
            _answering = false;
            _locked = false;
            _lockedValue = -1;
            _active.Clear();
            _numbers = def != null && def.inputType == InputType.Number1to10;
            _tie = tieBreak;

            if (kicker != null) kicker.text = def != null ? def.title : string.Empty;
            if (headline != null) headline.text = def != null ? DealText.ColourWords(def.prompt) : string.Empty;

            // Multi-step rounds (The Lever): which pull this is, and what's in the pot.
            bool steps = def != null && def.subRounds > 1;
            if (stepText != null)
            {
                Ink.BoxOf(stepText).gameObject.SetActive(steps);
                if (steps) stepText.text = $"PULL {subRound} OF {def.subRounds}";
            }
            for (int i = 0; i < stepPips.Length; i++)
            {
                if (stepPips[i] == null) continue;
                bool used = steps && i < def.subRounds;
                stepPips[i].gameObject.SetActive(used);
                if (used) SetPip(stepPips[i], i < subRound, 2.5f);
            }
            if (potText != null)
            {
                Ink.BoxOf(potText).gameObject.SetActive(steps);
                if (steps) potText.text = $"POT: {def.Param(0, 5) * subRound}";
            }

            if (dealText != null)
            {
                string deal = def != null ? DealText.Format(def.ruleText) : string.Empty;
                string sub = def != null ? FillCounts(def.subLine) : string.Empty;
                if (!string.IsNullOrEmpty(sub)) deal += "\n<i>" + DealText.Format(sub) + "</i>";
                dealText.text = deal;
            }

            if (tieBanner != null) tieBanner.SetActive(tieBreak);
            if (tieSentence != null) tieSentence.text = tieLine ?? string.Empty;

            if (slabRow != null) slabRow.gameObject.SetActive(!_numbers && def != null);
            if (tileRow != null) tileRow.gameObject.SetActive(_numbers);

            if (def != null && !_numbers)
            {
                var a = AnswerLook.ForButton(def, 1);
                var b = AnswerLook.ForButton(def, 0);
                buttonA?.Setup(1, a.Word, a, "1");
                buttonB?.Setup(0, b.Word, b, "2");
                if (buttonA != null) _active.Add(buttonA);
                if (buttonB != null) _active.Add(buttonB);
            }
            else if (_numbers)
            {
                for (int v = 0; v < numberButtons.Length; v++)
                {
                    var btn = numberButtons[v];
                    if (btn == null) continue;
                    bool show = v > 0 || def.allowZero;
                    btn.gameObject.SetActive(show);
                    if (!show) continue;
                    btn.Setup(v, v.ToString(), AnswerLook.For(def, v));
                    _active.Add(btn);
                }
            }

            ApplyLayout();
            foreach (var b in _active) if (b != null) b.SetIntro(true);
            if (burst != null) burst.SetVisible(false);
            SetHint(false);
            _shownOpensIn = -1;
            _shownLocked = _shownTotal = -1;
            SetOpensIn(-1f);
        }

        /// <summary>Round intro: the answers are on show but not open yet.</summary>
        public void SetOpensIn(float seconds)
        {
            if (opensIn == null) return;
            bool show = seconds > 0f && !_answering;
            int s = show ? Mathf.CeilToInt(seconds) : 0;
            if (s == _shownOpensIn) return;
            _shownOpensIn = s;
            Ink.BoxOf(opensIn).gameObject.SetActive(show);
            if (show) opensIn.text = $"OPENS IN {s} S";
        }

        public void SetAnswering(bool on)
        {
            _answering = on;
            if (burst != null) burst.SetVisible(on);
            if (on) SetOpensIn(-1f);
            if (!_locked)
            {
                foreach (var b in _active)
                {
                    if (b == null) continue;
                    b.SetIntro(false);
                    b.SetInteractable(on);
                }
                SetHint(false);
            }
        }

        public void Tick(float remaining, float duration)
        {
            if (burst != null && _answering) burst.Set(remaining);
        }

        /// <summary>"5 OF 8 HAVE PICKED" — who's locked in, never what they picked.</summary>
        public void SetPicked(int locked, int total)
        {
            if (locked == _shownLocked && total == _shownTotal) return;
            _shownLocked = locked;
            _shownTotal = total;
            if (pickedText != null) pickedText.text = $"{locked} OF {total} HAVE PICKED";
            for (int i = 0; i < pickedPips.Length; i++)
            {
                if (pickedPips[i] == null) continue;
                pickedPips[i].gameObject.SetActive(i < total);
                SetPip(pickedPips[i], i < locked, 2f);
            }
        }

        // ------------------------------------------------------------------

        private void OnAnswerClicked(AnswerButton btn)
        {
            if (!_answering || _locked || btn == null || _def == null) return;
            if (!_def.IsValidAnswer(btn.Value)) return;
            _locked = true;
            _lockedValue = btn.Value;
            Sounds.Play(Sounds.Kind.LockIn);
            foreach (var b in _active) if (b != null) b.SetLocked(b == btn);
            SetHint(true);
            var local = PlayerData.Local;
            if (local != null) local.SubmitAnswerRpc(btn.Value);
        }

        private void SetHint(bool locked)
        {
            if (hint == null) return;
            var box = Ink.BoxOf(hint);
            box.gameObject.SetActive(!_tie || locked);
            hint.text = locked
                ? (_numbers ? $"LOCKED IN ON {_lockedValue}. NO TAKEBACKS." : "LOCKED IN. NO TAKEBACKS.")
                : "ONE CLICK. NO TAKEBACKS.";
            box.GetComponent<Image>().color = locked ? Palette.Gold : new Color(1f, 1f, 1f, 0f);
        }

        private static void SetPip(Image pip, bool filled, float border)
        {
            pip.sprite = filled ? InkSprites.Box(border) : InkSprites.Frame(border);
            pip.type = Image.Type.Sliced;
            pip.color = Palette.Ink;
        }

        /// <summary>
        /// A round whose rule depends on the head count spells it out in its sub-line:
        /// "{players}" is how many are playing, "{half}" is half of that, rounded down.
        /// </summary>
        private static string FillCounts(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text ?? string.Empty;
            int n = PlayerData.All.Count;
            return text.Replace("{players}", n.ToString()).Replace("{half}", (n / 2).ToString());
        }

        /// <summary>
        /// Numbers take a narrower headline and a lower answer row; a tie-break drops everything
        /// under the TIE. ONE MORE. banner.
        /// </summary>
        private void ApplyLayout()
        {
            float headTop = _tie ? 150f : 8f;
            float headWidth = _numbers ? 1000f : 1100f;
            if (head != null) head.At(0f, headTop, headWidth, 330f);
            if (headline != null)
            {
                float size = _tie ? 96f : _numbers ? 100f : 136f;
                headline.fontSizeMax = size;
                headline.fontSize = size;
                headline.rectTransform.At(0f, 62f, headWidth, _tie ? 170f : 262f);
            }
            if (dealColumn != null) dealColumn.Pin(Width, _tie ? 148f : 0f, new Vector2(1f, 1f));

            if (slabRow != null)
            {
                float top = _tie ? 400f : 346f, h = _tie ? 190f : 210f;
                slabRow.At(0f, top, Width, h);
                if (buttonA != null) buttonA.GetComponent<RectTransform>().At(0f, 0f, (Width - 48f) * 0.5f, h);
                if (buttonB != null) buttonB.GetComponent<RectTransform>().At((Width + 48f) * 0.5f, 0f, (Width - 48f) * 0.5f, h);
            }
            if (tileRow != null)
            {
                int count = 0;
                foreach (var b in numberButtons) if (b != null && b.gameObject.activeSelf) count++;
                float gap = 20f;
                float w = Mathf.Min(160f, (1780f - (count - 1) * gap) / Mathf.Max(1, count));
                float total = count * w + (count - 1) * gap;
                float x = (Width - total) * 0.5f;
                tileRow.At(0f, _tie ? 410f : 360f, Width, 150f);
                // 0 first when it's offered, then 1..10.
                for (int v = 0; v < numberButtons.Length; v++)
                {
                    var b = numberButtons[v];
                    if (b == null || !b.gameObject.activeSelf) continue;
                    b.GetComponent<RectTransform>().At(x, 0f, w, 150f);
                    x += w + gap;
                }
            }
            if (burst != null)
            {
                var rt = (RectTransform)burst.transform;
                if (_numbers) rt.At(1026f, _tie ? 230f : 176f, 170f, 170f);
                else rt.At(822f, BurstTop(_tie ? 292f : 238f, _tie ? 400f : 346f), 180f, 180f);
            }
            if (hint != null) Ink.BoxOf(hint).Pin(Width * 0.5f, _numbers ? 566f : 590f, new Vector2(0.5f, 0.5f));
            if (opensIn != null) Ink.BoxOf(opensIn).Pin(Width * 0.5f, (_tie ? 400f : 346f) + 40f, new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// The burst sits between the slabs, just under the question. A long question whose
        /// last line runs under it pushes it down onto the slab gap instead of covering words.
        /// </summary>
        private float BurstTop(float preferred, float slabTop)
        {
            if (headline == null || head == null || string.IsNullOrEmpty(headline.text)) return preferred;
            headline.ForceMeshUpdate();
            var info = headline.textInfo;
            if (info == null || info.lineCount == 0) return preferred;
            var last = info.lineInfo[info.lineCount - 1];
            var rt = headline.rectTransform;
            // Line extents are local to the headline, around its pivot; bring them to stage space.
            float headTop = -head.anchoredPosition.y - head.sizeDelta.y * 0.5f; // y runs down in stage space
            float headLeft = head.anchoredPosition.x - head.sizeDelta.x * 0.5f;
            float centreX = headLeft + rt.anchoredPosition.x;
            float centreY = headTop - rt.anchoredPosition.y;
            float right = centreX + last.lineExtents.max.x;
            float bottom = centreY - last.descender;
            const float burstLeft = 822f;
            if (right < burstLeft - 10f || bottom < preferred) return preferred;
            return Mathf.Min(bottom + 4f, slabTop + 24f);
        }

        // ------------------------------------------------------------------
        // Construction (SceneBuilder)
        // ------------------------------------------------------------------

        public static RoundPanel Create(Transform stage, GameConfig config)
        {
            var root = Ink.Node(stage, "RoundPanel");
            root.At(0f, 0f, Width, Height);
            var panel = root.gameObject.AddComponent<RoundPanel>();
            panel.ApplyConfig(config);

            // Tie-break banner, under everything else in the head.
            var banner = Ink.Plain(root, "TieBanner", Palette.Ink);
            banner.rectTransform.At(-8f, 6f, Width + 16f, 112f);
            banner.Tilt(-1.2f);
            Ink.Shadow(banner, 8f, Palette.Gold);
            var tieWord = Ink.Text(banner.transform, "Tie", "TIE. ONE MORE.", TypeRole.Display, 104f, Palette.Gold,
                TextAlignmentOptions.MidlineLeft, caps: true, lineHeight: 0.8f).OneLine();
            tieWord.rectTransform.At(40f, 0f, 620f, 112f);
            tieWord.Fit(56f);
            var tieLine = Ink.Text(banner.transform, "Who", "", TypeRole.Body, 26f, Palette.PaperHi,
                TextAlignmentOptions.MidlineLeft);
            tieLine.fontStyle |= FontStyles.Bold;
            tieLine.rectTransform.At(690f, 0f, Width - 740f, 112f);
            tieLine.Fit(18f);
            panel.tieBanner = banner.gameObject;
            panel.tieSentence = tieLine;

            // Head: kicker row and headline.
            var head = Ink.Node(root, "Head");
            head.At(0f, 8f, 1100f, 330f);
            panel.head = head;
            var kickerRow = Ink.Row(head, "KickerRow", 18f);
            kickerRow.Pin(0f, 0f, new Vector2(0f, 1f));
            panel.kicker = Ink.Kicker(kickerRow, "Kicker", "THE BUTTON");
            Ink.Unhug(Ink.BoxOf(panel.kicker));

            var stepBox = Ink.Box(kickerRow, "Step", Palette.PaperHi, 3f);
            stepBox.Tilt(1.5f);
            var stepLayout = stepBox.gameObject.AddComponent<HorizontalLayoutGroup>();
            stepLayout.padding = new RectOffset(12, 12, 6, 6);
            stepLayout.spacing = 10f;
            stepLayout.childAlignment = TextAnchor.MiddleLeft;
            stepLayout.childControlWidth = stepLayout.childControlHeight = true;
            stepLayout.childForceExpandWidth = stepLayout.childForceExpandHeight = false;
            panel.stepText = Ink.Label(stepBox.transform, "Text", "PULL 1 OF 5", 13f, Palette.Ink,
                TextAlignmentOptions.MidlineLeft, 0.12f);
            var pips = Ink.Row(stepBox.transform, "Pips", 4f, TextAnchor.MiddleLeft, hug: false);
            panel.stepPips = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var pip = Ink.Frame(pips, "Pip" + i, Palette.Ink, 2.5f);
                Ink.Size(pip, 14f, 14f);
                panel.stepPips[i] = pip;
            }
            panel.potText = Ink.Sticker(kickerRow, "Pot", "POT: 5", 20f, Palette.Gold, Palette.Ink, -3f);
            Ink.Unhug(Ink.BoxOf(panel.potText));

            panel.headline = Ink.Headline(head, "Headline", "Everyone presses green?", 136f, 6f, 56f);
            panel.headline.rectTransform.At(0f, 62f, 1100f, 262f);

            // The deal, and the picked counter under it.
            var column = Ink.Column(root, "DealColumn", 22f, TextAnchor.UpperRight);
            column.Pin(Width, 0f, new Vector2(1f, 1f));
            panel.dealColumn = column;
            var deal = Ink.Box(column, "Deal", Palette.PaperHi, 4f);
            Ink.Shadow(deal, 10f);
            Ink.Size(deal, 666f, -1f);
            var dealStack = deal.gameObject.AddComponent<VerticalLayoutGroup>();
            dealStack.padding = new RectOffset(26, 26, 22, 24);
            dealStack.spacing = 12f;
            dealStack.childAlignment = TextAnchor.UpperLeft;
            dealStack.childControlWidth = dealStack.childControlHeight = true;
            dealStack.childForceExpandWidth = dealStack.childForceExpandHeight = false;
            DealHeading(deal.transform, "THE DEAL");
            var rule = Ink.Text(deal.transform, "Rule", "", TypeRole.Body, 28f, Palette.Ink,
                TextAlignmentOptions.TopLeft, lineHeight: 1.5f);
            rule.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            TextMarks.Add(rule); // gold and blue behind the points
            panel.dealText = rule;

            // The picked counter sits 16 px in from the column's right edge, as on the sheet.
            var pickedWrap = Ink.Row(column, "PickedWrap", 0f, TextAnchor.MiddleRight, hug: false);
            pickedWrap.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 16, 0, 0);
            var picked = Ink.Frame(pickedWrap, "Picked", Palette.Ink, 3f);
            picked.Tilt(-2.5f);
            var pickedLayout = picked.gameObject.AddComponent<HorizontalLayoutGroup>();
            pickedLayout.padding = new RectOffset(14, 14, 8, 8);
            pickedLayout.spacing = 12f;
            pickedLayout.childAlignment = TextAnchor.MiddleLeft;
            pickedLayout.childControlWidth = pickedLayout.childControlHeight = true;
            pickedLayout.childForceExpandWidth = pickedLayout.childForceExpandHeight = false;
            var pickedPips = Ink.Row(picked.transform, "Pips", 4f, TextAnchor.MiddleLeft, hug: false);
            int max = Mathf.Max(1, config != null ? config.maxPlayers : 8);
            panel.pickedPips = new Image[max];
            for (int i = 0; i < max; i++)
            {
                var pip = Ink.Frame(pickedPips, "Pip" + i, Palette.Ink, 2f);
                Ink.Size(pip, 12f, 12f);
                panel.pickedPips[i] = pip;
            }
            panel.pickedText = Ink.Label(picked.transform, "Text", "0 OF 1 HAVE PICKED", 15f, Palette.Ink,
                TextAlignmentOptions.MidlineLeft, 0.08f);

            // Answers.
            var slabs = Ink.Node(root, "Slabs");
            slabs.At(0f, 346f, Width, 210f);
            panel.slabRow = slabs;
            panel.buttonA = AnswerButton.CreateSlab(slabs, "ButtonA", new Vector2(888f, 210f));
            panel.buttonB = AnswerButton.CreateSlab(slabs, "ButtonB", new Vector2(888f, 210f));

            var tiles = Ink.Node(root, "Tiles");
            tiles.At(0f, 360f, Width, 150f);
            panel.tileRow = tiles;
            panel.numberButtons = new AnswerButton[11];
            for (int v = 0; v <= 10; v++)
                panel.numberButtons[v] = AnswerButton.CreateTile(tiles, "Num" + v, new Vector2(160f, 150f));

            panel.burst = CreateBurst(root, "Timer", 180f, InkSprites.Burst16, 86f, 13f, "SECONDS", "SECONDS!", 9f, 5f);

            panel.hint = Ink.Chip(root, "Hint", "ONE CLICK. NO TAKEBACKS.", TypeRole.Label, 14f,
                new Color(1f, 1f, 1f, 0f), Palette.Ink, 0f, new RectOffset(12, 12, 4, 3), caps: true, tracking: 0.14f);
            panel.opensIn = Ink.Sticker(root, "OpensIn", "OPENS IN 2 S", 26f, Palette.Gold, Palette.Ink, -4f);

            panel.ApplyLayout();
            return panel;
        }

        /// <summary>"THE DEAL" with its 3 px underline, exactly as wide as the words.</summary>
        public static TMP_Text DealHeading(Transform parent, string text, float size = 15f)
        {
            var col = Ink.Node(parent, "Heading");
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;   // the rule stretches to the words' width…
            v.childForceExpandHeight = false;
            var t = Ink.Label(col, "Text", text, size, Palette.Ink, TextAlignmentOptions.TopLeft, 0.14f);
            var line = Ink.Plain(col, "Rule", Palette.Ink);
            Ink.Size(line, 0f, 3f);
            return t;                         // …and the column is only as wide as the words.
        }

        /// <summary>A timer burst: star, number, unit. Used by rounds and minigames.</summary>
        public static Burst CreateBurst(Transform parent, string name, float size, Sprite star, float numberSize,
            float unitSize, string unit, string urgentUnit, float tilt, float tickFrom)
        {
            var root = Ink.Node(parent, name);
            root.sizeDelta = new Vector2(size, size);
            root.Tilt(tilt);
            var img = Ink.Icon(root, "Star", star, Palette.Gold);
            img.rectTransform.Fill();
            var col = Ink.Node(root, "Face");
            col.Fill();
            var value = Ink.Text(col, "Value", "30", TypeRole.Display, numberSize, Palette.Ink,
                TextAlignmentOptions.Center, lineHeight: 0.8f).OneLine();
            value.Fit(numberSize * 0.35f); // "118" still fits inside the star
            var unitText = Ink.Label(col, "Unit", unit, unitSize, Palette.Ink, TextAlignmentOptions.Center, 0.1f);
            float block = numberSize * 0.8f + (string.IsNullOrEmpty(unit) ? 0f : unitSize * 1.2f);
            float top = (size - block) * 0.5f;
            value.rectTransform.At(0f, top, size, numberSize * 0.8f);
            unitText.rectTransform.At(0f, top + numberSize * 0.8f, size, unitSize * 1.2f);

            var b = root.gameObject.AddComponent<Burst>();
            b.Wire(img, value, unitText, unit, urgentUnit, tickFrom);
            return b;
        }
    }
}
