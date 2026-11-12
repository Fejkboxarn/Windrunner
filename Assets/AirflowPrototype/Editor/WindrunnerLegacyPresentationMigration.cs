using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// Opt-in quarantine for three superseded, disabled prototype visuals.
    /// No deletion, no behavior enablement, no scene mutation on import.
    /// </summary>
    public static class WindrunnerLegacyPresentationMigration
    {
        private const string MenuRoot = "Tools/Windrunner/Production/Batch 5P/";
        private const string HostName = "LegacyPresentation";

        private static readonly Type[] LegacyTypes =
        {
            typeof(AirTargetIndicator),
            typeof(AirCastFeedback),
            typeof(AirChargeVisual)
        };

        private static readonly string[] RingNames =
        {
            "Air Compression Ring 1", "Air Compression Ring 2", "Air Compression Ring 3"
        };

        [MenuItem(MenuRoot + "1. Audit Legacy Presentation")]
        public static void Audit()
        {
            PlayerMotor motor = ResolvePlayer();
            if (motor == null)
            {
                Debug.LogWarning("[Windrunner 5P] Select the intended scene player with PlayerMotor.");
                return;
            }

            Transform root = motor.transform;
            Transform host = root.Find(HostName);
            int rootCount = 0;
            int hostedCount = 0;
            int enabledCount = 0;
            int duplicates = 0;
            int missingLinks = 0;

            foreach (Type type in LegacyTypes)
            {
                Component[] onRoot = motor.GetComponents(type);
                Component[] onHost = host != null ? host.GetComponents(type) : Array.Empty<Component>();
                rootCount += onRoot.Length;
                hostedCount += onHost.Length;
                if (onRoot.Length + onHost.Length > 1) duplicates++;

                foreach (MonoBehaviour item in onRoot)
                    if (item.enabled) enabledCount++;
                foreach (MonoBehaviour item in onHost)
                {
                    if (item.enabled) enabledCount++;
                    missingLinks += MissingRequiredLinks(item);
                }
            }

            int rootRings = 0;
            int hostedRings = 0;
            int ringDuplicates = 0;
            foreach (string name in RingNames)
            {
                int a = CountNamedLines(root, name);
                int b = CountNamedLines(host, name);
                rootRings += a;
                hostedRings += b;
                if (a > 1 || b > 1 || a > 0 && b > 0)
                    ringDuplicates++;
            }

            bool passed = host != null && host.gameObject.activeInHierarchy && IsIdentityHost(host) &&
                          rootCount == 0 && hostedCount == LegacyTypes.Length &&
                          enabledCount == 0 && duplicates == 0 && missingLinks == 0 &&
                          rootRings == 0 && ringDuplicates == 0;

            Debug.Log("[Windrunner 5P] " + (passed ? "PASS" : "INCOMPLETE") +
                      " — legacy on player root=" + rootCount + "/3, on " + HostName +
                      "=" + hostedCount + "/3, enabled=" + enabledCount +
                      ", duplicate types=" + duplicates + ", missing links=" + missingLinks +
                      ", old rings root/host=" + rootRings + "/" + hostedRings +
                      ", ring collisions=" + ringDuplicates +
                      ". Structural quarantine only; gameplay authority not modified.", motor);
        }

        [MenuItem(MenuRoot + "2. Quarantine Disabled Prototype Visuals (Undoable)")]
        public static void Migrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Windrunner 5P] Exit Play Mode before migration.");
                return;
            }

            PlayerMotor motor = ResolvePlayer();
            if (motor == null || !motor.gameObject.scene.IsValid())
            {
                Debug.LogError("[Windrunner 5P] Select a scene player containing PlayerMotor.");
                return;
            }

            GameObject player = motor.gameObject;
            if (PrefabUtility.IsPartOfPrefabAsset(player) ||
                PrefabUtility.IsPartOfPrefabInstance(player))
            {
                Debug.LogError("[Windrunner 5P] Prefab-backed player blocked. " +
                               "No scene-only overrides are performed.", player);
                return;
            }

            Transform presentation = motor.transform.Find("PresentationFeedback");
            if (presentation == null || !presentation.gameObject.activeInHierarchy)
            {
                Debug.LogError("[Windrunner 5P] Expected the PresentationFeedback host " +
                               "from passing 5L/5O. No changes made.", player);
                return;
            }

            Transform existingHost = motor.transform.Find(HostName);
            if (existingHost != null &&
                (!existingHost.gameObject.activeInHierarchy || !IsIdentityHost(existingHost)))
            {
                Debug.LogError("[Windrunner 5P] Existing LegacyPresentation is inactive " +
                               "or transformed. Correct that before migration.", existingHost);
                return;
            }

            var originals = new List<MonoBehaviour>(LegacyTypes.Length);
            foreach (Type type in LegacyTypes)
            {
                Component[] onRoot = player.GetComponents(type);
                Component[] onHost = existingHost != null
                    ? existingHost.GetComponents(type) : Array.Empty<Component>();

                if (onRoot.Length + onHost.Length > 1)
                {
                    Debug.LogError("[Windrunner 5P] Duplicate " + type.Name +
                                   ". Resolve it before migration.", player);
                    return;
                }

                if (onRoot.Length == 0 && onHost.Length == 0)
                {
                    Debug.LogError("[Windrunner 5P] Missing " + type.Name +
                                   ". This guarded migration requires all three legacy " +
                                   "components to exist. No changes made.", player);
                    return;
                }

                MonoBehaviour item = (MonoBehaviour)
                    (onRoot.Length > 0 ? onRoot[0] : onHost[0]);
                if (item.enabled)
                {
                    Debug.LogError("[Windrunner 5P] " + type.Name +
                                   " is ENABLED. This visual may still be in use. " +
                                   "Migration aborted without changing it.", item);
                    return;
                }

                if (onRoot.Length > 0)
                    originals.Add(item);
            }

            if (originals.Count == 0)
            {
                Debug.Log("[Windrunner 5P] All legacy visuals are already hosted.", player);
                Audit();
                return;
            }

            foreach (string name in RingNames)
            {
                int onRoot = CountNamedLines(motor.transform, name);
                int onHost = CountNamedLines(existingHost, name);
                if (onRoot > 1 || onHost > 1 || onRoot > 0 && onHost > 0)
                {
                    Debug.LogError("[Windrunner 5P] Duplicate " + name +
                                   " found. Resolve that before migration.", player);
                    return;
                }

                Transform line = FindNamedLine(motor.transform, name);
                if (line != null && PrefabUtility.IsPartOfPrefabInstance(line))
                {
                    Debug.LogError("[Windrunner 5P] " + name +
                                   " is prefab-backed; automated reparenting blocked.", line);
                    return;
                }

                if (line != null && existingHost != null && existingHost.Find(name) != null)
                {
                    Debug.LogError("[Windrunner 5P] LegacyPresentation already has a child " +
                                   "named " + name + ". Resolve conflict first.", line);
                    return;
                }
            }

            if (!EditorUtility.DisplayDialog("Windrunner 5P — Archive Disabled Visuals",
                    "Move " + originals.Count + " DISABLED prototype UI components into an " +
                    "active LegacyPresentation child?\n\n" +
                    "This is a reversible hierarchy move, not deletion. Serialized settings " +
                    "and loaded-scene component references are preserved. Existing ring " +
                    "GameObjects move with their owner.\n\nBACK UP YOUR SCENE FIRST.",
                    "Quarantine", "Cancel"))
                return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Windrunner 5P — Quarantine Disabled Visuals");
            try
            {
                Transform host;
                if (existingHost == null)
                {
                    GameObject created = new GameObject(HostName);
                    Undo.RegisterCreatedObjectUndo(created, "Create LegacyPresentation");
                    Undo.SetTransformParent(created.transform, motor.transform, "Parent LegacyPresentation");
                    created.transform.localPosition = Vector3.zero;
                    created.transform.localRotation = Quaternion.identity;
                    created.transform.localScale = Vector3.one;
                    created.layer = player.layer;
                    host = created.transform;
                }
                else
                {
                    host = existingHost;
                }

                var replacements = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
                foreach (MonoBehaviour original in originals)
                {
                    MonoBehaviour replacement = Undo.AddComponent(host.gameObject,
                        original.GetType()) as MonoBehaviour;
                    if (replacement == null)
                        throw new InvalidOperationException("Could not add " + original.GetType().Name);

                    EditorUtility.CopySerialized(original, replacement);
                    replacement.enabled = original.enabled;
                    EnsureLinks(replacement, motor);
                    replacements.Add(original, replacement);
                    EditorUtility.SetDirty(replacement);
                }

                int references = RemapLoadedScenes(replacements);
                int rings = 0;
                foreach (string name in RingNames)
                {
                    Transform ring = FindNamedLine(motor.transform, name);
                    if (ring != null)
                    {
                        Undo.SetTransformParent(ring, host, "Move disabled compression ring");
                        rings++;
                    }
                }

                foreach (MonoBehaviour original in originals)
                    Undo.DestroyObjectImmediate(original);

                EditorSceneManager.MarkSceneDirty(player.scene);
                Selection.activeGameObject = player;
                Undo.CollapseUndoOperations(group);
                Debug.Log("[Windrunner 5P] Quarantined " + originals.Count +
                          " disabled visuals, remapped " + references +
                          " scene references, moved " + rings + " legacy ring objects. " +
                          "No gameplay or feedback setting was enabled.", player);
                Audit();
            }
            catch (Exception error)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(error);
                Debug.LogError("[Windrunner 5P] Migration failed; Undo rollback requested. " +
                               "Inspect the scene before saving.");
            }
        }

        private static bool IsIdentityHost(Transform host)
        {
            return host.localPosition.sqrMagnitude < 0.000001f &&
                   Quaternion.Angle(host.localRotation, Quaternion.identity) < 0.001f &&
                   (host.localScale - Vector3.one).sqrMagnitude < 0.000001f;
        }

        private static int CountNamedLines(Transform parent, string name)
        {
            if (parent == null) return 0;
            int count = 0;
            foreach (Transform child in parent)
                if (child.name == name && child.GetComponent<LineRenderer>() != null)
                    count++;
            return count;
        }

        private static Transform FindNamedLine(Transform parent, string name)
        {
            if (parent == null) return null;
            foreach (Transform child in parent)
                if (child.name == name && child.GetComponent<LineRenderer>() != null)
                    return child;
            return null;
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

        private static void EnsureLinks(Component component, PlayerMotor motor)
        {
            SerializedObject obj = new SerializedObject(component);
            if (component is AirTargetIndicator)
            {
                LinkIfMissing(obj, "targeter", motor.GetComponent<AirTargeter>());
            }
            else if (component is AirCastFeedback)
            {
                LinkIfMissing(obj, "caster", motor.GetComponent<AirCaster>());
            }
            else if (component is AirChargeVisual)
            {
                LinkIfMissing(obj, "caster", motor.GetComponent<AirCaster>());
                LinkIfMissing(obj, "castOrigin", motor.transform.Find("Air Cast Origin"));
            }
            obj.ApplyModifiedProperties();
        }

        private static int MissingRequiredLinks(Component component)
        {
            SerializedObject obj = new SerializedObject(component);
            if (component is AirTargetIndicator)
                return Missing(obj, "targeter");
            if (component is AirCastFeedback)
                return Missing(obj, "caster");
            if (component is AirChargeVisual)
                return Missing(obj, "caster") + Missing(obj, "castOrigin");
            return 0;
        }

        private static void LinkIfMissing(SerializedObject obj, string name, UnityEngine.Object value)
        {
            SerializedProperty property = obj.FindProperty(name);
            if (property != null && property.objectReferenceValue == null && value != null)
            {
                Undo.RecordObject(obj.targetObject, "Connect Legacy Presentation Reference");
                property.objectReferenceValue = value;
            }
        }

        private static int Missing(SerializedObject obj, string name)
        {
            SerializedProperty property = obj.FindProperty(name);
            return property == null || property.objectReferenceValue == null ? 1 : 0;
        }

        private static int RemapLoadedScenes(
            Dictionary<UnityEngine.Object, UnityEngine.Object> replacements)
        {
            int count = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    {
                        if (component == null || replacements.ContainsKey(component))
                            continue;
                        SerializedObject obj = new SerializedObject(component);
                        SerializedProperty prop = obj.GetIterator();
                        bool changed = false;
                        while (prop.Next(true))
                        {
                            if (prop.propertyType != SerializedPropertyType.ObjectReference)
                                continue;

                            UnityEngine.Object old = prop.objectReferenceValue;
                            if (old == null ||
                                !replacements.TryGetValue(old, out UnityEngine.Object next))
                                continue;

                            if (!changed)
                                Undo.RecordObject(component, "Remap Legacy Visual Reference");
                            prop.objectReferenceValue = next;
                            changed = true;
                            count++;
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
            return count;
        }
    }
}
