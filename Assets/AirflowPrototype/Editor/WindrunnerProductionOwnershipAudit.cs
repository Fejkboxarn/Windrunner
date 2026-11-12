using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// Read-only ownership inventory. This deliberately does not infer that a
    /// component is redundant merely because another has a similar name.
    /// </summary>
    public static class WindrunnerProductionOwnershipAudit
    {
        private const string MenuPath =
            "Tools/Windrunner/Production/Batch 5P/3. Audit Player Ownership (Read-Only)";

        private static readonly string[] CoreTypes =
        {
            "PlayerMotor", "PlayerInputReader", "AirPower", "AirDash",
            "AirTargeter", "AirCaster", "AirFlowHitSystem",
            "AirSlingTargeter", "AirSlingController",
            "AirflowPortableAnimationBridge", "AirflowProductionFeedbackEmitter"
        };

        private static readonly string[] AnimationObservers =
        {
            "PlayerCustomAnimationDriver", "PlayerHumanoidAnimatorDriver",
            "AirflowPortableAnimationBridge", "PlayerRunningArmPoseController",
            "PlayerProceduralKnifeFlick", "PlayerDualThrowPose",
            "PlayerGroundedFootIK", "PlayerMovementVisuals"
        };

        private static readonly string[] LegacyWorkarounds =
        {
            "PlayerInputStartupRepair", "AirflowDebugRuntimeControls",
            "PlayerDebugHUD", "AirTargetDebugHUD"
        };

        [MenuItem(MenuPath)]
        public static void Audit()
        {
            PlayerMotor motor = ResolvePlayer();
            if (motor == null)
            {
                Debug.LogWarning("[Windrunner 5P Ownership] Select the intended scene player " +
                                 "with PlayerMotor. If multiple players exist, selection is required.");
                return;
            }

            Component[] root = motor.GetComponents<Component>();
            Component[] hierarchy = motor.GetComponentsInChildren<Component>(true);
            var byName = new Dictionary<string, List<Behaviour>>(StringComparer.Ordinal);
            int missing = 0;
            int enabled = 0;
            int disabled = 0;

            foreach (Component component in hierarchy)
            {
                if (component == null)
                {
                    missing++;
                    continue;
                }

                if (!(component is MonoBehaviour behaviour)) continue;
                if (behaviour.enabled && behaviour.gameObject.activeInHierarchy)
                    enabled++;
                else
                    disabled++;

                string name = component.GetType().Name;
                if (!byName.TryGetValue(name, out List<Behaviour> list))
                {
                    list = new List<Behaviour>(1);
                    byName.Add(name, list);
                }
                list.Add(behaviour);
            }

            var output = new StringBuilder(2500);
            output.Append("[Windrunner 5P Ownership] Player '").Append(motor.name)
                .Append("' — root components=").Append(root.Length)
                .Append(", entire hierarchy=").Append(hierarchy.Length)
                .Append(", behaviours active/disabled=").Append(enabled).Append('/')
                .Append(disabled).Append(", missing scripts=").Append(missing).AppendLine();

            int duplicatedCore = 0;
            output.AppendLine("Core ownership (1 each expected for a single player):");
            foreach (string name in CoreTypes)
            {
                byName.TryGetValue(name, out List<Behaviour> list);
                int count = list != null ? list.Count : 0;
                if (count > 1) duplicatedCore++;
                output.Append("  ").Append(name).Append(" = ").Append(count);
                if (count > 1) output.Append("  DUPLICATE — inspect manually");
                if (count == 0) output.Append("  not attached (might be optional)");
                output.AppendLine();
            }

            output.AppendLine("Animation/procedural inventory (not evidence of conflicting writers):");
            foreach (string name in AnimationObservers)
                AppendInventory(output, byName, name);

            output.AppendLine("Prototype/debug inventory (not automatically safe to delete):");
            foreach (string name in LegacyWorkarounds)
                AppendInventory(output, byName, name);

            output.AppendLine("Root components, in Inspector order:");
            foreach (Component component in root)
            {
                if (component == null)
                {
                    output.AppendLine("  <MISSING SCRIPT>");
                    continue;
                }

                output.Append("  ").Append(component.GetType().Name);
                if (component is Behaviour behaviour)
                    output.Append(behaviour.enabled ? " [enabled]" : " [disabled]");
                output.AppendLine();
            }

            output.Append("Read-only report. ")
                .Append(duplicatedCore > 0 || missing > 0
                    ? "REVIEW structural issues (duplicate core/missing script). "
                    : "No duplicate core types or missing scripts observed. ")
                .Append("Animation writer ownership, performance, behaviour, and safe " +
                        "removals are NOT proven by this inventory.");

            Debug.Log(output.ToString(), motor);
        }

        private static void AppendInventory(StringBuilder output,
            Dictionary<string, List<Behaviour>> index, string name)
        {
            output.Append("  ").Append(name).Append(" = ");
            if (!index.TryGetValue(name, out List<Behaviour> list))
            {
                output.AppendLine("0");
                return;
            }

            output.Append(list.Count);
            foreach (Behaviour behaviour in list)
            {
                output.Append(" [").Append(GetPath(behaviour.transform))
                    .Append(behaviour.enabled && behaviour.gameObject.activeInHierarchy
                        ? ": active]" : ": inactive]");
            }
            output.AppendLine();
        }

        private static string GetPath(Transform transform)
        {
            var parts = new List<string>();
            while (transform != null)
            {
                parts.Add(transform.name);
                transform = transform.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static PlayerMotor ResolvePlayer()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                PlayerMotor motor = selected.GetComponent<PlayerMotor>() ??
                                    selected.GetComponentInParent<PlayerMotor>();
                if (motor != null) return motor;
            }

            PlayerMotor[] motors = UnityEngine.Object.FindObjectsByType<PlayerMotor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return motors.Length == 1 ? motors[0] : null;
        }
    }
}
