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
            if (!readKeyboard)
            {
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

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                up = false;
                down = false;
            }
        }

        void FixedUpdate()
        {
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
            float amplitude =
                Mathf.Clamp(nominalAmplitudeRad, 0,
                            Mathf.Min(Mathf.Max(0, servo.maximumAmplitudeRad), angle / 2f,
                                      Mathf.Max(0, servo.maximumOffsetRad)));
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
