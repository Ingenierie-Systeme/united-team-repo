# IMU Unity → ROS2

La scène `RobotFishKatarstique` et le prefab `RobotFish` possèdent un composant
`IngeSys.Robot.RobotImuPublisher`. Le connecteur ROS TCP 0.7.0-preview est embarqué
sous `Packages/com.unity.robotics.ros-tcp-connector`, copié depuis le cache local
d’« Exercice 2 », avec sa licence. Aucun téléchargement n’est nécessaire.
Le symbole de compilation `ROS2` est activé pour Standalone.

## Connexion et vérification

Le serveur ROS-TCP-Endpoint ROS2 est inclus sous `ROS2/src/ROS-TCP-Endpoint`,
copié depuis les sources locales existantes, sans historique Git. Depuis la racine
du dépôt, construire une fois :

```bash
source /opt/ros/jazzy/setup.bash
cd ROS2
colcon build --symlink-install --packages-select ros_tcp_endpoint
cd ..
```

Puis lancer le serveur depuis la racine du dépôt :

```bash
source /opt/ros/jazzy/setup.bash
source ROS2/install/local_setup.bash
ros2 run ros_tcp_endpoint default_server_endpoint --ros-args -p ROS_IP:=127.0.0.1 -p ROS_TCP_PORT:=10000
```

Ouvrir la scène `RobotFishKatarstique`, puis lancer Play. Le connecteur utilise
par défaut `127.0.0.1:10000` ; pour une autre machine, configurer Robotics → ROS
Settings et l’adresse d’écoute du serveur. Dans un second terminal, à la racine
du dépôt :

```bash
source /opt/ros/jazzy/setup.bash
python3 ROS2/check_imu.py
ros2 topic echo /imu/data sensor_msgs/msg/Imu
```

Les futurs nodes de contrôle peuvent rester dans un workspace distinct : ils
s’abonnent au même topic standard, sans dépendance aux scripts Unity. Aucun topic
de commande n’est ajouté ici.

## Contrat du capteur

- Topic `/imu/data`, type `sensor_msgs/msg/Imu`, frame `imu_link`.
- Fréquence demandée 20 Hz, éditable dans l’Inspector ; cadence limitée par la
  physique, quantifiée aux pas `FixedUpdate` (20 ms par défaut).
- IMU idéale placée à l’origine du Rigidbody, axes alignés sur le corps.
- Repère ROS FLU : X avant, Y gauche, Z haut ; Unity : Z avant, X droite, Y haut.
- Quaternion complet relatif au monde Unity converti en FLU (monde ROS :
  X = Unity +Z, Y = Unity −X, Z = Unity +Y). Aucune référence géographique.
- Vitesse angulaire locale en rad/s, avec conversion des vecteurs axiaux.
- Accélération spécifique locale en m/s² : dérivée de la vitesse du point
  capteur moins la gravité ; au repos horizontal Z ≈ +9,81, chute libre ≈ 0.
- Horodatage UTC système ; `/clock` et `use_sim_time` ne sont pas implémentés.
  La pause Unity arrête la publication ; la fréquence réelle dépend de timeScale.
- Covariances nulles = inconnues ; aucun bruit, biais ou étalonnage matériel.
- Pas de TF publié : le node intégrateur doit fournir la transformation fixe
  `base_link → imu_link` conforme à son robot. Un seul robot doit utiliser ce
  topic/frame ; modifier ces champs avant d’ajouter plusieurs robots.

Le calcul est échantillonné à chaque pas physique, avant décimation. La première
mesure est ignorée pour éviter une accélération artificielle au démarrage.
Une téléportation du Rigidbody peut produire un pic ; désactiver/réactiver le
composant après une réinitialisation de la simulation.
