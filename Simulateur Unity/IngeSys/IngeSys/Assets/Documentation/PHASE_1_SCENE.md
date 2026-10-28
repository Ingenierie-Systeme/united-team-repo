# Phase 1 — scène de base et piscine

## Géométrie auditée

Le fichier source `Assets/CAD/Piscine/Piscine.obj` est conservé sans modification.
Son en-tête déclare des mètres. Ses bornes CAD sont `X = [-1, 21]`,
`Y = [-1, 13]`, `Z = [0, 7]`, soit 22 x 14 x 7 m hors tout.
La cavité mesure 20 x 12 x 6 m : `X = [0, 20]`, `Y = [0, 12]`,
`Z = [1, 7]`.

Le CAD est Z-up. Dans la scène Unity Y-up, son instance visuelle reçoit :

- rotation locale X = -90 degrés ;
- translation locale (10, 0, 6) m ;
- échelle (1, 1, 1).

Correction vérifiée dans Unity : l'importeur OBJ inverse X ; le mesh importé
possède les bornes X [-21, 1]. La translation initiale (-10, 0, 6), fondée
seulement sur le fichier brut, décalait le bassin de -20 m par rapport à l'eau.
La translation corrigée (+10, 0, 6) aligne bassin, eau et collisions sans
modifier l'OBJ ni ses réglages d'import. Les sommets transformés en scène
donnent les bornes hors tout X [-11, 11], Y [0, 7], Z [-7, 7].

Après correction, la cavité est centrée horizontalement et occupe :

- X = [-10, 10] m ;
- Y = [1, 7] m ;
- Z = [-6, 6] m.

## Collisions

Le CAD reste exclusivement visuel. Cinq `BoxCollider` statiques matérialisent
le fond et les quatre murs internes. Cette représentation simple évite les
incertitudes de collision liées aux onze objets surfaciques de l'OBJ.

## Eau provisoire

`WaterSurface_Flat_Placeholder` est un plan horizontal de 20 x 12 m.
Son composant `FlatWaterSurface` expose `waterLevel`, initialisé à
`h0 = 6.5 m`, soit 0.5 m sous la margelle. Le plan n'a aucun collider et
n'implémente ni flottabilité, ni courant, ni onde de Gerstner.

## Test phase 1

Le cube dynamique est rangé sous `SimulationRoot/_Tests`. En Play Mode, il
doit traverser le plan d'eau puis reposer sur le fond, avec son centre proche de
Y = 1.5 m. Ce cube n'est pas un élément du futur robot.

## Validation exécutée le 21 octobre 2026

Connexion directe unityMCP à IngeSys, Unity 6000.6.5f1. Scène construite
par les API Unity, sauvegardée à Assets/Scenes/PoolSimulation.unity et
ajoutée à EditorBuildSettings. SampleScene et les sources CAD préservées.

- Capture vérifiée : Captures/phase1_pool_corrected.png, eau dans la cavité.
- Bornes mesurées sur les sommets du mesh en coordonnées monde : 22 x 7 x 14 m
  (X, Y, Z), cavité 20 x 6 x 12 m.
- Eau : bornes X [-10,10], Y 6.5, Z [-6,6], zéro collider.
- Cinq BoxCollider statiques sans Rigidbody attaché vérifiés dans Unity.
- Play Mode exécuté : cube au repos Y=1.500 après traversée de l'eau.
- Essais contre les quatre murs à 5 m/s, gravité temporairement désactivée
  uniquement en Play Mode : centres arrêtés X=+9.50, X=-9.50, Z=+5.50,
  Z=-5.50. Console : contacts fond, est, ouest, nord, sud confirmés.
- Retour en mode édition : les modifications de test du Rigidbody sont
  abandonnées ; cube initial conservé sous _Tests avec gravité activée.
- Compilation Unity réussie. Les avertissements d'API obsolète ont été
  corrigés (FindAnyObjectByType). La Console comporte des avertissements
  d'infrastructure MCP/WebSocket et de fichier temporaire Unity ; ce n'est
  pas une validation « Console vide ».

Fichiers créés : scène et meta, matériau WaterPlaceholder et meta,
Assets/Scripts/Environment/FlatWaterSurface.cs,
Assets/Scripts/Diagnostics/Phase1CollisionProbe.cs,
Assets/Scripts/Editor/Phase1SceneBuilder.cs, leurs meta et dossiers,
ce document et captures avant/après. Réglage modifié :
ProjectSettings/EditorBuildSettings.asset (ajout de la scène).

Limites : eau purement visuelle, h0=6.5 m provisoire ; pas de test de build
exécutable ni de dynamique fluide. Colliders simplifiés pour les faces internes,
pas de reproduction détaillée de tous les triangles extérieurs du CAD.
