using System;
using UnityEngine;

namespace IngeSys.Robot
{
    /// <summary>
    /// Linearized backward-Euler fluid force closure. Solves a force response only;
    /// PhysX remains the sole integrator of the body's position and velocity.
    /// </summary>
    internal sealed class FinFluidCoupling
    {
        readonly double[,] response=new double[6,6], inverseMass=new double[6,6], system=new double[6,7];
        readonly double[] baseline=new double[6], externalAcceleration=new double[6], solution=new double[6];
        readonly double[,] b=new double[3,6], j=new double[3,3];
        public void Reset(Rigidbody body)
        {
            Array.Clear(response,0,36);Array.Clear(inverseMass,0,36);Array.Clear(baseline,0,6);
            for(int i=0;i<3;i++)inverseMass[i,i]=1/body.mass;
            Quaternion q=body.rotation*body.inertiaTensorRotation;
            Vector3[] axes={q*Vector3.right,q*Vector3.up,q*Vector3.forward};
            for(int i=0;i<3;i++)for(int k=0;k<3;k++)for(int a=0;a<3;a++)
                inverseMass[i+3,k+3]+=axes[a][i]*axes[a][k]/body.inertiaTensor[a];
        }
        public void Add(Vector3 r,Vector3 n,Vector3 tangent,float normalSlope,float tangentCoefficient,float addedMass,float dt,Vector3 force)
        {
            Vector3 torque=Vector3.Cross(r,force);
            for(int i=0;i<3;i++){baseline[i]+=force[i];baseline[i+3]+=torque[i];}
            Array.Clear(b,0,18);
            for(int i=0;i<3;i++)b[i,i]=1;
            b[0,4]=r.z;b[0,5]=-r.y;b[1,3]=-r.z;b[1,5]=r.x;b[2,3]=r.y;b[2,4]=-r.x;
            float speed=tangent.magnitude;
            for(int i=0;i<3;i++)for(int k=0;k<3;k++)
                j[i,k]=(addedMass+dt*normalSlope)*n[i]*n[k]
                    +dt*tangentCoefficient*(speed*((i==k?1:0)-n[i]*n[k])+(speed>1e-8f?tangent[i]*tangent[k]/speed:0));
            for(int a=0;a<6;a++)for(int c=0;c<6;c++)for(int i=0;i<3;i++)for(int k=0;k<3;k++)
                response[a,c]+=b[i,a]*j[i,k]*b[k,c];
        }
        public bool Solve(Rigidbody body,out Vector3 linearAcceleration,out Vector3 angularAcceleration)
        {
            float dt=Time.fixedDeltaTime;
            Vector3 f=body.GetAccumulatedForce(dt)+(body.useGravity?body.mass*Physics.gravity:Vector3.zero);
            Vector3 t=body.GetAccumulatedTorque(dt);
            for(int i=0;i<6;i++)
            {
                externalAcceleration[i]=0;
                for(int k=0;k<6;k++)externalAcceleration[i]+=inverseMass[i,k]*(k<3?f[k]:t[k-3]);
            }
            for(int i=0;i<6;i++)
            {
                system[i,6]=baseline[i];
                for(int k=0;k<6;k++)system[i,6]-=response[i,k]*externalAcceleration[k];
                for(int k=0;k<6;k++)
                {system[i,k]=i==k?1:0;for(int a=0;a<6;a++)system[i,k]+=response[i,a]*inverseMass[a,k];}
            }
            for(int k=0;k<6;k++)
            {
                int p=k;for(int i=k+1;i<6;i++)if(Math.Abs(system[i,k])>Math.Abs(system[p,k]))p=i;
                if(Math.Abs(system[p,k])<1e-12){linearAcceleration=angularAcceleration=Vector3.zero;return false;}
                for(int a=k;a<7;a++){double tmp=system[k,a];system[k,a]=system[p,a];system[p,a]=tmp;}
                double divisor=system[k,k];for(int a=k;a<7;a++)system[k,a]/=divisor;
                for(int i=0;i<6;i++)if(i!=k)
                {double factor=system[i,k];for(int a=k;a<7;a++)system[i,a]-=factor*system[k,a];}
            }
            for(int i=0;i<6;i++)
            {solution[i]=externalAcceleration[i];for(int k=0;k<6;k++)solution[i]+=inverseMass[i,k]*system[k,6];}
            linearAcceleration=new Vector3((float)solution[0],(float)solution[1],(float)solution[2]);
            angularAcceleration=new Vector3((float)solution[3],(float)solution[4],(float)solution[5]);
            return float.IsFinite(linearAcceleration.sqrMagnitude)&&float.IsFinite(angularAcceleration.sqrMagnitude);
        }
    }
}
