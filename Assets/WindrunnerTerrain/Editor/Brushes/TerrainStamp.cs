using UnityEngine;
namespace Windrunner.TerrainAuthoring.Editor
{
    public enum StampMode { Sculpt, Paint }
    public enum HeightOperation { Raise, Lower, Absolute }
    [PreferBinarySerialization]
    public sealed class TerrainStamp : ScriptableObject
    {
        public StampMode mode;
        public string category, description, provenance;
        public Texture2D field;
        public ushort[] Samples()=>field.GetPixelData<ushort>(0).ToArray();
        public static float Sample(ushort[] values,int resolution,float u,float v)
        {
            if(u<0||v<0||u>1||v>1)return 0;
            float x=u*(resolution-1),y=v*(resolution-1);int a=Mathf.FloorToInt(x),b=Mathf.FloorToInt(y),c=Mathf.Min(a+1,resolution-1),d=Mathf.Min(b+1,resolution-1);
            return Mathf.Lerp(Mathf.Lerp(values[b*resolution+a],values[b*resolution+c],x-a),Mathf.Lerp(values[d*resolution+a],values[d*resolution+c],x-a),y-b)/65535f;
        }
    }
    [System.Serializable] public sealed class StampSettings
    {
        public float size=80,strength=.5f,rotation,falloff=.3f,height=25,absoluteHeight=20;
        public bool invert;
        public HeightOperation operation;
        public TerrainLayer layer;
        public StampSettings Copy()=>JsonUtility.FromJson<StampSettings>(JsonUtility.ToJson(this));
    }
}
