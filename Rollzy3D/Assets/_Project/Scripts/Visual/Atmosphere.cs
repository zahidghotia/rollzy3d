using UnityEngine;
using UnityEngine.Rendering;

namespace Rollzy
{
    /// <summary>
    /// Blends sky, fog, ambient light and ground colour between locations as the ball travels.
    /// The sky is a small gradient texture on a panoramic skybox (skyboxes ignore fog).
    /// </summary>
    public sealed class Atmosphere
    {
        readonly Material skybox;
        readonly Texture2D gradient;
        readonly Color[] column = new Color[64];
        readonly Material groundMat;
        readonly Transform ground;

        public Atmosphere(Material skyboxTemplate, Material groundMaterial)
        {
            gradient = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            skybox = new Material(skyboxTemplate);
            skybox.SetTexture("_MainTex", gradient);
            RenderSettings.skybox = skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 70;
            RenderSettings.fogEndDistance = 420;
            RenderSettings.ambientMode = AmbientMode.Trilight;

            groundMat = groundMaterial;
            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.Destroy(g.GetComponent<Collider>());
            g.name = "Ground";
            g.transform.localScale = new Vector3(300, 1, 300);
            g.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
            g.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            ground = g.transform;
        }

        public void Apply(LevelData level, float s, Vector3 ballWorld)
        {
            var bs = level.biomes;
            Color top = bs[0].skyTop, bottom = bs[0].skyBottom, fog = bs[0].fog, gnd = bs[0].ground;
            for (int k = 1; k < bs.Length; k++)
            {
                float edge = k * level.regionLen;
                float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(edge - 30, edge + 30, s));
                if (t <= 0) break;
                top = Color.Lerp(top, bs[k].skyTop, t);
                bottom = Color.Lerp(bottom, bs[k].skyBottom, t);
                fog = Color.Lerp(fog, bs[k].fog, t);
                gnd = Color.Lerp(gnd, bs[k].ground, t);
            }

            // v: 0 = straight down, 0.5 = horizon, 1 = straight up.
            for (int i = 0; i < column.Length; i++)
            {
                float v = i / (column.Length - 1f);
                float h = Mathf.Clamp01((v - 0.5f) * 2f * 1.6f + 0.12f);
                column[i] = v < 0.5f ? Color.Lerp(fog, gnd, (0.5f - v) * 1.2f) : Color.Lerp(bottom, top, Mathf.Pow(h, 0.75f));
            }
            gradient.SetPixels(column);
            gradient.Apply(false);

            RenderSettings.fogColor = fog;
            RenderSettings.ambientSkyColor = Color.Lerp(top, Color.white, 0.6f) * 0.9f;
            RenderSettings.ambientEquatorColor = Color.Lerp(bottom, Color.white, 0.3f) * 0.8f;
            RenderSettings.ambientGroundColor = gnd * 0.6f;
            groundMat.SetColor("_BaseColor", gnd);
            ground.position = new Vector3(ballWorld.x, level.groundY, ballWorld.z);
        }
    }
}
