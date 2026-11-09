using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rollzy
{
    /// <summary>
    /// Collects flat-shaded triangles into one mesh with a submesh per material, so a whole level's
    /// road or scenery draws in a handful of calls.
    /// </summary>
    public sealed class MeshAccumulator
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly Dictionary<Material, List<int>> tris = new Dictionary<Material, List<int>>();

        public int VertexCount => verts.Count;

        List<int> TrianglesFor(Material m)
        {
            if (!tris.TryGetValue(m, out var list)) tris[m] = list = new List<int>();
            return list;
        }

        /// <summary>Quad a-b-c-d, clockwise as seen from the side it should face.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Material m)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
            var t = TrianglesFor(m);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        /// <summary>Appends a template mesh transformed by a matrix.</summary>
        public void Add(MeshTemplate template, Matrix4x4 matrix, Material m)
        {
            int start = verts.Count;
            for (int k = 0; k < template.vertices.Length; k++)
            {
                verts.Add(matrix.MultiplyPoint3x4(template.vertices[k]));
                uvs.Add(template.uvs[k]);
            }
            var t = TrianglesFor(m);
            foreach (int idx in template.triangles) t.Add(start + idx);
        }

        public GameObject Build(string name, Transform parent, bool castShadows, bool receiveShadows, List<Object> owned)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = tris.Count;
            var materials = new Material[tris.Count];
            int k = 0;
            foreach (var kv in tris)
            {
                mesh.SetTriangles(kv.Value, k, false);
                materials[k++] = kv.Key;
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            owned?.Add(mesh);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = materials;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = receiveShadows;
            return go;
        }
    }

    /// <summary>Unshared-vertex (flat shaded) low-poly shapes, unit sized and centred.</summary>
    public sealed class MeshTemplate
    {
        public Vector3[] vertices;
        public Vector2[] uvs;
        public int[] triangles;

        static MeshTemplate box, blob, ball, cone7, cone4, cyl7, cyl12;
        public static MeshTemplate Box => box ??= MakeBox();
        public static MeshTemplate Blob => blob ??= MakeIco(0);
        public static MeshTemplate Ball => ball ??= MakeIco(1);
        public static MeshTemplate Cone => cone7 ??= MakeCone(9);
        public static MeshTemplate Pyramid => cone4 ??= MakeCone(4);
        public static MeshTemplate Trunk => cyl7 ??= MakeCylinder(7);
        public static MeshTemplate Pillar => cyl12 ??= MakeCylinder(12);

        sealed class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua = default, Vector2 ub = default, Vector2 uc = default)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                uv.Add(ua); uv.Add(ub); uv.Add(uc);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }
            public MeshTemplate Done() => new MeshTemplate { vertices = v.ToArray(), uvs = uv.ToArray(), triangles = t.ToArray() };
        }

        static MeshTemplate MakeBox()
        {
            var b = new Builder();
            Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var normal in n)
            {
                Vector3 u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 r = Vector3.Cross(u, normal);
                Vector3 c = normal * 0.5f;
                Vector3 p0 = c - r * 0.5f - u * 0.5f, p1 = c - r * 0.5f + u * 0.5f, p2 = c + r * 0.5f + u * 0.5f, p3 = c + r * 0.5f - u * 0.5f;
                // Clockwise from outside in Unity's left-handed space.
                b.Tri(p0, p1, p2, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1));
                b.Tri(p0, p2, p3, new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 0));
            }
            return FixWinding(b.Done(), Vector3.zero);
        }

        static MeshTemplate MakeCone(int sides)
        {
            var b = new Builder();
            Vector3 apex = new Vector3(0, 0.5f, 0), baseC = new Vector3(0, -0.5f, 0);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                var p0 = new Vector3(Mathf.Cos(a0), -0.5f, Mathf.Sin(a0));
                var p1 = new Vector3(Mathf.Cos(a1), -0.5f, Mathf.Sin(a1));
                b.Tri(apex, p0, p1);
                b.Tri(baseC, p1, p0);
            }
            return FixWinding(b.Done(), Vector3.zero);
        }

        static MeshTemplate MakeCylinder(int sides)
        {
            var b = new Builder();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                var b0 = new Vector3(Mathf.Cos(a0), -0.5f, Mathf.Sin(a0));
                var b1 = new Vector3(Mathf.Cos(a1), -0.5f, Mathf.Sin(a1));
                var t0 = b0 + Vector3.up; var t1 = b1 + Vector3.up;
                b.Tri(b0, t0, t1); b.Tri(b0, t1, b1);
                b.Tri(new Vector3(0, 0.5f, 0), t1, t0);
                b.Tri(new Vector3(0, -0.5f, 0), b0, b1);
            }
            return FixWinding(b.Done(), Vector3.zero);
        }

        static MeshTemplate MakeIco(int subdivisions)
        {
            float g = (1 + Mathf.Sqrt(5)) / 2;
            var p = new List<Vector3>
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1),
            };
            var faces = new List<int[]>
            {
                new[] {0,11,5}, new[] {0,5,1}, new[] {0,1,7}, new[] {0,7,10}, new[] {0,10,11},
                new[] {1,5,9}, new[] {5,11,4}, new[] {11,10,2}, new[] {10,7,6}, new[] {7,1,8},
                new[] {3,9,4}, new[] {3,4,2}, new[] {3,2,6}, new[] {3,6,8}, new[] {3,8,9},
                new[] {4,9,5}, new[] {2,4,11}, new[] {6,2,10}, new[] {8,6,7}, new[] {9,8,1},
            };
            for (int i = 0; i < p.Count; i++) p[i] = p[i].normalized;
            for (int s = 0; s < subdivisions; s++)
            {
                var next = new List<int[]>();
                var cache = new Dictionary<long, int>();
                int Mid(int a, int c)
                {
                    long key = a < c ? ((long)a << 32) | (uint)c : ((long)c << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int m)) return m;
                    p.Add(((p[a] + p[c]) * 0.5f).normalized);
                    return cache[key] = p.Count - 1;
                }
                foreach (var f in faces)
                {
                    int ab = Mid(f[0], f[1]), bc = Mid(f[1], f[2]), ca = Mid(f[2], f[0]);
                    next.Add(new[] { f[0], ab, ca }); next.Add(new[] { f[1], bc, ab });
                    next.Add(new[] { f[2], ca, bc }); next.Add(new[] { ab, bc, ca });
                }
                faces = next;
            }
            var b = new Builder();
            foreach (var f in faces) b.Tri(p[f[0]], p[f[1]], p[f[2]]);
            return FixWinding(b.Done(), Vector3.zero);
        }

        /// <summary>Makes every triangle face away from the centre (all shapes here are convex).</summary>
        static MeshTemplate FixWinding(MeshTemplate m, Vector3 centre)
        {
            for (int i = 0; i < m.triangles.Length; i += 3)
            {
                Vector3 a = m.vertices[m.triangles[i]], b = m.vertices[m.triangles[i + 1]], c = m.vertices[m.triangles[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                Vector3 mid = (a + b + c) / 3f - centre;
                // Unity treats clockwise (as seen from the front) as front-facing, which gives a
                // cross product pointing toward the viewer in its left-handed space.
                if (Vector3.Dot(n, mid) < 0)
                {
                    int tmp = m.triangles[i + 1];
                    m.triangles[i + 1] = m.triangles[i + 2];
                    m.triangles[i + 2] = tmp;
                }
            }
            return m;
        }
    }
}
