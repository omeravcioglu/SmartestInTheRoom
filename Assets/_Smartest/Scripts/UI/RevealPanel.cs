using System.Collections;
using System.Collections.Generic;
using Smartest.Core;
using Smartest.Net;
using Smartest.Rounds;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// The reveal, as a front page. A headline says how the room split ("3 Red. 5 Green."),
    /// then the evidence: a box per answer with everyone who picked it (two-way rounds), a
    /// column per number with its pickers stacked on top (number rounds), or a podium
    /// (minigames). Points stay hidden until the Scoring beat, then land as stickers.
    /// </summary>
    public class RevealPanel : Panel
    {
        [SerializeField] private TMP_Text kicker;
        [SerializeField] private TMP_Text headline;
        [SerializeField] private TMP_Text scoring;
        [SerializeField] private RectTransform body;

        public const float Width = 1824f;
        public const float Height = 612f;

        private readonly List<Transform> _appear = new List<Transform>();
        private readonly List<Transform> _deltas = new List<Transform>();
        private Coroutine _sequence;

        /// <summary>One answered player, resolved to name and token.</summary>
        private struct Row
        {
            public ulong Id;
            public string Name;
            public string Mono;
            public bool You;
            public int Answer;
            public int Delta;
            public int Place;
        }

        // ------------------------------------------------------------------

        public void ShowResults(RoundDefinition def, IReadOnlyList<PlayerRoundResult> results, string revealLine,
            float stagger)
        {
            if (_sequence != null) StopCoroutine(_sequence);
            Clear();

            var rows = Resolve(results);
            bool minigame = def != null && def.IsMinigame;
            string title = def != null ? def.title : "The Reveal";
            if (kicker != null) kicker.text = minigame ? title + " · Final" : title + " · The Reveal";
            SetScoring(false);

            if (minigame)
            {
                if (headline != null) headline.text = string.IsNullOrEmpty(revealLine) ? "Game over." : revealLine;
                BuildRanking(rows);
            }
            else if (def != null && def.inputType == InputType.Number1to10)
            {
                if (headline != null) headline.text = NumbersHeadline(rows);
                BuildNumbers(def, rows);
            }
            else
            {
                if (headline != null) headline.text = SplitHeadline(def, rows);
                BuildBuckets(def, rows);
            }

            _sequence = StartCoroutine(Appear(Mathf.Max(0.04f, stagger)));
        }

        /// <summary>Scoring: the points land.</summary>
        public void AnimateDeltas(float duration)
        {
            SetScoring(true);
            if (isActiveAndEnabled) StartCoroutine(PopDeltas());
            else foreach (var d in _deltas) if (d != null) d.gameObject.SetActive(true);
        }

        private void SetScoring(bool on)
        {
            if (scoring != null) Ink.BoxOf(scoring).gameObject.SetActive(on);
        }

        private void Clear()
        {
            _appear.Clear();
            _deltas.Clear();
            if (body == null) return;
            for (int i = body.childCount - 1; i >= 0; i--) Destroy(body.GetChild(i).gameObject);
        }

        private IEnumerator Appear(float stagger)
        {
            // Each piece fades up to its own resting alpha (a "nobody picked it" column rests at 40%).
            var targets = new List<(CanvasGroup cg, float alpha)>();
            foreach (var t in _appear)
            {
                if (t == null) continue;
                var cg = t.GetComponent<CanvasGroup>();
                float rest = cg != null ? cg.alpha : 1f;
                if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                targets.Add((cg, rest));
            }
            foreach (var (cg, rest) in targets)
            {
                var group = cg;
                float to = rest;
                StartCoroutine(Tween.To(0.22f, Ease.OutCubic, k => { if (group != null) group.alpha = k * to; }));
                yield return new WaitForSecondsRealtime(stagger);
            }
            _sequence = null;
        }

        private IEnumerator PopDeltas()
        {
            foreach (var d in _deltas)
            {
                if (d == null) continue;
                d.gameObject.SetActive(true);
                var tr = d;
                StartCoroutine(Tween.To(0.22f, Ease.OutBack, k =>
                {
                    float s = Mathf.LerpUnclamped(0.4f, 1f, k);
                    if (tr != null) tr.localScale = new Vector3(s, s, 1f);
                }));
                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        private static List<Row> Resolve(IReadOnlyList<PlayerRoundResult> results)
        {
            var names = new List<string>();
            foreach (var p in PlayerData.All) names.Add(p.DisplayName);
            var monos = Monogram.ForAll(names);

            var rows = new List<Row>();
            if (results == null) return rows;
            foreach (var r in results)
            {
                var p = PlayerData.Get(r.ClientId);
                int index = -1;
                for (int i = 0; i < PlayerData.All.Count; i++) if (PlayerData.All[i].OwnerClientId == r.ClientId) index = i;
                string name = p != null ? p.DisplayName : "Player";
                rows.Add(new Row
                {
                    Id = r.ClientId,
                    Name = name,
                    Mono = index >= 0 ? monos[index] : Monogram.Base(name),
                    You = p != null && p.IsOwner,
                    Answer = r.Answer,
                    Delta = r.Delta,
                    Place = r.Place
                });
            }
            return rows;
        }

        // ------------------------------------------------------------------
        // Headlines
        // ------------------------------------------------------------------

        /// <summary>"3 Red. 5 Green." — the split, in the answers' own words.</summary>
        public static string SplitHeadline(RoundDefinition def, IReadOnlyList<PlayerRoundResult> results)
        {
            var rows = new List<Row>();
            if (results != null) foreach (var r in results) rows.Add(new Row { Answer = r.Answer, Delta = r.Delta });
            return SplitHeadline(def, rows);
        }

        private static string SplitHeadline(RoundDefinition def, List<Row> rows)
        {
            int a = 0, b = 0, none = 0;
            foreach (var r in rows)
            {
                if (r.Answer == 1) a++;
                else if (r.Answer == 0) b++;
                else none++;
            }
            if (a + b == 0) return "Nobody answered.";
            var lookA = AnswerLook.For(def, 1);
            var lookB = AnswerLook.For(def, 0);
            string wa = Title(lookA.Word), wb = Title(lookB.Word);
            string s = $"{a} <color={Palette.ToHex(lookA.WordInText)}>{wa}.</color> {b} <color={Palette.ToHex(lookB.WordInText)}>{wb}.</color>";
            return s;
        }

        /// <summary>"3 wins." / "Everyone scores." / "Nobody wins."</summary>
        private static string NumbersHeadline(List<Row> rows)
        {
            int answered = 0, best = int.MinValue;
            bool allSame = true;
            int first = int.MinValue;
            foreach (var r in rows)
            {
                if (r.Answer < 0) continue;
                answered++;
                if (first == int.MinValue) first = r.Delta;
                else if (r.Delta != first) allSame = false;
                best = Mathf.Max(best, r.Delta);
            }
            if (answered == 0) return "Nobody answered.";
            if (best <= 0) return best == 0 && allSame ? "No points." : "Nobody wins.";
            if (allSame) return "Everyone scores.";

            var winners = WinningNumbers(rows, best);
            if (winners.Count == 1) return $"{winners[0]} wins.";
            if (winners.Count == 2) return $"{winners[0]} and {winners[1]} win.";
            return "Split decision.";
        }

        private static List<int> WinningNumbers(List<Row> rows, int best)
        {
            var list = new List<int>();
            foreach (var r in rows)
                if (r.Answer >= 0 && r.Delta == best && !list.Contains(r.Answer)) list.Add(r.Answer);
            list.Sort();
            return list;
        }

        private static string Title(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            return word.Length == 1 ? word : word.Substring(0, 1) + word.Substring(1).ToLowerInvariant();
        }

        // ------------------------------------------------------------------
        // Two-way rounds: one box per answer
        // ------------------------------------------------------------------

        private void BuildBuckets(RoundDefinition def, List<Row> rows)
        {
            var groups = new List<(AnswerLook look, string word, List<Row> members)>();
            var a = new List<Row>(); var b = new List<Row>(); var none = new List<Row>();
            foreach (var r in rows)
            {
                if (r.Answer == 1) a.Add(r);
                else if (r.Answer == 0) b.Add(r);
                else none.Add(r);
            }
            var la = AnswerLook.For(def, 1);
            var lb = AnswerLook.For(def, 0);
            groups.Add((la, la.Word, a));
            groups.Add((lb, lb.Word, b));
            if (none.Count > 0)
                groups.Add((new AnswerLook { Word = "NO ANSWER", Fill = Palette.Paper2, Ink = Palette.Ink2 }, "NO ANSWER", none));

            const float top = 250f, height = 300f, gap = 24f;
            float available = Width - gap * (groups.Count - 1);

            // Widths follow the head count, but every box keeps room for its columns of names.
            var widths = new float[groups.Count];
            var mins = new float[groups.Count];
            float weightSum = 0f;
            for (int i = 0; i < groups.Count; i++)
            {
                int cols = Mathf.Max(1, Mathf.CeilToInt(groups[i].members.Count / 3f));
                mins[i] = Mathf.Max(400f, cols * 250f + 40f);
                weightSum += Mathf.Max(1, groups[i].members.Count);
            }
            for (int pass = 0; pass < 3; pass++)
            {
                float fixedSum = 0f, freeWeight = 0f;
                for (int i = 0; i < groups.Count; i++)
                {
                    if (widths[i] > 0f && Mathf.Approximately(widths[i], mins[i])) fixedSum += widths[i];
                    else freeWeight += Mathf.Max(1, groups[i].members.Count);
                }
                for (int i = 0; i < groups.Count; i++)
                {
                    if (widths[i] > 0f && Mathf.Approximately(widths[i], mins[i])) continue;
                    float w = (available - fixedSum) * Mathf.Max(1, groups[i].members.Count) / Mathf.Max(1f, freeWeight);
                    widths[i] = Mathf.Max(w, mins[i]);
                }
            }
            // If the minimums overflow the page, squeeze everything evenly.
            float total = 0f;
            foreach (var w in widths) total += w;
            if (total > available) for (int i = 0; i < widths.Length; i++) widths[i] *= available / total;

            float x = 0f;
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                BuildBucket(g.look, g.word, g.members, x, top, widths[i], height, i);
                x += widths[i] + gap;
            }
        }

        private void BuildBucket(AnswerLook look, string word, List<Row> members, float x, float top, float width,
            float height, int index)
        {
            var box = Ink.Box(body, "Bucket" + index, Palette.PaperHi, 4f);
            box.rectTransform.At(x, top, width, height);
            Ink.Shadow(box, 10f);
            _appear.Add(box.transform);

            var band = Ink.Plain(box.transform, "Band", look.Fill);
            band.rectTransform.At(4f, 4f, width - 8f, 72f);
            var rule = Ink.Plain(box.transform, "BandRule", Palette.Ink);
            rule.rectTransform.At(4f, 76f, width - 8f, 4f);

            float tx = 20f;
            if (look.Glyph != null)
            {
                var glyph = Ink.Icon(band.transform, "Glyph", look.Glyph, look.Ink);
                glyph.rectTransform.At(16f, 18f, 36f, 36f);
                tx = 64f;
            }
            var title = Ink.Text(band.transform, "Title", $"{word} — {members.Count}", TypeRole.Display, 56f,
                look.Ink, TextAlignmentOptions.MidlineLeft, caps: true, lineHeight: 1f).OneLine();
            title.rectTransform.At(tx, 0f, width - tx - 170f, 72f);
            title.Fit(28f);

            // Everyone in the box got the same? Say it once on the band.
            bool uniform = members.Count > 0;
            foreach (var m in members) if (m.Delta != members[0].Delta) uniform = false;
            if (uniform)
            {
                int d = members[0].Delta;
                string each = members.Count > 1 ? " EACH" : string.Empty;
                var sticker = Ink.Sticker(band.transform, "Each", SeatCard.Format(d) + each, 22f,
                    Palette.DeltaFill(d), Palette.DeltaText(d), index % 2 == 0 ? -3f : 2f);
                Ink.BoxOf(sticker).Pin(width - 8f - 20f, 36f, new Vector2(1f, 0.5f));
                Ink.BoxOf(sticker).gameObject.SetActive(false);
                _deltas.Add(Ink.BoxOf(sticker));
            }

            int cols = Mathf.Max(1, Mathf.CeilToInt(members.Count / 3f));
            float colGap = 28f;
            float colW = (width - 40f - (cols - 1) * colGap) / cols;
            for (int i = 0; i < members.Count; i++)
            {
                int c = i / 3, r = i % 3;
                var row = NameRow(box.transform, members[i], 30f, 38f);
                row.At(20f + c * (colW + colGap), 96f + r * 64f, colW, 54f);
                _appear.Add(row);
            }
        }

        /// <summary>token · name · delta chip, over a 2 px rule. Your row sits on soft gold.</summary>
        private RectTransform NameRow(Transform parent, Row m, float nameSize, float tokenSize)
        {
            var row = Ink.Plain(parent, "Row", m.You ? Palette.GoldSoft : new Color(1f, 1f, 1f, 0f));
            var rt = row.rectTransform;
            var line = Ink.Plain(row.transform, "Rule", Palette.Ink);
            line.rectTransform.anchorMin = new Vector2(0f, 0f);
            line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.pivot = new Vector2(0.5f, 0f);
            line.rectTransform.sizeDelta = new Vector2(0f, 2f);
            line.rectTransform.anchoredPosition = Vector2.zero;

            var mono = Ink.Token(row.transform, "Token", tokenSize, 2.5f, m.You ? Palette.Gold : Palette.Paper2, m.Mono, tokenSize * 0.47f);
            var tokenRt = (RectTransform)mono.transform.parent;
            tokenRt.anchorMin = tokenRt.anchorMax = new Vector2(0f, 0.5f);
            tokenRt.pivot = new Vector2(0f, 0.5f);
            tokenRt.anchoredPosition = new Vector2(0f, 0f);

            var name = Ink.Text(row.transform, "Name", m.You ? m.Name + " (you)" : m.Name, TypeRole.Name, nameSize,
                Palette.Ink, TextAlignmentOptions.MidlineLeft).OneLine();
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = new Vector2(tokenSize + 12f, 0f);
            name.rectTransform.offsetMax = new Vector2(-84f, 0f);

            var delta = Ink.Chip(row.transform, "Delta", SeatCard.Format(m.Delta), TypeRole.Sticker, 24f,
                Palette.DeltaFill(m.Delta), Palette.DeltaText(m.Delta), 0f, new RectOffset(12, 12, 3, 2));
            var dRt = Ink.BoxOf(delta);
            dRt.anchorMin = dRt.anchorMax = new Vector2(1f, 0.5f);
            dRt.pivot = new Vector2(1f, 0.5f);
            dRt.anchoredPosition = Vector2.zero;
            dRt.gameObject.SetActive(false);
            _deltas.Add(dRt);
            return rt;
        }

        // ------------------------------------------------------------------
        // Number rounds: a column per number
        // ------------------------------------------------------------------

        private void BuildNumbers(RoundDefinition def, List<Row> rows)
        {
            int min = def != null && def.allowZero ? 0 : 1;
            int count = 10 - min + 1;
            const float gap = 20f, top = 216f, height = 380f, tileH = 110f, chipH = 58f, chipGap = 8f;
            float w = Mathf.Min(160f, (1780f - (count - 1) * gap) / count);
            float x0 = (Width - (count * w + (count - 1) * gap)) * 0.5f;

            int best = int.MinValue;
            foreach (var r in rows) if (r.Answer >= 0) best = Mathf.Max(best, r.Delta);
            var winners = best > 0 ? WinningNumbers(rows, best) : new List<int>();
            bool youWon = false;
            foreach (var r in rows) if (r.You && r.Answer >= 0 && winners.Contains(r.Answer)) youWon = true;

            for (int v = min; v <= 10; v++)
            {
                float x = x0 + (v - min) * (w + gap);
                var col = Ink.Node(body, "Col" + v);
                col.At(x, top, w, height);
                _appear.Add(col);

                var pickers = new List<Row>();
                foreach (var r in rows) if (r.Answer == v) pickers.Add(r);
                bool won = winners.Contains(v);

                Image tile;
                if (pickers.Count == 0)
                {
                    tile = Ink.Dashed(col, "Tile", Palette.PaperHi, 4f);
                    col.gameObject.AddComponent<CanvasGroup>().alpha = 0.4f;
                }
                else
                {
                    tile = Ink.Box(col, "Tile", won ? Palette.Gold : Palette.PaperHi, 4f);
                    Ink.Shadow(tile, 6f);
                }
                tile.rectTransform.At(0f, height - tileH, w, tileH);
                var num = Ink.Text(tile.transform, "Value", v.ToString(), TypeRole.Display, 96f, Palette.Ink,
                    TextAlignmentOptions.Center, lineHeight: 1f).OneLine();
                num.rectTransform.Fill();

                int shown = pickers.Count <= 4 ? pickers.Count : 3;
                float y = height - tileH - chipGap - chipH;
                for (int i = 0; i < shown; i++, y -= chipH + chipGap)
                    PickChip(col, pickers[i], w).At(0f, y, w, chipH);
                if (pickers.Count > shown)
                {
                    var more = Ink.Box(col, "More", Palette.Paper2, 3f);
                    more.rectTransform.At(0f, y, w, chipH);
                    var t = Ink.Text(more.transform, "Text", $"+{pickers.Count - shown} MORE", TypeRole.Sticker, 20f,
                        Palette.Ink, TextAlignmentOptions.Center).OneLine();
                    t.rectTransform.Fill();
                    y -= chipH + chipGap;
                }

                if (won && winners.Count == 1)
                {
                    float labelY = y + chipH - 26f; // just above the top chip
                    var winner = Ink.Chip(col, "Winner", "WINNER " + SeatCard.Format(best), TypeRole.Sticker, 22f,
                        Palette.Ink, Palette.Gold, 0f, new RectOffset(10, 10, 6, 4));
                    Ink.BoxOf(winner).Tilt(-6f);
                    Ink.BoxOf(winner).Pin(4f, labelY, new Vector2(0f, 0.5f));
                    Ink.BoxOf(winner).gameObject.SetActive(false);
                    _deltas.Add(Ink.BoxOf(winner));
                    if (youWon)
                    {
                        var you = Ink.Sticker(col, "You", "THAT'S YOU!", 18f, Palette.Gold, Palette.Ink, 4f);
                        Ink.BoxOf(you).Pin(26f, labelY - 52f, new Vector2(0f, 0.5f));
                    }
                }
            }
        }

        private RectTransform PickChip(Transform parent, Row m, float width)
        {
            var chip = Ink.Box(parent, "Pick", m.You ? Palette.GoldSoft : Palette.PaperHi, 3f);
            var mono = Ink.Token(chip.transform, "Token", 36f, 2.5f, m.You ? Palette.Gold : Palette.Paper2, m.Mono, 16f);
            ((RectTransform)mono.transform.parent).At(8f, 11f, 36f, 36f);
            var name = Ink.Text(chip.transform, "Name", m.Name, TypeRole.Name, 22f, Palette.Ink,
                TextAlignmentOptions.MidlineLeft).OneLine();
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.rectTransform.At(52f, 0f, width - 60f, 58f);
            return chip.rectTransform;
        }

        // ------------------------------------------------------------------
        // Minigames: podium and the rest
        // ------------------------------------------------------------------

        private void BuildRanking(List<Row> rows)
        {
            rows.Sort((a, b) =>
            {
                int pa = a.Place > 0 ? a.Place : int.MaxValue, pb = b.Place > 0 ? b.Place : int.MaxValue;
                return pa != pb ? pa.CompareTo(pb) : string.CompareOrdinal(a.Name, b.Name);
            });

            var podium = new List<Row>();
            var rest = new List<Row>();
            foreach (var r in rows)
            {
                if (r.Place >= 1 && r.Place <= 3 && podium.Count < 3) podium.Add(r);
                else rest.Add(r);
            }

            // Podium, 2nd · 1st · 3rd.
            var order = new List<Row>();
            if (podium.Count >= 2) order.Add(podium[1]);
            if (podium.Count >= 1) order.Add(podium[0]);
            if (podium.Count >= 3) order.Add(podium[2]);
            const float colW = 290f, colGap = 26f, baseY = 600f;
            float stageW = order.Count * colW + (order.Count - 1) * colGap;
            float x = 880f + (944f - stageW) * 0.5f;
            foreach (var r in order)
            {
                float h = r.Place == 1 ? 420f : r.Place == 2 ? 340f : 280f;
                var fill = r.Place == 1 ? Palette.Gold : r.Place == 2 ? Palette.PaperHi : Palette.Paper2;
                var block = Ink.Box(body, "Place" + r.Place, fill, 4f);
                block.rectTransform.At(x, baseY - h, colW, h);
                Ink.Shadow(block, 10f);
                _appear.Add(block.transform);

                var place = Ink.Text(block.transform, "Place", Ordinal(r.Place), TypeRole.Display, 72f, Palette.Ink,
                    TextAlignmentOptions.Top, caps: true, lineHeight: 0.8f).OneLine();
                place.rectTransform.At(0f, 18f, colW, 62f);
                var mono = Ink.Token(block.transform, "Token", 84f, 3f, Palette.PaperHi, r.Mono, 36f);
                ((RectTransform)mono.transform.parent).At((colW - 84f) * 0.5f, 90f, 84f, 84f);
                var name = Ink.Text(block.transform, "Name", r.Name, TypeRole.Name, 40f, Palette.Ink,
                    TextAlignmentOptions.Top).OneLine();
                name.overflowMode = TextOverflowModes.Ellipsis;
                name.rectTransform.At(10f, 184f, colW - 20f, 54f);
                if (r.You)
                {
                    var you = Ink.Sticker(block.transform, "You", "THAT'S YOU!", 18f, Palette.Gold, Palette.Ink, 4f);
                    Ink.BoxOf(you).Pin(colW * 0.5f, 250f, new Vector2(0.5f, 0.5f));
                }

                var delta = Ink.Sticker(block.transform, "Delta", SeatCard.Format(r.Delta), 30f,
                    Palette.DeltaFill(r.Delta), Palette.DeltaText(r.Delta), 7f);
                Ink.BoxOf(delta).Pin(colW + 16f, 4f, new Vector2(1f, 0.5f));
                Ink.BoxOf(delta).gameObject.SetActive(false);
                _deltas.Add(Ink.BoxOf(delta));
                x += colW + colGap;
            }

            if (rest.Count == 0) return;
            var label = Ink.Label(body, "Else", "EVERYONE ELSE", 14f);
            label.rectTransform.At(0f, 250f, 820f, 20f);
            float y = 280f;
            foreach (var r in rest)
            {
                var row = Ink.Box(body, "Row", r.You ? Palette.GoldSoft : Palette.PaperHi, 3f);
                row.rectTransform.At(0f, y, 820f, 52f);
                _appear.Add(row.transform);
                var place = Ink.Text(row.transform, "Place", Ordinal(r.Place), TypeRole.Display, 34f, Palette.Ink,
                    TextAlignmentOptions.MidlineLeft, caps: true).OneLine();
                place.rectTransform.At(12f, 0f, 74f, 52f);
                var mono = Ink.Token(row.transform, "Token", 38f, 2.5f, Palette.Paper2, r.Mono, 17f);
                ((RectTransform)mono.transform.parent).At(92f, 7f, 38f, 38f);
                var name = Ink.Text(row.transform, "Name", r.You ? r.Name + " (you)" : r.Name, TypeRole.Name, 30f,
                    Palette.Ink, TextAlignmentOptions.MidlineLeft).OneLine();
                name.rectTransform.At(144f, 0f, 520f, 52f);
                var delta = Ink.Chip(row.transform, "Delta", SeatCard.Format(r.Delta), TypeRole.Sticker, 24f,
                    Palette.DeltaFill(r.Delta), Palette.DeltaText(r.Delta), 2.5f, new RectOffset(10, 10, 2, 1));
                Ink.BoxOf(delta).Pin(820f - 14f, 26f, new Vector2(1f, 0.5f));
                Ink.BoxOf(delta).gameObject.SetActive(false);
                _deltas.Add(Ink.BoxOf(delta));
                y += 60f;
            }
        }

        public static string Ordinal(int place)
        {
            if (place <= 0) return "—";
            int mod100 = place % 100;
            if (mod100 >= 11 && mod100 <= 13) return place + "TH";
            switch (place % 10)
            {
                case 1: return place + "ST";
                case 2: return place + "ND";
                case 3: return place + "RD";
                default: return place + "TH";
            }
        }

        // ------------------------------------------------------------------
        // Construction (SceneBuilder)
        // ------------------------------------------------------------------

        public static RevealPanel Create(Transform stage, GameConfig config)
        {
            var root = Ink.Node(stage, "RevealPanel");
            root.At(0f, 0f, Width, Height);
            var panel = root.gameObject.AddComponent<RevealPanel>();
            panel.ApplyConfig(config);

            panel.kicker = Ink.Kicker(root, "Kicker", "The Button · The Reveal");
            Ink.BoxOf(panel.kicker).Pin(0f, 8f, new Vector2(0f, 1f));
            var headline = Ink.Headline(root, "Headline", "3 Red. 5 Green.", 150f, 6f, 64f);
            headline.rectTransform.At(0f, 70f, 1560f, 150f);
            headline.OneLine();
            panel.headline = headline;

            panel.scoring = Ink.Chip(root, "Scoring", "SCORING", TypeRole.Sticker, 22f, Palette.PaperHi, Palette.Ink, 3f,
                new RectOffset(18, 18, 10, 8), caps: true, tracking: 0.08f);
            var sBox = Ink.BoxOf(panel.scoring);
            sBox.Tilt(2f);
            Ink.Shadow(sBox.GetComponent<Image>(), 6f);
            sBox.Pin(Width, 150f, new Vector2(1f, 0.5f));

            var body = Ink.Node(root, "Body");
            body.At(0f, 0f, Width, Height);
            panel.body = body;
            return panel;
        }
    }
}
