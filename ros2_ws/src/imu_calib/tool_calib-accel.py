#!/usr/bin/env python3

'''Fournit un offset b et une matrice M pour la calibration de l'accéléromètre'''
'''Il faut donc les reporter dans le accel_offset et accel_misal du node_fusion'''

import numpy as np
import time
import json
import bluerobotics_navigator as navigator


navigator.set_rgb_led_strip_size(1)
navigator.set_navigator_version(navigator.NavigatorVersion.Version2)
navigator.set_raspberry_pi_version(navigator.Raspberry.Pi4)
navigator.init()

# =========================
# CONFIG
# =========================
G = 9.81
DURATION = 5        # secondes par face
SAMPLE_DT = 0.01    # 100 Hz

POSITIONS = [
    "+X", "-X",
    "+Y", "-Y",
    "+Z", "-Z"
]

# =========================
# ACQUISITION
# =========================

def measure_face(navigator, label):
    input(f"\n👉 Place l'IMU en position {label}, puis ENTER")

    samples = []
    start = time.time()

    while time.time() - start < DURATION:
        accel_read = navigator.read_accel()
        accel = np.array([accel_read.x,accel_read.y,accel_read.z])

        samples.append(accel)
        time.sleep(SAMPLE_DT)

    samples = np.array(samples)
    mean = np.mean(samples, axis=0)

    print(f"{label} mean: {mean}")
    print(f"{label} norm: {np.linalg.norm(mean):.3f}")

    return mean


def collect_data(navigator):
    data = []
    for pos in POSITIONS:
        m = measure_face(navigator, pos)
        data.append(m)

    return np.array(data)


# =========================
# CALIBRATION
# =========================

def calibrate_accel_6faces(data):
    ref = np.array([
        [ G, 0, 0],
        [-G, 0, 0],
        [0,  G, 0],
        [0, -G, 0],
        [0, 0,  G],
        [0, 0, -G],
    ])

    A = []
    B = []

    for i in range(6):
        x, y, z = data[i]

        # ligne X
        A.append([x, y, z, 0, 0, 0, 0, 0, 0, 1, 0, 0])
        B.append(ref[i][0])

        # ligne Y
        A.append([0, 0, 0, x, y, z, 0, 0, 0, 0, 1, 0])
        B.append(ref[i][1])

        # ligne Z
        A.append([0, 0, 0, 0, 0, 0, x, y, z, 0, 0, 1])
        B.append(ref[i][2])

    A = np.array(A)
    B = np.array(B)

    # moindres carrés
    X, *_ = np.linalg.lstsq(A, B, rcond=None)

    M = np.array([
        X[0:3],
        X[3:6],
        X[6:9]
    ])

    Mb = X[9:12]

    # récupérer offset
    b = np.linalg.inv(M).dot(Mb)

    return M, b


# =========================
# APPLICATION
# =========================

def apply_calibration(accel, M, b):
    return M.dot(accel - b)


# =========================
# MAIN
# =========================

def run(navigator):
    print("=== Calibration accéléromètre 6 faces ===")

    data = collect_data(navigator)

    print("\n📊 Données collectées :")
    print(data)

    M, b = calibrate_accel_6faces(data)

    print("\n=== Résultats ===")
    print("Offset b:")
    print(b)

    print("\nMatrice M:")
    print(M)


# =========================
# USAGE
# =========================
run(navigator)