# Phase 5 — surface Gerstner et submersion locale

Référence : NVIDIA GPU Gems, chapitre 1, Mark Finch, section Gerstner Waves :
https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-1-effective-water-simulation-physical-models
Déplacements horizontaux et raideur issus de cette famille de modèles ;
adaptation aux repères Unity et à un domaine de piscine borné.

## Architecture

FlatWaterSurface conserve h0=6,5 m, l'emprise 20×12 m et les liens existants.
Son API GetHeightAtWorldXZ délègue à GerstnerSurface si coupleBuoyancy=true.
GerstnerSurface centralise ondes, temps physique et inversion. GerstnerMesh
est le seul producteur du maillage/rendu. CalmWaterBuoyancy échantillonne
uniquement l'API locale ; toutes les autres forces V1 restent inchangées.
Couplage activé seulement après un contrôle du rendu seul dans Unity.

Somme par onde i : theta=k*d.q-omega*t+phase, k=2*pi/lambda ;
Y=h0+E(q)*sum(A*sin(theta)) ; XZ=q+E(q)*sum(Q*A*d*cos(theta)).
Direction normalisée, longueur d'onde bornée à 0,1 m minimum, A>=0, Q dans
[0,1]. omega=sqrt(|gY|*k) en mode dispersion eau profonde, ou k*speedMps.
Le mode eau profonde est une hypothèse de dispersion visuelle, pas une
prédiction du comportement des vagues dans ce bassin à profondeur finie.

Preset 3 ondes : amplitudes (0,08;0,04;0,02) m ; longueurs (3;1,7;0,9) m ;
directions (1;0,3),(-0,4;1),(0,7;-1), normalisées ; phases (0;1;2) rad ;
raideurs (0,6;0,5;0,3), dispersion eau profonde activée.

E(q)=sin²(pi*u)*sin²(pi*v), u/v coordonnées normalisées dans le bassin,
zéro hors domaine. Cette atténuation artificielle immobilise le bord et
n'est pas une réflexion de houle. La somme des amplitudes est plafonnée à
rimHeight-h0-margin, avec margelle Y=7 m et marge 0,05 m. Une borne conservative
de la norme du gradient horizontal sum(Q*A*(k+pi/Lx+pi/Lz)) est limitée à
0,65 par réduction des déplacements horizontaux. Avec le bord fixe, le
mapping reste inversible et dans le rectangle ; aucune surface liquide ni
force de flottabilité n'est évaluée hors domaine couplé.

## Inversion et synchronisation

Pour un XZ mondial fixé, résolution q <- q-(XZ(q)-XZcible), au plus 24 tours
(paramètre borné 4..64), tolérance 1e-5 m. Résidu et compteur d'échecs exposés.
TryGetHeightAtWorldXZ renvoie false en dehors ou en cas de non-convergence ;
GetHeightAtWorldXZ retourne alors -Infinity, donc submersion nulle, sans
substitution silencieuse par une sinusoïde verticale ou une hauteur plate.

Horloge unique : Time.fixedTimeAsDouble, utilisée en FixedUpdate pour la
flottabilité et en LateUpdate pour le rendu. Pas d'interpolation temporelle
entre pas : rendu volontairement synchronisé au dernier instant physique.
En édition, aperçu à t=0. EvaluateParametric(q,t) et TryGetHeightAtWorldXZ
acceptent un temps explicite pour les vérifications.

Maillage CPU 160×96 cellules, 15 617 sommets, normals recalculées, matériau
Standard transparent existant, Built-in Unity 6000.6.5f1. Pas de package ni de
changement de pipeline. Le maillage runtime est reconstruit au chargement,
sans remplacer d'asset source. Aucun collider. Les triangles interpolent la
surface paramétrique ; l'écart maximal entre triangles n'est pas certifié.

## Vérifications exécutées via unityMCP

1. Rendu seul : couplage false, contrôle de sommets échantillonnés, erreur
hauteur maximale 1,907349e-6 m, zéro collider, aucun sommet hors bassin.
2. Scène GerstnerTests : amplitude nulle, erreur=0 ; 11 instants de 0 à 20 s
sur une grille 41×25 : erreur hauteur maximale 2,861023e-6 m, résidu horizontal
maximal 9,991559e-6 m, zéro échec d'inversion, dépassement horizontal=0 m,
hauteur maximale=6,632424 m < margelle 7 m.
3. Play Mode, corps neutre entièrement immergé, propulsion désactivée :
poussée min=max=38,17623 N ; vitesse maximale=1,369417e-5 m/s ; Yfinal=4,000073.
Pas de force de vague supplémentaire détectée.
4. Masse demi-neutre, centre initial à h0, même preset : poussée entre 0 et
38,17623 N, vitesse maximale=1,230992 m/s, Yfinal=6,431738 m. Le corps sort
par moments de l'eau : essai de variation de submersion, pas d'équilibre
stabilisé ni de fidélité expérimentale. Chaque cas physique dure 200 pas,
dt=0,02 s (~4 s). Pas de courant modélisé.
5. Contrôle pendant animation : temps rendu=temps physique=25,299991 s ;
écart sommet/API <=1,907349e-6 m ; compteur d'inversion=0.

Pour reproduire : ouvrir Assets/Scenes/GerstnerTests.unity, lancer Play Mode,
attendre finished sur GerstnerValidation ; résultats aussi dans la Console
avec préfixe PHASE5_RESULTS. Capture : Captures/phase5_gerstner.png.

## Fichiers et limites

Créés : GerstnerSurface.cs, GerstnerMesh.cs, GerstnerValidation.cs,
GerstnerTests.unity, ce rapport, capture et meta. Modifiés : FlatWaterSurface.cs,
CalmWaterBuoyancy.cs, PoolSimulation.unity (composants et couplage).
Les scènes d'essai des phases précédentes conservent leur eau plate.

Pas de courant orbital, d'effort de vague sur un corps totalement immergé,
de houle réfléchie réelle, turbulence, propagation résolue contre les murs,
déferlement, impact hydrodynamique, CFD ou contrôleur. La surface de piscine
est une géométrie paramétrique atténuée aux bords. Le bornage est vérifié pour
le preset et domaine actuels ; h0 doit rester sous le bord. Les performances
CPU n'ont pas fait l'objet d'un profilage ni d'un test de build exécutable.
