using System.Collections;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// One player's seat on the rail, 214 × 170: token, name, score, rank, and one status
    /// line that says what they're doing right now. During a reveal a band across the top
    /// says what they picked and a sticker says what it earned them. Built in code, so the
    /// rail can make one per player at runtime.
    /// </summary>
    public class SeatCard : MonoBehaviour
    {
        public enum Status
        {
            None,
            Thinking,   // italic "thinking…"
            Playing,    // italic "playing…"
            OutEarlier, // italic "out · level 2", dimmed
            Locked,     // stamp
            YourMove,   // ink chip
            Done,       // ink chip
            Tied,       // ink chip
            StillIn,    // outline chip
            SittingOut  // outline chip
        }

        public const float Width = 214f;
        public const float Height = 170f;

        private Image _card;
        private Image _band;
        private Image _bandRule;
        private Image _bandGlyph;
        private TMP_Text _bandText;
        private CanvasGroup _content;
        private Image _token;
        private TMP_Text _mono;
        private TMP_Text _name;
        private TMP_Text _tag;
        private TMP_Text _score;
        private TMP_Text _rank;
        private TMP_Text _stamp;
        private TMP_Text _italic;
        private TMP_Text _inkChip;
        private TMP_Text _outline;
        private Image _star;
        private TMP_Text _delta;
        private TMP_Text _bigStamp;

        private bool _hasBand;
        private bool _you;
        private int _shownScore;
        private Coroutine _count;
        private Coroutine _pop;

        public ulong ClientId { get; set; }
        public RectTransform Rect => (RectTransform)transform;

        // ------------------------------------------------------------------

        public static SeatCard Create(Transform parent, string name = "Seat")
        {
            var card = Ink.Box(parent, name, Palette.PaperHi, 3f, 4f);
            Ink.Shadow(card, 6f);
            card.rectTransform.sizeDelta = new Vector2(Width, Height);
            var seat = card.gameObject.AddComponent<SeatCard>();
            seat.Build(card);
            return seat;
        }

        private void Build(Image card)
        {
            _card = card;
            var root = card.transform;

            _band = Ink.Plain(root, "Band", Palette.Paper2);
            _band.rectTransform.At(3f, 3f, Width - 6f, 30f);
            _bandRule = Ink.Plain(root, "BandRule", Palette.Ink);
            _bandRule.rectTransform.At(3f, 33f, Width - 6f, 3f);
            _bandGlyph = Ink.Icon(_band.transform, "Glyph", InkSprites.Diamond, Palette.OnRed);
            _bandGlyph.rectTransform.At(10f, 9f, 12f, 12f);
            _bandText = Ink.Label(_band.transform, "Picked", "PICKED RED", 11f, Palette.Ink, TextAlignmentOptions.MidlineLeft, 0.12f);
            _bandText.rectTransform.At(29f, 0f, Width - 40f, 30f);

            var content = Ink.Node(root, "Content");
            content.Fill();
            _content = content.gameObject.AddComponent<CanvasGroup>();

            _mono = Ink.Token(content, "Token", 46f, 2.5f, Palette.Paper2, "??", 21f);
            _token = _mono.transform.parent.GetComponent<Image>();

            _name = Ink.Text(content, "Name", "Player", TypeRole.Name, 24f, Palette.Ink, TextAlignmentOptions.TopLeft, lineHeight: 1f);
            _name.textWrappingMode = TextWrappingModes.NoWrap;
            _name.overflowMode = TextOverflowModes.Ellipsis;
            _tag = Ink.Label(content, "Tag", "", 10f, Palette.Ink, TextAlignmentOptions.TopLeft, 0.12f);

            // Score and rank share a baseline: TMP's Baseline alignment puts it at the rect's middle.
            _score = Ink.Text(content, "Score", "0", TypeRole.Display, 56f, Palette.Ink, TextAlignmentOptions.BaselineLeft);
            _score.textWrappingMode = TextWrappingModes.NoWrap;
            _rank = Ink.Text(content, "Rank", "No.1", TypeRole.Sticker, 13f, Palette.Ink, TextAlignmentOptions.BaselineRight);
            _rank.textWrappingMode = TextWrappingModes.NoWrap;

            _stamp = Ink.Stamp(content, "Stamp", "LOCKED", 15f, -5f);
            _italic = Ink.Text(content, "Doing", "thinking…", TypeRole.Body, 17f, Palette.Ink2, TextAlignmentOptions.TopLeft);
            _italic.fontStyle |= FontStyles.Italic;
            _italic.textWrappingMode = TextWrappingModes.NoWrap;
            _inkChip = Ink.Chip(content, "InkChip", "YOUR MOVE", TypeRole.Sticker, 15f, Palette.Ink, Palette.Gold, 0f,
                new RectOffset(10, 10, 4, 3), caps: true, tracking: 0.1f);
            _outline = Ink.Stamp(content, "Outline", "STILL IN", 14f, 0f, 2.5f);

            _star = Ink.Icon(root, "Leader", InkSprites.Star, Palette.Gold);
            _star.rectTransform.At(Width + 12f - 34f, -14f, 34f, 34f);

            _delta = Ink.Sticker(root, "Delta", "+15", 22f, Palette.Gold, Palette.Ink, 6f);
            Ink.BoxOf(_delta).Pin(Width + 14f, 31f, new Vector2(1f, 0.5f));

            _bigStamp = Ink.Stamp(root, "BigStamp", "OUT", 30f, -9f, 4f, Palette.Ink, Palette.PaperHi);
            Ink.BoxOf(_bigStamp).Pin(Width * 0.5f + 8f, 84f, new Vector2(0.5f, 0.5f));

            SetBand(null, Palette.Paper2, Palette.Ink, null);
            SetStatus(Status.None);
            SetLeader(false);
            SetDelta(null, false);
            SetBigStamp(null);
            SetDim(false);
        }

        // ------------------------------------------------------------------

        public void SetIdentity(string playerName, string mono, string tag, bool you)
        {
            _you = you;
            _card.color = you ? Palette.Gold : Palette.PaperHi;
            _token.color = you ? Palette.PaperHi : Palette.Paper2;
            _mono.text = mono;
            _name.text = playerName;
            _tag.text = tag ?? string.Empty;
            Relayout();
        }

        public void SetScore(int score)
        {
            if (_count != null) { StopCoroutine(_count); _count = null; }
            _shownScore = score;
            _score.text = Score(score);
        }

        public void AnimateScore(int to, float seconds)
        {
            if (to == _shownScore) return;
            if (_count != null) StopCoroutine(_count);
            if (!isActiveAndEnabled || seconds <= 0f) { SetScore(to); return; }
            int from = _shownScore;
            _shownScore = to;
            _count = StartCoroutine(Tween.CountInt(from, to, seconds, v => _score.text = Score(v)));
        }

        public int ShownScore => _shownScore;

        public void SetRank(string text) => _rank.text = text ?? string.Empty;

        public void SetLeader(bool on) => _star.gameObject.SetActive(on);

        public void SetDim(bool dim) => _content.alpha = dim ? 0.45f : 1f;

        /// <summary>The band across the top during a reveal. Null text removes it.</summary>
        public void SetBand(string text, Color fill, Color ink, Sprite glyph)
        {
            bool on = !string.IsNullOrEmpty(text);
            _band.gameObject.SetActive(on);
            _bandRule.gameObject.SetActive(on);
            if (on)
            {
                _band.color = fill;
                _bandText.text = text;
                _bandText.color = ink;
                _bandGlyph.gameObject.SetActive(glyph != null);
                if (glyph != null)
                {
                    _bandGlyph.sprite = glyph;
                    _bandGlyph.color = ink;
                }
                _bandText.rectTransform.At(glyph != null ? 29f : 10f, 0f, Width - 40f, 30f);
            }
            if (on != _hasBand)
            {
                _hasBand = on;
                Relayout();
            }
        }

        public void SetStatus(Status status, string text = null)
        {
            _stamp.transform.parent.gameObject.SetActive(status == Status.Locked);
            bool italic = status == Status.Thinking || status == Status.Playing || status == Status.OutEarlier;
            _italic.gameObject.SetActive(italic);
            bool ink = status == Status.YourMove || status == Status.Done || status == Status.Tied;
            _inkChip.transform.parent.gameObject.SetActive(ink);
            bool outline = status == Status.StillIn || status == Status.SittingOut;
            _outline.transform.parent.gameObject.SetActive(outline);

            switch (status)
            {
                case Status.Locked: _stamp.text = text ?? "LOCKED"; break;
                case Status.Thinking: _italic.text = text ?? "thinking…"; break;
                case Status.Playing: _italic.text = text ?? "playing…"; break;
                case Status.OutEarlier: _italic.text = text ?? "out"; break;
                case Status.YourMove: _inkChip.text = text ?? "YOUR MOVE"; break;
                case Status.Done: _inkChip.text = text ?? "DONE"; break;
                case Status.Tied: _inkChip.text = text ?? "TIED"; break;
                case Status.StillIn: _outline.text = text ?? "STILL IN"; break;
                case Status.SittingOut: _outline.text = text ?? "SITTING OUT"; break;
            }
        }

        /// <summary>What this round earned them. Null hides the sticker; <paramref name="pop"/> punches it in.</summary>
        public void SetDelta(int? delta, bool pop)
        {
            var box = Ink.BoxOf(_delta);
            bool on = delta.HasValue;
            bool wasOn = box.gameObject.activeSelf;
            box.gameObject.SetActive(on);
            if (!on) return;
            int d = delta.Value;
            _delta.text = Format(d);
            _delta.color = Palette.DeltaText(d);
            box.GetComponent<Image>().color = Palette.DeltaFill(d);
            if (pop && !wasOn && isActiveAndEnabled)
            {
                if (_pop != null) StopCoroutine(_pop);
                _pop = StartCoroutine(Pop(box));
            }
        }

        public void SetBigStamp(string text)
        {
            var box = Ink.BoxOf(_bigStamp);
            bool on = !string.IsNullOrEmpty(text);
            box.gameObject.SetActive(on);
            if (on) _bigStamp.text = text;
        }

        public static string Format(int d) => d > 0 ? "+" + d : d < 0 ? "−" + (-d) : "0";

        /// <summary>A score, with a real minus sign when it's below zero.</summary>
        public static string Score(int s) => s < 0 ? "−" + (-s) : s.ToString();

        // ------------------------------------------------------------------

        private static IEnumerator Pop(Transform t)
        {
            yield return Tween.To(0.22f, Ease.OutBack, k =>
            {
                float s = Mathf.LerpUnclamped(0.4f, 1f, k);
                t.localScale = new Vector3(s, s, 1f);
            });
        }

        /// <summary>Two layouts: with the reveal band the token shrinks and everything drops by the band.</summary>
        private void Relayout()
        {
            float x = 17f;
            float top = _hasBand ? 45f : 15f;
            float tok = _hasBand ? 40f : 46f;

            var tokenRt = _token.rectTransform;
            tokenRt.At(x, top, tok, tok);
            if (!_hasBand)
            {
                _token.sprite = InkSprites.Disc(46f, 2.5f, true);
                _mono.fontSize = 21f;
            }
            else
            {
                _token.sprite = InkSprites.Disc(40f, 2.5f, true);
                _mono.fontSize = 19f;
            }

            float nameX = x + tok + 10f;
            float nameW = Width - 17f - nameX;
            bool hasTag = !string.IsNullOrEmpty(_tag.text);
            float block = hasTag ? 39f : 24f;
            float nameTop = top + (tok - block) * 0.5f;
            // Taller than the words: an ellipsised TMP line that doesn't fit vertically vanishes.
            _name.rectTransform.At(nameX, nameTop - 5f, nameW, 34f);
            _tag.gameObject.SetActive(hasTag);
            _tag.rectTransform.At(nameX, nameTop + 27f, nameW, 13f);

            float scoreTop = top + tok + 8f;
            float baseline = scoreTop + 48f; // 56 px at 0.85 leading
            _score.rectTransform.At(x, baseline - 30f, 130f, 60f);
            _rank.rectTransform.At(Width - 17f - 90f, baseline - 30f, 90f, 60f);

            float statusTop = scoreTop + 56f;
            Ink.BoxOf(_stamp).Pin(Width - 17f, statusTop + 12f, new Vector2(1f, 0.5f));
            _italic.rectTransform.At(x, statusTop + 2f, Width - 30f, 24f);
            Ink.BoxOf(_inkChip).Pin(x, statusTop + 12f, new Vector2(0f, 0.5f));
            Ink.BoxOf(_outline).Pin(x, statusTop + 12f, new Vector2(0f, 0.5f));
        }
    }
}
