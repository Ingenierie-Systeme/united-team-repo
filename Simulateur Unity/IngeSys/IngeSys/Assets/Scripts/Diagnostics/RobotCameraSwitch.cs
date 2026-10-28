using UnityEngine;

namespace IngeSys.Diagnostics
{
    /// <summary>V alterne la caméra de suivi et la caméra embarquée, sans changer le pilotage.</summary>
    [DisallowMultipleComponent]
    public sealed class RobotCameraSwitch : MonoBehaviour
    {
        public Camera baseCamera;
        public Camera fpsCamera;
        public KeyCode toggleKey = KeyCode.V;
        public bool IsFirstPerson { get; private set; }

        void Awake()
        {
            SetFirstPerson(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        public void Toggle()
        {
            SetFirstPerson(!IsFirstPerson);
        }

        void SetFirstPerson(bool enabled)
        {
            if (!baseCamera || !fpsCamera || baseCamera == fpsCamera) return;
            IsFirstPerson = enabled;
            baseCamera.enabled = !enabled;
            fpsCamera.enabled = enabled;
            SetListener(baseCamera, !enabled);
            SetListener(fpsCamera, enabled);
        }

        static void SetListener(Camera camera, bool enabled)
        {
            var listener = camera.GetComponent<AudioListener>();
            if (listener) listener.enabled = enabled;
        }

        void OnDisable()
        {
            SetFirstPerson(false);
        }
    }
}
