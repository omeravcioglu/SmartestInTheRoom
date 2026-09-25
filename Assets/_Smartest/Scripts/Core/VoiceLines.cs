using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Smartest.Core
{
    /// <summary>
    /// The host. Pre-recorded clips played at fixed moments, all triggered locally — every
    /// client plays its own copy and nothing about audio is networked.
    ///
    /// Each key holds several written variants. Playing a key draws from a shuffled bag of
    /// its variants, so you never hear the same line twice until you've heard them all,
    /// and never the same one twice in a row across bags.
    ///
    /// A line only interrupts one that's already playing if its priority is higher, so the
    /// quiet asides never talk over "that's the match".
    /// </summary>
    public class VoiceLines : MonoBehaviour
    {
        public enum PlayMode { Always, Chance, OncePerSession }

        [Serializable]
        public class VoiceLine
        {
            public string key;
            public PlayMode mode = PlayMode.Always;
            [Range(0f, 1f)] public float chance = 1f;
            public bool oncePerSession;
            public float cooldownSec;
            public int priority;
            [Tooltip("What the host says. One entry per recorded variant.")]
            [TextArea(1, 3)] public string[] texts = new string[0];
            [Tooltip("Parallel to texts: <key>_1, <key>_2, ...")]
            public AudioClip[] clips = new AudioClip[0];

            public int VariantCount => clips != null ? clips.Length : 0;
        }

        [SerializeField] private List<VoiceLine> lines = new List<VoiceLine>();

        public static VoiceLines Instance { get; private set; }

        private readonly Dictionary<string, VoiceLine> _map = new Dictionary<string, VoiceLine>();
        private readonly HashSet<string> _playedOnce = new HashSet<string>();
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private readonly Dictionary<string, List<int>> _bags = new Dictionary<string, List<int>>();
        private readonly Dictionary<string, int> _lastVariant = new Dictionary<string, int>();
        private int _currentPriority = int.MinValue;

        public IReadOnlyList<VoiceLine> Lines => lines;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            _map.Clear();
            int missing = 0;
            var firstFew = new List<string>();
            foreach (var l in lines)
            {
                if (l == null || string.IsNullOrEmpty(l.key)) continue;
                _map[l.key] = l;
                int have = 0;
                if (l.clips != null)
                    foreach (var c in l.clips) if (c != null) have++;
                if (have == 0)
                {
                    missing++;
                    if (firstFew.Count < 6) firstFew.Add(l.key);
                }
            }
            if (missing > 0)
            {
                var sb = new StringBuilder("[VoiceLines] ");
                sb.Append(missing).Append(" line(s) have no audio yet (");
                sb.Append(string.Join(", ", firstFew));
                if (missing > firstFew.Count) sb.Append(", …");
                sb.Append("). Generate them with Tools > Smartest > Voice Studio. The game plays fine without them.");
                Debug.Log(sb.ToString());
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Play(string key)
        {
            if (Instance != null) Instance.PlayInternal(key);
        }

        public static void PlayDelayed(string key, float delaySeconds)
        {
            if (Instance != null) Instance.StartCoroutine(Instance.DelayedRoutine(key, delaySeconds));
        }

        /// <summary>True if the key exists and has at least one recorded clip.</summary>
        public static bool Has(string key)
        {
            return Instance != null && Instance._map.TryGetValue(key, out var l) && l.VariantCount > 0;
        }

        private IEnumerator DelayedRoutine(string key, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            PlayInternal(key);
        }

        private void PlayInternal(string key)
        {
            if (string.IsNullOrEmpty(key) || !_map.TryGetValue(key, out var line)) return;

            bool once = line.oncePerSession || line.mode == PlayMode.OncePerSession;
            if (once && _playedOnce.Contains(key)) return;

            if (line.cooldownSec > 0f && _lastPlayed.TryGetValue(key, out var last) &&
                Time.unscaledTime - last < line.cooldownSec) return;

            if (line.mode == PlayMode.Chance && UnityEngine.Random.value > line.chance) return;

            var clip = NextVariant(line);
            if (clip == null) return; // not recorded yet: stay silent, don't complain every time

            var director = AudioDirector.Instance;
            if (director == null) return;

            bool busy = director.NarratorIsSpeaking;
            if (busy && line.priority <= _currentPriority) return;
            if (!director.PlayNarration(clip, interrupt: true)) return;

            _currentPriority = line.priority;
            _lastPlayed[key] = Time.unscaledTime;
            if (once) _playedOnce.Add(key);
            StartCoroutine(ResetPriorityAfter(clip.length));
        }

        /// <summary>
        /// Shuffle-bag selection: every variant is heard once before any repeats, and the
        /// first line of a new bag is never the last line of the old one.
        /// </summary>
        private AudioClip NextVariant(VoiceLine line)
        {
            var available = new List<int>();
            for (int i = 0; i < line.clips.Length; i++) if (line.clips[i] != null) available.Add(i);
            if (available.Count == 0) return null;
            if (available.Count == 1) return line.clips[available[0]];

            if (!_bags.TryGetValue(line.key, out var bag) || bag.Count == 0)
            {
                bag = new List<int>(available);
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    (bag[i], bag[j]) = (bag[j], bag[i]);
                }
                if (bag.Count > 1 && _lastVariant.TryGetValue(line.key, out int prev) && bag[bag.Count - 1] == prev)
                    (bag[0], bag[bag.Count - 1]) = (bag[bag.Count - 1], bag[0]);
                _bags[line.key] = bag;
            }

            int pick = bag[bag.Count - 1];
            bag.RemoveAt(bag.Count - 1);
            _lastVariant[line.key] = pick;
            return line.clips[pick];
        }

        private IEnumerator ResetPriorityAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds + 0.05f);
            var director = AudioDirector.Instance;
            if (director == null || !director.NarratorIsSpeaking) _currentPriority = int.MinValue;
        }

#if UNITY_EDITOR
        /// <summary>Used by SceneBuilder to populate the prefab without hand-wiring.</summary>
        public void EditorSetLines(List<VoiceLine> newLines)
        {
            lines = newLines;
        }
#endif

        /// <summary>The script as VoiceLine entries, with empty clip arrays ready to fill.</summary>
        public static List<VoiceLine> EntriesFromScript()
        {
            var result = new List<VoiceLine>();
            foreach (var e in VoiceScript.All())
            {
                int n = e.Texts != null ? e.Texts.Length : 0;
                result.Add(new VoiceLine
                {
                    key = e.Key,
                    mode = e.Mode,
                    chance = e.Chance,
                    oncePerSession = e.OncePerSession,
                    cooldownSec = e.CooldownSec,
                    priority = e.Priority,
                    texts = e.Texts != null ? (string[])e.Texts.Clone() : new string[0],
                    clips = new AudioClip[n]
                });
            }
            return result;
        }
    }
}
