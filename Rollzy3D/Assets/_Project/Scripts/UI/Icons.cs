using System.Collections.Generic;
using UnityEngine;

namespace Rollzy
{
    /// <summary>Simple white icons drawn in code (tinted in USS), so the UI needs no image files.</summary>
    public static class Icons
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly Color Clear = new Color(1, 1, 1, 0);
        const int S = 96;

        public static Texture2D Get(string name)
        {
            if (cache.TryGetValue(name, out var t)) return t;
            var p = new Painter(S, S, Clear);
            var w = Color.white;
            switch (name)
            {
                case "pause": p.Rect(24, 18, 16, 60, w); p.Rect(56, 18, 16, 60, w); break;
                case "play": p.Polygon(new[] { new Vector2(26, 14), new Vector2(82, 48), new Vector2(26, 82) }, w); break;
                case "back": p.Line(60, 18, 32, 48, 13, w); p.Line(32, 48, 60, 78, 13, w); break;
                case "settings":
                    for (int i = 0; i < 3; i++) { float y = 22 + i * 26; p.Rect(10, y - 5, 76, 10, w); p.Circle(i == 1 ? 64 : 30, y, 12, w); }
                    break;
                case "map":
                    p.Polygon(new[] { new Vector2(10, 22), new Vector2(34, 12), new Vector2(62, 22), new Vector2(86, 12), new Vector2(86, 74), new Vector2(62, 84), new Vector2(34, 74), new Vector2(10, 84) }, w);
                    p.overwrite = true; p.Rect(32, 14, 4, 62, Clear); p.Rect(60, 22, 4, 62, Clear); p.overwrite = false;
                    break;
                case "ball":
                    p.Circle(48, 48, 38, w);
                    p.overwrite = true;
                    for (int x = 12; x < 84; x += 2) { p.Circle(x, 38 + (x - 48) * (x - 48) / 120f, 2.2f, Clear); p.Circle(x, 58 - (x - 48) * (x - 48) / 120f, 2.2f, Clear); }
                    p.overwrite = false;
                    break;
                case "gift":
                    p.Rect(16, 40, 64, 46, w); p.Rect(10, 28, 76, 14, w); p.overwrite = true; p.Rect(44, 28, 8, 58, Clear); p.overwrite = false;
                    p.Circle(36, 22, 10, w); p.Circle(60, 22, 10, w);
                    break;
                case "wheel":
                    p.Circle(48, 48, 40, w); p.overwrite = true; p.Circle(48, 48, 32, Clear); p.overwrite = false;
                    for (int k = 0; k < 4; k++) { float a = k * Mathf.PI / 4; p.Line(48 - Mathf.Cos(a) * 32, 48 - Mathf.Sin(a) * 32, 48 + Mathf.Cos(a) * 32, 48 + Mathf.Sin(a) * 32, 6, w); }
                    p.Circle(48, 48, 9, w);
                    break;
                case "lock":
                    p.Rect(22, 44, 52, 42, w);
                    for (float a = 0; a <= Mathf.PI; a += 0.05f) p.Circle(48 + Mathf.Cos(a) * 18, 44 - Mathf.Sin(a) * 18, 5, w);
                    break;
                case "star":
                {
                    var pts = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float a = -Mathf.PI / 2 + i * Mathf.PI / 5, r = i % 2 == 0 ? 44 : 19;
                        pts[i] = new Vector2(48 + Mathf.Cos(a) * r, 52 + Mathf.Sin(a) * r);
                    }
                    p.Polygon(pts, w);
                    break;
                }
                case "pin": p.Polygon(new[] { new Vector2(8, 10), new Vector2(88, 10), new Vector2(48, 88) }, w); break;
                case "dot": p.Circle(48, 48, 46, w); break;
            }
            t = p.ToTexture(false, false);
            t.wrapMode = TextureWrapMode.Clamp;
            return cache[name] = t;
        }

        /// <summary>Spin wheel face: coloured segments with white dividers.</summary>
        public static Texture2D Wheel(Prize[] prizes)
        {
            const int size = 512;
            var p = new Painter(size, size, Clear);
            float c = size / 2f, r = c - 4;
            int n = prizes.Length;
            p.Shade((x, y) =>
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > r) return Clear;
                if (d > r - 12) return new Color(0.11f, 0.09f, 0.25f);
                // Angle measured clockwise from the top, matching the label layout.
                float a = Mathf.Atan2(dx, -dy);
                if (a < 0) a += Mathf.PI * 2;
                float seg = a / (Mathf.PI * 2) * n;
                int i = Mathf.FloorToInt(seg) % n;
                float edge = Mathf.Min(seg - Mathf.Floor(seg), Mathf.Ceil(seg) - seg) * (Mathf.PI * 2 / n) * d;
                return edge < 3 ? Color.white : prizes[i].color;
            });
            var t = p.ToTexture(false, false);
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }
    }
}
