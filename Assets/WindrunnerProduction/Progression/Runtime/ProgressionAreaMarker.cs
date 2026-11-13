using UnityEngine;

namespace Windrunner.Production.Progression
{
    public sealed class ProgressionAreaMarker : MonoBehaviour
    {
        public string areaId, displayName;
        [Min(1)] public float radius = 15;
        private void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, radius); }
    }
}
