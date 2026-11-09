using UnityEngine;

namespace Rollzy
{
    /// <summary>
    /// Tiny CPU canvas for procedural textures (ball skins, stripes, the spin wheel).
    /// Coordinates match an HTML canvas: (0,0) is the top-left pixel.
    /// </summary>
    public sealed class Painter
    {
        public readonly int width, height;
        /// <summary>When true, drawing replaces pixels instead of blending (use with a transparent colour to erase).</summary>
        public bool overwrite;
        readonly Color32[] px;

        public Painter(int width, int height, Color fill)
        {
            this.width = width;
            this.height = height;
            px = new Color32[width * height];
            Color32 c = fill;
            for (int i = 0; i < px.Length; i++) px[i] = c;
        }

        void Blend(int x, int y, Color c, bool wrapX)
        {
            if (wrapX) x = ((x % width) + width) % width;
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            int i = (height - 1 - y) * width + x;
            if (overwrite || c.a >= 0.999f) { px[i] = c; return; }
            Color dst = px[i];
            px[i] = Color.Lerp(dst, new Color(c.r, c.g, c.b, 1), c.a);
        }

        public void Rect(float x, float y, float w, float h, Color c)
        {
            for (int yy = Mathf.FloorToInt(y); yy < Mathf.CeilToInt(y + h); yy++)
                for (int xx = Mathf.FloorToInt(x); xx < Mathf.CeilToInt(x + w); xx++)
                    Blend(xx, yy, c, false);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color c, bool wrapX = true)
        {
            for (int yy = Mathf.FloorToInt(cy - ry); yy <= Mathf.CeilToInt(cy + ry); yy++)
                for (int xx = Mathf.FloorToInt(cx - rx); xx <= Mathf.CeilToInt(cx + rx); xx++)
                {
                    float dx = (xx + 0.5f - cx) / rx, dy = (yy + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1) Blend(xx, yy, c, wrapX);
                }
        }

        public void Circle(float cx, float cy, float r, Color c) => Ellipse(cx, cy, r, r, c);

        public void Line(float x0, float y0, float x1, float y1, float lineWidth, Color c)
        {
            float len = Mathf.Max(1, Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)));
            int steps = Mathf.CeilToInt(len);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Circle(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), lineWidth * 0.5f, c);
            }
        }

        public void Polygon(Vector2[] pts, Color c)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
            {
                float sy = y + 0.5f;
                var xs = new System.Collections.Generic.List<float>();
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                    if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy))
                        xs.Add(a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                    for (int x = Mathf.CeilToInt(xs[k] - 0.5f); x <= Mathf.FloorToInt(xs[k + 1] - 0.5f); x++)
                        Blend(x, y, c, true);
            }
        }

        /// <summary>Diagonal stripes across the whole canvas.</summary>
        public void Stripes(float angle, float period, float stripeWidth, Color c)
        {
            float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float u = x * ca + y * sa;
                    float m = ((u % period) + period) % period;
                    if (m < stripeWidth) Blend(x, y, c, false);
                }
        }

        /// <summary>Calls fill(x, y) for every pixel and blends the returned colour.</summary>
        public void Shade(System.Func<int, int, Color> fill)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var c = fill(x, y);
                    if (c.a > 0) Blend(x, y, c, false);
                }
        }

        public Texture2D ToTexture(bool repeatX = true, bool mipmaps = true)
        {
            var t = new Texture2D(width, height, TextureFormat.RGBA32, mipmaps);
            t.SetPixels32(px);
            t.Apply(mipmaps, false);
            t.wrapModeU = repeatX ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.wrapModeV = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = 4;
            return t;
        }
    }
}
