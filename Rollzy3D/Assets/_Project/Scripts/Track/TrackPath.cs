using System.Collections.Generic;
using UnityEngine;

namespace Rollzy
{
    public enum SectionType { Straight, Curve, Hill, Wave, Twist, Loop, Helix }

    public sealed class Section
    {
        public SectionType type;
        public float s0, s1;
        public float total, bank, amp;
        public int sign, turns = 1;
        public bool signSet;
        public Vector3 shift;
        public bool shiftSet;
    }

    /// <summary>
    /// The road's 3D curve. Gameplay runs in "track space" (x = sideways, y = height above the road,
    /// s = distance along the road); this class maps track space onto turns, hills, S-waves,
    /// corkscrews, loops and climbing spirals.
    /// </summary>
    public sealed class TrackPath
    {
        public readonly float sMin;
        public readonly int count;
        public readonly float minY;
        readonly Vector3[] positions;
        readonly Quaternion[] rotations;

        public static bool IsSpecial(SectionType t) => t == SectionType.Twist || t == SectionType.Loop || t == SectionType.Helix;

        public static List<Section> Plan(int level, Rng rnd, float endS)
        {
            float diff = Balance.Difficulty(level);
            var secs = new List<Section> { new Section { type = SectionType.Straight, s0 = -80, s1 = Balance.SafeStart + 8 } };
            // Every level opens with a showcase: turn, corkscrew, hill, loop, S-waves.
            var forced = new[] { SectionType.Curve, SectionType.Twist, SectionType.Hill, SectionType.Loop, SectionType.Wave };
            float s = Balance.SafeStart + 8;
            bool lastSpecial = false;
            int index = 0;

            while (s < endS)
            {
                SectionType type;
                if (index < forced.Length) type = forced[index];
                else
                {
                    var opts = new List<(SectionType, float)> { (SectionType.Straight, 1), (SectionType.Curve, 3.5f), (SectionType.Hill, 2), (SectionType.Wave, 2.5f) };
                    if (!lastSpecial)
                    {
                        opts.Add((SectionType.Twist, 2.5f + diff * 2));
                        opts.Add((SectionType.Loop, 1.6f + diff * 1.5f));
                        if (level >= 2) opts.Add((SectionType.Helix, 1.2f + diff));
                    }
                    type = Weighted(rnd, opts);
                }
                index++;

                int turns = type == SectionType.Twist && level >= 4 && rnd.Chance(0.4f) ? 2 : 1;
                float len;
                switch (type)
                {
                    case SectionType.Straight: len = rnd.Range(12, 24); break;
                    case SectionType.Curve: len = rnd.Range(35, 60); break;
                    case SectionType.Hill: len = rnd.Range(45, 70); break;
                    case SectionType.Wave: len = rnd.Range(60, 90); break;
                    case SectionType.Twist: len = rnd.Range(55, 75) * (turns == 2 ? 1.5f : 1f); break;
                    case SectionType.Loop: len = 64; break;
                    default: len = rnd.Range(120, 150); break;
                }
                if (IsSpecial(type) && s + len > endS - 30) { type = SectionType.Curve; len = 45; }

                var sec = new Section { type = type, s0 = s, s1 = s + len, turns = turns };
                if (type == SectionType.Curve) { sec.total = rnd.Range(0.6f, 1.4f); sec.bank = rnd.Range(0.25f, 0.5f); }
                if (type == SectionType.Hill) sec.amp = rnd.Range(0.15f, 0.3f) * (rnd.Chance(0.5f) ? 1 : -1);
                if (type == SectionType.Wave) sec.amp = rnd.Range(0.3f, 0.5f) * (rnd.Chance(0.5f) ? 1 : -1);
                if (IsSpecial(type)) sec.sign = rnd.Chance(0.5f) ? 1 : -1;
                secs.Add(sec);
                lastSpecial = IsSpecial(type);
                s += len;
            }
            secs.Add(new Section { type = SectionType.Straight, s0 = s, s1 = float.PositiveInfinity });
            return secs;
        }

        static SectionType Weighted(Rng rnd, List<(SectionType type, float weight)> opts)
        {
            float total = 0;
            foreach (var o in opts) total += o.weight;
            float r = rnd.Next() * total;
            foreach (var o in opts) { r -= o.weight; if (r <= 0) return o.type; }
            return opts[opts.Count - 1].type;
        }

