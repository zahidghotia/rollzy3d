using System.Collections.Generic;
using UnityEngine;

namespace Rollzy
{
    /// <summary>Renders each ball skin once into a small texture for the shop.</summary>
    public sealed class Thumbnails
    {
        const int Layer = 31, Size = 192;
        readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        readonly Camera cam;
        readonly MeshRenderer ball;
        readonly RenderTexture rt;

        public Thumbnails(Mesh sphere)
        {
            var root = new GameObject("ThumbnailStage");
            root.transform.position = new Vector3(0, -5000, 0);

            var b = new GameObject("Ball");
            b.layer = Layer;
            b.transform.SetParent(root.transform, false);
            b.transform.localRotation = Quaternion.Euler(14, -40, 0);
            b.AddComponent<MeshFilter>().sharedMesh = sphere;
            ball = b.AddComponent<MeshRenderer>();

            var c = new GameObject("ThumbCamera");
            c.transform.SetParent(root.transform, false);
            c.transform.localPosition = new Vector3(0, 0.3f, -3.4f);
            c.transform.LookAt(root.transform.position);
            cam = c.AddComponent<Camera>();
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.fieldOfView = 28;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 10;
            cam.enabled = false;
            rt = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
        }

        public Texture2D Get(string skinId)
        {
            if (cache.TryGetValue(skinId, out var t)) return t;
            ball.sharedMaterial = Skins.Get(skinId);
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            t = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            return cache[skinId] = t;
        }
    }
}
