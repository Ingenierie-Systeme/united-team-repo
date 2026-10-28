using UnityEngine;

namespace IngeSys.Environment
{
    [ExecuteAlways,
     RequireComponent(typeof(GerstnerSurface), typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GerstnerMesh : MonoBehaviour
    {
        [Range(16, 240)]
        public int columns = 160;
        [Range(16, 160)]
        public int rows = 96;
        Mesh mesh;
        Vector3[] vertices;
        int builtColumns, builtRows;
        public double EvaluatedTime { get; private set; }

        void OnEnable()
        {
            Rebuild();
        }

        void Rebuild()
        {
            columns = Mathf.Clamp(columns, 16, 240);
            rows = Mathf.Clamp(rows, 16, 160);
            if (mesh)
            {
                if (Application.isPlaying)
                {
                    Destroy(mesh);
                }
                else
                {
                    DestroyImmediate(mesh);
                }
            }
            mesh = new Mesh { name = "Gerstner_RuntimeMesh", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            vertices = new Vector3[(columns + 1) * (rows + 1)];
            var indices = new int[columns * rows * 6];
            var uv = new Vector2[vertices.Length];
            int t = 0;
            for (int z = 0; z <= rows; z++)
            {
                for (int x = 0; x <= columns; x++)
                {
                    uv[z * (columns + 1) + x] = new Vector2((float)x / columns, (float)z / rows);
                }
            }
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int a = z * (columns + 1) + x, b = a + columns + 1;
                    indices[t++] = a;
                    indices[t++] = b;
                    indices[t++] = a + 1;
                    indices[t++] = a + 1;
                    indices[t++] = b;
                    indices[t++] = b + 1;
                }
            }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = indices;
            GetComponent<MeshFilter>().sharedMesh = mesh;
            builtColumns = columns;
            builtRows = rows;
            UpdateMesh();
        }

        void LateUpdate()
        {
            if (!mesh || columns != builtColumns || rows != builtRows)
            {
                Rebuild();
            }
            else
            {
                UpdateMesh();
            }
        }

        void UpdateMesh()
        {
            var surface = GetComponent<GerstnerSurface>();
            var config = GetComponent<FlatWaterSurface>();
            var size = config.SurfaceSize;
            EvaluatedTime = surface.PhysicalTime;
            for (int z = 0; z <= rows; z++)
            {
                for (int x = 0; x <= columns; x++)
                {
                    var q = new Vector2(transform.position.x + size.x * ((float)x / columns - 0.5f),
                                        transform.position.z + size.y * ((float)z / rows - 0.5f));
                    vertices[z * (columns + 1) + x] = transform.InverseTransformPoint(
                        surface.EvaluateParametric(q, EvaluatedTime));
                }
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        void OnDisable()
        {
            if (mesh)
            {
                if (Application.isPlaying)
                {
                    Destroy(mesh);
                }
                else
                {
                    DestroyImmediate(mesh);
                }
            }
        }
    }
}
