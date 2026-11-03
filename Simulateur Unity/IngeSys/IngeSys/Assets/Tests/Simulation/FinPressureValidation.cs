using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using IngeSys.Robot;

namespace IngeSys.Diagnostics
{
    /// <summary>Repeatable prescribed-motion and free-swimming checks; no Play-mode auto-run.</summary>
    public sealed class FinPressureValidation : MonoBehaviour
    {
        public FinHydrodynamics hydro;
        public bool running,finished;
        [TextArea(12,35)] public string results;
        float measuredThrust;
        readonly StringBuilder report=new StringBuilder();

        [ContextMenu("Run pressure validation in Play mode")]
        public void Run() {if(Application.isPlaying&&!running&&hydro)StartCoroutine(Validate());}
        IEnumerator Validate()
        {
            running=true;finished=false;
            var flexible=hydro.GetComponent<FlexiblePale>();bool flexibleOn=flexible&&flexible.enabled;
            if(flexible)flexible.enabled=false;
            var robot=hydro.GetComponent<Rigidbody>();var servo=hydro.GetComponent<FinServo>();
            var pilot=hydro.GetComponent<RobotKeyboardInput>();var buoyancy=hydro.GetComponent<CalmWaterBuoyancy>();
            var drag=hydro.GetComponent<BodyWaterDrag>();var mass=hydro.GetComponent<SlidingMass>();
            Vector3 position=robot.position,velocity=robot.linearVelocity,omega=robot.angularVelocity;
            Quaternion rotation=robot.rotation;bool kinematic=robot.isKinematic,gravity=robot.useGravity;
            bool bOn=buoyancy.enabled,dOn=drag.enabled,mOn=mass.enabled,pOn=pilot.enabled,animate=servo.animate;
            bool evaluate=hydro.evaluateOnly;var mode=hydro.model;
            float amp=servo.amplitudeRad,freq=servo.frequencyHz,offset=servo.offsetRad,reactive=hydro.reactiveCoefficient;
            var disabledRos=new List<Behaviour>();
            foreach(var b in robot.GetComponents<MonoBehaviour>())if((b.GetType().Name=="RobotDepthPublisher"||b.GetType().Name=="RobotImuPublisher")&&b.enabled){b.enabled=false;disabledRos.Add(b);}
            report.Clear();report.AppendLine("case,pass,meanThrustN,meanLateralN,peakPressurePa,areaM2,extra");
            try
            {
                pilot.enabled=buoyancy.enabled=drag.enabled=mass.enabled=false;
                robot.useGravity=false;robot.linearVelocity=robot.angularVelocity=Vector3.zero;
                robot.isKinematic=true;hydro.evaluateOnly=true;hydro.model=FinHydrodynamics.Model.InstantaneousPressureV2;
                robot.position=new Vector3(0,4,0);robot.rotation=Quaternion.identity;
                servo.animate=true;servo.amplitudeRad=.2f;servo.frequencyHz=1;servo.offsetRad=0;
                yield return Measure("symmetric_pressure",true,1);
                float baseThrust=measuredThrust;
                servo.amplitudeRad=.1f;yield return Measure("half_amplitude",true,1);
                AddNote("quadratic_amplitude_scaling",measuredThrust>0&&measuredThrust<baseThrust*.55f,
                    "ratio="+F(measuredThrust/baseThrust));
                servo.amplitudeRad=.2f;servo.frequencyHz=.5f;yield return Measure("half_frequency",true,2);
                AddNote("quadratic_frequency_scaling",measuredThrust>0&&measuredThrust<baseThrust*.55f,
                    "ratio="+F(measuredThrust/baseThrust));
                servo.frequencyHz=1;servo.offsetRad=.15f;yield return Measure("offset_positive",true,1);
                servo.offsetRad=-.15f;yield return Measure("offset_negative",true,1);
                servo.offsetRad=0;robot.position=new Vector3(0,hydro.water.WaterLevel+1,0);
                yield return Measure("dry_blade",false,1);
                robot.position=new Vector3(0,4,0);servo.animate=false;
                yield return Measure("stopped_blade",false,1);
                servo.animate=true;servo.amplitudeRad=.2f;hydro.reactiveCoefficient=0;
                yield return Measure("drag_only_dissipative",false,1);
                hydro.reactiveCoefficient=reactive;
                robot.isKinematic=false;robot.useGravity=true;hydro.evaluateOnly=false;
                buoyancy.enabled=drag.enabled=mass.enabled=true;
                robot.position=new Vector3(0,4,0);robot.rotation=Quaternion.identity;
                robot.linearVelocity=robot.angularVelocity=Vector3.zero;robot.WakeUp();
                servo.offsetRad=0;servo.amplitudeRad=.2f;servo.frequencyHz=1;
                for(int i=0;i<Mathf.CeilToInt(6f/Time.fixedDeltaTime);i++)yield return new WaitForFixedUpdate();
                AddNote("free_swimming",robot.position.z>.01f&&robot.linearVelocity.sqrMagnitude<25&&Mathf.Abs(robot.position.y-4)<.2f,
                    "position="+robot.position.ToString("F4")+" speed="+F(robot.linearVelocity.magnitude)+" meanV1="+hydro.GetComponent<MeanFinPropulsion>().enabled);
                servo.animate=false;
                for(int i=0;i<Mathf.CeilToInt(2f/Time.fixedDeltaTime);i++)yield return new WaitForFixedUpdate();
                AddNote("propulsion_off_no_reactive_drive",Mathf.Abs(servo.actualAngularVelocityRadS)<.001f,
                    "servoRate="+F(servo.actualAngularVelocityRadS)+" passiveForce="+hydro.totalForceN.ToString("F4"));
                finished=true;
            }
            finally
            {
                if(flexible)flexible.enabled=flexibleOn;
                hydro.evaluateOnly=evaluate;hydro.model=mode;hydro.reactiveCoefficient=reactive;
                robot.isKinematic=kinematic;robot.useGravity=gravity;robot.position=position;robot.rotation=rotation;
                if(!kinematic){robot.linearVelocity=velocity;robot.angularVelocity=omega;}
                servo.amplitudeRad=amp;servo.frequencyHz=freq;servo.offsetRad=offset;servo.animate=animate;
                buoyancy.enabled=bOn;drag.enabled=dOn;mass.enabled=mOn;pilot.enabled=pOn;
                foreach(var b in disabledRos)if(b)b.enabled=true;
                running=false;results=report.ToString();Debug.Log("[FIN_PRESSURE_RESULTS]\n"+results);
            }
        }
        IEnumerator Measure(string name,bool positiveThrust,int periods)
        {
            for(int i=0;i<Mathf.CeilToInt(2f/Time.fixedDeltaTime);i++)yield return new WaitForFixedUpdate();
            int steps=Mathf.CeilToInt(4f*periods/Time.fixedDeltaTime);Vector3 sum=Vector3.zero;float peak=0,area=0,minDissipation=float.PositiveInfinity;
            for(int i=0;i<steps;i++)
            {
                yield return new WaitForFixedUpdate();sum+=hydro.totalForceN;
                peak=Mathf.Max(peak,Mathf.Abs(hydro.minPressurePa),Mathf.Abs(hydro.maxPressurePa));
                area+=hydro.submergedAreaM2;minDissipation=Mathf.Min(minDissipation,hydro.dissipatedPowerW);
            }
            var mean=sum/steps;measuredThrust=mean.z;
            bool pass=!float.IsNaN(mean.sqrMagnitude)&&minDissipation>=0;
            if(positiveThrust)pass &= mean.z>0.001f;
            if(name=="symmetric_pressure")pass &= Mathf.Abs(mean.x)<.03f;
            if(name=="offset_positive")pass &= mean.x>0;
            if(name=="offset_negative")pass &= mean.x<0;
            if(name=="dry_blade"||name=="stopped_blade")pass &= peak<.001f&&mean.magnitude<.0001f;
            if(name=="drag_only_dissipative")pass &= Mathf.Abs(mean.z)<.02f&&minDissipation>=0;
            report.AppendLine(name+","+pass+","+F(mean.z)+","+F(mean.x)+","+F(peak)+","+F(area/steps)+",dissipationMinW="+F(minDissipation));
            results=report.ToString();
        }
        void AddNote(string name,bool pass,string extra)
        {report.AppendLine(name+","+pass+",,,,,\""+extra.Replace("\"","\"\"")+"\"");results=report.ToString();}
        static string F(float value)=>value.ToString("F6",CultureInfo.InvariantCulture);
    }
}
