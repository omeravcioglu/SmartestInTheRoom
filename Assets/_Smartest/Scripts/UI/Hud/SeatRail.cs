using System;
using System.Collections.Generic;
using Smartest.Core;
using Smartest.Net;
using Smartest.Rounds;
using UnityEngine;

namespace Smartest.UI
{
    /// <summary>
    /// The row of seats along the bottom of the game screen, in join order so nobody's seat
    /// moves when the scores change. Each seat says what its player is doing right now —
    /// thinking, locked, playing, done, out — and during a reveal what they picked and what
    /// it earned them. It also feeds the race to 100 in the masthead, and raises
    /// <see cref="LeaderChanged"/> for the host's voice line.
    /// </summary>
    [AddComponentMenu("Smartest/Seat Rail")]
    public class SeatRail : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private RaceTrack race;
        [SerializeField] private float gap = 16f;

        public event Action<ulong> LeaderChanged;

        private readonly Dictionary<ulong, SeatCard> _cards = new Dictionary<ulong, SeatCard>();
        private readonly Dictionary<ulong, int> _rankBefore = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, int> _scoreBefore = new Dictionary<ulong, int>();
        private readonly List<RaceTrack.Entry> _entries = new List<RaceTrack.Entry>();
        private ulong _leader = ulong.MaxValue;
        private bool _leaderKnown;
        private bool _movesShown;

        public static SeatRail Create(Transform parent, float left, float top, RaceTrack race)
        {
            var root = Ink.Node(parent, "SeatRail");
            root.At(left, top, 1824f, 176f);
            var rail = root.gameObject.AddComponent<SeatRail>();
            rail.root = root;
            rail.race = race;
            return rail;
        }

        private void OnEnable()
        {
            PlayerData.RosterChanged += Refresh;
            GameState.PhaseChanged += OnPhase;
            GameState.ResultsChanged += Refresh;
            GameState.RoundChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerData.RosterChanged -= Refresh;
            GameState.PhaseChanged -= OnPhase;
            GameState.ResultsChanged -= Refresh;
            GameState.RoundChanged -= Refresh;
        }

        private void OnPhase(GamePhase phase)
        {
            if (phase == GamePhase.Reveal) CaptureBefore();
            if (phase == GamePhase.RoundIntro)
            {
                _rankBefore.Clear();
                _scoreBefore.Clear();
                _movesShown = false;
                if (race != null) race.ClearMoves();
            }
            Refresh();
        }

        /// <summary>Standings before this round's points land, for the arrows and the race bars.</summary>
        private void CaptureBefore()
        {
            _rankBefore.Clear();
            _scoreBefore.Clear();
            foreach (var p in PlayerData.All)
            {
                _rankBefore[p.OwnerClientId] = RankOf(p.Score.Value);
                _scoreBefore[p.OwnerClientId] = p.Score.Value;
            }
        }

        private static int RankOf(int score)
        {
            int rank = 1;
            foreach (var q in PlayerData.All) if (q.Score.Value > score) rank++;
            return rank;
        }

        // ------------------------------------------------------------------

        public void Refresh()
        {
            if (root == null) return;
            var cfg = GameBootstrap.ConfigOrDefault;
            var players = PlayerData.All;

            // Seats for players who left go; new players take the next seat.
            var present = new HashSet<ulong>();
            foreach (var p in players) present.Add(p.OwnerClientId);
            var gone = new List<ulong>();
            foreach (var kv in _cards) if (!present.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone)
            {
                if (_cards[id] != null) Destroy(_cards[id].gameObject);
                _cards.Remove(id);
            }

            var names = new List<string>(players.Count);
            foreach (var p in players) names.Add(p.DisplayName);
            var monos = Monogram.ForAll(names);

            var gs = GameState.Instance;
            bool live = gs != null && gs.IsSpawned;
            var phase = live ? gs.Phase.Value : GamePhase.Idle;
            var def = live ? gs.CurrentDef : null;

            int top = int.MinValue, atTop = 0;
            foreach (var p in players)
            {
                if (p.Score.Value > top) { top = p.Score.Value; atTop = 1; }
                else if (p.Score.Value == top) atTop++;
            }

            _entries.Clear();
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                ulong id = p.OwnerClientId;
                bool isNew = !_cards.TryGetValue(id, out var card) || card == null;
                if (isNew)
                {
                    card = SeatCard.Create(root, "Seat" + id);
                    card.ClientId = id;
                    card.SetScore(p.Score.Value);
                    _cards[id] = card;
                }
                card.Rect.At(i * (SeatCard.Width + gap), 0f, SeatCard.Width, SeatCard.Height);

                string tag = p.IsHostPlayer && p.IsOwner ? "HOST · YOU" : p.IsHostPlayer ? "HOST" : p.IsOwner ? "YOU" : string.Empty;
                card.SetIdentity(p.DisplayName, monos[i], tag, p.IsOwner);
                if (card.ShownScore != p.Score.Value) card.AnimateScore(p.Score.Value, cfg.scoreCountDuration);

                int rank = RankOf(p.Score.Value);
                string rankText = "No." + rank + (rank == 1 && atTop > 1 ? "=" : string.Empty);
                if (phase == GamePhase.Scoring && _rankBefore.TryGetValue(id, out int before) && before != rank)
                {
                    int moved = Mathf.Clamp(before - rank, -3, 3);
                    rankText += " " + new string(moved > 0 ? '↑' : '↓', Mathf.Abs(moved));
                }
                card.SetRank(rankText);
                card.SetLeader(atTop == 1 && p.Score.Value == top && top > 0);

                Decide(card, p, gs, def, phase, top, atTop, cfg);
                _entries.Add(new RaceTrack.Entry { Id = id, Mono = monos[i], Score = p.Score.Value, Local = p.IsOwner });
            }

