#!/usr/bin/env python3

import rclpy
from rclpy.node import Node

from std_msgs.msg import Float32MultiArray

from pynput import keyboard

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
        # Mass position command

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
        # Timer: 50 Hz

        self.timer = self.create_timer(
            1.0 / 50.0,
            self.publish_command
        )

        # ----------------------------------
        # Keyboard listener

        self.listener = keyboard.Listener(
            on_press=self.on_press,
            on_release=self.on_release
        )

        self.listener.start()

        self.get_logger().info(
            "Manual control started."
        )

        self.get_logger().info(
            "Z: forward | Q: left | D: right | S: stop"
        )

        self.get_logger().info(
            "I: mass forward | K: mass backward | SPACE: emergency stop"
        )

    # ==================================
    # Keyboard management

    def on_press(self, key):

        try:
            char = key.char.lower()
        except AttributeError:
            char = None

        # Propulsion commands

        if char == 'z':
            self.set_direction("forward")

        elif char == 'q':
            self.set_direction("left")

        elif char == 'd':
            self.set_direction("right")

        elif char == 's':
            self.set_direction("stop")

        # Mass commands

        elif char == 'i':
            self.mass_position += self.mass_step

        elif char == 'k':
            self.mass_position -= self.mass_step

        # Limit the mass command

        self.mass_position = max(
            -1.0,
            min(1.0, self.mass_position)
        )

        # Emergency stop

        if key == keyboard.Key.space:
            self.set_direction("stop")
            self.mass_position = 0.0

    def on_release(self, key):

        try:
            char = key.char.lower()
        except AttributeError:
            char = None

        # Stop propulsion when releasing the active direction key

        if char in ['z', 'q', 'd']:
            if self.direction == {
                'z': 'forward',
                'q': 'left',
                'd': 'right'
            }[char]:

                self.set_direction("stop")

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

        self.get_logger().info(
            f"Direction changed: {direction}"
        )

    # ==================================
    # Servo command calculation

    def publish_command(self):

        # Elapsed time
        t = time.monotonic() - self.start_time

        # Sinusoidal fin command
        fin_command = (
            self.center
            + self.amplitude * sin(2 * pi * self.frequency * t)
        )

        # Mass position command
        mass_command = self.mass_position

        # Create ROS message
        msg = Float32MultiArray()

        msg.data = [
            float(fin_command),
            float(mass_command)
        ]

        self.pub_servos.publish(msg)


# ==================================
# Main

def main():

    rclpy.init()

    node = ManualControlNode()

    try:
        rclpy.spin(node)

    except KeyboardInterrupt:
        pass

    finally:

        # Stop both servos
        node.center = 0.0
        node.amplitude = 0.0
        node.mass_position = 0.0

        node.publish_command()

        node.listener.stop()

        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()