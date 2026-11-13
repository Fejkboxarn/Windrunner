using UnityEngine;
using Windrunner.Production.Progression;

namespace AirflowPrototype
{
    /// <summary>Only for the engineering fixture, which intentionally has no combat abilities.</summary>
    public sealed class ProgressionFixtureSafety : MonoBehaviour, IProgressionControlSafety
    {
        public PlayerMotor motor;
        public bool CanStartCinematic => motor != null && motor.IsGrounded && motor.PlanarVelocity.sqrMagnitude < 0.01f;
    }
}
