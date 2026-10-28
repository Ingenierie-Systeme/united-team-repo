#!/usr/bin/env python3
"""Receive and validate Unity IMU samples without modifying the simulation."""
import math
import time

import rclpy
from sensor_msgs.msg import Imu


def main():
    rclpy.init()
    node = rclpy.create_node('unity_imu_check')
    samples = []
    subscription = node.create_subscription(Imu, '/imu/data', samples.append, 10)
    try:
        deadline = time.monotonic() + 20
        while len(samples) < 50 and time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=0.2)
        if len(samples) < 10:
            raise RuntimeError(f'Only {len(samples)} IMU messages in 20 seconds')
        stamps = []
        for msg in samples:
            assert msg.header.frame_id == 'imu_link'
            stamps.append(msg.header.stamp.sec + msg.header.stamp.nanosec * 1e-9)
            q = msg.orientation
            assert abs(sum(v * v for v in (q.x, q.y, q.z, q.w)) - 1) < 1e-4
            for vector in (msg.angular_velocity, msg.linear_acceleration):
                assert all(math.isfinite(v) for v in (vector.x, vector.y, vector.z))
        assert all(b > a for a, b in zip(stamps, stamps[1:]))
        frequency = (len(stamps) - 1) / (stamps[-1] - stamps[0])
        print(f'PASS: {len(samples)} messages, {frequency:.2f} Hz wall time, imu_link')
    finally:
        node.destroy_subscription(subscription)
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
