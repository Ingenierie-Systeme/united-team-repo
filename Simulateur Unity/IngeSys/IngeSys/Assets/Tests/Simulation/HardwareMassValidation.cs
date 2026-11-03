using System.Collections;
using System.Text;
using UnityEngine;
using IngeSys.Robot;
namespace IngeSys.Diagnostics
{
    public sealed class HardwareMassValidation : MonoBehaviour
    {
        public Rigidbody robot;
        public bool running,finished;
        [TextArea(8,25)]public string results;
        [ContextMenu("Run hardware mass validation in Play mode")]
        public void Run(){if(Application.isPlaying&&!running)StartCoroutine(Check());}
        IEnumerator Check()
        {
            running=true;finished=false;var report=new StringBuilder("case,pass,details\n");
            var mass=robot.GetComponent<RobotMassDistribution>();var slide=robot.GetComponent<SlidingMass>();
            var pilot=robot.GetComponent<RobotKeyboardInput>();var hydro=robot.GetComponent<FinHydrodynamics>();
            var servo=robot.GetComponent<FinServo>();var mean=robot.GetComponent<MeanFinPropulsion>();
            bool pOn=pilot.enabled,hOn=hydro.enabled,meanOn=mean.enabled,sOn=servo.enabled;
            bool trim=mass.includeReferenceTrim,gravity=robot.useGravity;float ballast=mass.referenceTrimKg;
            float target=slide.targetPositionM,position=slide.positionM;
            Vector3 pos=robot.position,v=robot.linearVelocity,w=robot.angularVelocity;Quaternion q=robot.rotation;
            string servoState=JsonUtility.ToJson(servo),pilotState=JsonUtility.ToJson(pilot);
            try
            {
                hydro.enabled=false;mean.enabled=false;pilot.enabled=false;servo.animate=false;servo.ResetMotion();
                mass.includeReferenceTrim=true;slide.positionM=slide.targetPositionM=0;mass.Apply(0);
                float originalMass=robot.mass;Vector3 c0=robot.centerOfMass;
                mass.Apply(.1f);float expected=mass.movablePayloadKg*.1f/originalMass;
                report.AppendLine("moving_payload_conserves_mass,"+(Mathf.Abs(robot.mass-originalMass)<1e-6f&&Mathf.Abs(robot.centerOfMass.z-c0.z-expected)<1e-5f)+",deltaCG="+(robot.centerOfMass.z-c0.z));
                mass.Apply(0);
                report.AppendLine("positive_principal_inertia,"+(robot.inertiaTensor.x>0&&robot.inertiaTensor.y>0&&robot.inertiaTensor.z>0)+",tensor="+robot.inertiaTensor.ToString("F6").Replace(',',';'));
                pilot.SetPalmageSettings(20,2,180);servo.amplitudeRad=20*Mathf.Deg2Rad;servo.frequencyHz=2;servo.offsetRad=0;servo.enabled=true;servo.limitAcceleration=true;
                yield return new WaitForFixedUpdate();
                report.AppendLine("hardware_angle_ceiling,"+(Mathf.Abs(servo.maximumAngleRad-Mathf.PI/3)<1e-5f)+",angleDeg="+servo.maximumAngleRad*Mathf.Rad2Deg);
                float feasible=40f/Mathf.Pow(4*Mathf.PI,2);
                report.AppendLine("sinusoidal_acceleration_ceiling,"+(Mathf.Abs(servo.effectiveAmplitudeRad-feasible)<1e-5f)+",amplitudeDeg="+servo.effectiveAmplitudeRad*Mathf.Rad2Deg);
                servo.enabled=false;robot.useGravity=true;
                ResetBody();Vector3 start=robot.position;
                for(int i=0;i<Mathf.CeilToInt(2/Time.fixedDeltaTime);i++)yield return new WaitForFixedUpdate();
                report.AppendLine("neutral_ballast,"+(Vector3.Distance(robot.position,start)<.01f)+",driftM="+Vector3.Distance(robot.position,start));
                mass.referenceTrimKg=ballast+.2f;ResetBody();start=robot.position;
                for(int i=0;i<Mathf.CeilToInt(2/Time.fixedDeltaTime);i++)yield return new WaitForFixedUpdate();
                report.AppendLine("extra_ballast_sinks,"+(robot.position.y<start.y-.03f)+",deltaY="+(robot.position.y-start.y));
                mass.referenceTrimKg=ballast;mass.includeReferenceTrim=false;ResetBody();start=robot.position;
                for(int i=0;i<Mathf.CeilToInt(2/Time.fixedDeltaTime);i++)yield return new WaitForFixedUpdate();
                report.AppendLine("without_ballast_rises,"+(robot.position.y>start.y+.03f)+",deltaY="+(robot.position.y-start.y));
                finished=true;
            }
            finally
            {
                mass.referenceTrimKg=ballast;mass.includeReferenceTrim=trim;
                slide.positionM=position;slide.targetPositionM=target;mass.Apply(position);
                JsonUtility.FromJsonOverwrite(servoState,servo);JsonUtility.FromJsonOverwrite(pilotState,pilot);
                hydro.enabled=hOn;mean.enabled=meanOn;servo.enabled=sOn;pilot.enabled=pOn;
                robot.useGravity=gravity;robot.position=pos;robot.rotation=q;robot.linearVelocity=v;robot.angularVelocity=w;
                running=false;results=report.ToString();
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath,"../Contexte/HARDWARE_MASS_VALIDATION.csv"),results);
            }
        }
        void ResetBody(){robot.position=new Vector3(0,4,0);robot.rotation=Quaternion.identity;robot.linearVelocity=robot.angularVelocity=Vector3.zero;robot.WakeUp();Physics.SyncTransforms();}
    }
}
