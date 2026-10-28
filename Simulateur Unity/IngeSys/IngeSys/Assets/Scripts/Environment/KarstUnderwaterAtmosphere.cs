using UnityEngine;
using UnityEngine.Rendering;

namespace IngeSys.Environment
{
    /// <summary>Visual atmosphere only: does not introduce water currents or physical forces.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class KarstUnderwaterAtmosphere : MonoBehaviour
    {
        public Camera baseCamera;
        public Camera fpsCamera;
        public IngeSys.Diagnostics.RobotCameraSwitch cameraSwitch;
        public Light headlamp;
        public ParticleSystem suspendedParticles;
        [Tooltip("World Y: start below the pool, full cave atmosphere deeper in the shaft.")]
        public float transitionTopY = 0.5f;
        public float transitionBottomY = -8f;
        [Range(0, 1)] public float caveAmbientMultiplier = 0.012f;
        [Min(0)] public float caveFogDensity = 0.16f;
        public Color caveFogColor = new Color(0.003f, 0.012f, 0.018f, 1);
        public float Blend { get; private set; }

        bool originalFog;
        FogMode originalFogMode;
        Color originalFogColor, originalAmbientLight;
        float originalFogDensity, originalFogStart, originalFogEnd;
        float originalAmbientIntensity, originalReflectionIntensity;
        SphericalHarmonicsL2 originalProbe;
        Material particleMaterial, originalParticleMaterial;
        bool captured;

        void OnEnable()
        {
            originalFog = RenderSettings.fog;
            originalFogMode = RenderSettings.fogMode;
            originalFogColor = RenderSettings.fogColor;
            originalFogDensity = RenderSettings.fogDensity;
            originalFogStart = RenderSettings.fogStartDistance;
            originalFogEnd = RenderSettings.fogEndDistance;
            originalAmbientIntensity = RenderSettings.ambientIntensity;
            originalAmbientLight = RenderSettings.ambientLight;
            originalReflectionIntensity = RenderSettings.reflectionIntensity;
            originalProbe = RenderSettings.ambientProbe;
            captured = true;
            if (suspendedParticles)
            {
                var renderer = suspendedParticles.GetComponent<ParticleSystemRenderer>();
                originalParticleMaterial = renderer.sharedMaterial;
                particleMaterial = new Material(originalParticleMaterial);
                renderer.sharedMaterial = particleMaterial;
                suspendedParticles.Play();
            }
        }

        void LateUpdate()
        {
            var camera = cameraSwitch && cameraSwitch.ActiveCamera
                ? cameraSwitch.ActiveCamera
                : (fpsCamera && fpsCamera.isActiveAndEnabled ? fpsCamera : baseCamera);
            if (!camera) return;
            Blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(transitionTopY, transitionBottomY, camera.transform.position.y));
            float ambient = Mathf.Lerp(1, caveAmbientMultiplier, Blend);
            RenderSettings.ambientIntensity = originalAmbientIntensity * ambient;
            RenderSettings.ambientLight = originalAmbientLight * ambient;
            var probe = originalProbe;
            for (int rgb = 0; rgb < 3; rgb++)
                for (int coefficient = 0; coefficient < 9; coefficient++)
                    probe[rgb, coefficient] *= ambient;
            RenderSettings.ambientProbe = probe;
            RenderSettings.reflectionIntensity = originalReflectionIntensity * ambient;
            if (Blend > 0)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = Color.Lerp(originalFog ? originalFogColor : caveFogColor, caveFogColor, Blend);
                RenderSettings.fogDensity = Mathf.Lerp(originalFog ? originalFogDensity : 0, caveFogDensity, Blend);
            }
            else RestoreFog();
            if (!suspendedParticles) return;
            suspendedParticles.transform.position = camera.transform.position;
            var emission = suspendedParticles.emission;
            emission.rateOverTime = 65f * Blend;
            if (Blend == 0 && suspendedParticles.particleCount > 0) suspendedParticles.Clear();
            if (!particleMaterial) return;
            particleMaterial.SetFloat("_CaveBlend", Blend);
            if (headlamp)
            {
                var p = headlamp.transform.position;
                var d = headlamp.transform.forward;
                particleMaterial.SetVector("_LampPositionRange", new Vector4(p.x,p.y,p.z,headlamp.range));
                particleMaterial.SetVector("_LampDirectionCos", new Vector4(d.x,d.y,d.z,Mathf.Cos(headlamp.spotAngle*Mathf.Deg2Rad*.5f)));
                particleMaterial.SetColor("_LampColor", headlamp.isActiveAndEnabled ? headlamp.color*headlamp.intensity : Color.black);
            }
        }

        void RestoreFog()
        {
            RenderSettings.fog = originalFog;
            RenderSettings.fogMode = originalFogMode;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogDensity = originalFogDensity;
            RenderSettings.fogStartDistance = originalFogStart;
            RenderSettings.fogEndDistance = originalFogEnd;
        }

        void OnDisable()
        {
            if (captured)
            {
                RestoreFog();
                RenderSettings.ambientIntensity = originalAmbientIntensity;
                RenderSettings.ambientLight = originalAmbientLight;
                RenderSettings.ambientProbe = originalProbe;
                RenderSettings.reflectionIntensity = originalReflectionIntensity;
            }
            if (suspendedParticles)
            {
                suspendedParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                suspendedParticles.GetComponent<ParticleSystemRenderer>().sharedMaterial = originalParticleMaterial;
            }
            captured = false;
            if (particleMaterial) Destroy(particleMaterial);
        }
    }
}
