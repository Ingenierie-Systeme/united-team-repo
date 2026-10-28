using UnityEngine;

namespace IngeSys.Robot
{
    [RequireComponent(typeof(Rigidbody), typeof(CalmWaterBuoyancy))]
    public sealed class BodyWaterDrag : MonoBehaviour
    {
        // Coefficients provisoires, axes du corps : N/(m/s) et N/(m/s)².
        public Vector3 linear = new Vector3(2f, 2f, 1f);
        public Vector3 quadratic = new Vector3(5f, 5f, 2f);
        // Amortissements provisoires : N.m/(rad/s) et N.m/(rad/s)².
        public Vector3 angularLinear = new Vector3(0.05f, 0.05f, 0.05f);
        public Vector3 angularQuadratic = new Vector3(0.01f, 0.01f, 0.01f);

        void FixedUpdate()
        {
            var body = GetComponent<Rigidbody>();
            var b = GetComponent<CalmWaterBuoyancy>();
            float fraction =
                b.TotalVolume > 0f ? Mathf.Clamp01(b.submergedVolume / b.TotalVolume) : 0f;
            var lastLocalForce =
                Resistance(transform.InverseTransformDirection(body.linearVelocity), linear,
                           quadratic) *
                fraction;
            var lastLocalTorque =
                Resistance(transform.InverseTransformDirection(body.angularVelocity), angularLinear,
                           angularQuadratic) *
                fraction;
            body.AddForce(transform.TransformDirection(lastLocalForce));
            body.AddTorque(transform.TransformDirection(lastLocalTorque));
        }

        static Vector3 Resistance(Vector3 v, Vector3 a, Vector3 b)
        {
            return new Vector3(-Mathf.Max(0, a.x) * v.x - Mathf.Max(0, b.x) * Mathf.Abs(v.x) * v.x,
                               -Mathf.Max(0, a.y) * v.y - Mathf.Max(0, b.y) * Mathf.Abs(v.y) * v.y,
                               -Mathf.Max(0, a.z) * v.z - Mathf.Max(0, b.z) * Mathf.Abs(v.z) * v.z);
        }
    }
}
