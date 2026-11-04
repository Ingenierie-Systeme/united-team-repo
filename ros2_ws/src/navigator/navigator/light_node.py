#!/usr/bin/env python3

import rclpy
from rclpy.node import Node

from std_msgs.msg import Float32

import sys
import select
import termios
import tty


class LightNode(Node):

    def __init__(self):

        super().__init__("light")

        # ----------------------------------
        # PWM values

        self.PWM_OFF = -0.5
        self.PWM_MID = 0.0
        self.PWM_ON = 0.5

        # ----------------------------------
        # Current state

        self.current_pwm = self.PWM_OFF

        # ----------------------------------
        # Publisher

        self.light_pub = self.create_publisher(
            Float32,
            "/output/light_pwm",
            10
        )

        # ----------------------------------
        # Terminal configuration

        self.settings = termios.tcgetattr(sys.stdin)
        tty.setcbreak(sys.stdin.fileno())

        # ----------------------------------
        # Timer

        self.timer = self.create_timer(
            0.1,
            self.update
        )

        print("\n========== LIGHT CONTROL ==========")
        print("1 : Light OFF")
        print("2 : Intermediate intensity")
        print("3 : Light ON")
        print("===================================\n")

    # ==================================
    # Keyboard management

    def read_keyboard(self):

        if select.select([sys.stdin], [], [], 0)[0]:

            key = sys.stdin.read(1)

            if key == '1':
                self.current_pwm = self.PWM_OFF
                print("Light OFF")

            elif key == '2':
                self.current_pwm = self.PWM_MID
                print("Light MID")

            elif key == '3':
                self.current_pwm = self.PWM_ON
                print("Light ON")

    # ==================================
    # Publish PWM command

    def update(self):

        self.read_keyboard()

        msg = Float32()
        msg.data = float(self.current_pwm)

        self.light_pub.publish(msg)

    # ==================================
    # Shutdown

    def shutdown(self):

        msg = Float32()
        msg.data = float(self.PWM_OFF)

        self.light_pub.publish(msg)

        termios.tcsetattr(
            sys.stdin,
            termios.TCSADRAIN,
            self.settings
        )


def main():

    rclpy.init()

    node = LightNode()

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