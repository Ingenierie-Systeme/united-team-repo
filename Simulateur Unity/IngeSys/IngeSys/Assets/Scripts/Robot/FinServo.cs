using UnityEngine;

namespace IngeSys.Robot
{
    [DefaultExecutionOrder(-60), RequireComponent(typeof(RobotFishParameters))]
    public sealed class FinServo : MonoBehaviour
    {
        public bool animate = true;
        public float amplitudeRad = 0.35f;
        public float frequencyHz = 1f;
        public float offsetRad;
        // Limites provisoires du servo, en radians et rad/s.
        public float maximumAmplitudeRad = 0.6f;
        public float maximumOffsetRad = 0.4f;
        public float maximumFrequencyHz = 3f;
        public float maximumAngleRad = 0.8f;
        public float maximumSpeedRadPerSecond = 6f;
        public float effectiveAmplitudeRad, effectiveFrequencyHz, effectiveOffsetRad;
        public float actualAngleRad;
        float phase;

        void ClampCommands()
        {
            float angle = Mathf.Max(0f, maximumAngleRad);
            effectiveOffsetRad =
                Mathf.Clamp(offsetRad, -Mathf.Min(angle, Mathf.Max(0, maximumOffsetRad)),
                            Mathf.Min(angle, Mathf.Max(0, maximumOffsetRad)));
            effectiveFrequencyHz = Mathf.Clamp(frequencyHz, 0f, Mathf.Max(0f, maximumFrequencyHz));
            effectiveAmplitudeRad =
                Mathf.Clamp(amplitudeRad, 0f,
                            Mathf.Min(Mathf.Max(0f, maximumAmplitudeRad),
                                      Mathf.Max(0f, angle - Mathf.Abs(effectiveOffsetRad))));
            // Conservative sinusoidal slew limit: max |d(delta)/dt| = 2*pi*f*A.
            if (effectiveFrequencyHz > 0f)
            {
                effectiveAmplitudeRad =
                    Mathf.Min(effectiveAmplitudeRad, Mathf.Max(0f, maximumSpeedRadPerSecond) /
                                                         (2f * Mathf.PI * effectiveFrequencyHz));
            }
        }

        void FixedUpdate()
        {
            ClampCommands();
            phase = Mathf.Repeat(phase + 2f * Mathf.PI * effectiveFrequencyHz * Time.fixedDeltaTime,
                                 2f * Mathf.PI);
            float desiredAngleRad =
                animate ? effectiveOffsetRad + effectiveAmplitudeRad * Mathf.Sin(phase) : 0f;
            actualAngleRad =
                Mathf.MoveTowards(actualAngleRad, desiredAngleRad,
                                  Mathf.Max(0f, maximumSpeedRadPerSecond) * Time.fixedDeltaTime);
            var pivot = GetComponent<RobotFishParameters>().finPivot;
            if (pivot)
            {
                pivot.localRotation =
                    Quaternion.AngleAxis(actualAngleRad * Mathf.Rad2Deg, Vector3.up);
            }
        }
    }
}
