using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// You're the traffic cop at a crossroads with no lights. Cars drive in from the edges;
    /// click one to stop it, click again to wave it on. Cars queue behind a stopped car. Two
    /// cars meeting in the middle is a crash, and a car kept waiting too long loses its temper
    /// (the bar over it fills). Both put you out. Everyone gets the same traffic. Ranked by
    /// how much waiting you caused, so keep it flowing.
    /// </summary>
    public class TrafficGame : MinigameView
    {
        private const float PhysicsStep = 1f / 120f;
        private const float Length = 66f, Width = 36f;
        private const float Box = 50f;          // half the crossing
        private const float Lane = 25f;         // lane centre, from the road's middle
        private const float SafeGap = 14f;
        private const float RageShowsAt = 0.4f;

        private static readonly Vector2[] Dirs = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };

        private class Car
        {
            public int Dir;
            public float At;            // spawn time
            public float Cruise, Speed;
            public float Pos;           // distance travelled from the spawn point
            public bool Out, Held, Cleared;
            public float Wait;
            public RectTransform View, Brake, RageBar;
            public Image Body, Rage;
        }

        private readonly List<Car> _cars = new List<Car>();
        private RectTransform _roads;
        private Vector2 _crossing;
        private float _halfW, _halfH;           // the field, each way from the crossing
        private float _rageAt;
        private float _waitSum;
        private float _acc;
        private int _cleared;
        private Car _demoCar;                   // the car the demo's hand is going to click
        private float _demoSince, _demoClicked = -9f;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int count, dirs;
            float window, speed;
            switch (Level)
            {
                case 1: count = 6; dirs = 2; window = 6f; speed = 230f; _rageAt = 5f; break;
                case 2: count = 8; dirs = 2; window = 6.5f; speed = 250f; _rageAt = 4.5f; break;
                case 3: count = 10; dirs = 3; window = 7f; speed = 270f; _rageAt = 4f; break;
                case 4: count = 12; dirs = 3; window = 7.5f; speed = 290f; _rageAt = 3.5f; break;
                default:
                    count = Mathf.Min(18, 13 + (Level - 5));
                    dirs = 4;
                    window = 8f;
                    speed = Mathf.Min(360f, 300f + (Level - 5) * 8f);
                    _rageAt = Mathf.Max(2.5f, 3.2f - (Level - 5) * 0.1f);
                    break;
            }

            // Everything on the roads is drawn inside a mask, so cars slide in from the edges.
            float top = size.y * 0.5f - 10f, bottom = -size.y * 0.5f + 62f; // the hint lives below
            float halfW = Mathf.Min(580f, size.x * 0.5f - 20f);
            var field = Rect.MinMaxRect(-halfW, bottom, halfW, top);
            _halfW = field.width * 0.5f;
            _halfH = field.height * 0.5f;
            UiKit.Box(Area, "Field", field.size + new Vector2(8f, 8f), field.center, Palette.Panel);
            _crossing = field.center;
            _roads = UiKit.Node(Area, "Roads", field.size, field.center);
            _roads.gameObject.AddComponent<RectMask2D>();

            UiKit.Fill(_roads, "RoadH", new Vector2(field.width, Box * 2f), Vector2.zero, Palette.Ink2);
            UiKit.Fill(_roads, "RoadV", new Vector2(Box * 2f, field.height), Vector2.zero, Palette.Ink2);
            for (float x = Box + 20f; x < halfW; x += 60f)
            {
                UiKit.Fill(_roads, "Dash", new Vector2(28f, 4f), new Vector2(x, 0f), Palette.PaperHi);
                UiKit.Fill(_roads, "Dash", new Vector2(28f, 4f), new Vector2(-x, 0f), Palette.PaperHi);
            }
            for (float y = Box + 20f; y < field.height * 0.5f; y += 60f)
            {
                UiKit.Fill(_roads, "Dash", new Vector2(4f, 28f), new Vector2(0f, y), Palette.PaperHi);
                UiKit.Fill(_roads, "Dash", new Vector2(4f, 28f), new Vector2(0f, -y), Palette.PaperHi);
            }
            // Stop lines where each lane meets the crossing.
            UiKit.Fill(_roads, "StopE", new Vector2(4f, Box), new Vector2(-Box - 4f, -Lane), Palette.PaperHi);
            UiKit.Fill(_roads, "StopW", new Vector2(4f, Box), new Vector2(Box + 4f, Lane), Palette.PaperHi);
            UiKit.Fill(_roads, "StopN", new Vector2(Box, 4f), new Vector2(Lane, -Box - 4f), Palette.PaperHi);
            UiKit.Fill(_roads, "StopS", new Vector2(Box, 4f), new Vector2(-Lane, Box + 4f), Palette.PaperHi);

            // The traffic: spread over the window, never two cars on top of each other in a lane.
            var lastIn = new float[4] { -9f, -9f, -9f, -9f };
            var lastSpeed = new float[4];
            var colours = new[] { Palette.Accent, Palette.Blue, Palette.PaperHi };
            var times = new List<float>();
            for (int i = 0; i < count; i++) times.Add(RandomRange(0.3f, window));
            times.Sort();
            foreach (float time in times)
            {
                int dir = RandomRange(0, dirs); // east and north first, then west, then south
                var car = new Car { Dir = dir, Cruise = speed * RandomRange(0.85f, 1.15f) };
                float gapTime = (Length + 40f) / Mathf.Min(car.Cruise, lastSpeed[dir] > 0f ? lastSpeed[dir] : car.Cruise);
                car.At = Mathf.Max(time, lastIn[dir] + gapTime);
                lastIn[dir] = car.At;
                lastSpeed[dir] = car.Cruise;
                MakeCar(car, colours[RandomRange(0, colours.Length)]);
                _cars.Add(car);
            }
            foreach (var c in _cars) Draw(c);

            _label = UiKit.Label(Area, "Hint", "CLICK A CAR: STOP / GO", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
        }

        private static bool Horizontal(int dir) => dir == 0 || dir == 2;

        private void MakeCar(Car car, Color colour)
        {
            bool h = Horizontal(car.Dir);
            var size = h ? new Vector2(Length, Width) : new Vector2(Width, Length);
            car.View = UiKit.Node(_roads, "Car", size, Vector2.zero);
            var fwd = Dirs[car.Dir];
            // Drawn nose up, then turned to face the way it drives.
            car.Body = UiKit.Art(car.View, "Body", "car", new Vector2(Width, Length), Vector2.zero, colour);
            car.Body.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg - 90f);
            car.Brake = (RectTransform)UiKit.Dot(car.View, "Brake", 14f, -fwd * (Length * 0.5f - 6f), Palette.Red).transform;
            car.Brake.gameObject.SetActive(false);
            car.Rage = UiKit.Fill(car.View, "Rage", new Vector2(56f, 7f), new Vector2(0f, (h ? Width : Length) * 0.5f + 10f), Palette.Accent);
            car.RageBar = (RectTransform)car.Rage.transform;
            car.RageBar.gameObject.SetActive(false);
        }

        /// <summary>How far out a car starts: a car's length past the edge, hidden by the mask.</summary>
        private float SpawnOf(Car c) => (Horizontal(c.Dir) ? _halfW : _halfH) + Length;

        /// <summary>The car's nose is on the field (only then can it be seen, clicked or get cross).</summary>
        private bool OnField(Car c) => c.Pos + Length * 0.5f > Length;

        /// <summary>Where a car's middle is, in the roads' space (the crossing is the origin).</summary>
        private Vector2 Centre(Car c)
        {
            var d = Dirs[c.Dir];
            var start = -d * SpawnOf(c) + new Vector2(-d.y, d.x) * -Lane; // keep to the right-hand lane
            return start + d * c.Pos;
        }

        private Rect RectOf(Car c)
        {
            var size = Horizontal(c.Dir) ? new Vector2(Length, Width) : new Vector2(Width, Length);
            return new Rect(Centre(c) - size * 0.5f, size);
        }

        /// <summary>How far the car's front still is from the crossing (negative once it's in).</summary>
        private float ToCrossing(Car c) => SpawnOf(c) - Box - c.Pos - Length * 0.5f;

        private bool PastCrossing(Car c) => c.Pos - Length * 0.5f > SpawnOf(c) + Box;

        private void Draw(Car c)
        {
            bool live = !c.Out && Elapsed >= c.At;
            c.View.gameObject.SetActive(live);
            if (!live) return;
            c.View.anchoredPosition = Centre(c);
            c.Brake.gameObject.SetActive(c.Held);
            bool angry = c.Wait > RageShowsAt;
            c.RageBar.gameObject.SetActive(angry);
            if (!angry) return;
            float t = Mathf.Clamp01(c.Wait / _rageAt);
            c.RageBar.sizeDelta = new Vector2(56f * t, 7f);
            c.Rage.color = t > 0.66f ? Palette.Red : Palette.Accent;
        }

        protected override void OnTick(float dt)
        {
            if (CanAct && KeyInput.MousePressed() && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m)) Click(m - _crossing);
            else if (Demo) DemoCop();
            else if (!Interactive) AutoCop();

            bool crashed = false, raged = false;
            _acc += dt;
            while (_acc >= PhysicsStep && !crashed && !raged)
            {
                _acc -= PhysicsStep;
                Simulate(PhysicsStep, Elapsed - _acc, out crashed, out raged);
            }
            foreach (var c in _cars) Draw(c);

            if (!CanMove) return;
            Progress("THROUGH", _cleared, _cars.Count);
            if (crashed) { Fail("CRASH!"); return; }
            if (raged) { Fail("ROAD RAGE"); return; }
            if (_cleared >= _cars.Count)
            {
                _label.text = "ALL THROUGH";
                _label.color = Palette.Green;
                Finish(false, Ms(_waitSum));
            }
        }

        private void Click(Vector2 p)
        {
            if (!CanMove) return;
            Car best = null;
            float bestD = float.MaxValue;
            foreach (var c in _cars)
            {
                if (c.Out || Elapsed < c.At || !OnField(c)) continue;
                var r = RectOf(c);
                r.min -= Vector2.one * 12f;
                r.max += Vector2.one * 12f;
                if (!r.Contains(p)) continue;
                float d = Vector2.Distance(p, Centre(c));
                if (d < bestD) { best = c; bestD = d; }
            }
            if (best != null) best.Held = !best.Held;
        }

        private void Simulate(float h, float now, out bool crashed, out bool raged)
        {
            crashed = raged = false;
            foreach (var c in _cars)
            {
                if (c.Out || now < c.At) continue;
                // The car ahead in the same lane sets a limit: slow down to keep a gap, never touch.
                float ahead = float.MaxValue;
                foreach (var o in _cars)
                {
                    if (o == c || o.Dir != c.Dir || o.Out || now < o.At || o.Pos <= c.Pos) continue;
                    ahead = Mathf.Min(ahead, o.Pos);
                }
                float want = c.Held ? 0f : c.Cruise;
                if (ahead < float.MaxValue) want = Mathf.Min(want, Mathf.Max(0f, (ahead - c.Pos - Length - SafeGap) * 5f));
                c.Speed = Mathf.MoveTowards(c.Speed, want, (want < c.Speed ? 2600f : 900f) * h);
                c.Pos += c.Speed * h;
                if (ahead < float.MaxValue) c.Pos = Mathf.Min(c.Pos, ahead - Length - 2f);

                // Waiting only counts once the car is on the field (a queue can back up past the edge).
                bool stuck = c.Speed < c.Cruise * 0.25f && OnField(c);
                if (stuck) { c.Wait += h; _waitSum += h; }
                else if (c.Speed > c.Cruise * 0.6f) c.Wait = 0f;
                if (c.Wait >= _rageAt && Interactive) { c.Body.color = Palette.Red; raged = true; }

                if (!c.Cleared && PastCrossing(c)) { c.Cleared = true; _cleared++; }
                if (c.Pos > SpawnOf(c) * 2f) c.Out = true;
            }
            if (!Interactive) return;
            // Crashes: only cars on crossing roads can meet.
            for (int i = 0; i < _cars.Count; i++)
            {
                var a = _cars[i];
                if (a.Out || now < a.At) continue;
                for (int j = i + 1; j < _cars.Count; j++)
                {
                    var b = _cars[j];
                    if (b.Out || now < b.At || Horizontal(a.Dir) == Horizontal(b.Dir)) continue;
                    var ra = RectOf(a);
                    var rb = RectOf(b);
                    if (ra.xMin + 3f < rb.xMax && rb.xMin + 3f < ra.xMax && ra.yMin + 3f < rb.yMax && rb.yMin + 3f < ra.yMax)
                    {
                        a.Body.color = Palette.Red;
                        b.Body.color = Palette.Red;
                        crashed = true;
                    }
                }
            }
        }

        /// <summary>For someone watching: whoever is nearer the crossing goes, the other waits.</summary>
        private void AutoCop()
        {
            foreach (var c in _cars)
            {
                if (c.Out || Elapsed < c.At || c.Cleared) { if (c.Held) c.Held = false; continue; }
                float mine = ToCrossing(c);
                if (mine < 0f || mine > 110f) { c.Held = false; continue; }
                bool yield = false;
                foreach (var o in _cars)
                {
                    if (o.Out || Elapsed < o.At || o.Cleared || Horizontal(o.Dir) == Horizontal(c.Dir)) continue;
                    float theirs = ToCrossing(o);
                    if (theirs < 0f || (theirs < 110f && (theirs < mine || (theirs == mine && !Horizontal(o.Dir))))) { yield = true; break; }
                }
                c.Held = yield;
            }
        }

        /// <summary>
        /// The rule card's demo directs the traffic by hand, a click at a time: the watcher's calls
        /// (whoever is nearer the crossing goes, the other waits), made further out so the hand
        /// has time to get there. A car that needs stopping comes before one waiting to go, and
        /// a car with another ahead of it in its lane is left to queue behind that one.
        /// </summary>
        private void DemoCop()
        {
            // Stops in order of how near the crossing they are, then wave-ons, longest kept first.
            float Order(Car c) => c.Held ? 1000f - c.Wait : ToCrossing(c);
            bool Due(Car c) => c != null && !c.Out && Elapsed >= c.At && OnField(c) && c.Held != DemoWaits(c);

            Car next = null;
            foreach (var c in _cars)
                if (Due(c) && (next == null || Order(c) < Order(next))) next = c;
            // The hand stays with its car unless another needs the click well before it.
            if (!Due(_demoCar) || (next != null && Order(next) < Order(_demoCar) - 40f))
            {
                if (_demoCar != next) _demoSince = Elapsed;
                _demoCar = next;
            }

            if (_demoCar == null)
            {
                if (!DemoHand.Shown) PointAt(_crossing + new Vector2(120f, 100f)); // waiting beside the crossing
                return;
            }
            var at = Centre(_demoCar);
            PointAt(_crossing + at);
            if (Elapsed - _demoSince < 0.25f || Elapsed - _demoClicked < 0.35f) return;
            TapAt(_crossing + at);
            Click(at);
            _demoClicked = Elapsed;
            _demoCar = null;
        }

        /// <summary>The demo's call for one car: should it be stopped now?</summary>
        private bool DemoWaits(Car c)
        {
            const float look = 200f;
            if (c.Out || Elapsed < c.At || c.Cleared) return false;
            float mine = ToCrossing(c);
            if (mine < 0f || mine > look) return false;
            // One with a car ahead of it in its lane will queue behind that one.
            foreach (var o in _cars)
                if (o != c && o.Dir == c.Dir && !o.Out && Elapsed >= o.At && !o.Cleared && o.Pos > c.Pos) return false;
            foreach (var o in _cars)
            {
                if (o.Out || Elapsed < o.At || o.Cleared || Horizontal(o.Dir) == Horizontal(c.Dir)) continue;
                float theirs = ToCrossing(o);
                if (theirs < 0f || (theirs < look && (theirs < mine || (theirs == mine && !Horizontal(o.Dir))))) return true;
            }
            return false;
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
