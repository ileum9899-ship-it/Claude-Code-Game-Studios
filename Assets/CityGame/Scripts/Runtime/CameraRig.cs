using UnityEngine;

namespace CityGame.Runtime
{
    /// <summary>
    /// Orbit camera around a ground focus point.
    /// WASD/arrows pan, Q/E or right-drag rotate, mouse wheel or +/- zoom, middle-drag pan.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        /// <summary>Point on the ground the camera looks at.</summary>
        public Vector3 Focus;
        /// <summary>Horizontal angle in degrees.</summary>
        public float Yaw = 45f;
        /// <summary>Vertical angle in degrees.</summary>
        public float Pitch = 50f;
        /// <summary>Distance from the focus.</summary>
        public float Distance = 30f;

        private float bounds = 48f;
        private Vector3 lastMouse;

        /// <summary>Limits panning to [0, size] on X and Z.</summary>
        public void SetBounds(float size)
        {
            bounds = size;
        }

        /// <summary>Applies one frame of camera input. <paramref name="allowMouse"/> is false while the pointer is over UI.</summary>
        public void Tick(float dt, bool allowMouse)
        {
            float panSpeed = Distance * 0.9f * dt;
            Vector3 fwd = Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, Yaw, 0f) * Vector3.right;
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move += fwd;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move -= fwd;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move += right;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move -= right;
            Focus += move * panSpeed;

            if (Input.GetKey(KeyCode.Q)) Yaw += 90f * dt;
            if (Input.GetKey(KeyCode.E)) Yaw -= 90f * dt;
            if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus)) Distance -= 30f * dt;
            if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus)) Distance += 30f * dt;

            Vector3 mouse = Input.mousePosition;
            Vector3 delta = mouse - lastMouse;
            lastMouse = mouse;
            if (allowMouse)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f) Distance *= 1f - wheel * 0.1f;
                if (Input.GetMouseButton(1))
                {
                    Yaw += delta.x * 0.3f;
                    Pitch -= delta.y * 0.2f;
                }
                if (Input.GetMouseButton(2))
                {
                    float k = Distance * 0.0025f;
                    Focus -= (right * delta.x + fwd * delta.y) * k;
                }
            }

            Distance = Mathf.Clamp(Distance, 6f, 80f);
            Pitch = Mathf.Clamp(Pitch, 20f, 85f);
            Focus.x = Mathf.Clamp(Focus.x, 0f, bounds);
            Focus.z = Mathf.Clamp(Focus.z, 0f, bounds);
            Focus.y = 0f;
            Apply();
        }

        /// <summary>Moves the transform to match Focus/Yaw/Pitch/Distance.</summary>
        public void Apply()
        {
            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            transform.position = Focus - rot * Vector3.forward * Distance;
            transform.rotation = rot;
        }
    }
}
