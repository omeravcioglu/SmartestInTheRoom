using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// Settings. Sound on the left: everything, the host's voice, background music, sound
    /// effects. On the right the screen (full screen, or a window of a chosen size) and whether
    /// the host's lines are captioned. Everything applies at once and is remembered on this
    /// machine (Unity keeps the screen mode itself). Same modal in the menu and mid-game,
    /// because turning the host down is usually something you decide at exactly the moment he
    /// says something for the fourth time; open over a match, it holds the game's input.
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
        [SerializeField] private Button captionsToggle;
        [SerializeField] private Image captionsTrack;
        [SerializeField] private RectTransform captionsKnob;

        [Header("Screen")]
        [SerializeField] private Button fullscreenButton;
        [SerializeField] private Button windowedButton;
        [SerializeField] private Button smallerButton;
        [SerializeField] private Button biggerButton;
        [SerializeField] private TMP_Text sizeText;

        private bool _syncing;
        private float _lastPreview;
        private bool _windowed;
        private Vector2Int _windowSize = new Vector2Int(1280, 720);

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
            if (fullscreenButton != null) fullscreenButton.onClick.AddListener(OnFullscreen);
            if (windowedButton != null) windowedButton.onClick.AddListener(OnWindowed);
            if (smallerButton != null) smallerButton.onClick.AddListener(() => StepSize(-1));
            if (biggerButton != null) biggerButton.onClick.AddListener(() => StepSize(1));
            AudioDirector.VolumesChanged += SyncFromDirector;
        }

        private void OnDestroy()
        {
            AudioDirector.VolumesChanged -= SyncFromDirector;
        }

        private void OnDisable() => KeyInput.Hold(this, false);

        private void Start()
        {
            SyncFromDirector();
            RefreshCaptions();
            SyncScreen();
        }

        private void Update()
        {
            if (IsShown && KeyInput.EscapePressed()) Close();
        }

        public void Toggle()
        {
            Sounds.Play(Sounds.Kind.Click);
            if (IsShown)
            {
                KeyInput.Hold(this, false);
                Hide();
            }
            else
            {
                SyncFromDirector();
                RefreshCaptions();
                SyncScreen();
                KeyInput.Hold(this, true);
                Show();
            }
        }

        public void Close()
        {
            Sounds.Play(Sounds.Kind.Click);
            KeyInput.Hold(this, false);
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

        // ------------------------------------------------------------------
        // Sound
        // ------------------------------------------------------------------

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
        // Screen
        // ------------------------------------------------------------------

        private static readonly Vector2Int[] CommonSizes =
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3200, 1800), new Vector2Int(3840, 2160),
        };

        /// <summary>
        /// The window sizes on offer: the common 16:9 ones (the page is 16:9) that fit a screen
        /// this big with room left for the title bar and the taskbar. Never empty.
        /// </summary>
        public static List<Vector2Int> WindowSizes(int screenWidth, int screenHeight)
        {
            var sizes = new List<Vector2Int>();
            foreach (var s in CommonSizes)
                if (s.x <= screenWidth && s.y <= screenHeight - 80) sizes.Add(s);
            if (sizes.Count == 0) sizes.Add(CommonSizes[0]); // a very small screen: the smallest anyway
            return sizes;
        }

        /// <summary>The offered size closest in area to <paramref name="size"/>.</summary>
        public static Vector2Int Nearest(List<Vector2Int> sizes, Vector2Int size)
        {
            var best = sizes[0];
            long want = (long)size.x * size.y, bestGap = long.MaxValue;
            foreach (var s in sizes)
            {
                long gap = System.Math.Abs((long)s.x * s.y - want);
                if (gap < bestGap) { bestGap = gap; best = s; }
            }
            return best;
        }

        /// <summary>The monitor's own resolution (headless runs don't know it: then the usual 1920 × 1080).</summary>
        private static Vector2Int Desktop
        {
            get
            {
                int w = Display.main.systemWidth, h = Display.main.systemHeight;
                if (w <= 0 || h <= 0) { w = Screen.currentResolution.width; h = Screen.currentResolution.height; }
                if (w <= 0 || h <= 0) { w = 1920; h = 1080; }
                return new Vector2Int(w, h);
            }
        }
        private static List<Vector2Int> Sizes => WindowSizes(Desktop.x, Desktop.y);

        /// <summary>Read the screen as it is now: settings changes take effect at the end of a frame, so this is only for opening.</summary>
        private void SyncScreen()
        {
            _windowed = Screen.fullScreenMode == FullScreenMode.Windowed;
            _windowSize = Nearest(Sizes, new Vector2Int(Screen.width, Screen.height));
            RefreshScreen();
        }

        /// <summary>Full screen is always the screen's own resolution, so the page is always sharp.</summary>
        private void OnFullscreen()
        {
            if (!_windowed) return;
            _windowed = false;
            var d = Desktop;
            Screen.SetResolution(d.x, d.y, FullScreenMode.FullScreenWindow);
            Sounds.Play(Sounds.Kind.Click);
            RefreshScreen();
        }

        private void OnWindowed()
        {
            if (_windowed) return;
            _windowed = true;
            Screen.SetResolution(_windowSize.x, _windowSize.y, FullScreenMode.Windowed);
            Sounds.Play(Sounds.Kind.Click);
            RefreshScreen();
        }

        private void StepSize(int step)
        {
            if (!_windowed) return;
            var sizes = Sizes;
            int i = Mathf.Max(0, sizes.IndexOf(_windowSize));
            int next = Mathf.Clamp(i + step, 0, sizes.Count - 1);
            if (next == i) return;
            _windowSize = sizes[next];
            Screen.SetResolution(_windowSize.x, _windowSize.y, FullScreenMode.Windowed);
            Sounds.Play(Sounds.Kind.Click);
            RefreshScreen();
        }

        private void RefreshScreen()
        {
            Choose(fullscreenButton, !_windowed);
            Choose(windowedButton, _windowed);
            var sizes = Sizes;
            int i = sizes.IndexOf(_windowSize);
            if (sizeText != null)
            {
                var shown = _windowed ? _windowSize : Desktop;
                sizeText.text = $"{shown.x} × {shown.y}";
                sizeText.alpha = _windowed ? 1f : 0.4f; // full screen: just saying what it uses
            }
            // A slab that can't be clicked fades itself (SlabPress).
            if (smallerButton != null) smallerButton.interactable = _windowed && i > 0;
            if (biggerButton != null) biggerButton.interactable = _windowed && i >= 0 && i < sizes.Count - 1;
        }

        private static void Choose(Button button, bool chosen)
        {
            if (button != null && button.targetGraphic is Image face) face.color = chosen ? Palette.Gold : Palette.PaperHi;
        }

        // ------------------------------------------------------------------
        // Construction (SceneBuilder)
        // ------------------------------------------------------------------

        /// <summary>The modal: a 62% ink scrim and a 1240 × 720 card with a gold shadow, two columns.</summary>
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
            const float cardW = 1240f, cardH = 720f, colW = 540f, leftX = 48f, rightX = 652f;
            var card = Ink.Box(frameSpace, "Card", Palette.PaperHi, 4f, 0f, raycast: true);
            card.rectTransform.At((1920f - cardW) * 0.5f, (1080f - cardH) * 0.5f, cardW, cardH);
            Ink.Shadow(card, 14f, Palette.Gold);
            var c = card.transform;

            var title = Ink.Kicker(c, "Title", "Settings", 44f);
            Ink.BoxOf(title).Pin(44f, 38f, new Vector2(0f, 1f));
            var escRow = Ink.Row(c, "Esc", 8f, TextAnchor.MiddleRight);
            escRow.Pin(cardW - 44f, 62f, new Vector2(1f, 0.5f));
            var esc = Ink.Chip(escRow, "Key", "ESC", TypeRole.Sticker, 13f, Palette.Ink, Palette.PaperHi, 0f,
                new RectOffset(8, 8, 2, 2));
            Ink.Unhug(Ink.BoxOf(esc));
            Ink.Label(escRow, "Closes", "CLOSES", 12f);

            // Left: sound.
            string[] labels = { "EVERYTHING", "HOST VOICE", "MUSIC", "SOUND EFFECTS" };
            var sliders = new Slider[4];
            var values = new TMP_Text[4];
            float y = 118f;
            for (int i = 0; i < 4; i++)
            {
                var label = Ink.Label(c, "Label" + i, labels[i], 15f, Palette.Ink, TextAlignmentOptions.BottomLeft);
                label.rectTransform.At(leftX, y, 300f, 40f);
                values[i] = Ink.Text(c, "Value" + i, "100%", TypeRole.Display, 46f, Palette.Ink,
                    TextAlignmentOptions.BottomRight, lineHeight: 1f).OneLine();
                values[i].rectTransform.At(leftX + colW - 200f, y - 8f, 200f, 50f);
                sliders[i] = CreateSlider(c, "Slider" + i, leftX, y + 48f, colW);
                y += 112f;
            }
            panel.masterSlider = sliders[0]; panel.narratorSlider = sliders[1];
            panel.musicSlider = sliders[2]; panel.sfxSlider = sliders[3];
            panel.masterValue = values[0]; panel.narratorValue = values[1];
            panel.musicValue = values[2]; panel.sfxValue = values[3];

            Ink.Plain(c, "Between", Palette.Ink).rectTransform.At(cardW * 0.5f - 1.5f, 124f, 3f, 418f);

            // Right: the screen.
            Ink.Label(c, "ScreenLabel", "SCREEN", 15f, Palette.Ink, TextAlignmentOptions.BottomLeft)
                .rectTransform.At(rightX, 118f, 300f, 40f);
            panel.fullscreenButton = Ink.Slab(c, "Fullscreen", "Full screen", Palette.PaperHi, Palette.Ink, 30f, 3f, 6f);
            panel.fullscreenButton.GetComponent<RectTransform>().At(rightX, 166f, 262f, 64f);
            panel.windowedButton = Ink.Slab(c, "Windowed", "Window", Palette.PaperHi, Palette.Ink, 30f, 3f, 6f);
            panel.windowedButton.GetComponent<RectTransform>().At(rightX + colW - 262f, 166f, 262f, 64f);
            Ink.Label(c, "SizeLabel", "WINDOW SIZE", 15f, Palette.Ink, TextAlignmentOptions.BottomLeft)
                .rectTransform.At(rightX, 254f, 300f, 40f);
            panel.smallerButton = Ink.Slab(c, "Smaller", "<", Palette.PaperHi, Palette.Ink, 40f, 3f, 6f);
            panel.smallerButton.GetComponent<RectTransform>().At(rightX, 302f, 64f, 64f);
            panel.biggerButton = Ink.Slab(c, "Bigger", ">", Palette.PaperHi, Palette.Ink, 40f, 3f, 6f);
            panel.biggerButton.GetComponent<RectTransform>().At(rightX + colW - 64f, 302f, 64f, 64f);
            panel.sizeText = Ink.Text(c, "Size", "1280 × 720", TypeRole.Display, 40f, Palette.Ink,
                TextAlignmentOptions.Center, lineHeight: 1f).OneLine();
            panel.sizeText.rectTransform.At(rightX + 80f, 302f, colW - 160f, 64f);

            // Right, below: captions.
            Ink.Plain(c, "Divider", Palette.Ink).rectTransform.At(rightX, 400f, colW, 3f);
            float cy = 424f;
            Ink.Label(c, "CaptionsLabel", "HOST CAPTIONS", 15f).rectTransform.At(rightX, cy, 400f, 22f);
            var sub = Ink.Text(c, "CaptionsNote", "Subtitles for every voice line", TypeRole.Body, 19f, Palette.Ink2,
                TextAlignmentOptions.TopLeft).OneLine();
            sub.rectTransform.At(rightX, cy + 26f, 420f, 26f);

            var track = Ink.Box(c, "CaptionsToggle", Palette.Gold, 3f, 0f, raycast: true);
            track.rectTransform.At(rightX + colW - 96f, cy + 2f, 96f, 48f);
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
            done.GetComponent<RectTransform>().At((cardW - 320f) * 0.5f, cardH - 38f - 104f, 320f, 104f);
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
