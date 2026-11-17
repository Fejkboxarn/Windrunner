using AirflowPrototype;
using UnityEngine;
namespace Windrunner.Daggers
{
    [CreateAssetMenu(menuName="Windrunner/Daggers/Second Wind")]
    public sealed class SecondWind : DaggerActiveEffect
    {
        [Range(.01f,1f)] public float restoreFraction=1;
        public bool allowAtFull;
        public override string Unavailable(AirPower power)
        {
            if(!power || power.Max<=0 || restoreFraction<=0)return "AirPower unavailable";
            return !allowAtFull && power.Current>=power.Max ? "AirPower full" : null;
        }
        public override void Execute(AirPower power)=>power.Restore(power.Max*Mathf.Clamp01(restoreFraction));
    }
}
