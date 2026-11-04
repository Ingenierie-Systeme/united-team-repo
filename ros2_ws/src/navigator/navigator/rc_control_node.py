#!/usr/bin/env python3

import rclpy
from rclpy.node import Node

from std_msgs.msg import Float32MultiArray

from math import sin, pi
import time


class RcControlNode(Node):

    def __init__(self):

        super().__init__("rc_control")

        # Paramètres de propulsion
        self.frequency_min = 0.2
        self.frequency_max = 1.0
        self.frequency = 0.5

        self.center = 0.0
        self.amplitude = 0.0

        # Position de la masse
        self.mass_position = 0.0

        # Gestion du temps
        self.start_time = time.monotonic()
        self.last_rc_time = None

        # Sécurité : délai maximal sans réception radio
        self.rc_timeout = 0.5

        # État de sécurité
        self.armed = False

        # Subscriber radiocommande
        self.rc_sub = self.create_subscription(
            Float32MultiArray,
            "/input/rc",
            self.rc_callback,
            10,
        )

        # Publisher servos
        self.servos_pub = self.create_publisher(
            Float32MultiArray,
            "/output/servos",
            10,
        )

        # Boucle de commande à 50 Hz
        self.timer = self.create_timer(
            1.0 / 50.0,
            self.update,
        )

        self.get_logger().info("RC control node started.")

    # --------------------------------------------------
    # Arrêt du robot
    # --------------------------------------------------

    def stop_robot(self):

        self.center = 0.0
        self.amplitude = 0.0
        self.mass_position = 0.0

    # --------------------------------------------------
    # Lecture des commandes radio
    # --------------------------------------------------

    def rc_callback(self, msg):

        self.last_rc_time = time.monotonic()

        # Vérification du nombre de canaux
        if len(msg.data) < 8:
            self.get_logger().warn("Not enough RC channels.")
            self.armed = False
            self.stop_robot()
            return

        # Récupération des canaux
        channel_5 = msg.data[4]  # Joystick gauche
        channel_6 = msg.data[5]  # Joystick droit
        channel_7 = msg.data[6]  # Interrupteur direction
        channel_8 = msg.data[7]  # Interrupteur propulsion

        # --------------------------------------------------
        # Canal 8 : activation / arrêt
        # --------------------------------------------------

        # Position centrale : propulsion activée
        # Positions extrêmes : arrêt

        if abs(channel_8) < 0.4:

            self.armed = True

        else:

            self.armed = False
            self.stop_robot()
            return

        # --------------------------------------------------
        # Canal 5 : position de la masse
        # --------------------------------------------------

        self.mass_position = max(-1.0, min(1.0, channel_5))

        # --------------------------------------------------
        # Canal 6 : fréquence de pulsation
        # --------------------------------------------------

        x = max(-1.0, min(1.0, channel_6))

        self.frequency = self.frequency_min + (
            (x + 1.0) / 2.0
        ) * (
            self.frequency_max - self.frequency_min
        )

        # --------------------------------------------------
        # Canal 7 : direction
        # --------------------------------------------------

        if channel_7 > 0.5:

            # Droite
            self.center = -0.5
            self.amplitude = 0.5

        elif channel_7 < -0.5:

            # Gauche
            self.center = 0.5
            self.amplitude = 0.5

        else:

            # Tout droit
            self.center = 0.0
            self.amplitude = 1.0

    # --------------------------------------------------
    # Boucle de commande
    # --------------------------------------------------

    def update(self):

        # Vérification de la réception radio
        if self.last_rc_time is None:

            self.stop_robot()
            self.armed = False

        elif time.monotonic() - self.last_rc_time > self.rc_timeout:

            self.stop_robot()
            self.armed = False

        # Calcul de la commande de propulsion
        if self.armed:

            t = time.monotonic() - self.start_time

            fin_command = self.center + self.amplitude * sin(
                2.0 * pi * self.frequency * t
            )

        else:

            fin_command = 0.0
            self.mass_position = 0.0

        # Publication des commandes
        msg = Float32MultiArray()

        msg.data = [
            float(fin_command),
            float(self.mass_position),
        ]

        self.servos_pub.publish(msg)


def main():

    rclpy.init()

    node = RcControlNode()

    try:
        rclpy.spin(node)

    except KeyboardInterrupt:
        pass

    finally:
        node.stop_robot()

        # Publication d'une dernière commande neutre
        msg = Float32MultiArray()
        msg.data = [0.0, 0.0]
        node.servos_pub.publish(msg)

        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()