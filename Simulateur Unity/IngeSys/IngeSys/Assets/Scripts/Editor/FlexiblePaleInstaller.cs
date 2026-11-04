using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IngeSys.Robot;

namespace IngeSys.EditorTools
{
    /// <summary>Creates derived clipped meshes, preserving the imported CAD and its importer settings.</summary>
    public static class FlexiblePaleInstaller
    {
        struct Vertex
        {
            public Vector3 p,n;public Vector2 uv;
            public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)};
        }
        [MenuItem("IngeSys/Build three-section flexible pale")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Build outside Play mode");
            var p=Object.FindFirstObjectByType<RobotFishParameters>();
            var hydro=p.GetComponent<FinHydrodynamics>();var pivot=hydro.cadPivot;
            var blade=pivot.Find("Pale_STL");var source=blade.GetComponentInChildren<MeshFilter>(true);
            var importer=(ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source.sharedMesh));
            bool readable=importer.isReadable;
            try
            {
                if(!readable){importer.isReadable=true;importer.SaveAndReimport();}
                source=blade.GetComponentInChildren<MeshFilter>(true);var mesh=source.sharedMesh;
                Vector3[] positions=mesh.vertices,normals=mesh.normals;Vector2[] uv=mesh.uv;int[] triangles=mesh.triangles;
                var toPivot=pivot.worldToLocalMatrix*source.transform.localToWorldMatrix;
                var normalMatrix=toPivot.inverse.transpose;
                var vertices=new Vertex[positions.Length];
                for(int i=0;i<vertices.Length;i++)vertices[i]=new Vertex{p=toPivot.MultiplyPoint3x4(positions[i]),n=normalMatrix.MultiplyVector(normals[i]).normalized,uv=uv.Length==positions.Length?uv[i]:Vector2.zero};
                var root=pivot.Find("FlexiblePale_3Sections");
                if(!root){var go=new GameObject("FlexiblePale_3Sections");Undo.RegisterCreatedObjectUndo(go,"Build flexible pale");root=go.transform;root.SetParent(pivot,false);}
                float l=hydro.chordM/3;
                var j1=Child(root,"ElasticJoint1",new Vector3(0,0,-hydro.leadingDistanceM-l));
                var j2=Child(j1,"ElasticJoint2",new Vector3(0,0,-l));
                if(!AssetDatabase.IsValidFolder("Assets/CAD/Robot/Derived/FlexiblePale"))AssetDatabase.CreateFolder("Assets/CAD/Robot/Derived","FlexiblePale");
                for(int section=0;section<3;section++)
                {
                    Vector3 origin=section==0?Vector3.zero:new Vector3(0,0,-hydro.leadingDistanceM-section*l);
                    var vp=new List<Vector3>();var vn=new List<Vector3>();var vu=new List<Vector2>();var indices=new List<int>();
                    for(int t=0;t<triangles.Length;t+=3)
                    {
                        var poly=new List<Vertex>{vertices[triangles[t]],vertices[triangles[t+1]],vertices[triangles[t+2]]};
                        poly=Clip(poly,hydro.leadingDistanceM+section*l,true);
                        poly=Clip(poly,hydro.leadingDistanceM+(section+1)*l,false);
                        for(int k=1;k<poly.Count-1;k++)foreach(var v in new[]{poly[0],poly[k],poly[k+1]})
                        {indices.Add(vp.Count);vp.Add(v.p-origin);vn.Add(v.n);vu.Add(v.uv);}
                    }
                    var derived=new Mesh{name="Pale_Section"+(section+1)};derived.SetVertices(vp);derived.SetNormals(vn);derived.SetUVs(0,vu);derived.SetTriangles(indices,0);derived.RecalculateBounds();
                    string path="Assets/CAD/Robot/Derived/FlexiblePale/Section"+(section+1)+".asset";
                    var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(saved){EditorUtility.CopySerialized(derived,saved);Object.DestroyImmediate(derived);EditorUtility.SetDirty(saved);}else{AssetDatabase.CreateAsset(derived,path);saved=derived;}
                    var parent=section==0?root:section==1?j1:j2;
                    var visual=Child(parent,"PVC_Section"+(section+1),Vector3.zero);
                    var mf=visual.GetComponent<MeshFilter>();if(!mf)mf=Undo.AddComponent<MeshFilter>(visual.gameObject);mf.sharedMesh=saved;
                    var mr=visual.GetComponent<MeshRenderer>();if(!mr)mr=Undo.AddComponent<MeshRenderer>(visual.gameObject);mr.sharedMaterials=source.GetComponent<Renderer>().sharedMaterials;
                }
                var flex=p.GetComponent<FlexiblePale>();if(!flex)flex=Undo.AddComponent<FlexiblePale>(p.gameObject);
                flex.hinge1Visual=j1;flex.hinge2Visual=j2;flex.flexibleVisualRoot=root;flex.originalBlade=source.GetComponent<Renderer>();
                flex.pivotLocal=p.transform.InverseTransformPoint(pivot.position);flex.enabled=true;flex.lockJoints=false;
                root.gameObject.SetActive(false);flex.originalBlade.enabled=true;
                EditorUtility.SetDirty(flex);EditorSceneManager.MarkSceneDirty(p.gameObject.scene);
                AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(p.gameObject.scene);
                Debug.Log("Three derived pale sections built. CAD source preserved; first strip fixed to servo.");
            }
            finally{if(importer.isReadable!=readable){importer.isReadable=readable;importer.SaveAndReimport();}}
        }
        static Transform Child(Transform parent,string name,Vector3 position)
        {
            var child=parent.Find(name);
            if(!child){var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Build pale section");child=go.transform;child.SetParent(parent,false);}
            child.localPosition=position;child.localRotation=Quaternion.identity;child.localScale=Vector3.one;return child;
        }
        static List<Vertex> Clip(List<Vertex> input,float distance,bool greater)
        {
            var output=new List<Vertex>();if(input.Count==0)return output;
            Vertex previous=input[input.Count-1];float pd=-previous.p.z-distance;bool pin=greater?pd>=-1e-7f:pd<=1e-7f;
            foreach(var current in input)
            {
                float cd=-current.p.z-distance;bool cin=greater?cd>=-1e-7f:cd<=1e-7f;
                if(cin!=pin)output.Add(Vertex.Lerp(previous,current,pd/(pd-cd)));
                if(cin)output.Add(current);
                previous=current;pd=cd;pin=cin;
            }
            return output;
        }
    }
}
