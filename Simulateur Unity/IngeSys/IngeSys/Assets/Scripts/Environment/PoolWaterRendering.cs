using System.Collections.Generic;
using UnityEngine;

namespace IngeSys.Environment
{
    /// <summary>Built-in render pipeline refraction depth and planar reflection; no water physics.</summary>
    [ExecuteAlways, RequireComponent(typeof(FlatWaterSurface), typeof(Renderer))]
    public sealed class PoolWaterRendering : MonoBehaviour
    {
        [Range(0.2f, 1)] public float reflectionResolution = 0.5f;
        [Min(0.001f)] public float clipOffset = 0.03f;
        Camera reflectionCamera;
        RenderTexture reflectionTexture;
        Renderer waterRenderer;
        MaterialPropertyBlock properties;
        readonly Dictionary<Camera, DepthTextureMode> originalDepth = new Dictionary<Camera, DepthTextureMode>();
        static bool reflecting;

        void OnEnable()
        {
            waterRenderer = GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
            Camera.onPreCull += EnableDepth;
        }

        void EnableDepth(Camera camera)
        {
            if (reflecting || !camera || !waterRenderer || !waterRenderer.enabled) return;
            if (!originalDepth.ContainsKey(camera)) originalDepth.Add(camera, camera.depthTextureMode);
            camera.depthTextureMode |= DepthTextureMode.Depth;
        }

        void OnWillRenderObject()
        {
            var source = Camera.current;
            if (!source || reflecting || !enabled) return;
            float level = GetComponent<FlatWaterSurface>().WaterLevel;
            waterRenderer.GetPropertyBlock(properties);
            properties.SetFloat("_ReflectionAvailable", 0);
            if (source.transform.position.y > level + 0.02f)
            {
                EnsureResources(source);
                reflectionCamera.CopyFrom(source);
                reflectionCamera.enabled = false;
                reflectionCamera.targetTexture = reflectionTexture;
                reflectionCamera.depthTextureMode = DepthTextureMode.None;
                reflectionCamera.cullingMask = source.cullingMask & ~(1 << gameObject.layer);
                Vector4 plane = new Vector4(0, 1, 0, -level);
                var mirror = Matrix4x4.identity;
                for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 4; column++)
                        mirror[row,column] -= 2 * plane[row] * plane[column];
                reflectionCamera.transform.position = mirror.MultiplyPoint(source.transform.position);
                reflectionCamera.transform.rotation = source.transform.rotation;
                reflectionCamera.worldToCameraMatrix = source.worldToCameraMatrix * mirror;
                var point = reflectionCamera.worldToCameraMatrix.MultiplyPoint(new Vector3(0, level + clipOffset, 0));
                var normal = reflectionCamera.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
                reflectionCamera.projectionMatrix = source.CalculateObliqueMatrix(
                    new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal)));
                bool oldCulling = GL.invertCulling;
                try
                {
                    reflecting = true;
                    GL.invertCulling = !oldCulling;
                    reflectionCamera.Render();
                }
                finally { GL.invertCulling = oldCulling; reflecting = false; }
                properties.SetTexture("_ReflectionTex", reflectionTexture);
                properties.SetMatrix("_ReflectionVP", GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix, true)
                    * reflectionCamera.worldToCameraMatrix);
                properties.SetFloat("_ReflectionAvailable", 1);
            }
            waterRenderer.SetPropertyBlock(properties);
        }

        void EnsureResources(Camera source)
        {
            if (!reflectionCamera)
            {
                var go = new GameObject("PoolWaterReflection") { hideFlags = HideFlags.HideAndDontSave };
                reflectionCamera = go.AddComponent<Camera>();
                reflectionCamera.enabled = false;
            }
            int width = Mathf.Clamp(Mathf.RoundToInt(source.pixelWidth * reflectionResolution), 128, 1024);
            int height = Mathf.Clamp(Mathf.RoundToInt(source.pixelHeight * reflectionResolution), 128, 1024);
            if (reflectionTexture && reflectionTexture.width == width && reflectionTexture.height == height) return;
            if (reflectionTexture) Release(reflectionTexture);
            reflectionTexture = new RenderTexture(width, height, 24) { name = "PoolWaterReflection", hideFlags = HideFlags.HideAndDontSave };
            reflectionTexture.Create();
        }

        void OnDisable()
        {
            Camera.onPreCull -= EnableDepth;
            foreach (var item in originalDepth) if (item.Key) item.Key.depthTextureMode = item.Value;
            originalDepth.Clear();
            if (waterRenderer) waterRenderer.SetPropertyBlock(null);
            if (reflectionCamera) Release(reflectionCamera.gameObject);
            if (reflectionTexture) Release(reflectionTexture);
        }

        static void Release(Object obj)
        {
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }
    }
}
