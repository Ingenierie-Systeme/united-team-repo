using UnityEngine;
using IngeSys.Environment;

namespace IngeSys.Robot
{
    [RequireComponent(typeof(Rigidbody), typeof(RobotFishParameters))]
    public sealed class CalmWaterBuoyancy : MonoBehaviour
    {
        public FlatWaterSurface water;
        [Min(1f)]
        public float density = 1000f;
        public bool includeFinVolume = true;
        public bool useCadFinCenter;
        public Vector3 cadFinCenterLocal;

        [Range(4, 32)]
        public int axialSamples = 12;
        [Range(2, 16)]
        public int radialSamples = 6;
        [Range(8, 64)]
        public int angularSamples = 24;
        [Min(0.0001f)]
        public float interfaceSmoothingM = 0.004f;
        public float hullVolume, finVolume, submergedVolume, buoyancyN, weightN;
        Rigidbody body;
        RobotFishParameters p;
        public float TotalVolume
        {
            get {
                return hullVolume + finVolume;
            }
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            p = GetComponent<RobotFishParameters>();
        }

        void FixedUpdate()
        {
            hullVolume = Mathf.PI * p.hullDiameter * p.hullDiameter * 0.25f * p.hullLength;
            finVolume = includeFinVolume ? p.finLength * p.finHeight * p.finThickness : 0f;
            submergedVolume = 0f;
            if (!water)
            {
                return;
            }
            int nz = Mathf.Max(4, axialSamples), nr = Mathf.Max(2, radialSamples),
                na = Mathf.Max(8, angularSamples);
            float dv = hullVolume / (nz * nr * na);
            for (int z = 0; z < nz; z++)
            {
                for (int r = 0; r < nr; r++)
                {
                    for (int a = 0; a < na; a++)
                    {
                        float radius = p.hullDiameter * 0.5f * Mathf.Sqrt((r + 0.5f) / nr);
                        float angle = 2f * Mathf.PI * (a + 0.5f) / na;
                        Sample(transform.TransformPoint(
                                   new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle),
                                               p.hullLength * ((z + 0.5f) / nz - 0.5f))),
                               dv);
                    }
                }
            }
            if (includeFinVolume && p.fin)
            {
                for (int x = 0; x < 4; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        for (int z = 0; z < 12; z++)
                        {
                            // Mean fin volume stays in its neutral pose: visual beating adds no
                            // alternating buoyancy torque.
                            Vector3 finPoint = useCadFinCenter
                                ? cadFinCenterLocal + new Vector3(
                                    p.finThickness * ((x + .5f) / 4 - .5f),
                                    p.finHeight * ((y + .5f) / 8 - .5f),
                                    p.finLength * ((z + .5f) / 12 - .5f))
                                : new Vector3(p.finThickness*((x+.5f)/4-.5f),
                                    p.finHeight*((y+.5f)/8-.5f),
                                    -p.hullLength/2-p.finLength*(z+.5f)/12);
                            Sample(transform.TransformPoint(finPoint), finVolume/(4*8*12));
                        }
                    }
                }
            }
            buoyancyN = density * Mathf.Abs(Physics.gravity.y) * submergedVolume;
            weightN = body.mass * Mathf.Abs(Physics.gravity.y);
        }

        void Sample(Vector3 point, float volume)
        {
            float fraction =
                Mathf.Clamp01(0.5f + (water.GetHeightAtWorldXZ(point.x, point.z) - point.y) /
                                         Mathf.Max(0.0001f, interfaceSmoothingM));
            float v = volume * fraction;
            submergedVolume += v;
            body.AddForceAtPosition(Vector3.up * (density * Mathf.Abs(Physics.gravity.y) * v),
                                    point, ForceMode.Force);
        }
    }
}
