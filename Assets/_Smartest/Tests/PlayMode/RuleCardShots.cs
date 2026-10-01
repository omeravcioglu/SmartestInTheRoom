using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Smartest.Core;
using Smartest.Minigames;
using Smartest.Net;
using Smartest.Rounds;
using Smartest.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Smartest.Tests
{
    /// <summary>
    /// Pictures of each minigame's rule card with its demo running: three frames per game, a
    /// couple of seconds apart, so the demo's moves can be checked by eye. All games, or just
    /// the ones in $SMARTEST_CARD_ONLY ("herd,simon").
    ///
    /// Explicit: it writes files. Run it on purpose:
    ///   Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter RuleCardShots
    /// Pictures go to $SMARTEST_SHOTS, or Previews/cards next to Assets.
    /// </summary>
    [Explicit("Renders rule cards to PNG; run it on purpose.")]
    public class RuleCardShots
    {
        private static readonly float[] Frames = { 1.5f, 3.5f, 5.5f };

        private NetworkMode _savedMode;
        private string _dir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var config = GameBootstrap.ConfigOrDefault;
            _savedMode = config.networkMode;
            config.networkMode = NetworkMode.Local;
            _dir = Environment.GetEnvironmentVariable("SMARTEST_SHOTS");
            if (string.IsNullOrEmpty(_dir)) _dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Previews", "cards"));
            Directory.CreateDirectory(_dir);
            if (NetSession.Instance != null) NetSession.Instance.LocalPlayerName = "Deniz";
            SceneManager.LoadScene(NetSession.MenuSceneName);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (NetSession.Instance != null)
            {
                var leave = NetSession.Instance.LeaveAsync();
                float t = 0f;
                while (!leave.IsCompleted && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
            }
            GameBootstrap.ConfigOrDefault.networkMode = _savedMode;
        }

        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator Cards()
        {
            // A picture tool, not a check: an editor package grumbling in the log (a copied
            // project's first shader import) shouldn't stop the pictures.
            LogAssert.ignoreFailingMessages = true;
            yield return Wait(0.5f);
            var menu = Object.FindAnyObjectByType<MenuUI>();
            Assert.IsNotNull(menu, "the Menu scene should have a MenuUI");
            typeof(MenuUI).GetMethod("OnHostClicked", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Invoke(menu, null);
            var lobby = (LobbyUI)typeof(MenuUI).GetField("lobby", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .GetValue(menu);
            yield return WaitFor(() => lobby.IsShown, 15f, "the lobby");
            NetSession.Instance.StartGame();
            yield return WaitFor(() => GameState.Instance != null && GameState.Instance.IsSpawned
                                       && GameState.Instance.Phase.Value == GamePhase.RoundIntro, 20f, "the first round");
            yield return Wait(0.5f);
            // Hold the host's clock so the card stays up for as long as the pictures take.
            typeof(GameState).GetField("_phaseEnd", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(GameState.Instance, double.MaxValue);

            var ui = Object.FindAnyObjectByType<GameUI>();
            var stage = (MinigameStage)typeof(GameUI).GetField("minigameStage", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(ui);
            var round = (RoundPanel)typeof(GameUI).GetField("roundPanel", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(ui);
            if (round != null) round.HideInstant();
            stage.ShowInstant();

            string only = Environment.GetEnvironmentVariable("SMARTEST_CARD_ONLY");
            var wanted = string.IsNullOrEmpty(only) ? null : new HashSet<string>(only.Split(','));
            foreach (var entry in MinigameRegistry.All)
            {
                if (wanted != null && !wanted.Contains(entry.Id)) continue;
                stage.ShowIntro(entry, entry.Title, entry.Rule, 4);
                float t = 0f;
                foreach (float at in Frames)
                {
                    while (t < at) { t += Time.unscaledDeltaTime; yield return null; }
                    yield return Shot($"card-{entry.Id}-{at:0.0}");
                }
            }
        }

        /// <summary>Every canvas in front of a camera for one frame, rendered at 1920 × 1080.</summary>
        private IEnumerator Shot(string name)
        {
            var camGo = new GameObject("ShotCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Paper;
            cam.orthographic = true;
            cam.enabled = false;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            cam.targetTexture = rt;
            var changed = new List<(Canvas c, RenderMode mode, Camera cam, float plane)>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas) continue;
                changed.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 10f;
            }
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            foreach (var (c, mode, oldCam, plane) in changed)
            {
                if (c == null) continue;
                c.renderMode = mode;
                c.worldCamera = oldCam;
                c.planeDistance = plane;
            }
            Object.Destroy(tex);
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(camGo);
        }

        private static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string what)
        {
            float waited = 0f;
            while (!condition())
            {
                waited += Time.unscaledDeltaTime;
                if (waited > seconds) Assert.Fail("Timed out waiting for " + what + ".");
                yield return null;
            }
        }
    }
}
