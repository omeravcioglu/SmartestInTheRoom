using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Smartest.Core;
using Smartest.Minigames;
using Smartest.Rounds;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Smartest.EditorTools
{
    /// <summary>
    /// One-click Windows release. Checks the project is ready to ship, builds a
    /// non-development player into a fresh folder under Build/Release/ (never over an older
    /// build), moves the Burst debug symbols out of the shipping folder and adds a short
    /// README. The font licences are copied by <see cref="ShipLicences"/>, which runs for
    /// every standalone build, not just this one.
    ///
    /// Without the editor open:
    ///   Unity.exe -batchmode -quit -projectPath &lt;project&gt; -executeMethod Smartest.EditorTools.ReleaseBuilder.BuildWindowsBatch -logFile build.log
    /// Exit code 0 = built, 1 = not ready or the build failed (the reasons are in the log).
    /// </summary>
    public static class ReleaseBuilder
    {
        private const string ExeName = "SmartestInTheRoom";
        private const string MenuScenePath = "Assets/_Smartest/Scenes/Menu.unity";
        private const string GameScenePath = "Assets/_Smartest/Scenes/Game.unity";
        private const string LibraryPath = "Assets/_Smartest/Resources/RoundLibrary.asset";

        [MenuItem("Tools/Smartest/Build Windows Release")]
        public static void BuildWindowsMenu()
        {
            var problems = Check(out var warnings);
            foreach (var w in warnings) Debug.LogWarning("[Release] " + w);
            if (problems.Count > 0)
            {
                EditorUtility.DisplayDialog("Not ready to build", string.Join("\n\n", problems), "OK");
                return;
            }
            if (BuildWindows(out string folder)) EditorUtility.RevealInFinder(Path.Combine(folder, ExeName + ".exe"));
        }

        public static void BuildWindowsBatch()
        {
            bool ok = false;
            try
            {
                var problems = Check(out var warnings);
                foreach (var w in warnings) Debug.LogWarning("[Release] " + w);
                foreach (var p in problems) Debug.LogError("[Release] " + p);
                ok = problems.Count == 0 && BuildWindows(out _);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Blocking problems (the build would be wrong) and warnings (worth fixing before a public release).</summary>
        public static List<string> Check(out List<string> warnings)
        {
            var problems = new List<string>();
            warnings = new List<string>();

            // Only the two real scenes ship: no template leftovers.
            var enabled = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) enabled.Add(s.path);
            if (enabled.Count != 2 || enabled[0] != MenuScenePath || enabled[1] != GameScenePath)
                problems.Add($"The build should be exactly Menu then Game, but it is: {string.Join(", ", enabled)}. Run Tools > Smartest > Build Scenes.");

            // Every registered minigame needs its generated round asset.
            var library = AssetDatabase.LoadAssetAtPath<RoundLibrary>(LibraryPath);
            if (library == null) problems.Add("There is no RoundLibrary. Run Tools > Smartest > Build Scenes.");
            else
            {
                var have = new HashSet<string>();
                foreach (var r in library.rounds) if (r != null && r.IsMinigame) have.Add(r.minigameId);
                var missing = new List<string>();
                foreach (var e in MinigameRegistry.All) if (!have.Contains(e.Id)) missing.Add(e.Id);
                if (missing.Count > 0)
                    problems.Add($"{missing.Count} minigame(s) have no round asset yet ({string.Join(", ", missing)}). Run Tools > Smartest > Build Scenes.");
            }

            foreach (var licence in ShipLicences.Files)
                if (!File.Exists(licence)) problems.Add("A font licence is missing: " + licence);

            if (PlayerSettings.companyName == "DefaultCompany")
                warnings.Add("Company Name is still DefaultCompany (Project Settings > Player). It names the save folder and shows in the file properties.");
            if (EditorUserBuildSettings.development)
                warnings.Add("Development Build is ticked in Build Settings; this release builds without it anyway.");

            int silent = MissingVoiceLines();
            if (silent > 0)
                warnings.Add($"{silent} voice line(s) have no clip yet and will be silent (Tools > Smartest > Voice Studio).");
            return problems;
        }

        private static int MissingVoiceLines()
        {
            int missing = 0;
            foreach (var e in VoiceScript.All())
            {
                if (e.Texts == null) continue;
                for (int i = 0; i < e.Texts.Length; i++)
                    if (VoicePaths.Find(e.Key, i) == null) missing++;
            }
            return missing;
        }

        public static bool BuildWindows(out string folder)
        {
            string version = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion) ? "0" : PlayerSettings.bundleVersion.Trim();
            string releases = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build", "Release"));
            folder = FreshFolder(releases, $"{ExeName}-{version}-win64");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, GameScenePath },
                locationPathName = Path.Combine(folder, ExeName + ".exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Release] Build {summary.result}: {summary.totalErrors} error(s). See the log above.");
                return false;
            }

            // Burst's debug symbols are for us, not for players: keep them next to the release.
            // (The folder is named after the product name, not the exe.)
            foreach (string symbols in Directory.GetDirectories(folder, "*_BurstDebugInformation_DoNotShip"))
            {
                string keep = FreshFolder(Path.Combine(releases, "_symbols"), Path.GetFileName(folder));
                Directory.Delete(keep);
                Directory.Move(symbols, keep);
            }
            File.WriteAllText(Path.Combine(folder, "README.txt"), Readme(version), new UTF8Encoding(false));

            long bytes = 0;
            foreach (var f in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) bytes += new FileInfo(f).Length;
            Debug.Log($"[Release] Built {PlayerSettings.productName} {version} for Windows into {folder} ({bytes / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0}s).");
            return true;
        }

        /// <summary>A new, empty folder: the name, or the name with -2, -3... if it's taken.</summary>
        private static string FreshFolder(string parent, string name)
        {
            Directory.CreateDirectory(parent);
            string path = Path.Combine(parent, name);
            for (int n = 2; Directory.Exists(path); n++) path = Path.Combine(parent, $"{name}-{n}");
            Directory.CreateDirectory(path);
            return path;
        }

        private static string Readme(string version)
        {
            int max = 8;
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Smartest/Data/GameConfig.asset");
            if (config != null) max = config.maxPlayers;
            return
$@"{PlayerSettings.productName}  v{version}

A party game of quick minigames for up to {max} players: online, on the same Wi-Fi, or on one PC.

HOW TO PLAY
  Host   Starts a lobby and shows a join code. Send the code to your friends.
  Join   Type in the code (or the host's IP on the same Wi-Fi).
  Every round is a minigame. Everyone plays the same level at the same time;
  fail, or come last, and you're out. Last one standing takes the points.
  First to the target score is the smartest in the room.

  Mouse and keyboard. There's no in-game chat, so talk on Discord.

CREDITS
  Fonts: Archivo, Atkinson Hyperlegible Next, Atkinson Hyperlegible Mono and
  Liberation Sans, all under the SIL Open Font License 1.1. The licence texts
  are in the Licenses folder.
  Made with Unity.
";
        }
    }

    /// <summary>
    /// The fonts are under the SIL Open Font License, which asks for the licence to travel
    /// with the fonts. Every standalone build gets a Licenses folder next to the executable.
    /// </summary>
    public class ShipLicences : IPostprocessBuildWithReport
    {
        public static readonly string[] Files =
        {
            "Assets/_Smartest/Fonts/Archivo-OFL.txt",
            "Assets/_Smartest/Fonts/AtkinsonHyperlegibleNext-OFL.txt",
            "Assets/_Smartest/Fonts/AtkinsonHyperlegibleMono-OFL.txt",
            "Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt",
        };

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platformGroup != BuildTargetGroup.Standalone) return;
            string dir = Path.GetDirectoryName(report.summary.outputPath);
            if (string.IsNullOrEmpty(dir)) return;
            string licences = Path.Combine(dir, "Licenses");
            Directory.CreateDirectory(licences);
            foreach (string file in Files)
            {
                if (!File.Exists(file)) { Debug.LogWarning("[Release] Licence not found: " + file); continue; }
                File.Copy(file, Path.Combine(licences, Path.GetFileName(file)), true);
            }
        }
    }
}
