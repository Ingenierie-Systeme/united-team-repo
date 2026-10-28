#!/usr/bin/env python3

import numpy as np
import time
import bluerobotics_navigator as navigator


navigator.set_rgb_led_strip_size(1)
navigator.set_navigator_version(navigator.NavigatorVersion.Version2)
navigator.set_raspberry_pi_version(navigator.Raspberry.Pi4)
navigator.init()

# =========================
# 1. Acquisition des données
# =========================

duration=75
time.sleep(2)
print("Bouge le capteur dans toutes les orientations...")
data = []

start = time.time()
while time.time() - start < duration:
    mag_read = navigator.read_mag()  # [mx, my, mz]
    mag = [-mag_read.y, mag_read.x, mag_read.z]
    data.append(mag)
    time.sleep(0.1)

data = np.array(data)

print(f"{data.shape} points collectés")

np.save("mag-log.npy", data)