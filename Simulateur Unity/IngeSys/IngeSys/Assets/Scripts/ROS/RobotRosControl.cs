using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

namespace IngeSys.Robot
{
    /// <summary>Exclusive command authority. ROS callbacks latch targets; physics applies them in FixedUpdate.</summary>
    [DefaultExecutionOrder(-180), DisallowMultipleComponent]
    [RequireComponent(typeof(FinServo),typeof(SlidingMass),typeof(RobotKeyboardInput))]
    public sealed class RobotRosControl : MonoBehaviour
    {
        public enum ControlSource { Keyboard, RosServoPositions, RosFinParameters }
        public ControlSource source=ControlSource.Keyboard;
        [Header("ROS interfaces (restart Play after changing topic names)")]
        public string servoTopic="/output/servos";
        public string finTopic="/robot_fish/fin_command";
        public string massTopic="/robot_fish/mass_position";
        public string stateTopic="/robot_fish/actuators_state";
        [Min(.05f)] public float commandTimeoutS=1;
        [Min(.1f)] public float stateFrequencyHz=20;
        [Tooltip("Normalized servo1 +/-1 maps to this angle before mechanical clamping, radians.")]
        [Min(0)] public float normalizedFinRangeRad=Mathf.PI/3;
        public bool invertFin, invertMass;
        public int acceptedCommands,rejectedCommands;
        public float requestedAngleRad;
        public bool OwnsActuators=>isActiveAndEnabled&&source!=ControlSource.Keyboard;
        public double CommandAgeS=>hasCommand?System.Math.Max(0,Time.realtimeSinceStartupAsDouble-lastCommandTime):-1;
        public bool CommandFresh=>OwnsActuators&&hasCommand&&CommandAgeS<=Mathf.Max(.05f,commandTimeoutS);
        public string Status=>!OwnsActuators?"Commande clavier":!hasCommand?"ROS : attente de consigne":!CommandFresh?"ROS : délai dépassé, pale au neutre":"ROS : consigne reçue";
        FinServo servo; SlidingMass mass; RobotKeyboardInput keyboard; RobotFishParameters parameters; MeanFinPropulsion mean;
        ROSConnection ros;
        ControlSource appliedSource;
        bool hasCommand,hasMass,subscribed;
        double lastCommandTime,nextStateTime;
        float finNormalized,massNormalized,amplitude,frequency,offset,massMeters;
        bool oscillationEnabled;
        string subscribedServos,subscribedFin,subscribedMass;

