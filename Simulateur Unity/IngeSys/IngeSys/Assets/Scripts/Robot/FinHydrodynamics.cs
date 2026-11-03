using UnityEngine;
using IngeSys.Environment;

namespace IngeSys.Robot
{
    /// <summary>
    /// V2 strip-pressure approximation with implicit fluid reaction.
    /// Not CFD: circulation and wake are unresolved. Optional FlexiblePale supplies three-section bending.
    /// </summary>
    [DefaultExecutionOrder(-40), RequireComponent(typeof(Rigidbody), typeof(FinServo), typeof(MeanFinPropulsion)), RequireComponent(typeof(FinHydrodynamicStep))]
    public sealed class FinHydrodynamics : MonoBehaviour
    {
        public enum Model { InstantaneousPressureV2, MeanV1 }
        public Model model = Model.InstantaneousPressureV2;
        public Transform cadPivot;
        public FlatWaterSurface water;
        [Header("CAD dimensions; thickness confirmed by user")]
        public float thicknessM=.0015f;
        [Tooltip("PVC-U rigid reference; actual grade not identified.")]
        public float pvcDensityKgM3=1450f;
        [Tooltip("PVC reference modulus; used by FlexiblePale when attached. Grade remains unmeasured.")]
        public float referenceYoungModulusPa=3000000000f;
        public float EstimatedPaleMassKg => chordM*spanM*thicknessM*pvcDensityKgM3;
        public float leadingDistanceM=.04f, chordM=.3f, spanM=.15f;
        [Range(4,32)] public int chordSamples=12;
        [Range(2,16)] public int spanSamples=6;
        [Tooltip("Generic flat-plate reference (NASA), not calibrated for this oscillating underwater fin.")]
        [Min(0)] public float normalDragCoefficient=1.28f;
        [Min(0)] public float tangentialDragCoefficient=.008f;
        [Tooltip("Provisional strip-inertia closure; not a PVC material property.")]
        [Min(0)] public float reactiveCoefficient=.25f;
        [Min(.0001f)] public float interfaceSmoothingM=.004f;
        [Tooltip("Diagnostic only: calculate pressures without applying forces.")]
        public bool evaluateOnly;
        [Tooltip("Couple pressure and rigid-body acceleration implicitly, avoiding explicit drag overshoot.")]
        public bool implicitFluidCoupling=true;
        public float servoFluidLoadNm;
        readonly FinFluidCoupling coupling=new FinFluidCoupling();
        struct Panel {public Vector3 point,normal,tangent,actuatorVelocity,force;public float area,pressure,mass,slope,tangentCoefficient;}
        Panel[] panels;
        public Vector3 totalForceN, totalTorqueNm;
        public float meanPressurePa, minPressurePa, maxPressurePa, submergedAreaM2;
        public float thrustN, lateralN, dissipatedPowerW, actuatorFluidPowerW;
        Rigidbody body;
        FinServo servo;
        MeanFinPropulsion mean;
        CalmWaterBuoyancy buoyancy;
        bool originalMeanEnabled, originalLimitAcceleration;
        Vector3 pivotLocal;
        float heightLocal;

