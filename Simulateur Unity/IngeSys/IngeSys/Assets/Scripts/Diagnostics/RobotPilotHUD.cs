using UnityEngine;
using System.Globalization;
using IngeSys.Robot;

namespace IngeSys.Diagnostics
{
    public sealed class RobotPilotHUD : MonoBehaviour
    {
        public RobotKeyboardInput pilot;
        public bool showPalmageSettings;
        public static bool IsEditingPalmageField { get; private set; }
        static readonly string[] Tabs = { "Pilotage", "Palmage" };
        string amplitudeText, frequencyText, angleText, settingsMessage = "";
        bool settingsLoaded;

        void OnEnable() { LoadSettings(); }
        void OnDisable() { IsEditingPalmageField = false; }

        void LoadSettings()
        {
            if (!pilot) return;
            amplitudeText = Number(pilot.nominalAmplitudeRad * Mathf.Rad2Deg);
            frequencyText = Number(pilot.nominalFrequencyHz);
            angleText = Number(pilot.GetComponent<FinServo>().maximumAngleRad * Mathf.Rad2Deg);
            settingsLoaded = true;
        }

        public bool ApplyPalmageTextSettings(string amplitude, string frequency, string absoluteAngle)
        {
            if (!pilot || !Parse(amplitude, out float a) || !Parse(frequency, out float f) || !Parse(absoluteAngle, out float limit))
            {
                settingsMessage = "Saisir trois nombres valides (virgule ou point).";
                return false;
            }
            if (!pilot.SetPalmageSettings(a, f, limit)) return false;
            LoadSettings();
            settingsMessage = "Réglages appliqués pour cette session.";
            return true;
        }

        static bool Parse(string text, out float value)
        {
            return float.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
        }
        static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        static string NumericField(string label, string name, string value, string unit)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.ExpandWidth(true));
            GUI.SetNextControlName(name);
            value = GUILayout.TextField(value ?? "", 16, GUILayout.Width(85));
            GUILayout.Label(unit, GUILayout.Width(25));
            GUILayout.EndHorizontal();
            return value;
        }

        void DrawPalmageSettings(Rect area, FinServo servo)
        {
            if (!settingsLoaded) LoadSettings();
            GUILayout.BeginArea(area);
            GUILayout.Label("Amplitude : excursion de chaque côté de l'angle central.");
            GUILayout.Space(4);
            amplitudeText = NumericField("Amplitude A (±)", "Palmage_Amplitude", amplitudeText, "°");
            GUILayout.Space(4);
            frequencyText = NumericField("Fréquence de palmage", "Palmage_Frequency", frequencyText, "Hz");
            GUILayout.Space(4);
            angleText = NumericField("Angle maximal du servo |δ|", "Palmage_Limit", angleText, "°");
            GUILayout.Space(4);
            GUILayout.Label($"Limite symétrique : −{Number(servo.maximumAngleRad * Mathf.Rad2Deg)}° / +{Number(servo.maximumAngleRad * Mathf.Rad2Deg)}°");
            GUILayout.Label($"Consigne réalisable : A={Number(servo.effectiveAmplitudeRad * Mathf.Rad2Deg)}° | f={Number(servo.effectiveFrequencyHz)} Hz");
            GUILayout.Label($"Butée matériel : ±{Number(servo.hardwareMaximumAngleRad * Mathf.Rad2Deg)}° ; A + décalage bornés.");
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Appliquer", GUILayout.Height(26)))
            {
                if (ApplyPalmageTextSettings(amplitudeText, frequencyText, angleText)) GUI.FocusControl(null);
            }
            if (GUILayout.Button("Valeurs actuelles", GUILayout.Height(26)))
            {
                LoadSettings(); settingsMessage = ""; GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(settingsMessage);
            GUILayout.EndArea();
            IsEditingPalmageField = GUI.GetNameOfFocusedControl().StartsWith("Palmage_");
        }

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
            var hydro = pilot.GetComponent<FinHydrodynamics>();
            bool instant = hydro && hydro.isActiveAndEnabled && hydro.model == FinHydrodynamics.Model.InstantaneousPressureV2;
            var contacts = pilot.GetComponent<RobotContactDiagnostics>();
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
            if (instant) moment = pilot.transform.InverseTransformDirection(hydro.totalTorqueNm).y;
            float thrustValue = instant ? hydro.thrustN : thrust.thrustN;
            float lateralValue = instant ? hydro.lateralN : thrust.lateralN;
            float panelWidth = Mathf.Min(520, Screen.width / uiScale - 24);
            GUI.Box(new Rect(12, 12, panelWidth, 325), "United TEAM");
            int selected = GUI.Toolbar(new Rect(24, 36, panelWidth - 24, 26), showPalmageSettings ? 1 : 0, Tabs);
            if ((selected == 1) != showPalmageSettings)
            {
                showPalmageSettings = selected == 1;
                LoadSettings(); GUI.FocusControl(null);
            }
            if (showPalmageSettings)
            {
                DrawPalmageSettings(new Rect(24, 74, panelWidth - 24, 255), servo);
                GUI.matrix = previousMatrix;
                return;
            }
            IsEditingPalmageField = false;
            GUI.Label(
                new Rect(24, 70, panelWidth - 24, 260),
                $"Propulsion {(pilot.propulsionOn?"ON":"OFF")} | {pilot.mode}\n" +
                    $"A {servo.effectiveAmplitudeRad*Mathf.Rad2Deg:F1}° | f {servo.effectiveFrequencyHz:F2} Hz | offset {servo.effectiveOffsetRad*Mathf.Rad2Deg:F1}°\n" +
                    $"Servo réel {servo.actualAngleRad*Mathf.Rad2Deg:F1}° | masse s={mass.positionM:F3} / cible {mass.targetPositionM:F3} m\n" +
                    $"Position {rb.position:F2} m | profondeur moyenne {depth:F2} m / locale {localDepth:F2} m\n" +
                    $"Vitesse longitudinale {pilot.transform.InverseTransformDirection(rb.linearVelocity).z:F3} m/s\n" +
                    $"Pitch {pitch:F1}° | yaw {yaw:F1}° | vitesse lacet {pilot.transform.InverseTransformDirection(rb.angularVelocity).y:F3} rad/s\n" +
                    $"T={thrustValue:F3} N | Y={lateralValue:F3} N | moment lacet={moment:F4} N.m\n" +
                    $"Immergé {b.submergedVolume:F6} m³ ({(b.TotalVolume>0?b.submergedVolume/b.TotalVolume:0):P0})\n" +
                    $"Archimède {b.buoyancyN:F3} N | bilan vertical poids+poussée {b.buoyancyN-b.weightN:F4} N\n" +
                    (instant ? $"Pale : pression diff. {hydro.minPressurePa:F0} à {hydro.maxPressurePa:F0} Pa | surface immergée {hydro.submergedAreaM2:F4} m²\n" : "") +
                    (contacts ? $"Contact : J={contacts.stepImpulseNs.magnitude:F3} N·s | effort moyen/pas={contacts.stepMeanForceN.magnitude:F2} N\n" +
                        $"{contacts.lastOwnCollider} / {contacts.lastOtherCollider} | max moyen/pas={contacts.maxStepMeanForceN:F1} N" : ""));
            GUI.matrix = previousMatrix;
        }
    }
}
