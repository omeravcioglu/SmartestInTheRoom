using System;
using System.Collections;
using System.Collections.Generic;
using Smartest.Core;
using Smartest.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// The lobby panel: big join code + Copy, live player list, Start (host) / Leave.
    /// Purely a view over <see cref="PlayerData.All"/> and <see cref="NetSession"/>;
    /// MenuUI decides when this panel is shown.
    /// </summary>
    public class LobbyUI : Panel
    {
        [Header("Lobby")]
        [SerializeField] private TMP_Text codeText;
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private Button copyButton;
        [SerializeField] private RectTransform rowsContainer;
        [SerializeField] private PlayerRowView rowPrefab;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button leaveButton;

        public event Action StartRequested;
        public event Action LeaveRequested;

        private readonly List<PlayerRowView> _rows = new List<PlayerRowView>();
        private TMP_Text _copyLabel;
        private Coroutine _copyFeedback;
        private int _lastCount;

        protected override void Awake()
        {
            base.Awake();
            if (copyButton != null)
            {
                _copyLabel = copyButton.GetComponentInChildren<TMP_Text>();
                copyButton.onClick.AddListener(CopyCode);
            }
            if (startButton != null) startButton.onClick.AddListener(() => StartRequested?.Invoke());
            if (leaveButton != null) leaveButton.onClick.AddListener(() => LeaveRequested?.Invoke());
        }

        private void OnEnable()
        {
            PlayerData.RosterChanged += Refresh;
        }

        private void OnDisable()
        {
            PlayerData.RosterChanged -= Refresh;
        }

        protected override void OnShown()
        {
            base.OnShown();
            Refresh();
        }

        public void Refresh()
        {
            if (!IsShown && CanvasGroup != null && CanvasGroup.alpha <= 0f) return;

            var net = NetSession.Instance;
            bool isHost = net != null && net.IsHost;
            string code = net != null ? net.JoinCode : null;

            if (codeText != null)
            {
                string shown = string.IsNullOrEmpty(code) ? "----" : code;
                codeText.text = shown;
                bool longCode = shown.Length > 8;
                codeText.characterSpacing = longCode ? 2f : 18f;
                codeText.fontSize = longCode ? 36f : 72f;
            }
            if (modeText != null)
            {
                switch (net != null ? net.CurrentMode : NetSession.Mode.None)
                {
                    case NetSession.Mode.Local:
                        modeText.text = "LOCAL — same PC only. A second instance joins with LOCAL.";
                        break;
                    case NetSession.Mode.Lan:
                        modeText.text = "WI-FI — friends on this network type the IP.";
                        break;
                    default:
                        modeText.text = "ONLINE — share this code. Friends can join from anywhere.";
                        break;
                }
            }

            // Rebuild rows (cheap; the list is at most 8 entries).
            foreach (var r in _rows) if (r != null) Destroy(r.gameObject);
            _rows.Clear();

            var players = PlayerData.All;
            if (rowsContainer != null && rowPrefab != null)
            {
                foreach (var p in players)
                {
                    var row = Instantiate(rowPrefab, rowsContainer);
                    row.Bind(p.OwnerClientId);
                    row.SetName(p.DisplayName);
                    row.SetTags(p.IsHostPlayer, p.IsOwner);
                    row.SetHighlight(p.IsOwner);
                    row.SetScore(0, false);
                    row.SetState(string.Empty, Palette.TextDim, false);
                    _rows.Add(row);
                }
            }

            int count = players.Count;
            int max = GameBootstrap.ConfigOrDefault.maxPlayers;

            // Voice: someone (not you) joined; the room is full.
            if (count > _lastCount && _lastCount > 0)
            {
                var newest = players[players.Count - 1];
                if (newest != null && !newest.IsOwner) VoiceLines.Play(VoiceKeys.LobbyPlayerJoined);
                if (count >= max) VoiceLines.Play(VoiceKeys.LobbyFull);
            }
            _lastCount = count;

            if (startButton != null)
            {
                startButton.gameObject.SetActive(isHost);
                startButton.interactable = isHost && count >= 1 && !(net != null && net.IsBusy);
            }
            if (hintText != null)
            {
                hintText.text = isHost
                    ? (count <= 1 ? "You can start solo to test, or wait for friends." : $"{count}/{max} players. Start when everyone's in.")
                    : $"{count}/{max} players. Waiting for host…";
            }
        }

        private void CopyCode()
        {
            var net = NetSession.Instance;
            if (net == null || string.IsNullOrEmpty(net.JoinCode)) return;
            GUIUtility.systemCopyBuffer = net.JoinCode;
            if (_copyLabel != null)
            {
                if (_copyFeedback != null) StopCoroutine(_copyFeedback);
                _copyFeedback = StartCoroutine(CopyFeedback());
            }
        }

        private IEnumerator CopyFeedback()
        {
            string original = "COPY";
            _copyLabel.text = "COPIED";
            yield return new WaitForSecondsRealtime(1.2f);
            _copyLabel.text = original;
            _copyFeedback = null;
        }
    }
}
