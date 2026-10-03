using System;
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
    /// The host's minigame list: every game as a ticket, gold while it's in the deck and struck
    /// through on paper once it's out. Each click shows in the lobby straight away, and the
    /// choice is saved on this machine when the list closes. The last
    /// <see cref="HostOptions.MinGamesOn"/> games can't be taken out.
    /// </summary>
    public class MinigamePicker : Panel
    {
        private const int Columns = 7;
        private const float CellH = 44f;
        private const float Gap = 10f;
        private const string Note = "Click a game to take it out of the deck, or to put it back.";

        [SerializeField] private RectTransform grid;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text noteText;
        [SerializeField] private Button allOnButton;
        [SerializeField] private Button doneButton;

        private sealed class Ticket
        {
            public MinigameEntry Entry;
            public Image Box;
            public TMP_Text Title;
        }

        private readonly List<Ticket> _tickets = new List<Ticket>();
        private float _noteUntil;

        protected override void Awake()
        {
            base.Awake();
            if (allOnButton != null) allOnButton.onClick.AddListener(OnAllOn);
            if (doneButton != null) doneButton.onClick.AddListener(Close);
        }

        private void Update()
        {
            if (!IsShown) return;
            if (KeyInput.EscapePressed())
            {
                Close();
                return;
            }
            if (_noteUntil > 0f && Time.unscaledTime > _noteUntil)
            {
                _noteUntil = 0f;
                SetNote(Note, Palette.Ink2);
            }
        }

        public void Open()
        {
            BuildTickets();
            Refresh();
            Sounds.Play(Sounds.Kind.Click);
            Show();
        }

        public void Close()
        {
            if (!IsShown) return;
            HostOptions.Current.Save();
            Sounds.Play(Sounds.Kind.Click);
            Hide();
        }

        /// <summary>One ticket per registered game, A to Z, made the first time the list opens.</summary>
        private void BuildTickets()
        {
            if (_tickets.Count > 0 || grid == null) return;
            var games = new List<MinigameEntry>(MinigameRegistry.All);
            games.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
            float width = grid.rect.width > 1f ? grid.rect.width : grid.sizeDelta.x;
            float cellW = (width - Gap * (Columns - 1)) / Columns;
            for (int i = 0; i < games.Count; i++)
            {
                var entry = games[i];
                int col = i % Columns, row = i / Columns;
                var box = Ink.Box(grid, "Game_" + entry.Id, Palette.Gold, 3f, 0f, raycast: true);
                box.rectTransform.At(col * (cellW + Gap), row * (CellH + Gap), cellW, CellH);
                // A button made at runtime tints itself the moment it's enabled (the list isn't
                // clickable yet, so "disabled": grey, half see-through), and changing its
                // transition afterwards doesn't undo it. Set it up switched off.
                box.gameObject.SetActive(false);
                var button = box.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = box;
                box.gameObject.SetActive(true);
                var title = Ink.Text(box.transform, "Title", entry.Title, TypeRole.Sticker, 19f, Palette.Ink,
                    TextAlignmentOptions.Center, caps: true).OneLine();
                title.rectTransform.Fill(8f, 2f, 8f, 2f);
                title.Fit(12f);
                var ticket = new Ticket { Entry = entry, Box = box, Title = title };
                button.onClick.AddListener(() => Toggle(ticket));
                _tickets.Add(ticket);
            }
        }

        private void Toggle(Ticket ticket)
        {
            var options = HostOptions.Current;
            if (!options.SetOn(ticket.Entry.Id, !options.IsOn(ticket.Entry.Id), MinigameRegistry.All))
            {
                // Refused: it's one of the last few.
                SetNote($"Keep at least {HostOptions.MinGamesOn} games in the deck.", Palette.RedText);
                _noteUntil = Time.unscaledTime + 2.5f;
                Sounds.Play(Sounds.Kind.Bad);
                return;
            }
            Sounds.Play(Sounds.Kind.Click);
            Apply();
        }

        private void OnAllOn()
        {
            HostOptions.Current.AllOn();
            Sounds.Play(Sounds.Kind.Click);
            Apply();
        }

        /// <summary>Show the change here and in the lobby.</summary>
        private void Apply()
        {
            if (MatchSettings.Instance != null) MatchSettings.Instance.ServerApply(HostOptions.Current);
            Refresh();
        }

        private void Refresh()
        {
            var options = HostOptions.Current;
            foreach (var t in _tickets)
            {
                bool on = options.IsOn(t.Entry.Id);
                t.Box.color = on ? Palette.Gold : Palette.Paper;
                t.Title.text = on ? t.Entry.Title : "<s>" + t.Entry.Title + "</s>";
                t.Title.color = on ? Palette.Ink : Palette.Ink2;
            }
            if (countText != null)
                countText.text = $"{options.CountOn(MinigameRegistry.All)} OF {MinigameRegistry.All.Count} IN THE DECK";
        }

        private void SetNote(string text, Color color)
        {
            if (noteText == null) return;
            noteText.text = text;
            noteText.color = color;
        }

        // ------------------------------------------------------------------
        // Construction (SceneBuilder)
        // ------------------------------------------------------------------

        /// <summary>A modal like the settings: an ink scrim, and a card as tall as the list needs.</summary>
        public static MinigamePicker Create(Transform frame, GameConfig config)
        {
            var root = Ink.Node(frame, "MinigamePicker");
            root.Fill(-2000f, -2000f, -2000f, -2000f); // the scrim covers any aspect ratio
            var scrim = root.gameObject.AddComponent<Image>();
            scrim.color = Palette.Scrim;
            scrim.raycastTarget = true;
            var panel = root.gameObject.AddComponent<MinigamePicker>();
            panel.ApplyConfig(config);
            panel.SetMotion(1f, Vector2.zero);

            var space = Ink.Node(root, "Frame");
            space.Fill(2000f, 2000f, 2000f, 2000f);
            int rows = (MinigameRegistry.All.Count + Columns - 1) / Columns;
            float gridH = rows * (CellH + Gap) - Gap;
            const float cardW = 1640f, gridTop = 168f;
            float cardH = gridTop + gridH + 40f + 84f + 36f;
            var card = Ink.Box(space, "Card", Palette.PaperHi, 4f, 0f, raycast: true);
            card.rectTransform.At((1920f - cardW) * 0.5f, (1080f - cardH) * 0.5f, cardW, cardH);
            Ink.Shadow(card, 14f, Palette.Gold);
            var c = card.transform;

            var title = Ink.Kicker(c, "Title", "Minigames", 44f);
            Ink.BoxOf(title).Pin(48f, 40f, new Vector2(0f, 1f));
            panel.countText = Ink.Label(c, "Count", "69 OF 69 IN THE DECK", 15f);
            panel.countText.rectTransform.At(48f, 128f, 700f, 20f);
            panel.noteText = Ink.Text(c, "Note", Note, TypeRole.Body, 20f, Palette.Ink2, TextAlignmentOptions.TopRight).OneLine();
            panel.noteText.rectTransform.At(cardW - 48f - 900f, 124f, 900f, 28f);

            panel.grid = Ink.Node(c, "Grid");
            panel.grid.At(48f, gridTop, cardW - 96f, gridH);

            float buttonsTop = cardH - 36f - 84f;
            panel.allOnButton = Ink.TextButton(c, "AllOn", "ALL ON", 22f);
            panel.allOnButton.GetComponent<RectTransform>().At(48f, buttonsTop + 20f, 200f, 44f);
            panel.doneButton = Ink.Slab(c, "Done", "Done", Palette.Gold, Palette.Ink, 64f, 4f, 8f);
            panel.doneButton.GetComponent<RectTransform>().At(cardW - 48f - 300f, buttonsTop, 300f, 84f);
            return panel;
        }
    }
}
