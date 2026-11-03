using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using IngeSys.Robot;

namespace IngeSys.Diagnostics
{
    /// <summary>Explicit manual tests; both references share the same fluid/dry-inertia calculation.</summary>
    public sealed class FlexiblePaleValidation : MonoBehaviour
    {
        public FlexiblePale flexible;
        public bool running,finished;
        [TextArea(10,35)] public string results;
        public float[] frequencies={1,2,2.5f};
        public float settleSeconds=4,measureSeconds=8;
        public bool runStaticTests=true,runPrescribedTests=true,runFreeTests=true;
        public string outputFile="FLEXIBLE_PALE_VALIDATION.csv";
        StringBuilder report;
        Rigidbody body;FinHydrodynamics hydro;FinServo servo;
        [ContextMenu("Run three-section pale validation in Play mode")]
        public void Run(){if(Application.isPlaying&&!running&&flexible)StartCoroutine(Check());}
        IEnumerator Check()
        {
            running=true;finished=false;body=flexible.GetComponent<Rigidbody>();hydro=flexible.GetComponent<FinHydrodynamics>();servo=flexible.GetComponent<FinServo>();
            var pilot=flexible.GetComponent<RobotKeyboardInput>();
            string servoState=JsonUtility.ToJson(servo),flexState=JsonUtility.ToJson(flexible);
            bool pilotOn=pilot.enabled,kinematic=body.isKinematic,gravity=body.useGravity,evaluate=hydro.evaluateOnly;
            Vector3 position=body.position,velocity=body.linearVelocity,omega=body.angularVelocity;Quaternion rotation=body.rotation;
            bool flexOn=flexible.enabled;float sleep=body.sleepThreshold;
            report=new StringBuilder("case,pass,frequencyHz,meanThrustN,fluidTorqueRmsNm,bodyTorqueRmsNm,yawRangeDeg,pitchRangeDeg,hinge1PeakDeg,hinge2PeakDeg,tipPeakM,forwardSpeedMps,extra\n");
            try
            {
                pilot.enabled=false;flexible.enabled=true;flexible.diagnosticTipForceN=0;hydro.evaluateOnly=false;
                servo.enabled=true;servo.offsetRad=0;servo.amplitudeRad=20*Mathf.Deg2Rad;body.sleepThreshold=0;
                if(runStaticTests)
                {
                    body.linearVelocity=body.angularVelocity=Vector3.zero;body.isKinematic=true;body.useGravity=false;
                    SetState(hydro.water.WaterLevel+2);servo.animate=false;servo.ResetMotion();flexible.lockJoints=false;
                    flexible.ResetDeformation();flexible.diagnosticTipForceN=.1f;
                    yield return Wait(6);
                    flexible.PrepareGeometry();float expected1=-.1f*(2*hydro.chordM/3)/flexible.stiffnessNmPerRad.x;
                    float expected2=-.1f*(hydro.chordM/3)/flexible.stiffnessNmPerRad.y;
                    bool pass=Mathf.Abs(flexible.anglesRad.x-expected1)<Mathf.Abs(expected1)*.08f&&Mathf.Abs(flexible.anglesRad.y-expected2)<Mathf.Abs(expected2)*.08f;
                    Row("dry_static_tip_load",pass,0,0,0,0,0,0,Mathf.Abs(flexible.anglesRad.x)*Mathf.Rad2Deg,Mathf.Abs(flexible.anglesRad.y)*Mathf.Rad2Deg,flexible.tipDeflectionM,0,
                        "expectedAnglesRad="+F(expected1)+";"+F(expected2));
                    if(flexible.matchCantileverCompliance)Row("cantilever_compliance_match",
                        Mathf.Abs(flexible.tipDeflectionM-.1f*flexible.referenceTipComplianceMPerN)<.0001f,
                        0,0,0,0,0,0,0,0,flexible.tipDeflectionM,0,"expectedTipM="+F(.1f*flexible.referenceTipComplianceMPerN));
                    float energy=flexible.elasticEnergyJ;flexible.diagnosticTipForceN=0;yield return Wait(6);
                    Row("dry_release_decay",flexible.elasticEnergyJ<energy*.02f&&!flexible.solveFailed,0,0,0,0,0,0,0,0,flexible.tipDeflectionM,0,"energyRatio="+F(flexible.elasticEnergyJ/energy));
                    SetState(4);servo.ResetMotion();flexible.ResetDeformation();yield return Wait(2);
                    Row("wet_unforced_rest",flexible.anglesRad.sqrMagnitude<1e-10f&&hydro.totalForceN.sqrMagnitude<1e-8f,0,0,0,0,0,0,0,0,flexible.tipDeflectionM,0,"force="+hydro.totalForceN.ToString("F6"));
                }
                foreach(bool locked in new[]{true,false})
                {
                    flexible.lockJoints=locked;
                    if(runPrescribedTests)
                    {
                        body.isKinematic=true;body.useGravity=false;
                        yield return Measure("prescribed_"+(locked?"rigid":"flexible"),1,true);
                    }
                    if(runFreeTests)foreach(float f in frequencies)
                    {
                        body.isKinematic=false;body.useGravity=true;
                        yield return Measure("free_"+(locked?"rigid":"flexible"),f,false);
                    }
                }
                finished=true;
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(servoState,servo);JsonUtility.FromJsonOverwrite(flexState,flexible);
                flexible.enabled=flexOn;body.isKinematic=kinematic;body.useGravity=gravity;body.sleepThreshold=sleep;
                body.position=position;body.rotation=rotation;if(!kinematic){body.linearVelocity=velocity;body.angularVelocity=omega;}
                pilot.enabled=pilotOn;hydro.evaluateOnly=evaluate;running=false;Save();
            }
        }
        IEnumerator Measure(string name,float f,bool prescribed)
        {
            SetState(4);servo.ResetMotion();flexible.ResetDeformation();servo.animate=true;servo.frequencyHz=f;servo.amplitudeRad=20*Mathf.Deg2Rad;servo.offsetRad=0;
            var contact=body.GetComponent<RobotContactDiagnostics>();contact.ResetMeasurements();
            yield return Wait(settleSeconds);
            int n=Mathf.CeilToInt(measureSeconds/Time.fixedDeltaTime);float thrust=0,fluidT2=0,bodyT2=0,speed=0;
            float minYaw=float.PositiveInfinity,maxYaw=float.NegativeInfinity,minPitch=minYaw,maxPitch=maxYaw,p1=0,p2=0,tip=0,residual=0;
            bool valid=true;
            for(int i=0;i<n;i++)
            {
                yield return new WaitForFixedUpdate();
                Vector3 forward=body.transform.forward;
                float yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg,pitch=Mathf.Asin(Mathf.Clamp(forward.y,-1,1))*Mathf.Rad2Deg;
                minYaw=Mathf.Min(minYaw,yaw);maxYaw=Mathf.Max(maxYaw,yaw);minPitch=Mathf.Min(minPitch,pitch);maxPitch=Mathf.Max(maxPitch,pitch);
                thrust+=hydro.thrustN;fluidT2+=hydro.servoFluidLoadNm*hydro.servoFluidLoadNm;
                float ty=body.transform.InverseTransformDirection(flexible.appliedBodyTorqueNm).y;bodyT2+=ty*ty;
                speed+=body.transform.InverseTransformDirection(body.linearVelocity).z;
                p1=Mathf.Max(p1,Mathf.Abs(flexible.anglesRad.x)*Mathf.Rad2Deg);p2=Mathf.Max(p2,Mathf.Abs(flexible.anglesRad.y)*Mathf.Rad2Deg);tip=Mathf.Max(tip,flexible.tipDeflectionM);
                residual=Mathf.Max(residual,flexible.maximumResidual);
                valid&=!flexible.solveFailed&&!flexible.validityLimitReached&&float.IsFinite(body.linearVelocity.sqrMagnitude)&&body.linearVelocity.sqrMagnitude<4;
            }
            valid&=contact.collisionEntries==0&&maxPitch-minPitch<5;
            Row(name,valid,f,thrust/n,Mathf.Sqrt(fluidT2/n),Mathf.Sqrt(bodyT2/n),maxYaw-minYaw,maxPitch-minPitch,p1,p2,tip,speed/n,
                "contacts="+contact.collisionEntries+" guard="+flexible.validityLimitReached+" residual="+F(residual));
        }
        void SetState(float y){body.position=new Vector3(0,y,0);body.rotation=Quaternion.identity;if(!body.isKinematic)body.linearVelocity=body.angularVelocity=Vector3.zero;Physics.SyncTransforms();body.WakeUp();}
        static IEnumerator Wait(float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/Time.fixedDeltaTime);i++)yield return new WaitForFixedUpdate();}
        static string F(float x)=>x.ToString("F6",CultureInfo.InvariantCulture);
        void Row(string name,bool pass,float f,float thrust,float fluid,float torque,float yaw,float pitch,float a,float b,float tip,float speed,string extra)
        {
            report.AppendLine(string.Join(",",name,pass,F(f),F(thrust),F(fluid),F(torque),F(yaw),F(pitch),F(a),F(b),F(tip),F(speed),"\""+extra.Replace("\"","'")+"\""));Save();
        }
        void Save(){results=report.ToString();System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath,"../Contexte/"+outputFile),results);}
    }
}
