using System;
using System.Collections.Generic;

namespace Smartest.Minigames
{
    /// <summary>Is a small metric good (a time, an error) or a big one (a bank, a margin)?</summary>
    public enum MetricOrder
    {
        LowerIsBetter = 0,
        HigherIsBetter = 1
    }

    /// <summary>What one player did on one level. Metric meaning is the game's business.</summary>
    public struct LevelReport
    {
        public ulong ClientId;
        public bool Failed;
        public int Metric;

        public LevelReport(ulong clientId, bool failed, int metric)
        {
            ClientId = clientId; Failed = failed; Metric = metric;
        }
    }

    /// <summary>What the ladder decided after a level.</summary>
    public sealed class LadderStep
    {
        /// <summary>The level that was just played.</summary>
        public int Level;
        /// <summary>Knocked out by this level, best first.</summary>
        public List<ulong> Eliminated = new List<ulong>();
        /// <summary>Everyone alive failed: nobody is out, the same players play again, harder.</summary>
        public bool RepeatHarder;
        /// <summary>A dead heat decided who goes out: only these players play the next level.</summary>
        public bool TieBreak;
        public List<ulong> TiedPlayers = new List<ulong>();
        /// <summary>The minigame is over.</summary>
        public bool Finished;
        /// <summary>Valid when Finished: index 0 is the winner.</summary>
        public List<ulong> Ranking = new List<ulong>();
        /// <summary>Valid when Finished: 1-based place per player. Dead heats share a place.</summary>
        public Dictionary<ulong, int> Places = new Dictionary<ulong, int>();
    }

    /// <summary>
    /// The rules every minigame shares, with no Unity and no networking in sight so it can
    /// be unit-tested exhaustively:
    ///
    ///  - fail a level and you are out of this minigame (you keep your score);
    ///  - if nobody fails, the worst player by the level's own metric goes out, so every
    ///    level removes someone and the game always ends;
    ///  - if everyone fails, nobody goes out and the same players replay one notch harder;
    ///  - a dead heat for the elimination spot is broken by an extra level between exactly
    ///    those players — never by a coin flip;
    ///  - the last player standing is 1st; everyone else is ranked by how long they lasted.
    /// </summary>
    public sealed class EliminationLadder
    {
        private struct Entry
        {
            public ulong Id;
            public int Metric;
            public bool Failed;
            /// <summary>Elimination batch index; int.MaxValue for players who never went out.</summary>
            public int Group;
        }

        private readonly List<ulong> _alive = new List<ulong>();
        private readonly List<ulong> _participants = new List<ulong>();
        private readonly List<List<Entry>> _batches = new List<List<Entry>>();
        private readonly Dictionary<ulong, int> _lastMetric = new Dictionary<ulong, int>();

        public MetricOrder Order { get; }
        public int MaxLevels { get; }
        /// <summary>1-based; also the difficulty handed to the game's level generator.</summary>
        public int Level { get; private set; } = 1;
        public bool InTieBreak { get; private set; }
        public bool Finished { get; private set; }

        /// <summary>Still in the minigame.</summary>
        public IReadOnlyList<ulong> Alive => _alive;
        /// <summary>Who plays the next level — everyone alive, or just the tied players.</summary>
        public IReadOnlyList<ulong> Participants => _participants;

        public EliminationLadder(IEnumerable<ulong> players, MetricOrder order, int maxLevels = 20)
        {
            Order = order;
            MaxLevels = Math.Max(1, maxLevels);
            if (players != null)
                foreach (var p in players) if (!_alive.Contains(p)) _alive.Add(p);
            _participants.AddRange(_alive);
            Finished = _alive.Count <= 1;
        }

        /// <summary>True when a is a better result than b.</summary>
        private bool Better(int a, int b) => Order == MetricOrder.LowerIsBetter ? a < b : a > b;

        private int WorstPossible => Order == MetricOrder.LowerIsBetter ? int.MaxValue : int.MinValue;

        /// <summary>A player left the game. If that leaves one player, they win.</summary>
        public void Remove(ulong clientId)
        {
            _alive.Remove(clientId);
            _participants.Remove(clientId);
            if (_participants.Count == 0) { _participants.Clear(); _participants.AddRange(_alive); InTieBreak = false; }
            if (_alive.Count <= 1) Finished = true;
        }

