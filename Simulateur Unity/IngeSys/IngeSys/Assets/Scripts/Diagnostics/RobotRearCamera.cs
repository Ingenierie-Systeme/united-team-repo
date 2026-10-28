using UnityEngine;

namespace IngeSys.Diagnostics
{
    /// <summary>Rear view in the robot frame; static geometry shortens the camera distance.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class RobotRearCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 localOffset = new Vector3(0, 0.25f, -1.2f);
        public float lookAheadM = 0.3f;
        [Min(0)] public float smoothing = 7f;
        [Min(0.01f)] public float collisionRadius = 0.12f;
        Camera view;
        bool wasActive;

        void Awake() { view = GetComponent<Camera>(); }

        void LateUpdate()
        {
            if (!view.enabled || !target) { wasActive = false; return; }
            Vector3 desired = target.TransformPoint(localOffset);
            Vector3 position = wasActive
                ? Vector3.Lerp(transform.position, desired, 1 - Mathf.Exp(-smoothing * Time.deltaTime))
                : desired;
            Vector3 direction = position - target.position;
            float distance = direction.magnitude;
            if (distance > 0.0001f)
            {
                foreach (var hit in Physics.SphereCastAll(target.position, collisionRadius,
                    direction / distance, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.gameObject.isStatic && !hit.transform.IsChildOf(target))
                        distance = Mathf.Min(distance, Mathf.Max(0.08f, hit.distance - 0.03f));
                }
                position = target.position + direction.normalized * distance;
            }
            transform.position = position;
            Vector3 lookAt = target.position + target.forward * lookAheadM;
            transform.rotation = Quaternion.LookRotation(lookAt - position, target.up);
            wasActive = true;
        }
    }
}
