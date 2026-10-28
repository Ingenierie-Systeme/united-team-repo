using UnityEngine;

namespace IngeSys.Environment
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FlatWaterSurface : MonoBehaviour
    {
        [SerializeField]
        float waterLevel = 6.5f;

        [SerializeField]
        Vector2 surfaceSize = new Vector2(20f, 12f);

        [Tooltip("Galeries inondées : niveau moyen hors de la surface visible, sans courant ni vague supplémentaire.")]
        public bool floodedBeyondSurface;

        public float WaterLevel
        {
            get {
                return waterLevel;
            }
        }

        public Vector2 SurfaceSize
        {
            get {
                return surfaceSize;
            }
        }
        GerstnerSurface cachedWaves;

        public float GetHeightAtWorldXZ(float x, float z)
        {
            var waves = cachedWaves ? cachedWaves : (cachedWaves = GetComponent<GerstnerSurface>());
            float height = waves && waves.enabled && waves.coupleBuoyancy
                ? waves.GetHeightAtWorldXZ(x, z) : waterLevel;
            return floodedBeyondSurface && float.IsNegativeInfinity(height) ? waterLevel : height;
        }

        void Awake()
        {
            ApplyGeometry();
        }

        void OnValidate()
        {
            surfaceSize.x = Mathf.Max(0.01f, surfaceSize.x);
            surfaceSize.y = Mathf.Max(0.01f, surfaceSize.y);
            ApplyGeometry();
        }

        void ApplyGeometry()
        {
            Vector3 position = transform.position;
            position.y = waterLevel;
            transform.position = position;

            // Plan 10mx10m en scale 1
            transform.localScale = new Vector3(surfaceSize.x / 10f, 1f, surfaceSize.y / 10f);
        }
    }
}
