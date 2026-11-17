using UnityEngine;
using AirflowPrototype;
namespace Windrunner.Daggers
{
    public abstract class DaggerActiveEffect : ScriptableObject
    {
        public string id, displayName;
        [TextArea] public string description;
        public abstract string Unavailable(AirPower power, float stored);
        public abstract void Execute(AirPower power, float stored);
    }
}
