using System.Collections.Generic;
using Smartest.Core;
using Smartest.Minigames;
using Smartest.Net;
using Smartest.Rounds;
using TMPro;
using UnityEngine;

namespace Smartest.UI
{
    /// <summary>
    /// Drives the Game scene from GameState's networked phase. Every transition is a
    /// reaction to a change callback; the only per-frame work is the countdown bar.
    /// Also fires all in-game voice lines (local only).
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [SerializeField] private Panel scoreboardPanel;
        [SerializeField] private ScoreboardUI scoreboard;
        [SerializeField] private RoundPanel roundPanel;
        [SerializeField] private MinigameStage minigameStage;
        [SerializeField] private RevealPanel revealPanel;
        [SerializeField] private WinnerPanel winnerPanel;
        [SerializeField] private TMP_Text roundCounter;

        private GameState _gs;
        private bool _bound;
        private bool _timerTenFired;
        private bool _wasPlayingLevel;
        private GamePhase _shownPhase = GamePhase.Idle;

        // For the "wait, they were last!" line: where everyone stood before this round scored.
        private readonly Dictionary<ulong, int> _rankBefore = new Dictionary<ulong, int>();

        private GameConfig Config => GameBootstrap.ConfigOrDefault;

        private void Start()
        {
            if (scoreboardPanel != null) scoreboardPanel.ShowInstant();
            if (roundPanel != null) roundPanel.HideInstant();
            if (minigameStage != null) minigameStage.HideInstant();
            if (revealPanel != null) revealPanel.HideInstant();
            if (winnerPanel != null) winnerPanel.HideInstant();
            if (scoreboard != null) scoreboard.LeaderChanged += OnLeaderChanged;
            if (minigameStage != null) minigameStage.LevelFinished += OnLevelFinished;

            VoiceLines.Play(VoiceKeys.GameStart);

            if (GameState.Instance != null && GameState.Instance.IsSpawned) Bind();
            else GameState.Spawned += OnGameStateSpawned;

            // Host safety net: if the scene-load-complete event never spawned the engine, do it here.
            if (NetSession.Instance != null && NetSession.Instance.IsHost)
                StartCoroutine(SpawnFallback());
        }

        private System.Collections.IEnumerator SpawnFallback()
        {
            yield return new WaitForSecondsRealtime(4f);
            if (!_bound && NetSession.Instance != null && NetSession.Instance.IsHost)
            {
                Debug.LogWarning("[GameUI] GameState not spawned after 4s — spawning it now.");
                NetSession.Instance.EnsureGameStateSpawned();
            }
        }

        private void OnDestroy()
        {
            GameState.Spawned -= OnGameStateSpawned;
            Unbind();
            if (scoreboard != null) scoreboard.LeaderChanged -= OnLeaderChanged;
            if (minigameStage != null) minigameStage.LevelFinished -= OnLevelFinished;
        }

        private void OnGameStateSpawned()
        {
            GameState.Spawned -= OnGameStateSpawned;
            Bind();
        }

        private void Bind()
        {
            if (_bound) return;
            _gs = GameState.Instance;
            if (_gs == null) return;
            _bound = true;
            GameState.PhaseChanged += OnPhase;
            GameState.RoundChanged += OnRoundChanged;
            if (_gs.Phase.Value != GamePhase.Idle) OnPhase(_gs.Phase.Value);
        }

        private void Unbind()
        {
            if (!_bound) return;
            _bound = false;
            GameState.PhaseChanged -= OnPhase;
            GameState.RoundChanged -= OnRoundChanged;
        }

        private void Update()
        {
            if (!_bound || _gs == null || !_gs.IsSpawned) return;
            if (_gs.Phase.Value != GamePhase.Answering) return;

            float remaining = _gs.RemainingSeconds;
            if (roundPanel != null) roundPanel.Tick(remaining, _gs.PhaseDuration.Value);
            if (!_timerTenFired && remaining <= 10f && _gs.PhaseDuration.Value > 10f)
            {
                _timerTenFired = true;
                VoiceLines.Play(VoiceKeys.TimerTen);
            }
        }

