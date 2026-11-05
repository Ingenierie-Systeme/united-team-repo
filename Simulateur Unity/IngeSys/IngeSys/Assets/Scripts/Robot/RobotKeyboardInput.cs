using UnityEngine;

namespace IngeSys.Robot
{
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(SlidingMass), typeof(FinServo), typeof(MeanFinPropulsion))]
    public sealed class RobotKeyboardInput : MonoBehaviour
    {
        public enum FinMode
        {
            STRAIGHT,
            TURN_LEFT,
            TURN_RIGHT
        }

        public FinMode mode;
        public bool propulsionOn;
        public float nominalAmplitudeRad = 0.34906585f;
        public float nominalFrequencyHz = 1f;
        public float centralPositionM;
        public float targetSpeedMps = 0.05f;
        public float offsetTransitionRadS = 0.7f;
        public bool readKeyboard = true;

        bool up;
        bool down;
        bool resetPending;

        void Update()
        {
            var rosControl=GetComponent<RobotRosControl>();
            if ((rosControl&&rosControl.OwnsActuators) || !readKeyboard || IngeSys.Diagnostics.RobotPilotHUD.IsEditingPalmageField)
            {
                up = down = false;
                return;
            }

            bool bothDirections =
                Input.GetKey(KeyCode.LeftArrow) && Input.GetKey(KeyCode.RightArrow);
            CaptureCommands(Input.GetKey(KeyCode.UpArrow), Input.GetKey(KeyCode.DownArrow),
                            Input.GetKeyDown(KeyCode.R), Input.GetKeyDown(KeyCode.Z),
                            !bothDirections && Input.GetKeyDown(KeyCode.LeftArrow),
                            !bothDirections && Input.GetKeyDown(KeyCode.RightArrow));
        }

        public void CaptureCommands(bool forward, bool backward, bool center, bool toggle,
                                    bool left, bool right)
        {
            var rosControl=GetComponent<RobotRosControl>();
            if(rosControl&&rosControl.OwnsActuators)return;
            up = forward;
            down = backward;
            resetPending |= center;

            if (toggle)
            {
                propulsionOn = !propulsionOn;
            }

            if (left != right)
            {
                var selected = left ? FinMode.TURN_LEFT : FinMode.TURN_RIGHT;
                mode = mode == selected ? FinMode.STRAIGHT : selected;
            }
        }

        public void ClearHeldCommands(){up=down=resetPending=false;}

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                up = false;
                down = false;
            }
        }

        /// <summary>UI units: degrees / hertz. The mechanical bound is symmetric and authoritative.</summary>
        public bool SetPalmageSettings(float amplitudeDegrees, float frequencyHz, float absoluteAngleDegrees)
        {
            var rosControl=GetComponent<RobotRosControl>();
            if(rosControl&&rosControl.OwnsActuators)return false;
            if (!Finite(amplitudeDegrees) || !Finite(frequencyHz) || !Finite(absoluteAngleDegrees)) return false;
            var servo = GetComponent<FinServo>();
            float limit = Mathf.Clamp(Mathf.Abs(absoluteAngleDegrees), 0, servo.hardwareMaximumAngleRad * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            servo.SetMechanicalAngleLimit(limit);
            // Older V1 amplitude/offset ceilings must not silently contradict the chosen mechanical bound.
            servo.maximumAmplitudeRad = limit;
            servo.maximumOffsetRad = limit;
            nominalAmplitudeRad = Mathf.Clamp(amplitudeDegrees * Mathf.Deg2Rad, 0, limit);
            nominalFrequencyHz = Mathf.Clamp(frequencyHz, 0, Mathf.Max(0, servo.maximumFrequencyHz));
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        void FixedUpdate()
        {
            var rosControl=GetComponent<RobotRosControl>();
            if(rosControl&&rosControl.OwnsActuators)return;
            var mass = GetComponent<SlidingMass>();
            var p = GetComponent<RobotFishParameters>();
            if (resetPending)
            {
                mass.targetPositionM =
                    Mathf.Clamp(centralPositionM, p.slidingMinimumZ, p.slidingMaximumZ);
                resetPending = false;
            }
            else if (up != down)
            {
                mass.targetPositionM = Mathf.Clamp(
                    mass.targetPositionM +
                        (up ? 1 : -1) * Mathf.Max(0, targetSpeedMps) * Time.fixedDeltaTime,
                    p.slidingMinimumZ, p.slidingMaximumZ);
            }

            var servo = GetComponent<FinServo>();
            var force = GetComponent<MeanFinPropulsion>();
            float sign = force.kY >= 0 ? 1f : -1f;
            float angle = Mathf.Max(0, servo.maximumAngleRad);
            float ceiling = Mathf.Min(Mathf.Max(0, servo.maximumAmplitudeRad), angle);
            if (mode != FinMode.STRAIGHT)
                ceiling = Mathf.Min(ceiling, angle / 2f, Mathf.Max(0, servo.maximumOffsetRad));
            float amplitude = Mathf.Clamp(nominalAmplitudeRad, 0, ceiling);
            float f = Mathf.Clamp(nominalFrequencyHz, 0, Mathf.Max(0, servo.maximumFrequencyHz));
            if (f > 0)
            {
                amplitude = Mathf.Min(amplitude, Mathf.Max(0, servo.maximumSpeedRadPerSecond) /
                                                     (2 * Mathf.PI * f));
            }

            float offset = 0f;

            if (mode == FinMode.TURN_LEFT)
            {
                offset = sign * amplitude;
            }
            else if (mode == FinMode.TURN_RIGHT)
            {
                offset = -sign * amplitude;
            }

            servo.amplitudeRad = amplitude;
            servo.frequencyHz = f;
            servo.offsetRad = Mathf.MoveTowards(
                servo.offsetRad, offset, Mathf.Max(0, offsetTransitionRadS) * Time.fixedDeltaTime);
            servo.animate = propulsionOn;
            force.propulsionEnabled = propulsionOn;
        }
    }
}
