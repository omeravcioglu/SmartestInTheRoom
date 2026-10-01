using System;
using Smartest.Core;
using UnityEngine;

namespace Smartest.Minigames
{
    /// <summary>
    /// Base class for every minigame. A game only has to do three things: build its level
    /// from (level, rng), react to input while running, and call Finish once. Everything
    /// else — timing, elimination, ranking, scoring, spectating — is handled for it.
    ///
    /// Two rules keep minigames fair online:
    ///  - the level content comes only from the shared seed, so everyone plays the same one;
    ///  - timings are measured on the player's own machine, so ping never costs a reaction.
    /// </summary>
    public abstract class MinigameView : MonoBehaviour
    {
        /// <summary>(failed, metric) — raised exactly once per level.</summary>
        public event Action<bool, int> Finished;

        /// <summary>The rect to draw inside. Cleared for you before Build().</summary>
        protected RectTransform Area { get; private set; }
        /// <summary>Deterministic per (game, seed, level). Never use UnityEngine.Random.</summary>
        protected System.Random Rng { get; private set; }
        /// <summary>1-based difficulty. Level 1 must be easy enough that nobody fails by accident.</summary>
        protected int Level { get; private set; }
        /// <summary>Hard deadline for this level.</summary>
        protected float LevelSeconds { get; private set; }
        /// <summary>Seconds since the level started.</summary>
        protected float Elapsed { get; private set; }

        /// <summary>False for players already knocked out: they watch, their input does nothing.</summary>
        public bool Interactive { get; set; } = true;

        public bool Running { get; private set; }
        public bool IsDone { get; private set; }

        /// <summary>True only while this player may actually act.</summary>
        protected bool CanAct => Running && Interactive && !IsDone;

        /// <summary>Reported when the level times out. Override for higher-is-better games.</summary>
        public virtual int WorstMetric => 999999;

        public void Init(RectTransform area)
        {
            Area = area;
        }

        /// <summary>Build the level, paused. Called well before the level actually starts.</summary>
        public void Prepare(int level, System.Random rng, float levelSeconds, bool interactive)
        {
            Level = Mathf.Max(1, level);
            Rng = rng;
            LevelSeconds = levelSeconds;
            Interactive = interactive;
            Elapsed = 0f;
            Running = false;
            IsDone = false;
            Progressed = default;
            TriesLeft = default;
            DemoHand = default;
            _answerShownAt = -1f;
            if (Area != null) UiKit.Clear(Area);
            Build();
        }

        /// <summary>Everyone's clock starts here, at the same instant.</summary>
        public void Begin()
        {
            if (IsDone) return;
            Elapsed = 0f;
            Running = true;
            OnBegin();
        }

        /// <summary>Stop ticking without reporting anything (the level is over for everyone).</summary>
        public void Freeze()
        {
            Running = false;
        }

        /// <summary>Time is up and this player never finished.</summary>
        public void TimeOut()
        {
            if (!IsDone) Finish(true, WorstMetric);
        }

        public void Teardown()
        {
            Running = false;
            OnTeardown();
            if (Area != null) UiKit.Clear(Area);
        }

        // ---- for subclasses ----

        /// <summary>Create this level's content. Nothing may move yet.</summary>
        protected abstract void Build();

        protected virtual void OnBegin() { }
        protected virtual void OnTick(float dt) { }
        protected virtual void OnFinished(bool failed, int metric) { }
        protected virtual void OnTeardown() { }

        /// <summary>Call once when this player's level is over. Metric meaning is the game's own.</summary>
        protected void Finish(bool failed, int metric)
        {
            if (IsDone) return;
            IsDone = true;
            Running = false;
            OnFinished(failed, metric);
            if (Interactive) Finished?.Invoke(failed, metric);
        }

        /// <summary>Milliseconds, as an int, for the common "how fast were you" metric.</summary>
        protected int Ms(float seconds) => Mathf.RoundToInt(Mathf.Max(0f, seconds) * 1000f);

