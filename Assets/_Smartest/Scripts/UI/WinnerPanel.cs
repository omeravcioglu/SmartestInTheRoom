using System.Collections.Generic;
using Smartest.Core;
using Smartest.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// The end of the match as a front page: SMARTEST IN THE ROOM across the top, the winner's
    /// name as the headline, their points in a burst, a big token, the final standings, and
    /// the host's verdict. The host gets BACK TO LOBBY; everyone else waits for them.
    /// </summary>
    public class WinnerPanel : Panel
    {
        [SerializeField] private TMP_Text roundsText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Burst points;
        [SerializeField] private TMP_Text tokenMono;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private RectTransform standings;
        [SerializeField] private TMP_Text quote;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text waitingText;

        protected override void Awake()
        {
            base.Awake();
            if (backButton != null) backButton.onClick.AddListener(OnBack);
        }

        public void ShowWinner(PlayerData winner, IReadOnlyList<PlayerData> players, bool isHost, int rounds,
            string hostQuote)
        {
            var cfg = GameBootstrap.ConfigOrDefault;
            string name = winner != null ? winner.DisplayName : "Nobody";
            int score = winner != null ? winner.Score.Value : 0;

            if (roundsText != null) roundsText.text = $"{Mathf.Max(1, rounds)} ROUNDS · FIRST PAST {cfg.targetScore}";
            if (nameText != null) nameText.text = name + " wins.";
            if (points != null) points.Show(SeatCard.Score(score), "POINTS");
            if (caption != null) caption.text = $"{name} · {SeatCard.Score(score)} points.";
            if (quote != null) quote.text = string.IsNullOrEmpty(hostQuote) ? "That's the match." : hostQuote;

            var names = new List<string>();
            foreach (var p in players) names.Add(p.DisplayName);
            var monos = Monogram.ForAll(names);
            if (tokenMono != null)
            {
                int wi = -1;
                for (int i = 0; i < players.Count; i++) if (players[i] == winner) wi = i;
                tokenMono.text = wi >= 0 ? monos[wi] : Monogram.Base(name);
            }

            BuildStandings(winner, players, monos);

            if (backButton != null) backButton.gameObject.SetActive(isHost);
            if (waitingText != null) Ink.BoxOf(waitingText).gameObject.SetActive(!isHost);
        }

        private void BuildStandings(PlayerData winner, IReadOnlyList<PlayerData> players, List<string> monos)
        {
            if (standings == null) return;
            for (int i = standings.childCount - 1; i >= 0; i--) Destroy(standings.GetChild(i).gameObject);

            var order = new List<int>();
            for (int i = 0; i < players.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int byScore = players[b].Score.Value.CompareTo(players[a].Score.Value);
                return byScore != 0 ? byScore : players[a].OwnerClientId.CompareTo(players[b].OwnerClientId);
            });

            int top = 1;
            foreach (var p in players) top = Mathf.Max(top, p.Score.Value);
            top = Mathf.Max(top, GameBootstrap.ConfigOrDefault.targetScore);

            int place = 0, lastScore = int.MinValue;
            for (int k = 0; k < order.Count; k++)
            {
                var p = players[order[k]];
                if (p.Score.Value != lastScore) { place = k + 1; lastScore = p.Score.Value; }

                var row = Ink.Plain(standings, "Row", p.IsOwner ? Palette.GoldSoft : new Color(1f, 1f, 1f, 0f));
                Ink.Size(row, 704f, 50f);
                var rule = Ink.Plain(row.transform, "Rule", Palette.Ink);
                rule.rectTransform.At(0f, 48f, 704f, 2f);

                var num = Ink.Text(row.transform, "Place", place.ToString(), TypeRole.Display, 38f, Palette.Ink,
                    TextAlignmentOptions.MidlineLeft, lineHeight: 1f).OneLine();
                num.rectTransform.At(10f, 0f, 36f, 50f);
                var mono = Ink.Token(row.transform, "Token", 36f, 2.5f, Palette.Paper2, monos[order[k]], 16f);
                ((RectTransform)mono.transform.parent).At(60f, 7f, 36f, 36f);
                var n = Ink.Text(row.transform, "Name", p.DisplayName, TypeRole.Name, 28f, Palette.Ink,
                    TextAlignmentOptions.MidlineLeft).OneLine();
                n.overflowMode = TextOverflowModes.Ellipsis;
                n.rectTransform.At(110f, 0f, 170f, 50f);

                var bar = Ink.Box(row.transform, "Bar", Palette.PaperHi, 2.5f);
                bar.rectTransform.At(294f, 17f, 320f, 16f);
                var fill = Ink.Plain(bar.transform, "Fill", p == winner ? Palette.Gold : Palette.Ink);
                float t = Mathf.Clamp01((float)Mathf.Max(0, p.Score.Value) / top);
                fill.rectTransform.At(2.5f, 2.5f, (320f - 5f) * t, 11f);

                var s = Ink.Text(row.transform, "Score", SeatCard.Score(p.Score.Value), TypeRole.Display, 42f, Palette.Ink,
                    TextAlignmentOptions.MidlineRight, lineHeight: 1f).OneLine();
                s.rectTransform.At(628f, 0f, 66f, 50f);
            }
        }

        private void OnBack()
        {
            if (NetSession.Instance != null && NetSession.Instance.IsHost)
                NetSession.Instance.ReturnToLobby();
        }

        // ------------------------------------------------------------------
        // Construction (SceneBuilder)
        // ------------------------------------------------------------------

        public static WinnerPanel Create(Transform frame, GameConfig config, SettingsPanel settings)
        {
            var root = Ink.Node(frame, "WinnerPanel");
            root.Fill();
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = Palette.Paper;
            var panel = root.gameObject.AddComponent<WinnerPanel>();
            panel.ApplyConfig(config);
            panel.SetMotion(1f, Vector2.zero);

            // Top strip.
            var over = Ink.Label(root, "GameOver", "GAME OVER", 14f);
            over.rectTransform.At(48f, 18f, 400f, 56f);
            over.alignment = TextAlignmentOptions.MidlineLeft;
            panel.roundsText = Ink.Label(root, "Rounds", "21 ROUNDS · FIRST PAST 100", 14f, null, TextAlignmentOptions.Midline);
            panel.roundsText.rectTransform.At(560f, 18f, 800f, 56f);
            SoundButton.Create(root, settings, 1872f - 52f, 20f, 52f);
            Ink.Plain(root, "Rule0", Palette.Ink).rectTransform.At(48f, 82f, 1824f, 2f);

            var plate = Ink.Headline(root, "Nameplate", "Smartest in the Room", 118f, 5f, 60f).OneLine();
            plate.alignment = TextAlignmentOptions.Top;
            plate.rectTransform.At(48f, 94f, 1824f, 106f);
            Ink.Plain(root, "Rule1", Palette.Ink).rectTransform.At(48f, 204f, 1824f, 6f);
            Ink.Plain(root, "Rule2", Palette.Ink).rectTransform.At(48f, 214f, 1824f, 2f);

            panel.nameText = Ink.Headline(root, "Winner", "Ayşe wins.", 260f, 10f, 110f).OneLine();
            panel.nameText.rectTransform.At(48f, 236f, 1460f, 226f);

            panel.points = RoundPanel.CreateBurst(root, "Points", 240f, InkSprites.Burst16, 104f, 15f, "POINTS", "POINTS", 10f, 0f);
            ((RectTransform)panel.points.transform).At(1540f, 228f, 240f, 240f);

            // The winner's token.
            var tokenBox = Ink.Box(root, "TokenBox", Palette.PaperHi, 4f);
            tokenBox.rectTransform.At(48f, 486f, 540f, 420f);
            Ink.Shadow(tokenBox, 10f);
            panel.tokenMono = Ink.Token(tokenBox.transform, "Token", 320f, 5f, Palette.Gold, "AY", 190f);
            ((RectTransform)panel.tokenMono.transform.parent).At(110f, 50f, 320f, 320f);
            var winnerTag = Ink.Chip(tokenBox.transform, "WinnerTag", "WINNER", TypeRole.Sticker, 40f, Palette.Ink,
                Palette.Gold, 0f, new RectOffset(18, 18, 8, 6), caps: true, tracking: 0.06f);
            Ink.BoxOf(winnerTag).Tilt(-8f);
            Ink.BoxOf(winnerTag).Pin(540f + 22f, 420f - 34f, new Vector2(1f, 0f)); // right -22, bottom 34
            panel.caption = Ink.Text(root, "Caption", "", TypeRole.Body, 24f, Palette.Ink, TextAlignmentOptions.TopLeft);
            panel.caption.fontStyle |= FontStyles.Italic | FontStyles.Bold;
            panel.caption.rectTransform.At(48f, 928f, 540f, 60f);

            // Final standings.
            var box = Ink.Box(root, "Standings", Palette.PaperHi, 4f);
            Ink.Shadow(box, 10f);
            box.rectTransform.Pin(640f, 486f, new Vector2(0f, 1f));
            box.rectTransform.sizeDelta = new Vector2(752f, 400f);
            var stack = box.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(22, 22, 18, 20);
            stack.spacing = 6f;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = stack.childForceExpandHeight = false;
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            RoundPanel.DealHeading(box.transform, "FINAL STANDINGS");
            panel.standings = Ink.Column(box.transform, "Rows", 0f, TextAnchor.UpperLeft, hug: false);

            // The host's verdict.
            var quoteBox = Ink.Box(root, "Quote", Palette.PaperHi, 4f);
            Ink.Shadow(quoteBox, 10f);
            quoteBox.rectTransform.Pin(1440f, 486f, new Vector2(0f, 1f));
            quoteBox.rectTransform.sizeDelta = new Vector2(432f, 300f);
            var qStack = quoteBox.gameObject.AddComponent<VerticalLayoutGroup>();
            qStack.padding = new RectOffset(26, 26, 22, 26);
            qStack.spacing = 10f;
            qStack.childControlWidth = qStack.childControlHeight = true;
            qStack.childForceExpandWidth = qStack.childForceExpandHeight = false;
            quoteBox.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var mark = Ink.Text(quoteBox.transform, "Mark", "“", TypeRole.Display, 120f, Palette.Ink, TextAlignmentOptions.TopLeft);
            Ink.Size(mark, 60f, 50f);
            panel.quote = Ink.Text(quoteBox.transform, "Line", "", TypeRole.Body, 30f, Palette.Ink, TextAlignmentOptions.TopLeft,
                lineHeight: 1.35f);
            panel.quote.fontStyle |= FontStyles.Italic | FontStyles.Bold;
            panel.quote.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Ink.Label(quoteBox.transform, "Who", "— YOUR HOST", 13f);

            // Host: back to the lobby. Everyone else: a stamp.
            panel.backButton = Ink.Slab(root, "BackToLobby", "Back to lobby", Palette.Gold, Palette.Ink, 64f);
            panel.backButton.GetComponent<RectTransform>().At(1440f, 900f, 432f, 104f);
            panel.waitingText = Ink.Stamp(root, "Waiting", "WAITING FOR HOST…", 30f, -4f, 4f, Palette.Ink, Palette.PaperHi);
            Ink.BoxOf(panel.waitingText).Pin(1470f, 950f, new Vector2(0f, 0.5f));
            return panel;
        }
    }
}
