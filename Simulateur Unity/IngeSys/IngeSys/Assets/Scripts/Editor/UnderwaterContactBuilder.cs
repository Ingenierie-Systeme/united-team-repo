using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IngeSys.Robot;
using IngeSys.Diagnostics;

namespace IngeSys.EditorTools
{
    public static class UnderwaterContactBuilder
    {
        const string Folder = "Assets/Physics";
        [MenuItem("IngeSys/Build CAD contact envelope (once)")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/RobotFishKatarstique.unity")
                throw new InvalidOperationException("Open the karst scene outside Play mode.");
            var robot = GameObject.Find("RobotFish");
            if (robot.transform.Find("CAD_CollisionEnvelope_NeutralV1"))
                throw new InvalidOperationException("Contact envelope already exists; refusing to overwrite.");
            var bodyMat = Material("Robot_Wet_PROVISIONAL", .25f, .15f, .02f);
            var poolMat = Material("Pool_Wet_PROVISIONAL", .35f, .25f, 0);
            var rockMat = Material("Rock_Wet_PROVISIONAL", .55f, .4f, 0);
            foreach (var c in robot.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            var root = new GameObject("CAD_CollisionEnvelope_NeutralV1");root.transform.SetParent(robot.transform,false);
            var cad = robot.transform.Find("Body_CAD_Assembly");
            foreach (var name in new[]{"tube","cap_front","cap_rear","clamp","dome_cercle","dome"})
            {
                var filter = cad.GetComponentsInChildren<MeshFilter>().First(f=>f.name==name);
                var b = LocalBounds(filter, robot.transform);
                var mesh = name == "dome" ? Ellipsoid(b) : Cylinder(b);
                mesh.name = "Contact_"+name;
                AssetDatabase.CreateAsset(mesh,Folder+"/"+mesh.name+".asset");
                var g = Child(root.transform,mesh.name,b.center);
                var collider=g.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;
            }
            foreach (var name in new[]{"servo_mount","underwater_servo"})
                Box(root.transform,"Contact_"+name,LocalBounds(cad.GetComponentsInChildren<MeshFilter>().First(f=>f.name==name),robot.transform));
            var hub = cad.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="underwater_servo_hub");
            Box(root.transform,"Contact_servo_hub",LocalBounds(hub,robot.transform));
            var pale = robot.transform.Find("Pale_CAD_Pivot");
            Quaternion previous = pale.localRotation;pale.localRotation=Quaternion.identity;
            foreach(Transform piece in pale)
            {
                var filter = piece.GetComponentInChildren<MeshFilter>();
                if(filter) Box(root.transform,piece.name=="Pale_STL"?"Contact_Blade_NeutralV1":"Contact_"+piece.name,LocalBounds(filter,robot.transform));
            }
            pale.localRotation=previous;
            var settings=robot.AddComponent<UnderwaterContactSettings>();
            settings.collisionEnvelope=root.transform;settings.robotMaterial=bodyMat;settings.Apply();
            robot.AddComponent<RobotContactDiagnostics>();
            foreach(var c in GameObject.Find("BestPiscineRoot").GetComponentsInChildren<Collider>())
            {c.sharedMaterial=poolMat;c.contactOffset=.0005f;}
            foreach(var c in GameObject.Find("KarstEnvironment_Flooded_NoLamp").GetComponentsInChildren<Collider>())
            {c.sharedMaterial=rockMat;c.contactOffset=.0005f;}
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        }

        static PhysicsMaterial Material(string name,float sf,float df,float restitution)
        {
            var m = new PhysicsMaterial(name){staticFriction=sf,dynamicFriction=df,bounciness=restitution,
                frictionCombine=PhysicsMaterialCombine.Average,bounceCombine=PhysicsMaterialCombine.Maximum};
            AssetDatabase.CreateAsset(m,Folder+"/"+name+".physicMaterial");return m;
        }
        static GameObject Child(Transform root,string name,Vector3 center)
        {var g=new GameObject(name);g.transform.SetParent(root,false);g.transform.localPosition=center;return g;}
        static void Box(Transform root,string name,Bounds bounds)
        {var c=Child(root,name,bounds.center).AddComponent<BoxCollider>();c.size=bounds.size;}
        static Bounds LocalBounds(MeshFilter filter,Transform robot)
        {
            var b=filter.sharedMesh.bounds;var corners=new List<Vector3>();
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
                corners.Add(robot.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)))));
            var result=new Bounds(corners[0],Vector3.zero);foreach(var p in corners)result.Encapsulate(p);return result;
        }
        static Mesh Cylinder(Bounds bounds)
        {
            const int n=24;var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int end=0;end<2;end++)for(int i=0;i<n;i++)
            {float a=2*Mathf.PI*i/n;vertices.Add(new Vector3(Mathf.Cos(a)*bounds.extents.x,Mathf.Sin(a)*bounds.extents.y,(end==0?-1:1)*bounds.extents.z));}
            for(int i=0;i<n;i++) {int j=(i+1)%n;triangles.AddRange(new[]{i,j,n+j,i,n+j,n+i});}
            for(int i=1;i<n-1;i++)triangles.AddRange(new[]{0,i+1,i,n,n+i,n+i+1});
            return Mesh(vertices,triangles);
        }
        static Mesh Ellipsoid(Bounds bounds)
        {
            const int n=16,rings=5;var v=new List<Vector3>{new Vector3(0,0,-bounds.extents.z)};
            for(int j=1;j<=rings;j++)for(int i=0;i<n;i++)
            {float lat=Mathf.PI*j/(rings+1),a=2*Mathf.PI*i/n;v.Add(Vector3.Scale(bounds.extents,new Vector3(Mathf.Sin(lat)*Mathf.Cos(a),Mathf.Sin(lat)*Mathf.Sin(a),-Mathf.Cos(lat))));}
            v.Add(new Vector3(0,0,bounds.extents.z));var t=new List<int>();
            for(int i=0;i<n;i++){int j=(i+1)%n;t.AddRange(new[]{0,1+j,1+i});t.AddRange(new[]{v.Count-1,1+(rings-1)*n+i,1+(rings-1)*n+j});}
            for(int r=0;r<rings-1;r++)for(int i=0;i<n;i++){int a=1+r*n+i,b=1+r*n+(i+1)%n;t.AddRange(new[]{a,b,b+n,a,b+n,a+n});}
            return Mesh(v,t);
        }
        static Mesh Mesh(List<Vector3> v,List<int> t)
        {var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;}
    }
}
