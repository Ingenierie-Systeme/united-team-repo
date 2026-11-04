using System;
using UnityEngine;

namespace IngeSys.Robot
{
    /// <summary>
    /// Three rigid strips, two passive elastic coordinates, one PhysX body. The eight-variable
    /// solve evaluates forces; only the two internal hinge coordinates are integrated here.
    /// Body mass/inertia retain the existing neutral-pose approximation (no second Rigidbody).
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(FinHydrodynamics))]
    public sealed class FlexiblePale : MonoBehaviour
    {
        [Tooltip("Rigid reference using the same pressure and dry-inertia model.")]
        public bool lockJoints;
        [Tooltip("Diagnostic tip force along robot +X, N. Must remain zero for normal use.")]
        public float diagnosticTipForceN;
        [Range(0,1), Tooltip("Provisional structural damping ratio; water drag is calculated separately.")]
        public float structuralDampingRatio=.05f;
        [Min(.001f), Tooltip("1 uses the reference PVC modulus. Calibration factor, not a measured material value.")]
        public float stiffnessMultiplier=1;
        [Tooltip("Match the small-deflection tip compliance of a cantilever with the same PVC and effective clamp.")]
        public bool matchCantileverCompliance=true;
        [Min(0), Tooltip("CAD support overlap estimate, m. Effective clamp, not experimentally measured.")]
        public float effectiveClampedLengthM=.04f;
        public float discretizationStiffnessScale=1;
        public float referenceTipComplianceMPerN;
        [Range(10,85), Tooltip("Model validity guard, not a material travel limit. Results hitting it are invalid.")]
        public float validityAngleDegrees=60;
        public Transform hinge1Visual,hinge2Visual,flexibleVisualRoot;
        public Renderer originalBlade;
        public Vector2 anglesRad,velocitiesRadS;
        public Vector2 stiffnessNmPerRad,dampingNmSPerRad;
        public Vector3 appliedBodyForceN,appliedBodyTorqueNm;
        public float elasticEnergyJ,structuralLossW,maximumResidual,tipDeflectionM;
        public bool validityLimitReached,solveFailed;
        public Vector3 pivotLocal;
        FinHydrodynamics hydro;
        Rigidbody body;
        FinServo servo;
        CalmWaterBuoyancy buoyancy;
        const int N=8;
        readonly double[,] dry=new double[N,N],matrix=new double[N,N],system=new double[N,N+1];
        readonly double[,] jacobian=new double[3,N],fluidJacobian=new double[3,3];
        readonly double[] rhs=new double[N],acceleration=new double[N];
        readonly Vector3[] pointBasis=new Vector3[N];
        struct Cell {public Vector3 p,n,t,vAct,force,j1,j2;public float area,mass,slope,ct,pressure;}
        Cell[] cells;
        Vector3 root,joint1,joint2,axis;
        Quaternion r0,r1,r2;
        Vector3 joint1Velocity,joint2Velocity,joint1Acceleration,joint2Acceleration;
        float l0,l1,l2,w0,w1,w2,alpha0;

        void Awake(){Cache();}
        void Cache(){hydro=GetComponent<FinHydrodynamics>();body=GetComponent<Rigidbody>();servo=GetComponent<FinServo>();buoyancy=GetComponent<CalmWaterBuoyancy>();}
        void Start(){if(hydro.cadPivot)pivotLocal=transform.InverseTransformPoint(hydro.cadPivot.position);}
        public void ResetDeformation(){anglesRad=velocitiesRadS=Vector2.zero;validityLimitReached=solveFailed=false;UpdateVisuals();}
        void LateUpdate(){UpdateVisuals();}
        void UpdateVisuals()
        {
            bool active=Application.isPlaying&&enabled&&hydro&&hydro.enabled&&hydro.model==FinHydrodynamics.Model.InstantaneousPressureV2;
            if(flexibleVisualRoot&&flexibleVisualRoot.gameObject.activeSelf!=active)flexibleVisualRoot.gameObject.SetActive(active);
            if(originalBlade)originalBlade.enabled=!active;
            if(hinge1Visual)hinge1Visual.localRotation=Quaternion.AngleAxis(anglesRad.x*Mathf.Rad2Deg,Vector3.up);
            if(hinge2Visual)hinge2Visual.localRotation=Quaternion.AngleAxis(anglesRad.y*Mathf.Rad2Deg,Vector3.up);
        }
        void OnDisable(){if(originalBlade)originalBlade.enabled=true;if(flexibleVisualRoot)flexibleVisualRoot.gameObject.SetActive(false);}
        public void PrepareGeometry()
        {
            if(!hydro)Cache();
            l0=l1=l2=hydro.chordM/3f;
            float ei=Mathf.Max(1,hydro.referenceYoungModulusPa)*Mathf.Max(.001f,stiffnessMultiplier)*hydro.spanM*Mathf.Pow(hydro.thicknessM,3)/12;
            stiffnessNmPerRad=new Vector2(ei/(.5f*(l0+l1)),ei/(.5f*(l1+l2)));
            float freeLength=hydro.chordM-Mathf.Clamp(effectiveClampedLengthM,0,hydro.chordM*.9f);
            referenceTipComplianceMPerN=Mathf.Pow(freeLength,3)/(3*ei);
            float coarseCompliance=Mathf.Pow(l1+l2,2)/stiffnessNmPerRad.x+l2*l2/stiffnessNmPerRad.y;
            discretizationStiffnessScale=matchCantileverCompliance?coarseCompliance/referenceTipComplianceMPerN:1;
            stiffnessNmPerRad*=discretizationStiffnessScale;
            float linearMass=hydro.pvcDensityKgM3*hydro.spanM*hydro.thicknessM;
            float i1=linearMass*Mathf.Pow(l1+l2,3)/3,i2=linearMass*Mathf.Pow(l2,3)/3;
            dampingNmSPerRad=new Vector2(2*structuralDampingRatio*Mathf.Sqrt(stiffnessNmPerRad.x*i1),2*structuralDampingRatio*Mathf.Sqrt(stiffnessNmPerRad.y*i2));
            root=transform.TransformPoint(pivotLocal);axis=transform.up;
            r0=transform.rotation*Quaternion.AngleAxis(servo.actualAngleRad*Mathf.Rad2Deg,Vector3.up);
            r1=r0*Quaternion.AngleAxis(anglesRad.x*Mathf.Rad2Deg,Vector3.up);
            r2=r1*Quaternion.AngleAxis(anglesRad.y*Mathf.Rad2Deg,Vector3.up);
            w0=servo.actualAngularVelocityRadS;w1=w0+velocitiesRadS.x;w2=w1+velocitiesRadS.y;alpha0=servo.actualAngularAccelerationRadS2;
            Vector3 a=r0*new Vector3(0,0,-hydro.leadingDistanceM-l0);
            joint1=root+a;joint1Velocity=Vector3.Cross(axis*w0,a);
            joint1Acceleration=Vector3.Cross(axis*alpha0,a)+Vector3.Cross(axis*w0,joint1Velocity);
            a=r1*new Vector3(0,0,-l1);joint2=joint1+a;
            joint2Velocity=joint1Velocity+Vector3.Cross(axis*w1,a);
            joint2Acceleration=joint1Acceleration+Vector3.Cross(axis*alpha0,a)+Vector3.Cross(axis*w1,Vector3.Cross(axis*w1,a));
        }
        public void Sample(float s,float y,out Vector3 p,out Vector3 normal,out Vector3 velocity,out Vector3 knownAcceleration,out Vector3 j1,out Vector3 j2)
        {
            if(s<l0)
            {
                Vector3 a=r0*new Vector3(0,y,-hydro.leadingDistanceM-s);p=root+a;normal=r0*Vector3.right;
                velocity=Vector3.Cross(axis*w0,a);knownAcceleration=Vector3.Cross(axis*alpha0,a)+Vector3.Cross(axis*w0,velocity);
                j1=j2=Vector3.zero;
            }
            else if(s<l0+l1)
            {
                Vector3 a=r1*new Vector3(0,y,-s+l0);p=joint1+a;normal=r1*Vector3.right;
                velocity=joint1Velocity+Vector3.Cross(axis*w1,a);
                knownAcceleration=joint1Acceleration+Vector3.Cross(axis*alpha0,a)+Vector3.Cross(axis*w1,Vector3.Cross(axis*w1,a));
                j1=Vector3.Cross(axis,p-joint1);j2=Vector3.zero;
            }
            else
            {
                Vector3 a=r2*new Vector3(0,y,-s+l0+l1);p=joint2+a;normal=r2*Vector3.right;
                velocity=joint2Velocity+Vector3.Cross(axis*w2,a);
                knownAcceleration=joint2Acceleration+Vector3.Cross(axis*alpha0,a)+Vector3.Cross(axis*w2,Vector3.Cross(axis*w2,a));
                j1=Vector3.Cross(axis,p-joint1);j2=Vector3.Cross(axis,p-joint2);
            }
            if(lockJoints)j1=j2=Vector3.zero;
        }
        void Basis(Vector3 p,Vector3 j1,Vector3 j2)
        {
            Vector3 r=p-body.worldCenterOfMass;
            pointBasis[0]=Vector3.right;pointBasis[1]=Vector3.up;pointBasis[2]=Vector3.forward;
            pointBasis[3]=Vector3.Cross(Vector3.right,r);pointBasis[4]=Vector3.Cross(Vector3.up,r);pointBasis[5]=Vector3.Cross(Vector3.forward,r);
            pointBasis[6]=j1;pointBasis[7]=j2;
            for(int i=0;i<3;i++)for(int k=0;k<N;k++)jacobian[i,k]=pointBasis[k][i];
        }
        public void Step()
        {
            if(!hydro)Cache();
            if(!hydro.water||!hydro.cadPivot)return;
            float dt=Time.fixedDeltaTime;
            if(lockJoints)anglesRad=velocitiesRadS=Vector2.zero;
            PrepareGeometry();
            int nx=Mathf.Max(6,hydro.chordSamples),ny=Mathf.Max(2,hydro.spanSamples);
            // Equal panel counts per strip ensure no panel straddles a mechanical joint.
            int perStrip=Mathf.Max(2,Mathf.CeilToInt(nx/3f)),count=3*perStrip*ny;
            if(cells==null||cells.Length!=count)cells=new Cell[count];
            Array.Clear(dry,0,N*N);Array.Clear(matrix,0,N*N);Array.Clear(rhs,0,N);
            for(int i=0;i<3;i++)dry[i,i]=body.mass;
            Quaternion iq=body.rotation*body.inertiaTensorRotation;
            for(int a=0;a<3;a++)
            {
                Vector3 v=iq*(a==0?Vector3.right:a==1?Vector3.up:Vector3.forward);
                for(int i=0;i<3;i++)for(int j=0;j<3;j++)dry[i+3,j+3]+=body.inertiaTensor[a]*v[i]*v[j];
            }
            Vector3 external=body.GetAccumulatedForce(dt)+(body.useGravity?body.mass*Physics.gravity:Vector3.zero),externalTorque=body.GetAccumulatedTorque(dt);
            for(int i=0;i<3;i++){rhs[i]=external[i];rhs[i+3]=externalTorque[i];}
            float density=buoyancy?buoyancy.density:1000;
            Vector3 dryKnownForce=Vector3.zero,dryKnownTorque=Vector3.zero;
            int index=0;
            for(int strip=0;strip<3;strip++)for(int x=0;x<perStrip;x++)for(int y=0;y<ny;y++)
            {
                float length=strip==0?l0:strip==1?l1:l2,start=strip==0?0:strip==1?l0:l0+l1;
                float s=start+length*(x+.5f)/perStrip;
                Vector3 p,n,vRel,aRel,j1,j2;
                Sample(s,hydro.spanM*((y+.5f)/ny-.5f),out p,out n,out vRel,out aRel,out j1,out j2);
                Basis(p,j1,j2);
                float area=length*hydro.spanM/(perStrip*ny),solidMass=area*hydro.thicknessM*hydro.pvcDensityKgM3;
                // Aggregate Rigidbody already contains the pale at rest. Add only internal mass
                // blocks/cross terms, never a second contribution to the translational body mass.
                for(int a=0;a<N;a++)for(int b=0;b<N;b++)if(a>=6||b>=6)
                    dry[a,b]+=solidMass*Vector3.Dot(pointBasis[a],pointBasis[b]);
                Vector3 dryBias=aRel+2*Vector3.Cross(body.angularVelocity,vRel);
                Vector3 inertiaForce=-solidMass*dryBias;
                dryKnownForce+=inertiaForce;dryKnownTorque+=Vector3.Cross(p-body.worldCenterOfMass,inertiaForce);
                for(int a=0;a<N;a++)rhs[a]+=Vector3.Dot(pointBasis[a],inertiaForce);
                Vector3 velocity=body.GetPointVelocity(p)+vRel;
                float vn=Vector3.Dot(velocity,n);Vector3 tangent=Vector3.ProjectOnPlane(velocity,n);
                float wet=Mathf.Clamp01(.5f+(hydro.water.GetHeightAtWorldXZ(p.x,p.z)-p.y)/Mathf.Max(.0001f,hydro.interfaceSmoothingM));
                float wetArea=area*wet,added=hydro.reactiveCoefficient*density*Mathf.PI*hydro.spanM*.25f*wetArea;
                Vector3 known=dryBias+Vector3.Cross(body.angularVelocity,Vector3.Cross(body.angularVelocity,p-body.worldCenterOfMass));
                float pressure=-.5f*density*hydro.normalDragCoefficient*vn*Mathf.Abs(vn)
                    -hydro.reactiveCoefficient*density*Mathf.PI*hydro.spanM*.25f*Vector3.Dot(known,n);
                float slope=density*hydro.normalDragCoefficient*Mathf.Abs(vn)*wetArea,ct=.5f*density*hydro.tangentialDragCoefficient*wetArea;
                Vector3 force=n*pressure*wetArea-ct*tangent.magnitude*tangent;
                cells[index++]=new Cell{p=p,n=n,t=tangent,vAct=vRel,force=force,j1=j1,j2=j2,area=wetArea,mass=added,slope=slope,ct=ct,pressure=pressure};
                for(int a=0;a<N;a++)rhs[a]+=Vector3.Dot(pointBasis[a],force);
                float speed=tangent.magnitude;
                for(int i=0;i<3;i++)for(int j=0;j<3;j++)fluidJacobian[i,j]=(added+dt*slope)*n[i]*n[j]
                    +dt*ct*(speed*((i==j?1:0)-n[i]*n[j])+(speed>1e-8f?tangent[i]*tangent[j]/speed:0));
                for(int a=0;a<N;a++)for(int b=0;b<N;b++)for(int i=0;i<3;i++)for(int j=0;j<3;j++)
                    matrix[a,b]+=jacobian[i,a]*fluidJacobian[i,j]*jacobian[j,b];
                hydro.dissipatedPowerW+=(.5f*density*hydro.normalDragCoefficient*Mathf.Pow(Mathf.Abs(vn),3)
                    +.5f*density*hydro.tangentialDragCoefficient*Mathf.Pow(speed,3))*wetArea;
            }
            Vector3 probePoint,pn,pv,pa,pj1,pj2;
            Sample(hydro.chordM,0,out probePoint,out pn,out pv,out pa,out pj1,out pj2);
            Vector3 probeForce=transform.right*diagnosticTipForceN;
            Basis(probePoint,pj1,pj2);
            for(int a=0;a<N;a++)rhs[a]+=Vector3.Dot(pointBasis[a],probeForce);
            for(int a=0;a<N;a++)for(int b=0;b<N;b++)matrix[a,b]+=dry[a,b];
            for(int j=0;j<2;j++)
            {
                if(lockJoints){matrix[6+j,6+j]=1;rhs[6+j]=0;}
                else
                {
                    float k=stiffnessNmPerRad[j],c=dampingNmSPerRad[j];
                    matrix[6+j,6+j]+=dt*c+dt*dt*k;
                    rhs[6+j]-=k*anglesRad[j]+(c+dt*k)*velocitiesRadS[j];
                }
            }
            if(body.isKinematic)for(int a=0;a<6;a++)
            {for(int b=0;b<N;b++){matrix[a,b]=0;matrix[b,a]=0;}matrix[a,a]=1;rhs[a]=0;}
            solveFailed=!Solve();
            if(solveFailed){Debug.LogError("Flexible pale solve failed",this);return;}
            Vector3 aBody=new Vector3((float)acceleration[0],(float)acceleration[1],(float)acceleration[2]);
            Vector3 alphaBody=new Vector3((float)acceleration[3],(float)acceleration[4],(float)acceleration[5]);
            for(int i=0;i<index;i++)
            {
                var p=cells[i];Vector3 a=aBody+Vector3.Cross(alphaBody,p.p-body.worldCenterOfMass)+p.j1*(float)acceleration[6]+p.j2*(float)acceleration[7];
                float an=Vector3.Dot(p.n,a);Vector3 at=Vector3.ProjectOnPlane(a,p.n);
                Vector3 force=p.force-p.n*((p.mass+dt*p.slope)*an)-dt*p.ct*(p.t.magnitude*at
                    +(p.t.sqrMagnitude>1e-16f?p.t*Vector3.Dot(p.t,at)/p.t.magnitude:Vector3.zero));
                float pressure=p.area>0?p.pressure-(p.mass+dt*p.slope)*an/p.area:0;
                hydro.totalForceN+=force;hydro.totalTorqueNm+=Vector3.Cross(p.p-body.worldCenterOfMass,force);
                hydro.servoFluidLoadNm-=Vector3.Dot(axis,Vector3.Cross(p.p-root,force));
                hydro.submergedAreaM2+=p.area;hydro.meanPressurePa+=pressure*p.area;
                hydro.minPressurePa=Mathf.Min(hydro.minPressurePa,pressure);hydro.maxPressurePa=Mathf.Max(hydro.maxPressurePa,pressure);
                hydro.actuatorFluidPowerW-=Vector3.Dot(force,p.vAct);
            }
            appliedBodyForceN=hydro.totalForceN+dryKnownForce+probeForce;appliedBodyTorqueNm=hydro.totalTorqueNm+dryKnownTorque+Vector3.Cross(probePoint-body.worldCenterOfMass,probeForce);
            // Internal dry-mass acceleration is reacted onto the single aggregate Rigidbody.
            for(int a=0;a<3;a++)for(int j=6;j<N;j++)
            {appliedBodyForceN[a]-=(float)(dry[a,j]*acceleration[j]);appliedBodyTorqueNm[a]-=(float)(dry[a+3,j]*acceleration[j]);}
            if(!hydro.evaluateOnly&&!body.isKinematic){body.AddForce(appliedBodyForceN);body.AddTorque(appliedBodyTorqueNm);}
            if(hydro.submergedAreaM2>0)hydro.meanPressurePa/=hydro.submergedAreaM2;
            Vector3 local=transform.InverseTransformDirection(hydro.totalForceN);hydro.thrustN=local.z;hydro.lateralN=local.x;
            // evaluateOnly is intentionally a frozen-state force probe.
            if(!hydro.evaluateOnly&&!lockJoints)
            {
                for(int j=0;j<2;j++)
                {
                    velocitiesRadS[j]+=(float)acceleration[6+j]*dt;anglesRad[j]+=velocitiesRadS[j]*dt;
                    float bound=validityAngleDegrees*Mathf.Deg2Rad;
                    if(Mathf.Abs(anglesRad[j])>bound){anglesRad[j]=Mathf.Clamp(anglesRad[j],-bound,bound);velocitiesRadS[j]=0;validityLimitReached=true;}
                }
            }
            elasticEnergyJ=.5f*(stiffnessNmPerRad.x*anglesRad.x*anglesRad.x+stiffnessNmPerRad.y*anglesRad.y*anglesRad.y);
            structuralLossW=dampingNmSPerRad.x*velocitiesRadS.x*velocitiesRadS.x+dampingNmSPerRad.y*velocitiesRadS.y*velocitiesRadS.y;
            PrepareGeometry();Vector3 tip,norm,vel,ac,jj1,jj2;Sample(hydro.chordM,0,out tip,out norm,out vel,out ac,out jj1,out jj2);
            tipDeflectionM=Vector3.Distance(tip,root+r0*new Vector3(0,0,-hydro.leadingDistanceM-hydro.chordM));
        }
        bool Solve()
        {
            for(int i=0;i<N;i++){for(int j=0;j<N;j++)system[i,j]=matrix[i,j];system[i,N]=rhs[i];}
            for(int k=0;k<N;k++)
            {
                int p=k;for(int i=k+1;i<N;i++)if(Math.Abs(system[i,k])>Math.Abs(system[p,k]))p=i;
                if(Math.Abs(system[p,k])<1e-12)return false;
                for(int j=k;j<=N;j++){double t=system[k,j];system[k,j]=system[p,j];system[p,j]=t;}
                double d=system[k,k];for(int j=k;j<=N;j++)system[k,j]/=d;
                for(int i=0;i<N;i++)if(i!=k){double f=system[i,k];for(int j=k;j<=N;j++)system[i,j]-=f*system[k,j];}
            }
            for(int i=0;i<N;i++){acceleration[i]=system[i,N];if(double.IsNaN(acceleration[i])||double.IsInfinity(acceleration[i]))return false;}
            double residual=0;
            for(int i=0;i<N;i++){double r=-rhs[i];for(int j=0;j<N;j++)r+=matrix[i,j]*acceleration[j];residual=Math.Max(residual,Math.Abs(r));}
            maximumResidual=(float)residual;return true;
        }
    }
}
