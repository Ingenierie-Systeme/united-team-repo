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
        [Tooltip("Hardware travel ceiling; pi preserves legacy scenes. D955TW factory PWM profile: pi/3.")]
        public float hardwareMaximumAngleRad = Mathf.PI;
        [Tooltip("Manufacturer stall torque at supply voltage; diagnostic reference, not continuous torque.")]
        public float stallTorqueNm;
        public float equivalentPositionPulseMicroseconds => 1500f + 600f * Mathf.Clamp(actualAngleRad / (Mathf.PI/3f), -1f, 1f);
        public float maximumSpeedRadPerSecond = 6f;
        public float effectiveAmplitudeRad, effectiveFrequencyHz, effectiveOffsetRad;
        public float actualAngleRad;
        public bool limitAcceleration;
        [Min(0)] public float maximumAccelerationRadS2 = 40f;
        public float actualAngularVelocityRadS, actualAngularAccelerationRadS2;
        float phase, motorVelocityRadS;
        bool wasAccelerationLimited;

        public void SetMechanicalAngleLimit(float radians)
        {
            maximumAngleRad=Mathf.Clamp(radians,0,Mathf.Max(0,hardwareMaximumAngleRad));
            float bounded=Mathf.Clamp(actualAngleRad,-maximumAngleRad,maximumAngleRad);
            if(Mathf.Approximately(bounded,actualAngleRad)) return;
            // A live configuration change is not a resolved mechanical impact. Reinitialize the
            // out-of-range servo state instead of generating a spurious pressure acceleration spike.
            actualAngleRad=bounded;
            actualAngularVelocityRadS=actualAngularAccelerationRadS2=motorVelocityRadS=0;
            var pivot=GetComponent<RobotFishParameters>().finPivot;
            if(pivot)pivot.localRotation=Quaternion.AngleAxis(actualAngleRad*Mathf.Rad2Deg,Vector3.up);
        }

        public void ResetMotion()
        {
            phase=0;motorVelocityRadS=0;wasAccelerationLimited=false;
            actualAngleRad=actualAngularVelocityRadS=actualAngularAccelerationRadS2=motorVelocityRadS=0;
            var pivot=GetComponent<RobotFishParameters>().finPivot;
            if(pivot)pivot.localRotation=Quaternion.identity;
        }

        void ClampCommands()
        {
            maximumAngleRad=Mathf.Clamp(maximumAngleRad,0,Mathf.Max(0,hardwareMaximumAngleRad));
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
                // A sinusoidal target also requires max acceleration A*(2*pi*f)^2.
                if(limitAcceleration) effectiveAmplitudeRad=Mathf.Min(effectiveAmplitudeRad,
                    Mathf.Max(0,maximumAccelerationRadS2)/Mathf.Pow(2f*Mathf.PI*effectiveFrequencyHz,2));
            }
        }

        void FixedUpdate()
        {
            ClampCommands();
            phase = Mathf.Repeat(phase + 2f * Mathf.PI * effectiveFrequencyHz * Time.fixedDeltaTime,
                                 2f * Mathf.PI);
            float desiredAngleRad =
                animate ? effectiveOffsetRad + effectiveAmplitudeRad * Mathf.Sin(phase) : 0f;
            float previousAngle=actualAngleRad, previousVelocity=actualAngularVelocityRadS;
            float dt=Time.fixedDeltaTime;
            if(limitAcceleration)
            {
                if(!wasAccelerationLimited)motorVelocityRadS=actualAngularVelocityRadS;
                // Resolve the actuator response more finely than the water/PhysX step. This is
                // internal actuator motion only, not a second rigid-body integration.
                int count=Mathf.Max(1,Mathf.CeilToInt(dt/.002f));float h=dt/count;
                float w=2*Mathf.PI*effectiveFrequencyHz;
                const float trackingRate=20f;
                for(int i=0;i<count;i++)
                {
                    float t=phase-w*dt+w*(i+.5f)*h;
                    float target=animate?effectiveOffsetRad+effectiveAmplitudeRad*Mathf.Sin(t):0;
                    float targetVelocity=animate?effectiveAmplitudeRad*w*Mathf.Cos(t):0;
                    float targetAcceleration=animate?-effectiveAmplitudeRad*w*w*Mathf.Sin(t):0;
                    float acceleration=targetAcceleration+trackingRate*trackingRate*(target-actualAngleRad)
                        +2*trackingRate*(targetVelocity-motorVelocityRadS);
                    motorVelocityRadS=Mathf.Clamp(motorVelocityRadS+
                        Mathf.Clamp(acceleration,-Mathf.Max(0,maximumAccelerationRadS2),Mathf.Max(0,maximumAccelerationRadS2))*h,
                        -Mathf.Max(0,maximumSpeedRadPerSecond),Mathf.Max(0,maximumSpeedRadPerSecond));
                    actualAngleRad=Mathf.Clamp(actualAngleRad+motorVelocityRadS*h,-maximumAngleRad,maximumAngleRad);
                    if(Mathf.Abs(actualAngleRad)>=maximumAngleRad&&motorVelocityRadS*actualAngleRad>0)motorVelocityRadS=0;
                }
            }
            else actualAngleRad=Mathf.MoveTowards(actualAngleRad,desiredAngleRad,
                Mathf.Max(0f, maximumSpeedRadPerSecond)*dt);
            wasAccelerationLimited=limitAcceleration;
            actualAngleRad=Mathf.Clamp(actualAngleRad,-Mathf.Max(0,maximumAngleRad),Mathf.Max(0,maximumAngleRad));
            actualAngularVelocityRadS=(actualAngleRad-previousAngle)/dt;
            actualAngularAccelerationRadS2=(actualAngularVelocityRadS-previousVelocity)/dt;
            var pivot = GetComponent<RobotFishParameters>().finPivot;
            if (pivot)
            {
                pivot.localRotation =
                    Quaternion.AngleAxis(actualAngleRad * Mathf.Rad2Deg, Vector3.up);
            }
        }
    }
}
