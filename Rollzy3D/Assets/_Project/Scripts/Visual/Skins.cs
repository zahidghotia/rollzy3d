using System.Collections.Generic;
using UnityEngine;

namespace Rollzy
{
    /// <summary>Procedural ball skins (512×256 equirectangular textures) and their materials.</summary>
    public static class Skins
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Color H(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        /// <summary>Main colour of each skin, used for crash particles.</summary>
        public static Color Tint(string id)
        {
            switch (id)
            {
                case "beach": return H("#ff4d4d");
                case "soccer": return Color.white;
                case "basket": return H("#e8772e");
                case "tennis": return H("#cfe83a");
                case "melon": return H("#2d8a3a");
                case "eight": return H("#444466");
                case "earth": return H("#2f7de1");
                case "smile": return H("#ffd23f");
                case "candy": return H("#ff4d8d");
                case "chrome": return H("#e8eef5");
                case "neon": return H("#00e5ff");
                case "lava": return H("#ff5a00");
                case "gold": return H("#ffc93c");
                case "galaxy": return H("#8a5cff");
                default: return H("#ff4f9a");
            }
        }

        public static Material Get(string id)
        {
            if (cache.TryGetValue(id, out var m)) return m;
            if (id == "gold") return cache[id] = Mats.Gold();
            if (id == "chrome") return cache[id] = Mats.Chrome();

            var p = Draw(id);
            var tex = p.ToTexture();
            float smooth = id == "eight" ? 0.9f : id == "classic" ? 0.75f : id == "lava" ? 0.3f : 0.6f;
            float glow = id == "neon" ? 1.2f : id == "lava" ? 1.4f : id == "galaxy" ? 0.7f : 0f;
            m = Mats.Textured("skin_" + id, tex, smooth, 0f, glow > 0 ? tex : null, glow);
            return cache[id] = m;
        }

        static Painter Draw(string id)
        {
            Painter p;
            switch (id)
            {
                case "beach":
                {
                    p = new Painter(512, 256, Color.white);
                    string[] cols = { "#ff4d4d", "#ffffff", "#2f80ed", "#ffd23f", "#ffffff", "#27ae60" };
                    for (int i = 0; i < 6; i++) p.Rect(i * 512f / 6, 0, 512f / 6 + 1, 256, H(cols[i]));
                    p.Rect(0, 0, 512, 24, Color.white); p.Rect(0, 232, 512, 24, Color.white);
                    return p;
                }
                case "soccer":
                {
                    p = new Painter(512, 256, H("#fafafa"));
                    var ink = H("#1d1d26");
                    void Pent(float cx, float cy, float r, float sx)
                    {
                        var pts = new Vector2[5];
                        for (int i = 0; i < 5; i++) { float a = -Mathf.PI / 2 + i * 2 * Mathf.PI / 5; pts[i] = new Vector2(cx + Mathf.Cos(a) * r * sx, cy + Mathf.Sin(a) * r); }
                        p.Polygon(pts, ink);
                    }
                    for (int k = 0; k < 5; k++) { Pent(k * 102.4f + 51, 128, 30, 1); Pent(k * 102.4f, 52, 22, 1.6f); Pent(k * 102.4f, 204, 22, 1.6f); }
                    p.Rect(0, 0, 512, 12, ink); p.Rect(0, 244, 512, 12, ink);
                    return p;
                }
                case "basket":
                {
                    p = new Painter(512, 256, H("#e8772e"));
                    var line = H("#2a1408");
                    p.Line(0, 128, 512, 128, 6, line);
                    foreach (float x in new[] { 0f, 256f, 511f }) p.Line(x, 0, x, 256, 6, line);
                    foreach (float off in new[] { 128f, 384f })
                        for (int y = 0; y < 256; y += 4)
                            p.Line(off + Mathf.Sin(y / 256f * Mathf.PI) * 60, y, off + Mathf.Sin((y + 4) / 256f * Mathf.PI) * 60, y + 4, 6, line);
                    return p;
                }
                case "tennis":
                {
                    p = new Painter(512, 256, H("#cfe83a"));
                    for (int x = 0; x < 512; x += 3)
                        p.Line(x, 128 + Mathf.Sin(x / 512f * 4 * Mathf.PI) * 62, x + 3, 128 + Mathf.Sin((x + 3) / 512f * 4 * Mathf.PI) * 62, 12, Color.white);
                    return p;
                }
                case "melon":
                {
                    p = new Painter(512, 256, H("#2d8a3a"));
                    var light = H("#9ad16b");
                    for (int k = 0; k < 12; k++)
                        for (int y = 0; y < 256; y += 8)
                            p.Line(k * 42.7f + Mathf.Sin(y * 0.12f + k) * 7, y, k * 42.7f + Mathf.Sin((y + 8) * 0.12f + k) * 7, y + 8, 12, light);
                    return p;
                }
                case "eight":
                {
                    p = new Painter(512, 256, H("#14141f"));
                    p.Ellipse(384, 128, 40, 48, Color.white);
                    var ink = H("#14141f");
                    // A drawn "8": two rings.
                    p.Ellipse(384, 113, 13, 13, ink); p.Ellipse(384, 113, 6, 6, Color.white);
                    p.Ellipse(384, 144, 16, 16, ink); p.Ellipse(384, 144, 8, 8, Color.white);
                    return p;
                }
                case "earth":
                {
                    p = new Painter(512, 256, H("#2f7de1"));
                    var r = new Rng(7);
                    for (int c = 0; c < 9; c++)
                    {
                        float cx = r.Next() * 512, cy = 50 + r.Next() * 156;
                        for (int k = 0; k < 14; k++) p.Circle(cx + (r.Next() - 0.5f) * 70, cy + (r.Next() - 0.5f) * 40, 8 + r.Next() * 16, k % 3 == 0 ? H("#7cc46a") : H("#3fae4f"));
                    }
                    p.Rect(0, 0, 512, 20, H("#f4fbff")); p.Rect(0, 236, 512, 20, H("#f4fbff"));
                    return p;
                }
                case "smile":
                {
                    p = new Painter(512, 256, H("#ffd23f"));
                    var ink = H("#3a2410");
                    p.Ellipse(360, 100, 11, 18, ink); p.Ellipse(408, 100, 11, 18, ink);
                    for (float a = 0.18f * Mathf.PI; a < 0.82f * Mathf.PI; a += 0.05f)
                        p.Circle(384 + Mathf.Cos(a) * 42, 124 + Mathf.Sin(a) * 42, 4.5f, ink);
                    p.Circle(336, 140, 12, new Color(1f, 0.4f, 0.47f, 0.45f)); p.Circle(432, 140, 12, new Color(1f, 0.4f, 0.47f, 0.45f));
                    return p;
                }
                case "candy":
                {
                    p = new Painter(512, 256, Color.white);
                    p.Stripes(0.5f, 112, 26, H("#ff4d8d"));
                    p.Shade((x, y) => { float u = x * Mathf.Cos(0.5f) + y * Mathf.Sin(0.5f) + 56; float m = ((u % 112) + 112) % 112; return m < 26 ? H("#ff9ec4") : new Color(0, 0, 0, 0); });
                    return p;
                }
                case "neon":
                {
                    p = new Painter(512, 256, H("#0b0b1e"));
                    for (int x = 0; x <= 512; x += 32) p.Rect(x - 2, 0, 4, 256, H("#00e5ff"));
                    for (int y = 0; y <= 256; y += 32) p.Rect(0, y - 2, 512, 4, H("#ff3d9a"));
                    return p;
                }
                case "lava":
                {
                    p = new Painter(512, 256, H("#1c0805"));
                    var r = new Rng(11);
                    for (int k = 0; k < 40; k++)
                    {
                        float x = r.Next() * 512, y = r.Next() * 256, w = 3 + r.Next() * 5;
                        var c = r.Chance(0.5f) ? H("#ff5a00") : H("#ffb000");
                        for (int s = 0; s < 6; s++) { float nx = x + (r.Next() - 0.5f) * 60, ny = y + (r.Next() - 0.5f) * 40; p.Line(x, y, nx, ny, w, c); x = nx; y = ny; }
                    }
                    return p;
                }
                case "galaxy":
                {
                    p = new Painter(512, 256, H("#1a0b3d"));
                    p.Shade((x, y) => Color.Lerp(H("#1a0b3d"), H("#3b1677"), Mathf.PingPong((x + y) / 384f, 1f)));
                    var r = new Rng(3);
                    for (int k = 0; k < 8; k++) p.Circle(r.Next() * 512, r.Next() * 256, 30 + r.Next() * 50, new Color(1f, 0.3f + r.Next() * 0.4f, 0.8f, 0.18f));
                    for (int k = 0; k < 260; k++) { float s = r.Chance(0.1f) ? 1.6f : 0.9f; p.Circle(r.Next() * 512, r.Next() * 256, s, new Color(1, 1, 1, 0.4f + r.Next() * 0.6f)); }
                    return p;
                }
                default: // classic
                {
                    p = new Painter(512, 256, H("#ff4f9a"));
                    p.Rect(0, 116, 512, 24, Color.white);
                    p.Rect(116, 0, 24, 256, Color.white);
                    p.Rect(372, 0, 24, 256, Color.white);
                    return p;
                }
            }
        }
    }
}
