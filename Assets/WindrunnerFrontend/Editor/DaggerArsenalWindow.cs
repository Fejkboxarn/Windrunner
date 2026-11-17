using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Windrunner.Daggers;
using Object=UnityEngine.Object;

namespace Windrunner.Frontend.Editor
{
    public sealed class DaggerArsenalWindow : EditorWindow
    {
        const string Folder="Assets/WindrunnerFrontend/Daggers/Defaults";
        public const string CatalogPath=Folder+"/Arsenal.asset";
        DaggerCatalog catalog;
        DaggerPair pair;
        DaggerPlayer player;
        [MenuItem("Tools/Windrunner/Dagger Arsenal")]
        public static void Open()=>GetWindow<DaggerArsenalWindow>("Dagger Arsenal");
        void OnEnable()=>catalog=AssetDatabase.LoadAssetAtPath<DaggerCatalog>(CatalogPath);
        void OnGUI()
        {
            catalog=(DaggerCatalog)EditorGUILayout.ObjectField("Arsenal",catalog,typeof(DaggerCatalog),false);
            if(!catalog)return;
            string issue=catalog.ValidateSetup();
            EditorGUILayout.HelpBox(issue??"Pair IDs, models, effects and starting ownership are valid.",issue==null?MessageType.Info:MessageType.Warning);
            if(GUILayout.Button("Edit catalog and starting ownership"))Selection.activeObject=catalog;
            pair=(DaggerPair)EditorGUILayout.ObjectField("Pair",pair,typeof(DaggerPair),false);
            foreach(var item in catalog.pairs)if(item && GUILayout.Button(item.displayName)){pair=item;Selection.activeObject=item;}
            using(new EditorGUI.DisabledScope(Application.isPlaying))
                if(GUILayout.Button("Create pair in this catalog"))
                {
                    var path=EditorUtility.SaveFilePanelInProject("New dagger pair","New dagger pair","asset","Save the pair definition.");
                    if(string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path))return;
                    var created=CreateInstance<DaggerPair>();created.id=Guid.NewGuid().ToString("N");created.displayName=Path.GetFileNameWithoutExtension(path);
                    AssetDatabase.CreateAsset(created,path);Undo.RecordObject(catalog,"Register dagger pair");
                    Array.Resize(ref catalog.pairs,catalog.pairs.Length+1);catalog.pairs[catalog.pairs.Length-1]=created;
                    EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);pair=created;Selection.activeObject=created;
                }
            player=(DaggerPlayer)EditorGUILayout.ObjectField("Play Mode player",player,typeof(DaggerPlayer),true);
            using(new EditorGUI.DisabledScope(!Application.isPlaying || !player || player.Equipment==null || !player.Equipment.Owns(pair)))
                if(GUILayout.Button("Equip on test player"))player.Equipment.Equip(pair);
        }

        // One-shot initial content factory. Existing designer assets must never be replaced.
        public static void CreateDefaults()
        {
            if(AssetDatabase.IsValidFolder(Folder))throw new IOException("Dagger defaults folder already exists.");
            AssetDatabase.CreateFolder("Assets/WindrunnerFrontend/Daggers","Defaults");
            Material Material(string name,Color color,string shader)
            {
                var material=new Material(Shader.Find(shader)){name=name};material.color=color;
                AssetDatabase.CreateAsset(material,Folder+"/"+name+".mat");return material;
            }
            var wind=Material("Windglass",new Color(.14f,.88f,.93f),"Universal Render Pipeline/Lit");
            wind.EnableKeyword("_EMISSION");wind.SetColor("_EmissionColor",new Color(.035f,.35f,.42f));
            var steel=Material("Practice steel",new Color(.54f,.51f,.44f),"Universal Render Pipeline/Lit");
            var grip=Material("Wrapped grip",new Color(.18f,.12f,.09f),"Universal Render Pipeline/Lit");
            var spark=Material("Wind motes",new Color(.32f,.98f,1),"Universal Render Pipeline/Particles/Unlit");
            var gust=new GameObject("Second Wind motes");var ps=gust.AddComponent<ParticleSystem>();
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=.32f;main.startLifetime=.45f;
            main.startSize=.035f;main.startSpeed=1.3f;main.maxParticles=22;main.startColor=new Color(.35f,1,1);
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,18)});
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=spark;
            var burst=PrefabUtility.SaveAsPrefabAsset(gust,Folder+"/Second Wind burst.prefab").GetComponent<ParticleSystem>();
            DestroyImmediate(gust);
            var active=CreateInstance<SecondWind>();active.id="second-wind";active.displayName="Second Wind";
            active.description="Restore AirPower up to its current maximum.";active.cooldown=30;
            AssetDatabase.CreateAsset(active,Folder+"/Second Wind.asset");
            DaggerPair Make(string id,string name,bool powered)
            {
                var pair=CreateInstance<DaggerPair>();pair.id=id;pair.displayName=name;
                pair.description=powered?"Windglass blades that draw a second breath from the air.":"Blunt steel blades for training.";
                pair.accent=powered?new Color(.4f,.96f,1f):new Color(.87f,.79f,.63f);
                var root=new GameObject(name);
                void Part(string label,Mesh mesh,Material material,Vector3 offset,Vector3 scale)
                {
                    var go=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);
                    go.transform.localPosition=offset;go.transform.localScale=scale;
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
                }
                var cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                Part("Grip",cube,grip,Vector3.zero,new Vector3(.028f,.13f,.027f));
                Part("Guard",cube,powered?wind:steel,new Vector3(0,.068f,0),new Vector3(powered?.12f:.08f,.018f,.032f));
                var blade=new Mesh{name=name+" blade"};float length=powered?.38f:.28f;
                blade.vertices=new[]{new Vector3(-.028f,0,0),new Vector3(0,0,.014f),new Vector3(.028f,0,0),new Vector3(0,0,-.014f),new Vector3(powered?.045f:0,length,0)};
                blade.triangles=new[]{0,1,4,1,2,4,2,3,4,3,0,4,0,2,1,0,3,2};blade.RecalculateNormals();blade.RecalculateBounds();
                AssetDatabase.CreateAsset(blade,Folder+"/"+id+" blade.asset");
                Part("Blade",blade,powered?wind:steel,new Vector3(0,.075f,0),Vector3.one);
                pair.leftModel=pair.rightModel=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+id+".prefab");DestroyImmediate(root);
                // A small transparent silhouette stays legible beside the resource status.
                var image=new Texture2D(64,64,TextureFormat.RGBA32,false);
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {
                    float dx=x-32-(y-32)*.42f;
                    bool ink=y>5 && y<58 && (y<18?Mathf.Abs(dx)<2:y<22?Mathf.Abs(dx)<9:Mathf.Abs(dx)<(58-y)*(powered?.18f:.125f));
                    image.SetPixel(x,y,ink?Color.white:Color.clear);
                }
                image.Apply();var path=Folder+"/"+id+".png";File.WriteAllBytes(path,image.EncodeToPNG());DestroyImmediate(image);
                AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.SaveAndReimport();
                pair.icon=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if(powered){pair.active=active;pair.activationVfx=burst;}
                AssetDatabase.CreateAsset(pair,Folder+"/"+name+".asset");return pair;
            }
            var basic=Make("training","Training Daggers",false);var far=Make("farseer","Farseer's Daggers",true);
            var catalog=CreateInstance<DaggerCatalog>();catalog.pairs=new[]{basic,far};catalog.startingPairs=new[]{basic,far};catalog.initialPair=basic;
            AssetDatabase.CreateAsset(catalog,CatalogPath);AssetDatabase.SaveAssets();
            if(catalog.ValidateSetup()!=null)throw new InvalidDataException(catalog.ValidateSetup());
        }
    }
}
