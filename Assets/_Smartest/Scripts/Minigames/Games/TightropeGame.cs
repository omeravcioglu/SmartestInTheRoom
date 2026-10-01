using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// A tightrope walker who is always about to fall. They walk on their own (the rope scrolls
    /// past); you hold the balance pole. Slide it with the mouse against the lean to keep them
    /// upright. Gusts of wind come now and then, and the streaks in the sky show which way a
    /// moment before they hit. Everyone gets the same gusts. Fall and you're out; everyone who
    /// makes it across is ranked by how much they wobbled.
    /// </summary>
    public class TightropeGame : MinigameView
    {
        private const float PhysicsStep = 1f / 240f;
        private const float Reach = 120f;                  // how far the pole slides each way
        private const float Push = 12f;                    // its pull at full slide, rad/s²
        private const float Damping = 1.8f;
        private const float FallAt = 45f * Mathf.Deg2Rad;
        private const float Warn = 0.8f;
        private const float GustFor = 0.9f;
        private const float Speed = 110f;                  // walking pace: the rope scrolls this fast
        private const float PoleY = 82f;
        private const float KnotGap = 100f;
        private const int Knots = 13;
        private const int StreakCount = 7;

        private float _instability, _gustPower, _swayPower, _walkFor;
        private readonly List<float> _gustAt = new List<float>();
        private readonly List<int> _gustDir = new List<int>();
        private float _f1, _f2, _p1, _p2;
        private float _theta, _omega, _shift;
        private bool _steadying; // the demo: partway through a correction
        private float _simTime, _acc, _leanSum;
        private float _ropeY, _halfW;
        private RectTransform _walker, _pole, _armL, _armR, _startPost, _endPost;
        private Image _head;
        private readonly List<RectTransform> _knots = new List<RectTransform>();
        private readonly List<RectTransform> _streaks = new List<RectTransform>();
        private readonly List<float> _streakPhase = new List<float>();
        private TMP_Text _gustLabel, _label;

        protected override void Build()
        {
            var size = AreaSize;
            int gusts;
            switch (Level)
            {
                // Tuned against a simulated player with a quarter-second reaction: a sloppy one gets
                // through level 1 about nine times in ten; by level 8 only a steady hand does.
                case 1: _instability = 1.2f; gusts = 1; _gustPower = 1.0f; _swayPower = 0.15f; _walkFor = 6f; break;
                case 2: _instability = 1.5f; gusts = 1; _gustPower = 1.4f; _swayPower = 0.2f; _walkFor = 6.5f; break;
                case 3: _instability = 1.8f; gusts = 2; _gustPower = 1.8f; _swayPower = 0.25f; _walkFor = 7f; break;
                case 4: _instability = 2.1f; gusts = 2; _gustPower = 2.2f; _swayPower = 0.3f; _walkFor = 7.5f; break;
                default:
                    _instability = Mathf.Min(3.8f, 2.4f + (Level - 5) * 0.12f);
                    gusts = Level >= 8 ? 4 : 3;
                    _gustPower = Mathf.Min(4.6f, 2.6f + (Level - 5) * 0.2f);
                    _swayPower = 0.35f;
                    _walkFor = 8f;
                    break;
            }
            // Gusts spread along the walk, none in the first second and a half.
            float span = (_walkFor - 1.5f - GustFor) / gusts;
            for (int i = 0; i < gusts; i++)
            {
                _gustAt.Add(1.5f + span * i + RandomRange(0f, Mathf.Max(0f, span - GustFor - 0.3f)));
                _gustDir.Add(RandomRange(0, 2) == 0 ? -1 : 1);
            }
            _f1 = RandomRange(1.1f, 1.6f); _f2 = RandomRange(2.4f, 3.2f);
            _p1 = RandomRange(0f, 6.3f); _p2 = RandomRange(0f, 6.3f);
            _theta = RandomRange(0, 2) == 0 ? -0.03f : 0.03f; // it starts tipping one way or the other

            _halfW = Mathf.Min(580f, size.x * 0.5f - 20f);
            _ropeY = -110f;

            // Poles at each end of the rope: the start scrolls away, the finish scrolls in.
            _startPost = Post("Start");
            _endPost = Post("Finish");
            UiKit.Line(Area, "Rope", new Vector2(-_halfW, _ropeY), new Vector2(_halfW, _ropeY), 5f, Palette.Ink);
            for (int i = 0; i < Knots; i++)
                _knots.Add((RectTransform)UiKit.Dot(Area, "Knot" + i, 12f, new Vector2(0f, _ropeY), Palette.Ink2).transform);

            // Wind streaks in the sky, only while a gust is coming or blowing.
            for (int i = 0; i < StreakCount; i++)
            {
                float y = 20f + i * 30f + RandomRange(-8f, 8f);
                var s = UiKit.Line(Area, "Streak" + i, new Vector2(0f, y), new Vector2(90f + RandomRange(0f, 60f), y), 3f, Palette.Ink2);
                s.gameObject.SetActive(false);
                _streaks.Add(s);
                _streakPhase.Add(RandomRange(0f, 1300f));
            }
            _gustLabel = UiKit.Label(Area, "Gust", "", 44f, Palette.Ink, new Vector2(320f, 60f), new Vector2(0f, 160f));

            // The walker, pivoting at the feet, with the pole in their hands.
            _walker = UiKit.Node(Area, "Walker", Vector2.zero, new Vector2(0f, _ropeY));
            UiKit.Line(_walker, "LegL", new Vector2(-12f, 0f), new Vector2(0f, 48f), 7f, Palette.Ink);
            UiKit.Line(_walker, "LegR", new Vector2(12f, 0f), new Vector2(0f, 48f), 7f, Palette.Ink);
            UiKit.Line(_walker, "Body", new Vector2(0f, 46f), new Vector2(0f, 104f), 9f, Palette.Ink);
            _armL = UiKit.Line(_walker, "ArmL", new Vector2(0f, 96f), new Vector2(-34f, PoleY), 6f, Palette.Ink);
            _armR = UiKit.Line(_walker, "ArmR", new Vector2(0f, 96f), new Vector2(34f, PoleY), 6f, Palette.Ink);
            _head = UiKit.Dot(_walker, "Head", 34f, new Vector2(0f, 124f), Palette.Accent);
            _pole = UiKit.Node(_walker, "Pole", Vector2.zero, new Vector2(0f, PoleY));
            UiKit.Line(_pole, "Bar", new Vector2(-150f, 0f), new Vector2(150f, 0f), 6f, Palette.Ink);
            UiKit.Dot(_pole, "WeightL", 24f, new Vector2(-150f, 0f), Palette.Blue);
            UiKit.Dot(_pole, "WeightR", 24f, new Vector2(150f, 0f), Palette.Blue);

            _label = UiKit.Label(Area, "Hint", "SLIDE THE POLE AGAINST THE LEAN", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            Draw();
        }

        private RectTransform Post(string name)
        {
            var root = UiKit.Node(Area, name, Vector2.zero, new Vector2(0f, _ropeY));
            UiKit.Line(root, "Pole", new Vector2(0f, 0f), new Vector2(0f, -100f), 12f, Palette.Ink);
            UiKit.Box(root, "Deck", new Vector2(80f, 16f), new Vector2(0f, -8f), Palette.PaperHi);
            return root;
        }

        private float Wind(float t)
        {
            for (int i = 0; i < _gustAt.Count; i++)
            {
                float into = t - _gustAt[i];
                if (into >= 0f && into <= GustFor) return _gustDir[i] * _gustPower * Mathf.Sin(Mathf.PI * into / GustFor);
            }
            return 0f;
        }

        /// <summary>The gust blowing now, or the one about to: its direction, 0 for calm.</summary>
        private int GustShowing(float t)
        {
            for (int i = 0; i < _gustAt.Count; i++)
                if (t >= _gustAt[i] - Warn && t <= _gustAt[i] + GustFor) return _gustDir[i];
            return 0;
        }

        /// <summary>For someone watching: a steady hand that knows the walker but not the wind.</summary>
        private float AutoPole()
        {
            float want = (-12f * _theta - 5f * _omega - _instability * Mathf.Sin(_theta)) / Push;
            return Mathf.Clamp(want, -1f, 1f) * Reach;
        }

        /// <summary>
        /// The rule card's demo balances the way a player does: it lets the walker tip a little,
        /// slides the pole firmly against the lean until they're upright, then lets it back to the
        /// middle. The watcher's hand above never lets them lean enough to see what the pole is for.
        /// </summary>
        private float DemoPole()
        {
            if (Mathf.Abs(_theta) > 3f * Mathf.Deg2Rad) _steadying = true;
            else if (Mathf.Abs(_theta) < 1f * Mathf.Deg2Rad && Mathf.Abs(_omega) < 0.06f) _steadying = false;
            return _steadying ? Mathf.Clamp(-(4f * _theta + 1.5f * _omega), -1f, 1f) * Reach : 0f;
        }

        protected override void OnTick(float dt)
        {
            float target;
            if (CanAct && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m)) target = Mathf.Clamp(m.x, -Reach, Reach);
            else if (Demo) target = DemoPole();
            else if (!Interactive) target = AutoPole();
            else target = _shift;

            bool fell = false;
            _acc += dt;
            while (_acc >= PhysicsStep && _simTime < _walkFor)
            {
                _acc -= PhysicsStep;
                _shift += (target - _shift) * (1f - Mathf.Exp(-18f * PhysicsStep));
                float sway = _swayPower * (Mathf.Sin(_f1 * _simTime + _p1) + 0.6f * Mathf.Sin(_f2 * _simTime + _p2));
                // Gravity tips them further the more they lean; the pole's weight pulls them back.
                float alpha = _instability * Mathf.Sin(_theta) + Push * (_shift / Reach) + Wind(_simTime) + sway - Damping * _omega;
                _omega += alpha * PhysicsStep;
                _theta += _omega * PhysicsStep;
                _simTime += PhysicsStep;
                _leanSum += Mathf.Abs(_theta) * PhysicsStep;
                if (Mathf.Abs(_theta) <= FallAt) continue;
                if (!Interactive) { _theta = Mathf.Sign(_theta) * FallAt; _omega = 0f; continue; }
                fell = true;
                break;
            }
            Draw();
            // The rule card's demo: only the mouse's x moves the pole, so the hand slides with it, over the walker's head.
            if (Demo) PointAt(new Vector2(_shift, _ropeY + 230f));

            if (!CanMove) return;
            if (fell)
            {
                _walker.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Sign(_theta) * 80f);
                _head.color = Palette.Red;
                _label.text = "FELL OFF";
                _label.color = Palette.Red;
                Finish(true, WorstMetric);
                return;
            }
            if (_simTime >= _walkFor)
            {
                _label.text = "MADE IT ACROSS";
                _label.color = Palette.Green;
                // Average lean in hundredths of a degree: steadier is better.
                Finish(false, Mathf.RoundToInt(_leanSum / _walkFor * Mathf.Rad2Deg * 100f));
            }
        }

        private void Draw()
        {
            _walker.localRotation = Quaternion.Euler(0f, 0f, -_theta * Mathf.Rad2Deg);
            _pole.anchoredPosition = new Vector2(_shift, PoleY);
            UiKit.SetLine(_armL, new Vector2(0f, 96f), new Vector2(_shift - 34f, PoleY));
            UiKit.SetLine(_armR, new Vector2(0f, 96f), new Vector2(_shift + 34f, PoleY));

            float walked = Mathf.Min(_simTime, _walkFor) * Speed;
            float loop = Knots * KnotGap;
            for (int i = 0; i < _knots.Count; i++)
            {
                float x = Mathf.Repeat(i * KnotGap - walked, loop) - loop * 0.5f;
                _knots[i].anchoredPosition = new Vector2(x, _ropeY);
                _knots[i].gameObject.SetActive(Mathf.Abs(x) < _halfW - 6f);
            }
            PlacePost(_startPost, -140f - walked);
            PlacePost(_endPost, _walkFor * Speed + 40f - walked);
            Progress("ACROSS", walked / (_walkFor * Speed));

            int dir = GustShowing(_simTime);
            _gustLabel.text = dir > 0 ? "GUST >>" : dir < 0 ? "<< GUST" : "";
            for (int i = 0; i < _streaks.Count; i++)
            {
                _streaks[i].gameObject.SetActive(dir != 0);
                if (dir == 0) continue;
                float x = Mathf.Repeat(_streakPhase[i] + dir * _simTime * 900f, 1300f) - 650f;
                var p = _streaks[i].anchoredPosition;
                _streaks[i].anchoredPosition = new Vector2(x, p.y);
                if (Mathf.Abs(x) > _halfW - 60f) _streaks[i].gameObject.SetActive(false);
            }
        }

        private void PlacePost(RectTransform post, float x)
        {
            post.anchoredPosition = new Vector2(x, _ropeY);
            post.gameObject.SetActive(Mathf.Abs(x) < _halfW - 40f);
        }
    }
}
