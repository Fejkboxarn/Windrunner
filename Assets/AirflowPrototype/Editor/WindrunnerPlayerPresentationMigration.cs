using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// Explicit, Undo-backed scene migration. Never mutates a scene on import.
    /// Moves three presentation-only components, not their gameplay event sources.
    /// </summary>
    public static class WindrunnerPlayerPresentationMigration
    {
        private const string MenuRoot = "Tools/Windrunner/Production/Batch 5L/";
        private const string HostName = "PresentationFeedback";

        private static readonly Type[] Types =
        {
            typeof(AirHitFeedback),
            typeof(PlayerDustFeedback),
            typeof(PlayerHandAirTrails)
        };

        [MenuItem(MenuRoot + "1. Audit Presentation Components")]
        public static void Audit()
        {
            PlayerMotor motor = ResolvePlayer();
            if (motor == null)
            {
                Debug.LogWarning("[Windrunner 5L] Select the intended scene player with PlayerMotor. " +
                                 "Automatic selection requires exactly one player in loaded scenes.");
                return;
            }

            GameObject player = motor.gameObject;
            Transform host = player.transform.Find(HostName);
            int rootCount = 0;
            int childCount = 0;
            int missingReferences = 0;
            int duplicates = 0;

            foreach (Type type in Types)
            {
                Component[] root = player.GetComponents(type);
                Component[] child = host != null ? host.GetComponents(type) : Array.Empty<Component>();
                rootCount += root.Length;
                childCount += child.Length;
                if (root.Length + child.Length > 1)
                    duplicates++;

                if (child.Length == 1)
                    missingReferences += MissingRequiredLinks(child[0]);
            }

            bool passed = rootCount == 0 && childCount == Types.Length &&
                          duplicates == 0 && missingReferences == 0 && host != null &&
                          host.gameObject.activeInHierarchy && IsIdentityHost(host);

            Debug.Log("[Windrunner 5L] " + (passed ? "PASS" : "INCOMPLETE") +
                      " — player root components=" + player.GetComponents<Component>().Length +
                      ", feedback on root=" + rootCount + "/3, on PresentationFeedback=" +
                      childCount + "/3, duplicate types=" + duplicates +
                      ", missing required links=" + missingReferences + ". " +
                      "This checks scene structure, NOT gameplay effects, visual parity, or FPS.", player);
        }

        [MenuItem(MenuRoot + "2. Move Presentation Components (Undoable)")]
        public static void Migrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Windrunner 5L] Exit Play Mode before changing the hierarchy.");
                return;
            }

            PlayerMotor motor = ResolvePlayer();
            if (motor == null || !motor.gameObject.scene.IsValid())
            {
                Debug.LogError("[Windrunner 5L] Select a scene player containing PlayerMotor.");
                return;
            }

            GameObject player = motor.gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(player) ||
                PrefabUtility.IsPartOfPrefabAsset(player))
            {
                Debug.LogError("[Windrunner 5L] This player is prefab-backed. Migration is " +
                               "intentionally blocked to avoid scene-only overrides. Work in " +
                               "the prefab asset deliberately; no changes were made.", player);
                return;
            }

            Transform existing = player.transform.Find(HostName);
            if (existing != null && (!existing.gameObject.activeInHierarchy || !IsIdentityHost(existing)))
            {
                Debug.LogError("[Windrunner 5L] PresentationFeedback exists but is inactive " +
                               "or has a non-identity local transform. Resolve it manually first.", existing);
                return;
            }

            var originals = new List<MonoBehaviour>(Types.Length);
            foreach (Type type in Types)
            {
                Component[] onRoot = player.GetComponents(type);
                Component[] onChild = existing != null ? existing.GetComponents(type) : Array.Empty<Component>();
                if (onRoot.Length > 1 || onChild.Length > 1 ||
                    (onRoot.Length > 0 && onChild.Length > 0))
                {
                    Debug.LogError("[Windrunner 5L] Duplicate " + type.Name +
                                   " detected. No changes were made.", player);
                    return;
                }

                if (onRoot.Length == 1)
                    originals.Add((MonoBehaviour)onRoot[0]);
            }

            if (originals.Count == 0)
            {
                Debug.Log("[Windrunner 5L] No root feedback components remain to move.", player);
                Audit();
                return;
            }

            Transform rootDust = player.transform.Find("Ground Dust");
            Transform hostedDust = existing != null ? existing.Find("Ground Dust") : null;
            if (rootDust != null && hostedDust != null &&
                player.GetComponent<PlayerDustFeedback>() != null)
            {
                Debug.LogError("[Windrunner 5L] Two Ground Dust objects exist. " +
                               "Resolve the duplicate before migration; nothing was changed.", player);
                return;
            }

            if (rootDust != null && rootDust.GetComponent<ParticleSystem>() != null &&
                player.GetComponent<PlayerDustFeedback>() != null &&
                PrefabUtility.IsPartOfPrefabInstance(rootDust))
            {
                Debug.LogError("[Windrunner 5L] Ground Dust is a nested prefab instance. " +
                               "Its parenting cannot be changed safely by this migration.", rootDust);
                return;
            }

            if (!EditorUtility.DisplayDialog("Windrunner 5L — Presentation Hierarchy",
                    "Move " + originals.Count + " presentation components to a " +
                    "PresentationFeedback child?\n\n" +
                    "This preserves serialized values and enabled states, remaps references " +
                    "in loaded scenes, and reparents an existing generated Ground Dust emitter " +
                    "when present. Edit > Undo is supported.\n\n" +
                    "BACK UP OR COMMIT THE SCENE FIRST. Test gameplay feedback afterward.",
                    "Move Components", "Cancel"))
                return;

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Windrunner 5L — Extract Presentation Feedback");
            try
            {
                GameObject host;
                if (existing == null)
                {
                    host = new GameObject(HostName);
                    Undo.RegisterCreatedObjectUndo(host, "Create PresentationFeedback");
                    Undo.SetTransformParent(host.transform, player.transform, "Parent PresentationFeedback");
                    host.transform.localPosition = Vector3.zero;
                    host.transform.localRotation = Quaternion.identity;
                    host.transform.localScale = Vector3.one;
                    host.layer = player.layer;
                }
                else
                {
                    host = existing.gameObject;
                }

                var replacements = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
                foreach (MonoBehaviour original in originals)
                {
                    MonoBehaviour replacement = Undo.AddComponent(host, original.GetType()) as MonoBehaviour;
                    if (replacement == null)
                        throw new InvalidOperationException("Failed to add " + original.GetType().Name);

                    EditorUtility.CopySerialized(original, replacement);
                    replacement.enabled = original.enabled;
                    replacements.Add(original, replacement);
                    EnsureRequiredLinks(replacement, motor);
                    EditorUtility.SetDirty(replacement);
                }

                int remapped = RemapLoadedSceneReferences(replacements);

                // Ground Dust is an existing user-visible emitter, not a component reference.
                // Retain its identity rather than letting the new host create an extra emitter.
                if (player.GetComponent<PlayerDustFeedback>() != null &&
                    rootDust != null && rootDust.GetComponent<ParticleSystem>() != null)
                {
                    Undo.SetTransformParent(rootDust, host.transform, "Move existing Ground Dust");
                }

                foreach (MonoBehaviour original in originals)
                    Undo.DestroyObjectImmediate(original);

                EditorSceneManager.MarkSceneDirty(player.scene);
                Selection.activeGameObject = player;
                Undo.CollapseUndoOperations(group);
                Debug.Log("[Windrunner 5L] Moved " + originals.Count +
                          " presentation components; remapped " + remapped +
                          " loaded-scene references. Validate the audit and playtest " +
                          "node hit/reward, dust, and hand trail feedback.", player);
                Audit();
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(exception);
                Debug.LogError("[Windrunner 5L] Migration rolled back using Unity Undo. " +
                               "Inspect the scene before saving.");
            }
        }

        private static bool IsIdentityHost(Transform host)
        {
            return host.localPosition.sqrMagnitude < 0.000001f &&
                   Quaternion.Angle(host.localRotation, Quaternion.identity) < 0.001f &&
                   (host.localScale - Vector3.one).sqrMagnitude < 0.000001f;
        }

        private static PlayerMotor ResolvePlayer()
        {
            GameObject selection = Selection.activeGameObject;
            if (selection != null)
            {
                PlayerMotor selected = selection.GetComponent<PlayerMotor>() ??
                                       selection.GetComponentInParent<PlayerMotor>();
                if (selected != null)
                    return selected;
            }

            PlayerMotor[] players = UnityEngine.Object.FindObjectsByType<PlayerMotor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return players.Length == 1 ? players[0] : null;
        }

        private static void EnsureRequiredLinks(Component component, PlayerMotor motor)
        {
            SerializedObject serialized = new SerializedObject(component);
            if (component is AirHitFeedback)
            {
                LinkIfMissing(serialized, "hitSystem", motor.GetComponent<AirFlowHitSystem>());
                LinkIfMissing(serialized, "playerTransform", motor.transform);
            }
            else if (component is PlayerDustFeedback)
            {
                LinkIfMissing(serialized, "motor", motor);
                LinkIfMissing(serialized, "movementVisuals", motor.GetComponent<PlayerMovementVisuals>());
            }
            else if (component is PlayerHandAirTrails)
            {
                LinkIfMissing(serialized, "motor", motor);
                LinkIfMissing(serialized, "airFlowHitSystem", motor.GetComponent<AirFlowHitSystem>());
                LinkIfMissing(serialized, "slingController", motor.GetComponent<AirSlingController>());
            }

            serialized.ApplyModifiedProperties();
        }

        private static void LinkIfMissing(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null && property.objectReferenceValue == null && value != null)
            {
                Undo.RecordObject(serialized.targetObject, "Link Presentation Feedback");
                property.objectReferenceValue = value;
            }
        }

        private static int MissingRequiredLinks(Component component)
        {
            SerializedObject serialized = new SerializedObject(component);
            if (component is AirHitFeedback)
                return Missing(serialized, "hitSystem") + Missing(serialized, "playerTransform");
            if (component is PlayerDustFeedback)
                return Missing(serialized, "motor") + Missing(serialized, "movementVisuals");
            if (component is PlayerHandAirTrails)
                return Missing(serialized, "motor") + Missing(serialized, "airFlowHitSystem") +
                       Missing(serialized, "slingController");
            return 0;
        }

        private static int Missing(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property == null || property.objectReferenceValue == null ? 1 : 0;
        }

        private static int RemapLoadedSceneReferences(
            Dictionary<UnityEngine.Object, UnityEngine.Object> replacements)
        {
            int count = 0;
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

                        SerializedObject serialized = new SerializedObject(component);
                        SerializedProperty iterator = serialized.GetIterator();
                        bool changed = false;
                        while (iterator.Next(true))
                        {
                            if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                                continue;

                            UnityEngine.Object previous = iterator.objectReferenceValue;
                            if (previous == null ||
                                !replacements.TryGetValue(previous, out UnityEngine.Object next))
                                continue;

                            if (!changed)
                                Undo.RecordObject(component, "Remap Presentation Feedback Reference");
                            iterator.objectReferenceValue = next;
                            changed = true;
                            count++;
                        }

                        if (changed)
                        {
                            serialized.ApplyModifiedProperties();
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
