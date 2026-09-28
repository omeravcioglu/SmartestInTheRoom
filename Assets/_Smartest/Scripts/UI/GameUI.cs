using System.Collections.Generic;
using Smartest.Core;
using Smartest.Minigames;
using Smartest.Net;
using Smartest.Rounds;
using UnityEngine;

namespace Smartest.UI
{
    /// <summary>
    /// Drives the Game scene from GameState's networked phase. Every transition is a
    /// reaction to a change callback; the only per-frame work is the timers. The masthead
    /// and the seat rail stay up the whole match; the stage in the middle swaps between the
    /// round, the minigame and the reveal; the winner gets the whole page.
    /// Also fires all in-game voice lines (local only).
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("Always on screen")]
        [SerializeField] private CanvasGroup hud;
        [SerializeField] private Masthead masthead;
        [SerializeField] private SeatRail seatRail;
        [SerializeField] private HostCaption hostCaption;

        [Header("The stage")]
        [SerializeField] private RoundPanel roundPanel;
        [SerializeField] private MinigameStage minigameStage;
        [SerializeField] private RevealPanel revealPanel;
        [SerializeField] private WinnerPanel winnerPanel;

        private GameState _gs;
        private bool _bound;
        private bool _timerTenFired;
        private bool _lockedLineFired;
        private bool _wasPlayingLevel;
        private int _minigameStartCount;
        private bool _lastTwoAnnounced;
        private GamePhase _shownPhase = GamePhase.Idle;

        // For the "wait, they were last!" line: where everyone stood before this round scored.
        private readonly Dictionary<ulong, int> _rankBefore = new Dictionary<ulong, int>();

        private GameConfig Config => GameBootstrap.ConfigOrDefault;

        private void Start()
        {
            SetHud(true);
            if (roundPanel != null) roundPanel.HideInstant();
            if (minigameStage != null) minigameStage.HideInstant();
            if (revealPanel != null) revealPanel.HideInstant();
            if (winnerPanel != null) winnerPanel.HideInstant();
            if (seatRail != null) seatRail.LeaderChanged += OnLeaderChanged;
            if (minigameStage != null) minigameStage.LevelFinished += OnLevelFinished;
            PlayerData.RosterChanged += OnRosterChanged;

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
            PlayerData.RosterChanged -= OnRosterChanged;
            Unbind();
            if (seatRail != null) seatRail.LeaderChanged -= OnLeaderChanged;
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
            var phase = _gs.Phase.Value;
            var def = _gs.CurrentDef;
            float remaining = _gs.RemainingSeconds;

            if (phase == GamePhase.RoundIntro)
            {
                if (def != null && def.IsMinigame)
                {
                    if (minigameStage != null && _gs.PhaseDuration.Value > 0f)
                        minigameStage.SetFuse(remaining / _gs.PhaseDuration.Value);
                }
                else if (roundPanel != null) roundPanel.SetOpensIn(remaining);
                return;
            }

            if (phase != GamePhase.Answering) return;

            if (roundPanel != null)
            {
                roundPanel.Tick(remaining, _gs.PhaseDuration.Value);
                roundPanel.SetPicked(LockedCount(), PlayerData.All.Count);
            }
            if (!_timerTenFired && remaining <= 10f && _gs.PhaseDuration.Value > 10f)
            {
                _timerTenFired = true;
                VoiceLines.Play(VoiceKeys.TimerTen);
            }
            // "That's everybody. No takebacks." belongs to the moment the last answer lands,
            // not to the reveal, where the reveal's own line would cut it off anyway.
            if (!_lockedLineFired && PlayerData.All.Count > 0 && LockedCount() == PlayerData.All.Count)
            {
                _lockedLineFired = true;
                VoiceLines.Play(VoiceKeys.TimerLocked);
            }
        }

        private static int LockedCount()
        {
            int n = 0;
            var all = PlayerData.All;
            for (int i = 0; i < all.Count; i++) if (all[i].LockedIn.Value) n++;
            return n;
        }

        private void SetHud(bool on)
        {
            if (hud == null) return;
            hud.alpha = on ? 1f : 0f;
            hud.blocksRaycasts = on;
            hud.interactable = on;
        }

        // ------------------------------------------------------------------

        private void OnRoundChanged()
        {
            if (_gs == null) return;
            var phase = _gs.Phase.Value;
            // The definition or level changed while (or right before) the intro — refresh.
            if (phase == GamePhase.RoundIntro || phase == GamePhase.Answering)
                PopulateRound();
            UpdateMasthead();
        }

        private void UpdateMasthead()
        {
            if (masthead == null || _gs == null) return;
            var def = _gs.CurrentDef;
            masthead.SetRound(_gs.RoundIndex.Value, def != null && def.IsMinigame, _gs.TieBreak.Value);
        }

        private void PopulateRound()
        {
            var def = _gs.CurrentDef;
            if (def == null) return;

            if (def.IsMinigame)
            {
                // The registry is the source of truth for a minigame's rule (the level screen
                // reads it from there too); the asset copy only updates on Build Scenes.
                var entry = MinigameRegistry.Get(def.minigameId);
                if (minigameStage != null)
                    minigameStage.ShowIntro(entry, def.title, entry != null ? entry.Rule : def.ruleText, PlayerData.All.Count);
            }
            else if (roundPanel != null)
            {
                roundPanel.ShowRound(def, _gs.SubRound.Value, _gs.TieBreak.Value, TieSentence());
                roundPanel.SetPicked(LockedCount(), PlayerData.All.Count);
            }
            UpdateMasthead();
        }

        /// <summary>"Ayşe and Mert are both on 104. Everyone plays this round."</summary>
        private string TieSentence()
        {
            if (_gs == null || !_gs.TieBreak.Value) return string.Empty;
            int top = int.MinValue;
            foreach (var p in PlayerData.All) top = Mathf.Max(top, p.Score.Value);
            var names = new List<string>();
            foreach (var p in PlayerData.All) if (p.Score.Value == top) names.Add(p.DisplayName);
            if (names.Count < 2) return "Level at the top. Everyone plays this round.";
            string who = names.Count == 2
                ? $"{names[0]} and {names[1]} are both"
                : string.Join(", ", names.GetRange(0, names.Count - 1)) + $" and {names[names.Count - 1]} are all";
            return $"{who} on {top}. Everyone plays this round.";
        }

        private void OnPhase(GamePhase phase)
        {
            if (_gs == null) return;
            _shownPhase = phase;
            var def = _gs.CurrentDef;
            bool minigame = def != null && def.IsMinigame;
            UpdateMasthead();

            switch (phase)
            {
                case GamePhase.RoundIntro:
                    SetHud(true);
                    _timerTenFired = false;
                    _lockedLineFired = false;
                    _wasPlayingLevel = false;
                    _minigameStartCount = minigame ? _gs.AliveCount.Value : 0;
                    _lastTwoAnnounced = false;
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
                    ShowLevelResult();
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
                    if (hostCaption != null) hostCaption.Hide();
                    SetHud(false);
                    ShowWinner();
                    break;
            }
        }

        private void OnRosterChanged()
        {
            if (!_bound || _gs == null || !_gs.IsSpawned) return;
            var phase = _gs.Phase.Value;

            // A player's own PlayState can land a moment after the phase flip. If the level was
            // built for the wrong role and hasn't started, build it again for the right one.
            if (phase == GamePhase.Play && minigameStage != null && !minigameStage.LevelBegun)
            {
                var local = PlayerData.Local;
                bool playing = local != null && local.IsPlayingLevel;
                if (playing != _wasPlayingLevel) StartLevel();
            }
            // Likewise the list of who just went out.
            if (phase == GamePhase.LevelResult) ShowLevelResult();
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
            bool stillIn = local != null && local.IsStillIn;
            _wasPlayingLevel = playing;

            if (!minigameStage.IsShown) minigameStage.Show();
            minigameStage.StartLevel(entry, _gs.SubRound.Value, _gs.MinigameSeed.Value, playing,
                _gs.AliveCount.Value, _gs.LevelStartsIn, entry.LevelSeconds, _gs.LevelTieBreak.Value, stillIn);
            minigameStage.SetAlive(StillIn());

            // "Down to two" is news only once, and only if there used to be more of you —
            // in a two-player game it's been two since the start.
            bool downToTwo = _gs.AliveCount.Value == 2 && _minigameStartCount > 2 && !_lastTwoAnnounced;
            if (_gs.LevelTieBreak.Value) VoiceLines.Play(VoiceKeys.MinigameTieBreak);
            else if (downToTwo) { _lastTwoAnnounced = true; VoiceLines.Play(VoiceKeys.MinigameLastTwo); }
            else if (_gs.SubRound.Value > 1) VoiceLines.Play(VoiceKeys.LevelUp);
        }

        private static List<(string, bool)> StillIn()
        {
            var names = new List<string>();
            foreach (var p in PlayerData.All) names.Add(p.DisplayName);
            var monos = Monogram.ForAll(names);
            var list = new List<(string, bool)>();
            for (int i = 0; i < PlayerData.All.Count; i++)
                if (PlayerData.All[i].IsStillIn) list.Add((monos[i], PlayerData.All[i].IsOwner));
            return list;
        }

        private void ShowLevelResult()
        {
            if (minigameStage == null || _gs == null) return;
            int level = _gs.SubRound.Value;
            var names = new List<string>();
            foreach (var p in PlayerData.All) names.Add(p.DisplayName);
            var monos = Monogram.ForAll(names);
            var outNow = new List<(string, string)>();
            for (int i = 0; i < PlayerData.All.Count; i++)
                if (PlayerData.All[i].OutAtLevel.Value == level && level > 0) outNow.Add((names[i], monos[i]));

            int left = Mathf.Max(0, _gs.AliveCount.Value - outNow.Count);
            bool solo = PlayerData.All.Count <= 1;
            minigameStage.ShowLevelResult(_gs.LevelOutcome.Value, _gs.LevelLine.Value.ToString(), outNow, left, solo);
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
            if (local == null) return false;
            // At the level result the ladder has spoken but PlayState hasn't caught up yet.
            if (_gs != null && local.OutAtLevel.Value > 0 && local.OutAtLevel.Value == _gs.SubRound.Value) return false;
            return local.IsStillIn;
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
                // The line ("Burak takes it.") is the headline; no need to say it twice.
                Sounds.Play(Sounds.Kind.Good);
                if (results.Count >= 2) VoiceLines.Play(VoiceKeys.MinigameWinner);
                return;
            }

            // The round's own verdict goes in the host's bubble; a recorded line replaces it.
            if (hostCaption != null && !string.IsNullOrEmpty(line)) hostCaption.Say(line, 4.5f);

            // Voice: order matters — the "everyone lost" line always plays; the others are chance-based.
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

            Sounds.Play(Sounds.Kind.Fanfare);
            VoiceLines.Play(VoiceKeys.Winner);
            if (players.Count >= 3) VoiceLines.PlayDelayed(VoiceKeys.WinnerLastPlace, 1.5f);

            if (winnerPanel != null)
            {
                winnerPanel.ShowWinner(winner, players, isHost, _gs.RoundIndex.Value, VoiceLines.TextOf(VoiceKeys.Winner));
                if (!winnerPanel.IsShown) winnerPanel.Show();
            }
        }

        private void OnLeaderChanged(ulong newLeader)
        {
            if (_shownPhase == GamePhase.Scoring || _shownPhase == GamePhase.Reveal)
                VoiceLines.Play(VoiceKeys.LeadChange);
        }
    }
}
