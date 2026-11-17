using UnityEngine;
using System;
namespace Windrunner.Daggers
{
    public abstract class DaggerPassiveEffect : ScriptableObject
    {
        public string displayName;
        [TextArea] public string description;
        // A future passive returns its own reversible subscription/modifier.
        public abstract IDisposable Attach(GameObject player);
    }
}
