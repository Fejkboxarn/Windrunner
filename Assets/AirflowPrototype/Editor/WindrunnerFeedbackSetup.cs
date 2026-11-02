using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using Windrunner.Production.Feedback;
using Windrunner.Production.Unity;

namespace AirflowPrototype.Editor
{
    /// <summary>
    /// Lives in Assembly-CSharp-Editor to reference the existing AirflowPrototype
    /// types and the new portable feedback assembly without circular dependencies.
    /// </summary>
    public static class WindrunnerFeedbackSetup
    {
        private const string Generated = "Assets/WindrunnerProduction/Feedback/Generated";
        private const string CatalogPath = Generated + "/WindrunnerFeedbackCatalog.asset";
        private const string Define = "WINDRUNNER_FEEL";
        private const string BackendTypeName =
            "Windrunner.Production.Integrations.Feel.WindrunnerFeelBackendAdapter";

        [MenuItem("Tools/Windrunner/Production/Batch 03 - Enable FEEL Adapter")]
        public static void EnableFeelAdapter()
        {
            if (FindType("MoreMountains.Feedbacks.MMF_Player") == null)
            {
                Debug.LogError("[Windrunner Feedback] Import your licensed FEEL package first. " +
                               "MMF_Player was not found in the project.");
                return;
            }

            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup);
            string current = PlayerSettings.GetScriptingDefineSymbols(target);
            string[] symbols = current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (symbols.Any(symbol => symbol.Trim() == Define))
            {
                Debug.Log("[Windrunner Feedback] FEEL adapter symbol already enabled.");
                return;
            }

            string joined = string.IsNullOrWhiteSpace(current) ? Define : current.TrimEnd(';') + ";" + Define;
            PlayerSettings.SetScriptingDefineSymbols(target, joined);
            Debug.Log("[Windrunner Feedback] FEEL integration enabled for the current build target. " +
                      "Wait for recompilation, then run Batch 03 Install Feedback.");
        }

