using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The phone game everyone has played on a bus: a block slides, you drop it, whatever
    /// hangs over the edge falls off, and the next block is only as wide as what stayed.
    /// Ranked by how much you trimmed away in total.
    /// </summary>
    public class StackGame : MinigameView
    {
        private const float BlockH = 30f;
        private const float PerBlock = 3f;
        private const float FallSpeed = 700f;

        private readonly List<RectTransform> _falling = new List<RectTransform>();
        private float[] _offsets = new float[0];
        private float _baseY;
        private float _topX;
        private float _topW;
        private float _speed;
        private float _range;
        private int _count;
        private int _placed;
        private float _blockStart;
        private RectTransform _moving;
        private float _cut;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            switch (Level)
            {
                case 1: _count = 4; _speed = 260f; _topW = 290f; break;
                case 2: _count = 5; _speed = 330f; _topW = 280f; break;
                case 3: _count = 6; _speed = 400f; _topW = 260f; break;
                case 4: _count = 7; _speed = 470f; _topW = 240f; break;
                default:
                    // Timing a moving block is good to about a fiftieth of a second; any faster
                    // and each drop loses more than the tower has to give.
                    _count = Mathf.Min(9, 8 + (Level - 5) / 3);
                    _speed = Mathf.Min(650f, 530f + (Level - 5) * 30f);
                    _topW = 220f;
                    break;
            }
            _range = Mathf.Min(430f, size.x * 0.5f - 160f);
            _baseY = -size.y * 0.5f + 40f;
            _topX = 0f;

            // Where along its sweep each block starts: from the left or from the right.
            _offsets = new float[_count];
            for (int i = 0; i < _count; i++) _offsets[i] = RandomRange(0, 2) == 0 ? 0f : 2f * _range;

            UiKit.Box(Area, "Base", new Vector2(_topW, BlockH), new Vector2(0f, _baseY), Palette.Ink);
            _label = UiKit.Label(Area, "Hint", "CLICK TO DROP", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, size.y * 0.5f - 30f));
            SpawnBlock();
        }

        private void SpawnBlock()
        {
            float y = _baseY + (_placed + 1) * BlockH;
            _moving = (RectTransform)UiKit.Box(Area, "Block" + _placed, new Vector2(_topW, BlockH),
                new Vector2(-_range, y), Palette.Accent).transform;
            _blockStart = Elapsed;
        }

        protected override void OnTick(float dt)
        {
            for (int i = _falling.Count - 1; i >= 0; i--)
            {
                var f = _falling[i];
                f.anchoredPosition += Vector2.down * (FallSpeed * dt);
                if (f.anchoredPosition.y < -AreaSize.y) { Destroy(f.gameObject); _falling.RemoveAt(i); }
            }
            if (_moving == null) return;

            float s = (Elapsed - _blockStart) * _speed + _offsets[_placed];
            float x = Mathf.PingPong(s, 2f * _range) - _range;
            _moving.anchoredPosition = new Vector2(x, _moving.anchoredPosition.y);

            // Someone already out watches a tidy tower build itself.
            bool drop = CanAct ? KeyInput.MousePressed() : !Interactive && Mathf.Abs(x - _topX) <= _speed * dt;
            if (!drop)
            {
                if (CanAct && Elapsed - _blockStart > PerBlock) Fail("TOO SLOW");
                return;
            }
            Drop(x);
        }

        private void Drop(float x)
        {
            float y = _moving.anchoredPosition.y;
            float left = Mathf.Max(x - _topW * 0.5f, _topX - _topW * 0.5f);
            float right = Mathf.Min(x + _topW * 0.5f, _topX + _topW * 0.5f);
            float overlap = right - left;
            if (overlap <= 0f)
            {
                _moving.GetComponent<Image>().color = Palette.Red;
                _moving = null;
                Fail("MISSED THE TOWER");
                return;
            }

            // Whatever hangs over the edge breaks off and falls (not off the last block: the
            // level ends there and it would hang in the air).
            float cut = _topW - overlap;
            _cut += cut;
            if (cut >= 1f && _placed + 1 < _count)
            {
                float cx = x < _topX ? left - cut * 0.5f : right + cut * 0.5f;
                _falling.Add((RectTransform)UiKit.Box(Area, "Offcut", new Vector2(cut, BlockH),
                    new Vector2(cx, y), Palette.Red).transform);
            }

            _moving.sizeDelta = new Vector2(overlap, BlockH);
            _moving.anchoredPosition = new Vector2((left + right) * 0.5f, y);
            _topX = (left + right) * 0.5f;
            _topW = overlap;
            _moving = null;
            _placed++;

            if (_placed >= _count)
            {
                if (_label != null) { _label.text = "STACKED"; _label.color = Palette.Green; }
                // Tenths of a pixel: two careful players would tie on whole ones.
                Finish(false, Mathf.RoundToInt(_cut * 10f));
                return;
            }
            SpawnBlock();
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
