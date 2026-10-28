# Phase 6 — pilotage manuel intégré

## Lancer la démonstration

Ouvrir `Assets/Scenes/RobotFishManualDemo.unity`, lancer Play, cliquer dans
Game View pour donner le focus au clavier. La scène est en tête des Build
Settings. Robot à (0,4,0) m, complètement immergé, propulsion OFF, STRAIGHT,
s=0. Caméra rapprochée en suivi, HUD et rappel des touches visibles.
Le prefab physique, CAD, colliders et Gerstner existants sont réutilisés.
Les scènes des phases précédentes sont conservées.

Active Input Handling est passé de Input System Package (1) à Both (2),
dans ProjectSettings/ProjectSettings.asset. Appel direct de Input.GetKey
via Unity MCP réussi après changement, sans exception. Aucun package ajouté.

## Commandes

- Haut/Bas maintenus : variation de la consigne s à 0,05 m/s, bornée à
  [-0,12;+0,12] m. Relâchement : consigne conservée. Les deux ensemble : maintien.
- R sur appui : cible centrale s0=0 m, prioritaire au prochain pas physique ;
  aucun déplacement instantané de la masse. Flèches de nouveau actives ensuite.
- Z sur appui : ON/OFF, utilisant Input.GetKeyDown(KeyCode.Z). OFF coupe
  immédiatement les forces et ramène progressivement le servo au neutre,
  sans freiner artificiellement le Rigidbody.
- Gauche/Droite sur appui : sélection persistante TURN_LEFT/TURN_RIGHT ;
  second appui identique -> STRAIGHT ; opposé -> remplacement ; les deux
  maintenus ensemble -> mode conservé. Le mode reste mémorisé propulsion OFF.

RobotKeyboardInput lit Input.GetKey/GetKeyDown en Update, mémorise les
événements et écrit uniquement les consignes des composants en FixedUpdate.
Ordre d'exécution -200 avant SlidingMass -100 et FinServo -60. Aucun
AddForce/AddTorque, changement de Transform/Rigidbody du corps dans ce script.
CaptureCommands est le point d'entrée commun aux essais automatisés et à la
lecture clavier. readKeyboard=false sert seulement aux essais reproductibles.
Perte de focus : arrêt de l'incrément de masse, consignes déjà demandées conservées.

## Paramètres et conventions

A nominal=20° (0,34906585 rad), f=1 Hz ; constantes pendant le pilotage initial.
Modes de virage : offset cible=+A pour gauche, -A pour droite avec kY positif,
signes inversés si kY négatif. Transition d'offset limitée à 0,7 rad/s.
Limitation cohérente d'A pour respecter angleMax/2, offsetMax, amplitudeMax
et vitesseMax/(2*pi*f) ; A nominal n'est pas dépassée lors d'un virage.
Pour les paramètres par défaut : STRAIGHT -20°..+20°, LEFT 0°..40°,
RIGHT -40°..0°, après transition. Phase continue, conservée entre ON/OFF.
FinServo garde son limiteur de vitesse pour les transitions et le retour neutre.

SlidingMass conserve le calcul de CG et l'inertie nominale V1. Ajout d'une
constante de temps de 0,15 s (éditable, 0 désactive ce lissage) ; déplacement
par pas=min(|erreur|*(1-exp(-dt/tau)), vitesseMax*dt). Vitesse réelle max
0,05 m/s. Masse mobile 0,5 kg, masse totale théorique neutre 3,891593 kg,
CG fixe, inertie et coefficients des phases précédentes restent provisoires.

Repère Unity : force locale (Ylatéral,0,T), bras arrière Z négatif,
moment local de lacet (r×F).y négatif pour force +X. Le HUD calcule ce moment
pour l'afficher uniquement ; aucune seconde application de couple.
Pitch positif = nez qui pique (rotation +X), yaw positif = nez vers +X.
Profondeur moyenne=h0-position.y ; immersion locale=hauteurGerstner(XZ)-position.y.
Le bilan vertical affiché est Archimède-poids, sans inclure traînée/propulsion.

## HUD et observation

RobotPilotHUD affiche ON/OFF, mode, A/f/offset effectifs, angle servo,
s/cible, position, profondeurs moyenne/locale, vitesse longitudinale,
pitch/yaw, vitesse de lacet corps, T/Y/moment de palme, volume et fraction
immergés, Archimède et bilan vertical. IMGUI natif, pas de logs par frame.
RobotFollowCamera ne modifie que la caméra ; évitement des colliders statiques
par SphereCast et rapprochement devant les murs. Pas de régulation du robot.