        void OnEnable()
        {
            body=GetComponent<Rigidbody>();servo=GetComponent<FinServo>();
            mean=GetComponent<MeanFinPropulsion>();buoyancy=GetComponent<CalmWaterBuoyancy>();
            originalMeanEnabled=mean.enabled;originalLimitAcceleration=servo.limitAcceleration;
            Configure();
        }
        void Start() { CaptureGeometry(); }
        public void CaptureGeometry()
        {
            if(cadPivot)pivotLocal=transform.InverseTransformPoint(cadPivot.position);
            heightLocal=0;
        }
        void Configure()
        {
            bool instantaneous=model==Model.InstantaneousPressureV2;
            mean.enabled=!instantaneous;
            servo.limitAcceleration=instantaneous||originalLimitAcceleration;
        }
        void FixedUpdate()
        {
            Configure();
        }
        public void StepHydrodynamics()
        {
            totalForceN=totalTorqueNm=Vector3.zero;
            meanPressurePa=minPressurePa=maxPressurePa=submergedAreaM2=0;
            thrustN=lateralN=dissipatedPowerW=actuatorFluidPowerW=servoFluidLoadNm=0;
            if(model!=Model.InstantaneousPressureV2 || !water || !cadPivot)return;
            var flexible=GetComponent<FlexiblePale>();
            if(flexible&&flexible.enabled){flexible.Step();return;}
            var rotation=Quaternion.AngleAxis(servo.actualAngleRad*Mathf.Rad2Deg,Vector3.up);
            var normal=transform.TransformDirection(rotation*Vector3.right);
            var axis=transform.up;
            Vector3 pivot=transform.TransformPoint(pivotLocal);
            float density=buoyancy?buoyancy.density:1000f;
            int nx=Mathf.Max(4,chordSamples),ny=Mathf.Max(2,spanSamples);
            float area=chordM*spanM/(nx*ny);
            if(panels==null||panels.Length!=nx*ny)panels=new Panel[nx*ny];
            int panelCount=0;
            bool coupled=implicitFluidCoupling&&!body.isKinematic;
            if(coupled)coupling.Reset(body);
            for(int x=0;x<nx;x++)for(int y=0;y<ny;y++)
            {
                float distance=leadingDistanceM+chordM*(x+.5f)/nx;
                var local=rotation*new Vector3(0,heightLocal+spanM*((y+.5f)/ny-.5f),-distance);
                Vector3 arm=transform.TransformVector(local),point=pivot+arm;
                float h=water.GetHeightAtWorldXZ(point.x,point.z);
                float immersion=Mathf.Clamp01(.5f+(h-point.y)/interfaceSmoothingM);
                if(immersion<=0)continue;
                Vector3 actuatorVelocity=Vector3.Cross(axis*servo.actualAngularVelocityRadS,arm);
                Vector3 relativeVelocity=body.GetPointVelocity(point)+actuatorVelocity;
                float vn=Vector3.Dot(relativeVelocity,normal);
                Vector3 actuatorAcceleration=Vector3.Cross(axis*servo.actualAngularAccelerationRadS2,arm)
                    +Vector3.Cross(axis*servo.actualAngularVelocityRadS,actuatorVelocity);
                // Prescribed and convective acceleration. Unknown rigid-body acceleration is
                // included by the implicit force closure below.
                Vector3 knownAcceleration=actuatorAcceleration+2*Vector3.Cross(body.angularVelocity,actuatorVelocity)
                    +Vector3.Cross(body.angularVelocity,Vector3.Cross(body.angularVelocity,point-body.worldCenterOfMass));
                float an=Vector3.Dot(knownAcceleration,normal);
                float pressure=-.5f*density*normalDragCoefficient*vn*Mathf.Abs(vn)
                    -reactiveCoefficient*density*Mathf.PI*spanM*.25f*an;
                Vector3 tangent=Vector3.ProjectOnPlane(relativeVelocity,normal);
                Vector3 force=(normal*pressure-.5f*density*tangentialDragCoefficient*tangent.magnitude*tangent)*area*immersion;
                float wetArea=area*immersion;
                var panel=new Panel {point=point,normal=normal,tangent=tangent,actuatorVelocity=actuatorVelocity,
                    force=force,area=wetArea,pressure=pressure,
                    mass=reactiveCoefficient*density*Mathf.PI*spanM*.25f*wetArea,
                    slope=density*normalDragCoefficient*Mathf.Abs(vn)*wetArea,
                    tangentCoefficient=.5f*density*tangentialDragCoefficient*wetArea};
                panels[panelCount++]=panel;
                if(coupled)coupling.Add(point-body.worldCenterOfMass,normal,tangent,panel.slope,
                    panel.tangentCoefficient,panel.mass,Time.fixedDeltaTime,force);
                dissipatedPowerW+=(.5f*density*normalDragCoefficient*Mathf.Pow(Mathf.Abs(vn),3)
                    +.5f*density*tangentialDragCoefficient*Mathf.Pow(tangent.magnitude,3))*wetArea;
            }
            Vector3 linearAcceleration=Vector3.zero,angularAcceleration=Vector3.zero;
            if(coupled&&!coupling.Solve(body,out linearAcceleration,out angularAcceleration))
                Debug.LogError("Fin fluid force solve failed",this);
            for(int i=0;i<panelCount;i++)
            {
                var p=panels[i];Vector3 force=p.force;float pressure=p.pressure;
                if(coupled)
                {
                    Vector3 a=linearAcceleration+Vector3.Cross(angularAcceleration,p.point-body.worldCenterOfMass);
                    float an=Vector3.Dot(p.normal,a);
                    Vector3 at=Vector3.ProjectOnPlane(a,p.normal);
                    pressure-=(p.mass+Time.fixedDeltaTime*p.slope)*an/p.area;
                    force-=p.normal*((p.mass+Time.fixedDeltaTime*p.slope)*an)
                        +Time.fixedDeltaTime*p.tangentCoefficient*(p.tangent.magnitude*at
                        +(p.tangent.sqrMagnitude>1e-16f?p.tangent*(Vector3.Dot(p.tangent,at)/p.tangent.magnitude):Vector3.zero));
                }
                if(!evaluateOnly)body.AddForceAtPosition(force,p.point,ForceMode.Force);
                totalForceN+=force;totalTorqueNm+=Vector3.Cross(p.point-body.worldCenterOfMass,force);
                servoFluidLoadNm-=Vector3.Dot(axis,Vector3.Cross(p.point-pivot,force));
                submergedAreaM2+=p.area;meanPressurePa+=pressure*p.area;
                minPressurePa=Mathf.Min(minPressurePa,pressure);maxPressurePa=Mathf.Max(maxPressurePa,pressure);
                actuatorFluidPowerW-=Vector3.Dot(force,p.actuatorVelocity);
            }
            if(submergedAreaM2>0)meanPressurePa/=submergedAreaM2;
            var localForce=transform.InverseTransformDirection(totalForceN);
            thrustN=localForce.z;lateralN=localForce.x;
        }
        void OnDisable()
        {
            if(mean)mean.enabled=originalMeanEnabled;
            if(servo)servo.limitAcceleration=originalLimitAcceleration;
            totalForceN=totalTorqueNm=Vector3.zero;
        }
    }
}
