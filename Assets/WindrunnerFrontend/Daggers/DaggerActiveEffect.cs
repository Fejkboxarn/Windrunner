using UnityEngine;
using AirflowPrototype;
namespace Windrunner.Daggers
{
    public abstract class DaggerActiveEffect : ScriptableObject
    {
        public string id, displayName;
        [TextArea] public string description;
        [Min(.01f)] public float cooldown=30f;
        public abstract string Unavailable(AirPower power);
        public abstract void Execute(AirPower power);
    }
}
