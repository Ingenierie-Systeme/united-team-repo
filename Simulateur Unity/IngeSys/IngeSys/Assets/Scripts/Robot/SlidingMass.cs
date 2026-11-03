using UnityEngine;

namespace IngeSys.Robot
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody), typeof(RobotFishParameters))]
    public sealed class SlidingMass : MonoBehaviour
    {
        public float targetPositionM;
        [Min(0f)]
        public float maximumSpeedMps = 0.05f;
        [Min(0f), Tooltip("Constante de temps de l'actionneur, s ; 0 = vitesse limitée seule")]
        public float actuatorTimeConstantS = 0.15f;
        // CG fixe (coque et palme), en mètres.
        public Vector3 fixedMassCenter = new Vector3(0f, -0.01f, -0.056f);
        // Inertie nominale, en kg.m², axes principaux XYZ.
        public Vector3 nominalInertia = new Vector3(0.08f, 0.08f, 0.006f);
        public float positionM;

        void Awake()
        {
            positionM = GetComponent<RobotFishParameters>().slidingPositionZ;
            Step(0f);
        }

        void FixedUpdate()
        {
            Step(Time.fixedDeltaTime);
        }

        public void Step(float dt)
        {
            var p = GetComponent<RobotFishParameters>();
            var body = GetComponent<Rigidbody>();
            float min = Mathf.Min(p.slidingMinimumZ, p.slidingMaximumZ);
            float max = Mathf.Max(p.slidingMinimumZ, p.slidingMaximumZ);

            targetPositionM = Mathf.Clamp(targetPositionM, min, max);

            float distance = Mathf.Abs(targetPositionM - positionM);
            float step = actuatorTimeConstantS > 0f
                             ? distance * (1f - Mathf.Exp(-dt / actuatorTimeConstantS))
                             : distance;
            positionM = Mathf.MoveTowards(Mathf.Clamp(positionM, min, max), targetPositionM,
                                          Mathf.Min(step, Mathf.Max(0f, maximumSpeedMps) * dt));

            float total = Mathf.Max(0.001f, p.totalMassKg);
            float moving = Mathf.Clamp(p.slidingMassKg, 0f, total);

            var distribution = GetComponent<RobotMassDistribution>();
            if (distribution && distribution.Apply(positionM))
            {
                if (p.slidingMassVisual) p.slidingMassVisual.localPosition = new Vector3(0, 0, positionM);
                return;
            }

            body.mass = total;
            body.centerOfMass =
                ((total - moving) * fixedMassCenter + moving * new Vector3(0f, 0f, positionM)) /
                total;
            body.inertiaTensorRotation = Quaternion.identity;
            body.inertiaTensor = new Vector3(Mathf.Max(0.00001f, nominalInertia.x),
                                             Mathf.Max(0.00001f, nominalInertia.y),
                                             Mathf.Max(0.00001f, nominalInertia.z));
            if (p.slidingMassVisual)
            {
                p.slidingMassVisual.localPosition = new Vector3(0f, 0f, positionM);
            }
        }
    }
}
