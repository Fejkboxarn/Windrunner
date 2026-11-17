using System;
using System.IO;
using System.Linq;
using PortableAnimation;
using PortableAnimation.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace AirflowPrototype.Editor
{
    public sealed partial class DaggerMotionPreview
    {
        float previewStep=1f/60;
        bool draftDirty;
        void BuildPreview()
        {
            rebuild=false;DisposeComposition();RefreshUsed();
            draftDirty=draft.Entries.Any(e=>CharacterTuningSession.Content(e.Draft)!=CharacterTuningSession.Content(e.Baseline));
            hasUnsavedChanges=draftDirty;saveChangesMessage="Save the staged animation profile changes?";
            isolatedOutput.SetTarget(composed?null:previewRig);
            if(!composed)
            {
                var chosen=clip;
                var elapsed=playhead;
                if(!candidate)
                {
                    if(action>=0)chosen=Resolved(action,activeContext);
                    else
                    {
                        using var so=new SerializedObject(draftGround);
                        chosen=so.FindProperty(slots[slot].path).objectReferenceValue as AnimationClip;
                    }
                }
                var libraryClip=clip;
                if(chosen && selected.GetAnimationClip()!=chosen)SetPreviewClip(chosen);
                mixer.SetInputWeight(1,chosen?1:0);
                clip=libraryClip;playhead=elapsed;
                return;
            }
            try
            {
                previewGround=Instantiate(draftGround);previewActions=Instantiate(Actions);
                previewGround.hideFlags=previewActions.hideFlags=HideFlags.HideAndDontSave;
                if(candidate && clip)
                {
                    using var so=new SerializedObject(action<0?(ScriptableObject)previewGround:previewActions);
                    var dest=slots[slot].path;
                    so.FindProperty(dest).objectReferenceValue=clip;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    if(action>=0 && activeContext!=1)
                    {
                        var v=activeContext==0?previewActions.Action(action).stationary:previewActions.Action(action).airborne;
                        v.enabled=true;v.inheritClip=false;
                    }
                }
                if(noMask && action>=0)
                {
                    var a=previewActions.Action(action);
                    a.mask=null;a.regions.enabled=false;
                    foreach(var v in new[]{a.stationary,a.airborne})
                    {v.mask=null;v.regions.enabled=false;}
                }
                composition=new GroundOnlyPipelineRuntime(previewRig,previewGround,previewActions);
                previewFrame=0;
                bool stop=action<0 && slots[slot].label.IndexOf("Stop",StringComparison.OrdinalIgnoreCase)>=0;
                bool starting=action<0 && slots[slot].label.Contains("Start");
                bool jump=action<0 && slot==(int)GroundPose.JumpStart;
                bool land=action<0 && slot==(int)GroundPose.Land;
                for(int i=0;i<75;i++)PreviewTick(starting?0:stop?8.5f:PreviewSpeed(),jump || (!land && activeContext!=2),true);
                if(action>=0)composition.RequestAction(action);
                evaluated=0;
            }
            catch(Exception ex){DisposeComposition();issue=ex.Message;}
        }
        float PreviewSpeed()
        {
            if(action>=0)return activeContext==0?0:6;
            string name=slots[slot].label;
            if(activeContext==0 || name.Contains("Stop") || name.Contains("Turn"))return 0;
            if(name.Contains("Walk"))return 8.5f*(draftGround.IdleToWalkEnd+draftGround.WalkToRunStart)*.5f;
            return 8.5f;
        }
        void PreviewTick(float metres,bool grounded,bool warmup=false)
        {
            string direction=action>=0?"Forward":slots[slot].label;
            float x=direction.IndexOf("Right",StringComparison.OrdinalIgnoreCase)>=0?1:
                direction.IndexOf("Left",StringComparison.OrdinalIgnoreCase)>=0?-1:0;
            float z=direction.IndexOf("Back",StringComparison.OrdinalIgnoreCase)>=0?-1:
                direction.IndexOf("Forward",StringComparison.OrdinalIgnoreCase)>=0||x==0?1:0;
            bool pivot=direction.Contains("Pivot");
            bool turn=direction.Contains("Turn");
            if(pivot){x=warmup?0:x*.1f;z=warmup?1:-1;}
            float yaw=!warmup && (pivot||turn)?(direction.Contains("Left")?-1:1)*(draftGround.TurnRateThreshold+1):0;
            bool jump=action<0 && slot==(int)GroundPose.JumpStart;
            var velocity=new Vector3(x,0,z).normalized*metres;
            float dt=previewStep;
            var motion=new AnimationFrameState(previewFrame++,dt,velocity,Mathf.Clamp01(metres/8.5f),
                grounded?LocomotionMode.Grounded:LocomotionMode.Airborne);
            composition.Evaluate(new GroundMotionFrame(motion,grounded,direction.Contains("Rise") || jump?3:-2,yaw,jump && !warmup && previewFrame==76),metres);
        }
        void AdvancePreview()
        {
            while(evaluated+previewStep<=playhead+.00001f)
            {PreviewTick(PreviewSpeed(),activeContext!=2);evaluated+=previewStep;}
        }
        HumanBodyBones Representative(AuthoringBodyRegion region)
        {
            for(int i=0;i<(int)HumanBodyBones.LastBone;i++)
            {
                var bone=(HumanBodyBones)i;
                if(RegionMaskSettings.Region(bone)==region && previewRig.GetBoneTransform(bone))return bone;
            }
            return HumanBodyBones.LastBone;
        }
        ActionMotionContext EffectiveContext
        {
            get
            {
                var v=Variant;
                return v!=null && v.enabled && (v.inheritClip||v.Available||v.muteWhenMissing)?Mode:ActionMotionContext.GroundedMoving;
            }
        }
        void DrawBones(Rect rect)
        {
            Vector2 ScreenPoint(Vector3 world)
            {
                var uv=studio.camera.WorldToViewportPoint(world);
                return new Vector2(rect.x+uv.x*rect.width,rect.y+(1-uv.y)*rect.height);
            }
            Handles.BeginGUI();
            GUI.BeginClip(rect);
            for(int i=0;i<(int)HumanBodyBones.LastBone;i++)
            {
                var b=(HumanBodyBones)i;var bone=previewRig.GetBoneTransform(b);
                if(!bone || !bone.parent)continue;
                float w=composed && composition!=null && action>=0?composition.FinalRotationContribution(contributionView==2?-1:action,contributionView==0?EffectiveContext:(ActionMotionContext)(-1),b):0;
                Handles.color=Color.Lerp(new Color(.40f,.53f,.62f),new Color(1,.65f,.08f),w);
                Vector2 point=ScreenPoint(bone.position)-rect.position;
                Handles.DrawLine(ScreenPoint(bone.parent.position)-rect.position,point);
                Handles.DrawSolidDisc(point,Vector3.forward,2);
            }
            GUI.EndClip();Handles.EndGUI();
        }
        void LaunchDraft()
        {
            try
            {
                var errors=draft.Validate();
                if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
                AnimationLaneDraft.Store(draftProfile,draftGround,Actions);
                EditorApplication.ExecuteMenuItem("Tools/Windrunner/Acceptance/Play traversal and rewards lane");
            }
            catch(Exception ex){issue=ex.Message;}
        }
    }
}
