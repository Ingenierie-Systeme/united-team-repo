using UnityEngine;

namespace IngeSys.Robot
{
    /// <summary>Copies the V1 fin angle to the CAD servo axis without adding physical forces.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class CadPaleAnimation : MonoBehaviour
    {
        public Transform simulatedFinPivot;
        void FixedUpdate()
        {
            if (simulatedFinPivot) transform.localRotation = simulatedFinPivot.localRotation;
        }
    }
}
