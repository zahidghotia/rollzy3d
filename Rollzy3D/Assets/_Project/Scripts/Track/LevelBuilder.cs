using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rollzy
{
    public sealed class ObstacleBox
    {
        public float x, y, z, hx, hy, hz;
        public Transform view;
    }

    public sealed class Mover
    {
        public ObstacleBox box;
        public Vector3 origin;
        public Quaternion rotation;
        public bool vertical;
        public float baseY, amp, speed, phase;
    }

    public sealed class Sweeper
    {
        public Transform spinner;
        public float z, r, angle, speed;
    }

    public sealed class CoinPickup
    {
        public float x, y, z;
        public Transform root, spin;
        public bool alive = true;
    }

    public sealed class LevelData
    {
        public int level;
        public GameObject root;
        public TrackPath path;
        public List<Section> sections;
        public readonly List<ObstacleBox> boxes = new List<ObstacleBox>();
        public readonly List<Mover> movers = new List<Mover>();
        public readonly List<Sweeper> sweepers = new List<Sweeper>();
        public readonly List<CoinPickup> coins = new List<CoinPickup>();
        public readonly List<Vector2> gaps = new List<Vector2>();
        public readonly List<Object> owned = new List<Object>();
        public Biome[] biomes;
        public float finishLine, baseSpeed, regionLen, groundY;
        public int totalCoins;

        public int RegionAt(float s) => Mathf.Clamp(Mathf.FloorToInt(s / regionLen), 0, biomes.Length - 1);
        public Biome BiomeAt(float s) => biomes[RegionAt(s)];

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
            foreach (var o in owned) if (o != null) Object.Destroy(o);
        }
    }

    /// <summary>Builds a level from its number. Same number, same level, every time.</summary>
    public static class LevelBuilder
    {
        const float W2 = Balance.TrackWidth / 2, Lip = 0.35f, Bottom = -0.7f;
        static readonly Vector2[] CrossSection =
        {
            new Vector2(-W2 - Lip, 0.3f), new Vector2(-W2, 0.3f), new Vector2(-W2, 0), new Vector2(W2, 0),
            new Vector2(W2, 0.3f), new Vector2(W2 + Lip, 0.3f), new Vector2(W2 + Lip, Bottom), new Vector2(-W2 - Lip, Bottom),
        };

        static Mesh cubeMesh, cylinderMesh;

        public static LevelData Build(int level)
        {
            level = Mathf.Max(1, level);
            var rnd = new Rng((uint)(level * 7919 + 1337));
            float diff = Balance.Difficulty(level);
            var d = new LevelData { level = level, root = new GameObject("Level " + level) };
            if (cubeMesh == null) cubeMesh = PrimitiveMesh(PrimitiveType.Cube);
            if (cylinderMesh == null) cylinderMesh = PrimitiveMesh(PrimitiveType.Cylinder);

            float finishZ = Balance.SafeStart + Balance.LevelLength(level);
            float cell = Balance.TrackWidth / 8;
            d.finishLine = finishZ + cell;
            d.baseSpeed = Balance.LevelSpeed(level);

            int regions = Mathf.Min(5, 3 + ((level - 1) % Balance.LevelsPerWorld) / 4);
            int world0 = Balance.WorldOf(level);
            d.regionLen = (d.finishLine + 10) / regions;
            d.biomes = new Biome[regions];
            for (int i = 0; i < regions; i++) d.biomes[i] = Catalog.Biomes[(world0 + i) % Catalog.Biomes.Length];

            d.sections = TrackPath.Plan(level, rnd, finishZ);
            d.path = new TrackPath(d.sections, d.finishLine + Balance.RunOut + 60);
            d.groundY = d.path.minY - 10;

            var ctx = new Ctx(d, rnd, diff);
            ctx.PlaceObstacles(finishZ);
            d.totalCoins = d.coins.Count;
            ctx.BuildRoad();
            ctx.BuildDashesAndPillars();
            ctx.BuildScenery();
            ctx.BuildFinish(finishZ, cell);
            return d;
        }

        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return mesh;
        }

        sealed class Ctx
        {
            readonly LevelData d;
            readonly Rng rnd;
            readonly float diff;
            readonly Transform obstaclesRoot, coinsRoot, envRoot;
            readonly int level;

            public Ctx(LevelData data, Rng rng, float difficulty)
            {
                d = data; rnd = rng; diff = difficulty; level = data.level;
                obstaclesRoot = Group("Obstacles"); coinsRoot = Group("Coins"); envRoot = Group("Environment");
            }

            Transform Group(string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(d.root.transform, false);
                return t;
            }

            float SpeedAt(float z) => d.baseSpeed * (1 + Balance.SpeedRamp * z / d.finishLine);

            Section NoObstacleSection(float a, float b) =>
                d.sections.Find(sc => TrackPath.IsSpecial(sc.type) && sc.s0 < b + 3 && sc.s1 > a - 3);

            // ------------------------------------------------------------ placement helpers
            Transform Place(Transform t, float x, float y, float s)
            {
                d.path.Frame(s, out var p, out var q);
                t.SetPositionAndRotation(p + q * new Vector3(x, y, 0), q);
                return t;
            }

            Transform Box(Transform parent, float x, float y, float s, Vector3 size, Material m, bool shadows = true)
            {
                var go = new GameObject("Box");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                go.transform.localScale = size;
                return Place(go.transform, x, y, s);
            }

            ObstacleBox Obstacle(float x, float y, float z, Vector3 size, Material m)
            {
                var t = Box(obstaclesRoot, x, y, z, size, m);
                var bx = new ObstacleBox { x = x, y = y, z = z, hx = size.x / 2, hy = size.y / 2, hz = size.z / 2, view = t };
                d.boxes.Add(bx);
                return bx;
            }

            void Coin(float x, float y, float z)
            {
                var root = new GameObject("Coin").transform;
                root.SetParent(coinsRoot, false);
                Place(root, x, y, z);
                var spin = new GameObject("Spin").transform;
                spin.SetParent(root, false);
                var disc = new GameObject("Disc");
                disc.transform.SetParent(spin, false);
                disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
                disc.transform.localScale = new Vector3(0.72f, 0.045f, 0.72f);
                disc.AddComponent<MeshFilter>().sharedMesh = cylinderMesh;
                var r = disc.AddComponent<MeshRenderer>();
                r.sharedMaterial = Mats.Gold();
                d.coins.Add(new CoinPickup { x = x, y = y, z = z, root = root, spin = spin });
            }

            void CoinLine(int lane, float a, float b, float step = Balance.CoinStep)
            {
                for (float z = a; z <= b + 0.01f; z += step) Coin(Balance.LaneX(lane), Balance.CoinY, z);
            }

            void CoinArc(int lane, float cz, float half, float h)
            {
                for (int i = 0; i < 7; i++)
                {
                    float t = -1 + 2f * i / 6;
                    Coin(Balance.LaneX(lane), Balance.CoinY + h * (1 - t * t), cz + t * half);
                }
            }

            Material WallMat(float z) => Mats.Stripes(d.BiomeAt(z).obstacle, Dim(d.BiomeAt(z).obstacle, 0.72f));
            Material HurdleMat(float z) => Mats.Stripes(d.BiomeAt(z).hazard, new Color(0.17f, 0.14f, 0.25f));

            void Wall(int lane, float z) =>
                Obstacle(Balance.LaneX(lane), 0.8f, z + 0.5f, new Vector3(Balance.LaneWidth - 0.25f, 1.6f, 1), WallMat(z));

            void AddMover(ObstacleBox bx, bool vertical, float amp, float speed, float phase)
            {
                d.path.Frame(bx.z, out var p, out var q);
                d.movers.Add(new Mover { box = bx, origin = p, rotation = q, vertical = vertical, baseY = bx.y, amp = amp, speed = speed, phase = phase });
            }

            void SlidingBlock(float z, float phase)
            {
                var bx = Obstacle(0, 0.7f, z + 0.6f, new Vector3(Balance.LaneWidth - 0.4f, 1.4f, 1.2f), Mats.Stripes(d.BiomeAt(z).hazard, Dim(d.BiomeAt(z).hazard, 0.72f)));
                AddMover(bx, false, Balance.LaneWidth, Mathf.Lerp(1.8f, 3.4f, diff), phase);
            }

            int OtherLane(int lane) => (lane + 1 + rnd.Int(Balance.Lanes - 1)) % Balance.Lanes;

            // ------------------------------------------------------------ obstacle patterns
            string Pick(string last)
            {
                var opts = new List<(string, float)>();
                void AddOpt(string p, float w) => opts.Add((p, p == last ? w * 0.35f : w));
                AddOpt("wall", 2.5f); AddOpt("double", 3); AddOpt("hurdle", 2); AddOpt("gap", 2.5f); AddOpt("mover", 2);
                if (level >= 2) { AddOpt("sweeper", 2); AddOpt("combo", 2.2f); }
                if (level >= 3) AddOpt("zigzag", 2.2f);
                if (level >= 4) AddOpt("pistons", 2);
                if (level >= 5) AddOpt("twomovers", 1.6f);
                float total = 0;
                foreach (var o in opts) total += o.Item2;
                float r = rnd.Next() * total;
                foreach (var o in opts) { r -= o.Item2; if (r <= 0) return o.Item1; }
                return opts[opts.Count - 1].Item1;
            }

            float PlacePattern(string p, float z)
            {
                var th = d.BiomeAt(z);
                switch (p)
                {
                    case "wall":
                    {
                        int blocked = rnd.Int(3);
                        Wall(blocked, z);
                        CoinLine(OtherLane(blocked), z - 7.5f, z + 1.5f);
                        return 1;
                    }
                    case "double":
                    {
                        int open = rnd.Int(3);
                        for (int l = 0; l < 3; l++) if (l != open) Wall(l, z);
                        CoinLine(open, z - 7.5f, z + 1.5f);
                        return 1;
                    }
                    case "hurdle":
                        Obstacle(0, 0.2f, z + 0.2f, new Vector3(Balance.TrackWidth - 0.6f, 0.4f, 0.38f), HurdleMat(z));
                        CoinArc(rnd.Int(3), z + 0.2f, 3, 1);
                        return 0.5f;
                    case "gap":
                    {
                        float len = Mathf.Lerp(3.6f, 5.6f, diff);
                        d.gaps.Add(new Vector2(z, z + len));
                        CoinArc(rnd.Int(3), z + len / 2, len / 2 + 1.5f, 1);
                        return len;
                    }
                    case "mover":
                        SlidingBlock(z, rnd.Range(0, Mathf.PI * 2));
                        return 1.2f;
                    case "twomovers":
                    {
                        float ph = rnd.Range(0, 6.28f), gap = Mathf.Max(6, SpeedAt(z) * 0.45f);
                        SlidingBlock(z, ph);
                        SlidingBlock(z + gap, ph + Mathf.PI);
                        return gap + 1.2f;
                    }
                    case "combo":
                    {
                        // Switch lanes first, then jump: the low bar sits a little after the walls.
                        int open = rnd.Int(3);
                        for (int l = 0; l < 3; l++) if (l != open) Wall(l, z);
                        float hz = z + 0.5f + Mathf.Max(4, SpeedAt(z) * 0.3f);
                        Obstacle(Balance.LaneX(open), 0.2f, hz, new Vector3(Balance.LaneWidth - 0.3f, 0.4f, 0.38f), HurdleMat(z));
                        CoinArc(open, hz, 3, 1);
                        return hz - z + 0.5f;
                    }
                    case "zigzag":
                    {
                        int lane = rnd.Int(3), dir = rnd.Chance(0.5f) ? 1 : 2;
                        float gap = Mathf.Max(6.5f, SpeedAt(z) * 0.5f);
                        for (int k = 0; k < 3; k++) { Wall(lane, z + k * gap); lane = (lane + dir) % 3; }
                        return 2 * gap + 1;
                    }
                    case "pistons":
                    {
                        // Walls that rise and drop; pass underneath while one is up.
                        float ph = rnd.Range(0, 6.28f), sp = Mathf.Lerp(2.2f, 3.4f, diff);
                        for (int l = 0; l < 3; l++)
                        {
                            var bx = Obstacle(Balance.LaneX(l), 0.8f, z + 0.5f, new Vector3(Balance.LaneWidth - 0.3f, 1.6f, 1), Mats.Stripes(th.obstacle, new Color(0.17f, 0.14f, 0.25f)));
                            AddMover(bx, true, 2, sp, ph + l * 2.1f);
                        }
                        return 1;
                    }
                    case "sweeper":
                    {
                        float r = Balance.LaneWidth * 1.5f + 0.35f;
                        var pivot = Place(new GameObject("Sweeper").transform, 0, 0, z + r);
                        pivot.SetParent(obstaclesRoot, true);
                        var spinner = new GameObject("Spinner").transform;
                        spinner.SetParent(pivot, false);
                        var bar = new GameObject("Bar");
                        bar.transform.SetParent(spinner, false);
                        bar.transform.localPosition = new Vector3(0, 0.35f, 0);
                        bar.transform.localScale = new Vector3(r * 2, 0.3f, 0.3f);
                        bar.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
                        bar.AddComponent<MeshRenderer>().sharedMaterial = Mats.Stripes(th.hazard, Color.white);
                        var post = new GameObject("Post");
                        post.transform.SetParent(spinner, false);
                        post.transform.localPosition = new Vector3(0, 0.35f, 0);
                        post.transform.localScale = new Vector3(0.5f, 0.35f, 0.5f);
                        post.AddComponent<MeshFilter>().sharedMesh = cylinderMesh;
                        post.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(th.obstacle);
                        float angle = rnd.Range(0, Mathf.PI);
                        spinner.localRotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);
                        d.sweepers.Add(new Sweeper
                        {
                            spinner = spinner, z = z + r, r = r, angle = angle,
                            speed = Mathf.Lerp(90, 170, diff) * Mathf.Deg2Rad * (rnd.Chance(0.5f) ? 1 : -1),
                        });
                        return r * 2;
                    }
                }
                return 1;
            }

            public void PlaceObstacles(float finishZ)
            {
                float gapTime = Balance.GapTime(diff);
                float z = Balance.SafeStart;
                string last = null;
                while (z < finishZ - 16)
                {
                    // Corkscrews, loops and spirals only carry coins: the view spins there.
                    var sp = NoObstacleSection(z, z + 15);
                    if (sp != null)
                    {
                        CoinLine(1, Mathf.Max(z, sp.s0 + 3), sp.s1 - 3, 2.2f);
                        z = sp.s1 + Mathf.Max(10, SpeedAt(sp.s1) * 0.9f);
                        continue;
                    }
                    string p = Pick(last);
                    last = p;
                    float used = PlacePattern(p, z);
                    float next = z + used + Mathf.Max(9, SpeedAt(z) * gapTime) * rnd.Range(0.85f, 1.15f);
                    if (rnd.Chance(0.55f)) CoinLine(rnd.Int(3), z + used + 2.5f, Mathf.Min(next - 9, finishZ - 2));
                    z = next;
                }
            }

            // ------------------------------------------------------------ road
            List<Vector2> runs;

            Vector3 Local(Vector3 p, Quaternion q, float x, float y) => p + q * new Vector3(x, y, 0);

            public void BuildRoad()
            {
                d.gaps.Sort((a, b) => a.x.CompareTo(b.x));
                runs = new List<Vector2>();
                float cur = -Balance.RunUp;
                foreach (var g in d.gaps) { runs.Add(new Vector2(cur, g.x)); cur = g.y; }
                runs.Add(new Vector2(cur, d.finishLine + Balance.RunOut));

                var road = new MeshAccumulator();
                var glow = new MeshAccumulator();
                foreach (var run in runs) RoadSegment(road, glow, run.x, run.y);
                road.Build("Road", envRoot, true, true, d.owned);
                glow.Build("RoadGlow", envRoot, false, false, d.owned);
            }

            void RoadSegment(MeshAccumulator road, MeshAccumulator glow, float a, float b)
            {
                if (b - a < 0.05f) return;
                var ss = new List<float> { a };
                for (float s = Mathf.Ceil(a / 0.5f) * 0.5f; s < b - 1e-3f; s += 0.5f) if (s > a + 1e-3f) ss.Add(s);
                ss.Add(b);

                var prev = new Vector3[8];
                var cur = new Vector3[8];
                var prevG = new Vector3[4];
                var curG = new Vector3[4];
                Ring(a, prev, prevG);
                for (int i = 1; i < ss.Count; i++)
                {
                    Ring(ss[i], cur, curG);
                    float mid = (ss[i - 1] + ss[i]) / 2;
                    var th = d.BiomeAt(mid);
                    int idx = Mathf.FloorToInt(mid / Balance.TileLength);
                    var top = Mats.Lit(((idx % 2) + 2) % 2 == 0 ? th.roadA : th.roadB, 0.25f);
                    var edge = Mats.Lit(th.edge, 0.3f);
                    var side = Mats.Lit(Dim(th.edge, 0.78f), 0.2f);
                    var under = Mats.Lit(Dim(th.edge, 0.5f), 0.1f);
                    for (int e = 0; e < 8; e++)
                    {
                        int e2 = (e + 1) % 8;
                        var m = e == 2 ? top : e == 6 ? under : (e == 5 || e == 7) ? side : edge;
                        road.Quad(prev[e], cur[e], cur[e2], prev[e2], m);
                    }
                    var gm = Mats.Unlit(th.glow);
                    glow.Quad(prevG[0], curG[0], curG[1], prevG[1], gm);
                    glow.Quad(prevG[2], curG[2], curG[3], prevG[3], gm);
                    System.Array.Copy(cur, prev, 8);
                    System.Array.Copy(curG, prevG, 4);
                }
                Cap(road, a, true);
                Cap(road, b, false);
            }

            void Ring(float s, Vector3[] ring, Vector3[] glowPts)
            {
                d.path.Frame(s, out var p, out var q);
                for (int k = 0; k < 8; k++) ring[k] = Local(p, q, CrossSection[k].x, CrossSection[k].y);
                glowPts[0] = Local(p, q, -W2 - 0.27f, 0.31f); glowPts[1] = Local(p, q, -W2 - 0.08f, 0.31f);
                glowPts[2] = Local(p, q, W2 + 0.08f, 0.31f); glowPts[3] = Local(p, q, W2 + 0.27f, 0.31f);
            }

            void Cap(MeshAccumulator road, float s, bool start)
            {
                d.path.Frame(s, out var p, out var q);
                var m = Mats.Lit(Dim(d.BiomeAt(s).edge, 0.8f), 0.2f);
                void Rect(float x0, float y0, float x1, float y1)
                {
                    Vector3 bl = Local(p, q, x0, y0), tl = Local(p, q, x0, y1), tr = Local(p, q, x1, y1), br = Local(p, q, x1, y0);
                    if (start) road.Quad(bl, tl, tr, br, m); else road.Quad(br, tr, tl, bl, m);
                }
                Rect(-W2 - Lip, Bottom, W2 + Lip, 0);
                Rect(-W2 - Lip, 0, -W2, 0.3f);
                Rect(W2, 0, W2 + Lip, 0.3f);
            }

            // ------------------------------------------------------------ lane dashes, pillars
            public void BuildDashesAndPillars()
            {
                var acc = new MeshAccumulator();
                foreach (var run in runs)
                {
                    for (float s = Mathf.Ceil(run.x / 3) * 3 + 0.8f; s < run.y - 0.8f; s += 3)
                    {
                        d.path.Frame(s, out var p, out var q);
                        var m = Mats.Lit(Color.Lerp(d.BiomeAt(s).edge, Color.white, 0.35f), 0.2f);
                        foreach (float x in new[] { Balance.LaneWidth / 2, -Balance.LaneWidth / 2 })
                            acc.Add(MeshTemplate.Box, Matrix4x4.TRS(Local(p, q, x, 0.012f), q, new Vector3(0.14f, 0.02f, 1.4f)), m);
                    }
                }
                for (float s = 6; s < d.finishLine + Balance.RunOut; s += 22)
                {
                    if (NoObstacleSection(s - 4, s + 4) != null) continue;
                    bool inGap = false;
                    foreach (var g in d.gaps) if (s > g.x - 2 && s < g.y + 2) inGap = true;
                    if (inGap) continue;
                    d.path.Frame(s, out var p, out var q);
                    if ((q * Vector3.up).y < 0.9f) continue;
                    Vector3 basePt = Local(p, q, 0, Bottom);
                    float h = basePt.y - d.groundY;
                    if (h < 2) continue;
                    var edge = d.BiomeAt(s).edge;
                    acc.Add(MeshTemplate.Pillar, Matrix4x4.TRS(new Vector3(basePt.x, d.groundY + h / 2, basePt.z), Quaternion.identity, new Vector3(1.1f, h, 1.1f)), Mats.Lit(Dim(edge, 0.85f), 0.2f));
                    acc.Add(MeshTemplate.Box, Matrix4x4.TRS(basePt + Vector3.down * 0.2f, q, new Vector3(Balance.TrackWidth * 0.6f, 0.4f, 1.6f)), Mats.Lit(edge, 0.2f));
                }
                acc.Build("DashesAndPillars", envRoot, false, true, d.owned);
            }

            // ------------------------------------------------------------ scenery
            MeshAccumulator scenery;
            float[] pathXZ;
            Vector3 rightH = Vector3.right;

            bool Clear(float x, float z, float r)
            {
                float rr = (r + 7) * (r + 7);
                for (int i = 0; i < pathXZ.Length; i += 2)
                {
                    float dx = x - pathXZ[i], dz = z - pathXZ[i + 1];
                    if (dx * dx + dz * dz < rr) return false;
                }
                return true;
            }

            void Add(MeshTemplate t, float x, float y, float z, Vector3 scale, Color c, float ry = 0, float rz = 0, Material special = null)
            {
                scenery.Add(t, Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0, ry * Mathf.Rad2Deg, rz * Mathf.Rad2Deg), scale), special ?? Mats.Lit(c, 0.1f));
            }

            Vector4 Spot(float s, int side, float dist)
            {
                d.path.Frame(s, out var p, out var q);
                Vector3 r = q * Vector3.right;
                r.y = 0;
                if (r.sqrMagnitude > 0.05f) rightH = r.normalized;
                return new Vector4(p.x + rightH.x * side * dist, p.y, p.z + rightH.z * side * dist, Mathf.Atan2(rightH.x, rightH.z));
            }

            static Color H(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
            static Color Dim(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, 1f);
            static readonly Color[] Greens = { H("#2e9e48"), H("#45b649"), H("#1f7a3a"), H("#6cc24a") };
            static readonly Color[] Pines = { H("#2a7a46"), H("#226b3c"), H("#35884f") };
            static readonly Color[] Towers = { H("#8c7ae6"), H("#6c5ce7"), H("#a29bfe"), H("#74b9ff"), H("#fd79a8"), H("#81ecec") };
            static readonly Color[] Sand = { H("#f0c27b"), H("#e8b46a"), H("#f5d090") };

            void Tree(float x, float z, float h)
            {
                float gy = d.groundY, r = h * 0.28f;
                Add(MeshTemplate.Trunk, x, gy + h * 0.3f, z, new Vector3(0.8f, h * 0.6f, 0.8f), H("#7a4b2a"));
                Add(MeshTemplate.Blob, x, gy + h * 0.68f, z, new Vector3(r, r * 0.85f, r), rnd.Pick(Greens), rnd.Range(0, 6));
                Add(MeshTemplate.Blob, x + r * 0.3f, gy + h * 0.9f, z - r * 0.2f, new Vector3(r * 0.7f, r * 0.6f, r * 0.7f), rnd.Pick(Greens), rnd.Range(0, 6));
            }

            void Pine(float x, float z, float h, bool snow)
            {
                float gy = d.groundY;
                Add(MeshTemplate.Trunk, x, gy + h * 0.1f, z, new Vector3(0.6f, h * 0.2f, 0.6f), H("#6b4428"));
                Add(MeshTemplate.Cone, x, gy + h * 0.58f, z, new Vector3(h * 0.27f, h * 0.8f, h * 0.27f), snow ? H("#2f6b4f") : rnd.Pick(Pines));
                if (snow) Add(MeshTemplate.Cone, x, gy + h * 0.86f, z, new Vector3(h * 0.13f, h * 0.26f, h * 0.13f), Color.white);
            }

            void Mountain(float x, float z, float r, float h, Color c)
            {
                float gy = d.groundY;
                Add(MeshTemplate.Cone, x, gy + h / 2, z, new Vector3(r, h, r), c, rnd.Range(0, 6));
                Add(MeshTemplate.Cone, x, gy + h * 0.82f, z, new Vector3(r * 0.37f, h * 0.36f, r * 0.37f), Color.white);
            }

            void Near(Scenery kind, float x, float z, float ry)
            {
                float gy = d.groundY;
                switch (kind)
                {
                    case Scenery.Jungle:
                    {
                        float k = rnd.Next();
                        if (k < 0.72f) { float h = rnd.Range(9, 18); if (Clear(x, z, h * 0.28f)) Tree(x, z, h); }
                        else if (k < 0.9f) { float s = rnd.Range(2, 4); if (Clear(x, z, s)) Add(MeshTemplate.Ball, x, gy + s * 0.4f, z, new Vector3(s, s * 0.7f, s), rnd.Chance(0.5f) ? H("#3fae4f") : H("#2c8f43")); }
                        else { float s = rnd.Range(1.5f, 3); if (Clear(x, z, s)) Add(MeshTemplate.Blob, x, gy + s * 0.5f, z, new Vector3(s, s * 0.8f, s), H("#8d8a80"), rnd.Range(0, 6)); }
                        break;
                    }
                    case Scenery.Mountains:
                    {
                        if (rnd.Chance(0.75f)) { float h = rnd.Range(8, 15); if (Clear(x, z, h * 0.27f)) Pine(x, z, h, false); }
                        else { float s = rnd.Range(2, 4); if (Clear(x, z, s)) Add(MeshTemplate.Blob, x, gy + s * 0.5f, z, new Vector3(s, s * 0.8f, s), H("#9097a8"), rnd.Range(0, 6)); }
                        break;
                    }
                    case Scenery.City:
                    {
                        float bw = rnd.Range(6, 11), bd = rnd.Range(6, 11), h = rnd.Range(14, 52);
                        if (!Clear(x, z, Mathf.Max(bw, bd) * 0.72f)) return;
                        Add(MeshTemplate.Box, x, gy + h / 2, z, new Vector3(bw, h, bd), Color.white, ry, 0, Mats.Windows());
                        if (rnd.Chance(0.5f)) Add(MeshTemplate.Box, x, gy + h + 1, z, new Vector3(bw * 0.45f, 2, bd * 0.45f), rnd.Pick(Towers), ry);
                        if (rnd.Chance(0.25f)) Add(MeshTemplate.Box, x, gy + h + 5, z, new Vector3(0.3f, 8, 0.3f), H("#ff3d9a"), ry);
                        break;
                    }
                    case Scenery.Desert:
                    {
                        if (rnd.Chance(0.55f))
                        {
                            float h = rnd.Range(4, 7);
                            if (!Clear(x, z, 2)) return;
                            var green = H("#3c9d5d");
                            Add(MeshTemplate.Trunk, x, gy + h / 2, z, new Vector3(1, h, 1), green);
                            Add(MeshTemplate.Trunk, x + 1.1f, gy + h * 0.55f, z, new Vector3(0.7f, 1.4f, 0.7f), green, 0, Mathf.PI / 2);
                            Add(MeshTemplate.Trunk, x + 1.8f, gy + h * 0.55f + 1, z, new Vector3(0.7f, 2, 0.7f), green);
                        }
                        else
                        {
                            float s = rnd.Range(8, 16);
                            if (!Clear(x, z, s)) return;
                            Add(MeshTemplate.Ball, x, gy, z, new Vector3(s, rnd.Range(2.5f, 5), s * rnd.Range(0.6f, 1)), rnd.Pick(Sand), rnd.Range(0, 6));
                        }
                        break;
                    }
                    case Scenery.Snow:
                    {
                        float k = rnd.Next();
                        if (k < 0.6f) { float h = rnd.Range(8, 14); if (Clear(x, z, h * 0.27f)) Pine(x, z, h, true); }
                        else if (k < 0.85f) { float h = rnd.Range(6, 13); if (Clear(x, z, 2)) Add(MeshTemplate.Pyramid, x, gy + h / 2, z, new Vector3(1.6f, h, 1.6f), H("#a8e0ff"), rnd.Range(0, 6)); }
                        else if (Clear(x, z, 2))
                        {
                            Add(MeshTemplate.Ball, x, gy + 1.4f, z, Vector3.one * 1.4f, Color.white);
                            Add(MeshTemplate.Ball, x, gy + 3.4f, z, Vector3.one, Color.white);
                            Add(MeshTemplate.Box, x, gy + 4.6f, z, new Vector3(1.1f, 0.8f, 1.1f), H("#2a2a3a"));
                        }
                        break;
                    }
                }
            }

            void Far(Scenery kind, float x, float z, float ry)
            {
                float gy = d.groundY;
                switch (kind)
                {
                    case Scenery.Jungle: { float s = rnd.Range(18, 30); if (Clear(x, z, s)) Add(MeshTemplate.Ball, x, gy, z, new Vector3(s, rnd.Range(10, 20), s), rnd.Pick(new[] { H("#2f8f46"), H("#3fa356"), H("#247a3b") })); break; }
                    case Scenery.Mountains: { float r = rnd.Range(22, 42); if (Clear(x, z, r)) Mountain(x, z, r, rnd.Range(45, 90), rnd.Pick(new[] { H("#6b7aa8"), H("#7d8bb5"), H("#5c6b94") })); break; }
                    case Scenery.City: { float bw = rnd.Range(10, 18); if (Clear(x, z, bw)) { float h = 80 + rnd.Range(0, 40); Add(MeshTemplate.Box, x, gy + h / 2, z, new Vector3(bw, h, bw), Color.white, ry, 0, Mats.Windows()); } break; }
                    case Scenery.Desert: { float r = rnd.Range(14, 26); if (Clear(x, z, r)) { float h = r * rnd.Range(1, 1.3f); Add(MeshTemplate.Pyramid, x, gy + h / 2, z, new Vector3(r, h, r), rnd.Chance(0.5f) ? H("#e0a95b") : H("#d99a4a"), rnd.Range(0, 1.5f)); } break; }
                    case Scenery.Snow: { float r = rnd.Range(24, 44); if (Clear(x, z, r)) Mountain(x, z, r, rnd.Range(50, 100), rnd.Pick(new[] { H("#c9dcf5"), H("#b4c9ea"), H("#dce8fa") })); break; }
                }
            }

            public void BuildScenery()
            {
                var xz = new List<float>();
                for (int i = 0; i < d.path.count; i += 12) { var p = d.path.SamplePosition(i); xz.Add(p.x); xz.Add(p.z); }
                pathXZ = xz.ToArray();
                scenery = new MeshAccumulator();
                int regions = d.biomes.Length;
                for (int k = 0; k < regions; k++)
                {
                    var bm = d.biomes[k];
                    float s0 = k * d.regionLen - (k == 0 ? 60 : 0);
                    float s1 = (k + 1) * d.regionLen + (k == regions - 1 ? Balance.RunOut + 60 : 0);
                    for (float s = s0; s < s1; s += rnd.Range(3.5f, 6.5f))
                        for (int side = -1; side <= 1; side += 2)
                            if (rnd.Chance(0.85f)) { var p = Spot(s, side, rnd.Range(10, 34)); Near(bm.scenery, p.x, p.z, p.w); }
                    for (float s = s0; s < s1; s += rnd.Range(22, 36))
                        for (int side = -1; side <= 1; side += 2) { var p = Spot(s, side, rnd.Range(55, 140)); Far(bm.scenery, p.x, p.z, p.w); }
                    for (float s = s0; s < s1; s += rnd.Range(35, 60))
                    {
                        var p = Spot(s, rnd.Chance(0.5f) ? -1 : 1, rnd.Range(30, 110));
                        Add(MeshTemplate.Ball, p.x, p.y + rnd.Range(22, 50), p.z, new Vector3(rnd.Range(7, 12), rnd.Range(2, 3.5f), rnd.Range(4, 7)), Color.white);
                    }
                    // Flush per region so no single mesh grows huge.
                    scenery.Build("Scenery " + bm.name, envRoot, false, false, d.owned);
                    scenery = new MeshAccumulator();
                }
            }

            // ------------------------------------------------------------ finish gate
            public void BuildFinish(float finishZ, float cell)
            {
                var fin = d.BiomeAt(finishZ);
                Box(envRoot, 0, 0.03f, finishZ + cell, new Vector3(Balance.TrackWidth, 0.05f, cell * 2), Mats.Checker(), false);
                var post = Mats.Stripes(fin.hazard, Color.white);
                for (int side = -1; side <= 1; side += 2)
                    Box(envRoot, side * (W2 + 0.75f), 2.4f, d.finishLine, new Vector3(0.7f, 4.8f, 0.7f), post);
                Box(envRoot, 0, 4.7f, d.finishLine, new Vector3(Balance.TrackWidth + 2.2f, 1, 0.5f), Mats.Checker());
            }
        }
    }
}
