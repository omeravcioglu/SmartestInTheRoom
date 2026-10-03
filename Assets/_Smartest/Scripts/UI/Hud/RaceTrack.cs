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
    /// "The race to 100" in the masthead: a ruler with one tag per player at their score.
    /// Your tag is gold and the leader wears a star. Tags closer than their own width drop to
    /// the lane under the ruler so nobody hides behind anybody. After a round, a gold bar
    /// shows how far each player just moved up and a blue bar how far they fell.
    /// </summary>
    [AddComponentMenu("Smartest/Race Track")]
    public class RaceTrack : MonoBehaviour
    {
        [SerializeField] private RectTransform tagsRoot;
        [SerializeField] private RectTransform barsRoot;
        [SerializeField] private Image star;
        [SerializeField] private TMP_Text toGo;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text endMark;
        [Tooltip("x of score 0 inside the track.")]
        [SerializeField] private float originX = 20f;
        [Tooltip("Length of the ruler from 0 to the target score.")]
        [SerializeField] private float rulerLength = 1000f;

        private const float TagWidth = 40f;
        private const float UpTop = 28f;
        private const float DownTop = 73f;
        private const float BarTop = 66f;

        private sealed class Tag
        {
            public RectTransform Root;
            public Image Box;
            public TMP_Text Mono;
            public Image PointerDown;
            public Image PointerUp;
            public float X = float.NaN;
            public bool Down;
            public Coroutine Move;
        }

        private readonly Dictionary<ulong, Tag> _tags = new Dictionary<ulong, Tag>();
        private readonly List<Image> _bars = new List<Image>();
        private int _shownTarget;

        // The lobby's choice (50, 100, 150); the scene is built with the config's.
        private static int Target => MatchSettings.Target;
        private float PxPerPoint => rulerLength / Target;

        private void ShowTarget()
        {
            int target = Target;
            if (target == _shownTarget) return;
            _shownTarget = target;
            if (label != null) label.text = $"THE RACE TO {target}";
            if (endMark != null) endMark.text = target.ToString();
        }

        public float CentreOf(int score) => originX + Mathf.Clamp(score, 0, Target) * PxPerPoint;

        /// <summary>One entry per player on the board.</summary>
        public struct Entry
        {
            public ulong Id;
            public string Mono;
            public int Score;
            public bool Local;
        }

        /// <summary>Place every tag. <paramref name="animate"/> seconds to glide, 0 to jump.</summary>
        public void Refresh(IReadOnlyList<Entry> entries, float animate)
        {
            if (tagsRoot == null) return;
            ShowTarget();

            // Drop tags for players who left.
            var alive = new HashSet<ulong>();
            foreach (var e in entries) alive.Add(e.Id);
            var gone = new List<ulong>();
            foreach (var kv in _tags) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone)
            {
                if (_tags[id].Root != null) Destroy(_tags[id].Root.gameObject);
                _tags.Remove(id);
            }

            // Lanes: left to right, a tag that would overlap the previous one in the top lane
            // drops underneath, if there's room there.
            var order = new List<Entry>(entries);
            order.Sort((a, b) => a.Score != b.Score ? a.Score.CompareTo(b.Score) : a.Id.CompareTo(b.Id));
            float lastUp = float.NegativeInfinity, lastDown = float.NegativeInfinity;
            var lanes = new Dictionary<ulong, bool>();
            foreach (var e in order)
            {
                float x = CentreOf(e.Score) - TagWidth * 0.5f;
                bool down = false;
                if (x - lastUp < TagWidth && x - lastDown >= TagWidth) down = true;
                if (down) lastDown = x; else lastUp = x;
                lanes[e.Id] = down;
            }

            int topScore = int.MinValue, leaders = 0;
            ulong leader = 0;
            foreach (var e in entries)
            {
                if (e.Score > topScore) { topScore = e.Score; leaders = 1; leader = e.Id; }
                else if (e.Score == topScore) leaders++;
            }

            Tag localTag = null;
            foreach (var e in entries)
            {
                if (!_tags.TryGetValue(e.Id, out var tag) || tag.Root == null)
                {
                    tag = BuildTag(e.Id);
                    _tags[e.Id] = tag;
                }
                tag.Mono.text = e.Mono;
                tag.Box.color = e.Local ? Palette.Gold : Palette.PaperHi;
                if (e.Local) localTag = tag;

                bool down = lanes[e.Id];
                tag.Down = down;
                tag.PointerDown.gameObject.SetActive(!down);
                tag.PointerUp.gameObject.SetActive(down);
                ((RectTransform)tag.Box.transform).At(0f, down ? 10f : 0f, TagWidth, 27f);
                tag.PointerDown.rectTransform.At(14f, 27f, 12f, 12f);
                tag.PointerUp.rectTransform.At(14f, 0f, 12f, 12f);

                float x = CentreOf(e.Score) - TagWidth * 0.5f;
                float top = down ? DownTop : UpTop;
                MoveTag(tag, x, top, animate);
            }
            if (localTag != null) localTag.Root.SetAsLastSibling(); // yours is never hidden

            // The leader's star, and the countdown sticker once they're close.
            bool clearLeader = leaders == 1 && topScore > 0 && _tags.ContainsKey(leader);
            if (star != null)
            {
                star.gameObject.SetActive(clearLeader);
                if (clearLeader)
                {
                    var t = _tags[leader];
                    float x = CentreOf(topScore) - TagWidth * 0.5f;
                    star.rectTransform.At(x + 27f, (t.Down ? DownTop + 10f : UpTop) - 10f, 16f, 16f);
                    star.transform.SetAsLastSibling();
                }
            }
            if (toGo != null)
            {
                var box = Ink.BoxOf(toGo);
                int left = Target - topScore;
                bool show = clearLeader && topScore >= Target - 15 && left > 0;
                box.gameObject.SetActive(show);
                if (show)
                {
                    toGo.text = $"{left} TO GO!";
                    float x = Mathf.Max(0f, CentreOf(topScore) - 20f - 64f);
                    box.Pin(x, 0f, new Vector2(0f, 1f));
                }
            }
        }

        /// <summary>After a round: gold bars for points gained, blue for points lost.</summary>
        public void ShowMoves(IReadOnlyList<(int before, int after)> moves)
        {
            ClearMoves();
            if (barsRoot == null) return;
            foreach (var (before, after) in moves)
            {
                if (before == after) continue;
                float a = CentreOf(before), b = CentreOf(after);
                if (Mathf.Approximately(a, b)) continue;
                var bar = Ink.Plain(barsRoot, "Move", after > before ? Palette.Gold : Palette.Blue);
                bar.rectTransform.At(Mathf.Min(a, b), BarTop, Mathf.Abs(b - a), 7f);
                _bars.Add(bar);
            }
        }

        public void ClearMoves()
        {
            foreach (var b in _bars) if (b != null) Destroy(b.gameObject);
            _bars.Clear();
        }

        /// <summary>The static parts: label, ruler, ticks, 0 and the target, the chequered flag.</summary>
        public static RaceTrack Create(Transform parent, float left, float top, GameConfig config)
        {
            int target = config != null ? Mathf.Max(1, config.targetScore) : 100;
            var root = Ink.Node(parent, "RaceTrack");
            root.At(left, top, 1080f, 110f);
            var race = root.gameObject.AddComponent<RaceTrack>();

            race.label = Ink.Label(root, "Label", $"THE RACE TO {target}", 11f);
            race.label.rectTransform.At(0f, 6f, 400f, 14f);
            Ink.Plain(root, "Ruler", Palette.Ink).rectTransform.At(20f, 68f, 1000f, 3f);
            for (int i = 0; i <= 10; i++)
            {
                float x = 20f + i * 100f - 1f;
                Ink.Plain(root, "Tick" + i, Palette.Ink).rectTransform.At(x, 71f, 2f, i % 5 == 0 ? 12f : 7f);
                if (i == 0 || i == 10)
                {
                    var n = Ink.Text(root, "Mark" + i, i == 0 ? "0" : target.ToString(), TypeRole.Sticker, 10f,
                        Palette.Ink, TextAlignmentOptions.Top).OneLine();
                    n.rectTransform.At(20f + i * 100f - 15f, 86f, 30f, 14f);
                    if (i == 10) race.endMark = n;
                }
            }
            var flag = Ink.Icon(root, "Finish", InkSprites.Checker, Color.white);
            flag.preserveAspect = false;
            flag.rectTransform.At(1022f, 28f, 22f, 40f);

            race.barsRoot = Ink.Node(root, "Moves");
            race.barsRoot.Fill();
            race.tagsRoot = Ink.Node(root, "Tags");
            race.tagsRoot.Fill();
            race.star = Ink.Icon(root, "Leader", InkSprites.Star, Palette.Gold);
            race.star.rectTransform.sizeDelta = new Vector2(16f, 16f);
            race.star.gameObject.SetActive(false);
            race.toGo = Ink.Chip(root, "ToGo", "13 TO GO!", TypeRole.Sticker, 13f, Palette.Ink, Palette.Gold, 0f,
                new RectOffset(9, 9, 3, 2), caps: true, tracking: 0.06f);
            Ink.BoxOf(race.toGo).Tilt(-4f);
            Ink.BoxOf(race.toGo).gameObject.SetActive(false);
            return race;
        }

        private Tag BuildTag(ulong id)
        {
            var root = Ink.Node(tagsRoot, "Tag" + id);
            root.At(0f, UpTop, TagWidth, 40f);
            var tag = new Tag { Root = root };
            tag.Box = Ink.Box(root, "Box", Palette.PaperHi, 2.5f);
            tag.Mono = Ink.Text(tag.Box.transform, "Mono", "?", TypeRole.Display, 17f, Palette.Ink,
                TextAlignmentOptions.Center, caps: true);
            tag.Mono.rectTransform.Fill();
            tag.Mono.textWrappingMode = TextWrappingModes.NoWrap;
            tag.PointerDown = Ink.Icon(root, "PointDown", InkSprites.TriDown, Palette.Ink);
            tag.PointerUp = Ink.Icon(root, "PointUp", InkSprites.TriUp, Palette.Ink);
            return tag;
        }

        private void MoveTag(Tag tag, float x, float top, float seconds)
        {
            var rt = tag.Root;
            float height = tag.Down ? 37f : 40f;
            bool sameSpot = !float.IsNaN(tag.X) && Mathf.Approximately(tag.X, x)
                            && Mathf.Approximately(rt.sizeDelta.y, height)
                            && Mathf.Approximately(-rt.anchoredPosition.y - height * 0.5f, top);
            if (sameSpot && tag.Move == null) return;
            if (tag.Move != null) { StopCoroutine(tag.Move); tag.Move = null; }

            bool jump = seconds <= 0f || float.IsNaN(tag.X) || !isActiveAndEnabled;
            if (jump)
            {
                rt.At(x, top, TagWidth, height);
                tag.X = x;
                return;
            }
            float fromX = tag.X;
            tag.X = x;
            rt.At(fromX, top, TagWidth, height);
            tag.Move = StartCoroutine(Glide(rt, fromX, x, top, height, seconds));
        }

        private static IEnumerator Glide(RectTransform rt, float from, float to, float top, float height, float seconds)
        {
            yield return Tween.To(seconds, Ease.OutCubic, t => rt.At(Mathf.LerpUnclamped(from, to, t), top, TagWidth, height));
        }
    }
}
