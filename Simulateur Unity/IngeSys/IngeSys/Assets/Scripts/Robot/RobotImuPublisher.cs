using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Sensor;

namespace IngeSys.Robot
{
    /// <summary>Ideal IMU at the body origin; ROS FLU vectors, orientation relative to FLU world.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class RobotImuPublisher : MonoBehaviour
    {
        public string topicName = "/imu/data";
        public string frameId = "imu_link";
        [Min(0.1f)] public float frequency = 20f;
        Rigidbody body;
        ROSConnection ros;
        Vector3 previousVelocity;
        double nextPublishTime;
        bool hasVelocity;

        void Start()
        {
            body = GetComponent<Rigidbody>();
            ros = ROSConnection.GetOrCreateInstance();
            ros.RegisterPublisher<ImuMsg>(topicName);
        }

        void OnEnable()
        {
            hasVelocity = false;
            nextPublishTime = Time.fixedTimeAsDouble;
        }

        void FixedUpdate()
        {
            // Point velocity includes the effect of rotation when the centre of mass moves.
            Vector3 velocity = body.GetPointVelocity(body.position);
            Vector3 acceleration = (velocity - previousVelocity) / Time.fixedDeltaTime;
            previousVelocity = velocity;
            if (!hasVelocity) { hasVelocity = true; return; }
            double now = Time.fixedTimeAsDouble;
            if (now + 1e-8 < nextPublishTime) return;
            double period = 1.0 / Mathf.Max(0.1f, frequency);
            nextPublishTime += (Math.Floor(Math.Max(0, now - nextPublishTime) / period) + 1) * period;

            ImuMsg message = CreateMessage(body.rotation, body.angularVelocity, acceleration,
                                          Physics.gravity, frameId);
            long ticks = DateTime.UtcNow.Ticks - 621355968000000000L;
            message.header.stamp.sec = (int)(ticks / TimeSpan.TicksPerSecond);
            message.header.stamp.nanosec = (uint)((ticks % TimeSpan.TicksPerSecond) * 100);
            ros.Publish(topicName, message);
        }

        public static ImuMsg CreateMessage(Quaternion rotation, Vector3 angularVelocity,
                                           Vector3 acceleration, Vector3 gravity, string frame)
        {
            Quaternion inverse = Quaternion.Inverse(rotation);
            Quaternion orientation = FLU.ConvertFromRUF(rotation);
            // Angular velocity is an axial vector: its handedness conversion differs from acceleration.
            Vector3 angular = FLU.ConvertAngularVelocityFromRUF(inverse * angularVelocity);
            Vector3 specificForce = FLU.ConvertFromRUF(inverse * (acceleration - gravity));
            var message = new ImuMsg();
            message.header.frame_id = frame;
            message.orientation.x = orientation.x;
            message.orientation.y = orientation.y;
            message.orientation.z = orientation.z;
            message.orientation.w = orientation.w;
            message.angular_velocity.x = angular.x;
            message.angular_velocity.y = angular.y;
            message.angular_velocity.z = angular.z;
            message.linear_acceleration.x = specificForce.x;
            message.linear_acceleration.y = specificForce.y;
            message.linear_acceleration.z = specificForce.z;
            // Zero covariance means unknown (ideal sensor, no measured noise model).
            return message;
        }
    }
}
