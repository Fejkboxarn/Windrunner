using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using PortableAnimation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed class HighSpeedLegTests
    {
        const string Folder = "Assets/WindrunnerProduction/Animation/Generated/";
        static readonly HumanBodyBones[] Bones = { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.Spine, HumanBodyBones.Chest,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm };
        [Test]
        public void SustainedProductionStrideComparison()
        {
            string label = Environment.GetEnvironmentVariable("WINDRUNNER_STRIDE_LABEL") ?? "stride-probe";
            var ground = AssetDatabase.LoadAssetAtPath<GroundMotionProfile>(Folder+"WindrunnerGroundMotion.asset");
            var authored = AssetDatabase.LoadAssetAtPath<PortableCharacterActionProfile>(Folder+"WindrunnerActionClips.asset");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AirflowPrototype/VampGirl_V2/Vamgirl_Windrunner.fbx");
            float maximumMaskedLegDifference = 0;
            var rows = new List<string> { "Speed,Direction,Variant,Bone,MotionDegrees,LocomotionDifferenceDegrees,ReportedActionContribution,AuthoredCompositionDifferenceDegrees" };
            foreach (float speed in new[] { 7.5f, 15f })
            foreach (float angle in new[] { 0f, -22.5f, -45f, -67.5f, -90f })
            foreach (int variant in new[] { 0, 1, 2 })
            {
                var actions = Object.Instantiate(authored);
                if (variant == 2)
                    for (int i = 0; i < actions.Count; i++)
                    { actions.Action(i).regions.enabled = false; actions.Action(i).stationary.regions.enabled = false; actions.Action(i).airborne.regions.enabled = false; }
                var actor = Object.Instantiate(model); var peer = Object.Instantiate(model);
                var composedPeer = Object.Instantiate(model);
                Animator Rig(GameObject obj)
                {
                    var rig = obj.GetComponentInChildren<Animator>(); rig.runtimeAnimatorController = null;
                    rig.applyRootMotion = false; rig.fireEvents = false; rig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    return rig;
                }
                var rig = Rig(actor); var peerRig = Rig(peer); var composedRig = Rig(composedPeer);
                PreviewRenderUtility preview = null;
                Texture2D sheet = null;
                Material inspectionMaterial = null;
                if (variant == 1 && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    inspectionMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    inspectionMaterial.SetColor("_BaseColor", new Color(.65f, .83f, .95f));
                    foreach (var subject in new[] { actor, peer })
                        foreach (var renderer in subject.GetComponentsInChildren<Renderer>())
                        {
                            var materials = new Material[renderer.sharedMaterials.Length];
                            for (int i = 0; i < materials.Length; i++) materials[i] = inspectionMaterial;
                            renderer.sharedMaterials = materials;
                        }
                    preview = new PreviewRenderUtility();
                    preview.AddSingleGO(actor); preview.AddSingleGO(peer);
                    preview.camera.fieldOfView = 30;
                    preview.camera.transform.position = new Vector3(2, 1.5f, 3.6f);
                    preview.camera.transform.LookAt(new Vector3(0, .9f, 0));
                    preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 100;
                    preview.lights[0].intensity = 1.4f; preview.lights[0].transform.rotation = Quaternion.Euler(40, 30, 0);
                    preview.lights[1].intensity = .7f;
                    sheet = new Texture2D(6*240, 2*320, TextureFormat.RGB24, false);
                }
                void Capture(int column)
                {
                    if (preview == null) return;
                    for (int row = 0; row < 2; row++)
                    {
                        actor.SetActive(row == 0); peer.SetActive(row == 1);
                        preview.BeginPreview(new Rect(0, 0, 240, 320), GUIStyle.none);
                        preview.Render(true); var image = preview.EndPreview();
                        var prior = RenderTexture.active;
                        RenderTexture.active = (RenderTexture)image;
                        sheet.ReadPixels(new Rect(0, 0, 240, 320), column*240, (1-row)*320);
                        RenderTexture.active = prior;
                    }
                    actor.SetActive(true); peer.SetActive(true);
                }
                try
                {
                    using var graph = new GroundOnlyPipelineRuntime(rig, ground, actions);
                    using var reference = new GroundOnlyPipelineRuntime(peerRig, ground, null);
                    using var composed = new GroundOnlyPipelineRuntime(composedRig, ground, actions);
                    var first = new Quaternion[Bones.Length]; var motion = new float[Bones.Length]; var difference = new float[Bones.Length];
                    var composedDifference = new float[Bones.Length];
                    float previousSeconds = -1; int advances = 0;
                    for (int frame = 0; frame < 540; frame++)
                    {
                        var velocity = Quaternion.Euler(0, angle, 0)*Vector3.forward*speed;
                        var state = new AnimationFrameState(frame, 1f/60, velocity, speed/8.5f, LocomotionMode.Grounded);
                        var input = new GroundMotionFrame(state, true, 0, 0, false, speed);
                        if (frame == 60 && variant != 0) Assert.IsTrue(graph.RequestAction(5));
                        graph.Evaluate(input, speed); reference.Evaluate(input, speed); composed.Evaluate(input, speed);
                        graph.ReadRecordingPose(out var recording);
                        if (frame >= 120 && recording.groundTiming.SourceSeconds != previousSeconds) advances++;
                        if (variant == 1 && frame >= 120)
                        {
                            Assert.IsFalse(recording.weighted, "Diagnostics must identify the authored native mask.");
                            Assert.AreSame(actions.Action(5).mask, recording.mask);
                        }
                        previousSeconds = recording.groundTiming.SourceSeconds;
                        if (frame >= 180 && frame <= 200 && (frame-180)%4 == 0) Capture((frame-180)/4);
                        for (int b = 0; b < Bones.Length; b++)
                        {
                            var bone = rig.GetBoneTransform(Bones[b]); var target = peerRig.GetBoneTransform(Bones[b]);
                            if (!bone || !target) continue;
                            if (frame == 120) first[b] = bone.localRotation;
                            if (frame < 120) continue;
                            motion[b] = Mathf.Max(motion[b], Quaternion.Angle(first[b], bone.localRotation));
                            difference[b] = Mathf.Max(difference[b], Quaternion.Angle(target.localRotation, bone.localRotation));
                            composedDifference[b] = Mathf.Max(composedDifference[b], Quaternion.Angle(composedRig.GetBoneTransform(Bones[b]).localRotation, bone.localRotation));
                            if (b < 7 && variant == 1) maximumMaskedLegDifference = Mathf.Max(maximumMaskedLegDifference, composedDifference[b]);
                        }
                    }
                    for (int b = 0; b < Bones.Length; b++)
                        rows.Add(string.Join(",", speed.ToString(System.Globalization.CultureInfo.InvariantCulture), angle.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            variant, Bones[b], motion[b].ToString("F3",System.Globalization.CultureInfo.InvariantCulture),
                            difference[b].ToString("F3",System.Globalization.CultureInfo.InvariantCulture),
                            graph.FinalRotationContribution(5, (ActionMotionContext)(-1), Bones[b]).ToString("F3",System.Globalization.CultureInfo.InvariantCulture),
                            composedDifference[b].ToString("F3",System.Globalization.CultureInfo.InvariantCulture)));
                    Assert.Greater(advances, 400, "The source clock stopped during the sustained run.");
                    Assert.Greater(motion[1], 15, "Left thigh did not cycle.");
                    Assert.Greater(motion[2], 15, "Right thigh did not cycle.");
                    if (variant == 1)
                    {
                        graph.DebugIsolateLocomotion = true;
                        float before = graph.ReadActionTiming(5, ActionMotionContext.GroundedMoving).SourceSeconds;
                        for (int f = 540; f < 555; f++)
                            graph.Evaluate(new GroundMotionFrame(new AnimationFrameState(f, 1f/60, Vector3.forward*speed, 1, LocomotionMode.Grounded), true, 0, 0, false, speed), speed);
                        graph.ReadRecordingPose(out var isolated);
                        Assert.AreEqual(0, isolated.actionWeight); Assert.AreEqual("AirDash", graph.ActiveAction);
                        Assert.AreNotEqual(before, graph.ReadActionTiming(5, ActionMotionContext.GroundedMoving).SourceSeconds);
                        graph.DebugIsolateLocomotion = false;
                        graph.Evaluate(new GroundMotionFrame(new AnimationFrameState(555, 1f/60, Vector3.forward*speed, 1, LocomotionMode.Grounded), true, 0, 0, false, speed), speed);
                        graph.ReadRecordingPose(out var restored);
                        Assert.Greater(restored.actionWeight, .6f, "The active action did not return after ending isolation.");
                        graph.DebugIsolateLocomotion = true;
                        graph.CancelGroup("Dash");
                        for (int f = 556; f < 615; f++)
                            graph.Evaluate(new GroundMotionFrame(new AnimationFrameState(f, 1f/60, Vector3.forward*speed, 1, LocomotionMode.Grounded), true, 0, 0, false, speed), speed);
                        graph.DebugIsolateLocomotion = false;
                        Assert.AreEqual("None", graph.ActiveAction);
                    }
                    Assert.Less(actor.transform.position.sqrMagnitude, .000001);
                    if (sheet != null)
                    {
                        sheet.Apply();
                        File.WriteAllBytes("Logs/ProductionMigration/"+label+"-"+speed+"-"+angle+".png", sheet.EncodeToPNG());
                    }
                }
                finally { preview?.Cleanup(); if(inspectionMaterial)Object.DestroyImmediate(inspectionMaterial); if(sheet)Object.DestroyImmediate(sheet); if(actor)Object.DestroyImmediate(actor); if(peer)Object.DestroyImmediate(peer); Object.DestroyImmediate(composedPeer); Object.DestroyImmediate(actions); }
            }
            Directory.CreateDirectory("Logs/ProductionMigration");
            File.WriteAllLines("Logs/ProductionMigration/"+label+".csv",rows);
            Assert.Less(maximumMaskedLegDifference, .15f, "A masked action changed hips, thighs, shins or feet relative to the authored inactive-action composition.");
        }
    }
}
