using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Timeline;
using Windrunner.Production.Progression;

namespace AirflowPrototype.EditorTools
{
    public static class ProgressionFixtureUsabilitySetup
    {
        [MenuItem("Tools/Windrunner/Upgrade Fixture Readability")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Save open scenes before the fixture upgrade.");
            var scene = EditorSceneManager.OpenScene(ProgressionFixtureGuide.ScenePath, OpenSceneMode.Single);
            try
            {
                var roots = scene.GetRootGameObjects();
                var actor = roots.SelectMany(r => r.GetComponentsInChildren<ProgressionActor>(true)).Single();
                var journal = actor.GetComponent<ProgressionJournal>(); var camera = journal.interaction.view;
                foreach (var interaction in roots.SelectMany(r => r.GetComponentsInChildren<ProgressionInteraction>(true)))
                {
                    string label; Color color; PrimitiveType shape = PrimitiveType.Cylinder;
                    switch (interaction.name)
                    {
                        case "Accept character A": label = "QUEST OFFER A"; color = new Color(.2f,.6f,1); break;
                        case "Accept character B": label = "QUEST OFFER B"; color = new Color(.7f,.4f,1); break;
                        case "World switch": label = "WORLD SWITCH"; color = new Color(1,.72f,.15f); shape = PrimitiveType.Sphere; break;
                        case "Start race": label = "RACE START"; color = Color.cyan; break;
                        case "Turn in A": label = "QUEST TURN-IN A"; color = new Color(.2f,.6f,1); break;
                        case "Turn in B": label = "QUEST TURN-IN B"; color = new Color(.7f,.4f,1); break;
                        case "Puzzle switch one": label = "PUZZLE SWITCH 1"; color = new Color(1,.4f,.15f); shape = PrimitiveType.Sphere; break;
                        case "Puzzle switch two": label = "PUZZLE SWITCH 2"; color = new Color(1,.4f,.15f); shape = PrimitiveType.Sphere; break;
                        default: continue;
                    }
                    // Spread only engineering stations; retain all identities, bindings, ranges and race gates.
                    switch (interaction.name)
                    {
                        case "Accept character A": interaction.transform.position = new Vector3(-3, 1, 1); break;
                        case "Accept character B": interaction.transform.position = new Vector3(3, 1, 1); break;
                        case "Start race": interaction.transform.position = new Vector3(-3, 1, 8); break;
                        case "Puzzle switch one": interaction.transform.position = new Vector3(7, 1, 5); break;
                        case "Puzzle switch two": interaction.transform.position = new Vector3(7, 1, 9); break;
                    }
                    interaction.prompt = label;
                    var renderer = interaction.GetComponent<Renderer>(); renderer.sharedMaterial = Material(label, color);
                    Label(interaction.transform, label, "Aim + interact • within " + interaction.range + " m", camera, color);
                    if (interaction.transform.Find("Station beacon") == null)
                    {
                        var beacon = GameObject.CreatePrimitive(shape); beacon.name = "Station beacon"; beacon.transform.SetParent(interaction.transform, false);
                        beacon.transform.localPosition = new Vector3(0, .75f, 0); beacon.transform.localScale = Vector3.one * .6f;
                        UnityEngine.Object.DestroyImmediate(beacon.GetComponent<Collider>()); beacon.GetComponent<Renderer>().sharedMaterial = renderer.sharedMaterial;
                    }
                }
                foreach (var trigger in roots.SelectMany(r => r.GetComponentsInChildren<ProgressionTrigger>(true)))
                {
                    bool finish = trigger.checkpointId == "finish"; var color = finish ? new Color(.3f,1,.4f) : Color.cyan;
                    Label(trigger.transform, finish ? "RACE FINISH" : "RACE CHECKPOINT", "Pass through the arch →", camera, color);
                    {
                        Bar(trigger.transform, "Arch left", new Vector3(-5,1,0), new Vector3(.2f,4,.2f), color);
                        Bar(trigger.transform, "Arch right", new Vector3(5,1,0), new Vector3(.2f,4,.2f), color);
                        Bar(trigger.transform, "Arch top", new Vector3(0,3,0), new Vector3(10,.2f,.2f), color);
                    }
                }
                var guide = Ensure<ProgressionFixtureGuide>(actor.gameObject);
                guide.actor = actor; guide.journal = journal; guide.interaction = journal.interaction; guide.cinematic = journal.cinematics;
                guide.race = roots.SelectMany(r => r.GetComponentsInChildren<RaceHost>(true)).Single(); guide.puzzle = roots.SelectMany(r => r.GetComponentsInChildren<PuzzleHost>(true)).Single();
                var floor = roots.Single(r => r.name == "Fixture floor"); floor.GetComponent<Renderer>().sharedMaterial = Material("Floor", new Color(.08f,.14f,.2f));
                if (roots.All(r => r.name != "Fixture welcome"))
                {
                    var welcome = new GameObject("Fixture welcome"); welcome.transform.position = new Vector3(0,0,-2);
                    Label(welcome.transform, "WINDRUNNER • PROGRESSION LAB", "Blue / violet offers → gold switch → cyan race → turn-ins", camera, Color.white);
                    for (int i = 0; i < 5; i++) Bar(welcome.transform, "Route marker " + i, new Vector3(0,.03f,8 + i*4), new Vector3(.35f,.03f,1.5f), new Color(.3f,.8f,.8f));
                    var cinematic = new GameObject("Cinematic testing area"); cinematic.transform.position = new Vector3(0,0,27);
                    Label(cinematic.transform, "CINEMATIC TEST AREA", "Stop moving • open journal • play / replay unlocked clips", camera, Color.white);
                }
                var welcomeObject = scene.GetRootGameObjects().Single(r => r.name == "Fixture welcome");
                var welcomeText = welcomeObject.GetComponentInChildren<TextMesh>();
                welcomeText.characterSize = .012f; welcomeText.transform.localPosition = new Vector3(-4, 1.7f, 1);
                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(ProgressionFixtureBuilder.Folder + "/FixtureTimeline.asset");
                foreach (var track in timeline.GetRootTracks()) foreach (var clip in track.GetClips()) clip.duration = 8;
                EditorUtility.SetDirty(timeline); AssetDatabase.SaveAssetIfDirty(timeline);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
        private static T Ensure<T>(GameObject obj) where T : Component
        { var component = obj.GetComponent<T>(); return component != null ? component : obj.AddComponent<T>(); }
        private static Material Material(string name, Color color)
        {
            string path = ProgressionFixtureBuilder.Folder + "/" + name.Replace(" ", "_") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.color = color; EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); return material;
        }
        private static void Label(Transform parent, string title, string instructions, Camera camera, Color color)
        {
            var existing = parent.Find("World label"); var obj = existing != null ? existing.gameObject : new GameObject("World label");
            obj.transform.SetParent(parent, false); obj.transform.localPosition = new Vector3(0, 1.4f, 0);
            var text = Ensure<TextMesh>(obj); text.text = title + "\n" + instructions;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); obj.GetComponent<MeshRenderer>().sharedMaterial = text.font.material; text.fontSize = 64; text.characterSize = .035f; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = color;
            Ensure<FixtureWorldLabel>(obj).view = camera;
        }
        private static void Bar(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            var existing = parent.Find(name); var bar = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube); bar.name = name; bar.transform.SetParent(parent, false); bar.transform.localPosition = position; bar.transform.localScale = scale;
            if (bar.GetComponent<Collider>() != null) UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>()); bar.GetComponent<Renderer>().sharedMaterial = Material("Route_" + ColorUtility.ToHtmlStringRGB(color), color);
        }
    }
}
