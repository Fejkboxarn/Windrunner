using UnityEngine;
namespace Windrunner.MovementLab
{
    [CreateAssetMenu(menuName="Windrunner/Movement Lab/Profile")]
    public sealed class MovementLabProfile : ScriptableObject
    {
        public WindSettings continuous = new WindSettings();
        public WindSettings impulse = new WindSettings { mode = WindMode.EntryImpulse, strength = 60, rampSeconds = .1f, edgeFalloff = 0 };
        public OverdriveSettings overdrive = new OverdriveSettings();
        [Min(100)] public float combinedSpeedCap = 200;
    }
}
