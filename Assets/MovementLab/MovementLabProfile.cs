using UnityEngine;
namespace Windrunner.MovementLab
{
    [CreateAssetMenu(menuName="Windrunner/Movement Lab/Profile")]
    public sealed class MovementLabProfile : ScriptableObject
    {
        public enum WindLevel { Subtle, Standard, Strong }
        [Min(1)] public float flightSpeed = 18, flightSprint = 4;
        public WindSettings continuous = Preset(false, WindLevel.Standard);
        public WindSettings impulse = Preset(true, WindLevel.Standard);
        public static WindSettings Preset(bool burst, WindLevel level)
        {
            int n = (int)level;
            return new WindSettings {
                mode = burst ? WindMode.EntryImpulse : WindMode.Continuous,
                strength = burst ? new[]{3f,6f,10f}[n] : new[]{9f,12f,18f}[n],
                speedCap = burst ? new[]{16f,23f,34f}[n] : new[]{14f,22f,32f}[n],
                minimumSpeed = burst ? new[]{1f,3f,6f}[n] : .5f,
                rampSeconds = burst ? 0 : new[]{2f,1.25f,.75f}[n],
                edgeFalloff = burst ? 0 : .22f, verticalWeight = burst ? .25f : .4f,
                rearmSeconds = burst ? new[]{.3f,.5f,.8f}[n] : 0
            };
        }
        public OverdriveSettings overdrive = new OverdriveSettings();
        [Min(100)] public float combinedSpeedCap = 200;
    }
}
