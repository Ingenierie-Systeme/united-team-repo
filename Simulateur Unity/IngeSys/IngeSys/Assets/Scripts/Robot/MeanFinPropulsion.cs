using UnityEngine;

namespace IngeSys.Robot
{
    [RequireComponent(typeof(Rigidbody), typeof(FinServo))]
    public sealed class MeanFinPropulsion : MonoBehaviour
    {
        public bool propulsionEnabled;
        // Coefficients provisoires non identifiés : A en rad, f en Hz.
        [Min(0f)]
        public float kT = 4f;
        public float kY = 4f;
        public float thrustN;
        public float lateralN;
        public Vector3 applicationPointWorld;

        void FixedUpdate()
        {
            var servo = GetComponent<FinServo>();
            var p = GetComponent<RobotFishParameters>();
            // Point moyen fixe, indépendant de l'angle visuel instantané de la palme.
            applicationPointWorld = transform.TransformPoint(
                new Vector3(0f, 0f, -p.hullLength / 2f - p.finLength / 2f));
            float amplitudeFrequency = servo.effectiveAmplitudeRad * servo.effectiveFrequencyHz;
            float propulsionScale = amplitudeFrequency * amplitudeFrequency;
            bool active = propulsionEnabled && servo.animate;

            thrustN = active ? Mathf.Max(0f, kT) * propulsionScale : 0f;
            lateralN = active ? kY * propulsionScale * servo.effectiveOffsetRad : 0f;

            Vector3 localForce = new Vector3(lateralN, 0f, thrustN);
            Vector3 worldForce = transform.TransformDirection(localForce);

            GetComponent<Rigidbody>().AddForceAtPosition(worldForce, applicationPointWorld,
                                                         ForceMode.Force);
        }
    }
}
