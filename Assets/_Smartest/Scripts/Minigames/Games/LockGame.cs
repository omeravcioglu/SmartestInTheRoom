using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Pop the lock. A needle sweeps round the dial of a padlock; click (or SPACE) while it's
    /// over the gold notch. Every hit flips the needle's direction and puts the next notch
    /// somewhere ahead of it. Click on nothing, or let the needle sail past a notch, and you're
    /// out. Everyone gets the same run of notches. Ranked by how close to the middle of each
    /// notch you clicked.
    /// </summary>
    public class LockGame : MinigameView
    {
        private const float R = 124f;          // the middle of the dial's ring
        private const float Ring = 34f;
        private const int ArcPieces = 6;

        private readonly List<float> _offsets = new List<float>();  // how far ahead each notch sits
        private readonly List<RectTransform> _arc = new List<RectTransform>();
        private Vector2 _centre;
        private float _speed, _half;
        private float _angle = 90f;            // the needle, degrees, 90 = top
        private int _dir = -1;                 // -1 clockwise, +1 anticlockwise
        private float _notch;                  // the notch's angle
        private float _toGo;                   // degrees the needle still has to travel to the notch's middle
        private int _hits;
        private float _errorSum;
        private RectTransform _needle, _shackle;
        private TMP_Text _left, _label;

        protected override void Build()
        {
            var size = AreaSize;
            int need;
            float minOff, maxOff;
            switch (Level)
            {
                case 1: need = 4; _speed = 120f; _half = 16f; minOff = 70f; maxOff = 190f; break;
                case 2: need = 5; _speed = 140f; _half = 14f; minOff = 60f; maxOff = 200f; break;
                case 3: need = 6; _speed = 165f; _half = 12f; minOff = 50f; maxOff = 220f; break;
                case 4: need = 7; _speed = 185f; _half = 11f; minOff = 45f; maxOff = 240f; break;
                default:
                    need = Mathf.Min(10, 8 + (Level - 5) / 2);
                    _speed = Mathf.Min(300f, 205f + (Level - 5) * 12f);
                    _half = Mathf.Max(7f, 10f - (Level - 5) * 0.5f);
                    minOff = 40f; maxOff = 260f;
                    break;
            }
            for (int i = 0; i < need; i++) _offsets.Add(RandomRange(minOff, maxOff));

            // The padlock: a shackle over a gold body with the dial in it.
            _centre = new Vector2(0f, -28f);
            _shackle = UiKit.Node(Area, "Shackle", Vector2.zero, _centre + new Vector2(0f, 150f));
            float sr = 78f;
            UiKit.Line(_shackle, "LegL", new Vector2(-sr, -40f), new Vector2(-sr, 22f), 26f, Palette.Ink2);
            UiKit.Line(_shackle, "LegR", new Vector2(sr, -40f), new Vector2(sr, 22f), 26f, Palette.Ink2);
            for (int i = 0; i < 8; i++)
            {
                float a0 = Mathf.PI * i / 8f, a1 = Mathf.PI * (i + 1) / 8f;
                var p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * sr + new Vector2(0f, 22f);
                var p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * sr + new Vector2(0f, 22f);
                var d = (p1 - p0).normalized * 6f;
                UiKit.Line(_shackle, "Arc" + i, p0 - d, p1 + d, 26f, Palette.Ink2);
            }
            UiKit.Box(Area, "Body", new Vector2(340f, 326f), _centre, Palette.Accent);
            UiKit.Dot(Area, "Ring", (R + Ring * 0.5f) * 2f, _centre, Palette.Ink);
            UiKit.Dot(Area, "Face", (R - Ring * 0.5f) * 2f, _centre, Palette.PaperHi);

            // The notch: a gold band along the ring, drawn in short pieces.
            for (int i = 0; i < ArcPieces; i++) _arc.Add(UiKit.Line(Area, "Notch" + i, Vector2.zero, Vector2.right, Ring - 8f, Palette.Accent));
            _needle = UiKit.Line(Area, "Needle", Vector2.zero, Vector2.right, 9f, Palette.Red);

            _left = UiKit.Label(Area, "Left", need.ToString(), 72f, Palette.Ink, new Vector2(160f, 100f), _centre);
            _label = UiKit.Label(Area, "Hint", "CLICK ON THE GOLD", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));

            PlaceNotch();
            DrawNeedle();
        }

        private Vector2 OnRing(float degrees, float radius) =>
            _centre + new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * radius;

        private void PlaceNotch()
        {
            _toGo = _offsets[_hits];
            _notch = _angle + _dir * _toGo;
            for (int i = 0; i < _arc.Count; i++)
            {
                float a0 = _notch - _half + 2f * _half * i / _arc.Count;
                float a1 = _notch - _half + 2f * _half * (i + 1) / _arc.Count;
                // Pieces overlap a touch so the band has no seams.
                UiKit.SetLine(_arc[i], OnRing(a0 - 0.6f, R), OnRing(a1 + 0.6f, R));
            }
        }

        private void DrawNeedle() => UiKit.SetLine(_needle, OnRing(_angle, R - Ring * 0.5f - 6f), OnRing(_angle, R + Ring * 0.5f + 6f));

        protected override void OnTick(float dt)
        {
            if (_hits >= _offsets.Count) return;
            float step = _speed * dt;
            _angle += _dir * step;
            _toGo -= step;
            DrawNeedle();

            bool click;
            if (!Interactive) click = _toGo <= 0.8f; // someone watching sees a sure hand
            else if (!CanAct) return;
            else click = KeyInput.MousePressed() || KeyInput.SpacePressed();

            if (click)
            {
                if (Mathf.Abs(_toGo) > _half) { Fail(_toGo > 0f ? "TOO EARLY" : "TOO LATE"); return; }
                _errorSum += Mathf.Abs(_toGo);
                _hits++;
                if (Interactive) Sounds.Play(Sounds.Kind.Click);
                _left.text = (_offsets.Count - _hits).ToString();
                if (_hits >= _offsets.Count)
                {
                    foreach (var piece in _arc) piece.gameObject.SetActive(false);
                    _shackle.anchoredPosition += new Vector2(0f, 24f); // pop
                    if (!CanAct) return;
                    _label.text = "UNLOCKED";
                    _label.color = Palette.Green;
                    // Hundredths of a degree off the middle, summed.
                    Finish(false, Mathf.RoundToInt(_errorSum * 100f));
                    return;
                }
                _dir = -_dir;
                PlaceNotch();
                return;
            }
            if (CanAct && _toGo < -_half) Fail("MISSED IT");
        }

        private void Fail(string why)
        {
            _needle.GetComponent<Image>().color = Palette.Ink;
            foreach (var piece in _arc) piece.GetComponent<Image>().color = Palette.Red;
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
