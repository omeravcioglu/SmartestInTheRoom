using UnityEngine;

namespace Smartest.Core
{
    /// <summary>
    /// Everything that makes noise goes through here, on one of three channels the player
    /// can set independently: the host's voice, background music, and sound effects.
    /// Levels are saved per machine (PlayerPrefs) and applied the moment they change, so the
    /// settings panel is just four sliders pointed at these properties.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public enum Channel { Narrator, Music, Sfx }

        public static AudioDirector Instance { get; private set; }

        [SerializeField] private AudioSource narratorSource;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [Tooltip("Optional. Drop a loop in Assets/_Smartest/Audio/Music and rebuild to fill this.")]
        [SerializeField] private AudioClip musicLoop;

        private const string PrefMaster = "smartest.vol.master";
        private const string PrefNarrator = "smartest.vol.narrator";
        private const string PrefMusic = "smartest.vol.music";
        private const string PrefSfx = "smartest.vol.sfx";

        private float _master = 1f;
        private float _narrator = 1f;
        private float _music = 0.5f;
        private float _sfx = 0.8f;

        /// <summary>Raised whenever any level changes, so open settings panels can follow along.</summary>
        public static event System.Action VolumesChanged;

        public float Master { get => _master; set => Set(ref _master, value, PrefMaster); }
        public float Narrator { get => _narrator; set => Set(ref _narrator, value, PrefNarrator); }
        public float Music { get => _music; set => Set(ref _music, value, PrefMusic); }
        public float Sfx { get => _sfx; set => Set(ref _sfx, value, PrefSfx); }

        public bool NarratorIsSpeaking => narratorSource != null && narratorSource.isPlaying;
        /// <summary>False until a loop is dropped into Assets/_Smartest/Audio/Music.</summary>
        public bool HasMusic => musicLoop != null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            narratorSource = Ensure(narratorSource, "Narrator");
            musicSource = Ensure(musicSource, "Music");
            sfxSource = Ensure(sfxSource, "Sfx");
            musicSource.loop = true;

            _master = PlayerPrefs.GetFloat(PrefMaster, 1f);
            _narrator = PlayerPrefs.GetFloat(PrefNarrator, 1f);
            _music = PlayerPrefs.GetFloat(PrefMusic, 0.5f);
            _sfx = PlayerPrefs.GetFloat(PrefSfx, 0.8f);

            Apply();
            if (musicLoop != null)
            {
                musicSource.clip = musicLoop;
                musicSource.Play();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private AudioSource Ensure(AudioSource existing, string name)
        {
            if (existing != null) return existing;
            var go = new GameObject(name, typeof(AudioSource));
            go.transform.SetParent(transform, false);
            var src = go.GetComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            return src;
        }

        private void Set(ref float field, float value, string pref)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(field, value)) return;
            field = value;
            PlayerPrefs.SetFloat(pref, value);
            Apply();
            VolumesChanged?.Invoke();
        }

        private void Apply()
        {
            if (narratorSource != null) narratorSource.volume = _master * _narrator;
            if (musicSource != null) musicSource.volume = _master * _music;
            if (sfxSource != null) sfxSource.volume = _master * _sfx;
        }

        // ------------------------------------------------------------------

        /// <summary>Speak a line. Returns false if the channel is busy with something louder.</summary>
        public bool PlayNarration(AudioClip clip, bool interrupt)
        {
            if (clip == null || narratorSource == null) return false;
            if (narratorSource.isPlaying)
            {
                if (!interrupt) return false;
                narratorSource.Stop();
            }
            narratorSource.clip = clip;
            narratorSource.Play();
            return true;
        }

        public void StopNarration()
        {
            if (narratorSource != null) narratorSource.Stop();
        }

        /// <summary>Sound effects overlap freely — they're short and they're meant to pile up.</summary>
        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        public void SetMusic(AudioClip clip)
        {
            if (musicSource == null) return;
            musicLoop = clip;
            musicSource.clip = clip;
            if (clip != null) musicSource.Play();
            else musicSource.Stop();
        }

        /// <summary>A short preview so dragging the narrator slider actually tells you something.</summary>
        public void PreviewChannel(Channel channel)
        {
            switch (channel)
            {
                case Channel.Sfx: Sounds.Play(Sounds.Kind.Click); break;
                case Channel.Music:
                    if (musicSource != null && musicLoop != null && !musicSource.isPlaying) musicSource.Play();
                    break;
                case Channel.Narrator: Sounds.Play(Sounds.Kind.Good); break;
            }
        }

#if UNITY_EDITOR
        public void EditorSetSources(AudioSource narrator, AudioSource music, AudioSource sfx, AudioClip loop)
        {
            narratorSource = narrator;
            musicSource = music;
            sfxSource = sfx;
            musicLoop = loop;
        }
#endif
    }
}
