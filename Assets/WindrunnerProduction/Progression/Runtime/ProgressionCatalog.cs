using UnityEngine;

namespace Windrunner.Production.Progression
{
    [CreateAssetMenu(menuName = "Windrunner/Progression/Catalog")]
    public sealed class ProgressionCatalog : ScriptableObject
    {
        public ProgressionDefinitions definitions = new ProgressionDefinitions();
    }
}
