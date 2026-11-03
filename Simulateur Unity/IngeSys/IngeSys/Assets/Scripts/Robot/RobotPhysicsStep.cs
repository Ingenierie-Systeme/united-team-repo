using UnityEngine;
namespace IngeSys.Robot
{
    /// <summary>Scene-scoped timestep selection; restores the project setting when disabled.</summary>
    [DefaultExecutionOrder(-1000), DisallowMultipleComponent]
    public sealed class RobotPhysicsStep : MonoBehaviour
    {
        [Tooltip("Scene physics timestep. High-frequency convergence must be checked separately. Global while this scene runs.")]
        [Range(.002f,.02f)] public float fixedStepSeconds=.01f;
        float previous,applied;
        void OnEnable(){previous=Time.fixedDeltaTime;applied=Mathf.Clamp(fixedStepSeconds,.002f,.02f);Time.fixedDeltaTime=applied;}
        void OnDisable(){if(Mathf.Approximately(Time.fixedDeltaTime,applied))Time.fixedDeltaTime=previous;}
    }
}