            if (race != null)
            {
                race.Refresh(_entries, cfg.scoreCountDuration);
                if (phase == GamePhase.Scoring && _scoreBefore.Count > 0)
                {
                    var moves = new List<(int, int)>();
                    foreach (var p in players)
                        if (_scoreBefore.TryGetValue(p.OwnerClientId, out int b)) moves.Add((b, p.Score.Value));
                    race.ShowMoves(moves);
                    _movesShown = true;
                }
                else if (_movesShown && phase != GamePhase.Scoring)
                {
                    race.ClearMoves();
                    _movesShown = false;
                }
            }

            // Leader change (ignoring the very first look, and ties).
            if (players.Count > 0)
            {
                ulong leader = ulong.MaxValue;
                foreach (var p in players) if (p.Score.Value == top) { leader = p.OwnerClientId; break; }
                bool tie = atTop > 1;
                if (_leaderKnown && !tie && leader != _leader) LeaderChanged?.Invoke(leader);
                if (!tie) _leader = leader;
                _leaderKnown = true;
            }
        }

        // ------------------------------------------------------------------

        private static readonly string[] Places = { "", "1ST", "2ND", "3RD" };

        private static string PlaceWord(int place)
        {
            if (place <= 0) return "—";
            return place < Places.Length ? Places[place] : place + "TH";
        }

        /// <summary>What one seat should say in this phase.</summary>
        private static void Decide(SeatCard card, PlayerData p, GameState gs, RoundDefinition def, GamePhase phase,
            int top, int atTop, GameConfig cfg)
        {
            card.SetBigStamp(null);
            card.SetDim(false);

            bool minigame = def != null && def.IsMinigame;
            bool haveResult = gs != null && gs.IsSpawned && gs.TryGetResult(p.OwnerClientId, out _);

            if ((phase == GamePhase.Reveal || phase == GamePhase.Scoring) && haveResult)
            {
                gs.TryGetResult(p.OwnerClientId, out var r);
                Band(card, def, r);
                card.SetStatus(SeatCard.Status.None);
                card.SetDelta(phase == GamePhase.Scoring ? r.Delta : (int?)null, pop: true);
                return;
            }

            card.SetBand(null, Palette.Paper2, Palette.Ink, null);
            card.SetDelta(null, false);

            if (minigame)
            {
                int level = gs != null ? gs.SubRound.Value : 0;
                int outAt = p.OutAtLevel.Value;
                if (phase == GamePhase.Play)
                {
                    if (p.IsPlayingLevel)
                        card.SetStatus(p.LevelDone.Value ? SeatCard.Status.Done : SeatCard.Status.Playing);
                    else if (p.IsStillIn) card.SetStatus(SeatCard.Status.SittingOut);
                    else OutEarlier(card, outAt);
                }
                else if (phase == GamePhase.LevelResult)
                {
                    if (outAt > 0 && outAt == level)
                    {
                        card.SetStatus(SeatCard.Status.None);
                        card.SetBigStamp("OUT");
                        card.SetDim(true);
                    }
                    else if (outAt > 0) OutEarlier(card, outAt);
                    else card.SetStatus(SeatCard.Status.StillIn);
                }
                else card.SetStatus(SeatCard.Status.None);
                return;
            }

            bool tiedAtTop = gs != null && gs.TieBreak.Value && atTop > 1 && p.Score.Value == top && top >= MatchSettings.Target;
            switch (phase)
            {
                case GamePhase.RoundIntro:
                    card.SetStatus(tiedAtTop ? SeatCard.Status.Tied : SeatCard.Status.None,
                        tiedAtTop ? "TIED ON " + p.Score.Value : null);
                    break;
                case GamePhase.Answering:
                    if (p.LockedIn.Value) card.SetStatus(SeatCard.Status.Locked);
                    else if (p.IsOwner) card.SetStatus(SeatCard.Status.YourMove);
                    else card.SetStatus(SeatCard.Status.Thinking);
                    break;
                default:
                    card.SetStatus(SeatCard.Status.None);
                    break;
            }
        }

        private static void OutEarlier(SeatCard card, int outAt)
        {
            card.SetStatus(SeatCard.Status.OutEarlier, outAt > 0 ? "out · level " + outAt : "out");
            card.SetDim(true);
        }

        /// <summary>The reveal band: what they picked (or where they finished), in that answer's colours.</summary>
        private static void Band(SeatCard card, RoundDefinition def, PlayerRoundResult r)
        {
            if (def != null && def.IsMinigame)
            {
                string place = r.Place > 0 ? PlaceWord(r.Place) + " PLACE" : "OUT";
                card.SetBand(place, r.Place == 1 ? Palette.Gold : Palette.Paper2, Palette.Ink, null);
                return;
            }
            if (r.Answer < 0)
            {
                card.SetBand("NO ANSWER", Palette.Paper2, Palette.Ink2, null);
                return;
            }
            var look = AnswerLook.For(def, r.Answer);
            card.SetBand("PICKED " + look.Word, look.Fill, look.Ink, look.Glyph);
        }
    }
}
