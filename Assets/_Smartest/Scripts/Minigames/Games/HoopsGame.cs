using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Basketball with a slingshot: press on the ball, pull back, let go. A dotted guide shows
    /// the start of the arc (shorter every level), the ball rattles off the rim and the
    /// backboard, and from level three the hoop drifts. Sink enough before you run out of
    /// shots. The first drag-and-release game. Ranked by time.
    /// </summary>
    public class HoopsGame : MinigameView
    {
        private const float PhysicsStep = 1f / 240f;
        private const float BallR = 22f;
        private const float RimHalf = 62f;
        private const float RimEndR = 7f;
        private const float Gravity = 1300f;
        private const float MaxPull = 190f;
        private const float Power = 7.4f;
        private const float Limit = 16f;
        private const int GuideDots = 7;

        private RectTransform _ball;
        private Image _ballImage;
        private RectTransform _rim, _board, _netL, _netR, _netX, _netY, _netMid, _rimL, _rimR;
        private readonly List<RectTransform> _guide = new List<RectTransform>();
        private TMP_Text _label;
        private Vector2 _rest;
        private Vector2 _p, _v;
        private bool _aiming, _flying;
        private float _resetAt = -1f;
        private float _nextAuto = 0.8f;
        private float _demoFrom = -1f; // the demo: when it went for the ball
        private float _floorY, _halfW;
        private Vector2 _hoopBase;
        private float _hoopSwing, _hoopFreq;
        private float _guideTime;
        private int _need, _shots, _shotsLeft, _made;
        private float _acc;
        private float _spin;

        protected override void Build()
        {
            var size = AreaSize;
            switch (Level)
            {
                case 1: _need = 2; _shotsLeft = 5; _hoopSwing = 0f; _hoopFreq = 0f; _guideTime = 0.6f; break;
                case 2: _need = 2; _shotsLeft = 4; _hoopSwing = 0f; _hoopFreq = 0f; _guideTime = 0.45f; break;
                case 3: _need = 3; _shotsLeft = 5; _hoopSwing = 120f; _hoopFreq = 0.5f; _guideTime = 0.3f; break;
                case 4: _need = 3; _shotsLeft = 5; _hoopSwing = 160f; _hoopFreq = 0.8f; _guideTime = 0.2f; break;
                default:
                    _need = 3; _shotsLeft = 4; _hoopSwing = 180f;
                    _hoopFreq = Mathf.Min(1.4f, 1.0f + (Level - 5) * 0.05f);
                    _guideTime = 0.12f;
                    break;
            }

            _halfW = Mathf.Min(560f, size.x * 0.5f - 30f);
            float top = size.y * 0.5f - 10f;
            _floorY = -size.y * 0.5f + 70f; // the hint lives below
            UiKit.Box(Area, "Court", new Vector2(_halfW * 2f, top - _floorY + 16f), new Vector2(0f, (top + _floorY - 16f) * 0.5f), Palette.PanelRaised);
            UiKit.Fill(Area, "Floor", new Vector2(_halfW * 2f - 8f, 8f), new Vector2(0f, _floorY), Palette.Ink);

            _hoopBase = new Vector2(RandomRange(80f, 260f), RandomRange(0f, 90f));
            _board = UiKit.Line(Area, "Board", Vector2.zero, Vector2.up, 10f, Palette.Ink);
            _netL = UiKit.Line(Area, "NetL", Vector2.zero, Vector2.up, 3f, Palette.Ink2);
            _netR = UiKit.Line(Area, "NetR", Vector2.zero, Vector2.up, 3f, Palette.Ink2);
            _netX = UiKit.Line(Area, "NetX", Vector2.zero, Vector2.up, 2f, Palette.Ink2);
            _netY = UiKit.Line(Area, "NetY", Vector2.zero, Vector2.up, 2f, Palette.Ink2);
            _netMid = UiKit.Line(Area, "NetMid", Vector2.zero, Vector2.right, 2f, Palette.Ink2);
            _rim = UiKit.Line(Area, "Rim", Vector2.zero, Vector2.right, 6f, Palette.Red);
            _rimL = (RectTransform)UiKit.Dot(Area, "RimL", RimEndR * 2f, Vector2.zero, Palette.Red).transform;
            _rimR = (RectTransform)UiKit.Dot(Area, "RimR", RimEndR * 2f, Vector2.zero, Palette.Red).transform;
            PlaceHoop(0f);

            for (int i = 0; i < GuideDots; i++)
            {
                var dot = (RectTransform)UiKit.Dot(Area, "Guide" + i, 10f, Vector2.zero, Palette.Ink2).transform;
                dot.gameObject.SetActive(false);
                _guide.Add(dot);
            }
            _rest = new Vector2(-_halfW + 130f, _floorY + BallR + 4f);
            _p = _rest;
            _ballImage = UiKit.Art(Area, "Ball", "hoop_ball", new Vector2(BallR * 2f, BallR * 2f), _rest, Palette.Accent);
            _ball = (RectTransform)_ballImage.transform;

            _shots = _shotsLeft;
            Tell();
            _label = UiKit.Label(Area, "Hint", "PULL BACK AND LET GO", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        /// <summary>The scoreline: baskets made, and shots left to make them with.</summary>
        private void Tell()
        {
            Progress("IN", _made, _need);
            Tries("SHOTS", _shotsLeft, _shots);
        }

        private Vector2 HoopAt(float t) => _hoopBase + new Vector2(_hoopSwing * Mathf.Sin(_hoopFreq * t), 0f);

        private void PlaceHoop(float t)
        {
            var h = HoopAt(t);
            UiKit.SetLine(_rim, h + new Vector2(-RimHalf, 0f), h + new Vector2(RimHalf, 0f));
            _rimL.anchoredPosition = h + new Vector2(-RimHalf, 0f);
            _rimR.anchoredPosition = h + new Vector2(RimHalf, 0f);
            UiKit.SetLine(_board, h + new Vector2(RimHalf + 14f, -20f), h + new Vector2(RimHalf + 14f, 120f));
            UiKit.SetLine(_netL, h + new Vector2(-RimHalf, 0f), h + new Vector2(-RimHalf * 0.55f, -60f));
            UiKit.SetLine(_netR, h + new Vector2(RimHalf, 0f), h + new Vector2(RimHalf * 0.55f, -60f));
            // The mesh between them.
            UiKit.SetLine(_netX, h + new Vector2(-RimHalf, 0f), h + new Vector2(RimHalf * 0.55f, -60f));
            UiKit.SetLine(_netY, h + new Vector2(RimHalf, 0f), h + new Vector2(-RimHalf * 0.55f, -60f));
            UiKit.SetLine(_netMid, h + new Vector2(-RimHalf * 0.78f, -30f), h + new Vector2(RimHalf * 0.78f, -30f));
        }

        protected override void OnTick(float dt)
        {
            PlaceHoop(Elapsed);
            if (!CanAct && Interactive) return;
            if (CanAct && Elapsed > Limit) { Fail("TIME'S UP"); return; }

            // Between shots the ball comes back to the mark.
            if (_resetAt >= 0f)
            {
                if (Elapsed < _resetAt) return;
                _resetAt = -1f;
                _p = _rest;
                _ball.anchoredPosition = _p;
                _ballImage.color = Palette.Accent;
                if (_shotsLeft <= 0) { if (CanAct) Fail("OUT OF SHOTS"); return; }
            }

            if (!_flying)
            {
                if (Interactive) Aim();
                else if (Demo) DemoShot();
                else AutoShot();
                return;
            }
            Fly(dt);
        }

        /// <summary>For someone watching: a decent shooter who leads the moving hoop, and sometimes rims out.</summary>
        private void AutoShot()
        {
            if (_shotsLeft <= 0 || Elapsed < _nextAuto) return;
            const float flight = 0.95f;
            // Dropping in from a little above the rim, within a dozen pixels of the middle, clears
            // both rim ends; any wider and the front one knocks it out.
            var target = HoopAt(Elapsed + flight) + new Vector2(RandomRange(-12f, 12f), 10f);
            _v = new Vector2((target.x - _rest.x) / flight, (target.y - _rest.y) / flight + 0.5f * Gravity * flight);
            _p = _rest;
            _flying = true;
            _shotsLeft--;
            Tell();
            _nextAuto = Elapsed + 1.6f;
        }

        /// <summary>
        /// The rule card's demo takes that shot the way a player does: on to the ball, press, pull
        /// back along the shot for half a second while the ball and the guide show it, let go.
        /// Dead on the middle of the hoop, so level 1 goes in.
        /// </summary>
        private void DemoShot()
        {
            if (_shotsLeft <= 0) return;
            const float flight = 0.95f, press = 0.3f, pulled = 0.6f, letGo = 0.85f;
            if (_demoFrom < 0f) _demoFrom = Elapsed + (_shotsLeft == _shots ? 0.4f : 0f);
            float t = Elapsed - _demoFrom;
            if (t < press)
            {
                // A look at the court first, then on to the ball.
                PointAt(t < 0f ? _rest + new Vector2(160f, 120f) : _rest);
                return;
            }
            var target = HoopAt(Elapsed + Mathf.Max(0f, letGo - t) + flight) + new Vector2(0f, 10f);
            var v = new Vector2((target.x - _rest.x) / flight, (target.y - _rest.y) / flight + 0.5f * Gravity * flight);
            var pull = Vector2.ClampMagnitude(v / Power, MaxPull) * Mathf.SmoothStep(0f, 1f, (t - press) / (pulled - press));
            _ball.anchoredPosition = _rest - pull * 0.15f;
            for (int i = 0; i < _guide.Count; i++)
            {
                float u = _guideTime * (i + 1) / _guide.Count;
                _guide[i].anchoredPosition = _rest + pull * Power * u + 0.5f * Gravity * u * u * Vector2.down;
                _guide[i].gameObject.SetActive(pull.magnitude > 12f);
            }
            // The ball sits on the floor, so a full pull ends below the picture, where the hand
            // would be cut off: it goes all the way back sideways and only a little lower than
            // the ball. The guide shows where the shot really goes.
            var back = _rest - pull;
            back.y = Mathf.Max(back.y, _rest.y - 30f);
            HoldAt(back, t < letGo);
            if (t < letGo) return;

            // Let go.
            foreach (var g in _guide) g.gameObject.SetActive(false);
            _ball.anchoredPosition = _rest;
            _v = pull * Power;
            _p = _rest;
            _flying = true;
            _shotsLeft--;
            Tell();
            _demoFrom = -1f;
        }

        private void Aim()
        {
            if (!UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m)) return;
            if (!_aiming)
            {
                if (KeyInput.MousePressed() && Vector2.Distance(m, _rest) <= 90f) _aiming = true;
                return;
            }
            var pull = Vector2.ClampMagnitude(_rest - m, MaxPull);
            _ball.anchoredPosition = _rest - pull * 0.15f;
            var v = pull * Power;
            // The guide: the start of the arc, a little shorter every level.
            for (int i = 0; i < _guide.Count; i++)
            {
                float t = _guideTime * (i + 1) / _guide.Count;
                _guide[i].anchoredPosition = _rest + v * t + 0.5f * Gravity * t * t * Vector2.down;
                _guide[i].gameObject.SetActive(pull.magnitude > 12f);
            }
            if (KeyInput.MouseHeld()) return;

            // Let go: a real shot only if it was pulled back a little.
            _aiming = false;
            foreach (var g in _guide) g.gameObject.SetActive(false);
            _ball.anchoredPosition = _rest;
            if (pull.magnitude < 20f) return;
            _v = v;
            _p = _rest;
            _flying = true;
            _shotsLeft--;
            Tell();
        }

        private void Fly(float dt)
        {
            var hoop = HoopAt(Elapsed);
            var left = hoop + new Vector2(-RimHalf, 0f);
            var right = hoop + new Vector2(RimHalf, 0f);
            float boardX = hoop.x + RimHalf + 14f;
            bool scored = false, over = false;

            _acc += dt;
            while (_acc >= PhysicsStep && !scored && !over)
            {
                _acc -= PhysicsStep;
                var before = _p;
                _v.y -= Gravity * PhysicsStep;
                _p += _v * PhysicsStep;

                // The rim's two ends are solid: the ball rattles off them.
                Bounce(left);
                Bounce(right);
                // The backboard.
                if (_p.x + BallR > boardX - 5f && before.x + BallR <= boardX - 5f && _p.y > hoop.y - 20f - BallR && _p.y < hoop.y + 120f + BallR)
                {
                    _p.x = boardX - 5f - BallR;
                    _v.x = -Mathf.Abs(_v.x) * 0.6f;
                }
                // Through the hoop, going down, between the rim ends.
                if (before.y > hoop.y && _p.y <= hoop.y && _p.x > left.x + BallR * 0.5f && _p.x < right.x - BallR * 0.5f) scored = true;
                if (_p.y - BallR <= _floorY || _p.x > _halfW + BallR || _p.x < -_halfW - BallR) over = true;
            }
            _ball.anchoredPosition = _p;
            _spin -= _v.x * dt / BallR * Mathf.Rad2Deg; // it rolls through the air the way it's going
            _ball.localRotation = Quaternion.Euler(0f, 0f, _spin);

            if (scored)
            {
                _made++;
                Tell();
                _ballImage.color = Palette.Green;
                _flying = false;
                if (_made >= _need)
                {
                    _label.text = "NOTHING BUT NET";
                    _label.color = Palette.Green;
                    Finish(false, Ms(Elapsed));
                    return;
                }
                _resetAt = Elapsed + 0.45f;
            }
            else if (over)
            {
                _flying = false;
                _resetAt = Elapsed + 0.35f;
            }
        }

        private void Bounce(Vector2 post)
        {
            var n = _p - post;
            float d = n.magnitude;
            if (d >= BallR + RimEndR || d < 0.001f) return;
            n /= d;
            _p = post + n * (BallR + RimEndR);
            float into = Vector2.Dot(_v, n);
            if (into < 0f) _v -= (1f + 0.6f) * into * n;
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
