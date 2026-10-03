using System;
using Smartest.Core;
using Smartest.Minigames;
using Smartest.Rounds;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Smartest.Net
{
    /// <summary>
    /// The host's options as everyone sees them: the lobby shows them, and the match plays to
    /// <see cref="Target"/>. One network object, spawned by the host with the lobby and kept
    /// for the whole session (it rides through Menu → Game → Menu the way the players do). The
    /// host writes it; everyone else only reads.
    /// </summary>
    public class MatchSettings : NetworkBehaviour
    {
        public const string PrefabResourceName = "MatchSettings";

        public static MatchSettings Instance { get; private set; }

        /// <summary>Fired on every machine when the options change, or the object comes or goes.</summary>
        public static event Action Changed;

        public NetworkVariable<int> TargetScore = new NetworkVariable<int>(100);
        public NetworkVariable<int> LengthIndex = new NetworkVariable<int>(HostOptions.StandardLength);
        public NetworkVariable<int> GamesOn = new NetworkVariable<int>(0);

        /// <summary>
        /// The score that wins the match: the lobby's choice, or the config's when there's no
        /// lobby (tests, previews).
        /// </summary>
        public static int Target
        {
            get
            {
                var s = Instance;
                if (s != null && s.IsSpawned && s.TargetScore.Value > 0) return s.TargetScore.Value;
                return Mathf.Max(1, GameBootstrap.ConfigOrDefault.targetScore);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Instance = this;
            if (IsServer)
            {
                // Survive the scene loads; Netcode moves it on the clients too, as with PlayerData.
                DontDestroyOnLoad(gameObject);
                ServerApply(HostOptions.Current);
            }
            TargetScore.OnValueChanged += OnChanged;
            LengthIndex.OnValueChanged += OnChanged;
            GamesOn.OnValueChanged += OnChanged;
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            TargetScore.OnValueChanged -= OnChanged;
            LengthIndex.OnValueChanged -= OnChanged;
            GamesOn.OnValueChanged -= OnChanged;
            if (Instance == this) Instance = null;
            Changed?.Invoke();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        private void OnChanged(int previous, int current) => Changed?.Invoke();

        /// <summary>Host: show these options to the lobby.</summary>
        public void ServerApply(HostOptions options)
        {
            if (!IsServer || options == null) return;
            LengthIndex.Value = options.LengthIndex;
            TargetScore.Value = options.Target;
            GamesOn.Value = options.CountOn(MinigameRegistry.All);
        }

        /// <summary>
        /// Host → everyone, just before they're all taken back to the lobby: the line it shows
        /// them there ("The host ended the match.").
        /// </summary>
        [Rpc(SendTo.Everyone)]
        public void TellEveryoneRpc(FixedString128Bytes note)
        {
            NetSession.PendingMenuMessage = note.ToString();
        }
    }
}
