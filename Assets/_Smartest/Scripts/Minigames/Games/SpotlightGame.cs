using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// The lights are off and your cursor is a torch. Somewhere in the dark is a gold dot,
    /// among plain ones (and from level six some pale-gold fakes); find it and click it. The
    /// torch is a round UI mask that follows the cursor, with the lit scene held still
    /// behind it. Ranked by time; clicking a wrong one puts you out.
    /// </summary>
    public class SpotlightGame : MinigameView
    {
        private const float Limit = 10f;

        private RectTransform _torch;
        private RectTransform _world;
        private Rect _box;
        private Vector2 _target;
        private float _targetR;
        private readonly List<Vector2> _decoys = new List<Vector2>();
        private Image _targetImage;
        private TMP_Text _label;
        private Vector2 _wanderFreq;
        private Vector2 _demoAt;       // the demo's torch
        private int _demoLeg;          // which stretch of its sweep the demo is on
        private float _demoSeenAt;     // when the gold showed in the demo's light; negative until then
        private float _demoStill;      // how long the demo has been on it

        protected override void Build()
        {
            var size = AreaSize;
            float torchR;
            int decoys;
            bool fakes = Level >= 6;
            switch (Level)
            {
                case 1: torchR = 150f; decoys = 0; _targetR = 22f; break;
                case 2: torchR = 125f; decoys = 4; _targetR = 20f; break;
                case 3: torchR = 105f; decoys = 8; _targetR = 18f; break;
                case 4: torchR = 90f; decoys = 12; _targetR = 16f; break;
                default:
                    torchR = Mathf.Max(60f, 80f - (Level - 5) * 3f);
                    decoys = Mathf.Min(24, 16 + (Level - 5) * 2);
                    _targetR = 14f;
                    break;
            }

            float w = Mathf.Min(1040f, size.x - 140f), h = size.y - 100f;
            var centre = new Vector2(0f, 20f);
            UiKit.Box(Area, "Dark", new Vector2(w, h), centre, Palette.Ink);
            _box = new Rect(centre.x - w * 0.5f + 6f, centre.y - h * 0.5f + 6f, w - 12f, h - 12f);

            // The torch: a round mask that follows the cursor. The lit scene inside it is held
            // still by moving it the opposite way, so only the circle moves.
            _torch = UiKit.Node(Area, "Torch", Vector2.one * (torchR * 2f), centre);
            var disc = _torch.gameObject.AddComponent<Image>();
            disc.sprite = UiKit.Circle;
            disc.raycastTarget = false;
            _torch.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            _world = UiKit.Node(_torch, "Scene", Vector2.zero, -centre);
            UiKit.Fill(_world, "Lit", _box.size, _box.center, Palette.PanelRaised);

            var inner = new Rect(_box.xMin + 30f, _box.yMin + 30f, _box.width - 60f, _box.height - 60f);
            _target = new Vector2(RandomRange(inner.xMin, inner.xMax), RandomRange(inner.yMin, inner.yMax));
            for (int i = 0; i < decoys; i++)
            {
                Vector2 p;
                int tries = 0;
                do p = new Vector2(RandomRange(inner.xMin, inner.xMax), RandomRange(inner.yMin, inner.yMax));
                while (TooClose(p) && ++tries < 30);
                _decoys.Add(p);
                bool fake = fakes && i % 3 == 0;
                UiKit.Dot(_world, "Decoy" + i, _targetR * 2f, p, fake ? Palette.AccentDim : Palette.Neutral);
            }
            _targetImage = UiKit.Dot(_world, "Target", _targetR * 2f, _target, Palette.Accent);
            _wanderFreq = new Vector2(RandomRange(0.35f, 0.55f), RandomRange(0.5f, 0.8f));

            _label = UiKit.Label(Area, "Hint", "FIND THE GOLD", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            // The demo's torch starts where the torch does, in the middle.
            _demoAt = centre;
            _demoLeg = 0;
            _demoSeenAt = -1f;
            _demoStill = 0f;
        }

        private bool TooClose(Vector2 p)
        {
            float min = _targetR * 3f;
            if (Vector2.Distance(p, _target) < min) return true;
            foreach (var d in _decoys) if (Vector2.Distance(p, d) < min) return true;
            return false;
        }

        protected override void OnTick(float dt)
        {
            Vector2 at;
            if (CanAct && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var cursor)) at = cursor;
            else if (Demo) at = DemoSearch(dt); // the demo hunts for it with the torch, and clicks it
            else // someone watching sees the torch sweep about on its own
                at = _box.center + new Vector2(_box.width * 0.4f * Mathf.Sin(_wanderFreq.x * Elapsed), _box.height * 0.4f * Mathf.Sin(_wanderFreq.y * Elapsed));
            var p = new Vector2(Mathf.Clamp(at.x, _box.xMin, _box.xMax), Mathf.Clamp(at.y, _box.yMin, _box.yMax));
            _torch.anchoredPosition = p;
            _world.anchoredPosition = -p;

            if (!CanAct) return;
            if (Elapsed > Limit) { Fail("TOO SLOW"); return; }
            if (KeyInput.MousePressed()) Click(at);
        }

        /// <summary>
        /// The rule card's demo: a beat, then a sweep with the torch along the half the gold is in
        /// (a wrong guess first, then back across) until the gold shows in the light; a moment to
        /// see it, over to it, and click.
        /// </summary>
        private Vector2 DemoSearch(float dt)
        {
            if (_demoSeenAt < 0f && Vector2.Distance(_demoAt, _target) <= _torch.sizeDelta.x * 0.5f - _targetR) _demoSeenAt = Elapsed;
            if (Elapsed >= 0.3f)
            {
                if (_demoSeenAt < 0f && _demoLeg < 2)
                {
                    float side = _target.x >= _box.center.x ? 1f : -1f;
                    var to = new Vector2(_box.center.x + side * (_demoLeg == 0 ? -250f : 420f),
                                         _box.center.y + (_target.y >= _box.center.y ? 80f : -80f));
                    _demoAt = Vector2.MoveTowards(_demoAt, to, 450f * dt);
                    if (_demoAt == to) _demoLeg++;
                }
                else if (_demoSeenAt < 0f || Elapsed - _demoSeenAt >= 0.3f)
                {
                    _demoAt = Vector2.MoveTowards(_demoAt, _target, 500f * dt);
                    _demoStill = _demoAt == _target ? _demoStill + dt : 0f;
                    if (_demoStill >= 0.25f)
                    {
                        TapAt(_demoAt);
                        Click(_demoAt);
                        return _demoAt;
                    }
                }
            }
            PointAt(_demoAt);
            return _demoAt;
        }

        /// <summary>A click, the player's or the demo's: the gold one wins, a plain one puts you out.</summary>
        private void Click(Vector2 at)
        {
            if (!CanMove || !_box.Contains(at)) return;
            if (Vector2.Distance(at, _target) <= _targetR + 6f)
            {
                _targetImage.color = Palette.Green;
                _label.text = "FOUND IT";
                _label.color = Palette.Green;
                Finish(false, Ms(Elapsed));
                return;
            }
            foreach (var d in _decoys)
            {
                if (Vector2.Distance(at, d) > _targetR + 4f) continue;
                Fail("WRONG ONE");
                return;
            }
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            // Swing the torch onto the gold one, so it's plain where it was.
            _torch.anchoredPosition = _target;
            _world.anchoredPosition = -_target;
            Finish(true, WorstMetric);
        }
    }
}
