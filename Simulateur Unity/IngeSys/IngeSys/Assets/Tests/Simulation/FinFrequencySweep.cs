using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using IngeSys.Robot;

namespace IngeSys.Diagnostics
{
    /// <summary>Compare free-swimming oscillations at fixed amplitude, without stabilizing the robot.</summary>
    public sealed class FinFrequencySweep : MonoBehaviour
    {
        public Rigidbody robot;
        public float[] frequencies={.25f,.4f,.6f,.8f,1f,1.25f,1.5f,2f};
        public int settlePeriods=2, measurePeriods=3;
        public float minimumSettleS=2, minimumMeasureS=4;
        public string outputFile="FREQUENCY_SWEEP.csv";
        public bool running,finished;
        [TextArea(10,30)] public string results;
        [ContextMenu("Run frequency sweep in Play mode")]
        public void Run(){if(Application.isPlaying&&!running)StartCoroutine(Sweep());}
        IEnumerator Sweep()
        {
            running=true;finished=false;
            var pilot=robot.GetComponent<RobotKeyboardInput>();var servo=robot.GetComponent<FinServo>();
            var fin=robot.GetComponent<FinHydrodynamics>();var contact=robot.GetComponent<RobotContactDiagnostics>();
            var flexibleState=robot.GetComponent<FlexiblePale>();
            Vector2 previousAngles=flexibleState?flexibleState.anglesRad:Vector2.zero;
            Vector2 previousRates=flexibleState?flexibleState.velocitiesRadS:Vector2.zero;
            bool previousGuard=flexibleState&&flexibleState.validityLimitReached;
            bool input=pilot.readKeyboard,on=pilot.propulsionOn;var mode=pilot.mode;
            float freq=pilot.nominalFrequencyHz;Vector3 pos=robot.position,vel=robot.linearVelocity,omega=robot.angularVelocity;
            Quaternion rot=robot.rotation;
            var report=new StringBuilder("frequencyHz,yawRangeDeg,pitchRangeDeg,yawRateRmsRadS,forwardSpeedMps,forwardDistanceM,contacts,actualAmplitudeDeg,nearAccelerationLimitFraction,stableNoContacts\n");
            try
            {
                pilot.readKeyboard=false;pilot.mode=RobotKeyboardInput.FinMode.STRAIGHT;
                foreach(float f in frequencies)
                {
                    pilot.propulsionOn=false;
                    for(int i=0;i<Mathf.CeilToInt(2f/Time.fixedDeltaTime);i++)yield return new WaitForFixedUpdate();
                    robot.position=pos;robot.rotation=Quaternion.identity;robot.linearVelocity=robot.angularVelocity=Vector3.zero;
                    servo.ResetMotion();var flexible=robot.GetComponent<FlexiblePale>();if(flexible)flexible.ResetDeformation();robot.WakeUp();Physics.SyncTransforms();contact.ResetMeasurements();
                    pilot.nominalFrequencyHz=f;pilot.propulsionOn=true;
                    int settle=Mathf.CeilToInt(Mathf.Max(minimumSettleS,settlePeriods/f)/Time.fixedDeltaTime);
                    for(int i=0;i<settle;i++)yield return new WaitForFixedUpdate();
                    int count=Mathf.CeilToInt(Mathf.Max(minimumMeasureS,measurePeriods/f)/Time.fixedDeltaTime);
                    float yawMin=float.PositiveInfinity,yawMax=float.NegativeInfinity,pitchMin=yawMin,pitchMax=yawMax;
                    float rate2=0,speed=0,start=robot.position.z;
                    float angleMin=float.PositiveInfinity,angleMax=float.NegativeInfinity;int limited=0;bool valid=true;
                    for(int i=0;i<count;i++)
                    {
                        yield return new WaitForFixedUpdate();
                        Vector3 forward=robot.transform.forward;
                        float yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
                        float pitch=Mathf.Atan2(-forward.y,new Vector2(forward.x,forward.z).magnitude)*Mathf.Rad2Deg;
                        yawMin=Mathf.Min(yawMin,yaw);yawMax=Mathf.Max(yawMax,yaw);
                        pitchMin=Mathf.Min(pitchMin,pitch);pitchMax=Mathf.Max(pitchMax,pitch);
                        float rate=robot.transform.InverseTransformDirection(robot.angularVelocity).y;
                        rate2+=rate*rate;speed+=robot.transform.InverseTransformDirection(robot.linearVelocity).z;
                        angleMin=Mathf.Min(angleMin,servo.actualAngleRad);angleMax=Mathf.Max(angleMax,servo.actualAngleRad);
                        if(Mathf.Abs(servo.actualAngularAccelerationRadS2)>=.98f*servo.maximumAccelerationRadS2)limited++;
                        valid &= robot.linearVelocity.sqrMagnitude<1 && robot.position.y>1.3f && robot.position.y<fin.water.WaterLevel-.2f;
                    }
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0:F3},{1:F5},{2:F5},{3:F6},{4:F6},{5:F6},{6},{7:F4},{8:F5},{9}",
                        f,yawMax-yawMin,pitchMax-pitchMin,Mathf.Sqrt(rate2/count),speed/count,robot.position.z-start,contact.collisionEntries,(angleMax-angleMin)*.5f*Mathf.Rad2Deg,limited/(float)count,valid&&contact.collisionEntries==0));
                    results=report.ToString();
                    System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath,"../Contexte/"+outputFile),results);
                }
                finished=true;
            }
            finally
            {
                pilot.readKeyboard=input;pilot.propulsionOn=on;pilot.mode=mode;pilot.nominalFrequencyHz=freq;
                robot.position=pos;robot.rotation=rot;robot.linearVelocity=vel;robot.angularVelocity=omega;
                if(flexibleState){flexibleState.anglesRad=previousAngles;flexibleState.velocitiesRadS=previousRates;flexibleState.validityLimitReached=previousGuard;}
                running=false;results=report.ToString();
            }
        }
    }
}
