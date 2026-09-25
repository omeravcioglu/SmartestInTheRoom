using System.IO;
using UnityEditor;
using UnityEngine;

namespace Smartest.EditorTools
{
    /// <summary>Where a voice clip lives, and how to find one that's already there.</summary>
    public static class VoicePaths
    {
        public const string VoiceDir = "Assets/_Smartest/Audio/Voice";
        public const string MusicDir = "Assets/_Smartest/Audio/Music";

        private static readonly string[] Extensions = { ".mp3", ".wav", ".ogg" };

        /// <summary>Variant 0 of "lead_change" is Audio/Voice/lead_change_1.mp3.</summary>
        public static string Mp3(string key, int variantIndex)
            => $"{VoiceDir}/{key}_{variantIndex + 1}.mp3";

        /// <summary>The existing file for this variant in any supported format, or null.</summary>
        public static string Find(string key, int variantIndex)
        {
            string stem = $"{VoiceDir}/{key}_{variantIndex + 1}";
            foreach (string ext in Extensions)
                if (File.Exists(stem + ext)) return stem + ext;

            // A single-variant line may have been recorded by hand as just <key>.mp3.
            if (variantIndex == 0)
            {
                string plain = $"{VoiceDir}/{key}";
                foreach (string ext in Extensions)
                    if (File.Exists(plain + ext)) return plain + ext;
            }
            return null;
        }

        public static AudioClip Load(string key, int variantIndex)
        {
            string path = Find(key, variantIndex);
            return path == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>First audio file in Audio/Music, used as the background loop. Null if empty.</summary>
        public static AudioClip FindMusicLoop()
        {
            if (!AssetDatabase.IsValidFolder(MusicDir)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicDir }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) return clip;
            }
            return null;
        }
    }
}
