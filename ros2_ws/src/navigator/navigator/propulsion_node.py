#!/usr/bin/env python3

import rclpy
from rclpy.node import Node

from std_msgs.msg import Float32MultiArray

import numpy as np
import time


class PropulsionNode(Node):

    def __init__(self):
        super().__init__("propulsion_node")

        # Paramètres du mouvement
        self.amplitude = 0.3
        self.frequency = 0.5

        # Publisher
        self.pub_servos = self.create_publisher(
            Float32MultiArray,
            "/output/servos",
            10
        )

        # Temps initial
        self.start_time = time.monotonic()

        # Timer : fréquence de publication de 50 Hz
        self.timer = self.create_timer(
            1.0 / 50.0,
            self.publish_command
        )

        self.get_logger().info("Propulsion node started")

    def publish_command(self):

        # Temps écoulé
        t = time.monotonic() - self.start_time

        # Commande sinusoïdale
        servo1 = self.amplitude * np.sin(
            2 * np.pi * self.frequency * t
        )

        # Deuxième servo immobile
        servo2 = 0.0

        # Création du message
        msg = Float32MultiArray()
        msg.data = [float(servo1), float(servo2)]

        # Publication
        self.pub_servos.publish(msg)


def main():

    rclpy.init()

    node = PropulsionNode()

    try:
        rclpy.spin(node)

    except KeyboardInterrupt:
        pass

    finally:
        # Envoi d'une commande neutre à l'arrêt
        msg = Float32MultiArray()
        msg.data = [0.0, 0.0]
        node.pub_servos.publish(msg)

        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()