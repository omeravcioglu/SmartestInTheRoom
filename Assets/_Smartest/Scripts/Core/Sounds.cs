using System.Collections.Generic;
using UnityEngine;

namespace Smartest.Core
{
    /// <summary>
    /// Sound effects, generated in code the first time each one is asked for. No audio files
    /// to source, nothing to import, and every blip is in the same small family so the game
    /// sounds like one thing. Drop real samples in later and only this file changes.
    /// </summary>
    public static class Sounds
    {
        public enum Kind
        {
            Click,      // any button
            LockIn,     // answer submitted
            Good,       // correct / cleared a level
            Bad,        // wrong / failed
            Tick,       // last seconds of a countdown
            Go,         // the level starts
            Out,        // knocked out of a minigame
            Fanfare     // somebody won
        }

        private const int SampleRate = 44100;
        private static readonly Dictionary<Kind, AudioClip> s_cache = new Dictionary<Kind, AudioClip>();

        public static void Play(Kind kind, float volume = 1f)
        {
            var director = AudioDirector.Instance;
            if (director == null) return;
            director.PlaySfx(Get(kind), volume);
        }

        public static AudioClip Get(Kind kind)
        {
            if (s_cache.TryGetValue(kind, out var cached) && cached != null) return cached;
            var clip = Build(kind);
            s_cache[kind] = clip;
            return clip;
        }

        private static AudioClip Build(Kind kind)
        {
            switch (kind)
            {
                case Kind.Click: return Blip("sfx_click", 0.07f, 660f, 660f, 0.25f);
                case Kind.LockIn: return Blip("sfx_lockin", 0.13f, 520f, 780f, 0.30f);
                case Kind.Good: return Arpeggio("sfx_good", new[] { 660f, 880f, 1320f }, 0.075f, 0.30f);
                case Kind.Bad: return Blip("sfx_bad", 0.22f, 240f, 110f, 0.32f, square: true);
                case Kind.Tick: return Blip("sfx_tick", 0.05f, 1200f, 1200f, 0.18f);
                case Kind.Go: return Arpeggio("sfx_go", new[] { 520f, 780f }, 0.09f, 0.34f);
                case Kind.Out: return Arpeggio("sfx_out", new[] { 440f, 330f, 220f }, 0.10f, 0.32f, square: true);
                case Kind.Fanfare: return Arpeggio("sfx_fanfare", new[] { 523f, 659f, 784f, 1047f }, 0.12f, 0.34f);
                default: return Blip("sfx", 0.06f, 440f, 440f, 0.2f);
            }
        }

        /// <summary>One tone that slides from startHz to endHz, with a soft attack and decay.</summary>
        private static AudioClip Blip(string name, float seconds, float startHz, float endHz, float gain,
            bool square = false)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            var data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float hz = Mathf.Lerp(startHz, endHz, t);
                phase += hz / SampleRate;
                float wave = square
                    ? (Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f)
                    : Mathf.Sin(phase * 2f * Mathf.PI);
                data[i] = wave * Envelope(t) * gain;
            }
            return FromSamples(name, data);
        }

        /// <summary>A short run of notes — used for the "you did it" and "you didn't" stings.</summary>
        private static AudioClip Arpeggio(string name, float[] notes, float noteSeconds, float gain,
            bool square = false)
        {
            int perNote = Mathf.Max(1, Mathf.RoundToInt(noteSeconds * SampleRate));
            var data = new float[perNote * notes.Length];
            for (int n = 0; n < notes.Length; n++)
            {
                float phase = 0f;
                for (int i = 0; i < perNote; i++)
                {
                    float t = (float)i / perNote;
                    phase += notes[n] / SampleRate;
                    float wave = square
                        ? (Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f)
                        : Mathf.Sin(phase * 2f * Mathf.PI);
                    data[n * perNote + i] = wave * Envelope(t) * gain;
                }
            }
            return FromSamples(name, data);
        }

        /// <summary>Quick fade in, slow fade out. Without this every blip clicks at the edges.</summary>
        private static float Envelope(float t)
        {
            const float attack = 0.06f;
            if (t < attack) return t / attack;
            float rest = (t - attack) / (1f - attack);
            return Mathf.Pow(1f - rest, 1.6f);
        }

        private static AudioClip FromSamples(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
