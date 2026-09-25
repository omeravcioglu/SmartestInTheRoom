using Smartest.Core;
using UnityEditor;
using UnityEngine;

namespace Smartest.EditorTools
{
    /// <summary>
    /// Connects the recorded mp3s to the host's lines on the Bootstrap prefab.
    ///
    /// Recording a clip only puts a file on disk; the prefab is what the game actually reads,
    /// so new recordings stay silent until this runs. Voice Studio calls it automatically
    /// when a recording run finishes, and Build Scenes does the same wiring as it rebuilds.
    /// </summary>
    public static class VoiceWiring
    {
        public const string BootstrapPath = "Assets/_Smartest/Resources/Bootstrap.prefab";

        public struct Result
        {
            public bool Ok;
            public int Wired;
            public int Total;
            public string Message;
        }

        public static Result Rewire()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPath) == null)
                return new Result
                {
                    Ok = false,
                    Message = "No Bootstrap prefab yet — run Tools > Smartest > Build Scenes first."
                };

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(BootstrapPath);
                var voice = root.GetComponent<VoiceLines>();
                if (voice == null)
                    return new Result
                    {
                        Ok = false,
                        Message = "The Bootstrap prefab has no VoiceLines component — run Build Scenes."
                    };

                var entries = VoiceLines.EntriesFromScript();
                int wired = 0, total = 0;
                foreach (var e in entries)
                {
                    for (int i = 0; i < e.clips.Length; i++)
                    {
                        total++;
                        e.clips[i] = VoicePaths.Load(e.key, i);
                        if (e.clips[i] != null) wired++;
                    }
                }
                voice.EditorSetLines(entries);
                PrefabUtility.SaveAsPrefabAsset(root, BootstrapPath);

                return new Result
                {
                    Ok = true,
                    Wired = wired,
                    Total = total,
                    Message = wired == total
                        ? $"All {total} clips are in the game."
                        : $"{wired} of {total} clips are in the game; the rest aren't recorded yet."
                };
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/Smartest/Wire Voice Clips")]
        public static void RewireFromMenu()
        {
            var result = Rewire();
            AssetDatabase.SaveAssets();
            Debug.Log("[Smartest] " + result.Message);
            EditorUtility.DisplayDialog(result.Ok ? "Voice clips wired" : "Couldn't wire the clips",
                result.Message, "OK");
        }
    }
}
