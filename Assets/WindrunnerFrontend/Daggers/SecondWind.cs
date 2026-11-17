using AirflowPrototype;
using UnityEngine;
namespace Windrunner.Daggers
{
    [CreateAssetMenu(menuName="Windrunner/Daggers/Second Wind")]
    public sealed class SecondWind : DaggerActiveEffect
    {
        [Min(.01f)] public float capacity = 50;
        public AirPowerRewardSource eligibleSources = AirPowerRewardSource.AirNode | AirPowerRewardSource.SlingNode;
        [Range(0,1)] public float storageRatio = 1;
        [Tooltip("Fraction of banked energy returned on release. Every successful release empties the reserve.")]
        [Range(.01f,1)] public float transferEfficiency = 1;
        public Color meterColor = new Color(.10f,.65f,.72f);
        public Sprite meterSprite;
        public ParticleSystem chargeVfx;
        public override string Unavailable(AirPower power, float stored)
        {
            if (!power || power.Max <= 0) return "AirPower unavailable";
            if (stored <= 0) return "Reserve empty";
            return power.Current >= power.Max ? "AirPower full" : null;
        }
        public override void Execute(AirPower power, float stored)
        {
            power.Restore(Mathf.Min(Mathf.Max(0,power.Max-power.Current),stored * Mathf.Clamp01(transferEfficiency)));
        }
    }
}
