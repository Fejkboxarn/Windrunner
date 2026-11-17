using UnityEngine;
namespace Windrunner.TerrainAuthoring
{
    [CreateAssetMenu(menuName="Windrunner/Terrain/Surface profile")]
    public sealed class SurfaceProfile : ScriptableObject
    {
        public TerrainLayer grass, soil, rock;
        [Tooltip("Horizontal axis is slope in degrees. Vertical axis is relative coverage.")]
        public AnimationCurve rockBySlope = AnimationCurve.EaseInOut(12,0,53,1);
        public AnimationCurve grassBySlope = AnimationCurve.EaseInOut(5,1,48,.03f);
        [Tooltip("World elevation, metres. Scales meadow coverage independently of slope.")]
        public AnimationCurve grassByElevation = AnimationCurve.Linear(0,1,480,.83f);
        [Tooltip("Optional soft-edged region of exposed soil in world X/Z metres.")]
        public bool useSoilRegion;
        public Rect soilRegion=new Rect(-40,-30,80,60);
        [Min(.1f)] public float regionFeather=15;
        [Range(0,1)] public float regionStrength=.5f;
        public int seed=4723;
        [Min(1)] public float patchMetres=75, breakupMetres=14;
        [Range(0,1)] public float variation=.65f, curvatureInfluence=.45f;
        [Min(1)] public float curvatureRadius=8;
        [Tooltip("Unit world direction facing the sun; influences exposed rock breakup.")]
        public Vector2 exposureDirection=new Vector2(.6f,.8f);
        public Vector3 Evaluate(Vector2 world,float elevation,Vector3 normal,float curvature)
        {
            float broad=Noise(world,patchMetres),fine=Noise(world,breakupMetres);
            float slope=Vector3.Angle(Vector3.up,normal);
            float exposure=Vector2.Dot(new Vector2(normal.x,normal.z),exposureDirection.normalized);
            float effective=Mathf.Clamp(slope+variation*(9*broad+3*fine+2*exposure),0,90);
            float stone=Mathf.Clamp01(rockBySlope.Evaluate(effective)+Mathf.Max(0,curvature)*curvatureInfluence);
            float meadow=Mathf.Max(0,grassBySlope.Evaluate(effective)*grassByElevation.Evaluate(elevation));
            float soilWeight=Mathf.Clamp01(.23f+variation*(.52f*broad+.13f*fine)-curvature*.3f);
            if(useSoilRegion)
            {
                float inside=Mathf.Min(Mathf.Min(world.x-soilRegion.xMin,soilRegion.xMax-world.x),Mathf.Min(world.y-soilRegion.yMin,soilRegion.yMax-world.y));
                soilWeight=Mathf.Lerp(soilWeight,1,regionStrength*Mathf.SmoothStep(0,1,Mathf.Clamp01(inside/Mathf.Max(.1f,regionFeather))));
            }
            float green=Mathf.Clamp01(meadow*(1-soilWeight))*(1-stone);
            return new Vector3(green,1-stone-green,stone);
        }
        float Noise(Vector2 p,float scale) => 2*Mathf.PerlinNoise(p.x/Mathf.Max(1,scale)+(seed%997)*.71f,p.y/Mathf.Max(1,scale)+(seed%443)*.83f)-1;
    }
}
