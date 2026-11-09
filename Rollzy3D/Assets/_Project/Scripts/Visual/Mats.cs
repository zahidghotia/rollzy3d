using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rollzy
{
    /// <summary>
    /// Material factory. Every runtime material is a copy of a template created by the editor setup
    /// (so the URP shaders are guaranteed to be included in the Android build).
    /// </summary>
    public static class Mats
    {
        static Material lit, unlit, particle;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static void Init(Material litTemplate, Material unlitTemplate, Material particleTemplate)
        {
            lit = litTemplate;
            unlit = unlitTemplate;
            particle = particleTemplate;
            if (lit == null) Debug.LogError("[Rollzy] Lit template material missing. Run Rollzy 3D → 1. Create Gameplay Scene.");
        }

        static string Key(string kind, Color c, float a = 0, float b = 0) => kind + ColorUtility.ToHtmlStringRGBA(c) + a.ToString("F2") + b.ToString("F2");

        public static Material Lit(Color color, float smoothness = 0.35f, float metallic = 0f)
        {
            string k = Key("lit", color, smoothness, metallic);
            if (cache.TryGetValue(k, out var m)) return m;
            m = new Material(lit) { name = "Lit_" + ColorUtility.ToHtmlStringRGB(color) };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;
            return cache[k] = m;
        }

        public static Material Textured(string key, Texture2D tex, float smoothness = 0.35f, float metallic = 0f, Texture2D glow = null, float glowStrength = 1f)
        {
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(lit) { name = key };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (glow != null)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", glow);
                m.SetColor("_EmissionColor", Color.white * glowStrength);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return cache[key] = m;
        }

        /// <summary>Glowing, unaffected by light (neon strips).</summary>
        public static Material Unlit(Color color)
        {
            string k = Key("unlit", color);
            if (cache.TryGetValue(k, out var m)) return m;
            m = new Material(unlit) { name = "Unlit_" + ColorUtility.ToHtmlStringRGB(color) };
            m.SetColor("_BaseColor", color);
            return cache[k] = m;
        }

        public static Material Particles(Texture2D sprite)
        {
            var m = new Material(particle) { name = "Particles" };
            m.SetTexture("_BaseMap", sprite);
            return m;
        }

        public static Material Gold() => Glossy("gold", new Color(1f, 0.79f, 0.16f), new Color(0.45f, 0.27f, 0f));
        public static Material Chrome() => Glossy("chrome", new Color(0.86f, 0.9f, 0.95f), new Color(0.12f, 0.13f, 0.16f));

        static Material Glossy(string key, Color color, Color glow)
        {
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(lit) { name = key };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.85f);
            m.SetFloat("_Metallic", 0.6f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", glow);
            return cache[key] = m;
        }

        /// <summary>Diagonal hazard stripes with a white border, used on obstacles.</summary>
        public static Material Stripes(Color main, Color alt)
        {
            string k = "stripe" + ColorUtility.ToHtmlStringRGB(main) + ColorUtility.ToHtmlStringRGB(alt);
            if (cache.TryGetValue(k, out var m)) return m;
            var p = new Painter(128, 128, main);
            p.Stripes(Mathf.PI / 4, 36, 18, alt);
            var white = new Color(1, 1, 1, 0.9f);
            p.Rect(0, 0, 128, 9, white); p.Rect(0, 119, 128, 9, white);
            p.Rect(0, 0, 9, 128, white); p.Rect(119, 0, 9, 128, white);
            return Textured(k, p.ToTexture(false), 0.6f);
        }

        public static Material Checker()
        {
            const string k = "checker";
            if (cache.TryGetValue(k, out var m)) return m;
            var p = new Painter(256, 64, Color.white);
            var ink = new Color(0.11f, 0.09f, 0.25f);
            for (int x = 0; x < 16; x++) for (int y = 0; y < 4; y++) if ((x + y) % 2 == 1) p.Rect(x * 16, y * 16, 16, 16, ink);
            var tex = p.ToTexture(false);
            tex.filterMode = FilterMode.Point;
            return Textured(k, tex, 0.4f);
        }

        public static Material Windows()
        {
            const string k = "windows";
            if (cache.TryGetValue(k, out var m)) return m;
            var rng = new Rng(42);
            var baseP = new Painter(64, 128, new Color(0.93f, 0.91f, 0.97f));
            var glowP = new Painter(64, 128, Color.black);
            for (int row = 0; row < 12; row++)
                for (int col = 0; col < 5; col++)
                {
                    bool on = rng.Chance(0.55f);
                    float x = 4 + col * 12, y = 4 + row * 10;
                    baseP.Rect(x, y, 8, 6, on ? new Color(1f, 0.96f, 0.77f) : new Color(0.3f, 0.32f, 0.5f));
                    if (on) glowP.Rect(x, y, 8, 6, new Color(1f, 0.81f, 0.45f));
                }
            return Textured(k, baseP.ToTexture(false), 0.5f, 0, glowP.ToTexture(false), 0.9f);
        }

        public static Texture2D SoftDot()
        {
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var cols = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32)) / 32f;
                    float a = Mathf.Clamp01(1 - d);
                    cols[y * 64 + x] = new Color(1, 1, 1, a * a * (d < 0.35f ? 1 : 0.85f));
                }
            t.SetPixels(cols);
            t.Apply();
            return t;
        }
    }
}
