using System;
using System.Collections.Generic;
using Smartest.Minigames;
using UnityEngine;

namespace Smartest.Rounds
{
    /// <summary>
    /// What the host picked for the next match: how long it runs, and which minigames are in
    /// the deck. Remembered on the host's machine between sessions; MatchSettings shows it to
    /// the lobby. Pure apart from <see cref="Current"/> and <see cref="Save"/>, so the rules
    /// are unit-testable.
    /// </summary>
    public sealed class HostOptions
    {
        /// <summary>A match length: what it's called and the score that wins it.</summary>
        public readonly struct Length
        {
            public readonly string Name;
            public readonly int Target;

            public Length(string name, int target)
            {
                Name = name;
                Target = target;
            }
        }

        public static readonly Length[] Lengths =
        {
            new Length("Short", 50),
            new Length("Standard", 100),
            new Length("Long", 150),
        };

        public const int StandardLength = 1;

        /// <summary>Fewer games than this and the deck comes round again too soon.</summary>
        public const int MinGamesOn = 3;

        private const string PrefKey = "smartest.host_options";

        private readonly HashSet<string> _off = new HashSet<string>();

        public int LengthIndex { get; private set; } = StandardLength;
        public int Target => Lengths[LengthIndex].Target;

        public void SetLength(int index) => LengthIndex = Mathf.Clamp(index, 0, Lengths.Length - 1);

        public bool IsOn(string minigameId) => !string.IsNullOrEmpty(minigameId) && !_off.Contains(minigameId);

        /// <summary>How many of these games are in the deck.</summary>
        public int CountOn(IReadOnlyList<MinigameEntry> all)
        {
            int n = 0;
            foreach (var e in all) if (e != null && IsOn(e.Id)) n++;
            return n;
        }

        /// <summary>
        /// Puts a game in the deck or takes it out. Taking out one of the last
        /// <see cref="MinGamesOn"/> is refused. Returns whether anything changed.
        /// </summary>
        public bool SetOn(string minigameId, bool on, IReadOnlyList<MinigameEntry> all)
        {
            if (string.IsNullOrEmpty(minigameId) || IsOn(minigameId) == on) return false;
            if (on) return _off.Remove(minigameId);
            if (CountOn(all) <= MinGamesOn) return false;
            return _off.Add(minigameId);
        }

        public void AllOn() => _off.Clear();

        // Saved as "1|maze,stroop": the length, then the games switched OFF, so a game that
        // arrives in an update starts out in the deck.

        public string Serialize()
        {
            var ids = new List<string>(_off);
            ids.Sort(StringComparer.Ordinal);
            return LengthIndex + "|" + string.Join(",", ids);
        }

        /// <summary>Reads what <see cref="Serialize"/> wrote. Anything it can't read gives the defaults.</summary>
        public static HostOptions Parse(string saved, IReadOnlyList<MinigameEntry> all)
        {
            var options = new HostOptions();
            if (string.IsNullOrEmpty(saved)) return options;
            int bar = saved.IndexOf('|');
            if (int.TryParse(bar >= 0 ? saved.Substring(0, bar) : saved, out int length)) options.SetLength(length);
            if (bar >= 0)
            {
                var known = new HashSet<string>();
                foreach (var e in all) if (e != null) known.Add(e.Id);
                foreach (var id in saved.Substring(bar + 1).Split(','))
                {
                    string trimmed = id.Trim();
                    if (known.Contains(trimmed)) options._off.Add(trimmed); // a game since removed is forgotten
                }
            }
            if (options.CountOn(all) < MinGamesOn) options.AllOn();
            return options;
        }

        private static HostOptions s_current;

        /// <summary>This machine's options, read the first time they're needed.</summary>
        public static HostOptions Current =>
            s_current ??= Parse(PlayerPrefs.GetString(PrefKey, string.Empty), MinigameRegistry.All);

        /// <summary>Remember these as this machine's options for next time.</summary>
        public void Save()
        {
            PlayerPrefs.SetString(PrefKey, Serialize());
            PlayerPrefs.Save();
        }

        /// <summary>Tests swap in their own options; returns the ones they replaced.</summary>
        public static HostOptions Replace(HostOptions options)
        {
            var was = s_current;
            s_current = options;
            return was;
        }
    }
}
