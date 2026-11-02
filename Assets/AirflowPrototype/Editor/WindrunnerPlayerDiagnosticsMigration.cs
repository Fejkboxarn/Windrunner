using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// An explicit, Undo-backed scene migration. No changes are made on import.
    /// Moves only development HUD/keyboard diagnostics; gameplay stays on the player.
    /// </summary>
    public static class WindrunnerPlayerDiagnosticsMigration
    {
        private const string MenuRoot = "Tools/Windrunner/Production/Batch 5B/";
        private const string ChildName = "DevelopmentDiagnostics";

        private static readonly Type[] Types =
        {
            typeof(PlayerDebugHUD),
            typeof(AirTargetDebugHUD),
            typeof(AirflowDebugRuntimeControls)
        };

        [MenuItem(MenuRoot + "1. Audit Player Components")]
        public static void Audit()
        {
            GameObject player = ResolvePlayer();
            if (player == null)
            {
                Debug.LogWarning("[Windrunner 5B] Select a player containing PlayerMotor. " +
                                 "Multiple players require an explicit selection.");
                return;
            }

            int total = player.GetComponents<Component>().Length;
            Transform child = player.transform.Find(ChildName);
            int rootDiagnostics = 0;
            int childDiagnostics = 0;
            foreach (Type type in Types)
            {
                if (player.GetComponent(type) != null)
                    rootDiagnostics++;
                if (child != null && child.GetComponent(type) != null)
                    childDiagnostics++;
            }

            Debug.Log($"[Windrunner 5B] Player={player.name}; root components={total}; " +
                      $"diagnostics on root={rootDiagnostics}/3, on child={childDiagnostics}/3. " +
                      "This is a structural audit; no profiler/performance claim is implied.", player);
        }

        [MenuItem(MenuRoot + "2. Extract Debug Components (Undoable)")]
        public static void Extract()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Windrunner 5B] Exit Play Mode before migrating scene components.");
                return;
            }

            GameObject player = ResolvePlayer();
            if (player == null || !player.scene.IsValid())
            {
                Debug.LogError("[Windrunner 5B] Select a scene player with PlayerMotor first.");
                return;
            }

            // Editing a prefab instance would add scene-only overrides and can break its source prefab.
            if (PrefabUtility.IsPartOfPrefabInstance(player))
            {
                Debug.LogError("[Windrunner 5B] Player is a prefab instance. Open the prefab " +
                               "asset and migrate it deliberately, or leave its diagnostics where " +
                               "they are. This scene instance was not changed.", player);
                return;
            }

            Transform existingChild = player.transform.Find(ChildName);
            if (existingChild != null && !existingChild.gameObject.activeSelf)
            {
                Debug.LogError("[Windrunner 5B] DevelopmentDiagnostics already exists but is " +
                               "inactive. Enable it manually before extracting, or cancel. " +
                               "No changes were made.", existingChild);
                return;
            }
            var originals = new List<MonoBehaviour>();
            foreach (Type type in Types)
            {
                MonoBehaviour original = player.GetComponent(type) as MonoBehaviour;
                if (original == null)
                    continue;

                if (existingChild != null && existingChild.GetComponent(type) != null)
                {
                    Debug.LogError($"[Windrunner 5B] Duplicate {type.Name} already exists " +
                                   "on DevelopmentDiagnostics. Resolve this manually first.", player);
                    return;
                }

                originals.Add(original);
            }

            if (originals.Count == 0)
            {
                Debug.Log("[Windrunner 5B] No root debug components to migrate. Already done or not installed.", player);
                Audit();
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Windrunner Batch 5B — Extract Diagnostics",
                    $"Move {originals.Count} development-only components from '{player.name}' " +
                    "to a DevelopmentDiagnostics child?\n\n" +
                    "Serialized values and references in currently open scenes will be remapped. " +
                    "Gameplay components stay in place. The entire operation supports Edit > Undo.\n\n" +
                    "Save a scene/commit backup first. This is not guaranteed to fix Inspector lag.",
                    "Extract", "Cancel"))
                return;

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Windrunner 5B — Extract Player Diagnostics");
            try
            {
                GameObject destination;
                if (existingChild == null)
                {
                    destination = new GameObject(ChildName);
                    Undo.RegisterCreatedObjectUndo(destination, "Create DevelopmentDiagnostics");
                    Undo.SetTransformParent(destination.transform, player.transform, "Parent Diagnostics");
                    destination.transform.localPosition = Vector3.zero;
                    destination.transform.localRotation = Quaternion.identity;
                    destination.transform.localScale = Vector3.one;
                    destination.layer = player.layer;
                }
                else
                {
                    destination = existingChild.gameObject;
                }

                var replacements = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
                foreach (MonoBehaviour original in originals)
                {
                    MonoBehaviour replacement = Undo.AddComponent(destination, original.GetType()) as MonoBehaviour;
                    if (replacement == null)
                        throw new InvalidOperationException("Could not add " + original.GetType().Name);

                    EditorUtility.CopySerialized(original, replacement);
                    if (replacement.gameObject != destination)
                        throw new InvalidOperationException("CopySerialized changed component ownership.");
                    replacement.enabled = original.enabled;
                    replacements.Add(original, replacement);
                    EditorUtility.SetDirty(replacement);
                }

                int remapped = RemapOpenSceneReferences(replacements);

                // If the old targeting HUD had relied on GetComponent in Awake, its new
                // child location would no longer find the targeter. Make that link explicit.
                AirTargetDebugHUD newTargetHud = destination.GetComponent<AirTargetDebugHUD>();
                if (newTargetHud != null)
                {
                    SerializedObject hud = new SerializedObject(newTargetHud);
                    SerializedProperty targeter = hud.FindProperty("targeter");
                    if (targeter != null && targeter.objectReferenceValue == null)
                    {
                        Undo.RecordObject(newTargetHud, "Link Air Target Debug HUD");
                        targeter.objectReferenceValue = player.GetComponent<AirTargeter>();
                        hud.ApplyModifiedProperties();
                    }
                }

                foreach (MonoBehaviour original in originals)
                    Undo.DestroyObjectImmediate(original);

                EditorSceneManager.MarkSceneDirty(player.scene);
                Selection.activeGameObject = player;
                Undo.CollapseUndoOperations(group);
                Debug.Log($"[Windrunner 5B] Migrated {originals.Count} debug components " +
                          $"and remapped {remapped} serialized scene references. " +
                          "Use Ctrl+Z to undo. Test F1/F2 and play mode before committing.", player);
                Audit();
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(exception);
                Debug.LogError("[Windrunner 5B] Migration failed; Unity Undo rollback requested. " +
                               "Inspect the scene before saving.");
            }
        }

        private static GameObject ResolvePlayer()
        {
            GameObject selection = Selection.activeGameObject;
            if (selection != null)
            {
                PlayerMotor selectedMotor = selection.GetComponent<PlayerMotor>() ??
                                            selection.GetComponentInParent<PlayerMotor>();
                if (selectedMotor != null)
                    return selectedMotor.gameObject;
            }

            PlayerMotor[] motors = UnityEngine.Object.FindObjectsByType<PlayerMotor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return motors.Length == 1 ? motors[0].gameObject : null;
        }

        private static int RemapOpenSceneReferences(
            Dictionary<UnityEngine.Object, UnityEngine.Object> replacements)
        {
            int remapped = 0;
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Component[] components = root.GetComponentsInChildren<Component>(true);
                    foreach (Component component in components)
                    {
                        if (component == null || replacements.ContainsKey(component))
                            continue;

                        SerializedObject serialized = new SerializedObject(component);
                        SerializedProperty property = serialized.GetIterator();
                        bool changed = false;
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference)
                                continue;

                            UnityEngine.Object oldValue = property.objectReferenceValue;
                            if (oldValue == null || !replacements.TryGetValue(oldValue, out UnityEngine.Object newValue))
                                continue;

                            if (!changed)
                                Undo.RecordObject(component, "Remap Player Diagnostic References");
                            property.objectReferenceValue = newValue;
                            changed = true;
                            remapped++;
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

            return remapped;
        }
    }
}
