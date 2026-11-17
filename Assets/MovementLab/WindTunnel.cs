using UnityEngine;
namespace Windrunner.MovementLab
{
    public sealed class WindTunnel : MonoBehaviour
    {
        public MovementLabProfile profile;
        public bool entryImpulse;
        public WindSettings Settings => entryImpulse ? profile.impulse : profile.continuous;
        public float Depth(Vector3 position)
        {
            Vector3 p = transform.InverseTransformPoint(position);
            return 1 - 2 * Mathf.Max(Mathf.Abs(p.x), Mathf.Max(Mathf.Abs(p.y), Mathf.Abs(p.z)));
        }
        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = entryImpulse ? Color.yellow : Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}
