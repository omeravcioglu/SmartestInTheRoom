using System;
using System.Collections.Generic;
using Smartest.Core;
using Smartest.Minigames;
using Smartest.Net;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Smartest.Rounds
{
    public enum GamePhase
    {
        Idle = 0,
        RoundIntro = 1,
        Answering = 2,
        Reveal = 3,
        Scoring = 4,
        Winner = 5,
        /// <summary>Minigames: one level is being played, simultaneously, by everyone still in.</summary>
        Play = 6,
        /// <summary>Minigames: the short beat between levels that says who just went out.</summary>
        LevelResult = 7
    }

    /// <summary>
    /// The challenge engine. A network prefab (Resources/GameState.prefab) that the host spawns
    /// once every client has loaded the Game scene. The host drives the state machine in
    /// Update(); clients only render from the networked fields below via their change
    /// callbacks — the UI never polls for phase changes.
    ///
    /// Two kinds of challenge run on the same machine:
    ///   social   RoundIntro -> Answering -> Reveal -> Scoring
    ///   minigame RoundIntro -> (Play -> LevelResult)* -> Reveal -> Scoring
    ///
    /// Minigame levels are generated on every client from (minigameId, Seed, level), so the
    /// host never sends puzzle content and everyone faces exactly the same challenge. Results
    /// are timed locally and reported with one RPC, which is also what keeps ping out of it.
    ///
    /// Field order matters: Netcode sends deltas in declaration order, so Results and
    /// RevealLine are declared before Phase — a client always has the reveal data by
    /// the time it sees Phase flip to Reveal.
    /// </summary>
    public class GameState : NetworkBehaviour
    {
        public static GameState Instance { get; private set; }

        /// <summary>Fired on every client when the GameState finishes spawning.</summary>
        public static event Action Spawned;
        public static event Action<GamePhase> PhaseChanged;
        public static event Action RoundChanged;
        public static event Action ResultsChanged;

        [SerializeField] private RoundLibrary library;

        // ---- Networked state (server-authoritative). Keep Phase LAST. ----
        public NetworkList<PlayerRoundResult> Results;
        public NetworkVariable<FixedString512Bytes> RevealLine = new NetworkVariable<FixedString512Bytes>(new FixedString512Bytes(string.Empty));
        public NetworkVariable<FixedString128Bytes> LevelLine = new NetworkVariable<FixedString128Bytes>(new FixedString128Bytes(string.Empty));
        public NetworkVariable<int> RoundDefId = new NetworkVariable<int>(-1);
        public NetworkVariable<int> RoundIndex = new NetworkVariable<int>(0);
        /// <summary>Social: multi-step rounds. Minigames: the level number, which is also the difficulty.</summary>
        public NetworkVariable<int> SubRound = new NetworkVariable<int>(1);

        /// <summary>Minigames: the shared seed every client builds this minigame's levels from.</summary>
        public NetworkVariable<int> MinigameSeed = new NetworkVariable<int>(0);
        /// <summary>Minigames: how many players are still in.</summary>
        public NetworkVariable<int> AliveCount = new NetworkVariable<int>(0);
        /// <summary>Minigames: this level is a play-off between tied players.</summary>
        public NetworkVariable<bool> LevelTieBreak = new NetworkVariable<bool>(false);
        /// <summary>What the last level decided: 0 nothing yet, 1 someone went out, 2 everyone failed, 3 dead heat.</summary>
        public NetworkVariable<byte> LevelOutcome = new NetworkVariable<byte>(0);

        public const byte OutcomeNone = 0;
        public const byte OutcomeEliminated = 1;
        public const byte OutcomeRepeat = 2;
        public const byte OutcomeTieBreak = 3;

        public NetworkVariable<double> PhaseEndTime = new NetworkVariable<double>(0);
        public NetworkVariable<float> PhaseDuration = new NetworkVariable<float>(0f);
        public NetworkVariable<ulong> WinnerClientId = new NetworkVariable<ulong>(ulong.MaxValue);
        public NetworkVariable<bool> TieBreak = new NetworkVariable<bool>(false);
        public NetworkVariable<GamePhase> Phase = new NetworkVariable<GamePhase>(GamePhase.Idle);

        // ---- Server-only working state ----
        private ChallengeDeck _deck;
        private RoundDefinition _current;
        private int _subRound = 1;
        private bool _continueSubRounds;
        private bool _forceRedGreenNext;
        private RoundDefinition _predictDef;
        private Dictionary<ulong, int> _pendingPredictions;
        private List<PlayerData> _roundPlayers = new List<PlayerData>();
        private int[] _finalDeltas = new int[0];
        private double _phaseEnd;
        private bool _graceApplied;
        private System.Random _rng;

        // Minigame working state
        private MinigameEntry _entry;
        private EliminationLadder _ladder;
        private readonly Dictionary<ulong, LevelReport> _levelReports = new Dictionary<ulong, LevelReport>();
        private bool _solo;
        private int _soloLevel = 1;
        private bool _levelClosing;

        public RoundLibrary Library => library;
        public RoundDefinition CurrentDef => library != null ? library.GetById(RoundDefId.Value) : null;

        /// <summary>The minigame this round is playing, or null for a social round.</summary>
        public MinigameEntry CurrentMinigame
        {
            get
            {
                var def = CurrentDef;
                return def != null && def.IsMinigame ? MinigameRegistry.Get(def.minigameId) : null;
            }
        }

        private GameConfig Config => GameBootstrap.ConfigOrDefault;

        public double ServerNow => NetworkManager != null ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
        public float RemainingSeconds => (float)Math.Max(0.0, PhaseEndTime.Value - ServerNow);
        public float Progress01 => PhaseDuration.Value > 0f ? Mathf.Clamp01(RemainingSeconds / PhaseDuration.Value) : 0f;

        /// <summary>
        /// Seconds until this level's "GO". Every client derives it from the same replicated
        /// phase clock, so the 3-2-1 lands on the same instant on every machine.
        /// </summary>
        public float LevelStartsIn
        {
            get
            {
                var entry = CurrentMinigame;
                float levelSeconds = entry != null ? entry.LevelSeconds : 10f;
                float total = PhaseDuration.Value;
                float leadIn = Mathf.Max(0f, total - levelSeconds - Config.levelReportGraceSeconds);
                float elapsed = total - RemainingSeconds;
                return Mathf.Max(0f, leadIn - elapsed);
            }
        }

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        public const string LibraryResourceName = "RoundLibrary";
        public const string PrefabResourceName = "GameState";

        private void Awake()
        {
            Instance = this;
            Results = new NetworkList<PlayerRoundResult>();
            if (library == null) library = Resources.Load<RoundLibrary>(LibraryResourceName);
            if (library == null) Debug.LogError("[GameState] No RoundLibrary found (Resources/RoundLibrary). Run Tools > Smartest > Build Scenes.");
        }

#if UNITY_EDITOR
        /// <summary>Used by SceneBuilder when authoring the prefab.</summary>
        public void EditorSetLibrary(RoundLibrary lib) => library = lib;
#endif

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Instance = this;
            Phase.OnValueChanged += OnPhaseChanged;
            RoundDefId.OnValueChanged += OnRoundChanged;
            SubRound.OnValueChanged += OnSubRoundChanged;
            Results.OnListChanged += OnResultsChanged;

            if (IsServer) ServerStartMatch();
            Spawned?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            Phase.OnValueChanged -= OnPhaseChanged;
            RoundDefId.OnValueChanged -= OnRoundChanged;
            SubRound.OnValueChanged -= OnSubRoundChanged;
            Results.OnListChanged -= OnResultsChanged;
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        private void OnPhaseChanged(GamePhase prev, GamePhase now) => PhaseChanged?.Invoke(now);
        private void OnRoundChanged(int prev, int now) => RoundChanged?.Invoke();
        private void OnSubRoundChanged(int prev, int now) => RoundChanged?.Invoke();
        private void OnResultsChanged(NetworkListEvent<PlayerRoundResult> e) => ResultsChanged?.Invoke();

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;
            ServerTick();
        }

        // ------------------------------------------------------------------
        // Server: match flow
        // ------------------------------------------------------------------

        private void ServerStartMatch()
        {
            if (library == null || library.rounds.Count == 0)
            {
                Debug.LogError("[GameState] RoundLibrary is missing or empty. Run Tools > Smartest > Build Scenes.");
                return;
            }
            _rng = new System.Random(Environment.TickCount);
            _deck = new ChallengeDeck(library.SocialIds(), library.MinigameIds(),
                id => library.GetById(id)?.inputType, Environment.TickCount,
                Config.alternateSocialAndMinigame);
            _current = null;
            _continueSubRounds = false;
            _forceRedGreenNext = false;
            _pendingPredictions = null;
            RoundIndex.Value = 0;
            WinnerClientId.Value = ulong.MaxValue;
            TieBreak.Value = false;
            foreach (var p in PlayerData.All) p.ServerResetForNewMatch();
            ServerBeginRoundIntro();
        }

        private void ServerTick()
        {
            double now = ServerNow;
            switch (Phase.Value)
            {
                case GamePhase.RoundIntro:
                    if (now >= _phaseEnd)
                    {
                        if (_current != null && _current.IsMinigame) ServerBeginLevel();
                        else ServerBeginAnswering();
                    }
                    break;

                case GamePhase.Answering:
                    if (!_graceApplied && AllLockedIn())
                    {
                        _graceApplied = true;
                        double early = now + Config.lockAllGraceSeconds;
                        if (early < _phaseEnd)
                        {
                            _phaseEnd = early;
                            PhaseEndTime.Value = _phaseEnd;
                        }
                    }
                    if (now >= _phaseEnd) ServerBeginReveal();
                    break;

                case GamePhase.Play:
                    ServerPruneLadder();
                    if (now >= _phaseEnd) ServerEndLevel();
                    break;

                case GamePhase.LevelResult:
                    if (now >= _phaseEnd) ServerBeginLevel();
                    break;

                case GamePhase.Reveal:
                    if (now >= _phaseEnd) ServerBeginScoring();
                    break;

                case GamePhase.Scoring:
                    if (now >= _phaseEnd) ServerEndScoring();
                    break;
            }
        }

        private bool AllLockedIn()
        {
            var all = PlayerData.All;
            if (all.Count == 0) return false;
            for (int i = 0; i < all.Count; i++) if (!all[i].LockedIn.Value) return false;
            return true;
        }

        private void ServerBeginRoundIntro()
        {
            if (_current != null && _continueSubRounds)
            {
                _subRound++;
            }
            else
            {
                int id = _deck.Draw(_forceRedGreenNext ? InputType.RedGreen : (InputType?)null);
                _forceRedGreenNext = false;
                _current = library.GetById(id);
                _subRound = 1;
                RoundIndex.Value = RoundIndex.Value + 1;
            }
            _continueSubRounds = false;

            foreach (var p in PlayerData.All)
            {
                p.LockedIn.Value = false;
                p.CurrentAnswer.Value = -1;
                p.PlayState.Value = PlayerData.PlayStateOut;
            }
            Results.Clear();
            RevealLine.Value = new FixedString512Bytes(string.Empty);
            LevelLine.Value = new FixedString128Bytes(string.Empty);
            SubRound.Value = _subRound;
            RoundDefId.Value = _current.id;
            LevelTieBreak.Value = false;
            LevelOutcome.Value = OutcomeNone;

            float intro = Config.roundIntroSeconds;
            if (_current.IsMinigame)
            {
                ServerSetupMinigame();
                intro = Config.minigameIntroSeconds;
            }
            else
            {
                _entry = null;
                _ladder = null;
                AliveCount.Value = 0;
                MinigameSeed.Value = 0;
            }

            SetPhase(GamePhase.RoundIntro, intro);
        }

        // ------------------------------------------------------------------
        // Server: minigames
        // ------------------------------------------------------------------

        private void ServerSetupMinigame()
        {
            _entry = MinigameRegistry.Get(_current.minigameId);
            if (_entry == null)
            {
                Debug.LogError($"[GameState] No minigame registered as '{_current.minigameId}'. Run Tools > Smartest > Build Scenes.");
                _entry = MinigameRegistry.All.Count > 0 ? MinigameRegistry.All[0] : null;
            }

            _roundPlayers = new List<PlayerData>(PlayerData.All);
            var ids = new List<ulong>(_roundPlayers.Count);
            for (int i = 0; i < _roundPlayers.Count; i++) ids.Add(_roundPlayers[i].OwnerClientId);

            _solo = ids.Count <= 1;
            _soloLevel = 1;
            _levelReports.Clear();
            _ladder = new EliminationLadder(ids, _entry != null ? _entry.Order : MetricOrder.LowerIsBetter,
                Config.minigameMaxLevels);
            MinigameSeed.Value = _rng.Next(1, int.MaxValue);
            AliveCount.Value = ids.Count;
        }

        private void ServerBeginLevel()
        {
            if (_entry == null || _ladder == null) { ServerBeginReveal(); return; }
            if (!_solo && _ladder.Finished) { ServerFinishMinigame(); return; }

            _levelReports.Clear();
            _levelClosing = false;

            int level = _solo ? _soloLevel : _ladder.Level;
            var participants = _solo ? (IReadOnlyList<ulong>)AllIds() : _ladder.Participants;

            foreach (var p in PlayerData.All)
            {
                bool plays = Contains(participants, p.OwnerClientId);
                bool alive = _solo || Contains(_ladder.Alive, p.OwnerClientId);
                p.PlayState.Value = plays ? PlayerData.PlayStatePlaying
                    : (alive ? PlayerData.PlayStateWatching : PlayerData.PlayStateOut);
            }

            SubRound.Value = level;
            LevelTieBreak.Value = !_solo && _ladder.InTieBreak;
            AliveCount.Value = _solo ? 1 : _ladder.Alive.Count;
            LevelLine.Value = new FixedString128Bytes(string.Empty);

            // The extra grace is for the wire: a result submitted right on the buzzer still
            // has to reach the host, and a player who made it shouldn't be marked as a fail.
            float lead = Mathf.Max(0.5f, Config.minigameLeadInSeconds);
            SetPhase(GamePhase.Play, lead + _entry.LevelSeconds + Config.levelReportGraceSeconds);
        }

        /// <summary>Called by PlayerData's server RPC once per level, per player.</summary>
        public void ServerOnLevelResult(PlayerData player, int level, bool failed, int metric)
        {
            if (!IsServer || Phase.Value != GamePhase.Play) return;
            if (player == null || !player.IsPlayingLevel) return;
            if (level != SubRound.Value) return;
            ulong id = player.OwnerClientId;
            if (_levelReports.ContainsKey(id)) return;

            _levelReports[id] = new LevelReport(id, failed, metric);

            // Everyone has reported — close the level after a short grace instead of
            // making the room wait out the clock.
            if (!_levelClosing && AllParticipantsReported())
            {
                _levelClosing = true;
                double early = ServerNow + Config.levelReportGraceSeconds;
                if (early < _phaseEnd)
                {
                    _phaseEnd = early;
                    PhaseEndTime.Value = _phaseEnd;
                }
            }
        }

        private bool AllParticipantsReported()
        {
            var participants = _solo ? (IReadOnlyList<ulong>)AllIds() : _ladder.Participants;
            for (int i = 0; i < participants.Count; i++)
            {
                ulong id = participants[i];
                if (PlayerData.Get(id) == null) continue; // gone; the prune handles it
                if (!_levelReports.ContainsKey(id)) return false;
            }
            return true;
        }

        /// <summary>Someone left mid-minigame: take them out of the ladder, and end it if only one is left.</summary>
        private void ServerPruneLadder()
        {
            if (_ladder == null || _solo) return;
            for (int i = _ladder.Alive.Count - 1; i >= 0; i--)
            {
                ulong id = _ladder.Alive[i];
                if (PlayerData.Get(id) == null) _ladder.Remove(id);
            }
            if (_ladder.Finished) ServerFinishMinigame();
        }

        private void ServerEndLevel()
        {
            if (_entry == null || _ladder == null) { ServerBeginReveal(); return; }

            if (_solo)
            {
                ulong me = PlayerData.All.Count > 0 ? PlayerData.All[0].OwnerClientId : 0UL;
                bool cleared = _levelReports.TryGetValue(me, out var solo) && !solo.Failed;
                if (!cleared)
                {
                    _soloCleared = false;
                    ServerFinishMinigame();
                    return;
                }
                if (_soloLevel >= Math.Max(1, Config.minigameSoloLevels))
                {
                    _soloCleared = true;
                    ServerFinishMinigame();
                    return;
                }
                _soloLevel++;
                LevelLine.Value = new FixedString128Bytes($"Level {_soloLevel - 1} cleared.");
                LevelOutcome.Value = OutcomeEliminated;
                SetPhase(GamePhase.LevelResult, Config.levelResultSeconds);
                return;
            }

            var reports = new List<LevelReport>(_levelReports.Count);
            foreach (var kv in _levelReports) reports.Add(kv.Value);

            var step = _ladder.Submit(reports);
            LevelLine.Value = new FixedString128Bytes(Truncate(DescribeStep(step), 120));
            LevelOutcome.Value = step.RepeatHarder ? OutcomeRepeat
                : step.TieBreak ? OutcomeTieBreak
                : step.Eliminated != null && step.Eliminated.Count > 0 ? OutcomeEliminated
                : OutcomeNone;

            if (step.Finished) ServerFinishMinigame(step);
            else SetPhase(GamePhase.LevelResult, Config.levelResultSeconds);
        }

        private bool _soloCleared;

        private string DescribeStep(LadderStep step)
        {
            if (step == null) return string.Empty;
            if (step.RepeatHarder) return "Everyone failed. Again — harder.";
            if (step.TieBreak)
            {
                var names = new List<string>();
                foreach (var id in step.TiedPlayers) names.Add(NameOf(id));
                return "Dead heat. " + JoinNames(names) + " play it off.";
            }
            if (step.Eliminated != null && step.Eliminated.Count > 0)
            {
                var names = new List<string>();
                foreach (var id in step.Eliminated) names.Add(NameOf(id));
                return "OUT: " + JoinNames(names);
            }
            return string.Empty;
        }

        private void ServerFinishMinigame(LadderStep step = null)
        {
            _roundPlayers = new List<PlayerData>(PlayerData.All);
            int n = _roundPlayers.Count;
            _finalDeltas = new int[n];
            var places = new Dictionary<ulong, int>();
            string line;

            if (_solo)
            {
                int pts = _soloCleared ? Config.minigameSoloClearPoints : 0;
                for (int i = 0; i < n; i++) { _finalDeltas[i] = pts; places[_roundPlayers[i].OwnerClientId] = 1; }
                line = _soloCleared
                    ? $"All {Math.Max(1, Config.minigameSoloLevels)} levels. {pts} points."
                    : "Not this time. Nothing scored.";
            }
            else
            {
                var finalPlaces = step != null && step.Places != null && step.Places.Count > 0
                    ? step.Places
                    : _ladder.FinalPlaces();
                var deltas = PayoutTable.Deltas(finalPlaces, Config.minigamePlacePoints, Config.minigameLastPlacePoints);
                for (int i = 0; i < n; i++)
                {
                    ulong id = _roundPlayers[i].OwnerClientId;
                    places[id] = finalPlaces.TryGetValue(id, out int pl) ? pl : 0;
                    _finalDeltas[i] = deltas.TryGetValue(id, out int d) ? d : 0;
                }

                ulong winner = 0UL;
                bool haveWinner = false;
                foreach (var kv in finalPlaces) if (kv.Value == 1) { winner = kv.Key; haveWinner = true; break; }
                line = haveWinner
                    ? $"{NameOf(winner)} takes it."
                    : "Nobody survived that one.";
            }

            foreach (var p in PlayerData.All) p.PlayState.Value = PlayerData.PlayStateOut;

            Results.Clear();
            for (int i = 0; i < n; i++)
            {
                Results.Add(new PlayerRoundResult
                {
                    ClientId = _roundPlayers[i].OwnerClientId,
                    Answer = -1,
                    Delta = _finalDeltas[i],
                    Place = places.TryGetValue(_roundPlayers[i].OwnerClientId, out int pl) ? pl : 0
                });
            }
            RevealLine.Value = new FixedString512Bytes(Truncate(line, 500));
            _continueSubRounds = false;

            float seconds = Mathf.Max(Config.revealMinSeconds, n * Config.revealSecondsPerRow) + 1.0f;
            SetPhase(GamePhase.Reveal, seconds);
        }

        // ------------------------------------------------------------------
        // Server: social rounds
        // ------------------------------------------------------------------

        private void ServerBeginAnswering()
        {
            _graceApplied = false;
            float seconds = _current.answerSeconds > 0 ? _current.answerSeconds : Config.defaultAnswerSeconds;
            SetPhase(GamePhase.Answering, seconds);
        }

        /// <summary>Called by PlayerData's server RPC.</summary>
        public void ServerOnAnswer(PlayerData player, int answer)
        {
            if (!IsServer || Phase.Value != GamePhase.Answering || player == null || _current == null) return;
            if (player.LockedIn.Value) return;
            if (!_current.IsValidAnswer(answer)) return;
            player.CurrentAnswer.Value = answer;
            player.LockedIn.Value = true;
        }

        private void ServerBeginReveal()
        {
            _roundPlayers = new List<PlayerData>(PlayerData.All);
            int n = _roundPlayers.Count;

            var answers = new int[n];
            var scores = new int[n];
            var names = new string[n];
            for (int i = 0; i < n; i++)
            {
                var p = _roundPlayers[i];
                answers[i] = p.LockedIn.Value ? p.CurrentAnswer.Value : -1;
                scores[i] = p.Score.Value;
                names[i] = p.DisplayName;
            }

            var ctx = new RoundContext
            {
                PlayerCount = n,
                Answers = answers,
                Scores = scores,
                Names = names,
                SubRound = _subRound
            };

            RoundResult result = ResolverRegistry.Resolve(_current, ctx);
            _continueSubRounds = result.ContinueSubRounds;
            string line = !string.IsNullOrEmpty(result.RuntimeLine) ? result.RuntimeLine : _current.LineFor(result.OutcomeKey);

            // Predict the Room: remember predictions, force a RedGreen round next.
            if (_current.resolver == ResolverType.PredictTheRoom)
            {
                _predictDef = _current;
                _pendingPredictions = new Dictionary<ulong, int>();
                for (int i = 0; i < n; i++) _pendingPredictions[_roundPlayers[i].OwnerClientId] = answers[i];
                _forceRedGreenNext = true;
            }
            // ...and score those predictions once the follow-up RedGreen round resolves.
            else if (_pendingPredictions != null && _current.inputType == InputType.RedGreen && !_continueSubRounds)
            {
                int redCount = 0;
                for (int i = 0; i < n; i++) if (answers[i] == 1) redCount++;
                var preds = new int[n];
                for (int i = 0; i < n; i++)
                    preds[i] = _pendingPredictions.TryGetValue(_roundPlayers[i].OwnerClientId, out var v) ? v : -1;
                var bonus = PredictTheRoomResolver.ScoreFollowUp(_predictDef, preds, redCount, ctx);
                for (int i = 0; i < n; i++) result.Deltas[i] += bonus.Deltas[i];
                if (!string.IsNullOrEmpty(bonus.RuntimeLine)) line = line + "\n" + bonus.RuntimeLine;
                _pendingPredictions = null;
                _predictDef = null;
            }

            _finalDeltas = new int[n];
            for (int i = 0; i < n; i++)
                _finalDeltas[i] = result.Deltas != null && i < result.Deltas.Length ? result.Deltas[i] : 0;

            Results.Clear();
            for (int i = 0; i < n; i++)
            {
                Results.Add(new PlayerRoundResult
                {
                    ClientId = _roundPlayers[i].OwnerClientId,
                    Answer = answers[i],
                    Delta = _finalDeltas[i],
                    Place = 0
                });
            }
            RevealLine.Value = new FixedString512Bytes(Truncate(line, 500));

            float seconds = Mathf.Max(Config.revealMinSeconds, n * Config.revealSecondsPerRow) + 1.0f;
            SetPhase(GamePhase.Reveal, seconds);
        }

        private void ServerBeginScoring()
        {
            if (_continueSubRounds)
            {
                // Multi-step round: nothing to score yet, straight into the next pull.
                ServerBeginRoundIntro();
                return;
            }
            for (int i = 0; i < _roundPlayers.Count; i++)
            {
                var p = _roundPlayers[i];
                if (p == null || !p.IsSpawned) continue;
                if (i >= _finalDeltas.Length) break;
                p.Score.Value = p.Score.Value + _finalDeltas[i];
            }
            SetPhase(GamePhase.Scoring, Config.scoringSeconds);
        }

        private void ServerEndScoring()
        {
            int target = Config.targetScore;
            int top = int.MinValue;
            var leaders = new List<PlayerData>();
            foreach (var p in PlayerData.All)
            {
                int s = p.Score.Value;
                if (s > top) { top = s; leaders.Clear(); leaders.Add(p); }
                else if (s == top) leaders.Add(p);
            }

            if (top >= target && leaders.Count == 1)
            {
                TieBreak.Value = false;
                WinnerClientId.Value = leaders[0].OwnerClientId;
                SetPhase(GamePhase.Winner, 0f);
                return;
            }
            TieBreak.Value = top >= target && leaders.Count > 1;
            ServerBeginRoundIntro();
        }

        private void SetPhase(GamePhase phase, float duration)
        {
            _phaseEnd = ServerNow + duration;
            PhaseEndTime.Value = _phaseEnd;
            PhaseDuration.Value = duration;
            Phase.Value = phase;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static bool Contains(IReadOnlyList<ulong> list, ulong id)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++) if (list[i] == id) return true;
            return false;
        }

        private static List<ulong> AllIds()
        {
            var ids = new List<ulong>();
            var all = PlayerData.All;
            for (int i = 0; i < all.Count; i++) ids.Add(all[i].OwnerClientId);
            return ids;
        }

        private static string NameOf(ulong clientId)
        {
            var p = PlayerData.Get(clientId);
            return p != null ? p.DisplayName : "Player";
        }

        private static string JoinNames(List<string> names)
        {
            if (names == null || names.Count == 0) return "Nobody";
            if (names.Count == 1) return names[0];
            if (names.Count == 2) return names[0] + " and " + names[1];
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(i == names.Count - 1 ? " and " : ", ");
                sb.Append(names[i]);
            }
            return sb.ToString();
        }

        public bool TryGetResult(ulong clientId, out PlayerRoundResult r)
        {
            for (int i = 0; i < Results.Count; i++)
            {
                if (Results[i].ClientId == clientId) { r = Results[i]; return true; }
            }
            r = default;
            return false;
        }
    }
}
