using UnityEngine;
using UnityEngine.Splines;
namespace Windrunner.TerrainAuthoring
{
    [RequireComponent(typeof(SplineContainer))]
    public sealed class StudioRoad : MonoBehaviour
    {
        public RoadProfile profile;
    }
}
