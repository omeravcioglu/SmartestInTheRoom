using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Smartest.EditorTools
{
    /// <summary>
    /// The multiplayer-first redesign retired the trick-question bank and the shared puzzle
    /// grid. Their files still compile, so the project builds either way — but nothing uses
    /// them. This deletes them properly (through the asset database, so the .meta files go
    /// too), which is the one thing that can't be done from outside the editor.
    ///
    /// Run it once after the redesign compiles. Afterwards the menu item is a no-op.
    /// </summary>
    public static class RetiredFileCleanup
    {
        private static readonly string[] Retired =
        {
            "Assets/_Smartest/Scripts/Rounds/TrickQuestions.cs",
            "Assets/_Smartest/Scripts/Rounds/GridAnswer.cs",
            "Assets/_Smartest/Scripts/UI/PuzzleGridView.cs",
            "Assets/_Smartest/Scripts/UI/GridCell.cs",
            "Assets/_Smartest/Tests/EditMode/TrickQuestionTests.cs",
            "Assets/_Smartest/Tests/EditMode/PuzzleGenTests.cs",
            "Assets/_Smartest/Resources/TrickQuestions.asset",
            "Assets/_Smartest/Data/RoundLibrary.asset",
        };

        /// <summary>
        /// The 16 clips recorded before the host was rewritten. They were named
        /// &lt;key&gt;.mp3; every line is now &lt;key&gt;_1.mp3 and up. Only offered for deletion
        /// once a replacement exists, so a recording is never thrown away for nothing.
        /// </summary>
        private static readonly string[] LegacyVoiceKeys =
        {
            "menu_welcome", "menu_host_pressed", "menu_join_pressed", "lobby_player_joined",
            "lobby_full", "lobby_solo_start", "game_start", "round_start", "timer_ten",
            "timer_locked", "reveal_everyone_same", "reveal_one_winner", "reveal_all_lost",
            "lead_change", "winner", "winner_last_place",
        };

        [MenuItem("Tools/Smartest/Clean Up Retired Files")]
        public static void Clean()
        {
            var found = new List<string>();
            foreach (string path in Retired)
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) found.Add(path);

            foreach (string key in LegacyVoiceKeys)
            {
                string old = $"{VoicePaths.VoiceDir}/{key}.mp3";
                if (AssetDatabase.LoadAssetAtPath<Object>(old) == null) continue;
                if (VoicePaths.Find(key, 0) == old) continue; // nothing has replaced it yet
                found.Add(old);
            }

            if (found.Count == 0)
            {
                EditorUtility.DisplayDialog("Nothing to clean up",
                    "The retired trick-question and puzzle-grid files are already gone.", "OK");
                return;
            }

            bool ok = EditorUtility.DisplayDialog(
                "Delete retired files?",
                "These are left over from before the multiplayer-first redesign and nothing " +
                "uses them any more:\n\n" + string.Join("\n", found) +
                "\n\nThis cannot be undone.",
                "Delete", "Keep them");
            if (!ok) return;

            int deleted = 0;
            foreach (string path in found)
                if (AssetDatabase.DeleteAsset(path)) deleted++;
                else Debug.LogWarning($"[Smartest] Could not delete {path}.");

            AssetDatabase.Refresh();
            Debug.Log($"[Smartest] Removed {deleted} retired file(s).");
        }
    }
}
