using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Windrunner.Production.Animation;
using Windrunner.Production.Feedback;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// Idempotent Batch 4 scene wiring and conservative migration gates.
    /// No script disables a legacy Animator during installation.
    /// </summary>
    public static class WindrunnerAnimationSetup
    {
        private const string Prefix = "Tools/Windrunner/Production/";
        private const string ProfilePath =
            "Assets/WindrunnerProduction/Animation/Generated/WindrunnerAnimationProfile.asset";

        [MenuItem(Prefix + "Batch 4 - Install Animation Bridge")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[Animation Setup] Install in Edit Mode, not Play Mode.");
                return;
            }
            AirCaster[] players = Object.FindObjectsByType<AirCaster>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (players.Length != 1)
            {
                Debug.LogError("[Animation Setup] Expected exactly one AirCaster player. " +
                               "Found " + players.Length + ".");
                return;
            }
            PlayerMotor motor = players[0].GetComponent<PlayerMotor>();
            if (motor == null)
            {
                Debug.LogError("[Animation Setup] AirCaster has no PlayerMotor.");
                return;
            }
            PlayerHumanoidAnimatorDriver legacy =
                players[0].GetComponent<PlayerHumanoidAnimatorDriver>();
            Animator animator = legacy != null ? legacy.Animator : null;
            if (animator == null)
            {
                Animator[] candidates = motor.GetComponentsInChildren<Animator>(true)
                    .Where(a => a.avatar != null && a.avatar.isHuman && a.avatar.isValid)
                    .ToArray();
                if (candidates.Length != 1)
                {
                    Debug.LogError("[Animation Setup] Cannot uniquely resolve the character's " +
                                   "Humanoid Animator. Candidates=" + candidates.Length +
                                   ". Assign one to the bridge manually.");
                    return;
                }
                animator = candidates[0];
            }

            WindrunnerAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<WindrunnerAnimationProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WindrunnerAnimationProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                AssetDatabase.SaveAssets();
            }

            AirflowPortableAnimationBridge bridge =
                motor.GetComponent<AirflowPortableAnimationBridge>();
            if (bridge == null) bridge = Undo.AddComponent<AirflowPortableAnimationBridge>(
                motor.gameObject);
            Camera camera = Camera.main;
            bridge.Configure(profile, motor, motor.GetComponent<AirSlingController>(),
                animator, camera, legacy);
            EditorUtility.SetDirty(bridge);
            EditorSceneManager.MarkSceneDirty(bridge.gameObject.scene);
            Selection.activeGameObject = bridge.gameObject;
            Debug.Log("[Animation Setup] Batch 4 bridge installed in OBSERVATION MODE. " +
                      "Legacy PlayerHumanoidAnimatorDriver, procedural pose scripts, " +
                      "AnimatorController and physics are still untouched. Save the scene.");
        }

        [MenuItem(Prefix + "Batch 4 - Select Animation Profile")]
        public static void SelectProfile()
        {
            WindrunnerAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<WindrunnerAnimationProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError("[Animation Setup] First run Batch 4 - Install Animation Bridge.");
                return;
            }
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        [MenuItem(Prefix + "Batch 4 - Audit Animation Bridge")]
        public static void Audit()
        {
            int errors = 0;
            AirflowPortableAnimationBridge[] bridges =
                Object.FindObjectsByType<AirflowPortableAnimationBridge>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bridges.Length != 1)
            {
                Debug.LogError("[Animation Audit] Expected one bridge. Found " +
                               bridges.Length + ".");
                errors++;
            }
            else
            {
                AirflowPortableAnimationBridge bridge = bridges[0];
                Animator animator = bridge.HumanoidAnimator;
                if (animator == null || !animator.isHuman || animator.avatar == null ||
                    !animator.avatar.isValid || !animator.avatar.isHuman)
                {
                    Debug.LogError("[Animation Audit] A valid Humanoid Animator is required.");
                    errors++;
                }
                WindrunnerAnimationProfile profile = bridge.Profile;
                if (profile == null)
                {
                    Debug.LogError("[Animation Audit] Animation profile missing.");
                    errors++;
                }
                else
                {
                    bool ready = profile.ValidateForLiveGraph(out string reason);
                    Debug.Log("[Animation Audit] Portable profile ready=" + ready +
                              (ready ? "" : ", reason=" + reason));
                    if (profile.automaticallyStartPortableInPlayMode && !ready)
                    {
                        Debug.LogError("[Animation Audit] Auto-start requested without a valid graph.");
                        errors++;
                    }
                }
                if (Application.isPlaying)
                    bridge.ReportCounters();
                else if (bridge.PortableActive)
                {
                    Debug.LogError("[Animation Audit] Portable graph unexpectedly active in Edit Mode.");
                    errors++;
                }
                PlayerMovementVisuals visuals =
                    bridge.GetComponent<PlayerMovementVisuals>();
                if (visuals != null && visuals.LegacyProceduralAnimationEnabled)
                    Debug.Log("[Animation Audit] Legacy procedural presentation remains enabled. " +
                              "Expected for observation mode; live graph takeover is blocked " +
                              "until you explicitly disable it for comparison.");
                if (Object.FindObjectsByType<WindrunnerFeedbackHost>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                {
                    Debug.LogWarning("[Animation Audit] Missing/duplicate feedback host; " +
                                     "semantic observation may be unavailable.");
                }
            }
            Debug.Log(errors == 0 ?
                "[Animation Audit] PASS — bridge scene wiring. Live graph " +
                "activation and clip-quality parity are separate tests." :
                "[Animation Audit] FAILED: " + errors + " issue(s).");
        }

        [MenuItem(Prefix + "Batch 4 - Report Animation Counters (Play Mode)")]
        public static void Report()
        {
            AirflowPortableAnimationBridge bridge = FindBridge();
            bridge?.ReportCounters();
        }

        [MenuItem(Prefix + "Batch 4 - Try Portable Graph (Play Mode)")]
        public static void TryStart()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[Animation Setup] Enter Play Mode first.");
                return;
            }
            AirflowPortableAnimationBridge bridge = FindBridge();
            if (bridge != null) bridge.TryStartPortable();
        }

        [MenuItem(Prefix + "Batch 4 - Restore Legacy Animator (Play Mode)")]
        public static void RestoreLegacy()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[Animation Setup] Enter Play Mode first.");
                return;
            }
            AirflowPortableAnimationBridge bridge = FindBridge();
            if (bridge != null) bridge.StopPortable();
        }

        private static AirflowPortableAnimationBridge FindBridge()
        {
            AirflowPortableAnimationBridge[] bridges =
                Object.FindObjectsByType<AirflowPortableAnimationBridge>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bridges.Length != 1)
            {
                Debug.LogError("[Animation Setup] Expected exactly one bridge.");
                return null;
            }
            return bridges[0];
        }
    }
}
