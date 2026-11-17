using UnityEngine;

namespace Windrunner.Production.Progression
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class QuestRegion : MonoBehaviour
    {
        public string areaId;
        public BoxCollider volume;
        public bool Contains(Vector3 position)
        {
            if (!volume || !volume.enabled || !isActiveAndEnabled) return false;
            Vector3 p = volume.transform.InverseTransformPoint(position) - volume.center;
            Vector3 half = volume.size * .5f;
            return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
        }
        void OnDrawGizmos()
        {
            if (!volume) return;
            Gizmos.color = new Color(.2f,.8f,.65f,.6f);
            Gizmos.matrix = volume.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(volume.center, volume.size);
        }
    }
}
