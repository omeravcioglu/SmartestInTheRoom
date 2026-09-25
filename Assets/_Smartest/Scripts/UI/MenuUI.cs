using Smartest.Core;
using Smartest.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// Drives the Menu scene: main menu -> (host) lobby, or -> join code -> lobby.
    /// All references are wired by Tools > Smartest > Build Scenes.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private Panel mainPanel;
        [SerializeField] private Panel joinPanel;
        [SerializeField] private LobbyUI lobby;

        [Header("Main menu")]
        [SerializeField] private TMP_InputField nameField;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_Text mainStatus;

        [Header("Join panel")]
        [SerializeField] private TMP_InputField codeField;
        [SerializeField] private Button joinGoButton;
        [SerializeField] private Button joinBackButton;
        [SerializeField] private TMP_Text joinStatus;

        private Panel _current;
        private bool _busy;

        private NetSession Net => NetSession.Instance;

        private void Start()
        {
            // Name field
            if (nameField != null)
            {
                nameField.characterLimit = PlayerData.MaxNameLength;
                nameField.text = Net != null ? Net.LocalPlayerName : string.Empty;
                nameField.onValueChanged.AddListener(OnNameChanged);
                nameField.onSubmit.AddListener(_ => OnNameSubmitted());
            }
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

            // Join panel
            if (codeField != null)
            {
                codeField.characterLimit = 24; // long enough for an IPv4 address
                codeField.onValueChanged.AddListener(OnCodeChanged);
                codeField.onSubmit.AddListener(_ => OnJoinGoClicked());
            }
            if (joinGoButton != null) joinGoButton.onClick.AddListener(OnJoinGoClicked);
            if (joinBackButton != null) joinBackButton.onClick.AddListener(() => GoTo(mainPanel));

            // Lobby
            if (lobby != null)
            {
                lobby.StartRequested += OnStartClicked;
                lobby.LeaveRequested += OnLeaveClicked;
            }

            if (Net != null)
            {
                Net.Status += OnNetStatus;
                Net.SessionEnded += OnSessionEnded;
            }

            // Initial state: back from a game while still connected -> straight to the lobby.
            if (Net != null && Net.IsConnected)
            {
                if (mainPanel != null) mainPanel.HideInstant();
                if (joinPanel != null) joinPanel.HideInstant();
                if (lobby != null) lobby.ShowInstant();
                _current = lobby;
            }
            else
            {
                if (mainPanel != null) mainPanel.ShowInstant();
                if (joinPanel != null) joinPanel.HideInstant();
                if (lobby != null) lobby.HideInstant();
                _current = mainPanel;
            }

            if (!string.IsNullOrEmpty(NetSession.PendingMenuMessage))
            {
                SetStatus(NetSession.PendingMenuMessage);
                NetSession.PendingMenuMessage = null;
            }
            else
            {
                SetStatus(string.Empty);
            }

            RefreshMainButtons();
            VoiceLines.Play(VoiceKeys.MenuWelcome);
        }

        private void OnDestroy()
        {
            if (Net != null)
            {
                Net.Status -= OnNetStatus;
                Net.SessionEnded -= OnSessionEnded;
            }
            if (lobby != null)
            {
                lobby.StartRequested -= OnStartClicked;
                lobby.LeaveRequested -= OnLeaveClicked;
            }
        }

        private void Update()
        {
            if (_busy) return;
            if (KeyInput.EscapePressed() && _current == joinPanel)
                GoTo(mainPanel);
        }

        // ------------------------------------------------------------------
        // Main menu
        // ------------------------------------------------------------------

        private void OnNameChanged(string value)
        {
            if (Net != null) Net.LocalPlayerName = value;
            RefreshMainButtons();
        }

        private void OnNameSubmitted()
        {
            // Enter in the name field just confirms it; keep focus behaviour simple.
            if (nameField != null) nameField.DeactivateInputField();
        }

        private bool HasValidName => !string.IsNullOrEmpty(PlayerData.Sanitize(nameField != null ? nameField.text : string.Empty));

        private void RefreshMainButtons()
        {
            bool ok = HasValidName && !_busy;
            if (hostButton != null) hostButton.interactable = ok;
            if (joinButton != null) joinButton.interactable = ok;
            if (joinGoButton != null) joinGoButton.interactable = !_busy;
            if (joinBackButton != null) joinBackButton.interactable = !_busy;
        }

        private async void OnHostClicked()
        {
            if (_busy || !HasValidName || Net == null) return;
            VoiceLines.Play(VoiceKeys.MenuHostPressed);
            SetBusy(true);
            SetStatus("Creating lobby…");
            bool ok = await Net.HostAsync();
            if (!this) return; // scene changed while we were waiting
            SetBusy(false);
            if (ok)
            {
                SetStatus(string.Empty);
                GoTo(lobby);
            }
        }

        private void OnJoinClicked()
        {
            if (_busy || !HasValidName) return;
            VoiceLines.Play(VoiceKeys.MenuJoinPressed);
            SetStatus(string.Empty);
            GoTo(joinPanel);
            if (codeField != null)
            {
                codeField.text = string.Empty;
                codeField.ActivateInputField();
            }
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------
        // Join panel
        // ------------------------------------------------------------------

        private void OnCodeChanged(string value)
        {
            string upper = (value ?? string.Empty).ToUpperInvariant();
            if (upper != value && codeField != null) codeField.SetTextWithoutNotify(upper);
        }

        private async void OnJoinGoClicked()
        {
            if (_busy || Net == null) return;
            string code = codeField != null ? codeField.text : string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                SetStatus("Enter the code from the host.");
                return;
            }
            SetBusy(true);
            SetStatus("Joining…");
            bool ok = await Net.JoinAsync(code);
            if (!this) return; // scene changed while we were waiting
            SetBusy(false);
            if (ok)
            {
                SetStatus(string.Empty);
                GoTo(lobby);
            }
        }

        // ------------------------------------------------------------------
        // Lobby
        // ------------------------------------------------------------------

        private void OnStartClicked()
        {
            if (Net == null || !Net.IsHost) return;
            if (PlayerData.All.Count == 1) VoiceLines.Play(VoiceKeys.LobbySoloStart);
            Net.StartGame();
        }

        private async void OnLeaveClicked()
        {
            if (_busy || Net == null) return;
            SetBusy(true);
            await Net.LeaveAsync();
            if (!this) return;
            SetBusy(false);
            SetStatus(string.Empty);
            GoTo(mainPanel);
        }

        private void OnSessionEnded(string reason)
        {
            SetBusy(false);
            GoTo(mainPanel);
            SetStatus(reason);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private void GoTo(Panel target)
        {
            if (target == null) return;
            if (mainPanel != null && mainPanel != target && mainPanel.IsShown) mainPanel.Hide();
            if (joinPanel != null && joinPanel != target && joinPanel.IsShown) joinPanel.Hide();
            if (lobby != null && lobby != target && lobby.IsShown) lobby.Hide();
            if (!target.IsShown) target.Show();
            _current = target;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            RefreshMainButtons();
            if (lobby != null) lobby.Refresh();
        }

        private void OnNetStatus(string message) => SetStatus(message);

        private void SetStatus(string message)
        {
            // Show the message on whichever panel is in front.
            if (_current == joinPanel)
            {
                if (joinStatus != null) joinStatus.text = message;
            }
            else
            {
                if (mainStatus != null) mainStatus.text = message;
                if (joinStatus != null && _current != joinPanel) joinStatus.text = string.Empty;
            }
        }
    }
}
