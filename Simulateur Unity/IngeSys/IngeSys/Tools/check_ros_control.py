#!/usr/bin/env python3
"""Network smoke checks. Select the corresponding Unity control source before each case.
Run with a sourced ROS2 environment; writes a report inside the Unity project's Contexte.
"""
import argparse
import json
import math
from pathlib import Path
import time

import rclpy
from rclpy.node import Node
from std_msgs.msg import Float32MultiArray, Float32


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('case', choices=['raw', 'sine', 'invalid', 'parameters', 'disabled', 'manual'])
    args = parser.parse_args()
    rclpy.init()
    node = Node('unity_actuator_network_check')
    raw = node.create_publisher(Float32MultiArray, '/output/servos', 10)
    fin = node.create_publisher(Float32MultiArray, '/robot_fish/fin_command', 10)
    mass = node.create_publisher(Float32, '/robot_fish/mass_position', 10)
    samples = []
    node.create_subscription(Float32MultiArray, '/robot_fish/actuators_state', lambda msg: samples.append((time.monotonic(), list(msg.data))), 50)
    # Allow discovery before evaluating the command.
    start = time.monotonic()
    duration = 5 if args.case in ['raw', 'sine', 'parameters'] else 3
    while time.monotonic() - start < duration:
        t = time.monotonic() - start
        if args.case in ['raw', 'manual']:
            raw.publish(Float32MultiArray(data=[0.25, 0.5]))
        elif args.case == 'sine':
            raw.publish(Float32MultiArray(data=[0.3 * math.sin(math.pi*t), 0.0]))
        elif args.case == 'invalid':
            raw.publish(Float32MultiArray(data=[float('nan'), 0.0]))
        else:
            fin.publish(Float32MultiArray(data=[0.2, 0.5, 0.1, 0.0 if args.case == 'disabled' else 1.0]))
            mass.publish(Float32(data=0.04))
        rclpy.spin_once(node, timeout_sec=0.005)
        time.sleep(0.015)
    tail = [data for stamp, data in samples if stamp > start + duration - 1]
    assert tail and all(len(data) == 8 for data in tail), 'No valid state feedback received'
    latest = tail[-1]
    if args.case == 'raw':
        passed = abs(latest[0] - math.pi/12) < 0.001 and abs(latest[1] - math.pi/12) < 0.01 and abs(latest[4] - 0.06) < 0.001 and abs(latest[5] - 0.06) < 0.003 and latest[6] == 1
    elif args.case == 'sine':
        measured = [data[1] for stamp, data in samples if stamp > start+1]
        passed = min(measured) < -0.15 and max(measured) > 0.15 and all(abs(data[2]) <= 8.727 and abs(data[3]) <= 40.01 for _, data in samples) and latest[6] == 1
    elif args.case == 'invalid':
        passed = latest[7] > 1 and abs(latest[1]) < 0.01 and abs(latest[2]) < 0.01
    elif args.case == 'parameters':
        measured = [data[1] for stamp, data in samples if stamp > start+1]
        passed = min(measured) < -0.05 and max(measured) > 0.25 and abs(latest[4] - 0.04) < 0.001 and latest[6] == 2
    elif args.case == 'disabled':
        passed = abs(latest[1]) < 0.01 and abs(latest[2]) < 0.01 and latest[6] == 2
    else:
        passed = latest[6] == 0 and abs(latest[1]) < 0.01
    result = dict(case=args.case, passed=passed, feedback_count=len(samples), last_state=latest)
    path = Path(__file__).resolve().parents[1] / 'Contexte' / 'ROS_CONTROL_NETWORK_VALIDATION.json'
    previous = json.loads(path.read_text()) if path.exists() else []
    previous.append(result)
    path.write_text(json.dumps(previous, indent=2))
    print(json.dumps(result))
    node.destroy_node()
    rclpy.shutdown()
    if not passed:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
