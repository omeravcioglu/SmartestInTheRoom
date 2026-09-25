using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Smartest.Net
{
    /// <summary>
    /// One instance per connected player, spawned automatically by Netcode as the
    /// player object (NetworkManager.PlayerPrefab). The host has authority over every
    /// field; clients only ever send RPCs. The object is moved to DontDestroyOnLoad on
    /// the server so it survives the Menu -> Game scene load (Netcode syncs that move).
    /// </summary>
    public class PlayerData : NetworkBehaviour
    {
        public const int MaxNameLength = 12;
        private const int MaxNameBytes = 28; // FixedString32Bytes holds 29 UTF-8 bytes

        private static readonly List<PlayerData> s_all = new List<PlayerData>();

        /// <summary>All spawned players, sorted by client id (host first).</summary>
        public static IReadOnlyList<PlayerData> All => s_all;

        /// <summary>Fired on every client whenever a player joins/leaves or a displayed value changes.</summary>
        public static event Action RosterChanged;

        // ---- Networked state (server-authoritative) ----
        public NetworkVariable<FixedString32Bytes> PlayerName =
            new NetworkVariable<FixedString32Bytes>(new FixedString32Bytes(string.Empty));
        public NetworkVariable<int> Score = new NetworkVariable<int>(0);
        public NetworkVariable<bool> LockedIn = new NetworkVariable<bool>(false);
        public NetworkVariable<int> CurrentAnswer = new NetworkVariable<int>(-1);
        /// <summary>Minigames: 0 = knocked out, 1 = playing this level, 2 = still in but sitting a tie-break out.</summary>
        public NetworkVariable<byte> PlayState = new NetworkVariable<byte>(PlayStateOut);

        public const byte PlayStateOut = 0;
        public const byte PlayStatePlaying = 1;
        public const byte PlayStateWatching = 2;

        public bool IsPlayingLevel => PlayState.Value == PlayStatePlaying;
        public bool IsStillIn => PlayState.Value != PlayStateOut;

        public string DisplayName
        {
            get
            {
                string n = PlayerName.Value.ToString();
                return string.IsNullOrEmpty(n) ? $"Player {OwnerClientId + 1}" : n;
            }
        }

        public bool IsHostPlayer => OwnerClientId == NetworkManager.ServerClientId;

        public static PlayerData Local
        {
            get
            {
                for (int i = 0; i < s_all.Count; i++) if (s_all[i].IsOwner) return s_all[i];
                return null;
            }
        }

        public static PlayerData Get(ulong clientId)
        {
            for (int i = 0; i < s_all.Count; i++) if (s_all[i].OwnerClientId == clientId) return s_all[i];
            return null;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                // Keep player objects alive across scene loads. Netcode synchronizes this
                // migration to clients (NetworkObject.SceneMigrationSynchronization).
                DontDestroyOnLoad(gameObject);
            }

            if (!s_all.Contains(this)) s_all.Add(this);
            s_all.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

            PlayerName.OnValueChanged += OnNameChanged;
            Score.OnValueChanged += OnScoreChanged;
            LockedIn.OnValueChanged += OnLockChanged;

            if (IsOwner)
            {
                string local = NetSession.Instance != null ? NetSession.Instance.LocalPlayerName : string.Empty;
                SetNameRpc(new FixedString32Bytes(Sanitize(local)));
            }

            RosterChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            PlayerName.OnValueChanged -= OnNameChanged;
            Score.OnValueChanged -= OnScoreChanged;
            LockedIn.OnValueChanged -= OnLockChanged;
            s_all.Remove(this);
            RosterChanged?.Invoke();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            if (s_all.Remove(this)) RosterChanged?.Invoke();
            base.OnDestroy();
        }

        private void OnNameChanged(FixedString32Bytes previous, FixedString32Bytes current) => RosterChanged?.Invoke();
        private void OnScoreChanged(int previous, int current) => RosterChanged?.Invoke();
        private void OnLockChanged(bool previous, bool current) => RosterChanged?.Invoke();

        // ---- RPCs (client -> server) ----

        [Rpc(SendTo.Server)]
        private void SetNameRpc(FixedString32Bytes name)
        {
            PlayerName.Value = new FixedString32Bytes(Sanitize(name.ToString()));
        }

        /// <summary>Client → host. The host validates range/phase and ignores changes after lock-in.</summary>
        [Rpc(SendTo.Server)]
        public void SubmitAnswerRpc(int answer)
        {
            if (Smartest.Rounds.GameState.Instance != null)
                Smartest.Rounds.GameState.Instance.ServerOnAnswer(this, answer);
        }

        /// <summary>
        /// Client → host, once per minigame level. Timings are measured on the player's own
        /// machine so ping never costs anyone a reaction; the host only collects and ranks.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitLevelResultRpc(int level, bool failed, int metric)
        {
            if (Smartest.Rounds.GameState.Instance != null)
                Smartest.Rounds.GameState.Instance.ServerOnLevelResult(this, level, failed, metric);
        }

        /// <summary>Server-only helper used by the round engine (Phase 3).</summary>
        public void ServerResetForNewMatch()
        {
            if (!IsServer) return;
            Score.Value = 0;
            LockedIn.Value = false;
            CurrentAnswer.Value = -1;
            PlayState.Value = PlayStateOut;
        }

        /// <summary>
        /// Trims, strips control characters and surrogates (emoji), and clamps to
        /// <see cref="MaxNameLength"/> characters and <see cref="MaxNameBytes"/> UTF-8 bytes
        /// so the value always fits a FixedString32Bytes.
        /// </summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var sb = new StringBuilder();
            int bytes = 0;
            foreach (char c in raw.Trim())
            {
                if (char.IsControl(c) || char.IsSurrogate(c)) continue;
                int b = Encoding.UTF8.GetByteCount(c.ToString());
                if (sb.Length >= MaxNameLength || bytes + b > MaxNameBytes) break;
                sb.Append(c);
                bytes += b;
            }
            return sb.ToString().Trim();
        }
    }
}
