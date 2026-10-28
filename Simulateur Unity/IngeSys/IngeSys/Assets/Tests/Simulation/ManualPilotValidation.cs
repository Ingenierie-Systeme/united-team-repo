using System.Collections;
using System.Text;
using UnityEngine;
using IngeSys.Robot;

namespace IngeSys.Diagnostics
{
    public sealed class ManualPilotValidation : MonoBehaviour
    {
        public RobotKeyboardInput template;
        [TextArea(10, 40)]
        public string results;
        public bool finished;
        StringBuilder report = new StringBuilder();
        RobotKeyboardInput pilot;

        IEnumerator Wait(float seconds)
        {
            for (int i = 0; i < Mathf.Max(1, Mathf.RoundToInt(seconds / Time.fixedDeltaTime)); i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        void Spawn()
        {
            if (pilot)
            {
                Destroy(pilot.gameObject);
            }
            pilot = Instantiate(template, new Vector3(0, 4, 0), Quaternion.identity);
            pilot.gameObject.SetActive(true);
            pilot.readKeyboard = false;
        }

        void Check(string name, bool pass, string detail)
        {
            report.AppendLine(name + "," + pass + "," + detail);
            Debug.Log("[PHASE6] " + name + " " + (pass ? "PASS" : "FAIL"));
        }

        IEnumerator Start()
        {
            Spawn();
            var mass = pilot.GetComponent<SlidingMass>();
            var rb = pilot.GetComponent<Rigidbody>();
            float total = rb.mass;
            pilot.CaptureCommands(true, false, false, false, false, false);
            yield return Wait(4);
            Check("mass_forward", mass.positionM > 0.119f && pilot.transform.forward.y < -0.01f,
                  "s=" + mass.positionM + " forwardY=" + pilot.transform.forward.y);
            pilot.CaptureCommands(false, false, false, false, false, false);
            yield return Wait(0.3f);
            float retained = mass.targetPositionM;
            pilot.CaptureCommands(true, true, false, false, false, false);
            yield return Wait(0.3f);
            Check("both_mass_keys_hold", Mathf.Abs(mass.targetPositionM - retained) < 0.00001f,
                  "target=" + mass.targetPositionM);
            float before = mass.positionM;
            double start = Time.fixedTimeAsDouble;
            pilot.CaptureCommands(true, true, true, false, false, false);
            yield return Wait(0.02f);
            double elapsed = Time.fixedTimeAsDouble - start;
            Check("R_no_teleport",
                  Mathf.Abs(mass.positionM - before) <=
                          mass.maximumSpeedMps * (float)elapsed + 0.000001f &&
                      mass.targetPositionM == 0,
                  "s=" + mass.positionM + " elapsed=" + elapsed);
            pilot.CaptureCommands(false, false, false, false, false, false);
            yield return Wait(4);
            Check("R_center_constant_mass",
                  Mathf.Abs(mass.positionM) < 0.0001f && Mathf.Abs(rb.mass - total) < 0.0001f,
                  "s=" + mass.positionM + " mass=" + rb.mass);
            Spawn();
            pilot.CaptureCommands(false, true, false, false, false, false);
            yield return Wait(4);
            Check("mass_aft",
                  pilot.GetComponent<SlidingMass>().positionM < -0.119f &&
                      pilot.transform.forward.y > 0.01f,
                  "forwardY=" + pilot.transform.forward.y);
            Spawn();
            pilot.CaptureCommands(false, false, false, true, false, false);
            yield return Wait(3);
            var force = pilot.GetComponent<MeanFinPropulsion>();
            var servo = pilot.GetComponent<FinServo>();
            rb = pilot.GetComponent<Rigidbody>();
            float speed = rb.linearVelocity.magnitude;
            Check("Z_on_straight",
                  pilot.propulsionOn && force.thrustN > 0 && speed > 0.1f &&
                      Mathf.Abs(rb.angularVelocity.y) < 0.01f,
                  "speed=" + speed + " T=" + force.thrustN);
            pilot.CaptureCommands(false, false, false, true, false, false);
            yield return Wait(2);
            Check("Z_off_decay",
                  !pilot.propulsionOn && force.thrustN == 0 &&
                      Mathf.Abs(servo.actualAngleRad) < 0.0001f &&
                      rb.linearVelocity.magnitude < speed,
                  "speed=" + rb.linearVelocity.magnitude);
            Spawn();
            pilot.CaptureCommands(false, false, false, true, true, false);
            yield return Wait(3);
            rb = pilot.GetComponent<Rigidbody>();
            servo = pilot.GetComponent<FinServo>();
            Check("left",
                  rb.angularVelocity.y < -0.01f &&
                      Mathf.Abs(servo.effectiveOffsetRad - servo.effectiveAmplitudeRad) < 0.001f,
                  "yawRate=" + rb.angularVelocity.y + " offset=" + servo.effectiveOffsetRad);
            pilot.CaptureCommands(false, false, false, false, true, true);
            yield return Wait(0.1f);
            Check("both_direction_keys", pilot.mode == RobotKeyboardInput.FinMode.TURN_LEFT,
                  "mode=" + pilot.mode);
            pilot.CaptureCommands(false, false, false, false, false, true);
            yield return Wait(5);
            Check("right_reversal", rb.angularVelocity.y > 0.01f && servo.effectiveOffsetRad < 0,
                  "yawRate=" + rb.angularVelocity.y);
            pilot.CaptureCommands(false, false, false, false, false, true);
            yield return Wait(1);
            Check("right_toggle_straight",
                  pilot.mode == RobotKeyboardInput.FinMode.STRAIGHT &&
                      Mathf.Abs(servo.effectiveOffsetRad) < 0.001f,
                  "offset=" + servo.effectiveOffsetRad);
            pilot.CaptureCommands(true, false, false, false, true, false);
            yield return Wait(0.6f);
            float s = pilot.GetComponent<SlidingMass>().positionM;
            pilot.CaptureCommands(false, false, false, true, false, false);
            yield return Wait(0.5f);
            pilot.CaptureCommands(false, false, false, true, false, false);
            yield return Wait(0.5f);
            Check("combined_preserved",
                  pilot.propulsionOn && pilot.mode == RobotKeyboardInput.FinMode.TURN_LEFT &&
                      pilot.GetComponent<SlidingMass>().positionM >= s - 0.001f,
                  "s=" + pilot.GetComponent<SlidingMass>().positionM +
                      " forwardY=" + pilot.transform.forward.y);
            Spawn();
            rb = pilot.GetComponent<Rigidbody>();
            rb.position = new Vector3(0, 1.4f, 0);
            rb.linearVelocity = Vector3.down;
            yield return Wait(1);
            var hull = pilot.GetComponent<RobotFishParameters>().hull.GetComponent<Collider>();
            Check("floor_collision", hull.bounds.min.y >= 0.998f && rb.position.y < 1.15f,
                  "hullBottom=" + hull.bounds.min.y);
            Spawn();
            rb = pilot.GetComponent<Rigidbody>();
            rb.position = new Vector3(9, 4, 0);
            rb.linearVelocity = Vector3.right * 3;
            yield return Wait(1);
            hull = pilot.GetComponent<RobotFishParameters>().hull.GetComponent<Collider>();
            Check("wall_collision", hull.bounds.max.x <= 10.02f && rb.linearVelocity.x < 0.2f,
                  "hullMaxX=" + hull.bounds.max.x + " vx=" + rb.linearVelocity.x);
            Spawn();
            rb = pilot.GetComponent<Rigidbody>();
            rb.position = new Vector3(0, 6.5f, 0);
            var buoyancy = pilot.GetComponent<CalmWaterBuoyancy>();
            float minV = float.PositiveInfinity, maxV = 0;
            for (int n = 0; n < 100; n++)
            {
                yield return new WaitForFixedUpdate();
                minV = Mathf.Min(minV, buoyancy.submergedVolume);
                maxV = Mathf.Max(maxV, buoyancy.submergedVolume);
            }
            Check("Gerstner_partial", maxV - minV > 0.00001f, "minV=" + minV + " maxV=" + maxV);
            Destroy(pilot.gameObject);
            results = report.ToString();
            finished = true;
            Debug.Log("[PHASE6_RESULTS]\n" + results);
        }
    }
}
