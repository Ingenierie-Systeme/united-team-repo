using UnityEngine;

namespace IngeSys.Robot
{
    /// <summary>Projecteur local +Z, éteint au démarrage ; F bascule son état.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Light))]
    public sealed class RobotHeadlamp : MonoBehaviour
    {
        public KeyCode toggleKey = KeyCode.F;
        Light headlamp;

        void Awake()
        {
            headlamp = GetComponent<Light>();
            headlamp.enabled = false;
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                Toggle();
        }

        public void Toggle()
        {
            if (!headlamp) headlamp = GetComponent<Light>();
            headlamp.enabled = !headlamp.enabled;
        }

        void OnDisable()
        {
            if (headlamp) headlamp.enabled = false;
        }
    }
}
