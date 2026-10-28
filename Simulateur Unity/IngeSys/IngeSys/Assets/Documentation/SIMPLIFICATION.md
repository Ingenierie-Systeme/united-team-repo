# Nettoyage du 22 octobre 2026

La scène à utiliser reste `Assets/Scenes/RobotFishManualDemo.unity`.
Le réglage d'entrée présent avant nettoyage est conservé : Both,
`activeInputHandler: 2` (également vérifié dans la sauvegarde initiale).
Pas de modification des touches, des masses,
des coefficients physiques, des ondes ou du CAD.

## Organisation conservée

- Environment : FlatWaterSurface (niveau/emprise/API), GerstnerSurface
  (ondes et inversion), GerstnerMesh (rendu).
- Robot : RobotFishParameters, CalmWaterBuoyancy, BodyWaterDrag, SlidingMass,
  FinServo, MeanFinPropulsion, RobotKeyboardInput.
- Diagnostics : RobotPilotHUD et RobotFollowCamera.
- Tests/Simulation : quatre bancs de validation existants. Ils restent
  associés aux scènes de test par leurs GUID, sans intervenir dans la démo.

## Suppressions

Sept scripts et leurs meta supprimés : Phase1SceneBuilder,
RobotFishPrefabBuilder, CalmWaterSetup, FinPropulsionSetup, ManualDemoSetup,
NativeKeyboardProbe, Phase1CollisionProbe. Les six générateurs/outils Editor
ne sont plus nécessaires aux scènes et au prefab déjà construits. La sonde
de collision temporaire a été retirée des six scènes qui la référençaient
avant suppression, afin de ne laisser aucun Missing Script.

Retirés du runtime : compteurs/événements de dépannage clavier, OnGUI de
RobotKeyboardInput, ancien panneau de flottabilité redondant avec le HUD,
champs de diagnostic jamais lus, méthode Configure sans appelant, wrappers
Step inutilisés, recalcul redondant des limites du servo dans la propulsion,
commentaires de code désactivé et en-têtes purement décoratifs.
Les méthodes internes du maillage et de préparation sont privées.

Les mentions PROVISOIRE/NON IDENTIFIÉ et À CONFIRMER, les bornes de paramètres,
RequireComponent et l'ordre des composants sont conservés : ils décrivent
les incertitudes physiques ou empêchent une configuration invalide.
La phase est toujours continue, la masse conserve sa vitesse limitée, le
collider de palme reste indépendant de son animation, le temps Gerstner et
l'inversion restent communs au rendu et à la flottabilité. Un seul Rigidbody.
Les Gizmos et repères géométriques restent disponibles.

Le HUD personnalisé « United TEAM » et son contenu visible sont préservés ;
seules ses lignes de code commentées liées au dépannage ont été retirées.
Les autres ressources préexistantes et documents historiques sont conservés.

## Taille et récupération

Avant : 23 scripts, 1 526 lignes. Après : 16 scripts, 786 lignes,
dont 12 scripts de simulation et 4 bancs de test. Réduction de 740 lignes
(environ 48,5 %), sans fusionner les calculs physiques indépendants.

Sauvegarde avant nettoyage :
`Backups/before-simplification-20261022.tar.gz` (racine du projet), comprenant
scripts/meta, scènes, prefab et réglages Player/Build. Les suppressions sont
donc récupérables depuis cette archive.

## Vérifications

Compilation Unity : zéro erreur et zéro avertissement au premier contrôle.
Sept scènes et RobotFish.prefab inspectés : zéro Missing Script.
Quatre campagnes Play Mode réellement exécutées après nettoyage : 31 PASS.
Les appuis clavier physiques ne sont pas réinjectés par ce nettoyage ; le
lecteur UnityEngine.Input et le réglage Player initial sont conservés.

Pilotage/intégration : 15 PASS (masse avant/arrière, recentrage, course,
masse constante, ON/OFF, deux signes de lacet, transitions, manœuvre combinée,
collisions fond/paroi, immersion Gerstner). Avance : 0,2328017 m/s ; après
coupure : 0,1169761 m/s. Lacet gauche -0,5847576 et droite +0,6229298 rad/s.
Propulsion : 5 PASS ; T=0,49 N, Y=±0,098 N et lacets opposés ±1,075236 rad.
Gerstner : 4 PASS ; amplitude zéro exacte, inversion sans échec sur la grille
spatio-temporelle, erreur hauteur <=2,861023e-6 m, aucune sortie du domaine ;
poussée entièrement immergée min=max=38,17623 N, variation en immersion partielle.
Les oscillations en immersion partielle restent une limite des paramètres
provisoires, pas un objectif de stabilisation ajouté pendant ce nettoyage.

Eau calme : 7 PASS (neutralité, masse excessive, moitié immergé,
décélération, masse avant/arrière, rappel après inclinaison).
Neutralité : Y=4,00005 m et vitesse≈0,00001 m/s ; décélération 1→0,21947 m/s.
Les scènes de simulation et le prefab ont été resérialisés par Unity pour éliminer leurs
champs supprimés, puis les sept scènes ont été contrôlées à nouveau.
Éditeur laissé hors Play Mode sur RobotFishManualDemo, sans exécution
automatique d'un banc d'essai dans cette scène. Build exécutable non testé.

Le contrôle de SampleScene a révélé un avertissement de version ancienne de
Light (8 au lieu du minimum 9). Cette scène préexistante, hors simulation,
a été restaurée à l'identique depuis la sauvegarde : pas de modernisation
inutile de cet asset. L'avertissement est distinct des scripts compilés.
