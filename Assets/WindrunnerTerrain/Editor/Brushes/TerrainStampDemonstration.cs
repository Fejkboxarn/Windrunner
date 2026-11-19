using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace Windrunner.TerrainAuthoring.Editor
{
    public static class TerrainStampDemonstration
    {
        public const string Folder="Assets/WindrunnerTerrain/Editor/Brushes/Demonstration";
        public static void Create()
        {
            if(AssetDatabase.IsValidFolder(Folder))throw new InvalidOperationException("Disposable demonstration already exists; do not overwrite edits.");
            var previous=SceneManager.GetActiveScene();if(previous.isDirty&&!Application.isBatchMode)throw new InvalidOperationException("Save the open scene first.");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,string.IsNullOrEmpty(previous.path)?NewSceneMode.Single:NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            AssetDatabase.CreateFolder("Assets/WindrunnerTerrain/Editor/Brushes","Demonstration");
            const string vendor="Assets/Raygeas/Grasslands - Stylized Nature/Assets/Terrain Layers/";
            var profile=Object.Instantiate(StudioSetup.Preset<SurfaceProfile>("Basin surfaces"));
            profile.grass=AssetDatabase.LoadAssetAtPath<TerrainLayer>(vendor+"Grass.terrainlayer");profile.soil=AssetDatabase.LoadAssetAtPath<TerrainLayer>(vendor+"Mud.terrainlayer");profile.rock=AssetDatabase.LoadAssetAtPath<TerrainLayer>(vendor+"MudStones.terrainlayer");
            AssetDatabase.CreateAsset(profile,Folder+"/Disposable surface profile.asset");
            var data=new TerrainData{name="Disposable stamp terrain",heightmapResolution=257,alphamapResolution=128,size=new Vector3(512,180,512),terrainLayers=new[]{profile.grass,profile.soil,profile.rock}};
            var h=new float[257,257];for(int y=0;y<257;y++)for(int x=0;x<257;x++)h[y,x]=.22f;data.SetHeights(0,0,h);
            var a=new float[128,128,3];for(int y=0;y<128;y++)for(int x=0;x<128;x++)a[y,x,0]=1;data.SetAlphamaps(0,0,a);AssetDatabase.CreateAsset(data,Folder+"/Disposable source.asset");
            var terrain=Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();terrain.name="DISPOSABLE brush demonstration - no Zone 01 data";
            var mat=new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));AssetDatabase.CreateAsset(mat,Folder+"/Disposable terrain.mat");terrain.materialTemplate=mat;
            var tile=StudioSetup.Attach(terrain,profile,Folder);
            Stamp(tile,"Rounded Mountain",new Vector2(110,130),new StampSettings{size=180,height=65,strength=.85f});
            Stamp(tile,"Sharp Peak",new Vector2(350,350),new StampSettings{size=220,height=135,strength=.95f});
            Stamp(tile,"Long Ridge",new Vector2(130,365),new StampSettings{size=230,height=110,strength=.85f,rotation=35});
            Stamp(tile,"Broad Valley",new Vector2(340,135),new StampSettings{size=210,height=32,strength=.85f,operation=HeightOperation.Lower,rotation=65});
            Stamp(tile,"Terrain Breakup",new Vector2(250,250),new StampSettings{size=400,height=7,strength=.3f});
            Paint(tile,"Rocky Ground",new Vector2(350,350),new StampSettings{size=225,layer=profile.rock,strength=.95f});
            Paint(tile,"Dirt Variation",new Vector2(340,135),new StampSettings{size=200,layer=profile.soil,strength=.75f});
            Paint(tile,"Organic Variation",new Vector2(240,240),new StampSettings{size=210,layer=profile.soil,strength=.4f});
            var sun=new GameObject("Demonstration sun",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(42,-35,0);sun.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.52f,.60f,.7f);RenderSettings.fog=false;
            var camera=new GameObject("Demonstration Game camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(660,420,-230);camera.transform.LookAt(new Vector3(255,65,260));camera.nearClipPlane=.5f;camera.farClipPlane=2500;camera.fieldOfView=48;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.35f,.55f,.72f);
            EditorSceneManager.SaveScene(scene,Folder+"/Disposable brush demonstration.unity");
            foreach(string guid in AssetDatabase.FindAssets("",new[]{Folder})){var asset=AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));if(asset)AssetDatabase.SaveAssetIfDirty(asset);}
            Directory.CreateDirectory("Logs/TerrainBrushLibrary");Capture(camera,"Logs/TerrainBrushLibrary/game-camera.png");
            var view=ScriptableObject.CreateInstance<SceneView>();view.Show();view.LookAtDirect(new Vector3(255,60,260),Quaternion.Euler(55,-35,0),430);view.Repaint();
            var sceneCamera=view.camera;sceneCamera.transform.position=new Vector3(-160,490,-120);sceneCamera.transform.LookAt(new Vector3(250,60,260));sceneCamera.farClipPlane=2500;Capture(sceneCamera,"Logs/TerrainBrushLibrary/scene-camera.png");view.Close();
            if(previous.IsValid())SceneManager.SetActiveScene(previous);if(SceneManager.sceneCount==1)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);else EditorSceneManager.CloseScene(scene,true);
            Debug.Log("Disposable terrain demonstration saved and Game/Scene camera renders captured.");
        }
        static void Stamp(StudioTile tile,string name,Vector2 center,StampSettings settings)=>TerrainStampOperations.Apply(TerrainStampOperations.Prepare(tile,AssetDatabase.LoadAssetAtPath<TerrainStamp>(TerrainStampLibrary.Folder+"/"+name+".asset"),settings,center));
        static void Paint(StudioTile tile,string name,Vector2 center,StampSettings settings){Stamp(tile,name,center,settings);PaintHistory.Resolve(tile,tile.state.nativeDirty,true,1);}
        static void Capture(Camera camera,string path)
        {
            var target=new RenderTexture(1600,1000,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);}
        }
    }
}
