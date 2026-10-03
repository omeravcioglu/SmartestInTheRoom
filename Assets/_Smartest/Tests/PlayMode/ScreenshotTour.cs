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
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Smartest.Tests
{
    /// <summary>
    /// Plays a solo Local match and saves a picture of every screen on the way: menu, join,
    /// sound, lobby, each kind of round, a minigame from rule card to ranking, every other
    /// minigame live (started on the same stage while the first one plays), and the winner.
    /// Then it fakes the states a solo match can't reach (a full seat rail, someone going out,
    /// a tie-break) straight into the same scene.
    ///
    /// Explicit: it takes a few minutes and writes files. Run it on purpose:
    ///   Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter ScreenshotTour
    /// Pictures go to $SMARTEST_SHOTS, or Previews/ next to Assets. To look at a few games
    /// only, set $SMARTEST_TOUR_ONLY to their ids ("flap,goalie"): the tour stops after them.
    /// </summary>
    [Explicit("Renders every screen to PNG; run it on purpose.")]
    public class ScreenshotTour
    {
        /// <summary>A live level is photographed this long after its clock starts (lead-in included).</summary>
        private const float LevelShotAt = 4.5f;

        /// <summary>Games whose screen is empty by then, photographed this long after GO instead.</summary>
        private static readonly Dictionary<string, float> ShotAfterGo = new Dictionary<string, float>
        {
            { "bullseye", 1f },     // the level-1 target shows for 2 s, then it's gone
            { "memory_boxes", 1f }, // the boxes go dark at 2.5 s, the very frame of the usual shot
            { "flash_point", 1.3f }, // the level-1 dot shows somewhere in 0.5-2.1 s, always at 1.3
            { "traffic", 3.5f },    // cars drive in from off screen; by now some are at the crossing
        };

        private static float ShotAt(string game, float lead) =>
            game != null && ShotAfterGo.TryGetValue(game, out float s) ? lead + s : LevelShotAt;

        private NetworkMode _savedMode;
        private string _dir;
        private readonly HashSet<string> _taken = new HashSet<string>();
        private bool _everyGame;
        private HashSet<string> _only;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var config = GameBootstrap.ConfigOrDefault;
            _savedMode = config.networkMode;
            config.networkMode = NetworkMode.Local;
            _dir = Environment.GetEnvironmentVariable("SMARTEST_SHOTS");
            if (string.IsNullOrEmpty(_dir)) _dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Previews"));
            Directory.CreateDirectory(_dir);
            string only = Environment.GetEnvironmentVariable("SMARTEST_TOUR_ONLY");
            _only = null;
            if (!string.IsNullOrWhiteSpace(only))
            {
                _only = new HashSet<string>();
                foreach (var id in only.Split(',')) _only.Add(id.Trim());
            }
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
        [Timeout(900000)]
        public IEnumerator Tour()
        {
            yield return Wait(0.8f);
            yield return Shot("01-menu");

            var menu = Object.FindAnyObjectByType<MenuUI>();
            Assert.IsNotNull(menu, "the Menu scene should have a MenuUI");
            Call(menu, "OnJoinClicked");
            yield return Wait(0.6f);
            yield return Shot("02-join");
            Call(menu, "GoTo", Get<Panel>(menu, "mainPanel"));
            yield return Wait(0.5f);

            var settings = Object.FindAnyObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            settings.Toggle();
            yield return Wait(0.6f);
            yield return Shot("03-settings");
            settings.Close();
            yield return Wait(0.4f);

            Call(menu, "OnHostClicked");
            var lobby = Get<LobbyUI>(menu, "lobby");
            yield return WaitFor(() => lobby.IsShown, 15f, "the lobby");
            yield return Wait(0.8f);
            yield return Shot("04-lobby");

            // The host's minigame list, with a few games out to show both looks. On borrowed
            // options, and hidden rather than closed: closing would save them on this machine.
            var picker = Get<MinigamePicker>(lobby, "picker");
            if (picker != null)
            {
                var shown = new HostOptions();
                foreach (var id in new[] { "dodge", "fishing", "maze", "putt", "stroop", "type_it" })
                    shown.SetOn(id, false, MinigameRegistry.All);
                var mine = HostOptions.Replace(shown);
                if (MatchSettings.Instance != null) MatchSettings.Instance.ServerApply(shown);
                picker.Open();
                yield return Wait(0.6f);
                yield return Shot("05-minigames");
                picker.HideInstant();
                HostOptions.Replace(mine);
                if (MatchSettings.Instance != null) MatchSettings.Instance.ServerApply(HostOptions.Current);
                yield return Wait(0.2f);
            }

            NetSession.Instance.StartGame();
            yield return WaitFor(() => GameState.Instance != null && GameState.Instance.IsSpawned
                                       && GameState.Instance.Phase.Value != GamePhase.Idle, 20f, "the match");

            // Play the real thing for a while, photographing each new kind of moment.
            var gs = GameState.Instance;
            float started = Time.realtimeSinceStartup;
            GamePhase last = GamePhase.Idle;
            int lastRound = -1;
            bool cheated = false;
            while (Time.realtimeSinceStartup - started < 720f)
            {
                gs = GameState.Instance;
                if (gs == null) { yield return null; continue; }
                var phase = gs.Phase.Value;
                if (phase == last && gs.RoundIndex.Value == lastRound) { yield return null; continue; }
                last = phase;
                lastRound = gs.RoundIndex.Value;
                var def = gs.CurrentDef;
                string kind = def == null ? "none" : def.IsMinigame ? "minigame" : def.inputType.ToString().ToLowerInvariant();

                switch (phase)
                {
                    case GamePhase.RoundIntro:
                        yield return Wait(0.7f);
                        yield return Once(def != null && def.IsMinigame ? "20-rule-card" : $"10-intro-{kind}");
                        if (!_taken.Contains("24-match-menu"))
                        {
                            var matchMenu = Object.FindAnyObjectByType<MatchMenu>(FindObjectsInactive.Include);
                            if (matchMenu != null)
                            {
                                matchMenu.Open();
                                yield return Wait(0.6f);
                                yield return Once("24-match-menu");
                                matchMenu.Close();
                                yield return Wait(0.3f);
                            }
                        }
                        // Once every kind of answer has been on screen (or the deck has had its
                        // chance), hand ourselves the match.
                        // With question rounds off there are no answers to wait for.
                        bool seenAll = !GameBootstrap.ConfigOrDefault.questionRounds
                                       || (_taken.Contains("14-scoring-redgreen") && _taken.Contains("14-scoring-yesno")
                                           && _taken.Contains("14-scoring-number1to10"));
                        if (!cheated && (seenAll || gs.RoundIndex.Value >= 16) && gs.RoundIndex.Value >= 5
                            && PlayerData.Local != null)
                        {
                            PlayerData.Local.Score.Value = 118;
                            cheated = true;
                        }
                        break;

                    case GamePhase.Answering:
                        yield return Wait(1.0f);
                        yield return Once($"11-answer-{kind}");
                        PressFirstAnswer();
                        yield return Wait(0.5f);
                        yield return Once($"12-locked-{kind}");
                        break;

                    case GamePhase.Play:
                        yield return Wait(0.7f);
                        yield return Once("21-lead-in");
                        float shotAt = ShotAt(def?.minigameId, Mathf.Max(0.5f, GameBootstrap.ConfigOrDefault.minigameLeadInSeconds));
                        yield return WaitFor(() => GameState.Instance.RemainingSeconds < GameState.Instance.PhaseDuration.Value - shotAt,
                            10f, "the level to be under way");
                        yield return Once($"22-level-{def?.minigameId}");
                        if (!_everyGame && gs.SubRound.Value == 1 && !gs.LevelTieBreak.Value)
                        {
                            _everyGame = true;
                            yield return EveryOtherGame(gs);
                            if (_only != null) yield break; // just the games asked for
                        }
                        break;

                    case GamePhase.LevelResult:
                        yield return Wait(0.5f);
                        yield return Once("23-level-result");
                        break;

                    case GamePhase.Reveal:
                        yield return Wait(1.6f);
                        yield return Once(def != null && def.IsMinigame ? "25-ranking" : $"13-reveal-{kind}");
                        break;

                    case GamePhase.Scoring:
                        yield return Wait(1.0f);
                        yield return Once(def != null && def.IsMinigame ? "26-ranking-scored" : $"14-scoring-{kind}");
                        break;

                    case GamePhase.Winner:
                        yield return Wait(2.0f);
                        yield return Once("31-winner");
                        break;
                }
                if (phase == GamePhase.Winner) break;
            }

            yield return FakeStates();
        }

        // ------------------------------------------------------------------
        // Every other minigame, live on the same stage
        // ------------------------------------------------------------------

        /// <summary>
        /// A match deals only a few minigames. So while the first one is live, the host's clock
        /// is held and every game the deck didn't deal is started on the same stage, the way
        /// GameUI starts a level, and photographed at the same moment. None of their results
        /// reach the host; afterwards the dealt level starts over and the match goes on.
        /// </summary>
        private IEnumerator EveryOtherGame(GameState gs)
        {
            var ui = Object.FindAnyObjectByType<GameUI>();
            var stage = ui != null ? Get<MinigameStage>(ui, "minigameStage") : null;
            if (stage == null) yield break;

            var phaseEnd = typeof(GameState).GetField("_phaseEnd", BindingFlags.Instance | BindingFlags.NonPublic);
            var report = typeof(GameUI).GetMethod("OnLevelFinished", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(phaseEnd, "GameState._phaseEnd not found");
            Assert.IsNotNull(report, "GameUI.OnLevelFinished not found");
            var toHost = (Action<bool, int>)Delegate.CreateDelegate(typeof(Action<bool, int>), ui, report);

            phaseEnd.SetValue(gs, double.MaxValue);
            stage.LevelFinished -= toHost;
            bool ended = false;
            Action<bool, int> onEnded = (failed, metric) => ended = true;
            stage.LevelFinished += onEnded;

            float lead = Mathf.Max(0.5f, GameBootstrap.ConfigOrDefault.minigameLeadInSeconds);
            foreach (var entry in MinigameRegistry.All)
            {
                string name = "22-level-" + entry.Id;
                if (_taken.Contains(name) || (_only != null && !_only.Contains(entry.Id))) continue;
                // A level that ends by itself without input (Steady Hand's first-second grace
                // runs out) is played again and photographed a moment before it ends.
                float at = ShotAt(entry.Id, lead);
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    ended = false;
                    stage.StartLevel(entry, gs.SubRound.Value, gs.MinigameSeed.Value, true, gs.AliveCount.Value,
                        lead, entry.LevelSeconds, gs.LevelTieBreak.Value, true);
                    float t = 0f;
                    while (t < at && !ended) { t += Time.unscaledDeltaTime; yield return null; }
                    if (!ended) break;
                    at = Mathf.Max(lead + 0.2f, t - 0.3f);
                }
                yield return Once(name);
            }

            // Give the host its level back, from the top.
            stage.LevelFinished -= onEnded;
            stage.LevelFinished += toHost;
            double end = gs.ServerNow + gs.PhaseDuration.Value;
            phaseEnd.SetValue(gs, end);
            gs.PhaseEndTime.Value = end;
            Call(ui, "StartLevel");
        }

        // ------------------------------------------------------------------
        // States a solo match never reaches, staged directly
        // ------------------------------------------------------------------

        private IEnumerator FakeStates()
        {
            var ui = Object.FindAnyObjectByType<GameUI>();
            if (ui == null) yield break;
            var hud = Get<CanvasGroup>(ui, "hud");
            if (hud != null) { hud.alpha = 1f; hud.blocksRaycasts = true; }
            var winner = Get<WinnerPanel>(ui, "winnerPanel");
            if (winner != null) winner.HideInstant();
            var reveal = Get<RevealPanel>(ui, "revealPanel");
            if (reveal != null) reveal.HideInstant();
            var stage = Get<MinigameStage>(ui, "minigameStage");
            var round = Get<RoundPanel>(ui, "roundPanel");
            var rail = Get<SeatRail>(ui, "seatRail");
            var masthead = Get<Masthead>(ui, "masthead");

            // A full rail in every state the seat card has.
            if (rail != null)
            {
                rail.enabled = false;
                var root = (RectTransform)rail.transform;
                for (int i = root.childCount - 1; i >= 0; i--) Object.Destroy(root.GetChild(i).gameObject);
                yield return null;
                var names = new[] { "Mert", "Ayşe", "Deniz", "Burak", "Can", "Elif", "Zeynep", "Kerem" };
                var monos = Monogram.ForAll(names);
                for (int i = 0; i < names.Length; i++)
                {
                    var card = SeatCard.Create(root, "Fake" + i);
                    card.Rect.At(i * (SeatCard.Width + 16f), 0f, SeatCard.Width, SeatCard.Height);
                    card.SetIdentity(names[i], monos[i], i == 0 ? "HOST" : i == 2 ? "YOU" : "", i == 2);
                    card.SetScore(new[] { 64, 72, 51, 45, 42, 38, 26, 12 }[i]);
                    card.SetRank("No." + (i + 1));
                    card.SetLeader(i == 1);
                    switch (i)
                    {
                        case 0: card.SetStatus(SeatCard.Status.Thinking); break;
                        case 1: card.SetStatus(SeatCard.Status.Locked); break;
                        case 2: card.SetStatus(SeatCard.Status.YourMove); break;
                        case 3: card.SetBand("PICKED RED", Palette.Red, Palette.OnRed, InkSprites.Diamond); card.SetDelta(15, false); card.SetRank("No.4 ↑↑"); break;
                        case 4: card.SetBand("PICKED GREEN", Palette.Green, Palette.Ink, InkSprites.Dot); card.SetDelta(-5, false); card.SetRank("No.5 ↓"); break;
                        case 5: card.SetStatus(SeatCard.Status.Done); break;
                        case 6: card.SetStatus(SeatCard.Status.OutEarlier, "out · level 2"); card.SetDim(true); break;
                        case 7: card.SetBigStamp("OUT"); card.SetDim(true); break;
                    }
                }
                var race = masthead != null ? masthead.Race : null;
                if (race != null)
                {
                    var entries = new List<RaceTrack.Entry>();
                    var scores = new[] { 64, 87, 51, 45, 42, 38, 26, 12 };
                    for (int i = 0; i < names.Length; i++)
                        entries.Add(new RaceTrack.Entry { Id = (ulong)(100 + i), Mono = monos[i], Score = scores[i], Local = i == 2 });
                    race.Refresh(entries, 0f);
                    race.ShowMoves(new List<(int, int)> { (72, 87), (56, 51) });
                }
            }

            // Someone went out.
            if (stage != null && round != null)
            {
                round.HideInstant();
                stage.ShowInstant();
                var entry = MinigameRegistry.Get("memory_boxes");
                stage.StartLevel(entry, 3, 12345, true, 5, 0.01f, entry.LevelSeconds, false);
                stage.SetAlive(new List<(string, bool)> { ("AY", false), ("DE", true), ("BU", false) });
                yield return Wait(1.5f);
                stage.ShowLevelResult(GameState.OutcomeEliminated, "OUT: Ayşe and Deniz",
                    new List<(string, string)> { ("Ayşe", "AY"), ("Deniz", "DE") }, 3, false);
                yield return Wait(0.3f);
                yield return Shot("40-fake-out-banner");

                // Watching from the sidelines.
                stage.StartLevel(entry, 4, 777, false, 3, 0.01f, entry.LevelSeconds, false);
                yield return Wait(1.5f);
                yield return Shot("41-fake-watching");

                stage.ShowLevelResult(GameState.OutcomeTieBreak, "Dead heat. Mert and Elif play it off.",
                    new List<(string, string)>(), 2, false);
                yield return Wait(0.3f);
                yield return Shot("42-fake-dead-heat");
                stage.EndMinigame();
                stage.HideInstant();
            }

            // A tie at the top.
            if (round != null)
            {
                var lib = GameState.Instance != null ? GameState.Instance.Library : Resources.Load<RoundLibrary>("RoundLibrary");
                RoundDefinition sus = null;
                foreach (var d in lib.rounds) if (d != null && d.title == "Sus") sus = d;
                if (sus == null) foreach (var d in lib.rounds) if (d != null && d.inputType == InputType.RedGreen) { sus = d; break; }
                round.ShowInstant();
                round.ShowRound(sus, 1, true, "Ayşe and Mert are both on 104. Everyone plays this round.");
                round.SetOpensIn(2f);
                round.SetPicked(0, 8);
                if (masthead != null) masthead.SetRound(21, false, true);
                yield return Wait(0.4f);
                yield return Shot("43-fake-tie-break");
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static void PressFirstAnswer()
        {
            foreach (var b in Object.FindObjectsByType<AnswerButton>(FindObjectsSortMode.InstanceID))
            {
                if (!b.isActiveAndEnabled) continue;
                var btn = b.GetComponent<Button>();
                if (btn == null || !btn.IsInteractable()) continue;
                b.Press();
                return;
            }
        }

        private IEnumerator Once(string name)
        {
            if (_taken.Contains(name)) yield break;
            _taken.Add(name);
            yield return Shot(name);
        }

        /// <summary>
        /// Overlay canvases never reach a camera, so for one frame every canvas is put in front
        /// of a camera that renders into a 1920 × 1080 texture.
        /// </summary>
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
            yield return null; // let the scaler and layout catch up with the new size
            Canvas.ForceUpdateCanvases();
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Debug.Log($"[ScreenshotTour] {name}.png");

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
