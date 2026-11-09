using NeonHarbor.Core;
using UnityEngine;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Third-person orbit camera: mouse orbits, wheel zooms; behind the shoulder on
    /// foot, swings behind the car when driving, widens FOV with speed, shakes on
    /// impacts and pulls in front of buildings it would clip through.
    /// </summary>
    public sealed class CameraRig
    {
        private readonly Camera cam;
        private readonly CityLayout city;
        private Vector3 pos;
        private float idle, fov = 60, zoom = 1;

        /// <summary>Horizontal angle; forward = (sin, cos).</summary>
        public float Yaw = Mathf.PI;
        /// <summary>Elevation (rad).</summary>
        public float Pitch = 0.28f;
        /// <summary>Shake strength 0..1 (decays).</summary>
        public float Shake;

        /// <summary>Wraps a camera.</summary>
        public CameraRig(Camera cam, CityLayout city) { this.cam = cam; this.city = city; }

        /// <summary>Snaps to a point (respawn).</summary>
        public void Snap(Vector3 p) { pos = p + new Vector3(0, 4, -6); }

        /// <summary>Updates the camera for the player state.</summary>
        public void Tick(float dt, PlayerController player, bool mouseLook)
        {
            float mx = mouseLook ? Input.GetAxisRaw("Mouse X") : 0, my = mouseLook ? Input.GetAxisRaw("Mouse Y") : 0;
            if (Mathf.Abs(mx) + Mathf.Abs(my) > 0.001f) idle = 0;
            Yaw += mx * 0.045f; Pitch = Mathf.Clamp(Pitch - my * 0.03f, -0.25f, 1.2f);
            zoom = Mathf.Clamp(zoom - Input.mouseScrollDelta.y * 0.08f, 0.6f, 1.8f);
            idle += dt;
            Vector3 target; float dist; bool inCar = player.Car != null;
            if (inCar)
            {
                var c = player.Car; float sp = c.Speed;
                if (idle > 1.2f && sp > 2)
                {
                    float behind = c.Model.ForwardSpeed < -1 ? c.Model.Yaw + Mathf.PI : c.Model.Yaw;
                    Yaw += Mathx.AngleDiff(Yaw, behind) * (1 - Mathf.Exp(-3 * dt));
                    Pitch = Mathx.Damp(Pitch, 0.2f, 2, dt);
                }
                dist = (7.8f + c.Spec.Length * 0.3f + sp * 0.04f) * zoom;
                target = c.Position + Vector3.up * 1.4f;
                fov = Mathx.Damp(fov, 60 + Mathf.Clamp01(sp / 45) * 16, 3, dt);
            }
            else
            {
                dist = 4.4f * zoom; target = player.Position + Vector3.up * 1.55f;
                fov = Mathx.Damp(fov, 58, 3, dt);
            }
            float cp = Mathf.Cos(Pitch), spch = Mathf.Sin(Pitch);
            Vector3 fwd = new Vector3(Mathf.Sin(Yaw), 0, Mathf.Cos(Yaw)), right = new Vector3(Mathf.Cos(Yaw), 0, -Mathf.Sin(Yaw));
            Vector3 want = target - fwd * cp * dist + Vector3.up * (spch * dist + (inCar ? 0.6f : 0.25f));
            if (!inCar) { want += right * 0.55f; target += right * 0.55f; } // over the right shoulder
            float t = city.Raycast(target.x, target.y, target.z, want.x, want.y, want.z);
            if (t < 1) want = Vector3.Lerp(target, want, Mathf.Max(0.12f, t - 0.05f));
            want.y = Mathf.Max(want.y, city.GroundHeight(want.x, want.z) + 0.4f);
            pos = Vector3.Lerp(pos, want, 1 - Mathf.Exp(-(inCar ? 14 : 20) * dt));
            if ((pos - want).sqrMagnitude > 900) pos = want;
            Vector3 p = pos;
            if (Shake > 0) { p += Random.insideUnitSphere * Shake * 0.3f; Shake = Mathf.Max(0, Shake - dt * 2.5f); }
            cam.transform.position = p;
            cam.transform.rotation = Quaternion.LookRotation(target - p, Vector3.up);
            cam.fieldOfView = fov;
        }
    }
}
