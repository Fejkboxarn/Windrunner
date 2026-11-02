using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Windrunner.Production.Feedback;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// Editor-only FEEL integration verification. Synthetic events go through
    /// FeedbackService but never enter the game's catalog or service registry.
    /// </summary>
    [InitializeOnLoad]
    public static class WindrunnerFeelValidation
    {
        private const string Prefix = "Tools/Windrunner/Production/";
        private const string DirectoryPath =
            "Assets/WindrunnerProduction/Feedback/Generated/Diagnostics";
        private const string ProfilePath = DirectoryPath + "/FeelValidationProfile.asset";
        private const string PrefabPath = DirectoryPath + "/FeelValidation_Pause.prefab";

        private sealed class StressRun : IDisposable
        {
            public FeelValidationProbe Probe;
            public FeelValidationProfile Profile;
            public IFeedbackRecipeDiagnostics Diagnostics;
            public FeedbackBackendDiagnostics Before;
            public FeedbackBackendDiagnostics AfterFirstBurst;
            public Vector3 Position;
            public Quaternion Rotation;
            public float NextAt;
            public int Phase;
            public int LivePlayedBefore;

            public void Dispose()
            {
                Probe?.Dispose();
                Probe = null;
            }
        }

        private static StressRun activeRun;

        static WindrunnerFeelValidation()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingPlayMode)
                    CancelRun("Play Mode stopped; probe canceled.");
            };
        }

        [MenuItem(Prefix + "Batch 3.5 - Create FEEL Validation Recipe")]
        public static void CreateProbe()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[FEEL Probe] Create the recipe in Edit Mode.");
                return;
            }
            Type playerType = FindType("MoreMountains.Feedbacks.MMF_Player");
            Type pauseType = FindType("MoreMountains.Feedbacks.MMF_Pause");
            if (playerType == null || pauseType == null)
            {
                Debug.LogError("[FEEL Probe] Licensed FEEL is missing MMF_Player or MMF_Pause. " +
                               "Import FEEL and allow Unity to finish compiling.");
                return;
            }

            if (!Directory.Exists(DirectoryPath)) Directory.CreateDirectory(DirectoryPath);
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                GameObject draft = new GameObject("Windrunner FEEL Validation Pause");
                try
                {
                    Component player = draft.AddComponent(playerType);
                    if (player == null) throw new InvalidOperationException("Cannot add MMF_Player.");
                    SetBool(player, "AutoPlayOnEnable", false);
                    SetBool(player, "AutoPlayOnStart", false);
                    MethodInfo addFeedback = playerType.GetMethod("AddFeedback",
                        new[] { typeof(Type), typeof(bool) });
                    if (addFeedback == null)
                        throw new MissingMethodException("MMF_Player.AddFeedback(Type, bool) not found.");
                    object feedback = addFeedback.Invoke(player, new object[] { pauseType, true });
                    if (feedback == null)
                        throw new InvalidOperationException("Failed to add FEEL Pause feedback.");
                    FieldInfo durationField = pauseType.GetField("PauseDuration");
                    if (durationField == null)
                        throw new MissingFieldException("MMF_Pause.PauseDuration not found.");
                    durationField.SetValue(feedback, 0.35f);
                    prefab = PrefabUtility.SaveAsPrefabAsset(draft, PrefabPath);
                    if (prefab == null)
                        throw new InvalidOperationException("Failed to save FEEL validation prefab.");
                }
                catch (Exception ex)
                {
                    Debug.LogError("[FEEL Probe] Could not generate FEEL test prefab: " +
                                   (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                    return;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(draft);
                }
            }

            FeelValidationProfile profile =
                AssetDatabase.LoadAssetAtPath<FeelValidationProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<FeelValidationProfile>();
                profile.recipe.prefab = prefab;
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            else if (profile.recipe == null || profile.recipe.prefab == null)
            {
                // Repair only an absent reference, preserving all user settings.
                if (profile.recipe == null) profile.recipe = new FeedbackRecipeEntry();
                profile.recipe.prefab = prefab;
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
            Debug.Log("[FEEL Probe] Validation profile ready: " + ProfilePath +
                      ". Built-in FEEL Pause (0.35s) has no gameplay/audio/camera side effects. " +
                      "Existing recipes and production flags were not touched.");
        }

        [MenuItem(Prefix + "Batch 3.5 - Audit FEEL Connection")]
        public static void Audit()
        {
            int errors = 0;
            bool hasFeel = FindType("MoreMountains.Feedbacks.MMF_Player") != null;
            bool hasAdapterType = FindType(
                "Windrunner.Production.Integrations.Feel.WindrunnerFeelBackendAdapter") != null;
            NamedBuildTarget buildTarget = NamedBuildTarget.FromBuildTargetGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup);
            string[] symbols = PlayerSettings.GetScriptingDefineSymbols(buildTarget)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            bool hasDefine = symbols.Any(s => s.Trim() == "WINDRUNNER_FEEL");

            if (!hasFeel) { Debug.LogError("[FEEL Probe] FEEL not imported."); errors++; }
            if (!hasDefine) { Debug.LogError("[FEEL Probe] WINDRUNNER_FEEL define is not enabled."); errors++; }
            if (!hasAdapterType) { Debug.LogError("[FEEL Probe] FEEL adapter did not compile."); errors++; }

            WindrunnerFeedbackHost[] hosts = UnityEngine.Object.FindObjectsByType<
                WindrunnerFeedbackHost>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (hosts.Length != 1 ||
                !(hosts[0].FeelBackendComponent is IFeedbackRecipeBackend) ||
                !(hosts[0].FeelBackendComponent is IFeedbackRecipeDiagnostics))
            {
                Debug.LogError("[FEEL Probe] Expected one host with the compiled FEEL backend. " +
                               "Run Batch 03 - Install Feedback in the gameplay scene.");
                errors++;
            }

            FeelValidationProfile profile =
                AssetDatabase.LoadAssetAtPath<FeelValidationProfile>(ProfilePath);
            if (profile == null || profile.recipe == null || profile.recipe.prefab == null)
            {
                Debug.LogError("[FEEL Probe] Validation asset missing. " +
                               "Run Batch 3.5 - Create FEEL Validation Recipe.");
                errors++;
            }
            else if (FindType("MoreMountains.Feedbacks.MMF_Player") is Type playerType)
            {
                Component player = profile.recipe.prefab.GetComponentInChildren(playerType, true);
                if (player == null)
                {
                    Debug.LogError("[FEEL Probe] Probe prefab has no MMF_Player.");
                    errors++;
                }
                else
                {
                    FieldInfo feedbackList = playerType.GetField("FeedbacksList");
                    var contents = feedbackList?.GetValue(player) as System.Collections.IEnumerable;
                    bool hasPause = contents != null && contents.Cast<object>().Any(
                        feedback => feedback != null &&
                                    feedback.GetType().FullName ==
                                    "MoreMountains.Feedbacks.MMF_Pause");
                    if (!hasPause)
                    {
                        Debug.LogError("[FEEL Probe] Probe prefab is missing its FEEL Pause. " +
                                       "Inspect the MMF_Player. The generator will not overwrite edits.");
                        errors++;
                    }
                }
            }

            Debug.Log(errors == 0 ?
                "[FEEL Probe] AUDIT PASS — imported, compiled, connected, and test recipe present. " +
                "Runtime stress test still required." :
                "[FEEL Probe] AUDIT FAILED with " + errors + " issue(s).");
        }

        [MenuItem(Prefix + "Batch 3.5 - Play FEEL Probe Once (Play Mode)")]
        public static void PlayOnce()
        {
            if (!TryStart(false, out StressRun run)) return;
            try
            {
                run.Probe.Fire(run.Position, run.Rotation, run.Profile.firstIntensity);
                FeedbackBackendDiagnostics after = run.Diagnostics.Snapshot();
                int started = after.Started - run.Before.Started;
                int playerPlays = after.PlayerPlayCount - run.Before.PlayerPlayCount;
                Debug.Log("[FEEL Probe] Single synthetic FeedbackService event. " +
                          "Backend starts=" + started + ", MMF_Player.PlayCount delta=" +
                          playerPlays + "; " + after +
                          ". Live Windrunner production counters remain unaffected.");
                if (started < 1 || playerPlays < 1)
                    Debug.LogError("[FEEL Probe] FEEL playback did not start. Check the Console and prefab.");
                else
                    Debug.Log("[FEEL Probe] Single-play PASS. Next, run the pool stress test.");
            }
            finally
            {
                run.Dispose();
            }
        }

        [MenuItem(Prefix + "Batch 3.5 - Stress FEEL Pool (Play Mode)")]
        public static void Stress()
        {
            if (!TryStart(true, out StressRun run)) return;
            activeRun = run;
            try
            {
                run.Probe.Fire(run.Position, run.Rotation, run.Profile.firstIntensity);
                run.Phase = 0;
                run.NextAt = Time.realtimeSinceStartup + run.Profile.releaseWaitSeconds;
                EditorApplication.update += AdvanceStress;
                Debug.Log("[FEEL Probe] Stress started. First event fired. " +
                          "Waiting for release, then two bursts of " + run.Profile.burstSize +
                          " synthetic events. Do not exit Play Mode until PASS/FAIL.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                CancelRun("Stress start failed.");
            }
        }

        private static bool TryStart(bool stress, out StressRun run)
        {
            run = null;
            if (activeRun != null)
            {
                Debug.LogError("[FEEL Probe] A stress test is already running.");
                return false;
            }
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[FEEL Probe] Enter Play Mode and open the gameplay scene.");
                return false;
            }

            WindrunnerFeedbackHost host = WindrunnerFeedbackHost.Active;
            IFeedbackRecipeBackend backend = host != null ?
                host.FeelBackendComponent as IFeedbackRecipeBackend : null;
            IFeedbackRecipeDiagnostics diagnostics = backend as IFeedbackRecipeDiagnostics;
            FeelValidationProfile profile =
                AssetDatabase.LoadAssetAtPath<FeelValidationProfile>(ProfilePath);
            if (host == null || host.Service == null || backend == null || diagnostics == null ||
                profile == null || profile.recipe == null || profile.recipe.prefab == null)
            {
                Debug.LogError("[FEEL Probe] Incomplete host/FEEL/profile. " +
                               "Run the Batch 3.5 connection audit in Edit Mode first.");
                return false;
            }
            if (profile.recipe.maxPoolSize < 1 ||
                profile.recipe.prewarmCount > profile.recipe.maxPoolSize)
            {
                Debug.LogError("[FEEL Probe] Set pool cap >= 1 and prewarm count <= cap.");
                return false;
            }
            if (stress && profile.releaseWaitSeconds <= profile.recipe.safetyReleaseSeconds)
            {
                Debug.LogError("[FEEL Probe] Release wait must exceed safety release seconds.");
                return false;
            }
            try
            {
                FeelValidationProbe probe = new FeelValidationProbe(backend, profile.recipe);
                float scale = probe.Settings.Scale(profile.recipe.category);
                if (scale <= 0f || profile.recipe.intensityMultiplier <= 0f)
                {
                    Debug.LogError("[FEEL Probe] Recipe intensity/category scale is zero. " +
                                   "Restore category scale before testing.");
                    probe.Dispose();
                    return false;
                }
                FeedbackBackendDiagnostics baseline = diagnostics.Snapshot();
                if (stress && baseline.Active != 0)
                {
                    Debug.LogWarning("[FEEL Probe] FEEL pool still has " + baseline.Active +
                                     " active instance(s). Wait a moment after the single-play " +
                                     "test, then rerun stress from an idle pool.");
                    probe.Dispose();
                    return false;
                }
                run = new StressRun
                {
                    Probe = probe, Profile = profile, Diagnostics = diagnostics,
                    Before = baseline,
                    Position = host.transform.position + Vector3.up * 1.5f,
                    Rotation = host.transform.rotation,
                    LivePlayedBefore = host.Service.PlayedCount
                };
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        private static void AdvanceStress()
        {
            StressRun run = activeRun;
            if (run == null || !EditorApplication.isPlaying)
            {
                CancelRun("Play Mode exited.");
                return;
            }
            if (Time.realtimeSinceStartup < run.NextAt) return;
            try
            {
                if (run.Phase == 0)
                {
                    FireBurst(run);
                    run.AfterFirstBurst = run.Diagnostics.Snapshot();
                    Debug.Log("[FEEL Probe] Burst 1: " + run.AfterFirstBurst);
                    run.Phase = 1;
                }
                else if (run.Phase == 1)
                {
                    if (run.Diagnostics.Snapshot().Active != run.Before.Active)
                        Debug.LogWarning("[FEEL Probe] Pool not idle before reuse phase.");
                    FireBurst(run);
                    Debug.Log("[FEEL Probe] Burst 2: " + run.Diagnostics.Snapshot());
                    run.Phase = 2;
                }
                else
                {
                    FinishStress(run);
                    CancelRun(null);
                    return;
                }
                run.NextAt = Time.realtimeSinceStartup + run.Profile.releaseWaitSeconds;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                CancelRun("Stress threw an exception.");
            }
        }

        private static void FireBurst(StressRun run)
        {
            for (int i = 0; i < run.Profile.burstSize; i++)
                run.Probe.Fire(run.Position, run.Rotation, run.Profile.burstIntensity);
        }

        private static void FinishStress(StressRun run)
        {
            FeedbackBackendDiagnostics after = run.Diagnostics.Snapshot();
            int started = after.Started - run.Before.Started;
            int created = after.Created - run.Before.Created;
            int reused = after.Reused - run.Before.Reused;
            int rejected = after.Rejected - run.Before.Rejected;
            int playerPlays = after.PlayerPlayCount - run.Before.PlayerPlayCount;
            int expectedEvents = 1 + run.Profile.burstSize * 2;
            int cap = Mathf.Max(1, run.Profile.recipe.maxPoolSize);
            float expectedIntensity = run.Profile.burstIntensity *
                                      run.Profile.recipe.intensityMultiplier *
                                      run.Probe.Settings.Scale(run.Profile.recipe.category);
            bool bounded = created <= cap;
            bool played = started > 1 && playerPlays >= started;
            bool reusedPool = reused > 0;
            bool drained = after.Active == run.Before.Active;
            bool scaled = Mathf.Abs(after.LastIntensity - expectedIntensity) < 0.02f;
            bool positioned = Vector3.Distance(after.LastPosition, run.Position) < 0.02f;
            bool routed = run.Probe.ObservedCount == expectedEvents &&
                          run.Probe.AcceptedCount == expectedEvents;
            bool overflow = run.Profile.recipe.overflow != FeedbackPoolOverflow.RejectNewest ||
                            run.Profile.burstSize <= cap || rejected > 0;
            bool pass = bounded && played && reusedPool && drained && scaled && positioned &&
                        routed && overflow;

            Debug.Log("[FEEL Probe] FINAL — " + after +
                      ". Events=" + run.Probe.ObservedCount +
                      ", accepted=" + run.Probe.AcceptedCount +
                      ", FEEL starts=" + started + ", created=" + created +
                      ", reused=" + reused + ", rejected=" + rejected +
                      ", expected last intensity=" + expectedIntensity.ToString("F2") +
                      ". Checks: bounded=" + bounded + ", FEEL played=" + played +
                      ", pool reuse=" + reusedPool + ", pool drained=" + drained +
                      ", intensity=" + scaled + ", position=" + positioned +
                      ", synthetic route=" + routed +
                      ", overflow handled=" + overflow);
            if (pass)
                Debug.Log("[FEEL Probe] PASS — real FEEL playback, semantic routing, " +
                          "intensity, pool cap/reuse and release verified. " +
                          "This does NOT validate artistic effect parity.");
            else
                Debug.LogError("[FEEL Probe] FAIL — investigate failed checks above. " +
                               "No gameplay feedback event settings were changed.");
        }

        private static void CancelRun(string reason)
        {
            EditorApplication.update -= AdvanceStress;
            activeRun?.Dispose();
            activeRun = null;
            if (!string.IsNullOrEmpty(reason)) Debug.LogWarning("[FEEL Probe] " + reason);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type result = assembly.GetType(fullName, false);
                if (result != null) return result;
            }
            return null;
        }

        private static void SetBool(Component component, string member, bool value)
        {
            Type type = component.GetType();
            FieldInfo field = type.GetField(member);
            if (field != null && field.FieldType == typeof(bool))
                field.SetValue(component, value);
            else
            {
                PropertyInfo property = type.GetProperty(member);
                if (property != null && property.PropertyType == typeof(bool) &&
                    property.CanWrite) property.SetValue(component, value);
            }
        }
    }
}
