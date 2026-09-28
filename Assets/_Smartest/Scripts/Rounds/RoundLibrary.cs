using System.Collections.Generic;
using UnityEngine;

namespace Smartest.Rounds
{
    /// <summary>Every challenge available to the decks. Built by Tools &gt; Smartest &gt; Build Scenes.</summary>
    [CreateAssetMenu(menuName = "Smartest/Round Library", fileName = "RoundLibrary")]
    public class RoundLibrary : ScriptableObject
    {
        public List<RoundDefinition> rounds = new List<RoundDefinition>();

        private Dictionary<int, RoundDefinition> _byId;

        public RoundDefinition GetById(int id)
        {
            if (_byId == null || _byId.Count != rounds.Count)
            {
                _byId = new Dictionary<int, RoundDefinition>();
                foreach (var r in rounds) if (r != null) _byId[r.id] = r;
            }
            return _byId.TryGetValue(id, out var def) ? def : null;
        }

        public List<int> AllIds()
        {
            var ids = new List<int>();
            foreach (var r in rounds) if (r != null) ids.Add(r.id);
            return ids;
        }

        /// <summary>
        /// Social rounds only — the deck the "question" half of the match draws from. Pass the
        /// match's player count to leave out rounds that don't work with that few players (Sus
        /// can never pay out between two). Nobody can join a match once it starts, so the count
        /// at the start is the most it will ever be.
        /// </summary>
        public List<int> SocialIds(int players = 0)
        {
            var ids = new List<int>();
            foreach (var r in rounds)
                if (r != null && r.kind == RoundKind.Social && r.WorksWith(players)) ids.Add(r.id);
            // Never an empty deck: if nothing fits (a solo test, say), deal everything.
            if (ids.Count == 0 && players > 0) return SocialIds();
            return ids;
        }

        /// <summary>Minigames only — the deck the other half draws from.</summary>
        public List<int> MinigameIds()
        {
            var ids = new List<int>();
            foreach (var r in rounds) if (r != null && r.kind == RoundKind.Minigame) ids.Add(r.id);
            return ids;
        }

        public List<int> IdsWithInput(InputType type)
        {
            var ids = new List<int>();
            foreach (var r in rounds)
                if (r != null && r.kind == RoundKind.Social && r.inputType == type) ids.Add(r.id);
            return ids;
        }
    }
}
