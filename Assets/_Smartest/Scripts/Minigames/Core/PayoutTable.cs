using System;
using System.Collections.Generic;

namespace Smartest.Minigames
{
    /// <summary>
    /// Turns places into points. The whole scoring curve is three numbers on GameConfig,
    /// so tuning the game never means touching code:
    ///
    ///   placePoints = { 20, 10, 5 }, lastPlacePoints = -5
    ///     2 players  +20 / -5
    ///     3 players  +20 / +10 / -5
    ///     4 players  +20 / +10 / +5 / -5
    ///     8 players  +20 / +10 / +5 / 0 / 0 / 0 / 0 / -5
    ///
    /// Last place always takes the penalty instead of a table entry, which is what keeps
    /// three-player games honest. A genuine dead heat shares a place, and both players
    /// then get whatever that place pays.
    /// </summary>
    public static class PayoutTable
    {
        public static readonly int[] DefaultPlacePoints = { 20, 10, 5 };
        public const int DefaultLastPlacePoints = -5;

        public static int PointsFor(int place, int worstPlace, IReadOnlyList<int> placePoints, int lastPlacePoints)
        {
            if (place <= 0) return 0;
            if (place == worstPlace) return lastPlacePoints;
            if (placePoints != null && place <= placePoints.Count) return placePoints[place - 1];
            return 0;
        }

        public static Dictionary<ulong, int> Deltas(IReadOnlyDictionary<ulong, int> places,
            IReadOnlyList<int> placePoints, int lastPlacePoints)
        {
            var deltas = new Dictionary<ulong, int>();
            if (places == null || places.Count == 0) return deltas;

            // One player isn't a competition; the solo path in GameState handles that case.
            if (places.Count == 1)
            {
                foreach (var kv in places) deltas[kv.Key] = 0;
                return deltas;
            }

            int worst = 0;
            foreach (var kv in places) worst = Math.Max(worst, kv.Value);
            foreach (var kv in places) deltas[kv.Key] = PointsFor(kv.Value, worst, placePoints, lastPlacePoints);
            return deltas;
        }
    }
}
