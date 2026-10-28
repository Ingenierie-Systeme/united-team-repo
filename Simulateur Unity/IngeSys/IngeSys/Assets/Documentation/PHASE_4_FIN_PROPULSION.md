# Phase 4 — servo visuel et propulsion moyenne V1

`FinServo` expose animate, amplitudeRad A, frequencyHz f et offsetRad delta0
dans l'Inspector. Phase intégrée à partir de f dans FixedUpdate (pas de saut
de phase lors d'un changement de fréquence). Consigne : delta0+A*sin(phase).
Rotation visuelle autour du +Y local du pivot arrière. Angles internes en rad,
conversion en degrés seulement pour Quaternion.AngleAxis.

Limites toutes PROVISOIRES : Amax=0,6 rad ; |offset|max=0,4 rad ; fmax=3 Hz ;
|angle|max=0,8 rad ; vitesse maximale=6 rad/s. Valeurs effectives affichées.
A est réduit pour respecter |offset|+A<=angleMax et 2*pi*f*A<=vitesseMax.
MoveTowards limite aussi les transitions d'offset/consigne à vitesseMax.
Les valeurs négatives d'A ou f sont ramenées à zéro. Une fréquence nulle
donne une propulsion nulle. animate=false ramène visuellement vers zéro et
coupe également les efforts moyens.

`MeanFinPropulsion` expose propulsionEnabled (false par défaut), kT et kY.
Valeurs par défaut kT=4, kY=4 : **PROVISOIRES NON IDENTIFIÉS**.
Avec A en rad et f en Hz, unités formelles kT=N.s²/rad² et kY=N.s²/rad³.
T=kT*(Aeffectif*feffectif)² ; Y=kY*(Aeffectif*feffectif)²*offsetEffectif.
Une seule force (Y,0,T) dans les axes corps est appliquée avec
Rigidbody.AddForceAtPosition, au point moyen fixe local (0,0,-0,275) m
pour les dimensions nominales. Aucun couple de lacet ajouté séparément.
propulsionEnabled=false met immédiatement T et Y à zéro ; les traînées
et la flottabilité continuent et l'animation peut continuer.

## Absence de deuxième effort de battement

Le BoxCollider initial de Fin_Rectangular est retiré du visuel. Un BoxCollider
neutre, frère du pivot et fixe dans le repère corps, conserve les dimensions
0,05 × 0,10 × 0,15 m et le même Rigidbody. Aucun collider sur le pivot/mesh
animé, aucun joint, second Rigidbody ou intégration parallèle.
Le volume de palme reste inclus dans Archimède, une seule fois, mais ses
échantillons restent dans la pose moyenne. Le point de force et son marqueur
restent également dans la pose moyenne. Le battement ne modifie donc ni la
géométrie de collision, ni le centre du volume déplacé, ni l'inertie nominale.
La géométrie physique est une approximation moyenne ; la palme visible peut
visuellement pénétrer un mur avant son collider neutre.

## Essais reproductibles

Ouvrir Assets/Scenes/FinPropulsionTests.unity et lancer Play Mode.
FinPropulsionValidation crée cinq robots successifs depuis le prefab,
neutres à Y=4 m, eau plate à h0=6,5 m, A=0,35 rad, f=1 Hz, s=0.
Chaque cas dure 200 pas fixes de 0,02 s, environ 4 s.
Cas : propulsion coupée mais palme animée ; avance symétrique ; offset
+0,2 rad ; offset -0,2 rad ; avance puis coupure à t≈2 s.
Results et finished sont affichés sur le runner ; tableau et PASS/FAIL
dans la Console. Les mesures sont consignées dans PHASE_4_RESULTS.csv.

Pour kY positif et force latérale +X appliquée à l'arrière (Z négatif),
le moment Y est négatif : offset positif attendu -> nez vers -X (gauche),
offset négatif -> nez vers +X (droite). Le lacet est mesuré par
atan2(forward.x,forward.z), positif vers +X. Le signe de kY reste éditable.

## Résultats observés — 21 octobre 2026

Cinq PASS en Play Mode via unityMCP. Arrêt avec animation : vitesse finale
1,4e-5 m/s, lacet nul. Avance : T=0,49 N, déplacement +Z=0,671509 m,
vitesse=0,263572 m/s, lacet nul. Offset positif : Y=+0,098 N,
lacet=-1,075236 rad (-61,61°), vitesse de lacet=-0,379568 rad/s, gauche.
Offset négatif : mêmes modules, signes de X/lacet opposés, droite.
Après coupure à mi-essai : T=Y=0 et vitesse finale=0,096180 m/s ; PASS
du critère vitesse inférieure à la vitesse au moment de la coupure.
Le pic servo observé vaut 0,549309 rad pour les cas décentrés ; vitesse
maximale observée 6 rad/s (transition d'offset initiale).

Test de saturation des commandes A=f=offset=99 : valeurs effectives
A=0,3183099 rad, offset=0,4 rad, f=3 Hz et dérivée maximale=6 rad/s.
Vitesse servo limite nulle -> Aeffectif=0. Inspection Unity : zéro collider
sous le pivot animé, un seul Rigidbody sur le robot. Compilation réussie ;
la Console conserve une erreur interne Unity Temp/FSTimeGet.

## Limites du modèle

Modèle phénoménologique non identifié expérimentalement ; aucune validation
des coefficients par Betton. Les efforts dépendent des commandes effectives
bornées, pas d'un calcul hydrodynamique des angles réellement réalisés lors
d'une transition rapide. Hypothèse de fonctionnement immergé ; pas de
réduction automatique de propulsion hors eau. Pas de souplesse de palme,
réaction mécanique du servo, écoulement, CFD, PID ou ROS 2.

Fichiers créés : Robot/FinServo.cs, Robot/MeanFinPropulsion.cs,
Editor/FinPropulsionSetup.cs, Diagnostics/FinPropulsionValidation.cs,
FinPropulsionTests.unity, documentation et relevé avec meta associés.
Fichiers modifiés : RobotFish.prefab, RobotFishParameters.cs (dimensions du
collider moyen et marqueur), CalmWaterBuoyancy.cs (pose moyenne de palme),
PoolSimulation.unity si overrides ou références de prefab actualisés.
