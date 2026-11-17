using UnityEngine;
namespace Windrunner.TerrainAuthoring
{
    [CreateAssetMenu(menuName="Windrunner/Terrain/Road profile")]
    public sealed class RoadProfile : ScriptableObject
    {
        public TerrainLayer surface;
        [Min(.2f),Tooltip("Core road width in metres.")] public float width=7;
        [Min(.1f)] public float shoulderMetres=2;
        [Range(0,1)] public float vegetation=.18f, edgeIrregularity=.4f, wear=.75f;
        public AnimationCurve shoulderFade=AnimationCurve.EaseInOut(0,1,1,0);
        public int priority;
        public bool carriage;
        [Min(.1f)] public float trackSpacing=1.7f, trackWidth=.36f, textureRepeatMetres=3;
        [Range(0,1)] public float trackBreakup=.35f;
        public Material trackMaterial;
    }
}
