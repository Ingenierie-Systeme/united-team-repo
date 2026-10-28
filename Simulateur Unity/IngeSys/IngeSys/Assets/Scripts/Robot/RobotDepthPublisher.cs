using System;
using UnityEngine;
using IngeSys.Environment;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;

namespace IngeSys.Robot
{
    /// <summary>Ideal hydrostatic pressure sensor at a point fixed to the robot.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CalmWaterBuoyancy))]
    public sealed class RobotDepthPublisher : MonoBehaviour
    {
        public string pressureTopic = "/barometer/pressure";
        public string depthTopic = "/depth";
        public string frameId = "barometer_link";
        [Min(0.1f)] public float frequency = 20f;
        [Min(0f)] public double atmosphericPressurePa = 101325;
        [Tooltip("Sensor position in the robot local frame, in metres.")]
        public Vector3 sensorLocalPosition = Vector3.zero;
        public double DepthM { get; private set; }
        public double PressurePa { get; private set; }
        CalmWaterBuoyancy buoyancy;
        ROSConnection ros;
        double nextPublishTime;

        void Start()
        {
            buoyancy = GetComponent<CalmWaterBuoyancy>();
            if (!buoyancy.water)
            {
                Debug.LogError("Depth sensor requires the buoyancy water reference.", this);
                enabled = false;
                return;
            }
            ros = ROSConnection.GetOrCreateInstance();
            ros.RegisterPublisher<FluidPressureMsg>(pressureTopic);
            ros.RegisterPublisher<Float64Msg>(depthTopic);
        }

        void OnEnable() { nextPublishTime = Time.fixedTimeAsDouble; }

        void FixedUpdate()
        {
            double now = Time.fixedTimeAsDouble;
            if (now + 1e-8 < nextPublishTime || !buoyancy.water) return;
            double period = 1.0 / Mathf.Max(0.1f, frequency);
            nextPublishTime += (Math.Floor(Math.Max(0, now - nextPublishTime) / period) + 1) * period;
            Vector3 position = transform.TransformPoint(sensorLocalPosition);
            float height = buoyancy.water.GetHeightAtWorldXZ(position.x, position.z);
            // Outside the modelled water domain there is no valid pressure sample.
            if (float.IsNaN(height) || float.IsInfinity(height)) return;
            DepthM = Math.Max(0, (double)height - position.y);
            PressurePa = CalculatePressure(DepthM, buoyancy.density,
                                           Math.Abs(Physics.gravity.y), atmosphericPressurePa);
            var message = new FluidPressureMsg();
            message.header.frame_id = frameId;
            long ticks = DateTime.UtcNow.Ticks - 621355968000000000L;
            message.header.stamp.sec = (int)(ticks / TimeSpan.TicksPerSecond);
            message.header.stamp.nanosec = (uint)((ticks % TimeSpan.TicksPerSecond) * 100);
            message.fluid_pressure = PressurePa;
            message.variance = 0; // Unknown variance; no noise or bias model.
            ros.Publish(pressureTopic, message);
            ros.Publish(depthTopic, new Float64Msg(DepthM));
        }

        public static double CalculatePressure(double depthM, double density,
                                                double gravity, double atmosphericPressure)
        {
            return atmosphericPressure + density * gravity * Math.Max(0, depthM);
        }
    }
}
