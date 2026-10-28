#!/usr/bin/env python3

"""Obtenir l'offset sur les 3 valeurs du gyro, à reporter dans le node_fusion.py """

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

duration=10
print("Pas bouger")
time.sleep(1)
data = []

start = time.time()
while time.time() - start < duration:
    gyro_read = navigator.read_gyro()  # [mx, my, mz]
    gyro = [gyro_read.x, gyro_read.y, gyro_read.z]
    data.append(gyro)
    time.sleep(0.1)

data = np.array(data)

offset = np.mean(data,axis=0)

print(offset)