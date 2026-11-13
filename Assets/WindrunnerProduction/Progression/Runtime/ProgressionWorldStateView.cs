using UnityEngine;

namespace Windrunner.Production.Progression
{
    /// <summary>Explicitly authored gate/route presentation. No authority over player transforms.</summary>
    public sealed class ProgressionWorldStateView : MonoBehaviour
    {
        public GameObject route;
        public Transform movingStructure;
        public Vector3 closedLocalPosition, openLocalPosition;
        public void SetOpen(bool value)
        {
            if (route != null && route != gameObject) route.SetActive(value);
            if (movingStructure != null) movingStructure.localPosition = value ? openLocalPosition : closedLocalPosition;
        }
    }
}
