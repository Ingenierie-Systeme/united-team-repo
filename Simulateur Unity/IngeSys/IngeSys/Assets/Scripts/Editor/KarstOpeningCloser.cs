using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IngeSys.EditorTools
{
    /// <summary>Seals exposed module portals and stitches paired rims, keeping the pool access open.</summary>
    public static class KarstOpeningCloser
    {
        class Ring
        {
            public string name;
            public Vector3[] points;
            public Vector3 center;
            public int axis;
        }

        [MenuItem("IngeSys/Seal cave exterior openings (once)")]
        public static void Seal()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/RobotFishKatarstique.unity" || EditorApplication.isPlaying)
                throw new InvalidOperationException("Open the karst scene outside Play mode first.");
            if (GameObject.Find("KarstExteriorSeals"))
                throw new InvalidOperationException("Exterior seals already exist; refusing to overwrite.");
            var cave = GameObject.Find("CaveNetwork");
            var rings = cave.GetComponentsInChildren<MeshFilter>().SelectMany(m => FindPortals(m)).ToList();
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/CAD/Karst/KarstRock.mat");
            var root = new GameObject("KarstExteriorSeals");
            root.transform.SetParent(cave.transform.parent);
            int caps = 0, seams = 0;
            while (rings.Count > 0)
            {
                var a = rings[0]; rings.RemoveAt(0);
                var b = rings.Where(r => r.axis == a.axis && Vector3.Distance(r.center, a.center) < .02f)
                    .OrderBy(r => Vector3.Distance(r.center, a.center)).FirstOrDefault();
                if (b == null)
                {
                    Make(root.transform, material, "RockPlug_" + a.name + "_" + caps++, Cap(a));
                    continue;
                }
                rings.Remove(b);
                if (a.points.Length != b.points.Length)
                    throw new InvalidOperationException("Portal vertex counts differ: " + a.name + "/" + b.name);
                Make(root.transform, material, "Seam_" + a.name + "_" + b.name, Stitch(a,b));
                seams++;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Karst sealed: " + caps + " exterior plugs, " + seams + " stitched internal rims. Pool shaft retained.");
        }

        [MenuItem("IngeSys/Seal shaft junction and small cracks (once)")]
        public static void SealShaftJunction()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/RobotFishKatarstique.unity" || EditorApplication.isPlaying)
                throw new InvalidOperationException("Open the karst scene outside Play mode first.");
            if (GameObject.Find("SealedShaftLining"))
                throw new InvalidOperationException("Shaft lining already exists; refusing to overwrite.");
            var shaft = GameObject.Find("AccessShaft_3m");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/CAD/Karst/KarstRock.mat");
            var root = new GameObject("SealedShaftLining");
            root.transform.SetParent(shaft.transform, false);
            // Keep original panels available but inactive. The new lining includes the complete junction.
            foreach (Transform child in shaft.transform)
                if (child != root.transform) child.gameObject.SetActive(false);
            float bottom=-13.4f, top=1.05f, doorBottom=-12.7f, doorTop=-11.8f;
            for (int side=0;side<4;side++)
            {
                bool xWall=side<2; float sign=side%2==0?-1:1;
                Panel(root.transform,material,"Upper_"+side,xWall,sign,0,(doorTop+top)/2,3.6f,top-doorTop);
                Panel(root.transform,material,"Lower_"+side,xWall,sign,0,(bottom+doorBottom)/2,3.6f,doorBottom-bottom);
                float width=(3.6f-.9f)/2;
                foreach(float lateral in new[]{-1f,1f})
                    Panel(root.transform,material,"DoorJamb_"+side+"_"+lateral,xWall,sign,
                        lateral*(.45f+width/2),(doorBottom+doorTop)/2,width,doorTop-doorBottom);
            }
            Solid(root.transform,material,"ShaftFoot_Floor",new Vector3(0,-13.5f,0),new Vector3(3.6f,.2f,3.6f));
            var cracks=new GameObject("SmallRockCrackSeals");cracks.transform.SetParent(root.transform,false);
            int count=0;
            foreach(var ring in GameObject.Find("CaveNetwork").GetComponentsInChildren<MeshFilter>().SelectMany(m=>FindPortals(m,true)))
                Make(cracks.transform,material,"Crack_"+ring.name+"_"+count++,Cap(ring));
            // Bias is a distance in world units; the previous 0.4m normal bias exceeded wall thickness.
            var sun=GameObject.Find("Directional Light").GetComponent<Light>();
            sun.shadowNormalBias=.02f;sun.shadowBias=.01f;
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Debug.Log("Shaft junction sealed with four 0.9m square internal doors; small crack seals: "+count);
        }

        static void Panel(Transform parent,Material material,string name,bool xWall,float sign,
            float lateral,float y,float width,float height)
        {
            Solid(parent,material,name,
                xWall?new Vector3(sign*1.65f,y,lateral):new Vector3(lateral,y,sign*1.65f),
                xWall?new Vector3(.3f,height,width):new Vector3(width,height,.3f));
        }

        static void Solid(Transform parent,Material material,string name,Vector3 center,Vector3 size)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;
            g.transform.SetParent(parent,false);g.transform.position=center;g.transform.localScale=size;g.isStatic=true;
            var renderer=g.GetComponent<Renderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.TwoSided;renderer.receiveShadows=true;
        }

        static IEnumerable<Ring> FindPortals(MeshFilter filter, bool smallOnly = false)
        {
            var vertices = new List<Vector3>();
            var lookup = new Dictionary<Vector3Int,int>();
            var source = filter.sharedMesh;
            var ids = source.vertices.Select(v => {
                var p = filter.transform.TransformPoint(v);
                var key = new Vector3Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.y*10000),Mathf.RoundToInt(p.z*10000));
                if (!lookup.TryGetValue(key, out int id)) { id=vertices.Count; lookup.Add(key,id); vertices.Add(p); }
                return id;
            }).ToArray();
            var unique = new HashSet<string>();
            var edges = new Dictionary<(int,int),int>();
            var triangles = source.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var t = new[]{ids[triangles[i]],ids[triangles[i+1]],ids[triangles[i+2]]};
                var sorted=t.OrderBy(n=>n).ToArray();
                if(sorted[0]==sorted[1] || sorted[1]==sorted[2] || !unique.Add(string.Join(",",sorted))) continue;
                for(int k=0;k<3;k++) {int a=t[k],b=t[(k+1)%3];var edge=(Math.Min(a,b),Math.Max(a,b));edges.TryGetValue(edge,out int count);edges[edge]=count+1;}
            }
            var boundary=new HashSet<(int,int)>(edges.Where(e=>e.Value==1).Select(e=>e.Key));
            while(boundary.Count>0)
            {
                var e=boundary.First();boundary.Remove(e);
                var chain=new List<int>{e.Item1,e.Item2};
                while(chain[chain.Count-1]!=chain[0])
                {
                    int end=chain[chain.Count-1];
                    var next=boundary.Where(x=>x.Item1==end||x.Item2==end).ToArray();
                    if(next.Length==0)break;
                    var n=next[0];boundary.Remove(n);chain.Add(n.Item1==end?n.Item2:n.Item1);
                }
                if(chain[chain.Count-1]!=chain[0])continue;
                chain.RemoveAt(chain.Count-1);
                var points=chain.Select(i=>vertices[i]).ToArray();
                var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
                var size=bounds.size;
                // Imported passage rims are planar and >=1m in both cross-section axes.
                // The non-planar shaft cut and triangulation T-junctions are deliberately excluded.
                int axis=size.x<.001f?0:size.y<.001f?1:size.z<.001f?2:-1;
                if (smallOnly)
                {
                    if (Mathf.Max(size.x, size.y, size.z) >= 1.5f) continue;
                    axis = size.x < size.y ? (size.x < size.z ? 0 : 2) : (size.y < size.z ? 1 : 2);
                }
                else if(axis<0 || size[(axis+1)%3]<1 || size[(axis+2)%3]<1)continue;
                yield return new Ring{name=filter.name,points=points,center=points.Aggregate(Vector3.zero,(a,b)=>a+b)/points.Length,axis=axis};
            }
        }

        static Mesh Cap(Ring ring)
        {
            int n=ring.points.Length;var vertices=new List<Vector3>();var indices=new List<int>();
            Vector3 normal=Vector3.zero;normal[ring.axis]=.15f;
            // Slight overlap hides the rim and gives the closure 0.30m physical thickness.
            foreach(float sign in new[]{-1f,1f})
                foreach(var p in ring.points)vertices.Add(ring.center+(p-ring.center)*1.03f+normal*sign);
            vertices.Add(ring.center-normal);vertices.Add(ring.center+normal);
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;
                indices.AddRange(new[]{2*n,i,j,2*n+1,n+j,n+i,i,n+i,n+j,i,n+j,j});
            }
            return BuildMesh(vertices,indices);
        }

        static Mesh Stitch(Ring a,Ring b)
        {
            int n=a.points.Length,bestShift=0,bestSign=1;float best=float.PositiveInfinity;
            foreach(int sign in new[]{-1,1})for(int shift=0;shift<n;shift++)
            {
                float cost=0;for(int i=0;i<n;i++)cost+=(a.points[i]-b.points[(shift+sign*i+n)%n]).sqrMagnitude;
                if(cost<best){best=cost;bestShift=shift;bestSign=sign;}
            }
            var vertices=a.points.Concat(Enumerable.Range(0,n).Select(i=>b.points[(bestShift+bestSign*i+n)%n])).ToList();
            var triangles=new List<int>();
            for(int i=0;i<n;i++){int j=(i+1)%n;triangles.AddRange(new[]{i,j,n+j,i,n+j,n+i});}
            return BuildMesh(vertices,triangles);
        }

        static Mesh BuildMesh(List<Vector3> vertices,List<int> triangles)
        {
            // Separate reverse faces preserve usable normals for lighting from either side.
            int count=vertices.Count;vertices.AddRange(vertices.ToArray());
            triangles.AddRange(triangles.ToArray().Reverse().Select(i=>i+count));
            var mesh=new Mesh{name="KarstSeal"};mesh.SetVertices(vertices);
            mesh.SetUVs(0,vertices.Select(v=>new Vector2(v.x+v.z,v.y)).ToList());
            mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        static void Make(Transform root,Material material,string name,Mesh mesh)
        {
            string path="Assets/CAD/Karst/"+name+".asset";
            if(AssetDatabase.LoadAssetAtPath<Mesh>(path))throw new InvalidOperationException("Asset exists: "+path);
            AssetDatabase.CreateAsset(mesh,path);
            var g=new GameObject(name);g.transform.SetParent(root,false);g.isStatic=true;
            g.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.TwoSided;renderer.receiveShadows=true;
            g.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
    }
}
