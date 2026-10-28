# Phase 2 — RobotFish

Prefab : `Assets/Prefabs/RobotFish.prefab`, instance dans PoolSimulation à
(0,3,0) m, rotation nulle, échelle unité. Repère local : +Z avant, +Y haut.

## Géométrie et paramètres

- Coque cylindrique : longueur 0,40 m, diamètre 0,10 m. Primitive cylindre
  tournée de +90° autour de X : son axe +Y devient l'axe robot +Z.
- Pivot arrière : (0,0,-0,20) m, axe local +Y.
- Palme : longueur Z 0,15 m, hauteur Y 0,10 m, épaisseur X 0,05 m.
  Épaisseur conservée : **À CONFIRMER**, incompatible avec la description fine.
  Centre à Z=-0,275 m, extrémité arrière Z=-0,35 m.
- Masse coulissante : cube visuel 0,035 × 0,035 × 0,04 m, position Z=0.
  Bornes Z=-0,12 et +0,12 m, soit course totale 0,24 m, PROVISOIRE.
- Masse totale 1 kg, masse mobile 0,1 kg : valeurs PROVISOIRES NON MESURÉES.
  La masse mobile est un paramètre de préparation ; aucune dynamique de
  coulissement ni actualisation du CG liée au coulissement à cette phase.
- CoM nominal (0,0,0), référence flottabilité (0,0,0) : PROVISOIRES.
  Cette référence n'est pas un calcul de centre de volume immergé.
- Point moyen de force de palme : centre de la palme, PROVISOIRE.

RobotFishParameters expose ces paramètres et applique les dimensions dans
l'Inspector/OnValidate, à Awake ou par son menu Apply geometry. Le changement
de position de masse est seulement une mise en place visuelle à cette phase.
La masse totale reste indépendante du volume géométrique : pas de neutralité
supposée. Gravité active, aucun effort hydrodynamique.

## Physique et repères visibles

Un Rigidbody sur RobotFish, dynamique, gravité active, contraintes None,
ContinuousDynamic, interpolation. Coque : MeshCollider convexe du cylindre,
pour conserver ses extrémités planes. Palme : BoxCollider. Colliders rattachés
au même Rigidbody. Masse mobile et marqueurs sans collider/Rigidbody.
Inertie issue des colliders Unity, non mesurée et non calibrée.

References contient axe +Z bleu, CoM jaune, référence flottabilité cyan,
bornes magenta. Point de palme vert sous FinPivot. Les marqueurs internes
sont masqués par la coque en Game View ; Scene View avec Gizmos affiche
repères, course, volume filaire de masse et annotations, même à l'intérieur.

## Vérifications exécutées via unityMCP — 21 octobre 2026

- Dimensions mesurées Renderer en scène : coque XYZ=(0,10;0,10;0,40) m,
  palme XYZ=(0,05;0,10;0,15) m.
- Axe cylindre mondial=(0,0,1), pivot local=(0,0,-0,20), axe pivot=(0,1,0).
- Un seul Rigidbody ; constraints=None ; les deux colliders attachés à RobotFish.
- Play Mode : repos à Y≈1,05 m, bas de coque Y=0,999909 m sur fond Y=1 m,
  vitesse nulle. Le cube de phase 1 était encore actif lors de cet essai et
  a perturbé la position X du robot ; il est désormais désactivé sous _Tests.
- Essai paroi est : lancement à 3 m/s depuis X=9, Y=4 ; arrêt/rebond près
  de X=9,92, avec rotation libre. Aucun verrouillage de rotation ajouté.
- Masse visuelle placée à sa borne avant/arrière pendant les vérifications ;
  position minimale Z=-0,12 m confirmée. Les changements en Play Mode sont
  abandonnés en sortie.
- Capture : `Captures/phase2_robot_geometry.png`.

## Inconnues à mesurer ou confirmer

Masse totale réelle ; masse mobile et masse fixe ; dimensions de la masse
mobile ; course et butées mécaniques ; vitesse de coulissement ; CG des
pièces et CG assemblé ; inertie ; centre de volume et centre de flottabilité
selon immersion ; intersections/volume du cardan et de la palme ; épaisseur
de palme ; position/axe exacts du servo ; point effectif des futurs efforts.
Les limites de course ne sont pas automatiquement déduites de la mécanique.
Pas de test d'exécutable compilé ni de calibration expérimentale.

## Fichiers

Créés : RobotFishParameters.cs, RobotFishPrefabBuilder.cs, RobotFish.prefab,
matériaux Robot_*.mat, meta associés, ce rapport et capture.
Modifiée : PoolSimulation.unity (instance RobotFish et cube de test désactivé).
Ni propulsion, ni eau animée, ni régulateur implémentés.
