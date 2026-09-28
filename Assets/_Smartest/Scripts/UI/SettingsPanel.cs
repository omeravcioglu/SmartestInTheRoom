using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// Sound settings: everything, the host's voice, background music, sound effects, and
    /// whether the host's lines are captioned. Levels apply as you drag and are remembered
    /// on this machine. Same modal in the menu and mid-game, because turning the host down is
    /// usually something you decide at exactly the moment he says something for the fourth time.
    /// </summary>
    public class SettingsPanel : Panel
    {
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider narratorSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Text masterValue;
        [SerializeField] private TMP_Text narratorValue;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private TMP_Text sfxValue;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button openButton;
        [SerializeField] private TMP_Text musicHint;
        [SerializeField] private Button captionsToggle;
        [SerializeField] private Image captionsTrack;
        [SerializeField] private RectTransform captionsKnob;

        private bool _syncing;
        private float _lastPreview;

        protected override void Awake()
        {
            base.Awake();
            if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMaster);
            if (narratorSlider != null) narratorSlider.onValueChanged.AddListener(OnNarrator);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusic);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfx);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (openButton != null) openButton.onClick.AddListener(Toggle);
            if (captionsToggle != null) captionsToggle.onClick.AddListener(OnCaptions);
            AudioDirector.VolumesChanged += SyncFromDirector;
        }

        private void OnDestroy()
        {
            AudioDirector.VolumesChanged -= SyncFromDirector;
        }

        private void Start()
        {
            SyncFromDirector();
            RefreshCaptions();
        }

        private void Update()
        {
            if (IsShown && KeyInput.EscapePressed()) Close();
        }

        public void Toggle()
        {
            Sounds.Play(Sounds.Kind.Click);
            if (IsShown) Hide();
            else { SyncFromDirector(); RefreshCaptions(); Show(); }
        }

        public void Close()
        {
            Sounds.Play(Sounds.Kind.Click);
            Hide();
        }

        private void SyncFromDirector()
        {
            var d = AudioDirector.Instance;
            if (d == null) return;
            _syncing = true;
            if (masterSlider != null) masterSlider.value = d.Master;
            if (narratorSlider != null) narratorSlider.value = d.Narrator;
            if (musicSlider != null) musicSlider.value = d.Music;
            if (sfxSlider != null) sfxSlider.value = d.Sfx;
            _syncing = false;
            Relabel();

            if (musicHint != null) musicHint.gameObject.SetActive(!d.HasMusic);
        }

        private void Relabel()
        {
            Label(masterValue, masterSlider);
            Label(narratorValue, narratorSlider);
            Label(musicValue, musicSlider);
            Label(sfxValue, sfxSlider);
        }

        private static void Label(TMP_Text text, Slider slider)
        {
            if (text == null || slider == null) return;
            text.text = Mathf.RoundToInt(slider.value * 100f) + "%";
            text.alpha = slider.value <= 0.001f ? 0.35f : 1f;
        }

        private void OnMaster(float v) { if (!Apply(d => d.Master = v)) return; Preview(AudioDirector.Channel.Sfx); }
        private void OnNarrator(float v) { if (!Apply(d => d.Narrator = v)) return; Preview(AudioDirector.Channel.Narrator); }
        private void OnMusic(float v) { Apply(d => d.Music = v); }
        private void OnSfx(float v) { if (!Apply(d => d.Sfx = v)) return; Preview(AudioDirector.Channel.Sfx); }

        private bool Apply(System.Action<AudioDirector> set)
        {
            if (_syncing) return false;
            var d = AudioDirector.Instance;
            if (d == null) return false;
            set(d);
            Relabel();
            return true;
        }

        /// <summary>A blip while dragging, throttled so a slider drag isn't a machine gun.</summary>
        private void Preview(AudioDirector.Channel channel)
        {
            if (Time.unscaledTime - _lastPreview < 0.12f) return;
            _lastPreview = Time.unscaledTime;
            var d = AudioDirector.Instance;
            if (d != null) d.PreviewChannel(channel);
        }

        private void OnCaptions()
        {
            Sounds.Play(Sounds.Kind.Click);
            HostCaption.Enabled = !HostCaption.Enabled;
            RefreshCaptions();
        }

        private void RefreshCaptions()
        {
            bool on = HostCaption.Enabled;
            if (captionsTrack != null) captionsTrack.color = on ? Palette.Gold : Palette.Paper2;
            if (captionsKnob != null)
            {
                captionsKnob.anchorMin = captionsKnob.anchorMax = new Vector2(on ? 1f : 0f, 0.5f);
                captionsKnob.pivot = new Vector2(on ? 1f : 0f, 0.5f);
                captionsKnob.anchoredPosition = new Vector2(on ? -4f : 4f, 0f);
            }
        }

        // ------------------------------------------------------------------
        // Construction (SceneBuilder)
        // ------------------------------------------------------------------

        /// <summary>The modal: a 62% ink scrim and a 760 × 844 card with a gold shadow.</summary>
        public static SettingsPanel Create(Transform frame, GameConfig config)
        {
            var root = Ink.Node(frame, "SettingsPanel");
            root.Fill(-2000f, -2000f, -2000f, -2000f); // the scrim covers any aspect ratio
            var scrim = root.gameObject.AddComponent<Image>();
            scrim.color = Palette.Scrim;
            scrim.raycastTarget = true;
            var panel = root.gameObject.AddComponent<SettingsPanel>();
            panel.ApplyConfig(config);
            panel.SetMotion(1f, Vector2.zero);

            // The card lives in frame coordinates, inside a node that undoes the scrim's overhang.
            var frameSpace = Ink.Node(root, "Frame");
            frameSpace.Fill(2000f, 2000f, 2000f, 2000f);
            var card = Ink.Box(frameSpace, "Card", Palette.PaperHi, 4f, 0f, raycast: true);
            card.rectTransform.At(580f, 118f, 760f, 844f);
            Ink.Shadow(card, 14f, Palette.Gold);
            var c = card.transform;

            var title = Ink.Kicker(c, "Title", "Sound", 44f);
            Ink.BoxOf(title).Pin(44f, 38f, new Vector2(0f, 1f));
            var escRow = Ink.Row(c, "Esc", 8f, TextAnchor.MiddleRight);
            escRow.Pin(760f - 44f, 62f, new Vector2(1f, 0.5f));
            var esc = Ink.Chip(escRow, "Key", "ESC", TypeRole.Sticker, 13f, Palette.Ink, Palette.PaperHi, 0f,
                new RectOffset(8, 8, 2, 2));
            Ink.Unhug(Ink.BoxOf(esc));
            Ink.Label(escRow, "Closes", "CLOSES", 12f);

            string[] labels = { "EVERYTHING", "HOST VOICE", "MUSIC", "SOUND EFFECTS" };
            var sliders = new Slider[4];
            var values = new TMP_Text[4];
            float y = 118f;
            for (int i = 0; i < 4; i++)
            {
                var label = Ink.Label(c, "Label" + i, labels[i], 15f, Palette.Ink, TextAlignmentOptions.BottomLeft);
                label.rectTransform.At(48f, y, 400f, 40f);
                values[i] = Ink.Text(c, "Value" + i, "100%", TypeRole.Display, 46f, Palette.Ink,
                    TextAlignmentOptions.BottomRight, lineHeight: 1f).OneLine();
                values[i].rectTransform.At(760f - 48f - 200f, y - 8f, 200f, 50f);
                sliders[i] = CreateSlider(c, "Slider" + i, 48f, y + 48f, 664f);
                y += 112f;
                if (i == 2)
                {
                    var hint = Ink.Text(c, "MusicHint", "No music yet — drop a loop into Audio/Music and rebuild.",
                        TypeRole.Body, 18f, Palette.Ink2, TextAlignmentOptions.TopLeft).OneLine();
                    hint.fontStyle |= FontStyles.Italic;
                    hint.rectTransform.At(48f, y - 16f, 664f, 26f);
                    panel.musicHint = hint;
                    y += 18f;
                }
            }
            panel.masterSlider = sliders[0]; panel.narratorSlider = sliders[1];
            panel.musicSlider = sliders[2]; panel.sfxSlider = sliders[3];
            panel.masterValue = values[0]; panel.narratorValue = values[1];
            panel.musicValue = values[2]; panel.sfxValue = values[3];

            Ink.Plain(c, "Divider", Palette.Ink).rectTransform.At(44f, y + 4f, 672f, 3f);
            y += 26f;
            Ink.Label(c, "CaptionsLabel", "HOST CAPTIONS", 15f).rectTransform.At(48f, y, 400f, 22f);
            var sub = Ink.Text(c, "CaptionsNote", "Subtitles for every voice line", TypeRole.Body, 19f, Palette.Ink2,
                TextAlignmentOptions.TopLeft).OneLine();
            sub.rectTransform.At(48f, y + 26f, 480f, 26f);

            var track = Ink.Box(c, "CaptionsToggle", Palette.Gold, 3f, 0f, raycast: true);
            track.rectTransform.At(760f - 44f - 96f, y + 2f, 96f, 48f);
            var toggle = track.gameObject.AddComponent<Button>();
            toggle.transition = Selectable.Transition.None;
            toggle.targetGraphic = track;
            var knob = Ink.Box(track.transform, "Knob", Palette.PaperHi, 3f);
            knob.rectTransform.sizeDelta = new Vector2(34f, 34f);
            panel.captionsToggle = toggle;
            panel.captionsTrack = track;
            panel.captionsKnob = knob.rectTransform;
            panel.RefreshCaptions();

            var done = Ink.Slab(c, "Done", "Done", Palette.Gold, Palette.Ink, 80f, 4f, 8f);
            done.GetComponent<RectTransform>().At((760f - 320f) * 0.5f, 844f - 38f - 104f, 320f, 104f);
            panel.closeButton = done;
            return panel;
        }

        /// <summary>A bordered track that fills gold, and a paper thumb with a hard shadow.</summary>
        private static Slider CreateSlider(Transform parent, string name, float left, float top, float width)
        {
            var root = Ink.Node(parent, name);
            root.At(left, top, width, 40f);

            var track = Ink.Box(root, "Background", Palette.Paper2, 3f);
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0f, 18f);

            var fillArea = Ink.Node(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-6f, 12f);
            var fill = Ink.Plain(fillArea, "Fill", Palette.Gold);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = Ink.Node(root, "Handle Slide Area");
            handleArea.Fill(13f, 0f, 13f, 0f);
            var handle = Ink.Box(handleArea, "Handle", Palette.PaperHi, 3f, 0f, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(26f, 0f); // the slider stretches it to 40 tall
            Ink.Shadow(handle, 3f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.transition = Selectable.Transition.None;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 1f;
            return slider;
        }
    }
}