        [MenuItem("Tools/Windrunner/Production/Batch 03 - Install Feedback")]
        public static void InstallFeedback()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Windrunner Feedback] Run the installer in Edit Mode.");
                return;
            }

            ProjectRoot[] roots = UnityEngine.Object.FindObjectsByType<ProjectRoot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            AirCaster[] players = UnityEngine.Object.FindObjectsByType<AirCaster>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (roots.Length != 1 || players.Length != 1)
            {
                Debug.LogError("[Windrunner Feedback] Open a gameplay scene with exactly one " +
                               "Batch 01 ProjectRoot and one AirCaster player. Found roots=" +
                               roots.Length + ", players=" + players.Length + ".");
                return;
            }

            FeedbackCatalog catalog = EnsureCatalog();
            if (catalog == null) return;
            GameObject root = roots[0].gameObject;
            WindrunnerFeedbackHost host = root.GetComponent<WindrunnerFeedbackHost>();
            if (host == null) host = Undo.AddComponent<WindrunnerFeedbackHost>(root);

            MonoBehaviour feelBridge = host.FeelBackendComponent;
            Type backendType = FindType(BackendTypeName);
            if (backendType != null && typeof(MonoBehaviour).IsAssignableFrom(backendType))
            {
                Component component = root.GetComponent(backendType);
                if (component == null) component = Undo.AddComponent(root, backendType);
                feelBridge = component as MonoBehaviour;
            }
            else
            {
                Debug.LogWarning("[Windrunner Feedback] FEEL adapter not compiled. " +
                                 "Import FEEL, enable its integration symbol and re-run this installer. " +
                                 "Semantic feedback observation will still work.");
            }

            host.Configure(catalog, feelBridge);
            EditorUtility.SetDirty(host);

            AirCaster player = players[0];
            AirflowProductionFeedbackEmitter emitter =
                player.GetComponent<AirflowProductionFeedbackEmitter>();
            if (emitter == null)
                emitter = Undo.AddComponent<AirflowProductionFeedbackEmitter>(player.gameObject);
            emitter.Configure(player.GetComponent<PlayerMovementVisuals>(), player,
                player.GetComponent<AirSlingController>(),
                player.GetComponent<AirFlowHitSystem>(), player.GetComponent<AirPower>());
            EditorUtility.SetDirty(emitter);

            EditorSceneManager.MarkSceneDirty(root.scene);
            if (player.gameObject.scene != root.scene)
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Selection.activeGameObject = root;
            Debug.Log("[Windrunner Feedback] Installed observe-only feedback event pipeline. " +
                      "Existing Airflow sounds/VFX/camera effects remain unchanged. " +
                      "Save the scene and run Batch 03 Audit Feedback.");
        }

        [MenuItem("Tools/Windrunner/Production/Batch 03 - Create FEEL Recipe for Selected Event")]
        public static void CreateFeelRecipe()
        {
            FeedbackEventDefinition definition = Selection.activeObject as FeedbackEventDefinition;
            if (definition == null)
            {
                Debug.LogError("[Windrunner Feedback] Select a generated FeedbackEventDefinition asset " +
                               "in the Project window first.");
                return;
            }
            Type feelPlayer = FindType("MoreMountains.Feedbacks.MMF_Player");
            if (feelPlayer == null)
            {
                Debug.LogError("[Windrunner Feedback] MMF_Player not found. Import FEEL first.");
                return;
            }
            string dir = Generated + "/FeelRecipes";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            int index = definition.feelRecipes != null ? definition.feelRecipes.Length + 1 : 1;
            string path = AssetDatabase.GenerateUniqueAssetPath(
                dir + "/" + definition.key + "_FEEL_" + index + ".prefab");
            GameObject draft = new GameObject(definition.key + " FEEL Recipe");
            try
            {
                draft.AddComponent(feelPlayer);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(draft, path);
                if (prefab == null)
                {
                    Debug.LogError("[Windrunner Feedback] Could not save FEEL prefab.");
                    return;
                }
                int count = definition.feelRecipes != null ? definition.feelRecipes.Length : 0;
                Array.Resize(ref definition.feelRecipes, count + 1);
                definition.feelRecipes[count] = new FeedbackRecipeEntry
                {
                    prefab = prefab,
                    category = FeedbackCategory.General,
                    prewarmCount = 1,
                    maxPoolSize = 8,
                    safetyReleaseSeconds = 5f
                };
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                Debug.Log("[Windrunner Feedback] Created recipe for " + definition.key +
                          ". Edit the MMF_Player in the prefab. Production playback remains gated.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [MenuItem("Tools/Windrunner/Production/Batch 03 - Audit Feedback")]
        public static void AuditFeedback()
        {
            int errors = 0;
            WindrunnerFeedbackHost[] hosts = UnityEngine.Object.FindObjectsByType<WindrunnerFeedbackHost>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            AirflowProductionFeedbackEmitter[] emitters =
                UnityEngine.Object.FindObjectsByType<AirflowProductionFeedbackEmitter>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (hosts.Length != 1) { Debug.LogError("[Feedback Audit] Expected one host."); errors++; }
            if (emitters.Length != 1) { Debug.LogError("[Feedback Audit] Expected one Airflow emitter."); errors++; }

            if (hosts.Length == 1)
            {
                WindrunnerFeedbackHost host = hosts[0];
                if (Application.isPlaying && host.Service != null)
                    Debug.Log("[Feedback Audit] Live counters — observed=" +
                              host.Service.ObservedCount + ", production played=" +
                              host.Service.PlayedCount);
                FeedbackCatalog catalog = host.Catalog;
                if (catalog == null || catalog.Entries == null || catalog.Entries.Count == 0)
                {
                    Debug.LogError("[Feedback Audit] Catalog is missing/empty.");
                    errors++;
                }
                else
                {
                    var keys = new System.Collections.Generic.HashSet<FeedbackKey>();
                    int optedIn = 0;
                    int recipes = 0;
                    foreach (FeedbackCatalogItem item in catalog.Entries)
                    {
                        if (item == null || item.definition == null)
                        {
                            Debug.LogError("[Feedback Audit] Null event mapping."); errors++; continue;
                        }
                        if (item.key != item.definition.key || !keys.Add(item.key))
                        {
                            Debug.LogError("[Feedback Audit] Mismatched or duplicated key " + item.key);
                            errors++;
                        }
                        if (item.definition.enableProductionPlayback)
                        {
                            optedIn++;
                            if (!item.definition.legacyEffectsMutedAndTested)
                                Debug.LogWarning("[Feedback Audit] Production blocked until legacy effects " +
                                                 "are confirmed muted for " + item.key);
                        }
                        if (item.definition.feelRecipes != null)
                            foreach (FeedbackRecipeEntry recipe in item.definition.feelRecipes)
                                if (recipe != null && recipe.prefab != null) recipes++;
                    }
                    if (recipes > 0 && host.FeelBackendComponent == null)
                    {
                        Debug.LogError("[Feedback Audit] FEEL recipes configured but no FEEL backend linked.");
                        errors++;
                    }
                    Debug.Log("[Feedback Audit] Mappings=" + keys.Count +
                              ", enabled production events=" + optedIn + ", FEEL recipes=" + recipes);
                }
            }
            Debug.Log(errors == 0 ?
                "[Feedback Audit] PASS — scene wiring only. Test real gameplay and effect parity." :
                "[Feedback Audit] FAILED: " + errors + " issue(s).");
        }

        private static FeedbackCatalog EnsureCatalog()
        {
            FeedbackCatalog catalog = AssetDatabase.LoadAssetAtPath<FeedbackCatalog>(CatalogPath);
            if (catalog != null) return catalog;

            if (!Directory.Exists(Generated)) Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();
            catalog = ScriptableObject.CreateInstance<FeedbackCatalog>();
            var items = new System.Collections.Generic.List<FeedbackCatalogItem>();
            foreach (FeedbackKey key in Enum.GetValues(typeof(FeedbackKey)))
            {
                if (key == FeedbackKey.None) continue;
                string path = Generated + "/" + key + ".asset";
                FeedbackEventDefinition definition =
                    AssetDatabase.LoadAssetAtPath<FeedbackEventDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<FeedbackEventDefinition>();
                    definition.key = key;
                    definition.enableProductionPlayback = false;
                    definition.legacyEffectsMutedAndTested = false;
                    definition.cooldownSeconds = key == FeedbackKey.Footstep ? 0.05f : 0f;
                    AssetDatabase.CreateAsset(definition, path);
                }
                items.Add(new FeedbackCatalogItem { key = key, definition = definition });
            }
            catalog.Configure(items.ToArray());
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Windrunner Feedback] Generated " + items.Count +
                      " observe-only event assets. Edit individual assets; re-install will preserve them.");
            return catalog;
        }

        private static Type FindType(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
