using Smartest.Core;
using Smartest.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// Drives the Menu scene: the front page (name, HOST, JOIN) -> the lobby, or -> the join
    /// card (over the front page) -> the lobby. All references are wired by
    /// Tools > Smartest > Build Scenes.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private Panel mainPanel;
        [SerializeField] private Panel joinPanel;
        [SerializeField] private LobbyUI lobby;

        [Header("Main menu")]
        [SerializeField] private TMP_InputField nameField;
        [SerializeField] private TMP_Text nameCounter;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private TMP_Text mainStatus;

        [Header("Join panel")]
        [SerializeField] private TMP_InputField codeField;
        [SerializeField] private Button joinGoButton;
        [SerializeField] private Button joinBackButton;
        [SerializeField] private TMP_Text joinStatus;

        private Panel _current;
        private bool _busy;

        private NetSession Net => NetSession.Instance;

        private void Start()
        {
            if (nameField != null)
            {
                nameField.characterLimit = PlayerData.MaxNameLength;
                nameField.text = Net != null ? Net.LocalPlayerName : string.Empty;
                nameField.onValueChanged.AddListener(OnNameChanged);
                nameField.onSubmit.AddListener(_ => OnNameSubmitted());
            }
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
            if (settingsButton != null && settingsPanel != null) settingsButton.onClick.AddListener(settingsPanel.Toggle);

            if (codeField != null)
            {
                codeField.characterLimit = 24; // long enough for an IPv4 address
                codeField.onValueChanged.AddListener(OnCodeChanged);
                codeField.onSubmit.AddListener(_ => OnJoinGoClicked());
            }
            if (joinGoButton != null) joinGoButton.onClick.AddListener(OnJoinGoClicked);
            if (joinBackButton != null) joinBackButton.onClick.AddListener(() => GoTo(mainPanel));

            if (lobby != null)
            {
                lobby.StartRequested += OnStartClicked;
                lobby.LeaveRequested += OnLeaveClicked;
            }

            if (Net != null)
            {
                Net.Status += OnNetStatus;
                Net.SessionEnded += OnSessionEnded;
            }

            // Back from a game while still connected -> straight to the lobby.
            if (Net != null && Net.IsConnected)
            {
                if (mainPanel != null) mainPanel.HideInstant();
                if (joinPanel != null) joinPanel.HideInstant();
                if (lobby != null) lobby.ShowInstant();
                _current = lobby;
            }
            else
            {
                if (mainPanel != null) mainPanel.ShowInstant();
                if (joinPanel != null) joinPanel.HideInstant();
                if (lobby != null) lobby.HideInstant();
                _current = mainPanel;
            }

            if (!string.IsNullOrEmpty(NetSession.PendingMenuMessage))
            {
                // Back in the lobby with news (everyone else left mid-match): say it on the
                // lobby itself, since the main panel's status line is hidden behind it.
                if (_current == lobby && lobby != null) lobby.ShowNote(NetSession.PendingMenuMessage);
                else SetStatus(NetSession.PendingMenuMessage);
                NetSession.PendingMenuMessage = null;
            }
            else
            {
                SetStatus(string.Empty);
            }

            RefreshMainButtons();
            RefreshCounter();
            VoiceLines.Play(VoiceKeys.MenuWelcome);
        }

        private void OnDestroy()
        {
            if (Net != null)
            {
                Net.Status -= OnNetStatus;
                Net.SessionEnded -= OnSessionEnded;
            }
            if (lobby != null)
            {
                lobby.StartRequested -= OnStartClicked;
                lobby.LeaveRequested -= OnLeaveClicked;
            }
        }

        private void Update()
        {
            if (_busy) return;
            if (KeyInput.EscapePressed() && _current == joinPanel)
                GoTo(mainPanel);
        }

        // ------------------------------------------------------------------
        // Main menu
        // ------------------------------------------------------------------

        private void OnNameChanged(string value)
        {
            if (Net != null) Net.LocalPlayerName = value;
            RefreshMainButtons();
            RefreshCounter();
        }

        private void RefreshCounter()
        {
            if (nameCounter == null || nameField == null) return;
            nameCounter.text = $"{nameField.text.Length}/{PlayerData.MaxNameLength}";
        }

        private void OnNameSubmitted()
        {
            if (nameField != null) nameField.DeactivateInputField();
        }

        private bool HasValidName => !string.IsNullOrEmpty(PlayerData.Sanitize(nameField != null ? nameField.text : string.Empty));

        private void RefreshMainButtons()
        {
            bool ok = HasValidName && !_busy;
            if (hostButton != null) hostButton.interactable = ok;
            if (joinButton != null) joinButton.interactable = ok;
            if (joinGoButton != null) joinGoButton.interactable = !_busy;
            if (joinBackButton != null) joinBackButton.interactable = !_busy;
        }

        private async void OnHostClicked()
        {
            if (_busy || !HasValidName || Net == null) return;
            VoiceLines.Play(VoiceKeys.MenuHostPressed);
            SetBusy(true);
            SetStatus("Creating lobby…");
            bool ok = await Net.HostAsync();
            if (!this) return; // scene changed while we were waiting
            SetBusy(false);
            if (ok)
            {
                SetStatus(string.Empty);
                GoTo(lobby);
            }
        }

        private void OnJoinClicked()
        {
            if (_busy || !HasValidName) return;
            VoiceLines.Play(VoiceKeys.MenuJoinPressed);
            SetStatus(string.Empty);
            GoTo(joinPanel);
            if (codeField != null)
            {
                codeField.text = string.Empty;
                codeField.ActivateInputField();
            }
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------
        // Join card
        // ------------------------------------------------------------------

        private void OnCodeChanged(string value)
        {
            string upper = (value ?? string.Empty).ToUpperInvariant();
            if (upper != value && codeField != null) codeField.SetTextWithoutNotify(upper);
        }

        private async void OnJoinGoClicked()
        {
            if (_busy || Net == null) return;
            string code = codeField != null ? codeField.text : string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                SetStatus("Enter the code from the host.");
                return;
            }
            SetBusy(true);
            SetStatus("Joining…");
            bool ok = await Net.JoinAsync(code);
            if (!this) return; // scene changed while we were waiting
            SetBusy(false);
            if (ok)
            {
                SetStatus(string.Empty);
                GoTo(lobby);
            }
        }

        // ------------------------------------------------------------------
        // Lobby
        // ------------------------------------------------------------------

        private void OnStartClicked()
        {
            if (Net == null || !Net.IsHost) return;
            if (PlayerData.All.Count == 1) VoiceLines.Play(VoiceKeys.LobbySoloStart);
            Net.StartGame();
        }

        private async void OnLeaveClicked()
        {
            if (_busy || Net == null) return;
            SetBusy(true);
            await Net.LeaveAsync();
            if (!this) return;
            SetBusy(false);
            SetStatus(string.Empty);
            GoTo(mainPanel);
        }

        private void OnSessionEnded(string reason)
        {
            SetBusy(false);
            GoTo(mainPanel);
            SetStatus(reason);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>The join card opens over the front page; everything else replaces it.</summary>
        private void GoTo(Panel target)
        {
            if (target == null) return;
            bool keepMain = target == joinPanel;
            if (mainPanel != null && mainPanel != target && !keepMain && mainPanel.IsShown) mainPanel.Hide();
            if (keepMain && mainPanel != null && !mainPanel.IsShown) mainPanel.Show();
            if (joinPanel != null && joinPanel != target && joinPanel.IsShown) joinPanel.Hide();
            if (lobby != null && lobby != target && lobby.IsShown) lobby.Hide();
            if (!target.IsShown) target.Show();
            _current = target;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            RefreshMainButtons();
            if (lobby != null) lobby.Refresh();
        }

        private void OnNetStatus(string message) => SetStatus(message);

        private void SetStatus(string message)
        {
            // Show the message on whichever panel is in front.
            if (_current == joinPanel)
            {
                SetText(joinStatus, message);
            }
            else
            {
                SetText(mainStatus, message);
                if (_current != joinPanel) SetText(joinStatus, string.Empty);
            }
        }

        /// <summary>Status lines live on stamps; an empty message hides the stamp.</summary>
        private static void SetText(TMP_Text t, string message)
        {
            if (t == null) return;
            t.text = message ?? string.Empty;
            var box = t.transform.parent != null ? t.transform.parent.GetComponent<Image>() : null;
            if (box != null && box.GetComponent<ContentSizeFitter>() != null)
                box.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        // ------------------------------------------------------------------
        // Construction (SceneBuilder)
        // ------------------------------------------------------------------

        /// <summary>The front page and the join card, and the controller that runs them.</summary>
        public static MenuUI Create(Transform frame, GameConfig config, SettingsPanel settings, LobbyUI lobby,
            int socialRounds, int minigames)
        {
            var menu = new GameObject("MenuController").AddComponent<MenuUI>();
            menu.lobby = lobby;
            menu.mainPanel = BuildFront(frame, config, settings, menu, socialRounds, minigames);
            menu.joinPanel = BuildJoin(frame, config, menu);
            // The join card must sit above the front page; the lobby above both is fine either way.
            menu.joinPanel.transform.SetSiblingIndex(menu.mainPanel.transform.GetSiblingIndex() + 1);
            return menu;
        }

        private static Panel BuildFront(Transform frame, GameConfig config, SettingsPanel settings, MenuUI menu,
            int socialRounds, int minigames)
        {
            var root = Ink.Node(frame, "MainMenuPanel");
            root.Fill();
            var panel = root.gameObject.AddComponent<Panel>();
            panel.ApplyConfig(config);
            panel.SetMotion(1f, Vector2.zero);
            int target = config != null ? config.targetScore : 100;

            // The sound button over the masthead's rule, then the nameplate.
            SoundButton.Create(root, settings, 1872f - 52f, 20f, 52f);
            Ink.Plain(root, "Rule0", Palette.Ink).rectTransform.At(48f, 82f, 1824f, 2f);

            var title = Ink.Headline(root, "Title", "Smartest in the Room", 218f, 8f, 90f).OneLine();
            title.alignment = TextAlignmentOptions.Top;
            title.rectTransform.At(48f, 96f, 1824f, 190f);
            Ink.Plain(root, "Rule1", Palette.Ink).rectTransform.At(48f, 292f, 1824f, 6f);
            Ink.Plain(root, "Rule2", Palette.Ink).rectTransform.At(48f, 302f, 1824f, 2f);

            // Left column: the pitch.
            var pitch = Ink.Text(root, "Pitch", $"First to {target} points is the smartest in the room.", TypeRole.Display,
                84f, Palette.Ink, TextAlignmentOptions.TopLeft, caps: true, lineHeight: 0.9f);
            pitch.rectTransform.At(48f, 338f, 1060f, 160f);
            pitch.Fit(44f);
            // The two boxes say what a match is made of. Baked in by Build Scenes, so flipping
            // questionRounds in the config wants a rebuild to match.
            if (config != null && config.questionRounds)
            {
                KindBox(root, "Social", 48f, 532f, "Social rounds", $"{socialRounds} OF THEM",
                    "Everyone answers at once. What you score depends on what the room picked.", false);
                KindBox(root, "Minigames", 48f + 516f + 28f, 532f, "Minigames", $"{minigames} OF THEM",
                    "Same level for everyone. Fail and you're out. Last one standing wins.", true);
            }
            else
            {
                KindBox(root, "Minigames", 48f, 532f, "Minigames", $"{minigames} OF THEM",
                    "Reflexes, memory, aim and nerve. A new game every round, explained in one line.", true);
                KindBox(root, "Knockout", 48f + 516f + 28f, 532f, "Knockout", "EVERY LEVEL",
                    "Same level for everyone. Fail, or come last, and you're out. Last one standing wins.", false);
            }

            // Right: the play card.
            var card = Ink.Box(root, "Play", Palette.PaperHi, 4f);
            card.rectTransform.At(1160f, 338f, 704f, 640f);
            Ink.Shadow(card, 12f);
            var c = card.transform;
            Ink.Label(c, "NameLabel", "YOUR NAME", 15f).rectTransform.At(40f, 34f, 400f, 20f);
            menu.nameField = Field(c, "NameField", 40f, 66f, 616f, 88f, TypeRole.Name, 44f, false, "Your name");
            menu.nameCounter = Ink.Text(c, "Counter", "0/12", TypeRole.Sticker, 16f, Palette.Ink,
                TextAlignmentOptions.MidlineRight).OneLine();
            menu.nameCounter.rectTransform.At(40f + 616f - 22f - 80f, 66f, 80f, 88f);

            menu.hostButton = BigChoice(c, "HostButton", "Host", "Start a lobby. Share the code.", Palette.Gold, 186f);
            menu.joinButton = BigChoice(c, "JoinButton", "Join", "Got a code? Type it in.", Palette.PaperHi, 348f);
            menu.quitButton = Ink.TextButton(c, "QuitButton", "QUIT", 22f);
            menu.quitButton.GetComponent<RectTransform>().At(40f, 520f, 120f, 44f);
            menu.settingsButton = Ink.TextButton(c, "SettingsButton", "SETTINGS", 22f);
            menu.settingsButton.GetComponent<RectTransform>().At(170f, 520f, 200f, 44f);
            menu.settingsPanel = settings;

            var status = Ink.Text(c, "Status", "", TypeRole.Body, 20f, Palette.Ink, TextAlignmentOptions.TopLeft,
                lineHeight: 1.35f);
            status.fontStyle |= FontStyles.Italic | FontStyles.Bold;
            status.rectTransform.At(40f, 570f, 616f, 60f);
            menu.mainStatus = status;
            return panel;
        }

        /// <summary>"SOCIAL ROUNDS · 24 OF THEM" over one sentence.</summary>
        private static void KindBox(Transform parent, string name, float left, float top, string title, string count,
            string body, bool gold)
        {
            var box = Ink.Box(parent, name, Palette.PaperHi, 4f);
            box.rectTransform.At(left, top, 516f, 176f);
            Ink.Shadow(box, 8f);
            var band = Ink.Plain(box.transform, "Band", gold ? Palette.Gold : Palette.Ink);
            band.rectTransform.At(4f, 4f, 508f, 52f);
            if (gold) Ink.Plain(box.transform, "BandRule", Palette.Ink).rectTransform.At(4f, 56f, 508f, 4f);
            var t = Ink.Text(band.transform, "Title", title, TypeRole.Display, 34f, gold ? Palette.Ink : Palette.Gold,
                TextAlignmentOptions.MidlineLeft, caps: true).OneLine();
            t.rectTransform.At(18f, 0f, 300f, 52f);
            var n = Ink.Label(band.transform, "Count", count, 12f, gold ? Palette.Ink : Palette.Paper,
                TextAlignmentOptions.MidlineRight, 0.12f);
            n.rectTransform.At(508f - 18f - 180f, 0f, 180f, 52f);
            var p = Ink.Text(box.transform, "Body", body, TypeRole.Body, 25f, Palette.Ink, TextAlignmentOptions.TopLeft,
                lineHeight: 1.45f);
            p.rectTransform.At(24f, 74f, 468f, 96f);
            p.Fit(18f);
        }

        /// <summary>A 616 × 146 slab: the verb huge on the left, what it does on the right.</summary>
        private static Button BigChoice(Transform parent, string name, string word, string blurb, Color fill, float top)
        {
            var btn = Ink.Slab(parent, name, word, fill, Palette.Ink, 112f);
            btn.GetComponent<RectTransform>().At(40f, top, 616f, 146f);
            var label = btn.transform.Find("Label").GetComponent<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.Fill(28f, 4f, 300f, 4f);
            var b = Ink.Text(btn.transform, "Blurb", blurb, TypeRole.Body, 20f, Palette.Ink,
                TextAlignmentOptions.MidlineRight, lineHeight: 1.3f);
            b.fontStyle |= FontStyles.Bold;
            b.rectTransform.At(616f - 28f - 250f, 0f, 250f, 146f);
            return btn;
        }

        private static Panel BuildJoin(Transform frame, GameConfig config, MenuUI menu)
        {
            var root = Ink.Node(frame, "JoinPanel");
            root.Fill(-2000f, -2000f, -2000f, -2000f);
            var scrim = root.gameObject.AddComponent<Image>();
            scrim.color = Palette.Scrim;
            var panel = root.gameObject.AddComponent<Panel>();
            panel.ApplyConfig(config);
            panel.SetMotion(1f, Vector2.zero);

            var space = Ink.Node(root, "Frame");
            space.Fill(2000f, 2000f, 2000f, 2000f);
            var card = Ink.Box(space, "Card", Palette.PaperHi, 4f, 0f, raycast: true);
            card.rectTransform.At(600f, 270f, 720f, 540f);
            Ink.Shadow(card, 14f, Palette.Gold);
            var c = card.transform;

            var kick = Ink.Kicker(c, "Title", "Join a game", 44f);
            Ink.BoxOf(kick).Pin(48f, 40f, new Vector2(0f, 1f));
            Ink.Label(c, "CodeLabel", "JOIN CODE", 15f).rectTransform.At(48f, 136f, 400f, 20f);
            menu.codeField = Field(c, "CodeField", 48f, 172f, 616f, 118f, TypeRole.Mono, 70f, true, "CODE");

            menu.joinGoButton = Ink.Slab(c, "JoinGoButton", "Go", Palette.Gold, Palette.Ink, 76f, 4f, 8f);
            menu.joinGoButton.GetComponent<RectTransform>().At(48f, 316f, 394f, 104f);
            menu.joinBackButton = Ink.Slab(c, "JoinBackButton", "Back", Palette.PaperHi, Palette.Ink, 52f, 4f, 8f);
            menu.joinBackButton.GetComponent<RectTransform>().At(48f + 394f + 22f, 316f, 200f, 104f);

            menu.joinStatus = Ink.Stamp(c, "Status", "JOINING…", 22f, -3f, 3f);
            Ink.BoxOf(menu.joinStatus).Pin(48f, 474f, new Vector2(0f, 0.5f));
            Ink.BoxOf(menu.joinStatus).gameObject.SetActive(false);
            return panel;
        }

        /// <summary>A paper field with a 4 px ink border and a gold caret.</summary>
        public static TMP_InputField Field(Transform parent, string name, float left, float top, float width, float height,
            TypeRole role, float size, bool centred, string placeholder)
        {
            var bg = Ink.Box(parent, name, Palette.Paper, 4f, 0f, raycast: true);
            bg.rectTransform.At(left, top, width, height);
            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;

            var area = Ink.Node(bg.transform, "Text Area");
            area.Fill(22f, 4f, centred ? 22f : 90f, 4f);
            area.gameObject.AddComponent<RectMask2D>();

            var align = centred ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            var ph = Ink.Text(area, "Placeholder", placeholder, role, size, Palette.Ink2, align,
                tracking: centred ? 0.16f : 0f).OneLine();
            ph.rectTransform.Fill();
            ph.alpha = 0.35f;
            var txt = Ink.Text(area, "Text", string.Empty, role, size, Palette.Ink, align,
                tracking: centred ? 0.16f : 0f).OneLine();
            txt.rectTransform.Fill();
            txt.richText = false;

            input.textViewport = area;
            input.textComponent = txt;
            input.placeholder = ph;
            input.fontAsset = txt.font;
            input.pointSize = size;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.customCaretColor = true;
            input.caretColor = Palette.Gold;
            input.caretWidth = 6;
            input.selectionColor = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.45f);
            input.richText = false;

            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            input.colors = colors;
            return input;
        }
    }
}
