#!/usr/bin/env python3

import rclpy
from rclpy.node import Node

from std_msgs.msg import Float32MultiArray

import sys
import select
import termios
import tty

from math import sin, pi
import time


class ManualControlNode(Node):

    def __init__(self):

        super().__init__("manual_control")

        # ----------------------------------
        # Parameters

        self.frequency = 0.5

        # ----------------------------------
        # Propulsion state

        self.direction = "stop"

        self.amplitude = 0.0
        self.center = 0.0

        self.start_time = time.monotonic()

        # ----------------------------------
        # Mass position

        self.mass_position = 0.0
        self.mass_step = 0.05

        # ----------------------------------
        # Publisher

        self.pub_servos = self.create_publisher(
            Float32MultiArray,
            "/output/servos",
            10
        )

        # ----------------------------------
        # Terminal configuration

        self.settings = termios.tcgetattr(sys.stdin)
        tty.setcbreak(sys.stdin.fileno())

        # ----------------------------------
        # Timer: 50 Hz

        self.timer = self.create_timer(
            1.0 / 50.0,
            self.update
        )

        self.get_logger().info("Manual control started.")

        print("\n========== MANUAL CONTROL ==========")
        print("Z : Forward")
        print("Q : Left")
        print("D : Right")
        print("S : Stop propulsion")
        print("I : Mass forward")
        print("K : Mass backward")
        print("SPACE : Emergency stop")
        print("====================================\n")

    # ==================================
    # Direction management

    def set_direction(self, direction):

        self.direction = direction

        if direction == "forward":
            self.center = 0.0
            self.amplitude = 1.0

        elif direction == "left":
            self.center = 0.5
            self.amplitude = 0.5

        elif direction == "right":
            self.center = -0.5
            self.amplitude = 0.5

        else:
            self.center = 0.0
            self.amplitude = 0.0

        self.start_time = time.monotonic()

        print(f"Direction: {direction}")

    # ==================================
    # Keyboard management

    def read_keyboard(self):

        if select.select([sys.stdin], [], [], 0)[0]:

            key = sys.stdin.read(1).lower()

            if key == 'z':
                self.set_direction("forward")

            elif key == 'q':
                self.set_direction("left")

            elif key == 'd':
                self.set_direction("right")

            elif key == 's':
                self.set_direction("stop")

            elif key == 'i':
                self.mass_position += self.mass_step

            elif key == 'k':
                self.mass_position -= self.mass_step

            elif key == ' ':
                self.set_direction("stop")
                self.mass_position = 0.0

            self.mass_position = max(
                -1.0,
                min(1.0, self.mass_position)
            )

    # ==================================
    # Main update

    def update(self):

        self.read_keyboard()

        # Elapsed time
        t = time.monotonic() - self.start_time

        # Sinusoidal fin command
        fin_command = (
            self.center
            + self.amplitude * sin(2 * pi * self.frequency * t)
        )

        # Mass command
        mass_command = self.mass_position

        # ROS message
        msg = Float32MultiArray()

        msg.data = [
            float(fin_command),
            float(mass_command)
        ]

        self.pub_servos.publish(msg)

    # ==================================
    # Shutdown

    def shutdown(self):

        self.center = 0.0
        self.amplitude = 0.0
        self.mass_position = 0.0

        self.update()

        termios.tcsetattr(
            sys.stdin,
            termios.TCSADRAIN,
            self.settings
        )


def main():

    rclpy.init()

    node = ManualControlNode()

    try:
        rclpy.spin(node)

    except KeyboardInterrupt:
        pass

    finally:
        node.shutdown()
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