        // ------------------------------------------------------------------

        private void OnRoundChanged()
        {
            if (_gs == null) return;
            var phase = _gs.Phase.Value;
            // The definition or level changed while (or right before) the intro — refresh.
            if (phase == GamePhase.RoundIntro || phase == GamePhase.Answering)
                PopulateRound();
            if (roundCounter != null)
                roundCounter.text = _gs.CurrentDef != null ? $"ROUND {_gs.RoundIndex.Value}" : string.Empty;
        }

        private void PopulateRound()
        {
            var def = _gs.CurrentDef;
            if (def == null) return;

            if (def.IsMinigame)
            {
                if (minigameStage != null) minigameStage.ShowIntro(def.title, def.ruleText);
            }
            else if (roundPanel != null)
            {
                roundPanel.ShowRound(def, _gs.SubRound.Value, _gs.TieBreak.Value);
            }

            if (roundCounter != null) roundCounter.text = $"ROUND {_gs.RoundIndex.Value}";
        }

        private void OnPhase(GamePhase phase)
        {
            if (_gs == null) return;
            _shownPhase = phase;
            var def = _gs.CurrentDef;
            bool minigame = def != null && def.IsMinigame;

            switch (phase)
            {
                case GamePhase.RoundIntro:
                    _timerTenFired = false;
                    _wasPlayingLevel = false;
                    if (revealPanel != null && revealPanel.IsShown) revealPanel.Hide();
                    if (winnerPanel != null && winnerPanel.IsShown) winnerPanel.Hide();
                    PopulateRound();
                    if (minigame)
                    {
                        if (roundPanel != null && roundPanel.IsShown) roundPanel.Hide();
                        if (minigameStage != null && !minigameStage.IsShown) minigameStage.Show();
                        // Prefer the line written for this specific game; fall back to a
                        // generic one while the clip hasn't been recorded yet.
                        string intro = VoiceKeys.Minigame(def.minigameId);
                        if (VoiceLines.Has(intro)) VoiceLines.Play(intro);
                        else VoiceLines.Play(VoiceKeys.MinigameStart);
                    }
                    else
                    {
                        if (minigameStage != null && minigameStage.IsShown) minigameStage.Hide();
                        if (roundPanel != null && !roundPanel.IsShown) roundPanel.Show();
                        VoiceLines.Play(VoiceKeys.RoundStart);
                    }
                    break;

                case GamePhase.Answering:
                    if (roundPanel != null)
                    {
                        if (!roundPanel.IsShown) { PopulateRound(); roundPanel.Show(); }
                        roundPanel.SetAnswering(true);
                    }
                    break;

                case GamePhase.Play:
                    StartLevel();
                    break;

                case GamePhase.LevelResult:
                    if (minigameStage != null) minigameStage.ShowLevelResult(_gs.LevelLine.Value.ToString());
                    OnLevelResolved();
                    break;

                case GamePhase.Reveal:
                    if (roundPanel != null)
                    {
                        roundPanel.SetAnswering(false);
                        if (roundPanel.IsShown) roundPanel.Hide();
                    }
                    if (minigameStage != null)
                    {
                        minigameStage.EndMinigame();
                        if (minigameStage.IsShown) minigameStage.Hide();
                    }
                    CaptureRanks();
                    ShowReveal();
                    break;

                case GamePhase.Scoring:
                    if (revealPanel != null) revealPanel.AnimateDeltas(Config.scoreCountDuration);
                    CommentOnScores();
                    break;

                case GamePhase.Winner:
                    if (roundPanel != null && roundPanel.IsShown) roundPanel.Hide();
                    if (minigameStage != null && minigameStage.IsShown) minigameStage.Hide();
                    if (revealPanel != null && revealPanel.IsShown) revealPanel.Hide();
                    ShowWinner();
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Minigames
        // ------------------------------------------------------------------

        private void StartLevel()
        {
            var entry = _gs.CurrentMinigame;
            if (entry == null || minigameStage == null) return;

            var local = PlayerData.Local;
            bool playing = local != null && local.IsPlayingLevel;
            _wasPlayingLevel = playing;

            if (!minigameStage.IsShown) minigameStage.Show();
            minigameStage.StartLevel(entry, _gs.SubRound.Value, _gs.MinigameSeed.Value, playing,
                _gs.AliveCount.Value, _gs.LevelStartsIn, entry.LevelSeconds, _gs.LevelTieBreak.Value);

            if (_gs.LevelTieBreak.Value) VoiceLines.Play(VoiceKeys.MinigameTieBreak);
            else if (_gs.AliveCount.Value == 2) VoiceLines.Play(VoiceKeys.MinigameLastTwo);
            else if (_gs.SubRound.Value > 1) VoiceLines.Play(VoiceKeys.LevelUp);
        }

        /// <summary>The beat between levels: who's out, who survived, or "everyone, again".</summary>
        private void OnLevelResolved()
        {
            byte outcome = _gs.LevelOutcome.Value;
            bool stillIn = LocalIsStillIn();

            if (outcome == GameState.OutcomeRepeat)
            {
                VoiceLines.Play(VoiceKeys.MinigameRepeat);
                return;
            }
            if (outcome == GameState.OutcomeTieBreak) return; // the tie-break line plays as that level opens

            if (_wasPlayingLevel && !stillIn)
            {
                Sounds.Play(Sounds.Kind.Out);
                VoiceLines.Play(VoiceKeys.YouAreOut);
            }
            else if (_wasPlayingLevel && stillIn)
            {
                VoiceLines.Play(VoiceKeys.MinigameSurvived);
            }
        }

        private bool LocalIsStillIn()
        {
            var local = PlayerData.Local;
            return local != null && local.IsStillIn;
        }

        private void OnLevelFinished(bool failed, int metric)
        {
            var local = PlayerData.Local;
            if (local == null || _gs == null) return;
            Sounds.Play(failed ? Sounds.Kind.Bad : Sounds.Kind.Good);
            // Cleared it with most of the clock left — worth a word.
            if (!failed && _gs.RemainingSeconds > _gs.PhaseDuration.Value * 0.45f)
                VoiceLines.Play(VoiceKeys.MinigameClean);
            local.SubmitLevelResultRpc(_gs.SubRound.Value, failed, metric);
        }

        // ------------------------------------------------------------------

        private void ShowReveal()
        {
            var def = _gs.CurrentDef;
            var results = new List<PlayerRoundResult>();
            for (int i = 0; i < _gs.Results.Count; i++) results.Add(_gs.Results[i]);
            string line = _gs.RevealLine.Value.ToString();

            if (revealPanel != null)
            {
                revealPanel.ShowResults(def, results, line, Config.revealRowStagger);
                if (!revealPanel.IsShown) revealPanel.Show();
            }

            if (def != null && def.IsMinigame)
            {
                Sounds.Play(Sounds.Kind.Good);
                if (results.Count >= 2) VoiceLines.Play(VoiceKeys.MinigameWinner);
                return;
            }

            // Voice: order matters — the "everyone lost" line always plays; the others are chance-based.
            VoiceLines.Play(VoiceKeys.TimerLocked);
            if (results.Count == 0) return;

            bool allSame = true, allLost = true, nobodyAnswered = true;
            int winners = 0, ones = 0;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].Answer != results[0].Answer) allSame = false;
                if (results[i].Delta >= 0) allLost = false;
                if (results[i].Delta > 0) winners++;
                if (results[i].Answer >= 0) nobodyAnswered = false;
                if (results[i].Answer == 1) ones++;
            }

