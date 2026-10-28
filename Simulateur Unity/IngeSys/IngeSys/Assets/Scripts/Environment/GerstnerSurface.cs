using System;
using UnityEngine;

namespace IngeSys.Environment
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(FlatWaterSurface))]
    public sealed class GerstnerSurface : MonoBehaviour
    {
        [Serializable]
        public struct Wave
        {
            public float amplitudeM, wavelengthM;
            public Vector2 direction;
            public bool deepWaterDispersion;
            public float speedMps, phaseRad;
            [Range(0, 1)]
            public float steepness;
        }

        public Wave[] waves = {
            new Wave { amplitudeM = 0.08f, wavelengthM = 3f, direction = new Vector2(1, 0.3f),
                       deepWaterDispersion = true, steepness = 0.6f },
            new Wave { amplitudeM = 0.04f, wavelengthM = 1.7f, direction = new Vector2(-0.4f, 1),
                       deepWaterDispersion = true, phaseRad = 1f, steepness = 0.5f },
            new Wave { amplitudeM = 0.02f, wavelengthM = 0.9f, direction = new Vector2(0.7f, -1),
                       deepWaterDispersion = true, phaseRad = 2f, steepness = 0.3f }
        };
        public bool coupleBuoyancy;
        public float rimHeightM = 7f;
        public float freeboardMarginM = 0.05f;
        public int inversionIterations = 24;
        public float inversionToleranceM = 0.00001f;
        public double PhysicalTime
        {
            get {
                return Application.isPlaying ? Time.fixedTimeAsDouble : 0d;
            }
        }
        public float LastResidualM { get; private set; }
        public int InversionFailures { get; private set; }
        FlatWaterSurface config;
        FlatWaterSurface Config
        {
            get {
                if (!config)
                {
                    config = GetComponent<FlatWaterSurface>();
                }

                return config;
            }
        }

        struct PreparedWave
        {
            public float amplitude, k, horizontal, phase;
            public double omega;
            public Vector2 direction;
        }

        PreparedWave[] prepared;
        int preparedFrame = -1;
        Vector2 center, size;
        float meanLevel;

        public void InvalidateCache()
        {
            preparedFrame = -1;
        }

        void OnValidate()
        {
            InvalidateCache();
        }

        void Prepare()
        {
            if (preparedFrame == Time.frameCount && prepared != null)
            {
                return;
            }
            preparedFrame = Time.frameCount;
            center = new Vector2(transform.position.x, transform.position.z);
            size = Config.SurfaceSize;
            meanLevel = Config.WaterLevel;
            int count = waves == null ? 0 : waves.Length;
            if (prepared == null || prepared.Length != count)
            {
                prepared = new PreparedWave[count];
            }
            float amplitudeScale = AmplitudeScale, horizontalScale = HorizontalScale;
            for (int i = 0; i < count; i++)
            {
                var w = waves[i];
                float k = 2 * Mathf.PI / Mathf.Max(0.1f, w.wavelengthM),
                      a = Mathf.Max(0, w.amplitudeM) * amplitudeScale;
                prepared[i] = new PreparedWave {
                    amplitude = a,
                    k = k,
                    horizontal = Mathf.Clamp01(w.steepness) * a * horizontalScale,
                    phase = w.phaseRad,
                    direction = w.direction.sqrMagnitude > 0.00001f ? w.direction.normalized
                                                                    : Vector2.right,
                    omega = w.deepWaterDispersion ? Math.Sqrt(Math.Abs(Physics.gravity.y) * k)
                                                  : k * w.speedMps
                };
            }
        }

        float AmplitudeScale
        {
            get {
                float sum = 0;
                if (waves != null)
                {
                    foreach (var w in waves)
                    {
                        sum += Mathf.Max(0, w.amplitudeM);
                    }
                }
                return sum > 0? Mathf.Clamp01((rimHeightM - Mathf.Max(0, freeboardMarginM) -
                                               Config.WaterLevel) /
                                              sum) : 1f;
            }
        }
        float HorizontalScale
        {
            get {
                float bound = 0;
                if (waves != null)
                {
                    foreach (var w in waves)
                    {
                        bound +=
                            Mathf.Clamp01(w.steepness) * Mathf.Max(0, w.amplitudeM) *
                            AmplitudeScale *
                            (2 * Mathf.PI / Mathf.Max(0.1f, w.wavelengthM) +
                             Mathf.PI / Config.SurfaceSize.x + Mathf.PI / Config.SurfaceSize.y);
                    }
                }
                return bound > 0.65f? 0.65f / bound : 1f;
            }
        }

        float Envelope(Vector2 q)
        {
            float u = (q.x - center.x) / size.x + 0.5f, v = (q.y - center.y) / size.y + 0.5f;
            if (u <= 0 || u >= 1 || v <= 0 || v >= 1)
            {
                return 0;
            }
            float a = Mathf.Sin(Mathf.PI * u), b = Mathf.Sin(Mathf.PI * v);
            return a * a * b * b;
        }

        bool Contains(Vector2 xz)
        {
            Prepare();
            return Mathf.Abs(xz.x - center.x) <= size.x / 2 &&
                   Mathf.Abs(xz.y - center.y) <= size.y / 2;
        }

        public Vector3 EvaluateParametric(Vector2 q, double time)
        {
            Prepare();
            float envelope = Envelope(q);
            Vector3 result = new Vector3(q.x, meanLevel, q.y);
            foreach (var w in prepared)
            {
                Vector2 d = w.direction;
                float theta =
                    (float)((w.k * Vector2.Dot(d, q) - w.omega * time + w.phase) % (2 * Math.PI));
                float horizontal = envelope * w.horizontal * Mathf.Cos(theta);
                result.x += d.x * horizontal;
                result.z += d.y * horizontal;
                result.y += envelope * w.amplitude * Mathf.Sin(theta);
            }
            return result;
        }

        public bool TryGetHeightAtWorldXZ(Vector2 xz, double time, out float height)
        {
            height = Config.WaterLevel;
            if (!Contains(xz))
            {
                return false;
            }
            Vector2 q = xz;
            for (int i = 0; i < Mathf.Clamp(inversionIterations, 4, 64); i++)
            {
                var p = EvaluateParametric(q, time);
                var error = new Vector2(p.x - xz.x, p.z - xz.y);
                if (error.sqrMagnitude <= inversionToleranceM * inversionToleranceM)
                {
                    break;
                }
                q -= error;
            }
            var final = EvaluateParametric(q, time);
            LastResidualM = Vector2.Distance(xz, new Vector2(final.x, final.z));
            if (LastResidualM > Mathf.Max(0.000001f, inversionToleranceM))
            {
                InversionFailures++;
                return false;
            }
            height = final.y;
            return true;
        }

        public float GetHeightAtWorldXZ(float x, float z)
        {
            float h;
            return TryGetHeightAtWorldXZ(new Vector2(x, z), PhysicalTime, out h)
                       ? h
                       : float.NegativeInfinity;
        }
    }
}
