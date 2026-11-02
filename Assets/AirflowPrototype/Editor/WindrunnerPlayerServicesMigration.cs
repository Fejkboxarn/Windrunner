using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// An opt-in, Undo-backed follow-up to Batch 5L. Moves feedback services
    /// and their existing generated children while preserving scene references.
    /// Never modifies the scene merely by being imported.
    /// </summary>
    public static class WindrunnerPlayerServicesMigration
    {
        private const string MenuRoot = "Tools/Windrunner/Production/Batch 5O/";
        private const string HostName = "PresentationFeedback";

        private static readonly Type[] Types =
        {
            typeof(AirAudioDirector),
            typeof(AirRewardGlowFeedback),
            typeof(AirPerfectTimingFeedback)
        };

        // Exact names emitted by AirAudioDirector.BuildSourcesIfNeeded.
        // Only direct child objects with AudioSource are moved.
        private static readonly string[] AudioNames =
        {
            "Audio - Footstep", "Audio - Jump", "Audio - Landing", "Audio - Flip",
            "Audio - Dash Start", "Audio - Target Ready", "Audio - Cast Release",
            "Audio - Node Hit", "Audio - Return Launch", "Audio - Reward Arrival",
            "Audio - Air Power Depleted", "Audio - Dash Loop", "Audio - Charge Loop",
            "Audio - Ambience", "Audio - Music"
        };

        [MenuItem(MenuRoot + "1. Audit Presentation Services")]
        public static void Audit()
        {
            PlayerMotor motor = ResolvePlayer();
            if (motor == null)
            {
                Debug.LogWarning("[Windrunner 5O] Select the intended scene player with PlayerMotor.");
                return;
            }

            GameObject player = motor.gameObject;
            Transform host = player.transform.Find(HostName);
            int onRoot = 0;
            int onHost = 0;
            int duplicates = 0;
            int missingLinks = 0;
            foreach (Type type in Types)
            {
                int rootCount = player.GetComponents(type).Length;
                int hostCount = host != null ? host.GetComponents(type).Length : 0;
                onRoot += rootCount;
                onHost += hostCount;
                if (rootCount + hostCount > 1) duplicates++;
                if (hostCount == 1)
                    missingLinks += CountMissingLinks(host.GetComponent(type));
            }

            int rootAudio = 0;
            int hostAudio = 0;
            int conflictingChildren = 0;
            foreach (string name in AudioNames)
            {
                int atRoot = CountNamedComponent(player.transform, name, typeof(AudioSource));
                int atHost = CountNamedComponent(host, name, typeof(AudioSource));
                rootAudio += atRoot;
                hostAudio += atHost;
                if (atRoot > 1 || atHost > 1 || atRoot > 0 && atHost > 0)
                    conflictingChildren++;
            }

            int rootLights = CountNamedComponent(player.transform, "Reward Glow Light", typeof(Light));
            int hostLights = CountNamedComponent(host, "Reward Glow Light", typeof(Light));
            if (rootLights > 1 || hostLights > 1 || rootLights > 0 && hostLights > 0)
                conflictingChildren++;

            bool pass = host != null && host.gameObject.activeInHierarchy && IsIdentityHost(host) &&
                        onRoot == 0 && onHost == Types.Length && duplicates == 0 &&
                        missingLinks == 0 && rootAudio == 0 && rootLights == 0 && conflictingChildren == 0;

            Debug.Log("[Windrunner 5O] " + (pass ? "PASS" : "INCOMPLETE") +
                      " — root components=" + player.GetComponents<Component>().Length +
                      ", services on root=" + onRoot + "/3, on PresentationFeedback=" +
                      onHost + "/3, duplicates=" + duplicates +
                      ", missing links=" + missingLinks +
                      ", generated audio root/host=" + rootAudio + "/" + hostAudio +
                      ", glow light root/host=" + rootLights + "/" + hostLights +
                      ", conflicting children=" + conflictingChildren +
                      ". Scene structure only; not audio, visual or timing parity.", player);
        }

        [MenuItem(MenuRoot + "2. Move Presentation Services (Undoable)")]
        public static void Migrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Windrunner 5O] Exit Play Mode before migrating.");
                return;
            }

            PlayerMotor motor = ResolvePlayer();
            if (motor == null || !motor.gameObject.scene.IsValid())
            {
                Debug.LogError("[Windrunner 5O] Select a scene player with PlayerMotor.");
                return;
            }

            GameObject player = motor.gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(player) ||
                PrefabUtility.IsPartOfPrefabAsset(player))
            {
                Debug.LogError("[Windrunner 5O] Prefab-backed players are blocked to " +
                               "avoid scene-only overrides. No changes made.", player);
                return;
            }

            Transform host = player.transform.Find(HostName);
            if (host == null || !host.gameObject.activeInHierarchy || !IsIdentityHost(host))
            {
                Debug.LogError("[Windrunner 5O] Requires the active, identity-transform " +
                               "PresentationFeedback child from Batch 5L. No changes made.", player);
                return;
            }

            var originals = new List<MonoBehaviour>(Types.Length);
            foreach (Type type in Types)
            {
                Component[] rootCopies = player.GetComponents(type);
                Component[] hostCopies = host.GetComponents(type);
                if (rootCopies.Length > 1 || hostCopies.Length > 1 ||
                    rootCopies.Length > 0 && hostCopies.Length > 0)
                {
                    Debug.LogError("[Windrunner 5O] Duplicate " + type.Name +
                                   " encountered. Resolve manually first.", player);
                    return;
                }

                if (rootCopies.Length == 1)
                    originals.Add((MonoBehaviour)rootCopies[0]);
                else if (hostCopies.Length == 0)
                {
                    Debug.LogError("[Windrunner 5O] Missing " + type.Name +
                                   ". Batch assumes this service was installed. " +
                                   "No changes were made.", player);
                    return;
                }
            }

            if (originals.Count == 0)
            {
                Debug.Log("[Windrunner 5O] All three services already left the root.", player);
                Audit();
                return;
            }

            // Do not alter existing user objects or nested prefab roots silently.
            foreach (string name in AudioNames)
            {
                if (!CheckGeneratedChild(player.transform, host, name, typeof(AudioSource)))
                    return;
            }
            if (!CheckGeneratedChild(player.transform, host, "Reward Glow Light", typeof(Light)))
                return;

            if (!EditorUtility.DisplayDialog("Windrunner 5O — Presentation Services",
                    "Move " + originals.Count + " feedback services into PresentationFeedback?\n\n" +
                    "The tool keeps serialized tuning and enabled flags, remaps references " +
                    "in loaded scenes, and reparents known generated audio sources and glow light.\n\n" +
                    "BACK UP OR COMMIT YOUR SCENE FIRST. Use Edit > Undo if anything looks wrong.",
                    "Move Services", "Cancel"))
                return;

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Windrunner 5O — Extract Presentation Services");
            try
            {
                var replacements = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
                foreach (MonoBehaviour original in originals)
                {
                    MonoBehaviour replacement = Undo.AddComponent(host.gameObject,
                        original.GetType()) as MonoBehaviour;
                    if (replacement == null)
                        throw new InvalidOperationException("Unable to add " + original.GetType().Name);

                    EditorUtility.CopySerialized(original, replacement);
                    replacement.enabled = original.enabled;
                    EnsureRequiredLinks(replacement, motor);
                    replacements.Add(original, replacement);
                    EditorUtility.SetDirty(replacement);
                }

                int remapped = RemapLoadedScenes(replacements);
                int movedAudio = 0;
                if (player.GetComponent<AirAudioDirector>() != null)
                {
                    foreach (string name in AudioNames)
                    {
                        Transform child = FindNamedComponent<AudioSource>(player.transform, name);
                        if (child != null)
                        {
                            Undo.SetTransformParent(child, host, "Move generated audio source");
                            movedAudio++;
                        }
                    }
                }

                bool movedLight = false;
                if (player.GetComponent<AirRewardGlowFeedback>() != null)
                {
                    Transform light = FindNamedComponent<Light>(player.transform, "Reward Glow Light");
                    if (light != null)
                    {
                        Undo.SetTransformParent(light, host, "Move reward glow light");
                        movedLight = true;
                    }
                }

                foreach (MonoBehaviour original in originals)
                    Undo.DestroyObjectImmediate(original);

                EditorSceneManager.MarkSceneDirty(player.scene);
                Selection.activeGameObject = player;
                Undo.CollapseUndoOperations(group);
                Debug.Log("[Windrunner 5O] Moved " + originals.Count +
                          " services; remapped " + remapped + " serialized references, " +
                          "moved " + movedAudio + " generated audio sources and glow light=" +
                          movedLight + ". Validate structure and all live feedback.", player);
                Audit();
            }
            catch (Exception error)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(error);
                Debug.LogError("[Windrunner 5O] Migration failed. Undo rollback requested; " +
                               "inspect the scene before saving.");
            }
        }

        private static bool CheckGeneratedChild(Transform player, Transform host,
            string name, Type expectedType)
        {
            int rootCount = CountNamedComponent(player, name, expectedType);
            int hostCount = CountNamedComponent(host, name, expectedType);
            if (rootCount > 1 || hostCount > 1)
            {
                Debug.LogError("[Windrunner 5O] Multiple generated children named '" +
                               name + "' already exist. Resolve them before migration.", player);
                return false;
            }

            Transform rootChild = FindNamedComponent(player, name, expectedType);
            Transform hostChild = host.Find(name);
            if (rootCount == 0)
                return true;

            if (hostChild != null)
            {
                Debug.LogError("[Windrunner 5O] Both root and PresentationFeedback contain '" +
                               name + "'. Resolve duplicate before migration.", rootChild);
                return false;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(rootChild))
            {
                Debug.LogError("[Windrunner 5O] Generated child '" + name +
                               "' is a nested prefab instance; migration blocked.", rootChild);
                return false;
            }

            return true;
        }

        private static int CountNamedComponent(Transform parent, string name, Type type)
        {
            if (parent == null)
                return 0;

            int count = 0;
            foreach (Transform child in parent)
            {
                if (child.name == name && child.GetComponent(type) != null)
                    count++;
            }
            return count;
        }

        private static Transform FindNamedComponent<T>(Transform parent, string name)
            where T : Component
        {
            return FindNamedComponent(parent, name, typeof(T));
        }

        private static Transform FindNamedComponent(Transform parent, string name, Type type)
        {
            if (parent == null)
                return null;
            foreach (Transform child in parent)
            {
                if (child.name == name && child.GetComponent(type) != null)
                    return child;
            }
            return null;
        }

        private static bool IsIdentityHost(Transform host)
        {
            return host.localPosition.sqrMagnitude < 0.000001f &&
                   Quaternion.Angle(host.localRotation, Quaternion.identity) < 0.001f &&
                   (host.localScale - Vector3.one).sqrMagnitude < 0.000001f;
        }

        private static PlayerMotor ResolvePlayer()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                PlayerMotor player = selected.GetComponent<PlayerMotor>() ??
                                     selected.GetComponentInParent<PlayerMotor>();
                if (player != null)
                    return player;
            }

            PlayerMotor[] all = UnityEngine.Object.FindObjectsByType<PlayerMotor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.Length == 1 ? all[0] : null;
        }

        private static void EnsureRequiredLinks(Component component, PlayerMotor motor)
        {
            SerializedObject obj = new SerializedObject(component);
            if (component is AirAudioDirector)
            {
                LinkIfMissing(obj, "motor", motor);
                LinkIfMissing(obj, "movementVisuals", motor.GetComponent<PlayerMovementVisuals>());
                LinkIfMissing(obj, "caster", motor.GetComponent<AirCaster>());
                LinkIfMissing(obj, "hitSystem", motor.GetComponent<AirFlowHitSystem>());
                LinkIfMissing(obj, "airPower", motor.GetComponent<AirPower>());
            }
            else if (component is AirRewardGlowFeedback)
            {
                LinkIfMissing(obj, "hitSystem", motor.GetComponent<AirFlowHitSystem>());
                Transform visuals = motor.transform.Find("Capsule Animation Root");
                LinkIfMissing(obj, "visualRoot", visuals != null ? visuals : motor.transform);
            }
            else if (component is AirPerfectTimingFeedback)
            {
                LinkIfMissing(obj, "caster", motor.GetComponent<AirCaster>());
            }

            obj.ApplyModifiedProperties();
        }

        private static void LinkIfMissing(SerializedObject obj, string name, UnityEngine.Object value)
        {
            SerializedProperty property = obj.FindProperty(name);
            if (property != null && property.objectReferenceValue == null && value != null)
            {
                Undo.RecordObject(obj.targetObject, "Connect Presentation Service");
                property.objectReferenceValue = value;
            }
        }

        private static int CountMissingLinks(Component component)
        {
            SerializedObject obj = new SerializedObject(component);
            if (component is AirAudioDirector)
                return Missing(obj, "motor") + Missing(obj, "movementVisuals") +
                       Missing(obj, "caster") + Missing(obj, "hitSystem") + Missing(obj, "airPower");
            if (component is AirRewardGlowFeedback)
                return Missing(obj, "hitSystem") + Missing(obj, "visualRoot");
            if (component is AirPerfectTimingFeedback)
                return Missing(obj, "caster");
            return 0;
        }

        private static int Missing(SerializedObject obj, string name)
        {
            SerializedProperty property = obj.FindProperty(name);
            return property == null || property.objectReferenceValue == null ? 1 : 0;
        }

        private static int RemapLoadedScenes(
            Dictionary<UnityEngine.Object, UnityEngine.Object> replacements)
        {
            int mapped = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    {
                        if (component == null || replacements.ContainsKey(component))
                            continue;

                        SerializedObject obj = new SerializedObject(component);
                        SerializedProperty iterator = obj.GetIterator();
                        bool changed = false;
                        while (iterator.Next(true))
                        {
                            if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                                continue;

                            UnityEngine.Object old = iterator.objectReferenceValue;
                            if (old == null ||
                                !replacements.TryGetValue(old, out UnityEngine.Object next))
                                continue;

                            if (!changed)
                                Undo.RecordObject(component, "Remap Presentation Service Reference");
                            iterator.objectReferenceValue = next;
                            changed = true;
                            mapped++;
                        }

                        if (changed)
                        {
                            obj.ApplyModifiedProperties();
                            EditorUtility.SetDirty(component);
                            if (PrefabUtility.IsPartOfPrefabInstance(component))
                                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                            EditorSceneManager.MarkSceneDirty(scene);
                        }
                    }
                }
            }

            return mapped;
        }
    }
}