        public LadderStep Submit(IReadOnlyList<LevelReport> reports)
        {
            var step = new LadderStep { Level = Level };
            if (Finished)
            {
                FinishInto(step);
                return step;
            }

            // A player who never reported ran out of time: that's a fail.
            var res = new List<Entry>(_participants.Count);
            for (int i = 0; i < _participants.Count; i++)
            {
                ulong id = _participants[i];
                bool found = false;
                if (reports != null)
                {
                    for (int j = 0; j < reports.Count; j++)
                    {
                        if (reports[j].ClientId != id) continue;
                        res.Add(new Entry { Id = id, Metric = reports[j].Metric, Failed = reports[j].Failed });
                        found = true;
                        break;
                    }
                }
                if (!found) res.Add(new Entry { Id = id, Metric = WorstPossible, Failed = true });
                _lastMetric[id] = res[res.Count - 1].Metric;
            }

            int survivors = 0;
            for (int i = 0; i < res.Count; i++) if (!res[i].Failed) survivors++;

            List<Entry> outNow = null;

            if (res.Count == 0)
            {
                step.RepeatHarder = true;
            }
            else if (survivors == 0)
            {
                // Everyone failed — nobody leaves, the level comes back harder.
                step.RepeatHarder = true;
            }
            else if (survivors == res.Count)
            {
                // Nobody failed, so the level has to pick someone: the worst result goes.
                int worst = res[0].Metric;
                for (int i = 1; i < res.Count; i++) if (Better(worst, res[i].Metric)) worst = res[i].Metric;
                var tied = res.FindAll(e => e.Metric == worst);
                if (tied.Count == res.Count) step.RepeatHarder = true;
                else if (tied.Count > 1)
                {
                    step.TieBreak = true;
                    step.TiedPlayers = tied.ConvertAll(e => e.Id);
                }
                else outNow = tied;
            }
            else
            {
                outNow = res.FindAll(e => e.Failed);
            }

            if (outNow != null && outNow.Count > 0)
            {
                int group = _batches.Count;
                for (int i = 0; i < outNow.Count; i++)
                {
                    var e = outNow[i];
                    e.Group = group;
                    outNow[i] = e;
                    _alive.Remove(e.Id);
                    step.Eliminated.Add(e.Id);
                }
                outNow.Sort(CompareBetterFirst);
                step.Eliminated = outNow.ConvertAll(e => e.Id);
                _batches.Add(outNow);
                InTieBreak = false;
                _participants.Clear();
                _participants.AddRange(_alive);
            }
            else if (step.TieBreak)
            {
                InTieBreak = true;
                _participants.Clear();
                _participants.AddRange(step.TiedPlayers);
            }
            // RepeatHarder: the same participants play again.

            Level++;
            if (_alive.Count <= 1 || Level > MaxLevels)
            {
                Finished = true;
                FinishInto(step);
            }
            return step;
        }

        private void FinishInto(LadderStep step)
        {
            step.Finished = true;
            var ordered = Ordered();
            step.Ranking = ordered.ConvertAll(e => e.Id);
            step.Places = PlacesFrom(ordered);
        }

        private int CompareBetterFirst(Entry a, Entry b)
        {
            if (a.Failed != b.Failed) return a.Failed ? 1 : -1;
            if (a.Metric == b.Metric) return 0;
            return Better(a.Metric, b.Metric) ? -1 : 1;
        }

        /// <summary>Everyone, best first: survivors, then the most recent batch, and so on back.</summary>
        private List<Entry> Ordered()
        {
            var list = new List<Entry>();

            var survivors = new List<Entry>();
            for (int i = 0; i < _alive.Count; i++)
            {
                _lastMetric.TryGetValue(_alive[i], out int m);
                survivors.Add(new Entry { Id = _alive[i], Metric = m, Failed = false, Group = int.MaxValue });
            }
            survivors.Sort(CompareBetterFirst);
            list.AddRange(survivors);

            for (int b = _batches.Count - 1; b >= 0; b--)
                list.AddRange(_batches[b]);

            return list;
        }

        /// <summary>Standard competition ranking (1, 2, 2, 4). Only a true dead heat shares a place.</summary>
        private Dictionary<ulong, int> PlacesFrom(List<Entry> ordered)
        {
            var places = new Dictionary<ulong, int>();
            int place = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                bool sameAsPrevious = i > 0
                    && ordered[i - 1].Group == ordered[i].Group
                    && ordered[i - 1].Failed == ordered[i].Failed
                    && ordered[i - 1].Metric == ordered[i].Metric;
                if (!sameAsPrevious) place = i + 1;
                places[ordered[i].Id] = place;
            }
            return places;
        }

        public List<ulong> FinalRanking() => Ordered().ConvertAll(e => e.Id);
        public Dictionary<ulong, int> FinalPlaces() => PlacesFrom(Ordered());
    }
}
