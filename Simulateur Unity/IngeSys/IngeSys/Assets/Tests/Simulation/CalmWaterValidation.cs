using System.Collections;
using System.Text;
using UnityEngine;
using IngeSys.Robot;
using IngeSys.Environment;

namespace IngeSys.Diagnostics
{
    public sealed class CalmWaterValidation : MonoBehaviour
    {
        public GameObject robotPrefab;
        public FlatWaterSurface water;
        [TextArea(8, 30)]
        public string results;
        public bool finished;

        IEnumerator Start()
        {
            var report = new StringBuilder(
                "case,massKg,volumeM3,initialSubmergedM3,finalY,finalSpeed,forwardY,s,pass\n");
            string[] cases = { "neutral",      "heavy",    "half",     "free_decay",
                               "mass_forward", "mass_aft", "half_tilt" };
            for (int i = 0; i < cases.Length; i++)
            {
                var go = Instantiate(
                    robotPrefab,
                    new Vector3(-6f + 2f * i, (i == 2 || i == 6) ? water.WaterLevel : 4f, 0f),
                    i == 6 ? Quaternion.Euler(10f, 0f, 0f) : Quaternion.identity);
                go.name = "Test_" + cases[i];
                var p = go.GetComponent<RobotFishParameters>();
                var body = go.GetComponent<Rigidbody>();
                var b = go.GetComponent<CalmWaterBuoyancy>();
                var drag = go.GetComponent<BodyWaterDrag>();
                var mass = go.GetComponent<SlidingMass>();
                float volume = Mathf.PI * 0.05f * 0.05f * 0.4f + 0.15f * 0.1f * 0.05f;
                p.totalMassKg = 1000f * volume * (i == 1 ? 1.1f : (i == 2 || i == 6) ? 0.5f : 1f);
                p.slidingMassKg = 0.5f;
                p.ApplyParameters();
                b.water = water;
                float cbZ = -0.275f * 0.00075f / volume;
                mass.fixedMassCenter = new Vector3(
                    0f, -0.01f, cbZ * p.totalMassKg / (p.totalMassKg - p.slidingMassKg));
                mass.positionM = 0f;
                mass.targetPositionM = i == 4 ? 0.12f : i == 5 ? -0.12f : 0f;
                mass.Step(0f);
                body.linearDamping = 0f;
                body.angularDamping = 0f;
                body.useGravity = true;
                body.constraints = RigidbodyConstraints.None;
                if (i == 3)
                {
                    body.linearVelocity = Vector3.forward;
                }
                yield return new WaitForFixedUpdate();
                float initialV = b.submergedVolume;
                float startY = body.position.y;
                for (int step = 0; step < 150; step++)
                {
                    yield return new WaitForFixedUpdate();
                }
                float speed = body.linearVelocity.magnitude, fy = go.transform.forward.y;
                bool pass = i == 0   ? Mathf.Abs(body.position.y - startY) < 0.01f && speed < 0.01f
                            : i == 1 ? body.position.y < startY - 0.1f
                            : i == 2 ? Mathf.Abs(initialV / volume - 0.5f) < 0.01f &&
                                           Mathf.Abs(body.position.y - water.WaterLevel) < 0.03f
                            : i == 3 ? speed < 0.6f && body.position.z > 0f
                            : i == 4 ? fy < -0.01f && Mathf.Abs(mass.positionM - 0.12f) < 0.001f
                            : i == 5 ? fy > 0.01f && Mathf.Abs(mass.positionM + 0.12f) < 0.001f
                                     : Mathf.Abs(fy) < 0.05f;
                report.AppendLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1:F6},{2:F8},{3:F8},{4:F5},{5:F5},{6:F5},{7:F4},{8}", cases[i], body.mass,
                    volume, initialV, body.position.y, speed, fy, mass.positionM, pass));
                Debug.Log("[PHASE3] " + cases[i] + " " + (pass ? "PASS" : "FAIL"));
                Destroy(go);
                yield return null;
            }
            results = report.ToString();
            finished = true;
            Debug.Log("[PHASE3_RESULTS]\n" + results);
        }
    }
}
