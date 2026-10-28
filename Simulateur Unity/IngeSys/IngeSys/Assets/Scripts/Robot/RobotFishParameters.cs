using UnityEngine;

namespace IngeSys.Robot
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class RobotFishParameters : MonoBehaviour
    {
        [Min(0.001f)]
        public float hullLength = 0.40f;
        [Min(0.001f)]
        public float hullDiameter = 0.10f;
        [Min(0.001f)]
        public float finLength = 0.15f;
        [Min(0.001f)]
        public float finHeight = 0.10f;
        [Min(0.001f)]
        public float finThickness = 0.05f;
        [Min(0.001f)]
        public float totalMassKg = 1f;
        [Min(0f)]
        public float slidingMassKg = 0.1f;
        public float slidingMinimumZ = -0.12f;
        public float slidingMaximumZ = 0.12f;
        public float slidingPositionZ;
        public Vector3 nominalCenterOfMass = Vector3.zero;
        public Vector3 buoyancyReference = Vector3.zero;
        public Transform hull;
        public Transform finPivot;
        public Transform fin;
        public Transform slidingMassVisual;
        public Transform travelMinimum;
        public Transform travelMaximum;
        public Transform nominalCoMMarker;
        public Transform buoyancyMarker;
        public Transform finForcePoint;
        public Transform forwardAxis;

        void Awake()
        {
            ApplyParameters();
        }

        void OnValidate()
        {
            hullLength = Mathf.Max(0.001f, hullLength);
            hullDiameter = Mathf.Max(0.001f, hullDiameter);
            finLength = Mathf.Max(0.001f, finLength);
            finHeight = Mathf.Max(0.001f, finHeight);
            finThickness = Mathf.Max(0.001f, finThickness);
            totalMassKg = Mathf.Max(0.001f, totalMassKg);
            slidingMassKg = Mathf.Clamp(slidingMassKg, 0f, totalMassKg);
            slidingMaximumZ = Mathf.Max(slidingMinimumZ, slidingMaximumZ);
            slidingPositionZ = Mathf.Clamp(slidingPositionZ, slidingMinimumZ, slidingMaximumZ);
            ApplyParameters();
        }

        [ContextMenu("Apply geometry and nominal mass parameters")]
        public void ApplyParameters()
        {
            if (hull)
            {
                hull.localPosition = Vector3.zero;
                hull.localRotation = Quaternion.Euler(90f, 0f, 0f);
                hull.localScale = new Vector3(hullDiameter, hullLength / 2f, hullDiameter);
            }
            if (finPivot)
            {
                finPivot.localPosition = new Vector3(0f, 0f, -hullLength / 2f);
            }
            if (fin)
            {
                fin.localPosition = new Vector3(0f, 0f, -finLength / 2f);
                fin.localScale = new Vector3(finThickness, finHeight, finLength);
            }
            if (slidingMassVisual)
            {
                slidingMassVisual.localPosition = new Vector3(
                    0f, 0f, Mathf.Clamp(slidingPositionZ, slidingMinimumZ, slidingMaximumZ));
            }
            if (travelMinimum)
            {
                travelMinimum.localPosition = new Vector3(0f, 0f, slidingMinimumZ);
            }
            if (travelMaximum)
            {
                travelMaximum.localPosition = new Vector3(0f, 0f, slidingMaximumZ);
            }
            if (nominalCoMMarker)
            {
                nominalCoMMarker.localPosition = nominalCenterOfMass;
            }
            if (buoyancyMarker)
            {
                buoyancyMarker.localPosition = buoyancyReference;
            }
            if (finForcePoint)
            {
                finForcePoint.localPosition = new Vector3(0f, 0f,
                                                          finForcePoint.parent == finPivot
                                                              ? -finLength / 2f
                                                              : -hullLength / 2f - finLength / 2f);
            }
            var neutralFin = transform.Find("FinCollision_Neutral_STATIC_IN_BODY");
            if (neutralFin)
            {
                neutralFin.localPosition = new Vector3(0f, 0f, -hullLength / 2f - finLength / 2f);
                var box = neutralFin.GetComponent<BoxCollider>();
                if (box)
                {
                    box.size = new Vector3(finThickness, finHeight, finLength);
                }
            }
            if (forwardAxis)
            {
                forwardAxis.localPosition = new Vector3(0f, 0f, hullLength / 2f + 0.05f);
                forwardAxis.localScale = new Vector3(0.003f, 0.003f, hullLength + 0.1f);
            }
            var body = GetComponent<Rigidbody>();
            if (body)
            {
                body.mass = totalMassKg;
                body.centerOfMass = nominalCenterOfMass;
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * (hullLength / 2f + 0.15f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(nominalCenterOfMass, 0.009f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(buoyancyReference, 0.012f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(new Vector3(0, 0, slidingMinimumZ), new Vector3(0, 0, slidingMaximumZ));
            Gizmos.DrawWireCube(new Vector3(0, 0, slidingPositionZ),
                                new Vector3(0.035f, 0.035f, 0.04f));
            if (finPivot)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(finPivot.localPosition - Vector3.up * 0.08f,
                                finPivot.localPosition + Vector3.up * 0.08f);
            }
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.TransformPoint(nominalCenterOfMass + Vector3.up * 0.09f), "CoM nominal");
            UnityEditor.Handles.Label(
                transform.TransformPoint(buoyancyReference - Vector3.up * 0.09f),
                "Centre de flottabilité");
            if (finForcePoint)
            {
                UnityEditor.Handles.Label(finForcePoint.position, "Force palme moyenne");
            }
            if (finPivot)
            {
                UnityEditor.Handles.Label(finPivot.position + transform.up * 0.1f,
                                          "Pivot +Y / Palme : épaisseur 0,05 m");
            }
#endif
        }
    }
}
