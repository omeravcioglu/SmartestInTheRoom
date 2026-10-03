using System;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// One seat in the lobby grid. Taken: token, name, tag, a paper card with a hard shadow
    /// (gold if it's you). Open: a dashed outline that says "waiting…". The host also gets a
    /// small ✕ on everyone else's seat: one click turns it into KICK?, a second sends them away.
    /// </summary>
    public class LobbySeat : MonoBehaviour
    {
        [SerializeField] private Image card;
        [SerializeField] private Image dashed;
        [SerializeField] private HardShadow shadow;
        [SerializeField] private Image token;
        [SerializeField] private Image tokenDashed;
        [SerializeField] private TMP_Text mono;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text tagText;
        [SerializeField] private TMP_Text seatText;
        [SerializeField] private TMP_Text noteText;
        [SerializeField] private Button kickButton;
        [SerializeField] private Image kickIcon;
        [SerializeField] private TMP_Text kickText;

        private const float KickSize = 34f;
        private const float AskSeconds = 3f;

        /// <summary>The host clicked KICK? on this seat: the player sitting in it.</summary>
        public event Action<ulong> KickConfirmed;

        private ulong _clientId;
        private float _askingUntil;

        private void Awake()
        {
            if (kickButton != null) kickButton.onClick.AddListener(OnKick);
        }

        private void Update()
        {
            if (_askingUntil > 0f && Time.unscaledTime > _askingUntil) StopAsking();
        }

        public void SetTaken(int seat, ulong clientId, string playerName, string monogram, string tag, bool you,
            string note, bool kickable)
        {
            if (clientId != _clientId) StopAsking(); // someone else sits here now: don't kick them by mistake
            _clientId = clientId;
            card.enabled = true;
            card.color = you ? Palette.Gold : Palette.PaperHi;
            dashed.enabled = false;
            shadow.enabled = true;
            token.enabled = true;
            token.color = you ? Palette.PaperHi : Palette.Paper2;
            tokenDashed.enabled = false;
            mono.text = monogram;
            nameText.text = playerName;
            nameText.color = Palette.Ink;
            tagText.text = tag ?? string.Empty;
            seatText.text = "SEAT " + seat;
            noteText.text = note ?? string.Empty;
            SetKickable(kickable);
        }

        public void SetOpen(int seat)
        {
            card.enabled = false;
            dashed.enabled = true;
            shadow.enabled = false;
            token.enabled = false;
            tokenDashed.enabled = true;
            mono.text = string.Empty;
            nameText.text = "Open";
            nameText.color = Palette.Ink2;
            tagText.text = string.Empty;
            seatText.text = "SEAT " + seat;
            noteText.text = "waiting…";
            SetKickable(false);
        }

        private void SetKickable(bool on)
        {
            if (kickButton == null || kickButton.gameObject.activeSelf == on) return;
            kickButton.gameObject.SetActive(on);
            StopAsking();
        }

        private void OnKick()
        {
            if (_askingUntil <= 0f)
            {
                _askingUntil = Time.unscaledTime + AskSeconds;
                ShowAsking(true);
                Sounds.Play(Sounds.Kind.Click);
                return;
            }
            StopAsking();
            KickConfirmed?.Invoke(_clientId);
        }

        private void StopAsking()
        {
            _askingUntil = 0f;
            ShowAsking(false);
        }

        /// <summary>The ✕ in its corner, or a red KICK? grown out of it to the left.</summary>
        private void ShowAsking(bool asking)
        {
            if (kickButton == null) return;
            var box = (RectTransform)kickButton.transform;
            box.sizeDelta = new Vector2(asking ? 104f : KickSize, KickSize);
            if (kickButton.targetGraphic is Image face) face.color = asking ? Palette.Red : Palette.PaperHi;
            if (kickIcon != null) kickIcon.enabled = !asking;
            if (kickText != null) kickText.enabled = asking;
        }

        public static LobbySeat Create(Transform parent, string name, float left, float top, float width, float height)
        {
            var root = Ink.Node(parent, name);
            root.At(left, top, width, height);
            var seat = root.gameObject.AddComponent<LobbySeat>();

            seat.card = Ink.Box(root, "Card", Palette.PaperHi, 3f);
            seat.card.rectTransform.Fill();
            seat.shadow = Ink.Shadow(seat.card, 6f);
            seat.dashed = Ink.Dashed(root, "Open", Palette.Paper, 3f);
            seat.dashed.rectTransform.Fill();

            seat.token = Ink.Disc(root, "Token", 64f, 3f, Palette.Paper2, true);
            seat.token.rectTransform.At(18f, 16f, 64f, 64f);
            seat.tokenDashed = Ink.Icon(root, "OpenToken", InkSprites.Disc(64f, 3f), Palette.Paper);
            seat.tokenDashed.rectTransform.At(18f, 16f, 64f, 64f);
            seat.mono = Ink.Text(seat.token.transform, "Mono", "", TypeRole.Display, 30f, Palette.Ink,
                TextAlignmentOptions.Center, caps: true).OneLine();
            seat.mono.rectTransform.Fill();

            seat.nameText = Ink.Text(root, "Name", "Open", TypeRole.Name, 34f, Palette.Ink, TextAlignmentOptions.BottomLeft,
                lineHeight: 1f).OneLine();
            seat.nameText.overflowMode = TextOverflowModes.Ellipsis;
            seat.nameText.rectTransform.At(94f, 10f, width - 94f - 16f, 46f);
            seat.tagText = Ink.Label(root, "Tag", "", 11f, Palette.Ink, TextAlignmentOptions.TopLeft, 0.12f);
            seat.tagText.rectTransform.At(94f, 60f, width - 94f - 16f, 16f);

            seat.seatText = Ink.Label(root, "Seat", "SEAT 1", 12f, Palette.Ink, TextAlignmentOptions.BottomLeft, 0.14f);
            seat.seatText.rectTransform.At(18f, height - 16f - 24f, 120f, 24f);
            seat.noteText = Ink.Text(root, "Note", "", TypeRole.Body, 18f, Palette.Ink2, TextAlignmentOptions.BottomRight).OneLine();
            seat.noteText.fontStyle |= FontStyles.Italic;
            seat.noteText.rectTransform.At(width - 18f - 150f, height - 16f - 26f, 150f, 26f);

            // The kick: a paper square on the card's top-right corner, half off it, so it never
            // crowds the name. Asking, it grows leftwards into a red KICK?.
            var kick = Ink.Box(root, "Kick", Palette.PaperHi, 3f, 0f, raycast: true);
            var rt = kick.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(KickSize * 0.4f, KickSize * 0.4f);
            rt.sizeDelta = new Vector2(KickSize, KickSize);
            Ink.Shadow(kick, 3f);
            seat.kickButton = kick.gameObject.AddComponent<Button>();
            seat.kickButton.transition = Selectable.Transition.None;
            seat.kickButton.targetGraphic = kick;
            seat.kickIcon = Ink.Icon(kick.transform, "Cross", InkSprites.Cross, Palette.Ink);
            seat.kickIcon.rectTransform.Fill(9f, 9f, 9f, 9f);
            seat.kickText = Ink.Text(kick.transform, "Ask", "KICK?", TypeRole.Sticker, 18f, Palette.OnRed,
                TextAlignmentOptions.Center, caps: true, tracking: 0.06f).OneLine();
            seat.kickText.rectTransform.Fill(6f, 2f, 6f, 2f);
            seat.kickText.enabled = false;
            kick.gameObject.SetActive(false);
            return seat;
        }
    }
}
