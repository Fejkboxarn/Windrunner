using System;
using System.Collections.Generic;
using System.Linq;
using PortableAnimation;
using UnityEditor;
using UnityEngine;
using Windrunner.Production.Animation;

namespace AirflowPrototype.Editor
{
    public sealed partial class DaggerMotionPreview
    {
        CharacterTuningSession draft;
        WindrunnerAnimationProfile laneProfile, draftProfile;
        GroundMotionProfile draftGround;
        int slot;
        readonly List<(string label, CharacterTuningSession.Entry entry, string path)> slots = new();
        string[] slotLabels;
        string[] projectPaths;
        int indexed;
        readonly List<(string name,string path,long id,float length,bool loop)> library = new();
        readonly List<int> matches=new();
        string projectSearch="";
        bool Matches(string name,string path) =>
            name.IndexOf(projectSearch,StringComparison.OrdinalIgnoreCase)>=0 ||
            path.IndexOf(projectSearch,StringComparison.OrdinalIgnoreCase)>=0;
        bool showLibrary;
        void IndexProjectBatch()
        {
            if(projectPaths==null || !showLibrary || EditorApplication.isPlayingOrWillChangePlaymode)return;
            for(int end=Math.Min(indexed+4,projectPaths.Length);indexed<end;indexed++)
            {
                string path=projectPaths[indexed];
                if(AssetImporter.GetAtPath(path) is ModelImporter model &&
                    (!model.importAnimation || model.animationType!=ModelImporterAnimationType.Human))continue;
                foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if(asset is AnimationClip c && c.humanMotion && !c.legacy && !c.name.StartsWith("__preview__") &&
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c,out string _,out long id))
                    {
                        if(InCategory(c.name,path) && Matches(c.name,path))matches.Add(library.Count);
                        library.Add((c.name,path,id,c.length,c.isLooping));
                    }
            }
            if(indexed==projectPaths.Length)projectPaths=null;
            Repaint();
        }
        void CloseDraft()
        {
            draft?.Dispose();
            draft=null;draftProfile=null;draftGround=null;
            slots.Clear();
            slot=0;
        }
        void OpenDraft()
        {
            CloseDraft();
            draft=new CharacterTuningSession(bridge.gameObject);
            draftProfile=laneProfile?laneProfile:bridge.Profile;
            draft.IncludeAnimationProfile(draftProfile);
            var ground=draft.Entries.First(e=>e.Source==draftProfile.standaloneGroundProfile);
            var actions=draft.Entries.First(e=>e.Source==draftProfile.standaloneActionProfile);
            draftGround=(GroundMotionProfile)ground.Draft;
            for(int i=0;i<(int)GroundPose.Count;i++)
                slots.Add(("Ground/"+(GroundPose)i,ground,$"clips.Array.data[{i}]"));
            foreach(string field in new[]{"runForwardRight","runBackwardRight","runBackwardLeft","runForwardLeft",
                "walkForward","walkRight","walkBackward","walkLeft","walkForwardRight","walkBackwardRight","walkBackwardLeft","walkForwardLeft",
                "stopBackward","stopLeft","stopRight"})
                slots.Add(("Ground/"+ObjectNames.NicifyVariableName(field),ground,field));
            var profile=(PortableCharacterActionProfile)actions.Draft;
            for(int i=0;i<profile.Count;i++)
            {
                string path=$"actions.Array.data[{i}]";
                foreach(string suffix in new[]{"clip","stationary.clip","airborne.clip"})
                    slots.Add((profile.Action(i).id+"/"+(suffix=="clip"?"Moving and fallback":suffix.Split('.')[0]),actions,path+"."+suffix));
            }
            slotLabels=slots.Select(s=>s.label).ToArray();
        }
    }
}
