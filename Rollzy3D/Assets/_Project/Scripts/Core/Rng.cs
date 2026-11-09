using UnityEngine;

namespace Rollzy
{
    /// <summary>Small deterministic RNG (mulberry32): the same level number always builds the same level.</summary>
    public sealed class Rng
    {
        uint state;

        public Rng(uint seed) { state = seed; }

        public float Next()
        {
            state += 0x6D2B79F5u;
            uint t = state;
            t = (t ^ (t >> 15)) * (1u | t);
            t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
            double v = (t ^ (t >> 14)) / 4294967296.0;
            return Mathf.Min((float)v, 0.9999999f);
        }

        public float Range(float min, float max) => min + Next() * (max - min);
        public int Int(int count) => Mathf.Min(count - 1, (int)(Next() * count));
        public bool Chance(float p) => Next() < p;
        public T Pick<T>(T[] items) => items[Int(items.Length)];
    }
}
