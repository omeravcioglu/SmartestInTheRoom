using System;
using System.Collections.Generic;
using Smartest.Core;
using Smartest.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Everything around a minigame. Before the first level it's a rule card: the name, the
    /// one-line deal, the controls and the prizes, with a fuse burning down. During a level
    /// it's an info column on the left (level, deal, controls, who's left) and the game panel
    /// on the right, where the minigame draws inside a 1200 × 520 content area. Between levels
    /// a banner says who went out; a player who's out watches under a ribbon.
    /// </summary>
    public class MinigameStage : Panel
    {
        [Header("Rule card")]
        [SerializeField] private GameObject introGroup;
        [SerializeField] private TMP_Text introTitle;
        [SerializeField] private TMP_Text introPrompt;
        [SerializeField] private TMP_Text introDeal;
        [SerializeField] private RectTransform introControls;
        [SerializeField] private RectTransform prizeRows;
        [SerializeField] private TMP_Text howItWorks;
        [SerializeField] private RectTransform fuseFill;
        [SerializeField] private RectTransform fuseSpark;

        [Header("Level")]
        [SerializeField] private GameObject levelGroup;
        [SerializeField] private TMP_Text levelKicker;
        [SerializeField] private TMP_Text levelTitle;
        [SerializeField] private TMP_Text levelDeal;
        [SerializeField] private RectTransform levelControls;
        [SerializeField] private CanvasGroup levelControlsGroup;
        [SerializeField] private TMP_Text aliveText;
        [SerializeField] private RectTransform aliveTokens;
        [SerializeField] private RectTransform gamePanel;
        [SerializeField] private RectTransform contentArea;
        [SerializeField] private GameObject ribbon;
        [SerializeField] private TMP_Text ribbonTitle;
        [SerializeField] private TMP_Text ribbonNote;
        [SerializeField] private GameObject leadIn;
        [SerializeField] private Burst leadInBurst;
        [SerializeField] private TMP_Text leadInLabel;
        [SerializeField] private Burst timer;
        [SerializeField] private TMP_Text statusStamp;
        [SerializeField] private Scoreline progressTab;
        [SerializeField] private Scoreline triesTab;

        [Header("Level banner")]
        [SerializeField] private GameObject banner;
        [SerializeField] private TMP_Text bannerWord;
        [SerializeField] private RectTransform bannerNames;
        [SerializeField] private TMP_Text bannerSentence;
        [SerializeField] private TMP_Text bannerLeft;

        /// <summary>This player's result for the level just played: (failed, metric).</summary>
        public event Action<bool, int> LevelFinished;

        /// <summary>True once this level's GO has happened (the level can no longer be rebuilt).</summary>
        public bool LevelBegun => _begun;

        public const float Width = 1824f;
        public const float Height = 612f;
        private const float PanelLeft = 500f;
        private const float PanelWidth = 1314f;
        private const float PanelHeight = 602f;

        private MinigameView _view;
        private MinigameEntry _entry;
        private CanvasGroup _contentGroup;
        private float _startIn;
        private float _deadline;
        private int _lastCount = -1;
        private bool _begun;
        private bool _armed;
        private bool _playing;

        protected override void Awake()
        {
            base.Awake();
            if (contentArea != null && !contentArea.TryGetComponent(out _contentGroup))
                _contentGroup = contentArea.gameObject.AddComponent<CanvasGroup>();
            ShowLeadIn(false);
            ShowBanner(false);
        }

        /// <summary>
        /// The level is built during the 3-2-1 but must not be seen until GO: memory and
        /// flash games would otherwise get two free seconds of looking (a 0.25 s flash
        /// became 2.25 s), a Bullseye could be aimed at before the clock started, and the
        /// host — who gets the Play phase first — would see it longer than everyone else.
        /// </summary>
        private void SetContentVisible(bool visible)
        {
            if (_contentGroup == null) return;
            _contentGroup.alpha = visible ? 1f : 0f;
            _contentGroup.blocksRaycasts = visible;
            _contentGroup.interactable = visible;
        }

        // ------------------------------------------------------------------
        // Rule card
        // ------------------------------------------------------------------

        /// <summary>The rule card before the first level.</summary>
        public void ShowIntro(MinigameEntry entry, string fallbackTitle, string fallbackRule, int players)
        {
            DestroyView();
            _entry = entry;
            if (introGroup != null) introGroup.SetActive(true);
            if (levelGroup != null) levelGroup.SetActive(false);
            ShowBanner(false);
            ShowLeadIn(false);
            if (timer != null) timer.SetVisible(false);

            string title = entry != null ? entry.Title : fallbackTitle;
            string rule = entry != null ? entry.Rule : fallbackRule;
            if (introTitle != null) introTitle.text = title ?? string.Empty;
            if (introPrompt != null) introPrompt.text = entry != null ? entry.Prompt : string.Empty;
            if (introDeal != null) introDeal.text = DealText.Format(rule);
            BuildControls(introControls, entry != null ? entry.Controls : string.Empty, 28f, true);
            BuildPrizes(players);
            SetFuse(1f);
        }

        /// <summary>The fuse under the rule card burns down with the intro.</summary>
        public void SetFuse(float remaining01)
        {
            float t = Mathf.Clamp01(remaining01);
            const float length = 1808f; // inside the fuse's 3 px border
            if (fuseFill != null)
            {
                fuseFill.anchorMin = new Vector2(0f, 0f);
                fuseFill.anchorMax = new Vector2(0f, 1f);
                fuseFill.pivot = new Vector2(0f, 0.5f);
                fuseFill.anchoredPosition = new Vector2(3f, 0f);
                fuseFill.sizeDelta = new Vector2(t * length, -6f);
            }
            if (fuseSpark != null) fuseSpark.anchoredPosition = new Vector2(3f + t * length, fuseSpark.anchoredPosition.y);
        }

        private void BuildPrizes(int players)
        {
            if (prizeRows == null) return;
            for (int i = prizeRows.childCount - 1; i >= 0; i--) Destroy(prizeRows.GetChild(i).gameObject);

            var cfg = GameBootstrap.ConfigOrDefault;
            if (players <= 1)
            {
                PrizeRow($"{Mathf.Max(1, cfg.minigameSoloLevels)} LEVELS", cfg.minigameSoloClearPoints);
                if (howItWorks != null)
                    howItWorks.text = $"Playing alone: clear {Mathf.Max(1, cfg.minigameSoloLevels)} levels in a row to score. <b>One fail ends it.</b>";
                return;
            }

            var places = cfg.minigamePlacePoints ?? new int[0];
            int paid = Mathf.Min(places.Length, players - 1);
            for (int i = 0; i < paid; i++) PrizeRow(RevealPanel.Ordinal(i + 1), places[i]);
            PrizeRow("LAST", cfg.minigameLastPlacePoints);
            if (howItWorks != null)
                howItWorks.text = "Fail and you're out. Nobody fails? The worst score goes. Everyone fails? Again — harder. <b>Last one standing wins.</b>";
        }

        private void PrizeRow(string place, int points)
        {
            var row = Ink.Row(prizeRows, "Prize", 14f, TextAnchor.MiddleLeft, hug: false);
            Ink.Size(row, 638f, 56f);
            var word = Ink.Text(row, "Place", place, TypeRole.Display, 52f, Palette.Ink, TextAlignmentOptions.MidlineLeft,
                caps: true, lineHeight: 1f).OneLine();
            var wordSize = Ink.Size(word, -1f, 56f);
            wordSize.minWidth = 110f; // short places line up; long ones push the dots along
            // A dotted leader between the place and its points, like a results table.
            var leader = Ink.Node(row, "Leader");
            Ink.Size(leader, -1f, 8f).flexibleWidth = 1f;
            var dots = Ink.Icon(leader, "Dots", InkSprites.Dots, Palette.Ink);
            dots.preserveAspect = false;
            dots.type = Image.Type.Tiled;
            dots.rectTransform.anchorMin = new Vector2(0f, 0f);
            dots.rectTransform.anchorMax = new Vector2(1f, 0f);
            dots.rectTransform.pivot = new Vector2(0.5f, 0f);
            dots.rectTransform.sizeDelta = new Vector2(0f, 4f);
            dots.rectTransform.anchoredPosition = Vector2.zero;
            var chip = Ink.Chip(row, "Points", SeatCard.Format(points), TypeRole.Sticker, 30f, Palette.DeltaFill(points),
                Palette.DeltaText(points), 3f, new RectOffset(12, 12, 4, 2));
            Ink.Unhug(Ink.BoxOf(chip));
            Ink.Size(Ink.BoxOf(chip), 96f, -1f);
        }

        // ------------------------------------------------------------------
        // Levels
        // ------------------------------------------------------------------

        /// <summary>
        /// Build and arm one level. <paramref name="playing"/> is false for players already
        /// knocked out (or sitting out a tie-break) — they see exactly the same level, they
        /// just can't affect it.
        /// </summary>
        public void StartLevel(MinigameEntry entry, int level, int seed, bool playing, int aliveCount,
            float startIn, float levelSeconds, bool tieBreak, bool stillIn = false)
        {
            if (entry == null) return;
            _entry = entry;
            _playing = playing;
            _startIn = Mathf.Max(0f, startIn);
            _deadline = _startIn + levelSeconds;
            _lastCount = -1;
            _begun = false;
            _armed = true;

            if (introGroup != null) introGroup.SetActive(false);
            if (levelGroup != null) levelGroup.SetActive(true);
            ShowBanner(false);

            if (levelKicker != null) levelKicker.text = tieBreak ? "TIE-BREAK" : $"LEVEL {level}";
            FitLevelTitle(entry.Title);
            if (levelDeal != null) levelDeal.text = DealText.Format(entry.Rule);
            BuildControls(levelControls, entry.Controls, 20f, false);
            if (levelControlsGroup != null) levelControlsGroup.alpha = playing ? 1f : 0.35f;
            if (aliveText != null) aliveText.text = level <= 1 && !tieBreak ? $"{aliveCount} PLAYING" : $"{aliveCount} LEFT";

            // Spectators get the ribbon, and the content moves down to make room for it.
            if (ribbon != null) ribbon.SetActive(!playing);
            if (!playing)
            {
                if (ribbonTitle != null) ribbonTitle.text = stillIn ? "SITTING THIS ONE OUT" : "YOU'RE OUT — WATCHING";
                if (ribbonNote != null)
                    ribbonNote.text = stillIn
                        ? "The tie-break is theirs. You're still in."
                        : "Same level as the players still in. Your clicks don't count.";
            }
            if (contentArea != null)
            {
                if (playing) contentArea.At(53f, 37f, 1200f, 520f);
                else contentArea.At(53f, 82f, 1200f, 500f);
            }
            SetStatus(null);
            if (timer != null)
            {
                timer.SetVisible(true);
                timer.Set(levelSeconds);
            }
            ShowLeadIn(true);

            DestroyView();
            SetContentVisible(false);
            if (contentArea != null && entry.ViewType != null)
            {
                // The view lives beside the content area, not inside it, because clearing
                // the area between levels would otherwise destroy the view along with it.
                var go = new GameObject("View", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.sizeDelta = Vector2.zero;
                _view = go.AddComponent(entry.ViewType) as MinigameView;
                if (_view != null)
                {
                    _view.Init(contentArea);
                    _view.Finished += OnViewFinished;
                    _view.ReadingChanged += ShowScoreline;
                    _view.Prepare(level, LevelRng.For(entry.Id, seed, level), levelSeconds, playing);
                }
                else
                {
                    Debug.LogError($"[MinigameStage] {entry.ViewType} is not a MinigameView.");
                    Destroy(go);
                }
            }
        }

        /// <summary>
        /// The title's slot in the info column is as tall as its lines: one line leaves no gap
        /// above THE DEAL, and a title too tall for two lines' room shrinks into it. Shrinking
        /// can land on one line ("Green Light" wraps at full size, and a few points smaller it
        /// fits on one), so the slot is measured at the size TMP settles on, not assumed.
        /// </summary>
        private void FitLevelTitle(string title)
        {
            if (levelTitle == null) return;
            const float size = 104f, twoLines = 184f;
            levelTitle.enableAutoSizing = false;
            levelTitle.fontSize = size;
            levelTitle.text = title;
            float height = levelTitle.GetPreferredValues(title, 460f, 0f).y;
            if (height > twoLines + 4f)
            {
                // Let TMP shrink it into two lines' room, keep the size it chose, and measure
                // the lines at that size.
                levelTitle.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, twoLines);
                levelTitle.enableAutoSizing = true;
                levelTitle.fontSizeMax = size;
                levelTitle.fontSizeMin = 44f;
                levelTitle.ForceMeshUpdate(true);
                float fitted = levelTitle.fontSize;
                levelTitle.enableAutoSizing = false;
                levelTitle.fontSize = fitted;
                levelTitle.ForceMeshUpdate(true);
                var info = levelTitle.textInfo;
                height = info.lineCount > 0
                    ? Mathf.Min(twoLines, info.lineInfo[0].ascender - info.lineInfo[info.lineCount - 1].descender)
                    : twoLines;
            }
            var slot = levelTitle.GetComponent<LayoutElement>();
            if (slot != null) slot.preferredHeight = slot.minHeight = Mathf.Ceil(height);
        }

        /// <summary>Who's still in, as a stack of little tokens beside the count.</summary>
        public void SetAlive(IReadOnlyList<(string mono, bool you)> players)
        {
            if (aliveTokens == null) return;
            for (int i = aliveTokens.childCount - 1; i >= 0; i--) Destroy(aliveTokens.GetChild(i).gameObject);
            if (players == null) return;
            for (int i = 0; i < players.Count; i++)
            {
                var mono = Ink.Token(aliveTokens, "Alive" + i, 36f, 2.5f, players[i].you ? Palette.Gold : Palette.Paper2,
                    players[i].mono, 15f, halftone: false);
                ((RectTransform)mono.transform.parent).At(i * 28f, 0f, 36f, 36f);
            }
        }

        /// <summary>
        /// Between levels. <paramref name="outcome"/> is GameState's LevelOutcome; the names are
        /// the players who just went out.
        /// </summary>
        public void ShowLevelResult(byte outcome, string levelLine, IReadOnlyList<(string name, string mono)> outNow,
            int left, bool solo)
        {
            _armed = false;
            SetContentVisible(true); // whatever the level ended on stays readable, under the banner
            if (_view != null) _view.Freeze();
            ShowLeadIn(false);
            if (timer != null) timer.SetVisible(false);
            SetStatus(null);
            if (levelKicker != null && _entry != null && !levelKicker.text.EndsWith("RESULT"))
                levelKicker.text += " · RESULT";

            string word, sentence = null;
            bool names = false;
            if (solo)
            {
                word = "CLEARED!";
                sentence = string.IsNullOrEmpty(levelLine) ? "On to the next one." : levelLine;
            }
            else if (outcome == 2) // everyone failed
            {
                word = "AGAIN — HARDER!";
                sentence = "Everyone failed. Same players, one notch harder.";
            }
            else if (outcome == 3) // dead heat
            {
                word = "DEAD HEAT!";
                string who = levelLine ?? string.Empty;
                if (who.StartsWith("Dead heat. ")) who = who.Substring("Dead heat. ".Length);
                sentence = who + "\nEveryone else sits this one out.";
            }
            else if (outNow != null && outNow.Count > 0)
            {
                word = "OUT!";
                names = true;
            }
            else
            {
                word = "NEXT LEVEL!";
                sentence = string.IsNullOrEmpty(levelLine) ? "Everyone made it." : levelLine;
            }

            if (bannerWord != null) bannerWord.text = word;
            if (bannerSentence != null)
            {
                bannerSentence.gameObject.SetActive(!names);
                bannerSentence.text = sentence ?? string.Empty;
            }
            if (bannerNames != null)
            {
                bannerNames.gameObject.SetActive(names);
                for (int i = bannerNames.childCount - 1; i >= 0; i--) Destroy(bannerNames.GetChild(i).gameObject);
                if (names)
                {
                    int shown = Mathf.Min(outNow.Count, 3);
                    for (int i = 0; i < shown; i++)
                    {
                        var row = Ink.Row(bannerNames, "Out" + i, 16f, TextAnchor.MiddleLeft, hug: false);
                        var mono = Ink.Token(row, "Token", 60f, 3f, Palette.Paper2, outNow[i].mono, 26f);
                        Ink.Size(mono.transform.parent, 60f, 60f);
                        var n = Ink.Text(row, "Name", outNow[i].name, TypeRole.Display, shown > 2 ? 56f : 76f,
                            Palette.PaperHi, TextAlignmentOptions.MidlineLeft, lineHeight: 0.9f).OneLine();
                        n.overflowMode = TextOverflowModes.Ellipsis;
                    }
                    if (outNow.Count > shown)
                        Ink.Text(bannerNames, "More", $"+{outNow.Count - shown} more", TypeRole.Body, 26f,
                            Palette.PaperHi, TextAlignmentOptions.MidlineLeft);
                }
            }
            if (bannerLeft != null)
            {
                Ink.BoxOf(bannerLeft).gameObject.SetActive(!solo && left > 0 && outcome != 2);
                bannerLeft.text = $"{left} LEFT";
            }
            ShowBanner(true);
        }

        public void EndMinigame()
        {
            _armed = false;
            DestroyView();
            SetContentVisible(true);
            ShowLeadIn(false);
            ShowBanner(false);
            if (timer != null) timer.SetVisible(false);
            SetStatus(null);
        }

        // ------------------------------------------------------------------

        private void Update()
        {
            if (!_armed) return;

            // Lead-in: the same "3, 2, 1" on every machine, ending at the same instant.
            if (!_begun)
            {
                _startIn -= Time.unscaledDeltaTime;
                _deadline -= Time.unscaledDeltaTime;
                int n = Mathf.CeilToInt(_startIn);
                if (leadInBurst != null) leadInBurst.SetDigit(n > 0 ? n.ToString() : "GO");
                if (n != _lastCount)
                {
                    _lastCount = n;
                    if (n > 0) Sounds.Play(Sounds.Kind.Tick);
                }
                if (_startIn <= 0f)
                {
                    _begun = true;
                    Sounds.Play(Sounds.Kind.Go);
                    ShowLeadIn(false);
                    SetContentVisible(true);
                    ShowScoreline();
                    if (_view != null) _view.Begin();
                }
                return;
            }

            _deadline -= Time.unscaledDeltaTime;
            if (timer != null) timer.Set(Mathf.Max(0f, _deadline));

            if (_deadline <= 0f)
            {
                _armed = false;
                if (_view != null && !_view.IsDone) _view.TimeOut();
            }
        }

        private void OnViewFinished(bool failed, int metric)
        {
            if (!_playing) return;
            SetStatus(failed ? "OUT" : "DONE — WAITING");
            LevelFinished?.Invoke(failed, metric);
        }

        private void SetStatus(string text)
        {
            if (statusStamp == null) return;
            var box = Ink.BoxOf(statusStamp);
            box.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text)) statusStamp.text = text;
        }

        private void ShowLeadIn(bool on)
        {
            if (leadIn != null) leadIn.SetActive(on);
        }

        private void ShowBanner(bool on)
        {
            if (banner != null) banner.SetActive(on);
        }

        private void DestroyView()
        {
            if (_view != null)
            {
                _view.Finished -= OnViewFinished;
                _view.ReadingChanged -= ShowScoreline;
                _view.Teardown();
                Destroy(_view.gameObject);
                _view = null;
            }
            if (contentArea != null) UiKit.Clear(contentArea);
            if (progressTab != null) progressTab.Hide();
            if (triesTab != null) triesTab.Hide();
        }

        /// <summary>
        /// The level's own scoreline on the panel's tabs. Like the level itself it stays out of
        /// sight until GO (a count of boxes to remember is part of the puzzle), and someone
        /// watching gets none: the level they see is being played for them.
        /// </summary>
        private void ShowScoreline()
        {
            if (!_begun || !_playing || _view == null) return;
            if (progressTab != null) progressTab.Show(_view.Progressed);
            if (triesTab != null) triesTab.Show(_view.TriesLeft);
        }

        /// <summary>
        /// Controls from the registry as keycaps: "W A S D" is four caps, "1 / 2" two, "SPACE"
        /// one; anything you click with gets the mouse too.
        /// </summary>
        private void BuildControls(RectTransform row, string controls, float size, bool withNote)
        {
            if (row == null) return;
            for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
            if (string.IsNullOrWhiteSpace(controls)) return;

            string c = controls.Trim().ToUpperInvariant();
            bool mouse = c.Contains("CLICK") || c.Contains("MOUSE") || c.Contains("DRAG");
            if (mouse)
            {
                var icon = Ink.Icon(row, "Mouse", InkSprites.Mouse, Color.white);
                Ink.Size(icon, size * 1.55f, size * 2.1f);
            }

            var keys = new List<string>();
            if (c.Contains("/")) foreach (var k in c.Split('/')) keys.Add(k.Trim());
            else if (c.Contains("+")) foreach (var k in c.Split('+')) keys.Add(k.Trim());
            else
            {
                var words = c.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                bool letters = words.Length > 1;
                foreach (var w in words) if (w.Length != 1) letters = false;
                if (letters) keys.AddRange(words);
                else keys.Add(c);
            }
            bool keyboard = false;
            foreach (var k in keys)
            {
                if (string.IsNullOrEmpty(k)) continue;
                if (!k.Contains("CLICK") && !k.Contains("MOUSE") && !k.Contains("DRAG")) keyboard = true;
                var cap = Ink.Key(row, "Key", k, size);
                Ink.Unhug(Ink.BoxOf(cap));
            }

            if (withNote)
            {
                string note = mouse && keyboard ? "Mouse or keyboard" : mouse ? "Mouse only" : c.Contains("TYPE") ? "Keyboard" : "Keyboard only";
                var t = Ink.Text(row, "Note", note, TypeRole.Body, 22f, Palette.Ink2, TextAlignmentOptions.MidlineLeft).OneLine();
                t.fontStyle |= FontStyles.Bold;
            }
        }

        // ------------------------------------------------------------------
        // Construction (SceneBuilder)
        // ------------------------------------------------------------------

        public static MinigameStage Create(Transform stage, GameConfig config)
        {
            var root = Ink.Node(stage, "MinigameStage");
            root.At(0f, 0f, Width, Height);
            var s = root.gameObject.AddComponent<MinigameStage>();
            s.ApplyConfig(config);
            s.BuildIntro(root);
            s.BuildLevel(root);
            return s;
        }

        private void BuildIntro(RectTransform root)
        {
            var g = Ink.Node(root, "RuleCard");
            g.Fill();
            introGroup = g.gameObject;

            var kick = Ink.Chip(g, "Kicker", "MINIGAME!", TypeRole.Display, 42f, Palette.Ink, Palette.Gold, 0f,
                new RectOffset(18, 18, 6, 4));
            Ink.BoxOf(kick).Tilt(-2f);
            Ink.BoxOf(kick).Pin(0f, 8f, new Vector2(0f, 1f));

            introTitle = Ink.Headline(g, "Title", "Memory Boxes", 190f, 8f, 80f).OneLine();
            introTitle.rectTransform.At(0f, 70f, 1080f, 170f);
            introPrompt = Ink.Text(g, "Prompt", "Remember the lit boxes.", TypeRole.Body, 36f, Palette.Ink,
                TextAlignmentOptions.TopLeft).OneLine();
            introPrompt.fontStyle |= FontStyles.Italic | FontStyles.Bold;
            introPrompt.rectTransform.At(0f, 246f, 1080f, 50f);
            introPrompt.Fit(24f);

            var deal = Ink.Box(g, "Deal", Palette.PaperHi, 4f);
            Ink.Shadow(deal, 10f);
            deal.rectTransform.Pin(0f, 312f, new Vector2(0f, 1f));
            var stack = deal.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(24, 24, 18, 20);
            stack.spacing = 8f;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = stack.childForceExpandHeight = false;
            var fit = deal.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            deal.rectTransform.sizeDelta = new Vector2(1040f, 150f);
            Ink.Label(deal.transform, "Heading", "THE DEAL", 14f);
            introDeal = Ink.Text(deal.transform, "Rule", "", TypeRole.Body, 30f, Palette.Ink, TextAlignmentOptions.TopLeft,
                lineHeight: 1.45f);
            introDeal.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            TextMarks.Add((TextMeshProUGUI)introDeal);

            introControls = Ink.Row(g, "Controls", 16f);
            introControls.Pin(0f, 505f, new Vector2(0f, 0.5f));

            // The prizes.
            var prizes = Ink.Box(g, "Prizes", Palette.PaperHi, 4f);
            Ink.Shadow(prizes, 10f);
            prizes.rectTransform.Pin(1120f, 26f, new Vector2(0f, 1f));
            prizes.rectTransform.sizeDelta = new Vector2(694f, 400f);
            var pStack = prizes.gameObject.AddComponent<VerticalLayoutGroup>();
            pStack.padding = new RectOffset(28, 28, 22, 26);
            pStack.spacing = 10f;
            pStack.childControlWidth = pStack.childControlHeight = true;
            pStack.childForceExpandWidth = pStack.childForceExpandHeight = false;
            var pFit = prizes.gameObject.AddComponent<ContentSizeFitter>();
            pFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            RoundPanel.DealHeading(prizes.transform, "THE PRIZES");
            prizeRows = Ink.Column(prizes.transform, "Rows", 10f, TextAnchor.UpperLeft, hug: false);
            var rule = Ink.Plain(prizes.transform, "Rule", Palette.Ink);
            Ink.Size(rule, 638f, 3f);
            Ink.Label(prizes.transform, "How", "HOW IT WORKS", 15f);
            howItWorks = Ink.Text(prizes.transform, "HowText", "", TypeRole.Body, 22f, Palette.Ink,
                TextAlignmentOptions.TopLeft, lineHeight: 1.5f);
            howItWorks.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // The fuse.
            var fuse = Ink.Box(g, "Fuse", Palette.PaperHi, 3f);
            fuse.rectTransform.At(0f, 588f, 1814f, 18f);
            var fill = Ink.Plain(fuse.transform, "Burn", Palette.Gold);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            fuseFill = fill.rectTransform;
            var spark = Ink.Icon(g, "Spark", InkSprites.Spark, Palette.Gold);
            spark.rectTransform.anchorMin = spark.rectTransform.anchorMax = new Vector2(0f, 1f);
            spark.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            spark.rectTransform.sizeDelta = new Vector2(50f, 50f);
            spark.rectTransform.anchoredPosition = new Vector2(1811f, -597f);
            fuseSpark = spark.rectTransform;
        }

        private void BuildLevel(RectTransform root)
        {
            var g = Ink.Node(root, "Level");
            g.Fill();
            levelGroup = g.gameObject;

            // Info column.
            var col = Ink.Column(g, "Info", 16f, TextAnchor.UpperLeft, hug: false);
            col.At(0f, 8f, 460f, 600f);
            levelKicker = Ink.Kicker(col, "Kicker", "LEVEL 1");
            Ink.Unhug(Ink.BoxOf(levelKicker));
            levelTitle = Ink.Headline(col, "Title", "Memory Boxes", 104f, 5f, 44f);
            Ink.Size(levelTitle, 460f, 184f);

            var deal = Ink.Box(col, "Deal", Palette.PaperHi, 3f);
            Ink.Shadow(deal, 6f);
            Ink.Size(deal, 440f, -1f);
            var stack = deal.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(16, 16, 12, 14);
            stack.spacing = 6f;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = stack.childForceExpandHeight = false;
            Ink.Label(deal.transform, "Heading", "THE DEAL", 12f);
            levelDeal = Ink.Text(deal.transform, "Rule", "", TypeRole.Body, 22f, Palette.Ink, TextAlignmentOptions.TopLeft,
                lineHeight: 1.4f);
            levelDeal.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            TextMarks.Add((TextMeshProUGUI)levelDeal);

            levelControls = Ink.Row(col, "Controls", 12f, TextAnchor.MiddleLeft, hug: false);
            levelControlsGroup = levelControls.gameObject.AddComponent<CanvasGroup>();

            var alive = Ink.Row(col, "Alive", 14f, TextAnchor.MiddleLeft, hug: false);
            aliveText = Ink.Chip(alive, "Count", "4 LEFT", TypeRole.Sticker, 20f, Palette.Ink, Palette.Gold, 0f,
                new RectOffset(12, 12, 4, 3), caps: true, tracking: 0.06f);
            Ink.Unhug(Ink.BoxOf(aliveText));
            aliveTokens = Ink.Node(alive, "Tokens");
            Ink.Size(aliveTokens, 260f, 36f);

            // The game panel.
            var panelBox = Ink.Box(g, "GamePanel", Palette.PaperHi, 4f);
            panelBox.rectTransform.At(PanelLeft, 0f, PanelWidth, PanelHeight);
            Ink.Shadow(panelBox, 10f);
            gamePanel = panelBox.rectTransform;
            var clip = Ink.Node(panelBox.transform, "Clip");
            clip.Fill(4f, 4f, 4f, 4f);
            clip.gameObject.AddComponent<RectMask2D>();

            var content = Ink.Node(clip, "Content");
            content.At(49f, 33f, 1200f, 520f);
            contentArea = content;

            // Spectator ribbon.
            var rib = Ink.Plain(clip, "Ribbon", Palette.Ink);
            rib.rectTransform.At(0f, 0f, PanelWidth - 8f, 70f);
            var ribRow = Ink.Row(rib.transform, "Row", 22f, TextAnchor.MiddleCenter, hug: false);
            ribRow.Fill();
            ribbonTitle = Ink.Text(ribRow, "Title", "YOU'RE OUT — WATCHING", TypeRole.Display, 48f, Palette.Gold,
                TextAlignmentOptions.MidlineLeft, caps: true, lineHeight: 1f).OneLine();
            ribbonNote = Ink.Text(ribRow, "Note", "", TypeRole.Body, 20f, Palette.PaperHi, TextAlignmentOptions.MidlineLeft).OneLine();
            ribbon = rib.gameObject;

            // Lead-in: the big 3 · 2 · 1 over the (hidden) level.
            var lead = Ink.Node(clip, "LeadIn");
            lead.Fill();
            leadIn = lead.gameObject;
            leadInBurst = RoundPanel.CreateBurst(lead, "Burst", 370f, InkSprites.Burst18, 250f, 12f, "", "", -6f, 0f);
            ((RectTransform)leadInBurst.transform).At(468f - 4f, 92f - 4f, 370f, 370f);
            leadInLabel = Ink.Chip(lead, "Ready", "GET READY", TypeRole.Sticker, 34f, Palette.Ink, Palette.Gold, 0f,
                new RectOffset(20, 20, 8, 6), caps: true, tracking: 0.08f);
            Ink.BoxOf(leadInLabel).Tilt(3f);
            Ink.BoxOf(leadInLabel).Pin(653f - 4f, 510f - 4f, new Vector2(0.5f, 0.5f));

            // Finished-early stamp.
            statusStamp = Ink.Stamp(clip, "Status", "DONE — WAITING", 30f, -4f, 4f, Palette.Ink, Palette.PaperHi);
            Ink.BoxOf(statusStamp).Pin((PanelWidth - 8f) * 0.5f, PanelHeight - 60f, new Vector2(0.5f, 0.5f));

            // Level banner, over the frozen level.
            var ban = Ink.Plain(clip, "Banner", Palette.Ink);
            ban.rectTransform.At(26f, 172f, 1246f, 230f);
            ban.Tilt(-2f);
            Ink.Shadow(ban, 10f, Palette.Gold);
            banner = ban.gameObject;
            var banRow = Ink.Row(ban.transform, "Row", 34f, TextAnchor.MiddleLeft, hug: false);
            banRow.Fill(44f, 0f, 44f, 0f);
            bannerWord = Ink.Text(banRow, "Word", "OUT!", TypeRole.Display, 150f, Palette.Gold,
                TextAlignmentOptions.MidlineLeft, caps: true, lineHeight: 0.8f).OneLine();
            var divider = Ink.Plain(banRow, "Divider", Palette.PaperHi);
            Ink.Size(divider, 4f, 150f);
            bannerNames = Ink.Column(banRow, "Names", 12f, TextAnchor.MiddleLeft, hug: false);
            bannerSentence = Ink.Text(banRow, "Sentence", "", TypeRole.Body, 28f, Palette.PaperHi,
                TextAlignmentOptions.MidlineLeft, lineHeight: 1.35f);
            bannerSentence.fontStyle |= FontStyles.Bold;
            bannerSentence.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            bannerLeft = Ink.Sticker(ban.transform, "Left", "3 LEFT", 32f, Palette.Gold, Palette.Ink, 5f, 3f);
            Ink.BoxOf(bannerLeft).Pin(1246f - 40f, 115f, new Vector2(1f, 0.5f));

            // The level's scoreline: tabs riding the panel's top edge, what you've done on the
            // left and what you have left on the right.
            progressTab = Scoreline.Create(g, "ProgressTab", onPaper: false);
            progressTab.Pin(PanelLeft + 30f, 9f, new Vector2(0f, 0.5f)).Tilt(-1.5f);
            progressTab.gameObject.SetActive(false);
            triesTab = Scoreline.Create(g, "TriesTab", onPaper: true);
            triesTab.Pin(PanelLeft + PanelWidth - 30f, 9f, new Vector2(1f, 0.5f)).Tilt(1.5f);
            triesTab.gameObject.SetActive(false);

            // The level clock.
            // Silent: some games (Keep the Beat) are played by ear.
            timer = RoundPanel.CreateBurst(g, "Timer", 150f, InkSprites.Burst16, 72f, 11f, "SEC", "SEC", 9f, 0f);
            ((RectTransform)timer.transform).At(1683f, 491f, 150f, 150f);
        }
    }
}
