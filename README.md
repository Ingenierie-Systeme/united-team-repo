<h1 align="center">United Team</h1>

<p align="center">
  <strong>Robot pour l'exploration karstique</strong>
</p>

<h3 align="center">Membres de l'équipe</h3>

<p align="center">
  Kadijatou Diallo - Elodie Conan - Luíza Diesel Chesani - Tom Voisine - Alexandre Pinard - Matys BIECHE
</p>

<p align="center">
  <a href="#mécanique">Mécanique</a> &nbsp; / &nbsp;
  <a href="#electronique">Electronique</a> &nbsp; / &nbsp;
  <a href="#firmware">Firmware</a> &nbsp; / &nbsp;
  <a href="#software">Software</a> &nbsp; / &nbsp;
  <a href="#simulation">Simulation</a>
</p>

Liens Canva (pour l'équipe):
https://canva.link/1owfrv3v93n80cp

Lien drive : 
https://drive.google.com/drive/folders/1FjV8GK_41jrpMsA0FTS6ITrTZtgfy01Q?usp=sharing




---


---

## Mécanique

**Membres Concernés :** Prénom Nom

Conception mécanique, modélisation des pièces et assemblage du robot.

---

## Electronique

**Membres Concernés :**  Prénom Nom

Conception des cartes, choix des composants et intégration électronique.

**Consulter le schéma :** [Schéma électronique (PDF)](Schemas/SchémaElec.drawio.pdf).

[![Schéma électronique du robot](Schemas/SchemaElec.png)](Schemas/SchémaElec.drawio.pdf)

L’architecture électronique repose sur une bastterie LiPo 2S de 5 700 mAh (7,4 V nominal, 8,4 V chargée). Le module Holybro PM06 V2 distribue la tension de la batterie aux équipements. Un UBEC Blue Robotics abaisse cette tension à 5 V pour alimenter la carte Navigator et le Raspberry Pi 4 via les entrées MAIN et AUX. Ces deux sorties partagent un budget de courant total de 6 A ; la sortie 5,2 V du PM06 et son port de mesure ne sont pas utilisés dans ce schéma.

La carte Navigator assure la commande des actionneurs et l’acquisition du capteur :

- Le servomoteur HS-5496MH déplace la masse mobile. Il est alimenté directement par la batterie via le PM06 et reçoit son signal de commande sur PWM1.
- Le servomoteur D955TW assure le palmage. Il est commandé sur PWM2 et alimenté par le rail AUX à 5 V.
- La lampe Blue Robotics Lumen est alimentée par la batterie via le PM06 et commandée sur PWM3. Le schéma précise que sa pleine luminosité n’est pas disponible sous 10 V, ce qui limite l’éclairage avec une batterie 2S.
- Le capteur de pression BAR30 est raccordé au port I²C6 : alimentation à 5 V et lignes de communication SCL/SDA à 3,3 V.
- La caméra USB ELP est reliée au Raspberry Pi, qui fournit son alimentation à 5 V et reçoit les données vidéo par USB.

Tous les équipements partagent une masse commune (GND). Pour PWM1 et PWM3, seuls le signal et la masse sont raccordés à la Navigator, puisque le servomoteur de masse mobile et la lampe disposent de leur propre alimentation depuis le PM06.

---

## Firmware

**Membres Concernés :**  Prénom Nom

Programmation embarquée, gestion des capteurs et commande des actionneurs.

---

## Software

**Membres Concernés :** Prénom Nom

Logiciels de pilotage, interfaces utilisateur et outils de communication.

---

## Simulation

**Membres Concernés :**  Prénom Nom

Simulateur Unity du robot et de son environnement aquatique sous Unity

**Accéder au projet :** [Simulateur Unity](<Simulateur Unity/IngeSys/IngeSys>).
