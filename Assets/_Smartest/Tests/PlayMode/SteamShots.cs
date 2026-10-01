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
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Smartest.Tests
{
    /// <summary>
    /// Store-page screenshots. The front page, then a match in full swing staged on the real
    /// Game scene: eight seats, the race to 100, and a level of each chosen minigame actually
    /// running. Each level is started as a player still in sees it (no spectator ribbon, the
    /// scoreline tabs showing) and then handed to the game's own auto-play, the one a knocked-out
    /// player watches, so there is something happening in every shot. Then a knock-out and the
    /// winner. Everything on screen is the game's own UI and the game's own code; only the other
    /// seven players are stand-ins. Rendered at 3840 × 2160.
    ///
    /// Explicit: it writes files. Run it on purpose:
    ///   Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter SteamShots
    /// Pictures go to $SMARTEST_SHOTS, or Steam/Screenshots/raw next to Assets.
    /// </summary>
    [Explicit("Renders store screenshots to PNG; run it on purpose.")]
    public class SteamShots
    {
        private const int Width = 3840, Height = 2160;
        private static readonly string[] Names = { "Mert", "Ayşe", "Deniz", "Burak", "Can", "Elif", "Zeynep", "Kerem" };
        private const int You = 2;

        /// <summary>One staged moment of a match.</summary>
        private class Moment
        {
            public string File;
            public string Game;
            public int Round, Level, Seed;
            public float At;              // seconds into the level when the picture is taken
            public int[] Scores;
            public int[] OutAt;           // per seat: the level they went out at, 0 = still in
            public int[] Done = new int[0]; // seats that have already cleared this level
            public string Caption;        // the host's line, or null for none
        }

        // Scores along the match: early, middle, late (Ayşe is closing on 100).
        private static readonly int[] Early = { 30, 35, 25, 15, 20, 10, 5, -5 };
        private static readonly int[] Middle = { 64, 72, 51, 45, 42, 38, 26, 12 };
        private static readonly int[] Late = { 83, 92, 88, 64, 57, 49, 33, 21 };

        private static readonly Moment[] Moments =
        {
            new Moment { File = "traffic", Game = "traffic", Round = 5, Level = 6, Seed = 5150, At = 4.5f, Scores = Middle,
                OutAt = new[] { 0, 0, 0, 5, 0, 4, 3, 2 }, Caption = "Traffic. No crashes. No road rage. You're the lights now." },
            new Moment { File = "pop-the-lock", Game = "pop_lock", Round = 9, Level = 5, Seed = 9595, At = 3.0f, Scores = Late,
                OutAt = new[] { 0, 0, 0, 0, 4, 0, 3, 2 }, Caption = null },
            // The first shot is struck about 1.3 s into the level and is in the air for a third of a
            // second; the stage's lead-in adds a little before the level's clock starts.
            new Moment { File = "penalty", Game = "penalty", Round = 9, Level = 4, Seed = 909, At = 1.75f, Scores = Late,
                OutAt = new[] { 0, 0, 0, 0, 3, 0, 2, 1 }, Caption = "Penalty. Send the keeper the wrong way. He's watching your eyes." },
            new Moment { File = "rhythm", Game = "rhythm", Round = 4, Level = 6, Seed = 4040, At = 3.2f, Scores = Early,
                OutAt = new[] { 0, 0, 0, 0, 5, 4, 3, 0 }, Caption = null },
            new Moment { File = "herd", Game = "herd", Round = 6, Level = 6, Seed = 4242, At = 2.6f, Scores = Middle,
                OutAt = new[] { 0, 0, 0, 3, 5, 0, 4, 2 }, Done = new[] { 1 }, Caption = "Herd. You're the sheepdog. Sheep are not clever. Be cleverer." },
            new Moment { File = "tightrope", Game = "tightrope", Round = 8, Level = 5, Seed = 8080, At = 2.0f, Scores = Late,
                OutAt = new[] { 0, 0, 0, 4, 0, 3, 0, 2 }, Caption = "Tightrope. Keep them up. Whatever you do, don't look down." },
            new Moment { File = "fishing", Game = "fishing", Round = 3, Level = 5, Seed = 3131, At = 4.0f, Scores = Early,
                OutAt = new[] { 0, 0, 0, 0, 4, 0, 3, 0 }, Caption = null },
            new Moment { File = "juggle", Game = "juggle", Round = 7, Level = 4, Seed = 7070, At = 3.4f, Scores = Middle,
                OutAt = new[] { 0, 0, 0, 3, 0, 0, 2, 0 }, Caption = null },
            new Moment { File = "flap", Game = "flap", Round = 6, Level = 5, Seed = 6565, At = 4.0f, Scores = Middle,
                OutAt = new[] { 0, 0, 0, 4, 0, 3, 0, 2 }, Caption = null },
            new Moment { File = "maze-dark", Game = "maze", Round = 7, Level = 7, Seed = 777, At = 3.0f, Scores = Middle,
                OutAt = new[] { 6, 0, 0, 0, 4, 5, 3, 2 }, Caption = null },
            new Moment { File = "maze", Game = "maze", Round = 2, Level = 4, Seed = 2424, At = 3.0f, Scores = Early,
                OutAt = new[] { 0, 0, 0, 0, 0, 3, 0, 0 }, Caption = null },
            new Moment { File = "slice", Game = "slice", Round = 3, Level = 4, Seed = 303, At = 1.2f, Scores = Early,
                OutAt = new[] { 0, 0, 0, 0, 0, 3, 0, 2 }, Caption = null },
            new Moment { File = "statues", Game = "statues", Round = 4, Level = 4, Seed = 4141, At = 3.0f, Scores = Early,
                OutAt = new[] { 0, 0, 0, 0, 3, 0, 0, 2 }, Caption = null },
            new Moment { File = "stack", Game = "stack", Round = 6, Level = 5, Seed = 606, At = 4.0f, Scores = Middle,
                OutAt = new[] { 0, 0, 0, 0, 4, 0, 3, 2 }, Done = new[] { 1, 5 }, Caption = null },
        };

        private NetworkMode _savedMode;
        private string _dir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var config = GameBootstrap.ConfigOrDefault;
            _savedMode = config.networkMode;
            config.networkMode = NetworkMode.Local;
            _dir = Environment.GetEnvironmentVariable("SMARTEST_SHOTS");
            if (string.IsNullOrEmpty(_dir)) _dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Steam", "Screenshots", "raw"));
            Directory.CreateDirectory(_dir);
            if (NetSession.Instance != null) NetSession.Instance.LocalPlayerName = Names[You];
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
        [Timeout(900000)]
        public IEnumerator Shots()
        {
            yield return Wait(1.0f);
            yield return Shot("front-page");

            // Into a match, then hold the host's clock on the first rule card so nothing moves on
            // by itself while each moment is staged on top.
            var menu = Object.FindAnyObjectByType<MenuUI>();
            Assert.IsNotNull(menu, "the Menu scene should have a MenuUI");
            Call(menu, "OnHostClicked");
            var lobby = Get<LobbyUI>(menu, "lobby");
            yield return WaitFor(() => lobby.IsShown, 15f, "the lobby");
            yield return Wait(0.5f);
            NetSession.Instance.StartGame();
            yield return WaitFor(() => GameState.Instance != null && GameState.Instance.IsSpawned
                                       && GameState.Instance.Phase.Value == GamePhase.RoundIntro, 20f, "the first round");
            yield return Wait(1.0f);
            var gs = GameState.Instance;
            var phaseEnd = typeof(GameState).GetField("_phaseEnd", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(phaseEnd, "GameState._phaseEnd not found");
            phaseEnd.SetValue(gs, double.MaxValue);

            var ui = Object.FindAnyObjectByType<GameUI>();
            Assert.IsNotNull(ui, "the Game scene should have a GameUI");
            var stage = Get<MinigameStage>(ui, "minigameStage");
            var round = Get<RoundPanel>(ui, "roundPanel");
            var rail = Get<SeatRail>(ui, "seatRail");
            var masthead = Get<Masthead>(ui, "masthead");
            var caption = Get<HostCaption>(ui, "hostCaption");
            var hud = Get<CanvasGroup>(ui, "hud");
            Assert.IsNotNull(stage, "GameUI.minigameStage not found");
            if (rail != null) rail.enabled = false;
            if (round != null) round.HideInstant();
            stage.ShowInstant();
            var monos = Monogram.ForAll(Names);

            foreach (var m in Moments)
            {
                var entry = MinigameRegistry.Get(m.Game);
                if (entry == null) { Debug.LogWarning("[SteamShots] no minigame " + m.Game); continue; }
                yield return Table(rail, masthead, monos, m.Round, m.Level, m.Scores, m.OutAt, m.Done, null);

                int still = 0;
                var alive = new List<(string, bool)>();
                for (int i = 0; i < Names.Length; i++)
                    if (m.OutAt[i] == 0) { still++; alive.Add((monos[i], i == You)); }
                stage.StartLevel(entry, m.Level, m.Seed, true, still, 0.01f, entry.LevelSeconds, false);
                // Built as a player still in sees it; played by the game itself, the way
                // someone watching sees it played. Nothing it does reaches the host.
                var view = Get<MinigameView>(stage, "_view");
                if (view != null) view.Interactive = false;
                stage.SetAlive(alive);
                if (caption != null)
                {
                    if (string.IsNullOrEmpty(m.Caption)) caption.Hide();
                    else caption.Say(m.Caption, 60f);
                }
                yield return Wait(m.At);
                yield return Shot("level-" + m.File);
            }

            // A knock-out: the level ends and two seats go dark.
            {
                var entry = MinigameRegistry.Get("simon");
                var outAt = new[] { 0, 0, 0, 0, 0, 3, 2, 1 };
                yield return Table(rail, masthead, monos, 6, 4, Middle, outAt, new int[0], new[] { 3, 4 });
                var alive = new List<(string, bool)>();
                for (int i = 0; i < Names.Length; i++) if (outAt[i] == 0) alive.Add((monos[i], i == You));
                stage.StartLevel(entry, 4, 4444, true, alive.Count, 0.01f, entry.LevelSeconds, false);
                var view = Get<MinigameView>(stage, "_view");
                if (view != null) view.Interactive = false;
                stage.SetAlive(alive);
                yield return Wait(2.5f);
                stage.ShowLevelResult(GameState.OutcomeEliminated, $"OUT: {Names[3]} and {Names[4]}",
                    new List<(string, string)> { (Names[3], monos[3]), (Names[4], monos[4]) }, 3, false);
                if (caption != null) caption.Say("Out! You're out. It's fine. It's fine.", 60f);
                yield return Wait(0.6f);
                yield return Shot("knockout");
                stage.EndMinigame();
            }

            // The winner, with the table's final standings.
            var winnerPanel = Get<WinnerPanel>(ui, "winnerPanel");
            if (winnerPanel != null)
            {
                var finals = new[] { 88, 104, 91, 64, 57, 49, 33, 21 };
                var players = new List<PlayerData>();
                var standins = new List<GameObject>();
                for (int i = 0; i < Names.Length; i++)
                {
                    if (i == You && PlayerData.Local != null)
                    {
                        PlayerData.Local.Score.Value = finals[i];
                        players.Add(PlayerData.Local);
                        continue;
                    }
                    // Never spawned: the panel only reads a name and a score.
                    var go = new GameObject("Standin" + i);
                    standins.Add(go);
                    var p = go.AddComponent<PlayerData>();
                    p.PlayerName.Value = new FixedString32Bytes(Names[i]);
                    p.Score.Value = finals[i];
                    players.Add(p);
                }
                if (caption != null) caption.Hide();
                stage.HideInstant();
                if (hud != null) hud.alpha = 0f;
                winnerPanel.ShowWinner(players[1], players, true, 14,
                    "A HUNDRED! That's the game! Smartest in the room, officially. Put it on a résumé.");
                winnerPanel.ShowInstant();
                yield return Wait(2.5f);
                yield return Shot("winner");
                foreach (var go in standins) Object.Destroy(go);
            }
        }

        // ------------------------------------------------------------------
        // The table: seats, the race to 100, the round
        // ------------------------------------------------------------------

        private static IEnumerator Table(SeatRail rail, Masthead masthead, List<string> monos, int round, int level,
            int[] scores, int[] outAt, int[] done, int[] outNow)
        {
            if (masthead != null) masthead.SetRound(round, true, false);
            int top = int.MinValue;
            foreach (int s in scores) top = Mathf.Max(top, s);

            if (rail != null)
            {
                var root = (RectTransform)rail.transform;
                for (int i = root.childCount - 1; i >= 0; i--) Object.Destroy(root.GetChild(i).gameObject);
                yield return null;
                for (int i = 0; i < Names.Length; i++)
                {
                    int rank = 1;
                    foreach (int s in scores) if (s > scores[i]) rank++;
                    var card = SeatCard.Create(root, "Seat" + i);
                    card.Rect.At(i * (SeatCard.Width + 16f), 0f, SeatCard.Width, SeatCard.Height);
                    card.SetIdentity(Names[i], monos[i], i == 0 ? "HOST" : i == You ? "YOU" : "", i == You);
                    card.SetScore(scores[i]);
                    card.SetRank("No." + rank);
                    card.SetLeader(scores[i] == top);
                    card.SetBand(null, Palette.Paper2, Palette.Ink, null);
                    card.SetDelta(null, false);
                    card.SetBigStamp(null);
                    card.SetDim(false);
                    bool goingOut = outNow != null && Array.IndexOf(outNow, i) >= 0;
                    if (goingOut)
                    {
                        card.SetStatus(SeatCard.Status.None);
                        card.SetBigStamp("OUT");
                        card.SetDim(true);
                    }
                    else if (outAt[i] > 0)
                    {
                        card.SetStatus(SeatCard.Status.OutEarlier, "out · level " + outAt[i]);
                        card.SetDim(true);
                    }
                    else if (outNow != null) card.SetStatus(SeatCard.Status.StillIn);
                    else card.SetStatus(Array.IndexOf(done, i) >= 0 ? SeatCard.Status.Done : SeatCard.Status.Playing);
                }
            }

            var race = masthead != null ? masthead.Race : null;
            if (race != null)
            {
                var entries = new List<RaceTrack.Entry>();
                for (int i = 0; i < Names.Length; i++)
                    entries.Add(new RaceTrack.Entry { Id = (ulong)(100 + i), Mono = monos[i], Score = scores[i], Local = i == You });
                race.ClearMoves();
                race.Refresh(entries, 0f);
            }
        }

        // ------------------------------------------------------------------
        // Helpers (as in ScreenshotTour, at store resolution)
        // ------------------------------------------------------------------

        /// <summary>
        /// Overlay canvases never reach a camera, so for one frame every canvas is put in front
        /// of a camera that renders into a texture at store resolution.
        /// </summary>
        private IEnumerator Shot(string name)
        {
            var camGo = new GameObject("ShotCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Paper;
            cam.orthographic = true;
            cam.enabled = false;
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
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
            yield return null; // let the scaler and layout catch up with the new size
            Canvas.ForceUpdateCanvases();
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Debug.Log($"[SteamShots] {name}.png");

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

        private static object Call(object target, string method, params object[] args)
        {
            var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(m, $"{target.GetType().Name}.{method} not found");
            return m.Invoke(target, args);
        }

        private static T Get<T>(object target, string field) where T : class
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f != null ? f.GetValue(target) as T : null;
        }
    }
}
