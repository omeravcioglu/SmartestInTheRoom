using System;
using System.Collections.Generic;

namespace Smartest.Rounds
{
    /// <summary>
    /// Shuffled deck of round ids with no repeats until exhausted. Supports forcing a
    /// specific input type for the next draw (Predict the Room → any RedGreen round).
    /// Pure C#, deterministic with a seed, so it is unit-testable.
    /// </summary>
    public sealed class RoundDeck
    {
        private readonly List<int> _allIds;
        private readonly Func<int, InputType?> _inputOf;
        private readonly Random _rng;
        private readonly List<int> _remaining = new List<int>();
        private int _lastDrawn = -1;

        public RoundDeck(IEnumerable<int> allIds, Func<int, InputType?> inputOf, int seed)
        {
            _allIds = new List<int>(allIds);
            _inputOf = inputOf;
            _rng = new Random(seed);
            Reshuffle();
        }

        public int Remaining => _remaining.Count;

        public void Reshuffle()
        {
            _remaining.Clear();
            _remaining.AddRange(_allIds);
            for (int i = _remaining.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_remaining[i], _remaining[j]) = (_remaining[j], _remaining[i]);
            }
            // Avoid repeating the very last round of the previous deck as the first of the next.
            if (_remaining.Count > 1 && _remaining[_remaining.Count - 1] == _lastDrawn)
                (_remaining[0], _remaining[_remaining.Count - 1]) = (_remaining[_remaining.Count - 1], _remaining[0]);
        }

        /// <summary>
        /// Take rounds out of this deck for good, e.g. ones that stopped working when a player
        /// left. Returns how many went. Never empties the deck: if every round would go, none do.
        /// </summary>
        public int Remove(Predicate<int> drop)
        {
            if (drop == null || _allIds.TrueForAll(drop)) return 0;
            _remaining.RemoveAll(drop);
            return _allIds.RemoveAll(drop);
        }

        /// <summary>Draw the next round id; optionally restricted to an input type.</summary>
        public int Draw(InputType? requiredInput = null)
        {
            if (_remaining.Count == 0) Reshuffle();

            if (requiredInput.HasValue)
            {
                // Prefer a matching round still in the deck; otherwise any matching round.
                for (int i = _remaining.Count - 1; i >= 0; i--)
                {
                    if (_inputOf(_remaining[i]) == requiredInput.Value)
                    {
                        int id = _remaining[i];
                        _remaining.RemoveAt(i);
                        _lastDrawn = id;
                        return id;
                    }
                }
                var candidates = new List<int>();
                foreach (var id in _allIds) if (_inputOf(id) == requiredInput.Value && id != _lastDrawn) candidates.Add(id);
                if (candidates.Count > 0)
                {
                    int pick = candidates[_rng.Next(candidates.Count)];
                    _lastDrawn = pick;
                    return pick;
                }
            }

            int next = _remaining[_remaining.Count - 1];
            _remaining.RemoveAt(_remaining.Count - 1);
            _lastDrawn = next;
            return next;
        }
    }

    /// <summary>
    /// Two decks, drawn in turn: a social round, then <c>minigamesPerQuestion</c> minigames,
    /// then a social round again. A forced draw (Predict the Room needs a red/green round
    /// next) always comes from the social deck and doesn't disturb the rhythm afterwards.
    /// </summary>
    public sealed class ChallengeDeck
    {
        private readonly RoundDeck _social;
        private readonly RoundDeck _minigames;
        private readonly RoundDeck _mixed;
        private readonly HashSet<int> _minigameIds;
        private readonly int _minigamesPerQuestion;
        /// <summary>Minigames still to deal before the next question.</summary>
        private int _minigamesDue;

        public ChallengeDeck(IEnumerable<int> socialIds, IEnumerable<int> minigameIds,
            Func<int, InputType?> inputOf, int seed, bool alternate, bool startWithMinigame = false,
            int minigamesPerQuestion = 1)
        {
            var social = new List<int>(socialIds ?? new List<int>());
            var games = new List<int>(minigameIds ?? new List<int>());
            _minigameIds = new HashSet<int>(games);
            _social = social.Count > 0 ? new RoundDeck(social, inputOf, seed) : null;
            _minigames = games.Count > 0 ? new RoundDeck(games, inputOf, seed + 7919) : null;
            _minigamesPerQuestion = Math.Max(1, minigamesPerQuestion);
            _minigamesDue = startWithMinigame ? _minigamesPerQuestion : 0;

            if (!alternate)
            {
                var all = new List<int>(social);
                all.AddRange(games);
                if (all.Count > 0) _mixed = new RoundDeck(all, inputOf, seed);
            }
        }

        public bool HasSocial => _social != null;
        public bool HasMinigames => _minigames != null;

        /// <summary>
        /// Take questions out for good: the ones that no longer work with the players still
        /// here. Minigames never come out, and neither does the last question standing.
        /// </summary>
        public int RemoveSocial(Predicate<int> drop)
        {
            if (drop == null) return 0;
            Predicate<int> question = id => !_minigameIds.Contains(id) && drop(id);
            int removed = _social != null ? _social.Remove(question) : 0;
            // Without alternation the questions are dealt from the mixed pool, but a forced
            // red/green follow-up still comes from _social, so both lose the same ones.
            if (removed > 0 && _mixed != null) _mixed.Remove(question);
            return removed;
        }

        public int Draw(InputType? requiredInput = null)
        {
            // A forced draw is always a social round (only those have an input type).
            if (requiredInput.HasValue && _social != null) return _social.Draw(requiredInput);
            if (_mixed != null) return _mixed.Draw(requiredInput);
            if (_social == null) return _minigames.Draw();
            if (_minigames == null) return _social.Draw();

            if (_minigamesDue > 0)
            {
                _minigamesDue--;
                return _minigames.Draw();
            }
            _minigamesDue = _minigamesPerQuestion;
            return _social.Draw();
        }
    }
}