        // ---- the level's scoreline ----

        /// <summary>
        /// Something a level says about how it's going: "3 of 8 caught", "2 shots left", or how
        /// far along a run is. The stage shows it on the game panel; the rules never read it.
        /// </summary>
        public struct Reading
        {
            /// <summary>One or two words in capitals: "CAUGHT", "SHOTS". Empty means nothing to show.</summary>
            public string Word;
            public int Value;
            public int Total;
            /// <summary>0 to 1 for a run with nothing to count (a walk, a hold); negative for a count.</summary>
            public float Fill;

            public bool Shown => !string.IsNullOrEmpty(Word);
            public bool IsBar => Fill >= 0f;
        }

        /// <summary>How far through the level this player is.</summary>
        public Reading Progressed { get; private set; }
        /// <summary>Attempts this player has left: shots, strokes, darts.</summary>
        public Reading TriesLeft { get; private set; }
        /// <summary>Either reading changed. Nobody has to listen.</summary>
        public event Action ReadingChanged;

        /// <summary>"3 of 8": things done out of things to do.</summary>
        protected void Progress(string word, int done, int total)
        {
            var r = new Reading { Word = word, Value = Mathf.Clamp(done, 0, Mathf.Max(0, total)), Total = Mathf.Max(0, total), Fill = -1f };
            if (Same(Progressed, r)) return;
            Progressed = r;
            ReadingChanged?.Invoke();
        }

        /// <summary>How far along a run with nothing to count is, 0 to 1.</summary>
        protected void Progress(string word, float fill)
        {
            var r = new Reading { Word = word, Fill = Mathf.Clamp01(fill) };
            if (Same(Progressed, r)) return;
            Progressed = r;
            ReadingChanged?.Invoke();
        }

        /// <summary>Attempts left out of the attempts you get.</summary>
        protected void Tries(string word, int left, int total)
        {
            var r = new Reading { Word = word, Value = Mathf.Clamp(left, 0, Mathf.Max(0, total)), Total = Mathf.Max(0, total), Fill = -1f };
            if (Same(TriesLeft, r)) return;
            TriesLeft = r;
            ReadingChanged?.Invoke();
        }

        private static bool Same(Reading a, Reading b) =>
            a.Word == b.Word && a.Value == b.Value && a.Total == b.Total && Mathf.Abs(a.Fill - b.Fill) < 0.004f;

        // ---- the rule card's demo ----

        /// <summary>
        /// True while this level is the demo on the rule card: played by the game itself with
        /// its sounds off, showing its moves (a hand that points and clicks, keys that light up)
        /// so a player sees what to do before their own level starts. Set it before Prepare. A
        /// demo is never Interactive, and nothing it does reaches the host.
        ///
        /// Games play themselves in their not-Interactive path, which knocked-out players watch
        /// too. Moves that would give an answer away (a memory game's sequence, the odd one out)
        /// belong behind Demo, so someone watching the real level never sees it solved for them.
        /// </summary>
        public bool Demo { get; set; }

        /// <summary>
        /// The level the demo plays: 1, unless level 1 hides what the game is about (Stroop's
        /// never prints a word in the other colour). Read before Prepare.
        /// </summary>
        public virtual int DemoLevel => 1;

        /// <summary>The demo's hand: whether it's shown, where it is (in the play area's space), and whether its button is down.</summary>
        public struct Hand
        {
            public bool Shown;
            public Vector2 At;
            public bool Down;
        }

        public Hand DemoHand { get; private set; }
        /// <summary>The demo clicked here, in the play area's space.</summary>
        public event Action<Vector2> DemoTapped;
        /// <summary>The demo pressed a key, named as the controls name it: "SPACE", "1", "W", "A".</summary>
        public event Action<string> DemoKeyPressed;

