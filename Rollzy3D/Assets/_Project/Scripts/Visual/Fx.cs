using UnityEngine;

namespace Rollzy
{
    /// <summary>Particles: ball trails, coin sparkles, crash bursts and finish-line confetti.</summary>
    public sealed class Fx : MonoBehaviour
    {
        ParticleSystem floaty, falling;

        public void Init(Material particleMaterial)
        {
            floaty = Make("Trail", particleMaterial, 0f);
            falling = Make("Bursts", particleMaterial, 1f);
        }

        ParticleSystem Make(string name, Material m, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; // keeps the system alive; particles come only from Emit()
            main.playOnAwake = false;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startSpeed = 0;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.3f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, Color c, float life, float size)
        {
            var e = new ParticleSystem.EmitParams
            {
                position = pos, velocity = vel, startColor = c, startLifetime = life, startSize = size,
                applyShapeToPosition = false,
            };
            ps.Emit(e, 1);
        }

        public void Trail(Vector3 pos, Color c)
        {
            Emit(floaty, pos + Random.insideUnitSphere * 0.15f, Random.insideUnitSphere * 0.3f, c, 0.32f, 0.4f);
        }

        public void Sparkle(Vector3 pos)
        {
            for (int i = 0; i < 8; i++)
                Emit(falling, pos, Random.onUnitSphere * Random.Range(1.5f, 3.5f) + Vector3.up, new Color(1f, 0.85f, 0.3f), Random.Range(0.3f, 0.55f), 0.3f);
        }

        public void Crash(Vector3 pos, Color c)
        {
            for (int i = 0; i < 40; i++)
            {
                var v = Random.onUnitSphere * Random.Range(3f, 7f);
                v.y = Mathf.Abs(v.y) + 2;
                Emit(falling, pos, v, i % 3 == 0 ? Color.white : c, Random.Range(0.5f, 1f), 0.45f);
            }
        }

        public void Confetti(Vector3 pos)
        {
            for (int i = 0; i < 140; i++)
            {
                var c = Color.HSVToRGB(Random.value, 0.8f, 1f);
                var p = pos + new Vector3(Random.Range(-3f, 3f), 1, Random.Range(0f, 3f));
                var v = new Vector3(Random.Range(-4f, 4f), Random.Range(6f, 14f), Random.Range(-4f, 4f));
                Emit(falling, p, v, c, Random.Range(1.6f, 2.6f), 0.35f);
            }
        }
    }
}