        public TrackPath(List<Section> secs, float sMax)
        {
            const float Step = Balance.PathStep;
            sMin = -80;
            count = Mathf.CeilToInt((sMax - sMin) / Step) + 2;
            positions = new Vector3[count];
            rotations = new Quaternion[count];

            var P = new Vector3(0, 0, sMin);
            var Q = Quaternion.identity;
            int si = 0;
            const float D = Mathf.Rad2Deg;

            for (int i = 0; i < count; i++)
            {
                float s = sMin + i * Step;
                positions[i] = P;
                rotations[i] = Q;
                while (secs[si].s1 <= s) si++;
                var sec = secs[si];
                float L = sec.s1 - sec.s0, t = (s - sec.s0) / L;

                switch (sec.type)
                {
                    case SectionType.Curve:
                    {
                        if (!sec.signSet)
                        {
                            Vector3 f = Q * Vector3.forward;
                            float heading = Mathf.Atan2(f.x, f.z);
                            // Steer back toward the start direction so the road never crosses itself.
                            sec.sign = Mathf.Abs(heading) > 0.5f ? -(int)Mathf.Sign(heading) : (Mathf.Sin(s * 12.9898f) > 0 ? 1 : -1);
                            sec.signSet = true;
                        }
                        float yaw = sec.sign * sec.total * (Mathf.PI / 2) / L * Mathf.Sin(Mathf.PI * t) * Step;
                        Q = Quaternion.AngleAxis(yaw * D, Vector3.up) * Q;
                        float roll = -sec.sign * sec.bank * Mathf.PI / L * Mathf.Cos(Mathf.PI * t) * Step; // bank into the turn
                        Q *= Quaternion.AngleAxis(roll * D, Vector3.forward);
                        break;
                    }
                    case SectionType.Hill:
                        Q *= Quaternion.AngleAxis(sec.amp * 2 * Mathf.PI / L * Mathf.Cos(2 * Mathf.PI * t) * Step * D, Vector3.right);
                        break;
                    case SectionType.Wave:
                    {
                        float yr = sec.amp * 4 * Mathf.PI / L * Mathf.Cos(4 * Mathf.PI * t) * Step;
                        Q = Quaternion.AngleAxis(yr * D, Vector3.up) * Q;
                        Q *= Quaternion.AngleAxis(-yr * 0.9f * D, Vector3.forward);
                        break;
                    }
                    case SectionType.Twist:
                        Q *= Quaternion.AngleAxis(sec.sign * sec.turns * 2 * Mathf.PI / L * (1 - Mathf.Cos(2 * Mathf.PI * t)) * Step * D, Vector3.forward);
                        break;
                    case SectionType.Helix:
                    {
                        float w = (1 - Mathf.Cos(2 * Mathf.PI * t)) / L * Step;
                        Q = Quaternion.AngleAxis(sec.sign * 2 * Mathf.PI * w * D, Vector3.up) * Q;
                        Q *= Quaternion.AngleAxis(-sec.sign * 0.4f * Mathf.PI / L * Mathf.Cos(Mathf.PI * t) * Step * D, Vector3.forward);
                        // Climb so the spiral passes over its own entry.
                        Q *= Quaternion.AngleAxis(-0.075f * Mathf.PI / L * Mathf.Cos(Mathf.PI * t) * Step * D, Vector3.right);
                        break;
                    }
                    case SectionType.Loop:
                    {
                        if (!sec.shiftSet)
                        {
                            Vector3 right = Q * Vector3.right;
                            right.y = 0;
                            sec.shift = right.normalized * (sec.sign * (Balance.TrackWidth + 3));
                            sec.shiftSet = true;
                        }
                        float w = (1 - Mathf.Cos(2 * Mathf.PI * t)) / L * Step;
                        Q *= Quaternion.AngleAxis(-2 * Mathf.PI * w * D, Vector3.right); // nose up, all the way round
                        P += sec.shift * w; // sidestep so the exit doesn't hit the entry
                        break;
                    }
                }
                Q = Quaternion.Normalize(Q);
                P += Q * Vector3.forward * Step;
            }

            float lowest = float.PositiveInfinity;
            foreach (var p in positions) lowest = Mathf.Min(lowest, p.y);
            minY = lowest;
        }

        public void Frame(float s, out Vector3 position, out Quaternion rotation)
        {
            float f = Mathf.Clamp((s - sMin) / Balance.PathStep, 0, count - 1.0001f);
            int i = (int)f;
            float t = f - i;
            position = Vector3.LerpUnclamped(positions[i], positions[i + 1], t);
            rotation = Quaternion.Slerp(rotations[i], rotations[i + 1], t);
        }

        public Vector3 ToWorld(float x, float y, float s)
        {
            Frame(s, out var p, out var q);
            return p + q * new Vector3(x, y, 0);
        }

        public Vector3 SamplePosition(int index) => positions[Mathf.Clamp(index, 0, count - 1)];
    }
}
