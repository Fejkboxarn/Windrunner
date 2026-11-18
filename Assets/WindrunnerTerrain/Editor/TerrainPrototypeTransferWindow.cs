using System;
using UnityEditor;
using UnityEngine;

namespace Windrunner.TerrainAuthoring.Editor
{
    public sealed class TerrainPrototypeTransferWindow : EditorWindow
    {
        [SerializeField] Terrain source, target;
        [SerializeField] bool trees = true, details = true, layers = true;
        TerrainPrototypeTransfer.Preview preview;
        string message;
        Vector2 scroll;
        [MenuItem("Tools/Windrunner/Terrain Prototype Transfer")]
        public static void Open() => GetWindow<TerrainPrototypeTransferWindow>("Terrain Prototype Transfer");
        void OnEnable() { minSize = new Vector2(430, 400); Undo.undoRedoPerformed += Invalidate; }
        void OnDisable() { Undo.undoRedoPerformed -= Invalidate; }
        void Invalidate() { preview = null; message = "Preview again after Undo/Redo."; Repaint(); }
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Append with deduplication. Existing prototype indices and painted content are preserved. Replacement is intentionally unavailable. Use a separate editable target working copy; disable Terrain Studio Automatic Updates first.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            source = (Terrain)EditorGUILayout.ObjectField("Source Terrain", source, typeof(Terrain), true);
            target = (Terrain)EditorGUILayout.ObjectField("Target Terrain", target, typeof(Terrain), true);
            trees = EditorGUILayout.Toggle("Trees", trees);
            details = EditorGUILayout.Toggle("Grass / details", details);
            layers = EditorGUILayout.Toggle("TerrainLayers", layers);
            if (EditorGUI.EndChangeCheck()) { preview = null; message = null; }
            var categories = (trees ? PrototypeCategories.Trees : 0) | (details ? PrototypeCategories.Details : 0) | (layers ? PrototypeCategories.Layers : 0);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Preview transfer"))
                {
                    preview = null;
                    try { preview = TerrainPrototypeTransfer.CreatePreview(source, target, categories); message = preview.Summary; }
                    catch (Exception e) { message = e.Message; }
                }
                if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
                using (new EditorGUI.DisabledScope(preview == null || preview.Added == 0))
                    if (GUILayout.Button("Apply previewed append (Undo supported)"))
                    {
                        try { TerrainPrototypeTransfer.Apply(preview); message = "Applied. Inspect the Terrain and use Edit > Undo to revert. Save Project explicitly when satisfied."; }
                        catch (Exception e) { message = "Transfer refused: " + e.Message; }
                        finally { preview = null; }
                    }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
