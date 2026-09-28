using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// One seat in the lobby grid. Taken: token, name, tag, a paper card with a hard shadow
    /// (gold if it's you). Open: a dashed outline that says "waiting…".
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

        public void SetTaken(int seat, string playerName, string monogram, string tag, bool you, string note)
        {
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
            return seat;
        }
    }
}
