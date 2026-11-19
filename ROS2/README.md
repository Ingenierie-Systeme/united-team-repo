# IMU Unity → ROS2

La scène `RobotFishKatarstique` et le prefab `RobotFish` possèdent un composant
`IngeSys.Robot.RobotImuPublisher`. Le connecteur ROS TCP 0.7.0-preview est embarqué
sous `Packages/com.unity.robotics.ros-tcp-connector`, copié depuis le cache local
d’« Exercice 2 », avec sa licence. Aucun téléchargement n’est nécessaire.
Le symbole de compilation `ROS2` est activé pour Standalone.

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

## Capteur de profondeur / baromètre immergé

Le composant **Robot Depth Publisher** est attaché au prefab `RobotFish` comme
l’IMU et apparaît sur le robot de la scène. Il publie à 20 Hz :

- `/barometer/pressure` : `sensor_msgs/msg/FluidPressure`, pression absolue en Pa,
  frame `barometer_link`, horodatage UTC système, variance inconnue (0).
- `/depth` : `std_msgs/msg/Float64`, profondeur immergée en mètres, positive vers
  le bas, sans horodatage ni frame dans ce type de message.

La pression vaut `P_atm + rho * abs(gravity.y) * depth`. La pression atmosphérique
par défaut est 101325 Pa. La densité et la surface d’eau viennent du composant
`CalmWaterBuoyancy` du même robot (1000 kg/m³ par défaut). La mesure utilise
`FlatWaterSurface.GetHeightAtWorldXZ`, donc la même hauteur que la flottabilité :
Gerstner si son couplage physique est actif, sinon niveau moyen, y compris dans
les galeries inondées configurées. Hors du domaine d’eau valide, aucune mesure
n’est publiée. Au-dessus de l’eau : profondeur 0 et pression atmosphérique ; la
variation de pression dans l’air n’est pas modélisée.

Le point capteur est l’origine du robot par défaut ; `Sensor Local Position`
permet de le décaler en mètres dans le repère Unity du corps. Le capteur ne
modifie pas la physique. Aucun bruit, biais, pression dynamique, effet thermique
ou TF n’est ajouté. Pour un robot réel, les nodes peuvent dériver la profondeur
avec `(pression - pression_surface) / (rho * g)`, après calibration. La profondeur
simulée est ici une référence idéale pour vérifier ces algorithmes.