        /// <summary>
        /// The level can take a move: the player's, or the demo's. For the methods a click or a
        /// key and the demo both go through (a cell's click, a throw). Reading the mouse or the
        /// keyboard still wants CanAct, so the demo never reacts to the real player's hands.
        /// </summary>
        protected bool CanMove => Running && !IsDone && (Interactive || Demo);

        /// <summary>Move the hand without clicking. Cheap; call it every tick while playing yourself.</summary>
        protected void PointAt(Vector2 at) => DemoHand = new Hand { Shown = true, At = at, Down = DemoHand.Down };

        /// <summary>Move the hand and hold its button down, or let go: hold-to-rise, hold-to-run, drag.</summary>
        protected void HoldAt(Vector2 at, bool down) => DemoHand = new Hand { Shown = true, At = at, Down = down };

        /// <summary>Click here: the hand jumps there and taps.</summary>
        protected void TapAt(Vector2 at)
        {
            DemoHand = new Hand { Shown = true, At = at, Down = false };
            DemoTapped?.Invoke(at);
        }

        protected void HideHand() => DemoHand = default;

        /// <summary>Press a key, by the name the controls use.</summary>
        protected void PressKey(string key) => DemoKeyPressed?.Invoke(key);

        private const float AnswerLag = 0.45f; // about as long as the rule card shows a key
        private float _answerShownAt = -1f;

        /// <summary>
        /// A key that answers a question, for a demo: shows the key now and says yes a moment
        /// later, when the game should take it. A game that takes it at once shows the next
        /// question at once, and the key pops up beside a question it doesn't answer. Call it
        /// every tick once the demo has made up its mind.
        /// </summary>
        protected bool DemoAnswer(string key)
        {
            if (_answerShownAt < 0f)
            {
                PressKey(key);
                _answerShownAt = Elapsed;
                return false;
            }
            if (Elapsed - _answerShownAt < AnswerLag) return false;
            _answerShownAt = -1f;
            return true;
        }

        /// <summary>Where a piece of the level is, in the play area's space: for pointing the hand at a cell or a card.</summary>
        protected Vector2 Where(Component c)
        {
            if (c == null || Area == null) return Vector2.zero;
            return (Vector2)Area.InverseTransformPoint(c.transform.position) - Area.rect.center;
        }

        /// <summary>Size of the drawing area. Games lay themselves out relative to this.</summary>
        protected Vector2 AreaSize
        {
            get
            {
                if (Area == null) return new Vector2(1200f, 520f);
                var s = Area.rect.size;
                if (s.x < 50f || s.y < 50f) return new Vector2(1200f, 520f);
                return s;
            }
        }

        /// <summary>Level value that grows with difficulty but stops at a ceiling.</summary>
        protected int Step(int start, int perLevel, int max)
            => Mathf.Min(max, start + (Level - 1) * perLevel);

        /// <summary>Level value that shrinks with difficulty but stops at a floor.</summary>
        protected float Ramp(float start, float perLevel, float min)
            => Mathf.Max(min, start - (Level - 1) * perLevel);

        protected float RandomRange(float min, float max) => LevelRng.Range(Rng, min, max);
        protected int RandomRange(int minInclusive, int maxExclusive) => LevelRng.Range(Rng, minInclusive, maxExclusive);

        // A demo's longest step. After a hitch (the card being built, a slow frame) a demo would
        // jump past its own cue in one step and fumble, a keeper would land on the ball; a demo
        // just runs slow for that frame instead.
        private const float DemoLongestStep = 0.05f;

        private void Update()
        {
            if (!Demo) { Advance(Time.unscaledDeltaTime); return; }
            Sounds.Hush(true);
            try { Advance(Mathf.Min(Time.unscaledDeltaTime, DemoLongestStep)); }
            finally { Sounds.Hush(false); }
        }

        /// <summary>One tick of the level. Update drives it in play; tests drive it with a fixed step.</summary>
        public void Advance(float dt)
        {
            if (!Running) return;
            Elapsed += dt;
            OnTick(dt);
        }
    }
}