        void Awake(){Cache();appliedSource=source;}
        void Cache()
        {
            servo=GetComponent<FinServo>();mass=GetComponent<SlidingMass>();keyboard=GetComponent<RobotKeyboardInput>();
            parameters=GetComponent<RobotFishParameters>();mean=GetComponent<MeanFinPropulsion>();
        }
        void Start()
        {
            ros=ROSConnection.GetOrCreateInstance();
            subscribedServos=servoTopic;subscribedFin=finTopic;subscribedMass=massTopic;
            ros.Subscribe<Float32MultiArrayMsg>(subscribedServos,m=>ReceiveServoPositions(m.data));
            ros.Subscribe<Float32MultiArrayMsg>(subscribedFin,m=>ReceiveFinParameters(m.data));
            ros.Subscribe<Float32Msg>(subscribedMass,m=>ReceiveMassPosition(m.data));
            ros.RegisterPublisher<Float32MultiArrayMsg>(stateTopic);subscribed=true;
            if(OwnsActuators)StopActuators();
        }
        public void SetSource(ControlSource selected)
        {
            if(!servo)Cache();
            if(selected==source&&selected==appliedSource)return;
            source=appliedSource=selected;hasCommand=hasMass=false;
            StopActuators();servo.externalPositionControl=false;
            keyboard.propulsionOn=false;keyboard.mode=RobotKeyboardInput.FinMode.STRAIGHT;
            keyboard.ClearHeldCommands();
        }
        void LatchTime(){hasCommand=true;lastCommandTime=Time.realtimeSinceStartupAsDouble;acceptedCommands++;}
        public bool ReceiveServoPositions(float[] values)
        {
            if(!OwnsActuators||source!=ControlSource.RosServoPositions)return false;
            if(values==null||values.Length<2||!Finite(values[0])||!Finite(values[1])){rejectedCommands++;return false;}
            finNormalized=Mathf.Clamp(values[0],-1,1)*(invertFin?-1:1);
            massNormalized=Mathf.Clamp(values[1],-1,1)*(invertMass?-1:1);
            LatchTime();return true;
        }
        public bool ReceiveFinParameters(float[] values)
        {
            if(!OwnsActuators||source!=ControlSource.RosFinParameters)return false;
            if(values==null||values.Length!=4){rejectedCommands++;return false;}
            foreach(float value in values)if(!Finite(value)){rejectedCommands++;return false;}
            if(values[0]<0||values[1]<0||(values[3]!=0&&values[3]!=1)){rejectedCommands++;return false;}
            amplitude=values[0];frequency=values[1];offset=values[2];oscillationEnabled=values[3]==1;
            LatchTime();return true;
        }
        public bool ReceiveMassPosition(float value)
        {
            if(!OwnsActuators||source!=ControlSource.RosFinParameters)return false;
            if(!Finite(value)){rejectedCommands++;return false;}
            massMeters=Mathf.Clamp(value,parameters.slidingMinimumZ,parameters.slidingMaximumZ);hasMass=true;return true;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        void FixedUpdate()
        {
            if(source!=appliedSource){var selected=source;source=appliedSource;SetSource(selected);}
            if(!OwnsActuators)return;
            if(!CommandFresh){StopActuators();return;}
            if(source==ControlSource.RosServoPositions)
            {
                servo.externalPositionControl=true;
                requestedAngleRad=finNormalized*Mathf.Max(0,normalizedFinRangeRad);
                servo.externalTargetAngleRad=requestedAngleRad;
                servo.animate=true;
                servo.amplitudeRad=servo.frequencyHz=servo.offsetRad=0;
                float neutral=Mathf.Clamp(0,parameters.slidingMinimumZ,parameters.slidingMaximumZ);
                mass.targetPositionM=massNormalized>=0?Mathf.Lerp(neutral,parameters.slidingMaximumZ,massNormalized)
                    :Mathf.Lerp(neutral,parameters.slidingMinimumZ,-massNormalized);
                // A mean A*f model cannot infer propulsion from an externally supplied position stream.
                mean.propulsionEnabled=false;
            }
            else
            {
                servo.externalPositionControl=false;
                servo.amplitudeRad=amplitude;servo.frequencyHz=frequency;servo.offsetRad=offset;
                servo.animate=oscillationEnabled;mean.propulsionEnabled=oscillationEnabled;
                if(hasMass)mass.targetPositionM=massMeters;
            }
        }
        void StopActuators()
        {
            if(!servo)return;
            servo.animate=false;servo.externalTargetAngleRad=0;requestedAngleRad=0;
            mean.propulsionEnabled=false;mass.targetPositionM=mass.positionM;
        }
        void Update()
        {
            if(!subscribed||Time.realtimeSinceStartupAsDouble<nextStateTime)return;
            nextStateTime=Time.realtimeSinceStartupAsDouble+1/System.Math.Max(.1,stateFrequencyHz);
            var state=new Float32MultiArrayMsg();
            state.data=new[]{servo.externalPositionControl?servo.externalTargetAngleRad:servo.offsetRad,
                servo.actualAngleRad,servo.actualAngularVelocityRadS,servo.actualAngularAccelerationRadS2,
                mass.targetPositionM,mass.positionM,(float)source,(float)CommandAgeS};
            ros.Publish(stateTopic,state);
        }
        void OnDisable()
        {
            if(servo&&appliedSource!=ControlSource.Keyboard){StopActuators();servo.externalPositionControl=false;}
            hasCommand=hasMass=false;
        }
        void OnDestroy()
        {
            if(!subscribed||!ros)return;
            ros.Unsubscribe(subscribedServos);ros.Unsubscribe(subscribedFin);ros.Unsubscribe(subscribedMass);
        }
    }
}
