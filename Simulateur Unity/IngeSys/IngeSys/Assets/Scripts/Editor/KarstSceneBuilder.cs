using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IngeSys.EditorTools
{
    // Creates derived assets only. Imported pool and cave sources are never edited.
    public static class KarstSceneBuilder
    {
        const string Folder = "Assets/CAD/Karst";
        const string ScenePath = "Assets/Scenes/RobotFishKatarstique.unity";

        [MenuItem("IngeSys/Create karst scene (once)")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
                throw new System.InvalidOperationException("Karst scene already exists; refusing to overwrite.");
            var current = EditorSceneManager.GetActiveScene();
            if (current.isDirty || EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Save current scene and exit Play mode first.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/RobotFishManualDemo.unity");
            EditorSceneManager.SaveScene(scene, ScenePath, true);
            scene = EditorSceneManager.OpenScene(ScenePath);
            var cut = new Bounds(new Vector3(0, -5.5f, 0), new Vector3(3, 15, 3));
            var pool = Object.FindObjectsByType<MeshFilter>()
                .First(m => AssetDatabase.GetAssetPath(m.sharedMesh).EndsWith("Piscine.obj"));
            // Clip the entire floor thickness, retaining all pool walls and the CAD original.
            var poolMesh = Cut(pool.sharedMesh, pool.transform, cut);
            AssetDatabase.CreateAsset(poolMesh, Folder + "/PoolWithOpening.asset");
            pool.sharedMesh = poolMesh;
            var floor = GameObject.Find("PoolFloorCollider");
            floor.SetActive(false);
            var poolCollision = pool.gameObject.AddComponent<MeshCollider>();
            poolCollision.sharedMesh = poolMesh;
            pool.gameObject.isStatic = true;

            CompleteEnvironment();
        }

        public static void CompleteEnvironment()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath || GameObject.Find("KarstEnvironment_Flooded_NoLamp"))
                throw new System.InvalidOperationException("Expected an unfinished karst scene; refusing to overwrite.");
            var cut = new Bounds(new Vector3(0, -5.5f, 0), new Vector3(3, 15, 3));
            var rock = new Material(Shader.Find("Standard"));
            rock.name = "KarstRock";
            rock.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/cave3_копия.png"));
            rock.SetFloat("_Glossiness", 0.15f);
            rock.SetFloat("_Cull", 0);
            AssetDatabase.CreateAsset(rock, Folder + "/KarstRock.mat");
            var root = new GameObject("KarstEnvironment_Flooded_NoLamp");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/All_mine.fbx");
            var cave = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(cave, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            cave.name = "CaveNetwork";
            cave.transform.SetParent(root.transform);
            // Authored assembled network starts at Object010; mine_* is the separate module catalogue.
            cave.transform.localScale = Vector3.one * 0.5f;
            cave.transform.position = new Vector3(37.59f * 0.5f, -12f + 0.66f * 0.5f, 48.68f * 0.5f);
            foreach (var mf in cave.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.name.StartsWith("mine_")) { mf.gameObject.SetActive(false); continue; }
                var derived = Cut(mf.sharedMesh, mf.transform, cut);
                // Both windings ensure static cave collisions work from either side of thin imported walls.
                var triangles = derived.triangles;
                derived.triangles = triangles.Concat(triangles.Reverse()).ToArray();
                AssetDatabase.CreateAsset(derived, Folder + "/Cave_" + mf.name + ".asset");
                mf.sharedMesh = derived;
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = derived;
                mf.gameObject.isStatic = true;
                mf.gameObject.layer = 0;
                var renderer = mf.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = Enumerable.Repeat(rock, derived.subMeshCount).ToArray();
                renderer.receiveShadows = true;
                renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            }
            // Open shaft: no cap at either end; bottom overlaps the cut cave ceiling.
            var shaft = new GameObject("AccessShaft_3m");
            shaft.transform.SetParent(root.transform);
            Box(shaft.transform, rock, "West", new Vector3(-1.65f,-5.5f,0), new Vector3(.3f,13,3.6f));
            Box(shaft.transform, rock, "East", new Vector3(1.65f,-5.5f,0), new Vector3(.3f,13,3.6f));
            Box(shaft.transform, rock, "North", new Vector3(0,-5.5f,1.65f), new Vector3(3,13,.3f));
            Box(shaft.transform, rock, "South", new Vector3(0,-5.5f,-1.65f), new Vector3(3,13,.3f));
            // Keep the source scene lighting. Cave darkness comes from geometry and shadows.
            Object.FindAnyObjectByType<IngeSys.Environment.FlatWaterSurface>().floodedBeyondSurface = true;
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
        }

        static void Box(Transform parent, Material mat, string name, Vector3 pos, Vector3 size)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name; g.transform.SetParent(parent); g.transform.position = pos;
            g.transform.localScale = size; g.layer = 0; g.isStatic = true;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
        }

        // Subtract an axis-aligned world box by partitioning each triangle against its six planes.
        // Intersections interpolate UVs; no triangle crossing the opening is silently discarded.
        struct Vertex { public Vector3 p; public Vector2 uv; }
        static Mesh Cut(Mesh source, Transform transform, Bounds box)
        {
            var verts = source.vertices; var uvs = source.uv;
            var output = new List<Vector3>(); var tex = new List<Vector2>(); var indices = new List<int[]>();
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                var result = new List<int>(); var tri = source.GetTriangles(sub);
                for (int t = 0; t < tri.Length; t += 3)
                {
                    var inside = new List<Vertex>();
                    for (int k=0;k<3;k++) { int i=tri[t+k]; inside.Add(new Vertex {p=transform.TransformPoint(verts[i]),uv=uvs.Length>i?uvs[i]:Vector2.zero}); }
                    for (int plane=0;plane<6 && inside.Count>0;plane++)
                    {
                        var outside = Clip(inside, box, plane, false);
                        for (int k=1;k+1<outside.Count;k++)
                            foreach (int i in new[]{0,k,k+1}) { result.Add(output.Count); output.Add(transform.InverseTransformPoint(outside[i].p)); tex.Add(outside[i].uv); }
                        inside = Clip(inside, box, plane, true);
                    }
                }
                indices.Add(result.ToArray());
            }
            var mesh = new Mesh {name=source.name+"_KarstOpening",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(output); mesh.SetUVs(0,tex); mesh.subMeshCount=indices.Count;
            for(int i=0;i<indices.Count;i++) mesh.SetTriangles(indices[i],i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static List<Vertex> Clip(List<Vertex> polygon, Bounds box, int plane, bool keepInside)
        {
            var result = new List<Vertex>(); int axis=plane/2; bool minimum=plane%2==0;
            float boundary=minimum?box.min[axis]:box.max[axis];
            for(int i=0;i<polygon.Count;i++)
            {
                var a=polygon[i]; var b=polygon[(i+1)%polygon.Count];
                float da=minimum?a.p[axis]-boundary:boundary-a.p[axis];
                float db=minimum?b.p[axis]-boundary:boundary-b.p[axis];
                bool ia=keepInside?da>=0:da<0, ib=keepInside?db>=0:db<0;
                if(ia) result.Add(a);
                if(ia!=ib) {float t=da/(da-db);result.Add(new Vertex{p=Vector3.Lerp(a.p,b.p,t),uv=Vector2.Lerp(a.uv,b.uv,t)});}
            }
            return result;
        }
    }
}
