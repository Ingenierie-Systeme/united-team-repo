# Phase 3 — physique en eau calme

## Implémentation

`CalmWaterBuoyancy` applique uniquement Archimède avec AddForceAtPosition.
La gravité est appliquée une seule fois par Rigidbody.useGravity. Aucun
intégrateur Newton-Euler supplémentaire. Le niveau vient du composant
FlatWaterSurface lié dans la scène : h0=6,5 m. Eau au repos, vitesse nulle.
Hypothèse : gravité verticale Unity (0,-9,81,0), échelle robot unité.

Volume coque : pi × 0,05² × 0,40 = 0,003141593 m³.
Palme rectangulaire incluse explicitement : 0,15 × 0,10 × 0,05 = 0,00075 m³.
Pas d'intersection volumique avec le cylindre au pivot neutre. Pas de volume
ajouté pour les marqueurs, la masse interne ou un cardan non modélisé.
Total 0,003891593 m³ ; rho=1000 kg/m³ ; masse neutre théorique 3,891593 kg ;
poids et poussée complète théoriques ≈38,1765 N. Ce n'est pas une masse mesurée.

Quadrature distribuée : 12 tranches axiales × 6 anneaux de même aire × 24 angles
pour le cylindre, 4 × 8 × 12 cellules pour la palme. Chaque point porte une part
du volume exact. Immersion locale lissée linéairement sur 4 mm autour du niveau
d'eau ; approximation numérique, pas découpe analytique exacte des cellules.
Les points de palme suivent son Transform. Les forces distribuées produisent
le couple de rappel ; aucun couple artificiel d'assiette ajouté.
Le centroïde immergé, volumes, poids, poussée et masses sont affichés dans
l'Inspector et dans un panneau en Play Mode. Une erreur de quadrature de
quelques 10^-8 m³ est observée à immersion complète (somme float).

`BodyWaterDrag` utilise les vitesses du Rigidbody converties en axes corps.
Par composante : F=-d1*v-d2*abs(v)*v ; couple analogue sur omega en rad/s.
Efforts appliqués au centre de masse. Coefficients XYZ PROVISOIRES :
d1=(2,2,1) N/(m/s), d2=(5,5,2) N/(m/s)² ; amortissement angulaire
a1=(0,05;0,05;0,05) N.m/(rad/s), a2=(0,01;0,01;0,01) N.m/(rad/s)².
Les efforts sont pondérés par Vimmergé/Vtotal. Damping natif Unity nul pour
éviter une seconde traînée. Aucun courant, masse ajoutée ou effort de vague.

`SlidingMass` expose targetPositionM, s et maximumSpeedMps=0,05 m/s.
Course [-0,12;+0,12] m, masse mobile 0,5 kg PROVISOIRES.
Masse fixe = masse totale - masse mobile ; masse totale constante pendant
le coulissement. CG=(mfixe*rfixe+mmobile*(0,0,s))/mtotal.
CG fixe provisoire choisi pour aligner CG total et centre volumique en Z à
s=0, et abaisser CG fixe de 1 cm en Y. Pour le robot neutre : Zfixe≈-0,06083 m.
Inertie nominale V1 constante XYZ=(0,08;0,08;0,006) kg.m², axes principaux
identiques au repère corps, valeurs non mesurées. Réactions inertielles du
coulissement négligées. Aucun deuxième Rigidbody. Le mesh mobile suit s.

## Reproduction

Ouvrir `Assets/Scenes/CalmWaterTests.unity`, lancer Play Mode, attendre la
fin des cas (environ 22 s de simulation). CalmWaterValidation instancie
successivement le prefab, initialise chaque état, simule 150 pas fixes par
cas et affiche PASS/FAIL et le tableau CSV dans la Console ; results et
finished sont consultables pendant Play Mode. Aucun contrôleur embarqué.
Les seuils et conditions initiales sont dans ce script.

Tous les essais utilisent s=0 initial et eau à 6,5 m. Masse neutre 3,891593 kg,
cas lourd 110 %, cas demi-immergé 50 %. Corps initialement horizontal à Y=4 m,
sauf demi-immersion à Y=h0. Décélération : vitesse initiale +Z de 1 m/s.
Essais masse : consigne s=±0,12 m, atteinte en 2,4 s ; CG fixe aligné en Z au
centre volumique à s=0. +Z avant : forward.y négatif signifie nez qui pique,
soit pitch Unity positif autour de +X ; arrière : signe opposé.
Cas supplémentaire half_tilt : demi-immersion initiale, pitch Unity +10°,
vérification du retour vers une assiette proche de l'horizontale.

Le relevé des sept cas exécutés est dans PHASE_3_RESULTS.csv. Pas fixe Unity
mesuré : 0,02 s, durée mesurée par cas environ 3,02 s. Sept PASS observés.
Neutralité : Y=4,00005 m, vitesse≈0,00001 m/s. Demi-immersion :
Vimmergé=0,00194580 m³, Y=6,50000 m. Décélération : 1 → 0,21947 m/s.
Masse avant/arrière : forward.y=-0,83421/+0,83421 après environ 3 s.
Ces assiettes importantes résultent des paramètres provisoires choisis,
elles ne constituent pas une calibration du robot réel.
Le cas incliné revient de +10° à environ -0,59° (forward.y=0,01035),
avec Y=6,49952 m. Il reste une oscillation à 3 s : vitesse 0,06141 m/s,
donc cet essai valide le rappel sans démontrer un repos final strict.

Compilation et Play Mode vérifiés via unityMCP. La Console ne signale pas
d'erreur de compilation des scripts ; elle conserve des erreurs internes Unity
liées à Temp/FSTimeGet (currentFileSystemTime.ticks), indépendantes des cas.

Scène principale rechargée et essayée après enregistrement explicite du lien
eau comme override de prefab : à t=10,39 s, Y=3,00 m, vitesse=1,43e-5 m/s,
poids=38,17653 N, poussée=38,17623 N. Capture validée :
`Captures/phase3_calm_water_diagnostics-1.png`. La capture sans suffixe est
un état intermédiaire avant correction du lien d'eau ; ne pas l'utiliser
comme preuve de neutralité. Éditeur laissé hors Play Mode sur PoolSimulation.

## Fichiers et limites

Créés : CalmWaterBuoyancy.cs, BodyWaterDrag.cs, SlidingMass.cs,
Diagnostics/CalmWaterValidation.cs, Editor/CalmWaterSetup.cs,
CalmWaterTests.unity, relevé CSV et ce rapport (avec meta Unity).
Modifiés : RobotFish.prefab (composants, masse nominale théorique, inertie,
damping), PoolSimulation.unity (référence explicite à l'eau).

Les paramètres géométriques doivent être appliqués avant le lancement ;
les échantillons sont calculés à partir des dimensions courantes. Le modèle
n'est pas validé pour une palme tournée intersectant la coque ni une échelle
de Transform différente de 1. Les coefficients, CG, masse réelle, course,
inertie et épaisseur de palme restent à confirmer. Pas de test de convergence
complet du maillage d'échantillons ni de comparaison expérimentale. Aucun
contrôleur, propulsion, Gerstner, ROS 2 ou CFD implémenté.
