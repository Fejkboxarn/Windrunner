using UnityEditor;
using UnityEngine;
namespace Windrunner.Suntemple.Editor
{
    public static class SuntempleFinalReferences
    {
        public static void Run()
        {
            const string root="Assets/WindrunnerCompatibility/Suntemple/DreamscapeSunTemple/";
            // The authored mask is unavailable locally. Explicitly retain the shader's existing
            // default rather than a broken GUID or an invented substitute pattern.
            var bark=AssetDatabase.LoadAssetAtPath<Material>(root+"Materials/Foliage/Trees/MI_Suntemple_OliveBark.mat");
            bark.SetTexture("_Mask_Map",null);EditorUtility.SetDirty(bark);AssetDatabase.SaveAssetIfDirty(bark);
            // Missing alternate grass diffuse: documented same-pack grass fallback; weights untouched.
            var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(root+"Scenes/TerrainLayers/04_Grass_Other.terrainlayer");
            layer.diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Polyart/PolyartStudio/DreamscapeSunTemple/Textures/Terrain/Grass/T_Suntemple_Grass_01_C.png");
            EditorUtility.SetDirty(layer);AssetDatabase.SaveAssetIfDirty(layer);
            SuntempleDependencyAudit.Run();
        }
    }
}
