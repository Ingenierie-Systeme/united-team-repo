using System;
using UnityEngine;

namespace IngeSys.Robot
{
    /// <summary>CAD volume and hardware ledger. One Rigidbody; no motion integration here.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(RobotFishParameters))]
    public sealed class RobotMassDistribution : MonoBehaviour
    {
        [Serializable] public sealed class Part
        {
            public string name, basis;
            public float massKg, lowKg, highKg, volumeM3;
            public float[] center, unitInertia;
            public bool sliding;
        }
        [Serializable] public sealed class Ledger { public Part[] parts; }
        public TextAsset cadLedger;
        [Tooltip("PROVISIONAL: existing V1 movable payload, not the HS-5496 servo mass.")]
        [Min(0)] public float movablePayloadKg=.5f;
        [Tooltip("Explicit hypothetical trim ballast preserving the previous neutral demo. Not measured hardware.")]
        public bool includeReferenceTrim=true;
        [Min(0)] public float referenceTrimKg;
        public Vector3 referenceTrimCenter;
        public float estimatedDryMassKg, appliedMassKg;
        public Vector3 appliedCenter, principalInertia;
        Ledger ledger;
        TextAsset loaded;
        readonly double[,] inertia=new double[3,3], axes=new double[3,3];

        public bool Apply(float slidingPosition)
        {
            if(!enabled||!cadLedger)return false;
            if(loaded!=cadLedger){ledger=JsonUtility.FromJson<Ledger>(cadLedger.text);loaded=cadLedger;}
            if(ledger?.parts==null)return false;
            float total=0;Vector3 moment=Vector3.zero;
            foreach(var p in ledger.parts)
            {
                float m=p.sliding?movablePayloadKg:Mathf.Max(0,p.massKg);
                Vector3 c=Center(p,slidingPosition);total+=m;moment+=m*c;
            }
            estimatedDryMassKg=total;
            float trim=includeReferenceTrim?Mathf.Max(0,referenceTrimKg):0;
            total+=trim;moment+=trim*referenceTrimCenter;
            if(total<=0)return false;
            appliedMassKg=total;appliedCenter=moment/total;
            Array.Clear(inertia,0,9);
            foreach(var p in ledger.parts)Add(p.sliding?movablePayloadKg:Mathf.Max(0,p.massKg),Center(p,slidingPosition),p.unitInertia);
            Add(trim,referenceTrimCenter,null);
            Diagonalize();
            var body=GetComponent<Rigidbody>();
            body.mass=total;body.centerOfMass=appliedCenter;
            body.inertiaTensor=principalInertia;
            body.inertiaTensorRotation=Quaternion.LookRotation(
                new Vector3((float)axes[0,2],(float)axes[1,2],(float)axes[2,2]),
                new Vector3((float)axes[0,1],(float)axes[1,1],(float)axes[2,1]));
            var parameters=GetComponent<RobotFishParameters>();
            parameters.totalMassKg=total;parameters.slidingMassKg=movablePayloadKg;
            return true;
        }
        static Vector3 Center(Part p,float s)=>new Vector3(p.center[0],p.center[1],p.center[2]+(p.sliding?s:0));
        void Add(float mass,Vector3 c,float[] unit)
        {
            Vector3 d=c-appliedCenter;
            for(int i=0;i<3;i++)for(int j=0;j<3;j++)
                inertia[i,j]+=mass*((unit!=null?unit[3*i+j]:0)+(i==j?d.sqrMagnitude:0)-d[i]*d[j]);
        }
        void Diagonalize()
        {
            Array.Clear(axes,0,9);for(int i=0;i<3;i++)axes[i,i]=1;
            for(int n=0;n<24;n++)
            {
                int p=0,q=1;
                if(Math.Abs(inertia[0,2])>Math.Abs(inertia[p,q])){p=0;q=2;}
                if(Math.Abs(inertia[1,2])>Math.Abs(inertia[p,q])){p=1;q=2;}
                if(Math.Abs(inertia[p,q])<1e-12)break;
                double theta=.5*Math.Atan2(2*inertia[p,q],inertia[q,q]-inertia[p,p]);
                double c=Math.Cos(theta),s=Math.Sin(theta);
                double pp=inertia[p,p],qq=inertia[q,q],pq=inertia[p,q];
                for(int k=0;k<3;k++)if(k!=p&&k!=q)
                {double a=inertia[k,p],b=inertia[k,q];inertia[k,p]=inertia[p,k]=c*a-s*b;inertia[k,q]=inertia[q,k]=s*a+c*b;}
                inertia[p,p]=c*c*pp-2*s*c*pq+s*s*qq;
                inertia[q,q]=s*s*pp+2*s*c*pq+c*c*qq;inertia[p,q]=inertia[q,p]=0;
                for(int k=0;k<3;k++){double a=axes[k,p],b=axes[k,q];axes[k,p]=c*a-s*b;axes[k,q]=s*a+c*b;}
            }
            principalInertia=new Vector3((float)Math.Max(1e-6,inertia[0,0]),(float)Math.Max(1e-6,inertia[1,1]),(float)Math.Max(1e-6,inertia[2,2]));
        }
    }
}