            if (nobodyAnswered) { VoiceLines.Play(VoiceKeys.NobodyAnswered); return; }
            if (results.Count < 2) return;

            if (allLost) VoiceLines.Play(VoiceKeys.RevealAllLost);
            else if (winners == 1) VoiceLines.Play(VoiceKeys.RevealOneWinner);
            else if (allSame) VoiceLines.Play(VoiceKeys.RevealEveryoneSame);
            else if (def != null && def.inputType != InputType.Number1to10 &&
                     Mathf.Abs(ones * 2 - results.Count) <= 1)
                VoiceLines.Play(VoiceKeys.RevealSplit);
        }

        /// <summary>Standings before the deltas land, so a comeback can be spotted after.</summary>
        private void CaptureRanks()
        {
            _rankBefore.Clear();
            var ranked = Ranked();
            for (int i = 0; i < ranked.Count; i++) _rankBefore[ranked[i]] = i + 1;
        }

        private static List<ulong> Ranked()
        {
            var players = new List<PlayerData>(PlayerData.All);
            players.Sort((a, b) => b.Score.Value.CompareTo(a.Score.Value));
            var ids = new List<ulong>(players.Count);
            foreach (var p in players) ids.Add(p.OwnerClientId);
            return ids;
        }

        /// <summary>
        /// The host reacting to the scoreboard: a big swing for you, somebody climbing out
        /// of last, somebody getting close enough to a hundred that you should worry.
        /// </summary>
        private void CommentOnScores()
        {
            var local = PlayerData.Local;
            if (local != null && _gs.TryGetResult(local.OwnerClientId, out var mine))
            {
                if (mine.Delta >= 10) { Sounds.Play(Sounds.Kind.Good); VoiceLines.Play(VoiceKeys.ScoreBigGain); }
                else if (mine.Delta <= -8) { Sounds.Play(Sounds.Kind.Bad); VoiceLines.Play(VoiceKeys.ScoreBigLoss); }
            }

            var players = PlayerData.All;
            if (players.Count < 2) return;

            var after = Ranked();
            for (int i = 0; i < after.Count; i++)
            {
                if (!_rankBefore.TryGetValue(after[i], out int before)) continue;
                // Last place to the top half in one round is worth shouting about.
                if (before == _rankBefore.Count && i + 1 <= Mathf.Max(1, after.Count / 2))
                {
                    VoiceLines.Play(VoiceKeys.Comeback);
                    break;
                }
            }

            int target = Config.targetScore;
            int top = int.MinValue, bottom = int.MaxValue, secondLowest = int.MaxValue;
            foreach (var p in players)
            {
                int s = p.Score.Value;
                if (s > top) top = s;
                if (s < bottom) { secondLowest = bottom; bottom = s; }
                else if (s < secondLowest) secondLowest = s;
            }

            if (top >= target - 15 && top < target) VoiceLines.Play(VoiceKeys.CloseToWin);
            else if (players.Count >= 3 && secondLowest != int.MaxValue && secondLowest - bottom >= 20)
                VoiceLines.Play(VoiceKeys.LastPlace);
        }

        private void ShowWinner()
        {
            var players = PlayerData.All;
            var winner = PlayerData.Get(_gs.WinnerClientId.Value);
            bool isHost = NetSession.Instance != null && NetSession.Instance.IsHost;
            if (winnerPanel != null)
            {
                winnerPanel.ShowWinner(winner, players, isHost);
                if (!winnerPanel.IsShown) winnerPanel.Show();
            }
            Sounds.Play(Sounds.Kind.Fanfare);
            VoiceLines.Play(VoiceKeys.Winner);
            if (players.Count >= 3) VoiceLines.PlayDelayed(VoiceKeys.WinnerLastPlace, 1.5f);
        }

        private void OnLeaderChanged(ulong newLeader)
        {
            if (_shownPhase == GamePhase.Scoring || _shownPhase == GamePhase.Reveal)
                VoiceLines.Play(VoiceKeys.LeadChange);
        }
    }
}
