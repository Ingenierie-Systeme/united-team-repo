import cv2
import rclpy

from rclpy.node import Node
from sensor_msgs.msg import Image

# Sert à convertir une image OpenCv en message ROS2
from cv_bridge import CvBridge


class CameraPublisher(Node):

    def __init__(self):
        super().__init__('cam')

        # Publisher
        self.publisher_ = self.create_publisher(
            Image,
            '/camera/image_raw',
            10
        )

        # Conversion OpenCV - ROS 2
        self.bridge = CvBridge()

        # Ouverture de la caméra
        self.cap = cv2.VideoCapture('/dev/video0')

        if not self.cap.isOpened():
            raise RuntimeError(
                "Impossible d'ouvrir la caméra /dev/video0"
            )

        # Timer : acquisition et publication périodiques
        self.timer = self.create_timer(
            1.0 / 30.0,
            self.publish_image
        )

        self.get_logger().info(
            'Publisher camera démarré'
        )

    def publish_image(self):

        # Récupérer une image depuis OpenCV
        ret, frame = self.cap.read()

        if not ret:
            self.get_logger().warning(
                "Impossible de récupérer une image"
            )
            return

        # Convertir l'image OpenCV en message ROS 2
        msg = self.bridge.cv2_to_imgmsg(
            frame,
            encoding='bgr8'
        )

        # Ajouter un horodatage
        msg.header.stamp = self.get_clock().now().to_msg()

        # Publier le message
        self.publisher_.publish(msg)


def main(args=None):

    rclpy.init(args=args)

    node = None

    try:
        node = CameraPublisher()
        rclpy.spin(node)

    except (RuntimeError, KeyboardInterrupt) as error:
        if isinstance(error, RuntimeError):
            print(f"Erreur caméra : {error}")

    finally:
        if node is not None:
            node.cap.release()
            node.destroy_node()

        if rclpy.ok():
            rclpy.shutdown()


if __name__ == '__main__':
    main()