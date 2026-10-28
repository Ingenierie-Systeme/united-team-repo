using UnityEngine;

namespace IngeSys.Diagnostics
{
    public sealed class RobotFollowCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 worldOffset = new Vector3(1.2f, 0.65f, -1.5f);
        public float smoothing = 5f;

        void LateUpdate()
        {
            if (!target)
            {
                return;
            }
            transform.position =
                Vector3.Lerp(transform.position, target.position + worldOffset,
                             1 - Mathf.Exp(-Mathf.Max(0, smoothing) * Time.deltaTime));
            var direction = transform.position - target.position;
            foreach (var hit in Physics.SphereCastAll(target.position, 0.08f, direction.normalized,
                                                      direction.magnitude))
            {
                if (hit.collider.gameObject.isStatic && hit.distance < direction.magnitude)
                {
                    direction = direction.normalized * Mathf.Max(0.12f, hit.distance - 0.05f);
                }
            }
            transform.position = target.position + direction;
            transform.rotation =
                Quaternion.LookRotation(target.position - transform.position, Vector3.up);
        }
    }
}
