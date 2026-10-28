#!/usr/bin/env python3
"""Check the ideal Unity pressure/depth sensor with its default calibration."""
import math
import time

import rclpy
from sensor_msgs.msg import FluidPressure
from std_msgs.msg import Float64


def main():
    rclpy.init()
    node = rclpy.create_node('unity_depth_check')
    pressures, depths = [], []
    node.create_subscription(FluidPressure, '/barometer/pressure', pressures.append, 10)
    node.create_subscription(Float64, '/depth', depths.append, 10)
    try:
        deadline = time.monotonic() + 20
        while min(len(pressures), len(depths)) < 50 and time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=0.2)
        assert min(len(pressures), len(depths)) >= 10, 'Missing pressure or depth messages'
        stamps = []
        for msg in pressures:
            assert msg.header.frame_id == 'barometer_link'
            assert math.isfinite(msg.fluid_pressure) and msg.fluid_pressure >= 101325
            assert msg.variance == 0
            stamps.append(msg.header.stamp.sec + msg.header.stamp.nanosec * 1e-9)
        assert all(b > a for a, b in zip(stamps, stamps[1:]))
        assert all(math.isfinite(msg.data) and msg.data >= 0 for msg in depths)
        # Topics are received independently: compare the latest pressure-derived depth
        # with the nearest recent depth, allowing 2 cm for transport/sample skew.
        derived = (pressures[-1].fluid_pressure - 101325) / (1000 * 9.81)
        assert min(abs(derived - msg.data) for msg in depths[-10:]) < 0.02
        hz = (len(stamps) - 1) / (stamps[-1] - stamps[0])
        print(f'PASS: {len(pressures)} pressures, {len(depths)} depths, {hz:.2f} Hz; '
              f'{pressures[-1].fluid_pressure:.2f} Pa, {depths[-1].data:.3f} m')
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