## Validation

`Assets/Scenes/ManualPilotTests.unity` exécute ManualPilotValidation en Play.
Les consignes sont injectées via CaptureCommands, pas par des touches natives.
Les appuis physiques clavier et le comportement du clavier OS/AZERTY dans
Game View sont **non vérifiés** par cette automatisation. Le lecteur natif
est présent et Input.GetKey a été exécuté sans exception ; un appui maintenu
Z ne rebascule pas par construction (GetKeyDown), mais le maintien physique
Z reste **non vérifié**. Aucun outil MCP d'injection clavier n'est disponible.

Un premier test R avait attendu deux pas à cause d'un arrondi flottant de
0,02/dt, mais utilisait la tolérance d'un pas : échec du test, pas de téléportation
(s=0,118 au lieu de 0,120 m, soit 2 mm). Le test utilise désormais le temps
physique écoulé et les délais sont arrondis au pas fixe le plus proche.

Les résultats finaux sont consignés dans PHASE_6_RESULTS.txt.

Quinze contrôles finaux PASS dans Unity : masse avant/arrière, maintien aux
deux flèches, recentrage sans saut, retour au centre et masse totale constante,
activation droite, coupure/décélération, gauche, deux directions simultanées,
inversion à droite, second appui droite vers STRAIGHT, manœuvre combinée et
mémoire après OFF/ON, collisions fond/paroi, submersion Gerstner partielle.
Avance : 0,2328017 m/s après 3 s, T=0,4873878 N ; après coupure et 2 s :
0,1169761 m/s. Lacet gauche -0,5847576 rad/s, inversion droite +0,6229298 rad/s.
La manœuvre combinée conserve s≈0,02999 m et une assiette non nulle.
Fond : bas de coque=1,000007 m ; paroi : Xmax=9,999898 m.
Gerstner partiel : volume entre 0,001955979 et 0,003891563 m³.

Après chargement de la démo, Input.GetKey et Input.GetKeyDown ont également
été appelés en Play Mode sans exception ; readKeyboard=true, un Rigidbody,
gravité active et référence d'eau confirmés. Le HUD et la vue rapprochée sont
visuellement vérifiés dans Captures/phase6_gameview_hud.png (capture native
ScreenCapture, incluant IMGUI et rappel des touches).

Console : zéro entrée Error pendant la seconde campagne automatisée et avant
la capture MCP de démo. Cette capture a ensuite généré cinq erreurs internes
« PlayerLoop ... called recursively ». La pile désigne le package MCP,
ScreenshotUtility.CaptureCompositedAfterFrame ligne 197, pas les scripts de
simulation. La capture native asynchrone fournit le HUD complet. La Console
n'est donc pas laissée vide ; pas de suppression de l'historique utilisateur.
Les erreurs Temp/FSTimeGet des phases antérieures ne se sont pas reproduites
pendant cette seconde campagne. Éditeur laissé hors Play sur la démo sauvegardée.

## Fichiers

Créés : Robot/RobotKeyboardInput.cs, Diagnostics/RobotPilotHUD.cs,
Diagnostics/RobotFollowCamera.cs, Diagnostics/ManualPilotValidation.cs,
Editor/ManualDemoSetup.cs, RobotFishManualDemo.unity, ManualPilotTests.unity,
documentation, relevé et captures (avec meta Unity).
Modifiés : Robot/SlidingMass.cs, ProjectSettings/ProjectSettings.asset (Both),
ProjectSettings/EditorBuildSettings.asset (scène de démo en tête).
Composants de pilotage ajoutés à l'instance de démo, pas au prefab partagé,
pour préserver les autres bancs d'essai.

## Limites et anomalies

Masses réelles, course, CG, inertie, traînées et kT/kY non identifiés.
De fortes assiettes sont possibles avec la course provisoire et peuvent
modifier le sens du lacet mondial par rapport au lacet corps. Il n'y a pas
de stabilisation automatique. Le collider de palme et son volume déplacé
restent moyens, le battement est visuel. La caméra évite les murs mais
peut se rapprocher fortement lorsque le robot touche une paroi.
Gerstner ne simule ni courant orbital, réflexion réelle, turbulence ni impact.
Pas de PID, ROS 2 ou validation expérimentale. Pas de build exécutable testé.
