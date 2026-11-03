using UnityEngine;

namespace IngeSys.Robot
{
    /// <summary>Rigid contact settings, provisional until measured. PhysX alone solves contact reactions.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class UnderwaterContactSettings : MonoBehaviour
    {
        public Transform collisionEnvelope;
        public PhysicsMaterial robotMaterial;
        [Min(0.0001f)] public float contactOffsetM = 0.0005f;
        [Range(6, 32)] public int positionIterations = 12;
        [Range(1, 16)] public int velocityIterations = 4;
        [Min(.1f)] public float maxDepenetrationMps = 1;

        void Awake() { Apply(); }

        [ContextMenu("Apply contact settings")]
        public void Apply()
        {
            var body = GetComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations = positionIterations;
            body.solverVelocityIterations = velocityIterations;
            body.maxDepenetrationVelocity = maxDepenetrationMps;
            if (!collisionEnvelope) return;
            foreach (var collider in collisionEnvelope.GetComponentsInChildren<Collider>(true))
            {
                collider.contactOffset = contactOffsetM;
                collider.sharedMaterial = robotMaterial;
            }
        }
    }
}
