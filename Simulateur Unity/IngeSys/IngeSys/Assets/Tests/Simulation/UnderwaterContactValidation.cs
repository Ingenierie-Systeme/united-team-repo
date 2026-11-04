using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using IngeSys.Robot;

namespace IngeSys.Diagnostics
{
    /// <summary>Run only in Play mode, with the explicit Run context menu. Restores the robot afterwards.</summary>
    public sealed class UnderwaterContactValidation : MonoBehaviour
    {
        public Rigidbody robot;
        public bool finished, running;
        public bool runPressureAfter;
        [TextArea(10,30)] public string results;
        PhysicsMaterial frictionless;
        GameObject fixture;
        RobotContactDiagnostics diagnostics;
        Vector3 finalVelocity;
        float obliqueFrictionlessX;
        readonly StringBuilder report = new StringBuilder();

        [ContextMenu("Run contact validation in Play mode")]
        public void Run()
        {
            if (!Application.isPlaying || running || !robot) return;
            StartCoroutine(Validate());
        }

        IEnumerator Validate()
        {
            running=true;finished=false;
            diagnostics=robot.GetComponent<RobotContactDiagnostics>();
            var p=robot.GetComponent<RobotFishParameters>();
            var b=robot.GetComponent<CalmWaterBuoyancy>();
            var drag=robot.GetComponent<BodyWaterDrag>();
            var managed=new List<Behaviour>();
            var hydro=robot.GetComponent<FinHydrodynamics>();
            if(hydro) managed.Add(hydro);
            foreach(var behaviour in robot.GetComponents<MonoBehaviour>())
                if(behaviour is RobotKeyboardInput || behaviour is FinServo || behaviour is MeanFinPropulsion ||
                    behaviour is SlidingMass || behaviour is CalmWaterBuoyancy || behaviour is BodyWaterDrag ||
                    behaviour.GetType().Name=="RobotDepthPublisher" || behaviour.GetType().Name=="RobotImuPublisher")managed.Add(behaviour);
            var previousEnabled=managed.ConvertAll(x=>x.enabled);
            Vector3 position=robot.position,velocity=robot.linearVelocity,omega=robot.angularVelocity;
            Quaternion rotation=robot.rotation,finRotation=p.finPivot.localRotation;
            float mass=robot.mass,configuredMass=p.totalMassKg;
            bool gravity=robot.useGravity;
            float sleep=robot.sleepThreshold;robot.sleepThreshold=0;
            var colliders=robot.GetComponent<UnderwaterContactSettings>().collisionEnvelope.GetComponentsInChildren<Collider>();
            var materials=new List<PhysicsMaterial>();foreach(var c in colliders)materials.Add(c.sharedMaterial);
            foreach(var behaviour in managed)behaviour.enabled=false;
            p.finPivot.localRotation=Quaternion.identity;
            var cad=robot.transform.Find("Pale_CAD_Pivot");Quaternion cadRotation=cad.localRotation;cad.localRotation=Quaternion.identity;
            frictionless=new PhysicsMaterial("Validation_Frictionless"){staticFriction=0,dynamicFriction=0,bounciness=0,
                frictionCombine=PhysicsMaterialCombine.Minimum,bounceCombine=PhysicsMaterialCombine.Maximum};
            report.Clear();report.AppendLine("case,pass,contacts,impulseNs,meanStepMaxN,finalSpeed,angularSpeed,extra");
            try
            {
                robot.useGravity=false;
                yield return Impact("frontal_impulse_balance",new Vector3(0,0,1),false,false,colliders,materials);
                yield return Impact("oblique_frictionless",new Vector3(.6f,0,1),true,false,colliders,materials);
                obliqueFrictionlessX=Mathf.Abs(finalVelocity.x);
                yield return Impact("oblique_friction",new Vector3(.6f,0,1),false,false,colliders,materials);
                yield return Impact("pale_contact",new Vector3(0,0,-.8f),false,true,colliders,materials);
                RestoreMaterials(colliders,materials);
                fixture=GameObject.CreatePrimitive(PrimitiveType.Cube);fixture.name="Validation_Floor";
                fixture.isStatic=true;fixture.transform.position=new Vector3(0,2.5f,0);fixture.transform.localScale=new Vector3(4,.2f,4);
                fixture.GetComponent<Collider>().sharedMaterial=materials[0];fixture.GetComponent<Collider>().contactOffset=.0005f;
                robot.mass=mass*1.1f;p.totalMassKg=robot.mass;robot.useGravity=true;
                b.enabled=drag.enabled=true;
                SetState(new Vector3(0,3.2f,0),Vector3.down*.1f);
                diagnostics.ResetMeasurements();
                for(int i=0;i<Mathf.CeilToInt(4f/Time.fixedDeltaTime);i++)yield return new WaitForFixedUpdate();
                diagnostics.FlushStep();
                Vector3 before=diagnostics.accumulatedImpulseNs;float start=Time.fixedTime;
                for(int i=0;i<Mathf.CeilToInt(1f/Time.fixedDeltaTime);i++)yield return new WaitForFixedUpdate();
                diagnostics.FlushStep();
                float support=(diagnostics.accumulatedImpulseNs-before).y/(Time.fixedTime-start);
                float expected=b.weightN-b.buoyancyN;
                bool pass=diagnostics.collisionEntries>0 && robot.linearVelocity.magnitude<.03f &&
                    Mathf.Abs(support-expected)<Mathf.Max(.15f,expected*.15f);
                Add("submerged_floor_support",pass,"supportN="+F(support)+" expectedN="+F(expected));
                Destroy(fixture);fixture=null;
                b.enabled=drag.enabled=false;
                robot.mass=b.density*b.TotalVolume;p.totalMassKg=robot.mass;robot.useGravity=false;
                SetState(new Vector3(0,4,0),Vector3.zero);diagnostics.ResetMeasurements();
                b.enabled=drag.enabled=true;robot.useGravity=true;
                Vector3 stableStart=robot.position;
                for(int i=0;i<Mathf.CeilToInt(2f/Time.fixedDeltaTime);i++)yield return new WaitForFixedUpdate();
                Add("free_submerged_neutral",Vector3.Distance(robot.position,stableStart)<.01f &&
                    robot.linearVelocity.magnitude<.01f && diagnostics.collisionEntries==0,"driftM="+F(Vector3.Distance(robot.position,stableStart)));
                finished=true;
            }
            finally
            {
                if(fixture)Destroy(fixture);
                RestoreMaterials(colliders,materials);
                if(frictionless)Destroy(frictionless);
                robot.mass=mass;p.totalMassKg=configuredMass;robot.useGravity=gravity;robot.sleepThreshold=sleep;
                robot.position=position;robot.rotation=rotation;robot.linearVelocity=velocity;robot.angularVelocity=omega;
                p.finPivot.localRotation=finRotation;cad.localRotation=cadRotation;
                for(int i=0;i<managed.Count;i++)managed[i].enabled=previousEnabled[i];
                diagnostics.ResetMeasurements();
                running=false;results=report.ToString();Debug.Log("[CONTACT_RESULTS]\n"+results);
                if(finished && runPressureAfter) GetComponent<FinPressureValidation>().Run();
            }
        }

