using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Windrunner.Production.Progression;

namespace AirflowPrototype.EditorTools
{
    public sealed class ProgressionStudio : EditorWindow
    {
        private static readonly string[] Sections = { "quests", "relationships", "challenges", "races", "puzzles", "rewards", "cutscenes" };
        private ProgressionCatalog catalog;
        private SerializedObject serialized;
        private string search = "";
        private int section;
        private Vector2 scroll;
        private List<string> validation = new List<string>();
        [MenuItem("Tools/Windrunner/Progression Studio")]
        public static void Open() => GetWindow<ProgressionStudio>("Progression Studio");
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Authored definitions only. Runtime/saved progression is separate. Stable IDs are permanent; references use these IDs. No story content is generated automatically.", MessageType.Info);
            var selected = (ProgressionCatalog)EditorGUILayout.ObjectField("Catalog", catalog, typeof(ProgressionCatalog), false);
            if (selected != catalog) { catalog = selected; serialized = catalog != null ? new SerializedObject(catalog) : null; validation.Clear(); }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Create catalog")) Create();
                if (catalog != null && GUILayout.Button("Duplicate with new IDs")) Duplicate();
                EditorGUILayout.EndHorizontal();
            }
            if (catalog == null) return;
            if (serialized == null) serialized = new SerializedObject(catalog);
            if (GUILayout.Button("Validate definitions and content")) validation = ValidateCatalog(catalog, true);
            foreach (string error in validation) EditorGUILayout.HelpBox(error, MessageType.Warning);
            search = EditorGUILayout.TextField("Search names / IDs", search);
            section = GUILayout.Toolbar(section, Sections);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Add " + Sections[section])) AddRecord(catalog, Sections[section]);
                serialized.Update();
                var list = serialized.FindProperty("definitions." + Sections[section]);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                for (int i = 0; i < list.arraySize; i++)
                {
                    var entry = list.GetArrayElementAtIndex(i);
                    string id = entry.FindPropertyRelative("id").stringValue;
                    string display = entry.FindPropertyRelative("displayName")?.stringValue ?? id;
                    if (!string.IsNullOrEmpty(search) && (id + " " + display).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    EditorGUILayout.PropertyField(entry, new GUIContent(string.IsNullOrEmpty(display) ? id : display), true);
                }
                EditorGUILayout.EndScrollView();
                serialized.ApplyModifiedProperties();
                if (GUILayout.Button("Save validated catalog"))
                {
                    validation = ValidateCatalog(catalog, false);
                    if (validation.Count == 0) AssetDatabase.SaveAssetIfDirty(catalog);
                }
            }
        }
        private void Create()
        {
            string path = EditorUtility.SaveFilePanelInProject("New progression catalog", "ProgressionCatalog", "asset", "Choose a new asset path.");
            if (string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) != null) return;
            catalog = CreateInstance<ProgressionCatalog>(); AssetDatabase.CreateAsset(catalog, path); serialized = new SerializedObject(catalog); Selection.activeObject = catalog;
        }
        public static void AddRecord(ProgressionCatalog target, string section)
        {
            Undo.RecordObject(target, "Add progression definition"); string id = Guid.NewGuid().ToString("N");
            switch (section)
            {
                case "quests": target.definitions.quests.Add(new QuestDefinition { id = id, displayName = "New quest", kind = QuestKind.Optional }); break;
                case "relationships": target.definitions.relationships.Add(new RelationshipDefinition { id = id, displayName = "New character" }); break;
                case "challenges": target.definitions.challenges.Add(new ChallengeDefinition { id = id, displayName = "New challenge" }); break;
                case "races": target.definitions.races.Add(new RaceDefinition { id = id }); break;
                case "puzzles": target.definitions.puzzles.Add(new PuzzleDefinition { id = id }); break;
                case "rewards": target.definitions.rewards.Add(new RewardDefinition { id = id }); break;
                case "cutscenes": target.definitions.cutscenes.Add(new CutsceneDefinition { id = id, displayName = "New cutscene" }); break;
                default: throw new ArgumentException("Unknown section");
            }
            EditorUtility.SetDirty(target);
        }
        public static ProgressionCatalog CopyWithNewIds(ProgressionCatalog source)
        {
            var copy = Instantiate(source); copy.name = source.name + " Copy";
            var obj = new SerializedObject(copy); var ids = new Dictionary<string, string>();
            var property = obj.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.String && property.name == "id" && !string.IsNullOrEmpty(property.stringValue))
                    ids[property.stringValue] = Guid.NewGuid().ToString("N");
            property = obj.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.String && ids.TryGetValue(property.stringValue, out string replacement)) property.stringValue = replacement;
            obj.ApplyModifiedPropertiesWithoutUndo(); return copy;
        }
        private void Duplicate()
        {
            string path = EditorUtility.SaveFilePanelInProject("Duplicate progression catalog", catalog.name + " Copy", "asset", "References within the new catalog receive new IDs. Scene bindings are not changed.");
            if (string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) != null) return;
            var copy = CopyWithNewIds(catalog); AssetDatabase.CreateAsset(copy, path); catalog = copy; serialized = new SerializedObject(copy); Selection.activeObject = copy;
        }
        public static List<string> ValidateCatalog(ProgressionCatalog asset, bool content)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return ProgressionValidation.Validate(asset.definitions, content).Select(x => path + ": " + x).ToList();
        }
        [MenuItem("Tools/Windrunner/Validate Progression")]
        public static void ValidateAll()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ProgressionCatalog"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (string issue in ValidateCatalog(asset, true)) { Debug.LogWarning(issue, asset); count++; }
            }
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                foreach (string issue in ProgressionSceneValidation.Validate(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i))) { Debug.LogWarning(issue); count++; }
            Debug.Log("Progression validation: " + count + " authoring/content issues. Scene binding checks run before Play.");
        }
        [InitializeOnLoadMethod] private static void InstallPlayGuard()
        {
            EditorApplication.playModeStateChanged -= BeforePlay; EditorApplication.playModeStateChanged += BeforePlay;
        }
        private static void BeforePlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            bool invalid = false;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                foreach (string issue in ProgressionSceneValidation.Validate(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i))) { Debug.LogError(issue); invalid = true; }
            foreach (var host in Resources.FindObjectsOfTypeAll<ProgressionHost>())
            {
                if (!host.gameObject.scene.IsValid() || EditorUtility.IsPersistent(host)) continue;
                var errors = host.catalog == null ? new List<string> { "Missing catalog" } : ValidateCatalog(host.catalog, false);
                if (host.project == null) errors.Add("Missing ProjectRoot binding");
                foreach (string error in errors) { Debug.LogError(error, host); invalid = true; }
            }
            if (invalid) EditorApplication.isPlaying = false;
        }
    }
    [CustomEditor(typeof(ProgressionCatalog))]
    public sealed class ProgressionCatalogInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)) DrawDefaultInspector();
            if (GUILayout.Button("Open Progression Studio")) ProgressionStudio.Open();
        }
    }
}
