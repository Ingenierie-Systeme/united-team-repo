using UnityEngine;
namespace IngeSys.Robot
{
    // Buoyancy and hull drag must be accumulated before evaluating the fluid response.
    [DefaultExecutionOrder(500), DisallowMultipleComponent]
    public sealed class FinHydrodynamicStep : MonoBehaviour
    {
        FinHydrodynamics fin;
        void Awake(){fin=GetComponent<FinHydrodynamics>();}
        void FixedUpdate(){if(fin&&fin.enabled)fin.StepHydrodynamics();}
    }
}
