using UnityEngine;
using IngeSys.Robot;

namespace IngeSys.Diagnostics
{
    public sealed class RobotPilotHUD : MonoBehaviour
    {
        public RobotKeyboardInput pilot;

        void OnGUI()
        {
            if (!pilot)
            {
                return;
            }
            var previousMatrix = GUI.matrix;
            float uiScale = Mathf.Max(1f, Screen.width / 1280f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * uiScale);
            var rb = pilot.GetComponent<Rigidbody>();
            var mass = pilot.GetComponent<SlidingMass>();
            var servo = pilot.GetComponent<FinServo>();
            var thrust = pilot.GetComponent<MeanFinPropulsion>();
            var b = pilot.GetComponent<CalmWaterBuoyancy>();
            var f = pilot.transform.forward;
            float pitch = Mathf.Atan2(-f.y, new Vector2(f.x, f.z).magnitude) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            float depth = b.water ? b.water.WaterLevel - rb.position.y : float.NaN;
            float localDepth =
                b.water ? b.water.GetHeightAtWorldXZ(rb.position.x, rb.position.z) - rb.position.y
                        : float.NaN;
            var force =
                pilot.transform.TransformDirection(new Vector3(thrust.lateralN, 0, thrust.thrustN));
            float moment = pilot.transform
                               .InverseTransformDirection(Vector3.Cross(
                                   thrust.applicationPointWorld - rb.worldCenterOfMass, force))
                               .y;
            GUI.Box(new Rect(12, 12, 520, 200), "United TEAM");
            GUI.Label(
                new Rect(24, 40, 500, 250),
                $"Propulsion {(pilot.propulsionOn?"ON":"OFF")} | {pilot.mode}\n" +
                    $"A {servo.effectiveAmplitudeRad*Mathf.Rad2Deg:F1}° | f {servo.effectiveFrequencyHz:F2} Hz | offset {servo.effectiveOffsetRad*Mathf.Rad2Deg:F1}°\n" +
                    $"Servo réel {servo.actualAngleRad*Mathf.Rad2Deg:F1}° | masse s={mass.positionM:F3} / cible {mass.targetPositionM:F3} m\n" +
                    $"Position {rb.position:F2} m | profondeur moyenne {depth:F2} m / locale {localDepth:F2} m\n" +
                    $"Vitesse longitudinale {pilot.transform.InverseTransformDirection(rb.linearVelocity).z:F3} m/s\n" +
                    $"Pitch {pitch:F1}° | yaw {yaw:F1}° | vitesse lacet {pilot.transform.InverseTransformDirection(rb.angularVelocity).y:F3} rad/s\n" +
                    $"T={thrust.thrustN:F3} N | Y={thrust.lateralN:F3} N | moment lacet={moment:F4} N.m\n" +
                    $"Immergé {b.submergedVolume:F6} m³ ({(b.TotalVolume>0?b.submergedVolume/b.TotalVolume:0):P0})\n" +
                    $"Archimède {b.buoyancyN:F3} N | bilan vertical poids+poussée {b.buoyancyN-b.weightN:F4} N\n");
            GUI.matrix = previousMatrix;
        }
    }
}
