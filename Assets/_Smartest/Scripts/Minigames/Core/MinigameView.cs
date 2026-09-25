using System;
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

        /// <summary>Size of the drawing area. Games lay themselves out relative to this.</summary>
        protected Vector2 AreaSize
        {
            get
            {
                if (Area == null) return new Vector2(880f, 420f);
                var s = Area.rect.size;
                if (s.x < 50f || s.y < 50f) return new Vector2(880f, 420f);
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

        private void Update()
        {
            if (!Running) return;
            float dt = Time.unscaledDeltaTime;
            Elapsed += dt;
            OnTick(dt);
        }
    }
}
