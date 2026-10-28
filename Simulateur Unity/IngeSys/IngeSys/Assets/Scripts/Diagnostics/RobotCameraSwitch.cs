using UnityEngine;

namespace IngeSys.Diagnostics
{
    /// <summary>V cycles through the base, first-person and rear chase views.</summary>
    [DisallowMultipleComponent]
    public sealed class RobotCameraSwitch : MonoBehaviour
    {
        public Camera baseCamera;
        public Camera fpsCamera;
        public Camera rearCamera;
        public KeyCode toggleKey = KeyCode.V;
        public int ViewIndex { get; private set; }
        public bool IsFirstPerson => ActiveCamera && ActiveCamera == fpsCamera;
        public Camera ActiveCamera => CameraAt(ViewIndex);

        void Awake() { Select(0); }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        public void Toggle()
        {
            for (int step = 1; step <= 3; step++)
            {
                int next = (ViewIndex + step) % 3;
                var camera = CameraAt(next);
                if (camera && camera.gameObject.activeInHierarchy) { Select(next); return; }
            }
        }

        Camera CameraAt(int index) => index == 1 ? fpsCamera : index == 2 ? rearCamera : baseCamera;

        void Select(int index)
        {
            if (!CameraAt(index)) return;
            ViewIndex = index;
            var active = ActiveCamera;
            SetEnabled(baseCamera, active);
            SetEnabled(fpsCamera, active);
            SetEnabled(rearCamera, active);
        }

        static void SetEnabled(Camera camera, Camera active)
        {
            if (!camera) return;
            camera.enabled = camera == active;
            var listener = camera.GetComponent<AudioListener>();
            if (listener) listener.enabled = camera.enabled;
        }

        void OnDisable() { Select(0); }
    }
}
