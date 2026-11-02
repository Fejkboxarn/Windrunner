using System;
using System.Linq;
using PortableAnimation;
using UnityEditor;
using UnityEngine;
using Windrunner.Production.Animation;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// Creates or updates character-local assets using animations ALREADY
    /// present in Windrunner. Does not duplicate vendor FBX assets, replace
    /// controllers, modify the scene, or activate a PlayableGraph.
    /// </summary>
    public static class WindrunnerAnimationClipBinder
    {
        private const string Menu = "Tools/Windrunner/Production/";
        private const string Generated = "Assets/WindrunnerProduction/Animation/Generated/";
        private const string ProfilePath = Generated + "WindrunnerAnimationProfile.asset";
        private const string GroundPath = Generated + "WindrunnerGroundMotion.asset";
        private const string ActionsPath = Generated + "WindrunnerActionClips.asset";
        private const string Inplace =
            "Assets/DarkSorcerer_AnimSet/Animations/Humanoid/Inplace/";

        [MenuItem(Menu + "Batch 4B - Bind Windrunner Clips")]
        public static void Bind()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[Animation 4B] Run the binder in Edit Mode.");
                return;
            }
            var profile = AssetDatabase.LoadAssetAtPath<WindrunnerAnimationProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError("[Animation 4B] First install the Batch 4 Animation Bridge.");
                return;
            }

            AnimationClip idle = FindClip("idle_inplace");
            AnimationClip[] runs =
            {
                FindClip("move_run_front_inplace"), FindClip("move_run_back_inplace"),
                FindClip("move_run_left_inplace"), FindClip("move_run_right_inplace")
            };
            AnimationClip[] walks =
            {
                FindClip("move_walk_front_inplace"), FindClip("move_walk_back_inplace"),
                FindClip("move_walk_left_inplace"), FindClip("move_walk_right_inplace")
            };
            if (idle == null || runs.Any(c => c == null) || walks.Any(c => c == null))
            {
                Debug.LogError("[Animation 4B] Missing Idle or cardinal Inplace walk/run FBX " +
                               "clips. No assets were changed. Check the DarkSorcerer_AnimSet " +
                               "package and the model animation import settings.");
                return;
            }

            GroundMotionProfile ground =
                AssetDatabase.LoadAssetAtPath<GroundMotionProfile>(GroundPath);
            if (ground != null && !EditorUtility.DisplayDialog(
                    "Rebind Windrunner clips?",
                    "This will replace the locomotion assignments in the existing " +
                    "WindrunnerGroundMotion asset. All other generated assets " +
                    "and your scene remain untouched.", "Rebind", "Cancel"))
                return;

            if (ground == null)
            {
                ground = ScriptableObject.CreateInstance<GroundMotionProfile>();
                AssetDatabase.CreateAsset(ground, GroundPath);
            }
            else Undo.RecordObject(ground, "Rebind Windrunner locomotion");

            AnimationClip[] slots = new AnimationClip[(int)GroundPose.Count];
            slots[(int)GroundPose.Idle] = idle;
            slots[(int)GroundPose.MoveForward] = runs[0];
            slots[(int)GroundPose.MoveBackward] = runs[1];
            slots[(int)GroundPose.MoveLeft] = runs[2];
            slots[(int)GroundPose.MoveRight] = runs[3];
            slots[(int)GroundPose.JumpStart] = FindClip("jump_inplace");
            slots[(int)GroundPose.Rise] = FindClip("rise01_inplace");
            // Fall, landing, stop, pivot and turn are left empty rather than
            // mislabeling an unrelated animation as a production clip.
            ground.Configure(null, slots);
            ground.ConfigureRealClips(walks, new AnimationClip[4]);
            ground.ConfigureDirectionalDiagonals(
                new[]
                {
                    FindClip("move_run_frontR45_inplace"),
                    FindClip("move_run_backR45_inplace"),
                    FindClip("move_run_backL45_inplace"),
                    FindClip("move_run_frontL45_inplace")
                },
                new[]
                {
                    FindClip("move_walk_frontR45_inplace"),
                    FindClip("move_walk_backR45_inplace"),
                    FindClip("move_walk_backL45_inplace"),
                    FindClip("move_walk_frontL45_inplace")
                });

            PortableCharacterActionProfile actions =
                AssetDatabase.LoadAssetAtPath<PortableCharacterActionProfile>(ActionsPath);
            if (actions == null)
            {
                actions = ScriptableObject.CreateInstance<PortableCharacterActionProfile>();
                AssetDatabase.CreateAsset(actions, ActionsPath);
            }
            Undo.RecordObject(profile, "Bind Windrunner animation profile");
            profile.useStandaloneCharacterGraph = true;
            profile.standaloneGroundProfile = ground;
            profile.standaloneActionProfile = actions;
            profile.automaticallyStartPortableInPlayMode = false;
            EditorUtility.SetDirty(ground);
            EditorUtility.SetDirty(actions);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            bool ready = profile.ValidateForLiveGraph(out string reason);
            Debug.Log("[Animation 4B] Bound authentic Windrunner in-place locomotion " +
                      "to a standalone graph (8 run + 8 walk directions, Idle, optional " +
                      "jump/rise). Profile ready=" + ready +
                      (ready ? "" : ", reason=" + reason) +
                      ". Auto-start remains OFF. No scene or Animator changed.");
            Selection.activeObject = ground;
            EditorGUIUtility.PingObject(ground);
        }

        [MenuItem(Menu + "Batch 4B - Audit Standalone Graph")]
        public static void Audit()
        {
            WindrunnerAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<WindrunnerAnimationProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError("[Animation 4B] Missing WindrunnerAnimationProfile. " +
                               "Install Batch 4 bridge first.");
                return;
            }
            bool ready = profile.ValidateForLiveGraph(out string reason);
            GroundMotionProfile ground = profile.standaloneGroundProfile;
            if (ground != null)
            {
                int run = 0, walk = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (ground.HasUsableDirectionalClip(false, i)) run++;
                    if (ground.HasUsableDirectionalClip(true, i)) walk++;
                }
                Debug.Log("[Animation 4B] Direction samples: run=" + run +
                          "/8, walk=" + walk + "/8, idle=" +
                          (ground.Clip(GroundPose.Idle) != null) +
                          ", jump=" + (ground.Clip(GroundPose.JumpStart) != null) +
                          ", rise=" + (ground.Clip(GroundPose.Rise) != null) +
                          ", fall=" + (ground.Clip(GroundPose.Fall) != null) +
                          ", land=" + (ground.Clip(GroundPose.Land) != null));
            }
            int count = profile.standaloneActionProfile != null
                ? profile.standaloneActionProfile.Count : 0;
            Debug.Log("[Animation 4B] Standalone=" +
                      profile.useStandaloneCharacterGraph +
                      ", action clips=" + count +
                      ", ready=" + ready +
                      (ready ? "" : ", reason=" + reason) +
                      ". Audit covers asset wiring, NOT visual quality or safe live takeover.");
        }

        private static AnimationClip FindClip(string fileStem)
        {
            string path = Inplace + fileStem + ".fbx";
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(c => c != null && c.length > 0f && !c.legacy &&
                    !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return clips.FirstOrDefault(c =>
                c.name.IndexOf(fileStem, StringComparison.OrdinalIgnoreCase) >= 0)
                ?? clips.FirstOrDefault();
        }
    }
}
