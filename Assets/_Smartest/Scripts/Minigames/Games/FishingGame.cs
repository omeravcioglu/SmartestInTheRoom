using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// A line hangs in the water and the hook follows your mouse. Gold fish swim past at
    /// different depths: put the hook on one and click to reel it in. Red pufferfish swim
    /// too, and if one touches the hook you're stung and out, so you hunt the gold and weave
    /// round the red at once. Same fish for everyone. Ranked by time to land the catch.
    /// </summary>
    public class FishingGame : MinigameView
    {
        private const float Limit = 13f;
        private const float FishR = 24f;
        private const float HookR = 10f;
        private const float ReelTime = 0.35f;

        private struct Fish
        {
            public float Y, Start, Speed;
            public bool Right;      // swims left to right
            public bool Puffer;
            public bool Caught;
            public float CaughtAt;
            public float CaughtX;
            public bool Gone;
            public RectTransform View;
            public Image Image;
        }

        private readonly List<Fish> _fish = new List<Fish>();
        private RectTransform _line;
        private RectTransform _hook;
        /// <summary>Where the line ties on, from the point the hook catches with.</summary>
        private static readonly Vector2 Eye = new Vector2(5.5f, 23f);
        private Vector2 _hookAt;
        private float _surfaceY, _bedY, _halfW;
        private int _need, _caught;
        private float _onFor; // the demo: how long the hook has been on a gold fish
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int golds, puffers;
            float minSpeed, maxSpeed, spacing;
            switch (Level)
            {
                case 1: _need = 3; golds = 7; puffers = 0; minSpeed = 120f; maxSpeed = 160f; spacing = 0.7f; break;
                case 2: _need = 3; golds = 7; puffers = 3; minSpeed = 140f; maxSpeed = 190f; spacing = 0.6f; break;
                case 3: _need = 4; golds = 8; puffers = 5; minSpeed = 160f; maxSpeed = 220f; spacing = 0.55f; break;
                case 4: _need = 4; golds = 8; puffers = 7; minSpeed = 180f; maxSpeed = 250f; spacing = 0.5f; break;
                default:
                    _need = 5; golds = 10; puffers = Mathf.Min(12, 8 + (Level - 5));
                    minSpeed = Mathf.Min(280f, 200f + (Level - 5) * 10f); maxSpeed = minSpeed + 80f;
                    spacing = Mathf.Max(0.3f, 0.4f - (Level - 5) * 0.02f);
                    break;
            }

            _halfW = Mathf.Min(540f, size.x * 0.5f - 40f);
            _surfaceY = size.y * 0.5f - 40f;
            _bedY = -size.y * 0.5f + 70f; // the hint lives below
            var water = UiKit.Box(Area, "Water", new Vector2(_halfW * 2f, _surfaceY - _bedY), new Vector2(0f, (_surfaceY + _bedY) * 0.5f),
                Color.Lerp(Palette.PaperHi, Palette.Blue, 0.18f));
            // Fish swim in from past the edges and out again.
            water.gameObject.AddComponent<RectMask2D>().padding = new Vector4(4f, 4f, 4f, 4f);
            var world = UiKit.Node(water.transform, "World", Vector2.zero, new Vector2(0f, -(_surfaceY + _bedY) * 0.5f));

            // The schedule: every fish gets a depth, a side, a speed. Golds and puffers mixed.
            int total = golds + puffers;
            var puffer = new bool[total];
            for (int k = 0; k < puffers; k++)
            {
                int i;
                int tries = 0;
                do i = RandomRange(1, total); while (puffer[i] && ++tries < 20);
                puffer[i] = true;
            }
            float top = _surfaceY - FishR - 14f, bottom = _bedY + FishR + 10f;
            for (int i = 0; i < total; i++)
            {
                var f = new Fish
                {
                    Y = RandomRange(bottom, top),
                    Start = 0.4f + i * spacing,
                    Speed = RandomRange(minSpeed, maxSpeed),
                    Right = RandomRange(0, 2) == 0,
                    Puffer = puffer[i]
                };
                // Drawn the size of what touches the hook: a fish is wide and flat, a puffer round.
                var img = f.Puffer
                    ? UiKit.Art(world, "Puffer" + i, "puffer", new Vector2(FishR, FishR) * (60f / 22f), new Vector2(-9999f, f.Y), Palette.Red)
                    : UiKit.Art(world, "Fish" + i, "fish", new Vector2(FishR * 2.7f, FishR * 1.6f), new Vector2(-9999f, f.Y), Palette.Accent);
                f.View = (RectTransform)img.transform;
                f.Image = img;
                if (!f.Right) f.View.localScale = new Vector3(-1f, 1f, 1f); // the art faces right
                _fish.Add(f);
            }

            _hookAt = new Vector2(0f, (top + bottom) * 0.5f);
            _line = UiKit.Line(world, "Line", new Vector2(0f, _surfaceY + 30f), _hookAt, 3f, Palette.Ink);
            var hook = UiKit.Art(world, "Hook", "hook", new Vector2(22f, 32f), _hookAt);
            UiKit.PivotOn(hook, "hook", new Vector2(7.5f, 24f));
            _hook = hook.rectTransform;

            Progress("CAUGHT", 0, _need);
            _label = UiKit.Label(Area, "Hint", "HOOK THE GOLD, CLICK TO REEL", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        private float FishX(in Fish f, float t)
        {
            float from = f.Right ? -_halfW - FishR * 2f : _halfW + FishR * 2f;
            return from + (f.Right ? 1f : -1f) * f.Speed * (t - f.Start);
        }

        protected override void OnTick(float dt)
        {
            // The hook follows the mouse; someone watching sees it go after the next gold.
            float top = _surfaceY - HookR - 6f, bottom = _bedY + HookR + 6f, side = _halfW - HookR - 6f;
            if (CanAct && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m))
                _hookAt = new Vector2(Mathf.Clamp(m.x, -side, side), Mathf.Clamp(m.y, bottom, top));
            else if (!Interactive)
                foreach (var f in _fish)
                    if (!f.Puffer && !f.Caught && !f.Gone && Elapsed >= f.Start && Mathf.Abs(FishX(f, Elapsed)) < side)
                    {
                        _hookAt = Vector2.MoveTowards(_hookAt, new Vector2(FishX(f, Elapsed), f.Y), 500f * dt);
                        break;
                    }
            _hook.anchoredPosition = _hookAt;
            UiKit.SetLine(_line, new Vector2(_hookAt.x + Eye.x, _surfaceY + 30f), _hookAt + Eye);
            if (Demo) PointAt(_hookAt); // the hook goes where the mouse is: the demo's hand is on it

            int onHook = -1;
            for (int i = 0; i < _fish.Count; i++)
            {
                var f = _fish[i];
                if (f.Gone || Elapsed < f.Start) continue;
                if (f.Caught)
                {
                    // Reeled up to the surface, then gone.
                    float u = (Elapsed - f.CaughtAt) / ReelTime;
                    f.View.anchoredPosition = new Vector2(f.CaughtX, Mathf.Lerp(f.Y, _surfaceY + 40f, u));
                    // Hauled up by the mouth.
                    f.View.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, f.Right ? 70f : -70f, Mathf.Clamp01(u * 3f)));
                    if (u >= 1f) { f.Gone = true; f.View.gameObject.SetActive(false); }
                    _fish[i] = f;
                    continue;
                }
                float x = FishX(f, Elapsed);
                f.View.anchoredPosition = new Vector2(x, f.Y);
                if (Mathf.Abs(x) > _halfW + FishR * 3f && (f.Right ? x > 0f : x < 0f)) { f.Gone = true; _fish[i] = f; continue; }

                // Gold fish are drawn stretched (wide and flat); puffers are round, and only
                // what you can see of a puffer can sting.
                bool touching = f.Puffer
                    ? Vector2.Distance(new Vector2(x, f.Y), _hookAt) < FishR + HookR - 2f
                    : Mathf.Abs(x - _hookAt.x) < FishR * 1.2f + HookR && Mathf.Abs(f.Y - _hookAt.y) < FishR * 0.8f + HookR;
                if (touching && f.Puffer && CanAct)
                {
                    f.Image.color = Palette.Ink;
                    Fail("STUNG BY A PUFFERFISH");
                    return;
                }
                if (touching && !f.Puffer) onHook = i;
            }

            // The rule card's demo lets the hook settle on the fish a moment, then clicks.
            if (Demo) _onFor = onHook >= 0 ? _onFor + dt : 0f;
            bool reel = CanAct ? KeyInput.MousePressed() : (!Interactive && onHook >= 0 && (!Demo || _onFor >= 0.25f));
            if (reel && onHook >= 0)
            {
                if (Demo)
                {
                    TapAt(_hookAt);
                    _onFor = -0.15f; // and a beat longer before the next click
                }
                var f = _fish[onHook];
                f.Caught = true;
                f.CaughtAt = Elapsed;
                f.CaughtX = _hookAt.x;
                f.Y = _hookAt.y;
                f.Image.color = Palette.Green;
                _fish[onHook] = f;
                _caught++;
                Progress("CAUGHT", _caught, _need);
                if (_caught >= _need && CanMove)
                {
                    _label.text = "WHAT A CATCH";
                    _label.color = Palette.Green;
                    Finish(false, Ms(Elapsed));
                    return;
                }
            }
            if (CanAct && Elapsed > Limit) Fail("THEY GOT AWAY");
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
