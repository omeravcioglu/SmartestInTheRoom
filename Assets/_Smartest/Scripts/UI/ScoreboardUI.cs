using System;
using System.Collections.Generic;
using Smartest.Core;
using Smartest.Net;
using UnityEngine;

namespace Smartest.UI
{
    /// <summary>
    /// Left-column scoreboard in the Game scene. Rows are positioned manually (no layout
    /// group) so they can slide to their new sorted slot; scores count up/down and a
    /// +/− delta flashes beside them. Raises <see cref="LeaderChanged"/> for the voice line.
    /// </summary>
    public class ScoreboardUI : MonoBehaviour
    {
        [SerializeField] private RectTransform rowsContainer;
        [SerializeField] private PlayerRowView rowPrefab;
        [SerializeField] private float rowHeight = 52f;
        [SerializeField] private float rowSpacing = 8f;

        public event Action<ulong> LeaderChanged;

        private readonly Dictionary<ulong, PlayerRowView> _rows = new Dictionary<ulong, PlayerRowView>();
        private readonly Dictionary<ulong, int> _shownScores = new Dictionary<ulong, int>();
        private ulong _leader = ulong.MaxValue;
        private bool _leaderKnown;

        private void OnEnable()
        {
            PlayerData.RosterChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerData.RosterChanged -= Refresh;
        }

        public void Refresh()
        {
            if (rowsContainer == null || rowPrefab == null) return;
            var cfg = GameBootstrap.ConfigOrDefault;

            var players = new List<PlayerData>(PlayerData.All);
            players.Sort((a, b) =>
            {
                int byScore = b.Score.Value.CompareTo(a.Score.Value);
                return byScore != 0 ? byScore : a.OwnerClientId.CompareTo(b.OwnerClientId);
            });

            // Remove rows for players that left.
            var alive = new HashSet<ulong>();
            foreach (var p in players) alive.Add(p.OwnerClientId);
            var dead = new List<ulong>();
            foreach (var kv in _rows) if (!alive.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var id in dead)
            {
                if (_rows[id] != null) Destroy(_rows[id].gameObject);
                _rows.Remove(id);
                _shownScores.Remove(id);
            }

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                bool isNew = !_rows.TryGetValue(p.OwnerClientId, out var row) || row == null;
                if (isNew)
                {
                    row = Instantiate(rowPrefab, rowsContainer);
                    var rt = row.Rect;
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.sizeDelta = new Vector2(0f, rowHeight);
                    rt.anchoredPosition = SlotPosition(i);
                    row.Bind(p.OwnerClientId);
                    _rows[p.OwnerClientId] = row;
                    _shownScores[p.OwnerClientId] = p.Score.Value;
                    row.SetScore(p.Score.Value, true);
                }

                row.SetName(p.DisplayName);
                row.SetTags(p.IsHostPlayer, p.IsOwner);
                row.SetHighlight(p.IsOwner);
                bool locked = p.LockedIn.Value;
                row.SetState(locked ? "locked in" : "thinking", locked ? Palette.LockedIn : Palette.Thinking, true);

                int shown = _shownScores.TryGetValue(p.OwnerClientId, out var s) ? s : p.Score.Value;
                if (shown != p.Score.Value)
                {
                    row.AnimateScore(shown, p.Score.Value, cfg.scoreCountDuration);
                    _shownScores[p.OwnerClientId] = p.Score.Value;
                }

                row.MoveTo(SlotPosition(i), isNew ? 0f : cfg.scoreCountDuration);
            }

            // Leader change detection (ignore the very first evaluation).
            if (players.Count > 0)
            {
                ulong top = players[0].OwnerClientId;
                bool tie = players.Count > 1 && players[1].Score.Value == players[0].Score.Value;
                if (_leaderKnown && !tie && top != _leader) LeaderChanged?.Invoke(top);
                if (!tie) _leader = top;
                _leaderKnown = true;
            }
        }

        private Vector2 SlotPosition(int index) => new Vector2(0f, -index * (rowHeight + rowSpacing));
    }
}
