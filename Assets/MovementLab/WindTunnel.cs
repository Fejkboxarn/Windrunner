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

        // The previous *actual* displacement can traverse a thin volume between two motor ticks.
        // Clip that segment against the box, then credit only the fraction spent inside.
        public bool Crossed(Vector3 from, Vector3 to, out float fraction, out float depth)
        {
            Vector3 start = transform.InverseTransformPoint(from);
            Vector3 delta = transform.InverseTransformPoint(to) - start;
            float enter = 0, exit = 1;
            fraction = depth = 0;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(delta[axis]) < .000001f)
                {
                    if (Mathf.Abs(start[axis]) > .5f) return false;
                    continue;
                }
                float a = (-.5f - start[axis]) / delta[axis], b = (.5f - start[axis]) / delta[axis];
                enter = Mathf.Max(enter, Mathf.Min(a,b));
                exit = Mathf.Min(exit, Mathf.Max(a,b));
                if (enter >= exit) return false;
            }
            fraction = exit - enter;
            depth = Depth(Vector3.Lerp(from,to,(enter + exit) * .5f));
            return true;
        }
        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = entryImpulse ? Color.yellow : Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}
