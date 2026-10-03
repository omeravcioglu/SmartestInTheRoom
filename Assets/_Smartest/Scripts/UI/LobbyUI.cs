using System;
using System.Collections;
using System.Collections.Generic;
using Smartest.Core;
using Smartest.Minigames;
using Smartest.Net;
using Smartest.Rounds;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// The lobby: the join code big enough to read out to friends, the match the host has set
    /// up (how long, how many minigames), and the seats — taken ones carry a token and a name,
    /// open ones wait with a dashed outline. The host starts, sets the match up and can kick;
    /// everyone can leave. A view over <see cref="PlayerData.All"/>, <see cref="MatchSettings"/>
    /// and <see cref="NetSession"/>; MenuUI decides when it's shown.
    /// </summary>
    public class LobbyUI : Panel
    {
        [Header("Lobby")]
        [SerializeField] private TMP_Text codeText;
        [SerializeField] private Button copyButton;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private LobbySeat[] seats = new LobbySeat[0];
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button leaveButton;

        [Header("The match")]
        [SerializeField] private RectTransform optionsRoot;
        [SerializeField] private Button[] lengthButtons = new Button[0];
        [SerializeField] private TMP_Text gamesText;
        [SerializeField] private Button chooseButton;
        [SerializeField] private MinigamePicker picker;

        public event Action StartRequested;
        public event Action LeaveRequested;

        private TMP_Text _copyLabel;
        private Coroutine _copyFeedback;
        private int _lastCount;
        private string _note;
        private int _noteCount;
        private readonly Dictionary<ulong, float> _joinedAt = new Dictionary<ulong, float>();

        private const float JustJoinedSeconds = 6f;

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
            for (int i = 0; i < lengthButtons.Length; i++)
            {
                int length = i;
                if (lengthButtons[i] != null) lengthButtons[i].onClick.AddListener(() => OnLength(length));
            }
            if (chooseButton != null) chooseButton.onClick.AddListener(OnChoose);
            foreach (var seat in seats)
                if (seat != null) seat.KickConfirmed += OnKick;
        }

        private void OnEnable()
        {
            PlayerData.RosterChanged += Refresh;
            MatchSettings.Changed += Refresh;
        }

        private void OnDisable()
        {
            PlayerData.RosterChanged -= Refresh;
            MatchSettings.Changed -= Refresh;
        }

        // ---- The match: the host's options ----

        private void OnLength(int index)
        {
            var net = NetSession.Instance;
            if (net == null || !net.IsHost) return;
            var options = HostOptions.Current;
            options.SetLength(index);
            options.Save();
            if (MatchSettings.Instance != null) MatchSettings.Instance.ServerApply(options);
            Sounds.Play(Sounds.Kind.Click);
        }

        private void OnChoose()
        {
            var net = NetSession.Instance;
            if (net == null || !net.IsHost || picker == null) return;
            picker.Open();
        }

        private void OnKick(ulong clientId)
        {
            var net = NetSession.Instance;
            if (net == null || !net.IsHost) return;
            net.Kick(clientId);
            Sounds.Play(Sounds.Kind.Click);
        }

        /// <summary>What the host set, for everyone; only the host can change it.</summary>
        private void RefreshOptions(bool isHost)
        {
            var settings = MatchSettings.Instance;
            bool known = settings != null && settings.IsSpawned;
            if (optionsRoot != null) optionsRoot.gameObject.SetActive(known);
            if (!known) return;
            int length = settings.LengthIndex.Value;
            for (int i = 0; i < lengthButtons.Length; i++)
            {
                var button = lengthButtons[i];
                if (button == null) continue;
                button.interactable = isHost;
                if (button.targetGraphic is Image face) face.color = i == length ? Palette.Gold : Palette.PaperHi;
            }
            if (gamesText != null) gamesText.text = $"{settings.GamesOn.Value} OF {MinigameRegistry.All.Count}";
            if (chooseButton != null) chooseButton.gameObject.SetActive(isHost);
        }

        protected override void OnShown()
        {
            base.OnShown();
            Refresh();
        }

        private float _nextNoteCheck;

        private void Update()
        {
            // "just joined" wears off on its own.
            if (Time.unscaledTime < _nextNoteCheck) return;
            _nextNoteCheck = Time.unscaledTime + 0.5f;
            foreach (var kv in _joinedAt)
            {
                float age = Time.unscaledTime - kv.Value;
                if (age >= JustJoinedSeconds && age < JustJoinedSeconds + 0.6f) { Refresh(); break; }
            }
        }

        public void Refresh()
        {
            if (!IsShown && CanvasGroup != null && CanvasGroup.alpha <= 0f) return;

            var net = NetSession.Instance;
            bool isHost = net != null && net.IsHost;
            string code = net != null ? net.JoinCode : null;
            int max = GameBootstrap.ConfigOrDefault.maxPlayers;
            var players = PlayerData.All;
            int count = players.Count;

            if (codeText != null) codeText.text = string.IsNullOrEmpty(code) ? "------" : code;
            if (countText != null) countText.text = $"{count} / {max}";
            RefreshOptions(isHost);

            // Track arrivals for the "just joined" note.
            var present = new HashSet<ulong>();
            foreach (var p in players)
            {
                present.Add(p.OwnerClientId);
                if (!_joinedAt.ContainsKey(p.OwnerClientId))
                    _joinedAt[p.OwnerClientId] = _lastCount == 0 ? -999f : Time.unscaledTime;
            }
            var gone = new List<ulong>();
            foreach (var kv in _joinedAt) if (!present.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone) _joinedAt.Remove(id);

            var names = new List<string>();
            foreach (var p in players) names.Add(p.DisplayName);
            var monos = Monogram.ForAll(names);
            for (int i = 0; i < seats.Length; i++)
            {
                var seat = seats[i];
                if (seat == null) continue;
                seat.gameObject.SetActive(i < max);
                if (i < count)
                {
                    var p = players[i];
                    string tag = p.IsHostPlayer && p.IsOwner ? "HOST · YOU" : p.IsHostPlayer ? "HOST" : p.IsOwner ? "YOU" : string.Empty;
                    bool fresh = _joinedAt.TryGetValue(p.OwnerClientId, out float at) && Time.unscaledTime - at < JustJoinedSeconds;
                    // The host can send anyone else away, but not once a match is starting.
                    bool kickable = isHost && !p.IsOwner && !(net != null && net.GameStarted);
                    seat.SetTaken(i + 1, p.OwnerClientId, p.DisplayName, monos[i], tag, p.IsOwner,
                        fresh ? "just joined" : string.Empty, kickable);
                }
                else seat.SetOpen(i + 1);
            }

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
                // The host has the Start button; everyone else is told who they're waiting on.
                if (_note != null && count != _noteCount) _note = null;
                hintText.text = _note ?? (isHost ? string.Empty : "Waiting for the host…");
            }
        }

        /// <summary>
        /// One line in place of the usual hint, e.g. why the match just ended. It stays until
        /// someone joins or leaves.
        /// </summary>
        public void ShowNote(string note)
        {
            _note = string.IsNullOrEmpty(note) ? null : note;
            _noteCount = PlayerData.All.Count;
            Refresh();
        }

        private void CopyCode()
        {
            var net = NetSession.Instance;
            if (net == null || string.IsNullOrEmpty(net.JoinCode)) return;
            GUIUtility.systemCopyBuffer = net.JoinCode;
            Sounds.Play(Sounds.Kind.Click);
            if (_copyLabel != null)
            {
                if (_copyFeedback != null) StopCoroutine(_copyFeedback);
                _copyFeedback = StartCoroutine(CopyFeedback());
            }
        }

        private IEnumerator CopyFeedback()
        {
            _copyLabel.text = "COPIED";
            yield return new WaitForSecondsRealtime(1.2f);
            _copyLabel.text = "COPY";
            _copyFeedback = null;
        }

        // ------------------------------------------------------------------
        // Construction (SceneBuilder)
        // ------------------------------------------------------------------

        public static LobbyUI Create(Transform frame, GameConfig config, SettingsPanel settings, MinigamePicker picker)
        {
            var root = Ink.Node(frame, "LobbyPanel");
            root.Fill();
            var lobby = root.gameObject.AddComponent<LobbyUI>();
            lobby.ApplyConfig(config);
            lobby.SetMotion(1f, Vector2.zero);

            // Header.
            var name = Ink.Chip(root, "Lobby", "Lobby", TypeRole.Display, 46f, Palette.Ink, Palette.Paper, 0f,
                new RectOffset(14, 14, 4, 2), caps: true);
            Ink.BoxOf(name).Pin(48f, 29f, new Vector2(0f, 1f));
            var title = Ink.Headline(root, "Title", "Smartest in the Room", 70f, 4f, 40f).OneLine();
            title.alignment = TextAlignmentOptions.Midline;
            title.rectTransform.At(420f, 10f, 1080f, 110f);
            SoundButton.Create(root, settings, 1872f - 56f, 37f, 56f);
            Ink.Plain(root, "Rule1", Palette.Ink).rectTransform.At(48f, 124f, 1824f, 6f);
            Ink.Plain(root, "Rule2", Palette.Ink).rectTransform.At(48f, 134f, 1824f, 2f);

            // Join code.
            Ink.Label(root, "CodeLabel", "JOIN CODE", 15f).rectTransform.At(48f, 170f, 400f, 20f);
            var codeBox = Ink.Dashed(root, "CodeBox", Palette.PaperHi, 4f);
            codeBox.rectTransform.At(48f, 208f, 680f, 190f);
            lobby.codeText = Ink.Text(codeBox.transform, "Code", "------", TypeRole.Mono, 124f, Palette.Ink,
                TextAlignmentOptions.Center, tracking: 0.08f, lineHeight: 1f).OneLine();
            lobby.codeText.rectTransform.Fill(24f, 0f, 24f, 0f);
            lobby.codeText.Fit(36f);

            var copy = Ink.Slab(root, "Copy", "Copy", Palette.PaperHi, Palette.Ink, 56f, 4f, 8f);
            copy.GetComponent<RectTransform>().At(48f, 424f, 220f, 84f);
            lobby.copyButton = copy;

            // The match: how long, and which games. The host sets it up; everyone sees it.
            var options = Ink.Node(root, "Match");
            options.At(48f, 552f, 680f, 260f);
            lobby.optionsRoot = options;
            Ink.Label(options, "LengthLabel", "MATCH LENGTH", 15f).rectTransform.At(0f, 0f, 400f, 20f);
            var lengths = HostOptions.Lengths;
            lobby.lengthButtons = new Button[lengths.Length];
            for (int i = 0; i < lengths.Length; i++)
            {
                var choice = Ink.Slab(options, "Length" + i, lengths[i].Name, Palette.PaperHi, Palette.Ink, 34f, 3f, 6f);
                choice.GetComponent<RectTransform>().At(i * 234f, 32f, 212f, 88f);
                // A guest can't change it, but has to read it: no fading the host's choice.
                choice.GetComponent<SlabPress>().DimWhenDisabled = false;
                var word = choice.transform.Find("Label").GetComponent<TMP_Text>();
                word.rectTransform.Fill(10f, 10f, 10f, 36f);
                var target = Ink.Label(choice.transform, "Target", $"FIRST TO {lengths[i].Target}", 12f,
                    Palette.Ink, TextAlignmentOptions.Center);
                target.rectTransform.Fill(10f, 54f, 10f, 12f);
                lobby.lengthButtons[i] = choice;
            }
            Ink.Label(options, "GamesLabel", "MINIGAMES", 15f).rectTransform.At(0f, 152f, 400f, 20f);
            lobby.gamesText = Ink.Text(options, "Games", "69 OF 69", TypeRole.Display, 48f, Palette.Ink,
                TextAlignmentOptions.MidlineLeft, lineHeight: 1f).OneLine();
            lobby.gamesText.rectTransform.At(0f, 180f, 420f, 72f);
            lobby.chooseButton = Ink.Slab(options, "Choose", "Choose", Palette.PaperHi, Palette.Ink, 36f, 3f, 6f);
            lobby.chooseButton.GetComponent<RectTransform>().At(468f, 184f, 212f, 68f);
            lobby.picker = picker;

            // Seats.
            Ink.Label(root, "PlayersLabel", "THE PLAYERS", 15f).rectTransform.At(800f, 176f, 400f, 20f);
            lobby.countText = Ink.Text(root, "Count", "1 / 8", TypeRole.Display, 56f, Palette.Ink,
                TextAlignmentOptions.BottomRight, lineHeight: 1f).OneLine();
            lobby.countText.rectTransform.At(1864f - 300f, 150f, 300f, 56f);
            int max = Mathf.Max(1, config != null ? config.maxPlayers : 8);
            lobby.seats = new LobbySeat[max];
            const float seatW = 248f, seatH = 200f, gap = 24f;
            for (int i = 0; i < max; i++)
            {
                int col = i % 4, row = i / 4;
                lobby.seats[i] = LobbySeat.Create(root, "Seat" + (i + 1), 800f + col * (seatW + gap), 222f + row * (seatH + gap), seatW, seatH);
            }

            // Footer.
            lobby.hintText = Ink.Text(root, "Hint", "", TypeRole.Body, 24f, Palette.Ink, TextAlignmentOptions.MidlineLeft,
                lineHeight: 1.35f);
            lobby.hintText.fontStyle |= FontStyles.Bold;
            lobby.hintText.rectTransform.At(800f, 716f, 300f, 124f);
            lobby.startButton = Ink.Slab(root, "Start", "Start", Palette.Gold, Palette.Ink, 116f);
            lobby.startButton.GetComponent<RectTransform>().At(1864f - 220f - 24f - 500f, 716f, 500f, 124f);
            lobby.leaveButton = Ink.Slab(root, "Leave", "Leave", Palette.PaperHi, Palette.Ink, 64f);
            lobby.leaveButton.GetComponent<RectTransform>().At(1864f - 220f, 716f, 220f, 124f);
            return lobby;
        }
    }
}
