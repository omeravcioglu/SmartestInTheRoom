using Smartest.Core;
using Smartest.Net;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// Esc in a match: RESUME, SETTINGS and the way out. A guest's LEAVE MATCH takes them to the
    /// front page while the others play on; the host can END MATCH, which takes everyone back to
    /// the lobby, or LEAVE, which closes the lobby. Both ways out ask twice. The match doesn't
    /// stop while the menu is open, but it holds the game's keys and mouse so a click here
    /// never lands in a minigame.
    /// </summary>
    public class MatchMenu : Panel
    {
        private const float ChoiceH = 96f;
        private const float ChoiceGap = 16f;
        private const float FirstChoice = 150f;
        private const float AskSeconds = 3f;
        private const string Ask = "Sure? Click again";

        [SerializeField] private RectTransform card;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button endButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private TMP_Text leaveBlurb;
        [SerializeField] private SettingsPanel settings;

        private Button _asking;
        private string _askingWord;
        private float _askingUntil;
        private bool _leaving;

        protected override void Awake()
        {
            base.Awake();
            if (resumeButton != null) resumeButton.onClick.AddListener(Close);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            if (endButton != null) endButton.onClick.AddListener(OnEnd);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeave);
        }

        private void Update()
        {
            if (_asking != null && Time.unscaledTime > _askingUntil) StopAsking();
            if (_leaving || !KeyInput.EscapePressed()) return;
            if (settings != null && settings.IsShown) return; // Esc closes the settings first
            Toggle();
        }

        private void OnDisable() => KeyInput.Hold(this, false);

        public void Toggle()
        {
            if (IsShown) Close();
            else Open();
        }

        public void Open()
        {
            if (IsShown || _leaving) return;
            Arrange();
            KeyInput.Hold(this, true);
            Sounds.Play(Sounds.Kind.Click);
            Show();
        }

        public void Close()
        {
            if (!IsShown) return;
            StopAsking();
            KeyInput.Hold(this, false);
            Sounds.Play(Sounds.Kind.Click);
            Hide();
        }

        protected override void OnShown()
        {
            base.OnShown();
            KeyInput.Hold(this, true);
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            KeyInput.Hold(this, false);
        }

        /// <summary>The host gets END MATCH as well; a guest's LEAVE moves up into its place.</summary>
        private void Arrange()
        {
            var net = NetSession.Instance;
            bool host = net != null && net.IsHost;
            if (endButton != null) endButton.gameObject.SetActive(host);
            int choices = host ? 4 : 3;
            // Ink.At puts pivots in the middle, so positions here are centres.
            if (leaveButton != null)
            {
                SetWord(leaveButton, host ? "Leave" : "Leave match");
                var rt = (RectTransform)leaveButton.transform;
                float top = FirstChoice + (choices - 1) * (ChoiceH + ChoiceGap);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(top + ChoiceH * 0.5f));
            }
            if (leaveBlurb != null) leaveBlurb.text = host ? "Closes the lobby for everyone." : "The others play on.";
            if (card != null)
            {
                float height = FirstChoice + choices * (ChoiceH + ChoiceGap) - ChoiceGap + 44f;
                card.sizeDelta = new Vector2(card.sizeDelta.x, height);
                card.anchoredPosition = new Vector2(card.anchoredPosition.x, -540f); // centred on the page
            }
        }

        private void OnSettings()
        {
            if (settings != null) settings.Toggle();
        }

        private void OnEnd()
        {
            if (!Asked(endButton)) return;
            var net = NetSession.Instance;
            if (net == null || !net.IsHost) return;
            Close();
            net.EndMatchForEveryone();
        }

        private async void OnLeave()
        {
            if (_leaving || !Asked(leaveButton)) return;
            _leaving = true;
            KeyInput.Hold(this, false);
            var net = NetSession.Instance;
            if (net != null) await net.LeaveAsync();
            // A voluntary leave doesn't move scenes by itself (only a dropped connection does).
            SceneManager.LoadScene(NetSession.MenuSceneName);
        }

        /// <summary>The second click inside a few seconds counts; the first turns the button red and asks.</summary>
        private bool Asked(Button button)
        {
            if (button == null) return false;
            if (_asking == button)
            {
                StopAsking();
                return true;
            }
            StopAsking();
            _asking = button;
            _askingWord = WordOf(button);
            _askingUntil = Time.unscaledTime + AskSeconds;
            SetWord(button, Ask);
            Paint(button, Palette.Red, Palette.OnRed);
            Sounds.Play(Sounds.Kind.Click);
            return false;
        }

        private void StopAsking()
        {
            if (_asking == null) return;
            SetWord(_asking, _askingWord);
            Paint(_asking, Palette.PaperHi, Palette.Ink);
            _asking = null;
        }

        private static TMP_Text LabelOf(Button b) => b.transform.Find("Label")?.GetComponent<TMP_Text>();
        private static string WordOf(Button b) => LabelOf(b) != null ? LabelOf(b).text : string.Empty;

        private static void SetWord(Button b, string word)
        {
            var label = LabelOf(b);
            if (label != null) label.text = word;
        }

        private static void Paint(Button b, Color fill, Color ink)
        {
            if (b.targetGraphic is Image face) face.color = fill;
            foreach (var t in b.GetComponentsInChildren<TMP_Text>(true)) t.color = ink;
        }

        // ------------------------------------------------------------------
        // Construction (SceneBuilder)
        // ------------------------------------------------------------------

        /// <summary>A modal like the settings: an ink scrim, and a card of big choices.</summary>
        public static MatchMenu Create(Transform frame, GameConfig config, SettingsPanel settings)
        {
            var root = Ink.Node(frame, "MatchMenu");
            root.Fill(-2000f, -2000f, -2000f, -2000f); // the scrim covers any aspect ratio
            var scrim = root.gameObject.AddComponent<Image>();
            scrim.color = Palette.Scrim;
            scrim.raycastTarget = true;
            var menu = root.gameObject.AddComponent<MatchMenu>();
            menu.ApplyConfig(config);
            menu.SetMotion(1f, Vector2.zero);
            menu.settings = settings;

            var space = Ink.Node(root, "Frame");
            space.Fill(2000f, 2000f, 2000f, 2000f);
            const float cardW = 760f;
            float cardH = FirstChoice + 4f * (ChoiceH + ChoiceGap) - ChoiceGap + 44f;
            var box = Ink.Box(space, "Card", Palette.PaperHi, 4f, 0f, raycast: true);
            box.rectTransform.At((1920f - cardW) * 0.5f, (1080f - cardH) * 0.5f, cardW, cardH);
            Ink.Shadow(box, 14f, Palette.Gold);
            menu.card = box.rectTransform;
            var c = box.transform;

            var title = Ink.Kicker(c, "Title", "Match menu", 44f);
            Ink.BoxOf(title).Pin(44f, 38f, new Vector2(0f, 1f));
            var escRow = Ink.Row(c, "Esc", 8f, TextAnchor.MiddleRight);
            escRow.Pin(cardW - 44f, 62f, new Vector2(1f, 0.5f));
            var esc = Ink.Chip(escRow, "Key", "ESC", TypeRole.Sticker, 13f, Palette.Ink, Palette.PaperHi, 0f,
                new RectOffset(8, 8, 2, 2));
            Ink.Unhug(Ink.BoxOf(esc));
            Ink.Label(escRow, "Closes", "CLOSES", 12f);
            var note = Ink.Text(c, "Note", "The match keeps going while this is open.", TypeRole.Body, 20f, Palette.Ink2,
                TextAlignmentOptions.TopLeft).OneLine();
            note.fontStyle |= FontStyles.Italic;
            note.rectTransform.At(48f, 104f, cardW - 96f, 28f);

            menu.resumeButton = Choice(c, "Resume", "Resume", "Back to the game.", Palette.Gold, 0);
            menu.settingsButton = Choice(c, "Settings", "Settings", "Sound and screen.", Palette.PaperHi, 1);
            menu.endButton = Choice(c, "EndMatch", "End match", "Everyone goes back to the lobby.", Palette.PaperHi, 2);
            menu.leaveButton = Choice(c, "Leave", "Leave", "Closes the lobby for everyone.", Palette.PaperHi, 3);
            menu.leaveBlurb = menu.leaveButton.transform.Find("Blurb").GetComponent<TMP_Text>();
            return menu;
        }

        /// <summary>A full-width slab: the word big on the left, what it does on the right.</summary>
        private static Button Choice(Transform card, string name, string word, string blurb, Color fill, int slot)
        {
            var button = Ink.Slab(card, name, word, fill, Palette.Ink, 52f, 4f, 8f);
            button.GetComponent<RectTransform>().At(48f, FirstChoice + slot * (ChoiceH + ChoiceGap), 664f, ChoiceH);
            var label = button.transform.Find("Label").GetComponent<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.Fill(24f, 4f, 300f, 4f);
            var b = Ink.Text(button.transform, "Blurb", blurb, TypeRole.Body, 19f, Palette.Ink,
                TextAlignmentOptions.MidlineRight, lineHeight: 1.25f);
            b.fontStyle |= FontStyles.Bold;
            b.rectTransform.At(664f - 24f - 270f, 0f, 270f, ChoiceH);
            return button;
        }
    }
}
