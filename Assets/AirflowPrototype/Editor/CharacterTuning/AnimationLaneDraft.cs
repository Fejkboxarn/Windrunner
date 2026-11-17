using System;
using System.IO;
using System.Linq;
using PortableAnimation;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Windrunner.Production.Animation;

namespace AirflowPrototype.Editor
{
    internal static class AnimationLaneDraft
    {
        const string Key="Windrunner.AnimationStudio.Draft";
        static string PathFor(string token)=>Path.Combine("Library","WindrunnerStudio",token+".bin");
        public static bool Pending=>Guid.TryParseExact(SessionState.GetString(Key,""),"N",out _);
        public static void Store(WindrunnerAnimationProfile root,GroundMotionProfile ground,PortableCharacterActionProfile actions,bool launch=true)
        {
            var token=Guid.NewGuid().ToString("N");var path=PathFor(token);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var g=UnityEngine.Object.Instantiate(ground);var a=UnityEngine.Object.Instantiate(actions);
            try
            {
                g.hideFlags=a.hideFlags=HideFlags.None;g.name=ground.name;a.name=actions.name;
                InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[]{g,a},path,true);
                if(!File.Exists(path))throw new IOException("Could not stage the test profiles.");
                var assetPath=AssetDatabase.GetAssetPath(root);
                if(Pending)Finish();
                SessionState.SetString(Key,token);
                SessionState.SetString(Key+".source",AssetDatabase.GetAssetDependencyHash(assetPath).ToString());
                if(launch)SessionState.SetString("Windrunner.Acceptance.MotionProfile",AssetDatabase.AssetPathToGUID(assetPath));
                SessionState.SetString(Key+".profile",assetPath);
            }
            finally{UnityEngine.Object.DestroyImmediate(g);UnityEngine.Object.DestroyImmediate(a);}
        }
        public static WindrunnerAnimationProfile Source=>AssetDatabase.LoadAssetAtPath<WindrunnerAnimationProfile>(SessionState.GetString(Key+".profile",""));
        public static (GroundMotionProfile ground,PortableCharacterActionProfile actions) Read()
        {
            if(!Pending)throw new InvalidOperationException("The lane draft is unavailable.");
            string path=SessionState.GetString(Key+".profile","");
            if(AssetDatabase.GetAssetDependencyHash(path).ToString()!=SessionState.GetString(Key+".source",""))
                throw new InvalidOperationException("The authored profile changed since this draft was staged.");
            var modules=InternalEditorUtility.LoadSerializedFileAndForget(PathFor(SessionState.GetString(Key,"")));
            return (modules.OfType<GroundMotionProfile>().Single(),modules.OfType<PortableCharacterActionProfile>().Single());
        }
        public static void Finish()
        {
            if(Pending)File.Delete(PathFor(SessionState.GetString(Key,"")));
            SessionState.EraseString(Key);
        }
    }
}
