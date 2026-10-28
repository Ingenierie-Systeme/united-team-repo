# Lisibilité et cohérence du modèle V1

## Nettoyage du 22 octobre 2026

Les 12 scripts de simulation et les quatre validateurs ont été remis en forme :
blocs de méthodes développés, instructions séparées, indentation et lignes aérées.
Les attributs Header ont été retirés. Les incertitudes physiques restent dans des
commentaires. Les modificateurs private redondants des membres de classe ont été
retirés ; les public nécessaires aux paramètres sérialisés et aux appels entre
composants sont conservés. Les accesseurs private set restent nécessaires pour
empêcher une modification externe des diagnostics.

Ce nettoyage ne change pas intentionnellement les lois physiques, les noms des
paramètres sérialisés, les commandes clavier ni les réglages des scènes.
Le nombre minimal de lignes n'est plus un objectif : la lisibilité prime.
Sauvegarde antérieure : Backups/before-readable-format-20261022.tar.gz.

## Ce qui est réellement simulé

- Unity intègre un seul Rigidbody, avec sa gravité, les collisions et les forces.
- La flottabilité est évaluée par échantillonnage du volume immergé ; sa
  répartition permet les moments de rappel. La palme est représentée par son
  volume rectangulaire nominal, pas par une déformation du fluide.
- Les traînées linéaires, quadratiques et angulaires sont des forces et couples
  résistants dans les axes du corps, pondérés par l'immersion.
- La masse coulissante conserve la masse totale et déplace le centre de masse.
  L'inertie est nominale et les réactions internes sont négligées conformément
  à l'approximation quasi statique V1.
- Le servo anime la palme. Un autre composant applique les efforts MOYENS :
  T = kT (A f)^2 et Y = kY (A f)^2 delta0, au point arrière fixe. Le moment
  découle de cette application, sans ajout d'un second couple de lacet.
- Gerstner fournit une géométrie et une hauteur de surface partagées par le
  rendu et la submersion, avec inversion du déplacement horizontal. Il ne
  fournit aucun courant orbital sous-marin.

## Limites importantes

Il ne s'agit pas d'une simulation fluide/palme résolue : le maillage oscillant
ne déplace pas réellement un volume d'eau calculé. Son animation représente le
mécanisme auquel la loi moyenne de propulsion se substitue. Cela est cohérent
avec le référentiel V1, mais ne valide pas expérimentalement le vrai robot.

Les coefficients de propulsion, de traînée, les masses et inerties non mesurées
restent provisoires. L'épaisseur de palme de 0,05 m reste À CONFIRMER.
La poussée moyenne n'est actuellement pas pondérée par l'immersion de la palme :
elle peut donc rester active hors de l'eau. Cette limite n'a pas été corrigée
pendant le travail de lisibilité. La réaction instantanée de chaque battement,
la flexibilité de palme et la turbulence ne sont pas modélisées.

## Vérifications après remise en forme

Compilation Unity 6000.6.5f1 : aucune erreur ni avertissement relevé dans la Console.
ManualPilotTests exécuté en Play : 15 contrôles sur 15 réussis, couvrant masse
avant/arrière, recentrage progressif, masse totale constante, propulsion et arrêt,
virages des deux signes, persistance des commandes, collisions fond/paroi et
immersion partielle Gerstner. Exemples : vitesse en avance 0,233 m/s, poussée
0,487 N ; vitesse de lacet gauche -0,585 rad/s et droite +0,623 rad/s.

Ces tests passent par CaptureCommands : ils vérifient les commandes et la
simulation, pas de nouvelles frappes matérielles sur le clavier. La frappe
physique des touches n'a pas été retestée pendant ce nettoyage. Les autres
scènes de validation n'ont pas été rejouées lors de cette remise en forme.
La scène RobotFishManualDemo a été restaurée, hors Play, à la fin des essais.
