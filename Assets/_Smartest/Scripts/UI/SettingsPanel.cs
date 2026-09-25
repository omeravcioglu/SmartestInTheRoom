using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// Four sliders: everything, the host's voice, background music, sound effects.
    /// Levels apply as you drag and are remembered on this machine. Same panel in the menu
    /// and mid-game, because turning the host down is usually something you decide at
    /// exactly the moment he says something for the fourth time.
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
            AudioDirector.VolumesChanged += SyncFromDirector;
        }

        private void OnDestroy()
        {
            AudioDirector.VolumesChanged -= SyncFromDirector;
        }

        private void Start()
        {
            SyncFromDirector();
        }

        private void Update()
        {
            if (IsShown && KeyInput.EscapePressed()) Close();
        }

        public void Toggle()
        {
            Sounds.Play(Sounds.Kind.Click);
            if (IsShown) Hide();
            else { SyncFromDirector(); Show(); }
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

            if (musicHint != null)
            {
                bool hasMusic = d.HasMusic;
                musicHint.gameObject.SetActive(!hasMusic);
            }
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
            text.color = slider.value <= 0.001f ? Palette.TextDim : Palette.Text;
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
    }
}
