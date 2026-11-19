using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Windrunner.TerrainAuthoring.Editor
{
    public static class TerrainStampLibrary
    {
        public const string Folder="Assets/WindrunnerTerrain/Editor/Brushes/Library";
        public static readonly string[] Sculpt={"Rolling Hills","Rounded Mountain","Sharp Peak","Long Ridge","Rocky Plateau","Escarpment","Broad Valley","Natural Gully","Eroded Slope","Terrain Breakup"};
        public static readonly string[] Paint={"Grass Patches","Dirt Variation","Rocky Ground","Exposed Cliff","Forest Floor","Mud Patches","Sandy Shoreline","Worn Path","Organic Variation"};
        static float G(float x,float y,float sx,float sy)=>Mathf.Exp(-(x*x/sx+y*y/sy));
        static float N(float x,float y)=>Mathf.PerlinNoise(x+17.37f,y+41.29f);
        // Original elevation/mask functions. Light and shadows are never inputs to height data.
        public static float Value(int recipe,bool paint,float x,float y)
        {
            float broad=N(x*2.1f,y*2.1f),detail=N(x*9,y*9),fine=N(x*23,y*23);
            if(paint)
            {
                switch(recipe)
                {
                    case 0:return Mathf.SmoothStep(0,1,Mathf.Clamp01((broad*.75f+detail*.25f-.30f)*2.7f));
                    case 1:return .25f+.6f*broad+.15f*detail;
                    case 2:return Mathf.Pow(Mathf.Clamp01(1-Mathf.Abs(2*N(x*5,y*5)-1)),3)*(.45f+.55f*detail);
                    case 3:return Mathf.Clamp01(.25f+.55f*N(x*12,y*2)+.2f*fine);
                    case 4:return Mathf.SmoothStep(0,1,Mathf.Clamp01(broad*.5f+detail*.35f+fine*.15f));
                    case 5:return Mathf.Pow(Mathf.Clamp01((broad-.2f)*1.5f),1.6f);
                    case 6:return Mathf.Clamp01(.5f-y*.45f+(broad-.5f)*.25f);
                    case 7:return G(x+.08f*Mathf.Sin(y*5),0,.055f,1)*(.65f+.35f*detail);
                    default:return .15f+.45f*broad+.3f*detail+.1f*fine;
                }
            }
            switch(recipe)
            {
                case 0:return .58f*G(x+.35f,y+.05f,.24f,.45f)+.48f*G(x-.35f,y-.3f,.32f,.25f);
                case 1:return G(x,y,.28f,.36f)*(.82f+.18f*broad)+.14f*G(x+.38f,y-.28f,.12f,.18f);
                case 2:return Mathf.Pow(Mathf.Clamp01(1-Mathf.Sqrt(x*x+y*y)),1.65f)*(.84f+.16f*detail);
                case 3:return G(x+.08f*Mathf.Sin(y*5),y,.025f,.7f)*(.75f+.25f*N(0,y*4));
                case 4:return Mathf.SmoothStep(0,1,Mathf.Clamp01((.72f-Mathf.Sqrt(x*x+y*y))*5))*(.88f+.12f*detail);
                case 5:return Mathf.SmoothStep(0,1,Mathf.Clamp01((x+.15f+(broad-.5f)*.12f)*7))*(.87f+.13f*detail);
                case 6:return G(x+.12f*Mathf.Sin(y*3),y,.15f,1.4f);
                case 7:return G(x+.09f*Mathf.Sin(y*7),y,.015f,1.5f)*(.65f+.35f*(1-y)*.5f);
                case 8:return Mathf.Clamp01((1+y)*.5f)*(.65f+.35f*Mathf.Abs(Mathf.Sin(x*12+(broad-.5f)*4)));
                default:return .55f*broad+.3f*detail+.15f*fine;
            }
        }
        public static void Generate()
        {
            if(Directory.Exists(Folder))throw new InvalidOperationException("Library exists; never overwrite authored library assets.");
            AssetDatabase.CreateFolder("Assets/WindrunnerTerrain/Editor/Brushes","Library");
            foreach(bool paint in new[]{false,true})
            {
                var names=paint?Paint:Sculpt;
                for(int i=0;i<names.Length;i++)
                {
                    var b=ScriptableObject.CreateInstance<TerrainStamp>();b.name=names[i];b.mode=paint?StampMode.Paint:StampMode.Sculpt;
                    b.category=paint?(i<3?"Ground":i<6?"Surface breakup":"Transitions"):(i<2?"Hills":i<6?"Mountains and cliffs":i<9?"Valleys and slopes":"Detail");
                    b.description=paint?"Grayscale influence only; choose an existing TerrainLayer. No texture replacement.":i==6||i==7?"Use Lower to carve this smooth depression/channel into existing elevation.":"Original mathematical elevation field; height is in metres, not image brightness from lighting.";
                    b.provenance="Windrunner original analytical heightfield/mask recipe v1. No Blizzard images or third-party brush data. Eroded Slope is channel-inspired mathematics, not physical erosion simulation.";
                    var t=new Texture2D(257,257,TextureFormat.R16,false,true){name=names[i]+" R16 linear",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                    var data=new ushort[257*257];for(int y=0;y<257;y++)for(int x=0;x<257;x++)data[y*257+x]=(ushort)Mathf.RoundToInt(Mathf.Clamp01(Value(i,paint,x/128f-1,y/128f-1))*65535);
                    t.SetPixelData(data,0);t.Apply(false,false);b.field=t;AssetDatabase.CreateAsset(b,Folder+"/"+b.name+".asset");AssetDatabase.AddObjectToAsset(t,b);EditorUtility.SetDirty(b);
                }
            }
            var presets=new[]{"Gentle Woodland Hills","Rocky Highlands","Eroded Mountain Ridge","Broad Valley","Forest Soil Breakup","Exposed Rock Transition","Natural Grass Variation","Sandy Shoreline"};
            var brushes=new[]{Sculpt[0],Sculpt[1],Sculpt[3],Sculpt[6],Paint[4],Paint[3],Paint[0],Paint[6]};
            var layerGuids=AssetDatabase.FindAssets("t:TerrainLayer",new[]{"Assets/TriForge Assets","Assets/Raygeas"});
            var layers=layerGuids.Select(g=>AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            string[] hints={"Forestfloor01","MudStones","Grass01","Sand"};
            for(int i=0;i<presets.Length;i++)
            {
                var p=ScriptableObject.CreateInstance<TerrainStampPreset>();p.brush=AssetDatabase.LoadAssetAtPath<TerrainStamp>(Folder+"/"+brushes[i]+".asset");p.settings.size=i<4?120:45;p.settings.height=i==0?12:45;p.settings.strength=i<4?.65f:.35f;
                if(i==3)p.settings.operation=HeightOperation.Lower;if(i>=4)p.settings.layer=layers.FirstOrDefault(l=>l&&l.name.IndexOf(hints[i-4],StringComparison.OrdinalIgnoreCase)>=0);
                AssetDatabase.CreateAsset(p,Folder+"/Preset - "+presets[i]+".asset");
            }
            foreach(string guid in AssetDatabase.FindAssets("",new[]{Folder})){var asset=AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));if(asset)AssetDatabase.SaveAssetIfDirty(asset);}
            Debug.Log("Created 10 R16 sculpt stamps, 9 paint masks and 8 editable presets.");
        }
    }
}
