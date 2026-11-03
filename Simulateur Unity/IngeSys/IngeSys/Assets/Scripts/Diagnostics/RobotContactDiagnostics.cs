using UnityEngine;

namespace IngeSys.Diagnostics
{
    /// <summary>Solver impulses in N.s and mean forces J/dt in N; never applies a force.</summary>
    [DefaultExecutionOrder(20000), DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class RobotContactDiagnostics : MonoBehaviour
    {
        public Vector3 stepImpulseNs, stepMeanForceN, accumulatedImpulseNs;
        public float stepNormalImpulseNs, stepTangentialImpulseNs;
        public float lastImpactImpulseNs, maxStepMeanForceN;
        public int collisionEntries, contactPoints;
        public string lastOwnCollider, lastOtherCollider;
        public float lastContactTime;
        public Vector3 reportedSolverImpulseNs, accumulatedSolverImpulseNs;
        public Vector3 stepAngularImpulseNms, stepMeanTorqueNm;
        public bool zeroDamping = true;
        Rigidbody body;
        Vector3 previousMomentum, previousAngularMomentum, externalImpulse, externalAngularImpulse;
        bool pending;

        void Awake() { body=GetComponent<Rigidbody>(); }
        Vector3 AngularMomentum()
        {
            var rotation=body.rotation*body.inertiaTensorRotation;
            return rotation*Vector3.Scale(body.inertiaTensor,Quaternion.Inverse(rotation)*body.angularVelocity);
        }
        void LateUpdate() { FlushStep(); }
        public void FlushStep()
        {
            if(!pending || !body) return;
            pending=false;
            if(contactPoints==0) return;
            // Independent momentum balance includes tangential impulses not always exposed by callbacks.
            stepImpulseNs=body.mass*body.linearVelocity-previousMomentum-externalImpulse;
            stepAngularImpulseNms=AngularMomentum()-previousAngularMomentum-externalAngularImpulse;
            stepMeanForceN=stepImpulseNs/Time.fixedDeltaTime;
            stepMeanTorqueNm=stepAngularImpulseNms/Time.fixedDeltaTime;
            accumulatedImpulseNs+=stepImpulseNs;
            maxStepMeanForceN=Mathf.Max(maxStepMeanForceN,stepMeanForceN.magnitude);
        }

        void FixedUpdate()
        {
            FlushStep();
            if(!body)body=GetComponent<Rigidbody>();
            previousMomentum=body.mass*body.linearVelocity;
            previousAngularMomentum=AngularMomentum();
            externalImpulse=(body.GetAccumulatedForce(Time.fixedDeltaTime)+(body.useGravity?Physics.gravity*body.mass:Vector3.zero))*Time.fixedDeltaTime;
            externalAngularImpulse=body.GetAccumulatedTorque(Time.fixedDeltaTime)*Time.fixedDeltaTime;
            zeroDamping=body.linearDamping==0 && body.angularDamping==0;
            pending=true;
            stepAngularImpulseNms=stepMeanTorqueNm=reportedSolverImpulseNs=Vector3.zero;
            stepImpulseNs = stepMeanForceN = Vector3.zero;
            stepNormalImpulseNs = stepTangentialImpulseNs = 0;
            contactPoints = 0;
        }

        void OnCollisionEnter(Collision collision)
        {
            collisionEntries++;
            lastImpactImpulseNs = collision.impulse.magnitude;
            Record(collision);
        }

        void OnCollisionStay(Collision collision) { Record(collision); }

        void Record(Collision collision)
        {
            Vector3 impulse = collision.impulse;
            reportedSolverImpulseNs += impulse;
            accumulatedSolverImpulseNs += impulse;
            lastOtherCollider = collision.collider.name;
            lastContactTime = Time.fixedTime;
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                normal += contact.normal;
                lastOwnCollider = contact.thisCollider.name;
            }
            contactPoints += collision.contactCount;
            // Callback impulse decomposition is separate from the momentum-balance measurement above.
            normal.Normalize();
            float normalMagnitude = Mathf.Abs(Vector3.Dot(impulse, normal));
            stepNormalImpulseNs += normalMagnitude;
            stepTangentialImpulseNs += Vector3.ProjectOnPlane(impulse, normal).magnitude;
        }

        [ContextMenu("Reset measurements")]
        public void ResetMeasurements()
        {
            pending=false;
            stepImpulseNs = stepMeanForceN = accumulatedImpulseNs = Vector3.zero;
            reportedSolverImpulseNs=accumulatedSolverImpulseNs=stepAngularImpulseNms=stepMeanTorqueNm=Vector3.zero;
            stepNormalImpulseNs = stepTangentialImpulseNs = 0;
            lastImpactImpulseNs = maxStepMeanForceN = 0;
            collisionEntries = contactPoints = 0;
            lastOwnCollider = lastOtherCollider = "";
        }
    }
}
