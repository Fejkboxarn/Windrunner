using UnityEngine;

namespace AirflowPrototype
{
    [CreateAssetMenu(
        fileName = "RunningArmPose",
        menuName = "Airflow Prototype/Running Arm Pose Preset")]
    public sealed class PlayerRunningArmPosePreset : ScriptableObject
    {
        [Header("Left Arm Additive Local Rotation")]
        public Vector3 leftUpperArmEuler;
        public Vector3 leftLowerArmEuler;
        public Vector3 leftHandEuler;

        [Header("Right Arm Additive Local Rotation")]
        public Vector3 rightUpperArmEuler;
        public Vector3 rightLowerArmEuler;
        public Vector3 rightHandEuler;

        public void ResetPose()
        {
            leftUpperArmEuler = Vector3.zero;
            leftLowerArmEuler = Vector3.zero;
            leftHandEuler = Vector3.zero;

            rightUpperArmEuler = Vector3.zero;
            rightLowerArmEuler = Vector3.zero;
            rightHandEuler = Vector3.zero;
        }
    }
}