        IEnumerator Impact(string name,Vector3 initial,bool noFriction,bool reverse,Collider[] colliders,List<PhysicsMaterial> materials)
        {
            fixture=GameObject.CreatePrimitive(PrimitiveType.Cube);fixture.name="Validation_Wall";fixture.isStatic=true;
            fixture.transform.position=new Vector3(0,4,reverse?-1.3f:1.1f);fixture.transform.localScale=new Vector3(5,3,.1f);
            fixture.GetComponent<Collider>().sharedMaterial=noFriction?frictionless:materials[0];
            fixture.GetComponent<Collider>().contactOffset=.0005f;
            for(int i=0;i<colliders.Length;i++)colliders[i].sharedMaterial=noFriction?frictionless:materials[i];
            SetState(new Vector3(0,4,0),initial);diagnostics.ResetMeasurements();
            float beforeEnergy=Energy();
            for(int i=0;i<Mathf.CeilToInt(2.2f/Time.fixedDeltaTime);i++)yield return new WaitForFixedUpdate();
            diagnostics.FlushStep();
            finalVelocity=robot.linearVelocity;
            float momentumError=(robot.mass*(finalVelocity-initial)-diagnostics.accumulatedImpulseNs).magnitude;
            bool pass=diagnostics.collisionEntries>0 && Energy()<=beforeEnergy+.01f && momentumError<.02f;
            if(reverse)pass &= diagnostics.lastOwnCollider=="Contact_Blade_NeutralV1";
            if(name=="oblique_friction")pass &= Mathf.Abs(finalVelocity.x)<obliqueFrictionlessX-.02f;
            Add(name,pass,"momentumErrorNs="+F(momentumError)+" vx="+F(finalVelocity.x)+" collider="+diagnostics.lastOwnCollider);
            Destroy(fixture);fixture=null;
            yield return new WaitForFixedUpdate();
        }
        void SetState(Vector3 position,Vector3 velocity)
        {robot.position=position;robot.rotation=Quaternion.identity;robot.linearVelocity=velocity;robot.angularVelocity=Vector3.zero;robot.WakeUp();Physics.SyncTransforms();}
        float Energy()
        {
            var w=Quaternion.Inverse(robot.rotation*robot.inertiaTensorRotation)*robot.angularVelocity;
            return .5f*robot.mass*robot.linearVelocity.sqrMagnitude+.5f*Vector3.Dot(Vector3.Scale(robot.inertiaTensor,w),w);
        }
        void Add(string name,bool pass,string extra)
        {
            report.AppendLine(name+","+pass+","+diagnostics.collisionEntries+","+F(diagnostics.accumulatedImpulseNs.magnitude)+","+
                F(diagnostics.maxStepMeanForceN)+","+F(robot.linearVelocity.magnitude)+","+F(robot.angularVelocity.magnitude)+","+extra);
            results=report.ToString();
        }
        static void RestoreMaterials(Collider[] colliders,List<PhysicsMaterial> materials)
        {for(int i=0;i<colliders.Length;i++)if(colliders[i])colliders[i].sharedMaterial=materials[i];}
        static string F(float value)=>value.ToString("F6",CultureInfo.InvariantCulture);
    }
}
