using UnityEngine;

namespace Rollzy
{
    /// <summary>
    /// Chase camera that follows the road's own "up" (so it rolls through corkscrews and loops),
    /// widens the view on tall phones, kicks the FOV with speed and shakes on a crash.
    /// </summary>
    public sealed class CameraRig
    {
        readonly Camera cam;
        Vector3 up = Vector3.up;
        float baseFov = 60, orbit;
        public float shake;

        public CameraRig(Camera camera) { cam = camera; }

        public void UpdateFov(float speed, bool playing, float dt)
        {
            float vfov = 2 * Mathf.Atan(Mathf.Tan(50 * Mathf.Deg2Rad / 2) / cam.aspect) * Mathf.Rad2Deg;
            baseFov = Mathf.Clamp(vfov, 55, 78);
            float want = baseFov + (playing ? Mathf.Clamp((speed - 12) * 0.55f, 0, 10) : 0);
            cam.fieldOfView = dt <= 0 ? want : Mathf.Lerp(cam.fieldOfView, want, 1 - Mathf.Exp(-4 * dt));
        }

        public void Follow(TrackPath path, float x, float y, float s, float dt, bool snap)
        {
            float fx = x * 0.45f, fy = Mathf.Max(y - Balance.BallRadius, -1) * 0.5f;
            Vector3 pos = path.ToWorld(fx, fy + 6.5f, s - 8);
            Vector3 look = path.ToWorld(fx, fy + 0.5f, s + 8);
            path.Frame(s, out _, out var q);
            Vector3 targetUp = q * Vector3.up;

            float k = snap ? 1 : 1 - Mathf.Exp(-10 * dt);
            var t = cam.transform;
            t.position = Vector3.Lerp(t.position, pos, k);
            up = Vector3.Slerp(up, targetUp, k).normalized;
            t.rotation = Quaternion.LookRotation(look - t.position, up);

            if (shake > 0)
            {
                shake = Mathf.Max(0, shake - dt * 1.6f);
                t.position += Random.insideUnitSphere * shake * 0.6f;
            }
        }

        /// <summary>Slow orbit around the ball for the home screen.</summary>
        public void Orbit(Vector3 target, float dt)
        {
            orbit += dt * 0.18f;
            const float r = 6.2f;
            var t = cam.transform;
            var want = target + new Vector3(Mathf.Sin(orbit) * r, 2.4f, -Mathf.Cos(orbit) * r);
            t.position = Vector3.Lerp(t.position, want, 1 - Mathf.Exp(-3 * dt));
            up = Vector3.up;
            t.rotation = Quaternion.LookRotation(target + Vector3.up * 0.9f - t.position, Vector3.up);
        }
    }
}
