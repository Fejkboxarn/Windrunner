using System;
using System.Collections.Generic;
using UnityEngine;
namespace AirflowPrototype.Editor
{
    // Editor authoring index only. Runtime systems continue to consume their existing module profiles.
    public sealed class CharacterTuningPreset : ScriptableObject
    {
        [Serializable] public sealed class Binding
        {
            public string ownerPath, componentType, propertyPath;
            public ScriptableObject profile;
        }
        public string characterName = "Petra";
        public List<Binding> bindings = new List<Binding>();
    }

}
