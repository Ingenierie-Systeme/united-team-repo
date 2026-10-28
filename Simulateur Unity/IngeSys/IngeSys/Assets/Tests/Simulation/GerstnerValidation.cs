using System.Collections;
using System.Text;
using UnityEngine;
using IngeSys.Environment;
using IngeSys.Robot;

namespace IngeSys.Diagnostics
{
    public sealed class GerstnerValidation : MonoBehaviour
    {
        public GerstnerSurface surface;
        public GameObject robotPrefab;
        [TextArea(8, 30)]
        public string results;
        public bool finished;

        IEnumerator Start()
        {
            var report = new StringBuilder();
            var original = (GerstnerSurface.Wave[])surface.waves.Clone();
            for (int i = 0; i < surface.waves.Length; i++)
            {
                var w = surface.waves[i];
                w.amplitudeM = 0;
                surface.waves[i] = w;
            }
            surface.InvalidateCache();
            float flatError = 0;
            for (int i = 0; i < 20; i++)
            {
                float h;
                surface.TryGetHeightAtWorldXZ(new Vector2(i - 9.5f, 0), 2.5, out h);
                flatError = Mathf.Max(flatError, Mathf.Abs(h - 6.5f));
            }
            report.AppendLine("zeroAmplitude maxErrorM=" + flatError +
                              " pass=" + (flatError < 0.00001f));
            surface.waves = original;
            surface.InvalidateCache();
            float heightError = 0, outside = 0, maxY = 0, residual = 0;
            int failures = 0;
            for (int t = 0; t <= 20; t += 2)
            {
                for (int z = 0; z <= 24; z++)
                {
                    for (int x = 0; x <= 40; x++)
                    {
                        var point = surface.EvaluateParametric(
                            new Vector2(-10 + x * 0.5f, -6 + z * 0.5f), t);
                        float h;
                        if (!surface.TryGetHeightAtWorldXZ(new Vector2(point.x, point.z), t, out h))
                        {
                            failures++;
                        }
                        heightError = Mathf.Max(heightError, Mathf.Abs(point.y - h));
                        residual = Mathf.Max(residual, surface.LastResidualM);
                        outside =
                            Mathf.Max(outside, Mathf.Abs(point.x) - 10, Mathf.Abs(point.z) - 6);
                        maxY = Mathf.Max(maxY, point.y);
                    }
                }
            }
            report.AppendLine(
                "sampledTimes0to20 heightErrorM=" + heightError + " residualM=" + residual +
                " failures=" + failures + " outsideM=" + outside + " maxY=" + maxY +
                " pass=" + (failures == 0 && heightError < 0.00001f && outside == 0 && maxY < 7));
            surface.coupleBuoyancy = true;
            for (int scenario = 0; scenario < 2; scenario++)
            {
                var go = Instantiate(robotPrefab, new Vector3(0, scenario == 0 ? 4f : 6.5f, 0),
                                     Quaternion.identity);
                var p = go.GetComponent<RobotFishParameters>();
                var b = go.GetComponent<CalmWaterBuoyancy>();
                var rb = go.GetComponent<Rigidbody>();
                b.water = surface.GetComponent<FlatWaterSurface>();
                go.GetComponent<MeanFinPropulsion>().propulsionEnabled = false;
                go.GetComponent<FinServo>().animate = false;
                if (scenario == 1)
                {
                    p.totalMassKg *= 0.5f;
                    p.ApplyParameters();
                    var m = go.GetComponent<SlidingMass>();
                    float v = Mathf.PI * 0.05f * 0.05f * 0.4f + 0.00075f;
                    m.fixedMassCenter = new Vector3(0, -0.01f,
                                                    (-0.275f * 0.00075f / v) * p.totalMassKg /
                                                        (p.totalMassKg - p.slidingMassKg));
                    m.Step(0);
                }
                float minF = float.PositiveInfinity, maxF = 0, maxSpeed = 0;
                for (int n = 0; n < 200; n++)
                {
                    yield return new WaitForFixedUpdate();
                    minF = Mathf.Min(minF, b.buoyancyN);
                    maxF = Mathf.Max(maxF, b.buoyancyN);
                    maxSpeed = Mathf.Max(maxSpeed, rb.linearVelocity.magnitude);
                }
                bool pass =
                    scenario == 0 ? maxF - minF < 0.001f && maxSpeed < 0.001f : maxF - minF > 0.5f;
                report.AppendLine((scenario == 0 ? "fully_submerged" : "partially_submerged") +
                                  " minF=" + minF + " maxF=" + maxF + " maxSpeed=" + maxSpeed +
                                  " finalY=" + rb.position.y + " pass=" + pass);
                Destroy(go);
                yield return null;
            }
            results = report.ToString();
            finished = true;
            Debug.Log("[PHASE5_RESULTS]\n" + results);
        }
    }
}
