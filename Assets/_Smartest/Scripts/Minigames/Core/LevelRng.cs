using System;
using System.Collections.Generic;

namespace Smartest.Minigames
{
    /// <summary>
    /// Every client builds the same level from the same three numbers, so nobody has to
    /// send puzzle content over the network and everyone is genuinely racing the same
    /// challenge. Uses its own hash (string.GetHashCode is not stable across runtimes).
    /// </summary>
    public static class LevelRng
    {
        public static int Hash(string gameId, int seed, int level)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (gameId != null)
                    for (int i = 0; i < gameId.Length; i++) { h ^= gameId[i]; h *= 16777619u; }
                h ^= (uint)seed; h *= 16777619u;
                h ^= (uint)level; h *= 16777619u;
                int v = (int)(h & 0x7FFFFFFF);
                return v == 0 ? 1 : v;
            }
        }

        public static Random For(string gameId, int seed, int level)
        {
            return new Random(Hash(gameId, seed, level));
        }

        public static float Range(Random r, float min, float max)
        {
            return min + (float)r.NextDouble() * (max - min);
        }

        public static int Range(Random r, int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return r.Next(minInclusive, maxExclusive);
        }

        public static void Shuffle<T>(Random r, IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = r.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>`count` distinct values from 0..max-1.</summary>
        public static List<int> Distinct(Random r, int count, int max)
        {
            var pool = new List<int>(max);
            for (int i = 0; i < max; i++) pool.Add(i);
            Shuffle(r, pool);
            if (count > max) count = max;
            return pool.GetRange(0, count);
        }
    }
}
