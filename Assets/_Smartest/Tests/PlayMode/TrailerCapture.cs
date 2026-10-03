using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Smartest.Core;
using Smartest.Minigames;
using Smartest.Net;
using Smartest.Rounds;
using Smartest.UI;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Smartest.Tests
{
    /// <summary>
    /// The trailer's footage: the game's own screens, recorded as numbered JPEGs (up to 30 a
    /// second, each clip in its own folder with times.txt saying when every frame was taken)
    /// for Tools/Trailer/build_trailer.py to cut to music. Staged the way SteamShots stages its
    /// pictures: the real Menu and Game scenes, seven stand-ins round the table, and each
    /// minigame playing itself. The host's bubble says what the trailer's voice-over says.
    ///
    /// Explicit: it writes files. Run it on purpose (it needs graphics, so no -nographics):
    ///   Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter TrailerCapture
    /// Frames go to $SMARTEST_SHOTS, or Steam/Trailer/footage next to Assets.
    /// </summary>
    [Explicit("Records trailer footage to JPEG; run it on purpose.")]
    public class TrailerCapture
    {
        private const int Width = 1920, Height = 1080;
        private const float Fps = 30f;
        private const float MontageClip = 2.3f; // the edit uses 2 s; the rest is room to trim

        private static readonly string[] Names = { "Mert", "Ayşe", "Deniz", "Burak", "Can", "Elif", "Zeynep", "Kerem" };
        private const int You = 2;

        // The voice-over's lines, so the host's bubble agrees with what's heard.
        private const string Welcome = "Look who it is. Get your friends in, first to a hundred points, bragging rights forever. Or until next weekend.";
        private const string SkillLine = "Minigame! Actual skill required, sorry.";
        private const string OutLine = "Out! You're out. It's fine. It's fine.";
        private const string TwoLeftLine = "Two left! This is the good part.";
        private const string CloseLine = "One good round and this is over. Just saying.";
        private const string WinnerLine = "A HUNDRED! That's the game! Smartest in the room, officially. Put it on a résumé.";

        private static readonly int[] Early = { 30, 35, 25, 15, 20, 10, 5, -5 };
        private static readonly int[] Middle = { 64, 72, 51, 45, 42, 38, 26, 12 };
        private static readonly int[] Late = { 83, 92, 88, 64, 57, 49, 33, 21 };
        private static readonly int[] Finals = { 88, 104, 91, 64, 57, 49, 33, 21 };

        private class Clip
        {
            public string Name, Game, Caption;
            public int Round, Level, Seed;
            public float PreRoll;              // seconds of the level played before recording starts
            public int[] Scores, OutAt;
            public int[] Done = new int[0];
        }

        private static readonly Clip[] MontageOne =
        {
            new Clip { Name = "m1-traffic", Game = "traffic", Round = 5, Level = 6, Seed = 5150, PreRoll = 3.4f, Scores = Middle,
                OutAt = new[] { 0, 0, 0, 5, 0, 4, 3, 2 }, Caption = SkillLine },
            new Clip { Name = "m1-penalty", Game = "penalty", Round = 9, Level = 4, Seed = 909, PreRoll = 0.6f, Scores = Late,
                OutAt = new[] { 0, 0, 0, 0, 3, 0, 2, 1 }, Caption = SkillLine },
            new Clip { Name = "m1-pop-lock", Game = "pop_lock", Round = 9, Level = 5, Seed = 9595, PreRoll = 1.8f, Scores = Late,
                OutAt = new[] { 0, 0, 0, 0, 4, 0, 3, 2 } },
            new Clip { Name = "m1-rhythm", Game = "rhythm", Round = 4, Level = 6, Seed = 4040, PreRoll = 2.0f, Scores = Early,
                OutAt = new[] { 0, 0, 0, 0, 5, 4, 3, 0 } },
            new Clip { Name = "m1-herd", Game = "herd", Round = 6, Level = 6, Seed = 4242, PreRoll = 1.4f, Scores = Middle,
                OutAt = new[] { 0, 0, 0, 3, 5, 0, 4, 2 }, Done = new[] { 1 } },
            new Clip { Name = "m1-slice", Game = "slice", Round = 3, Level = 4, Seed = 303, PreRoll = 0.3f, Scores = Early,
                OutAt = new[] { 0, 0, 0, 0, 0, 3, 0, 2 } },
            new Clip { Name = "m1-fishing", Game = "fishing", Round = 3, Level = 5, Seed = 3131, PreRoll = 2.8f, Scores = Early,
                OutAt = new[] { 0, 0, 0, 0, 4, 0, 3, 0 } },
            new Clip { Name = "m1-juggle", Game = "juggle", Round = 7, Level = 4, Seed = 7070, PreRoll = 2.2f, Scores = Middle,
                OutAt = new[] { 0, 0, 0, 3, 0, 0, 2, 0 } },
            new Clip { Name = "m1-flap", Game = "flap", Round = 6, Level = 5, Seed = 6565, PreRoll = 2.8f, Scores = Middle,
                OutAt = new[] { 0, 0, 0, 4, 0, 3, 0, 2 } },
            new Clip { Name = "m1-tightrope", Game = "tightrope", Round = 8, Level = 5, Seed = 8080, PreRoll = 0.8f, Scores = Late,
                OutAt = new[] { 0, 0, 0, 4, 0, 3, 0, 2 } },
        };

        private static readonly Clip[] MontageTwo =
        {
            new Clip { Name = "m2-statues", Game = "statues", Round = 10, Level = 4, Seed = 4141, PreRoll = 1.8f, Scores = Late,
                OutAt = new[] { 0, 0, 0, 0, 3, 0, 0, 2 } },
            // Two left: Ayşe and you.
            new Clip { Name = "m2-stack", Game = "stack", Round = 10, Level = 7, Seed = 606, PreRoll = 2.8f, Scores = Late,
                OutAt = new[] { 6, 0, 0, 5, 4, 6, 3, 2 }, Caption = TwoLeftLine },
            new Clip { Name = "m2-whack", Game = "whack", Round = 11, Level = 4, Seed = 4411, PreRoll = 1.5f, Scores = Late,
                OutAt = new[] { 0, 0, 0, 3, 0, 0, 2, 0 } },
            new Clip { Name = "m2-keepy-uppy", Game = "keepy_uppy", Round = 11, Level = 4, Seed = 4422, PreRoll = 1.5f, Scores = Late,
                OutAt = new[] { 0, 0, 0, 0, 3, 0, 2, 0 } },
            new Clip { Name = "m2-goalie", Game = "goalie", Round = 11, Level = 4, Seed = 4433, PreRoll = 1.5f, Scores = Late,
                OutAt = new[] { 0, 0, 0, 0, 3, 0, 2, 0 } },
        };

        private NetworkMode _savedMode;
        private HostOptions _savedOptions;
        private string _dir;

        private Camera _cam;
        private RenderTexture _rt;
        private Texture2D _grab;
        private string _clipDir;
        private StreamWriter _times;
        private int _frame;
        private float _clipStart, _nextGrab;
        private int _pending;
        private List<SeatCard> _cards = new List<SeatCard>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var config = GameBootstrap.ConfigOrDefault;
            _savedMode = config.networkMode;
            config.networkMode = NetworkMode.Local;
            _savedOptions = HostOptions.Replace(new HostOptions()); // a standard match: the race to 100
            _dir = Environment.GetEnvironmentVariable("SMARTEST_SHOTS");
            if (string.IsNullOrEmpty(_dir)) _dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Steam", "Trailer", "footage"));
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
            HostOptions.Replace(_savedOptions);
            if (_cam != null) Object.Destroy(_cam.gameObject);
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
            if (_grab != null) Object.Destroy(_grab);
        }

        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator Footage()
        {
            // A recording tool, not a check: an editor package grumbling in the log shouldn't stop it.
            LogAssert.ignoreFailingMessages = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            MakeCamera();
            var monos = Monogram.ForAll(Names);

            // ---- The front page, the host saying the opening line ----
            yield return Wait(1.0f);
            AimCanvases();
            var menuCaption = Object.FindAnyObjectByType<HostCaption>();
            if (menuCaption != null) menuCaption.Say(Welcome, 60f);
            yield return Wait(0.6f);
            BeginClip("front-page");
            yield return RecordFor(3.4f);
            yield return EndClip();

            // ---- The lobby filling up ----
            var menu = Object.FindAnyObjectByType<MenuUI>();
            Assert.IsNotNull(menu, "the Menu scene should have a MenuUI");
            Call(menu, "OnHostClicked");
            var lobby = Get<LobbyUI>(menu, "lobby");
            yield return WaitFor(() => lobby.IsShown, 15f, "the lobby");
            yield return Wait(0.8f);
            var seats = Get<LobbySeat[]>(lobby, "seats");
            var count = Get<TMP_Text>(lobby, "countText");
            // An online lobby's code, not the "LOCAL" of an offline test run.
            var code = Get<TMP_Text>(lobby, "codeText");
            if (code != null) code.text = "JQ4RTX";
            if (menuCaption != null) menuCaption.Say(Welcome, 60f);
            var order = new[] { You, 0, 1, 3, 4, 5, 6, 7 }; // you first: you're hosting
            for (int i = 1; i < seats.Length; i++) seats[i].SetOpen(i + 1);
            seats[0].SetTaken(1, 0, Names[You], monos[You], "HOST · YOU", true, string.Empty, false);
            if (count != null) count.text = "1 / 8";
            BeginClip("lobby");
            yield return RecordFor(0.35f);
            for (int k = 1; k < order.Length && k < seats.Length; k++)
            {
                int who = order[k];
                seats[k].SetTaken(k + 1, (ulong)k, Names[who], monos[who], string.Empty, false, "just joined", false);
                if (count != null) count.text = $"{k + 1} / 8";
                Sounds.Play(Sounds.Kind.Click);
                yield return RecordFor(0.26f);
            }
            yield return RecordFor(0.7f);
            yield return EndClip();

            // ---- Into a match, holding the host's clock so each moment can be staged ----
            NetSession.Instance.StartGame();
            yield return WaitFor(() => GameState.Instance != null && GameState.Instance.IsSpawned
                                       && GameState.Instance.Phase.Value == GamePhase.RoundIntro, 20f, "the first round");
            yield return Wait(1.0f);
            var phaseEnd = typeof(GameState).GetField("_phaseEnd", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(phaseEnd, "GameState._phaseEnd not found");
            phaseEnd.SetValue(GameState.Instance, double.MaxValue);
            AimCanvases();

            var ui = Object.FindAnyObjectByType<GameUI>();
            Assert.IsNotNull(ui, "the Game scene should have a GameUI");
            var stage = Get<MinigameStage>(ui, "minigameStage");
            var round = Get<RoundPanel>(ui, "roundPanel");
            var rail = Get<SeatRail>(ui, "seatRail");
            var masthead = Get<Masthead>(ui, "masthead");
            var caption = Get<HostCaption>(ui, "hostCaption");
            var hud = Get<CanvasGroup>(ui, "hud");
            if (rail != null) rail.enabled = false;
            if (round != null) round.HideInstant();
            stage.ShowInstant();

            // ---- Points going up: the race to 100 ----
            yield return Scoring("scores", "catch", 3, 3333, Early, Middle, null, Welcome, stage, rail, masthead, caption, monos);

            // ---- The montages ----
            foreach (var clip in MontageOne) yield return Level(clip, stage, rail, masthead, caption, monos);
            foreach (var clip in MontageTwo) yield return Level(clip, stage, rail, masthead, caption, monos);

            // ---- A knock-out: the level ends and two seats go dark ----
            {
                var entry = MinigameRegistry.Get("simon");
                var outAt = new[] { 0, 0, 0, 0, 0, 3, 2, 1 };
                yield return Table(rail, masthead, monos, 6, 4, Middle, outAt, new int[0], null);
                var alive = Alive(outAt, monos);
                stage.StartLevel(entry, 4, 4444, true, alive.Count, 0.01f, entry.LevelSeconds, false);
                var view = Get<MinigameView>(stage, "_view");
                if (view != null) view.Interactive = false;
                stage.SetAlive(alive);
                if (caption != null) caption.Hide();
                yield return Wait(1.0f);
                BeginClip("knockout");
                yield return RecordFor(1.6f);
                yield return Table(rail, masthead, monos, 6, 4, Middle, outAt, new int[0], new[] { 3, 4 });
                stage.ShowLevelResult(GameState.OutcomeEliminated, $"OUT: {Names[3]} and {Names[4]}",
                    new List<(string, string)> { (Names[3], monos[3]), (Names[4], monos[4]) }, 3, false);
                if (caption != null) caption.Say(OutLine, 60f);
                Sounds.Play(Sounds.Kind.Out);
                yield return RecordFor(2.8f);
                yield return EndClip();
                stage.EndMinigame();
            }

            // ---- Closing in on 100 ----
            yield return Scoring("closing", "goalie", 12, 1212, Middle, Late, new[] { 0, 0, 0, 0, 0, 0, 0, 0 }, CloseLine,
                stage, rail, masthead, caption, monos);

            // ---- The winner ----
            var winnerPanel = Get<WinnerPanel>(ui, "winnerPanel");
            if (winnerPanel != null)
            {
                var players = new List<PlayerData>();
                var standins = new List<GameObject>();
                for (int i = 0; i < Names.Length; i++)
                {
                    if (i == You && PlayerData.Local != null)
                    {
                        PlayerData.Local.Score.Value = Finals[i];
                        players.Add(PlayerData.Local);
                        continue;
                    }
                    var go = new GameObject("Standin" + i); // never spawned: the panel reads a name and a score
                    standins.Add(go);
                    var p = go.AddComponent<PlayerData>();
                    p.PlayerName.Value = new FixedString32Bytes(Names[i]);
                    p.Score.Value = Finals[i];
                    players.Add(p);
                }
                stage.EndMinigame();
                if (caption != null) caption.Hide();
                stage.HideInstant();
                winnerPanel.ShowWinner(players[1], players, true, 14, WinnerLine);
                winnerPanel.HideInstant();
                if (hud != null) hud.alpha = 0f;
                BeginClip("winner");
                winnerPanel.Show();
                Sounds.Play(Sounds.Kind.Fanfare);
                yield return RecordFor(5.6f);
                yield return EndClip();
                foreach (var go in standins) Object.Destroy(go);
            }
            Debug.Log($"[TrailerCapture] footage in {_dir}");
        }

        // ------------------------------------------------------------------
        // Staged moments
        // ------------------------------------------------------------------

        /// <summary>One level of a minigame, playing itself at a moment in a match.</summary>
        private IEnumerator Level(Clip clip, MinigameStage stage, SeatRail rail, Masthead masthead, HostCaption caption,
            List<string> monos)
        {
            var entry = MinigameRegistry.Get(clip.Game);
            if (entry == null) { Debug.LogWarning("[TrailerCapture] no minigame " + clip.Game); yield break; }
            yield return Table(rail, masthead, monos, clip.Round, clip.Level, clip.Scores, clip.OutAt, clip.Done, null);
            var alive = Alive(clip.OutAt, monos);
            stage.StartLevel(entry, clip.Level, clip.Seed, true, alive.Count, 0.01f, entry.LevelSeconds, false);
            // Built as a player still in sees it; played by the game itself, as someone watching sees it.
            var view = Get<MinigameView>(stage, "_view");
            if (view != null) view.Interactive = false;
            stage.SetAlive(alive);
            if (caption != null)
            {
                if (string.IsNullOrEmpty(clip.Caption)) caption.Hide();
                else caption.Say(clip.Caption, 60f);
            }
            yield return Wait(clip.PreRoll);
            BeginClip(clip.Name);
            yield return RecordFor(MontageClip);
            yield return EndClip();
        }

        /// <summary>A round scored: every seat's points count up, the race tags slide, the gains show.</summary>
        private IEnumerator Scoring(string name, string game, int level, int seed, int[] from, int[] to, int[] outAt,
            string line, MinigameStage stage, SeatRail rail, Masthead masthead, HostCaption caption, List<string> monos)
        {
            outAt = outAt ?? new int[Names.Length];
            var entry = MinigameRegistry.Get(game);
            yield return Table(rail, masthead, monos, 5, level, from, outAt, new int[0], null);
            if (entry != null)
            {
                stage.StartLevel(entry, level, seed, true, Names.Length, 0.01f, entry.LevelSeconds, false);
                var view = Get<MinigameView>(stage, "_view");
                if (view != null) view.Interactive = false;
                stage.SetAlive(Alive(outAt, monos));
            }
            if (caption != null) caption.Say(line, 60f);
            yield return Wait(1.0f);
            BeginClip(name);
            yield return RecordFor(0.5f);

            var race = masthead != null ? masthead.Race : null;
            var moves = new List<(int, int)>();
            var entries = new List<RaceTrack.Entry>();
            for (int i = 0; i < Names.Length; i++)
            {
                moves.Add((from[i], to[i]));
                entries.Add(new RaceTrack.Entry { Id = (ulong)(100 + i), Mono = monos[i], Score = to[i], Local = i == You });
                if (i < _cards.Count && _cards[i] != null)
                {
                    _cards[i].AnimateScore(to[i], 0.9f);
                    _cards[i].SetDelta(to[i] - from[i], true);
                }
            }
            if (race != null)
            {
                race.ShowMoves(moves);
                race.Refresh(entries, 0.9f);
            }
            Sounds.Play(Sounds.Kind.Good);
            yield return RecordFor(2.6f);
            yield return EndClip();
            stage.EndMinigame();
        }

        private static List<(string, bool)> Alive(int[] outAt, List<string> monos)
        {
            var alive = new List<(string, bool)>();
            for (int i = 0; i < Names.Length; i++) if (outAt[i] == 0) alive.Add((monos[i], i == You));
            return alive;
        }

        /// <summary>The table: eight seats with their scores and states, the race to 100, the round.</summary>
        private IEnumerator Table(SeatRail rail, Masthead masthead, List<string> monos, int round, int level,
            int[] scores, int[] outAt, int[] done, int[] outNow)
        {
            if (masthead != null) masthead.SetRound(round, true, false);
            int top = int.MinValue;
            foreach (int s in scores) top = Mathf.Max(top, s);

            _cards = new List<SeatCard>();
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
                    _cards.Add(card);
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
        // Recording
        // ------------------------------------------------------------------

        private void MakeCamera()
        {
            var go = new GameObject("TrailerCamera");
            Object.DontDestroyOnLoad(go);
            _cam = go.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Palette.Paper;
            _cam.orthographic = true;
            _rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            _cam.targetTexture = _rt;
            _grab = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        }

        /// <summary>
        /// Every canvas draws into the trailer's camera at 1920 × 1080. The camera renders after
        /// the frame's LateUpdate, so a grab on the next frame sees a finished picture.
        /// </summary>
        private void AimCanvases()
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = _cam;
                c.planeDistance = 10f;
            }
        }

        private void BeginClip(string name)
        {
            _clipDir = Path.Combine(_dir, name);
            Directory.CreateDirectory(_clipDir);
            foreach (var old in Directory.GetFiles(_clipDir)) File.Delete(old); // this clip's last take
            _times = new StreamWriter(Path.Combine(_clipDir, "times.txt"));
            _frame = 0;
            _clipStart = Time.unscaledTime;
            _nextGrab = _clipStart;
        }

        private IEnumerator RecordFor(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
            {
                yield return null;
                if (Time.unscaledTime >= _nextGrab && Volatile.Read(ref _pending) < 48) Grab();
            }
        }

        /// <summary>The last finished picture, written as a JPEG on another thread, stamped with its time.</summary>
        private void Grab()
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            _grab.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            RenderTexture.active = prev;
            byte[] raw = _grab.GetRawTextureData<byte>().ToArray();
            var format = _grab.graphicsFormat;
            string path = Path.Combine(_clipDir, $"f{_frame:D5}.jpg");
            _times.WriteLine($"{_frame} {Time.unscaledTime - _clipStart:0.0000}");
            _frame++;
            _nextGrab += 1f / Fps;
            if (_nextGrab < Time.unscaledTime) _nextGrab = Time.unscaledTime;
            Interlocked.Increment(ref _pending);
            Task.Run(() =>
            {
                try
                {
                    File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(raw, format, (uint)Width, (uint)Height, 0, 92));
                }
                finally
                {
                    Interlocked.Decrement(ref _pending);
                }
            });
        }

        private IEnumerator EndClip()
        {
            if (_times != null)
            {
                _times.Dispose();
                _times = null;
            }
            while (Volatile.Read(ref _pending) > 0) yield return null;
            Debug.Log($"[TrailerCapture] {Path.GetFileName(_clipDir)}: {_frame} frames");
        }

        // ------------------------------------------------------------------
        // Helpers (as in SteamShots)
        // ------------------------------------------------------------------

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
