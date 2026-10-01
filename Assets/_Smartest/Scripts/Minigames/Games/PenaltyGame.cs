using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Penalties against a keeper who watches your aim. Move the mouse to aim, click to shoot.
    /// The keeper shuffles after wherever you were aiming a moment ago and dives when you
    /// shoot, so the trick is the feint: pull him one way, then shoot the other before he
    /// catches up. Corners are hard to save but easy to miss. Score three before your shots
    /// run out. Ranked by shots taken, then time.
    /// </summary>
    public class PenaltyGame : MinigameView
    {
        private const float GoalHalf = 290f;
        private const float GroundY = -110f;
        private const float BarY = 170f;
        private const float BallR = 20f;
        private const int Need = 3;
        private const int Shots = 5;
        private const float Limit = 16f;

        private float _delay, _shuffle, _dive, _reach, _flight;
        private readonly Queue<(float time, float x)> _trail = new Queue<(float, float)>();
        private float _keeperX, _keeperLean;
        private Vector2 _aim;
        private Vector2 _spot, _target;
        private bool _flying;
        private float _shotAt, _nextShotAt;
        private int _taken, _scored;
        private RectTransform _keeper, _ball, _cross;
        private Image _keeperBody;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            switch (Level)
            {
                // The warm-ups: a slow keeper, so a shot tucked inside a post can beat him without a feint.
                case 1: _delay = 0.45f; _shuffle = 300f; _dive = 380f; _reach = 90f; _flight = 0.42f; break;
                case 2: _delay = 0.40f; _shuffle = 360f; _dive = 460f; _reach = 96f; _flight = 0.40f; break;
                case 3: _delay = 0.35f; _shuffle = 420f; _dive = 620f; _reach = 108f; _flight = 0.38f; break;
                case 4: _delay = 0.30f; _shuffle = 480f; _dive = 680f; _reach = 110f; _flight = 0.36f; break;
                default:
                    _delay = Mathf.Max(0.2f, 0.28f - (Level - 5) * 0.01f);
                    _shuffle = Mathf.Min(640f, 520f + (Level - 5) * 20f);
                    _dive = Mathf.Min(900f, 720f + (Level - 5) * 25f);
                    _reach = 112f;
                    _flight = 0.34f;
                    break;
            }

            // The pitch, the goal and its net.
            float pitchBottom = -size.y * 0.5f + 62f; // the hint lives below
            UiKit.Box(Area, "Pitch", new Vector2(size.x - 40f, GroundY - pitchBottom), new Vector2(0f, (GroundY + pitchBottom) * 0.5f), Palette.Panel);
            UiKit.Fill(Area, "Net", new Vector2(GoalHalf * 2f, BarY - GroundY), new Vector2(0f, (BarY + GroundY) * 0.5f), Palette.PaperHi);
            for (float x = -GoalHalf + 40f; x < GoalHalf; x += 40f)
                UiKit.Line(Area, "NetV", new Vector2(x, GroundY), new Vector2(x, BarY), 2f, Palette.Paper2);
            for (float y = GroundY + 40f; y < BarY; y += 40f)
                UiKit.Line(Area, "NetH", new Vector2(-GoalHalf, y), new Vector2(GoalHalf, y), 2f, Palette.Paper2);
            UiKit.Line(Area, "PostL", new Vector2(-GoalHalf, GroundY), new Vector2(-GoalHalf, BarY + 7f), 14f, Palette.Ink);
            UiKit.Line(Area, "PostR", new Vector2(GoalHalf, GroundY), new Vector2(GoalHalf, BarY + 7f), 14f, Palette.Ink);
            UiKit.Line(Area, "Bar", new Vector2(-GoalHalf - 7f, BarY), new Vector2(GoalHalf + 7f, BarY), 14f, Palette.Ink);
            UiKit.Line(Area, "GoalLine", new Vector2(-size.x * 0.5f + 20f, GroundY), new Vector2(size.x * 0.5f - 20f, GroundY), 4f, Palette.Ink);

            // The keeper: pivots at the feet, leans into a dive.
            _keeper = UiKit.Node(Area, "Keeper", Vector2.zero, new Vector2(0f, GroundY));
            _keeperBody = UiKit.Box(_keeper, "Body", new Vector2(58f, 104f), new Vector2(0f, 60f), Palette.Blue);
            UiKit.Line(_keeper, "Arms", new Vector2(-_reach + 20f, 118f), new Vector2(_reach - 20f, 118f), 14f, Palette.Blue);
            UiKit.Dot(_keeper, "GloveL", 30f, new Vector2(-_reach + 20f, 118f), Palette.Accent);
            UiKit.Dot(_keeper, "GloveR", 30f, new Vector2(_reach - 20f, 118f), Palette.Accent);
            UiKit.Dot(_keeper, "Head", 36f, new Vector2(0f, 128f), Palette.PaperHi);

            _spot = new Vector2(0f, -size.y * 0.5f + 90f);
            UiKit.Dot(Area, "Spot", 14f, _spot, Palette.PaperHi);
            _ball = (RectTransform)UiKit.Dot(Area, "Ball", BallR * 2f, _spot, Palette.PaperHi).transform;
            UiKit.Dot(_ball, "Patch", 14f, Vector2.zero, Palette.Ink);
            _cross = (RectTransform)UiKit.Dot(Area, "Aim", 30f, new Vector2(0f, 30f), Palette.Accent).transform;
            UiKit.Dot(_cross, "Pip", 8f, Vector2.zero, Palette.Ink);
            _aim = new Vector2(0f, 30f);

            Tell();
            _label = UiKit.Label(Area, "Hint", "AIM, THEN CLICK TO SHOOT", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        /// <summary>The scoreline: goals scored, and shots left to score them with.</summary>
        private void Tell()
        {
            Progress("GOALS", _scored, Need);
            Tries("SHOTS", Shots - _taken, Shots);
        }

        /// <summary>For someone watching: drift to one side, then flick to the other and shoot.</summary>
        private Vector2 AutoAim(out bool shoot)
        {
            float into = Elapsed - _nextShotAt;
            float side = _taken % 2 == 0 ? 1f : -1f;
            shoot = into > 1.3f;
            return into < 1.1f ? new Vector2(-side * 200f, 20f) : new Vector2(side * 230f, 60f);
        }

        protected override void OnTick(float dt)
        {
            bool shoot = false;
            if (CanAct && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m))
            {
                _aim = new Vector2(Mathf.Clamp(m.x, -GoalHalf - 120f, GoalHalf + 120f), Mathf.Clamp(m.y, GroundY + 10f, BarY + 90f));
                shoot = KeyInput.MousePressed();
            }
            else if (!Interactive && !_flying && _taken < Shots) _aim = AutoAim(out shoot);
            _cross.anchoredPosition = _aim;

            // The keeper reads where you were aiming a moment ago.
            _trail.Enqueue((Elapsed, _aim.x));
            float read = _keeperX;
            while (_trail.Count > 0 && _trail.Peek().time <= Elapsed - _delay) read = _trail.Dequeue().x;
            if (!_flying)
            {
                _keeperX = Mathf.MoveTowards(_keeperX, Mathf.Clamp(read, -GoalHalf + 40f, GoalHalf - 40f), _shuffle * dt);
                _keeperLean = Mathf.MoveTowards(_keeperLean, 0f, 200f * dt);
            }

            if (_flying)
            {
                Fly(dt);
            }
            else if (shoot && Elapsed >= _nextShotAt && _taken < Shots)
            {
                _flying = true;
                _shotAt = Elapsed;
                _target = _aim;
                _taken++;
                Tell();
            }
            _keeper.anchoredPosition = new Vector2(_keeperX, GroundY);
            _keeper.localRotation = Quaternion.Euler(0f, 0f, _keeperLean);
            if (CanAct && !_flying && Elapsed > Limit)
            {
                _label.text = "TIME'S UP";
                _label.color = Palette.Red;
                Finish(true, WorstMetric);
            }
        }

        private void Fly(float dt)
        {
            float t = Mathf.Clamp01((Elapsed - _shotAt) / _flight);
            // Into the distance: the ball shrinks a little as it flies at the goal.
            var at = Vector2.Lerp(_spot, _target, t) + new Vector2(0f, Mathf.Sin(t * Mathf.PI) * 40f);
            _ball.anchoredPosition = at;
            _ball.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, t);

            // The dive: straight for the ball, as fast as he can go, leaning into it.
            float dir = Mathf.Sign(_target.x - _keeperX);
            _keeperX = Mathf.MoveTowards(_keeperX, _target.x, _dive * dt);
            _keeperLean = Mathf.MoveTowards(_keeperLean, -dir * Mathf.Min(65f, Mathf.Abs(_target.x - _keeperX) * 0.4f + 20f), 400f * dt);
            if (t < 1f) return;

            _flying = false;
            string result;
            bool wide = Mathf.Abs(_target.x) > GoalHalf - BallR * 0.5f || _target.y > BarY - BallR * 0.5f;
            // His reach is widest at chest height and shortest in the top corners.
            float height = Mathf.InverseLerp(GroundY, BarY, _target.y);
            float reach = _reach * (1f - 0.35f * Mathf.Max(0f, height - 0.55f) / 0.45f) + BallR;
            bool saved = !wide && Mathf.Abs(_target.x - _keeperX) <= reach;
            if (wide) result = "WIDE";
            else if (saved) result = "SAVED";
            else { result = "GOAL"; _scored++; }
            Tell();
            _nextShotAt = Elapsed + 0.7f;
            if (!CanAct)
            {
                ResetBall();
                return;
            }
            _label.text = result;
            _label.color = result == "GOAL" ? Palette.Green : Palette.Red;
            if (Interactive) Sounds.Play(result == "GOAL" ? Sounds.Kind.Good : Sounds.Kind.Bad);

            if (_scored >= Need)
            {
                _label.text = "HAT-TRICK";
                // Fewer shots first, then quicker.
                Finish(false, _taken * 100000 + Ms(Elapsed));
                return;
            }
            if (_scored + (Shots - _taken) < Need) { _label.text = "NOT ENOUGH GOALS"; Finish(true, WorstMetric); return; }
            ResetBall();
        }

        private void ResetBall()
        {
            _ball.anchoredPosition = _spot;
            _ball.localScale = Vector3.one;
        }
    }
}
