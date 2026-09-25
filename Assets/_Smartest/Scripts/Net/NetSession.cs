using System;
using System.Threading.Tasks;
using Smartest.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Smartest.Net
{
    /// <summary>
    /// Owns the whole connection lifecycle. Lives on the Bootstrap prefab next to the
    /// Netcode NetworkManager + UnityTransport.
    ///
    /// Online:  Unity Multiplayer Services "Sessions" with a Relay network. Creating or
    ///          joining a session starts the NetworkManager as host/client for us and
    ///          configures the transport. The session's join code is what players share.
    /// Wi-Fi:   Direct connection over the local network. The host binds 0.0.0.0 and shows
    ///          its LAN IP as the join code; friends on the same Wi-Fi type that IP. Needs
    ///          no cloud setup at all, which makes it the automatic fallback.
    /// Local:   127.0.0.1 with the code "LOCAL", for two instances on one PC. A Wi-Fi host
    ///          also accepts "LOCAL" from the same machine, since it binds every interface.
    ///
    /// Everything game-related (names, scores) lives on <see cref="PlayerData"/>; this
    /// class only deals with getting people into the same NetworkManager.
    /// </summary>
    public class NetSession : MonoBehaviour
    {
        public const string LocalCode = "LOCAL";
        public const string MenuSceneName = "Menu";
        public const string GameSceneName = "Game";
        private const string NamePrefKey = "smartest.player_name";

        public enum Mode { None, Relay, Local, Lan }

        public static NetSession Instance { get; private set; }

        /// <summary>Set when a session ends unexpectedly; MenuUI shows and clears it.</summary>
        public static string PendingMenuMessage;

        public Mode CurrentMode { get; private set; } = Mode.None;
        public string JoinCode { get; private set; }
        public bool GameStarted { get; private set; }
        public bool IsBusy { get; private set; }

        public event Action<string> Status;
        public event Action<string> SessionEnded;

        private NetworkManager _nm;
        private UnityTransport _transport;
        private ISession _session;
        private bool _leaving;
        private bool _wasConnected;
        private string _lastDisconnectReason;

        public NetworkManager Nm => _nm != null ? _nm : (_nm = GetComponent<NetworkManager>());
        public bool IsInSession => Nm != null && Nm.IsListening;
        public bool IsHost => Nm != null && Nm.IsHost;
        public bool IsConnected => Nm != null && Nm.IsListening && Nm.IsConnectedClient;
        private GameConfig Config => GameBootstrap.ConfigOrDefault;

        public string LocalPlayerName
        {
            get => PlayerPrefs.GetString(NamePrefKey, string.Empty);
            set
            {
                PlayerPrefs.SetString(NamePrefKey, PlayerData.Sanitize(value));
                PlayerPrefs.Save();
            }
        }

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _nm = GetComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();
            if (_nm == null || _transport == null)
            {
                Debug.LogError("[NetSession] NetworkManager + UnityTransport must be on the same object (run Tools > Smartest > Build Scenes).");
                return;
            }
            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.OnClientDisconnectCallback += OnClientDisconnected;
            _nm.OnClientStopped += OnClientStopped;
            _nm.OnServerStopped += OnServerStopped;
        }

        private void OnDestroy()
        {
            if (_nm != null)
            {
                _nm.OnClientConnectedCallback -= OnClientConnected;
                _nm.OnClientDisconnectCallback -= OnClientDisconnected;
                _nm.OnClientStopped -= OnClientStopped;
                _nm.OnServerStopped -= OnServerStopped;
            }
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        // Host / Join / Leave
        // ------------------------------------------------------------------

        public async Task<bool> HostAsync()
        {
            if (IsBusy) return false;
            IsBusy = true;
            try
            {
                await ResetForNewAttemptAsync();

                switch (Config.networkMode)
                {
                    case NetworkMode.Local: return HostDirect(false);
                    case NetworkMode.Lan: return HostDirect(true);
                }

                Report("Signing in…");
                if (!await EnsureServicesAsync())
                {
                    if (Config.networkMode == NetworkMode.Online)
                    {
                        Report("Can't reach Unity Services. Check the project is linked, or switch to Wi-Fi mode.");
                        CleanupLocalState();
                        return false;
                    }
                    Report("Unity Services unreachable — hosting over Wi-Fi instead.");
                    return HostDirect(true);
                }

                Report("Creating lobby…");
                var options = new SessionOptions
                {
                    MaxPlayers = Mathf.Max(1, Config.maxPlayers),
                    Name = "smartest-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    IsPrivate = true
                }.WithRelayNetwork();

                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                HookSession(_session);
                JoinCode = _session.Code;
                CurrentMode = Mode.Relay;

                if (!await WaitForConnectedAsync())
                {
                    Report("Hosting started but the connection never came up.");
                    await LeaveAsync();
                    return false;
                }
                Report("Lobby ready.");
                return true;
            }
            catch (SessionException se)
            {
                Report(Friendly(se));
                CleanupLocalState();
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NetSession] Host failed: {e}");
                Report("Couldn't create the lobby: " + e.Message);
                CleanupLocalState();
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<bool> JoinAsync(string code)
        {
            if (IsBusy) return false;
            IsBusy = true;
            try
            {
                await ResetForNewAttemptAsync();
                code = (code ?? string.Empty).Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(code))
                {
                    Report("Enter a code first.");
                    return false;
                }

                // "LOCAL" or an IP address means a direct connection — no cloud involved.
                if (code == LocalCode || Config.networkMode == NetworkMode.Local)
                    return await JoinDirectAsync("127.0.0.1", Mode.Local);
                if (LooksLikeIPv4(code))
                    return await JoinDirectAsync(code, Mode.Lan);

                Report("Signing in…");
                if (!await EnsureServicesAsync())
                {
                    Report("Can't reach Unity Services. Type the host's IP to join over Wi-Fi, or LOCAL on this PC.");
                    return false;
                }

                Report("Joining…");
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                HookSession(_session);
                JoinCode = code;
                CurrentMode = Mode.Relay;

                if (!await WaitForConnectedAsync())
                {
                    string why = string.IsNullOrEmpty(_lastDisconnectReason) ? "Couldn't reach the host." : _lastDisconnectReason;
                    Report(why);
                    await LeaveAsync();
                    return false;
                }
                Report("Connected.");
                return true;
            }
            catch (SessionException se)
            {
                Report(Friendly(se));
                CleanupLocalState();
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NetSession] Join failed: {e}");
                Report("Couldn't join: " + e.Message);
                CleanupLocalState();
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Host without Relay. We always listen on 0.0.0.0 so the same machine can connect
        /// via 127.0.0.1 and other machines via the LAN address; only the advertised join
        /// code differs between Wi-Fi and Local mode.
        /// </summary>
        private bool HostDirect(bool lan)
        {
            string advertised = lan ? GetLocalIPv4() : "127.0.0.1";
            _transport.SetConnectionData(advertised, Config.localPort, "0.0.0.0");
            if (!Nm.StartHost())
            {
                Report($"Couldn't start the host on port {Config.localPort} (already in use?).");
                CleanupLocalState();
                return false;
            }
            JoinCode = lan ? advertised : LocalCode;
            CurrentMode = lan ? Mode.Lan : Mode.Local;
            Report(lan ? $"Wi-Fi lobby ready on {advertised}." : "Local lobby ready (same PC only).");
            return true;
        }

        private async Task<bool> JoinDirectAsync(string address, Mode mode)
        {
            _transport.SetConnectionData(address, Config.localPort);
            Report($"Connecting to {address}…");
            if (!Nm.StartClient())
            {
                Report("Couldn't start the client.");
                CleanupLocalState();
                return false;
            }
            JoinCode = mode == Mode.Lan ? address : LocalCode;
            CurrentMode = mode;
            if (!await WaitForConnectedAsync())
            {
                string why = !string.IsNullOrEmpty(_lastDisconnectReason)
                    ? _lastDisconnectReason
                    : (mode == Mode.Lan
                        ? $"No host answering at {address}. Same Wi-Fi? Firewall allowing Unity?"
                        : "No local host found.");
                Report(why);
                await LeaveAsync();
                return false;
            }
            Report("Connected.");
            return true;
        }

        private static bool LooksLikeIPv4(string s)
        {
            return System.Net.IPAddress.TryParse(s, out var addr)
                && addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }

        /// <summary>
        /// This machine's LAN address. Opening a UDP socket toward a public address makes the
        /// OS pick the outbound interface without sending anything; falling back to the host
        /// entry list covers machines with no route out.
        /// </summary>
        private static string GetLocalIPv4()
        {
            try
            {
                using (var probe = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.InterNetwork,
                    System.Net.Sockets.SocketType.Dgram,
                    System.Net.Sockets.ProtocolType.Udp))
                {
                    probe.Connect("8.8.8.8", 65530);
                    if (probe.LocalEndPoint is System.Net.IPEndPoint ep) return ep.Address.ToString();
                }
            }
            catch { /* no route out — fall through */ }

            try
            {
                foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                        && !System.Net.IPAddress.IsLoopback(ip))
                        return ip.ToString();
            }
            catch { /* ignore */ }

            Debug.LogWarning("[NetSession] Couldn't determine a LAN address; falling back to loopback.");
            return "127.0.0.1";
        }

        /// <summary>
        /// Leave (client) or close (host) the current lobby/game and return to a clean state.
        /// Netcode finishes shutting down on a later frame and fires OnClientStopped /
        /// OnServerStopped then, so <c>_leaving</c> stays true until the next Host/Join
        /// attempt — otherwise a voluntary Leave would be reported as "Host left".
        /// </summary>
        public async Task LeaveAsync()
        {
            _leaving = true;
            var s = _session;
            _session = null;
            if (s != null)
            {
                try
                {
                    if (s.IsHost) await s.AsHost().DeleteAsync();
                    else await s.LeaveAsync();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[NetSession] Session leave/delete: {e.Message}");
                }
            }
            if (Nm != null && Nm.IsListening) Nm.Shutdown();

            // Wait for Netcode to actually stop (its stop callbacks fire during this).
            float t = 0f;
            while (Nm != null && Nm.IsListening && t < 3f)
            {
                await Task.Delay(50);
                t += 0.05f;
            }
            await Task.Yield();
            CleanupLocalState();
        }

        // ------------------------------------------------------------------
        // Game flow (host only)
        // ------------------------------------------------------------------

        /// <summary>Host: lock the lobby and load the Game scene for everyone.</summary>
        public async void StartGame()
        {
            if (!IsHost || GameStarted) return;
            GameStarted = true;
            await SetSessionLockedAsync(true);

            // Spawn the round engine once every client has finished loading the Game scene.
            Nm.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            Nm.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;

            var status = Nm.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[NetSession] Could not load Game scene: {status}");
                Nm.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
                GameStarted = false;
                await SetSessionLockedAsync(false);
            }
        }

        private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
        {
            if (sceneName != GameSceneName) return;
            Nm.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            if (clientsTimedOut != null && clientsTimedOut.Count > 0)
                Debug.LogWarning($"[NetSession] {clientsTimedOut.Count} client(s) timed out loading the Game scene.");
            EnsureGameStateSpawned();
        }

        /// <summary>
        /// Host only, idempotent: spawns the GameState network prefab (Resources/GameState) if it
        /// isn't already up. Normally called when the Game scene load completes; GameUI also
        /// calls it as a late fallback.
        /// </summary>
        public void EnsureGameStateSpawned()
        {
            if (Nm == null || !Nm.IsServer || !Nm.IsListening) return;
            var existing = Smartest.Rounds.GameState.Instance;
            if (existing != null && existing.IsSpawned) return;
            if (SceneManager.GetActiveScene().name != GameSceneName) return;

            var prefab = Resources.Load<GameObject>(Smartest.Rounds.GameState.PrefabResourceName);
            if (prefab == null)
            {
                Debug.LogError("[NetSession] Resources/GameState.prefab is missing. Run Tools > Smartest > Build Scenes.");
                return;
            }
            var go = Instantiate(prefab);
            go.name = "GameState";
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError("[NetSession] GameState prefab has no NetworkObject.");
                Destroy(go);
                return;
            }
            netObj.Spawn();
            Debug.Log("[NetSession] GameState spawned.");
        }

        /// <summary>Host: reset scores, unlock the lobby and bring everyone back to the Menu scene.</summary>
        public async void ReturnToLobby()
        {
            if (!IsHost) return;
            GameStarted = false;
            foreach (var p in PlayerData.All) p.ServerResetForNewMatch();
            await SetSessionLockedAsync(false);
            var status = Nm.SceneManager.LoadScene(MenuSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
                Debug.LogError($"[NetSession] Could not load Menu scene: {status}");
        }

        private async Task SetSessionLockedAsync(bool locked)
        {
            if (_session == null || !_session.IsHost) return;
            try
            {
                var host = _session.AsHost();
                host.IsLocked = locked;
                await host.SavePropertiesAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NetSession] Could not set session lock: {e.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Netcode callbacks
        // ------------------------------------------------------------------

        private void OnClientConnected(ulong clientId)
        {
            if (Nm.IsServer && clientId != NetworkManager.ServerClientId)
            {
                // Belt-and-braces: the session service already enforces MaxPlayers and
                // the lock, but local mode has no service, so the host checks too.
                if (Nm.ConnectedClientsIds.Count > Config.maxPlayers)
                {
                    Nm.DisconnectClient(clientId, "Lobby full");
                    return;
                }
                if (GameStarted)
                {
                    Nm.DisconnectClient(clientId, "Game already started");
                    return;
                }
            }
            if (clientId == Nm.LocalClientId) _wasConnected = true;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (Nm.IsServer) return; // a remote player left; PlayerData despawn updates the roster
            // We are a client and this is about us.
            string reason = Nm.DisconnectReason;
            if (!string.IsNullOrWhiteSpace(reason)) _lastDisconnectReason = reason;
        }

        private void OnClientStopped(bool wasHost)
        {
            if (_leaving || wasHost) return;
            string reason = !string.IsNullOrWhiteSpace(_lastDisconnectReason)
                ? _lastDisconnectReason
                : (_wasConnected ? "Host left" : "Couldn't connect");
            HandleSessionEnded(reason);
        }

        private void OnServerStopped(bool wasHost)
        {
            if (_leaving) return;
            HandleSessionEnded("Hosting stopped");
        }

        private void HookSession(ISession session)
        {
            if (session == null) return;
            session.RemovedFromSession += () => { if (!_leaving) _lastDisconnectReason = "Removed from lobby"; };
            session.Deleted += () => { if (!_leaving) _lastDisconnectReason = "Host left"; };
        }

        private void HandleSessionEnded(string reason)
        {
            var s = _session;
            _session = null;
            if (s != null)
            {
                // Best effort; the service will time the membership out anyway.
                try { _ = s.LeaveAsync(); } catch { /* ignore */ }
            }
            CleanupLocalState();
            PendingMenuMessage = reason;
            SessionEnded?.Invoke(reason);
            if (SceneManager.GetActiveScene().name != MenuSceneName)
                SceneManager.LoadScene(MenuSceneName);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private async Task ResetForNewAttemptAsync()
        {
            if (Nm != null && Nm.IsListening)
                await LeaveAsync(); // waits for the shutdown callbacks to finish
            _leaving = false;
            _wasConnected = false;
            _lastDisconnectReason = null;
            GameStarted = false;
        }

        private void CleanupLocalState()
        {
            JoinCode = null;
            CurrentMode = Mode.None;
            GameStarted = false;
            _wasConnected = false;
        }

        private async Task<bool> EnsureServicesAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NetSession] Unity Services unavailable: {e.Message}");
                return false;
            }
        }

        private async Task<bool> WaitForConnectedAsync()
        {
            float timeout = Mathf.Max(3f, Config.connectTimeoutSeconds);
            float t = 0f;
            while (t < timeout)
            {
                if (Nm == null || !Nm.IsListening) return false;
                if (Nm.IsConnectedClient) return true;
                if (!string.IsNullOrEmpty(_lastDisconnectReason)) return false;
                await Task.Delay(50);
                t += 0.05f;
            }
            return false;
        }

        private static string Friendly(SessionException se)
        {
            string msg = se.Message ?? string.Empty;
            string lower = msg.ToLowerInvariant();
            switch (se.Error)
            {
                case SessionError.SessionNotFound:
                    return "No lobby with that code.";
                case SessionError.NetworkManagerNotInitialized:
                case SessionError.TransportComponentMissing:
                    return "Network setup problem: run Tools > Smartest > Build Scenes.";
                case SessionError.NotAuthorized:
                    return "Not signed in to Unity Services.";
            }
            if (lower.Contains("full")) return "Lobby full.";
            if (lower.Contains("lock")) return "That game already started.";
            if (lower.Contains("not found") || lower.Contains("invalid")) return "No lobby with that code.";
            return "Couldn't join: " + msg;
        }

        private void Report(string message)
        {
            Debug.Log("[NetSession] " + message);
            Status?.Invoke(message);
        }
    }
}
