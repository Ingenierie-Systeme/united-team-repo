using System.Collections;
using System.Text;
using UnityEngine;
using IngeSys.Robot;
using IngeSys.Environment;

namespace IngeSys.Diagnostics
{
    public sealed class FinPropulsionValidation : MonoBehaviour
    {
        public GameObject robotPrefab;
        public FlatWaterSurface water;
        [TextArea(8, 30)]
        public string results;
        public bool finished;

        IEnumerator Start()
        {
            var report = new StringBuilder(
                "case,T_N,Y_N,x,z,speed,yawRad,yawRateRadS,servoPeakRad,servoPeakSpeedRadS,pass\n");
            string[] names = { "off_animated", "straight", "offset_positive", "offset_negative",
                               "cutoff" };
            for (int i = 0; i < names.Length; i++)
            {
                var go = Instantiate(robotPrefab, new Vector3(0f, 4f, 0f), Quaternion.identity);
                var b = go.GetComponent<CalmWaterBuoyancy>();
                b.water = water;
                var servo = go.GetComponent<FinServo>();
                servo.animate = true;
                servo.amplitudeRad = 0.35f;
                servo.frequencyHz = 1f;
                servo.offsetRad = i == 2 ? 0.2f : i == 3 ? -0.2f : 0f;
                var propulsion = go.GetComponent<MeanFinPropulsion>();
                propulsion.propulsionEnabled = i != 0;
                var body = go.GetComponent<Rigidbody>();
                float peak = 0f, peakSpeed = 0f, previous = 0f, cutSpeed = 0f;
                for (int n = 0; n < 200; n++)
                {
                    if (i == 4 && n == 100)
                    {
                        cutSpeed = body.linearVelocity.magnitude;
                        propulsion.propulsionEnabled = false;
                    }
                    yield return new WaitForFixedUpdate();
                    peak = Mathf.Max(peak, Mathf.Abs(servo.actualAngleRad));
                    peakSpeed = Mathf.Max(peakSpeed, Mathf.Abs(servo.actualAngleRad - previous) /
                                                         Time.fixedDeltaTime);
                    previous = servo.actualAngleRad;
                }
                float yaw = Mathf.Atan2(go.transform.forward.x, go.transform.forward.z),
                      rate = body.angularVelocity.y, speed = body.linearVelocity.magnitude;
                bool pass = i == 0   ? speed < 0.001f && Mathf.Abs(rate) < 0.001f
                            : i == 1 ? go.transform.position.z > 0.1f && Mathf.Abs(yaw) < 0.01f
                            : i == 2 ? yaw < -0.01f && rate < 0f
                            : i == 3 ? yaw > 0.01f && rate > 0f
                                     : propulsion.thrustN == 0f && propulsion.lateralN == 0f &&
                                           speed < cutSpeed;
                pass &= peak <= servo.maximumAngleRad + 0.001f &&
                        peakSpeed <= servo.maximumSpeedRadPerSecond + 0.001f;
                report.AppendLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1:F6},{2:F6},{3:F6},{4:F6},{5:F6},{6:F6},{7:F6},{8:F6},{9:F6},{10}",
                    names[i], propulsion.thrustN, propulsion.lateralN, go.transform.position.x,
                    go.transform.position.z, speed, yaw, rate, peak, peakSpeed, pass));
                Debug.Log("[PHASE4] " + names[i] + " " + (pass ? "PASS" : "FAIL"));
                Destroy(go);
                yield return null;
            }
            results = report.ToString();
            finished = true;
            Debug.Log("[PHASE4_RESULTS]\n" + results);
        }
    }
}
