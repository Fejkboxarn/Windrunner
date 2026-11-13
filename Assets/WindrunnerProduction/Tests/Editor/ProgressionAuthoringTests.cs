using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Windrunner.Production.Progression;
using Object = UnityEngine.Object;

namespace Windrunner.Production.Tests
{
    public sealed class ProgressionAuthoringTests
    {
        private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AirflowPrototype.EditorTools." + name)).First(t => t != null);
        [Test] public void FixtureAndStudioCopyHaveValidReferencesAndIndependentStableIds()
        {
            // Creates only a new engineering fixture; refuses overwrites and preserves the current scene.
            Type("ProgressionFixtureBuilder").GetMethod("Create").Invoke(null, null);
            var catalog = AssetDatabase.LoadAssetAtPath<ProgressionCatalog>("Assets/WindrunnerProduction/Progression/Fixtures/FixtureCatalog.asset");
            Assert.IsNotNull(catalog); Assert.IsEmpty(ProgressionValidation.Validate(catalog.definitions, true));
            var copy = (ProgressionCatalog)Type("ProgressionStudio").GetMethod("CopyWithNewIds").Invoke(null, new object[] { catalog });
            try
            {
                Assert.IsEmpty(ProgressionValidation.Validate(copy.definitions, true));
                Assert.AreNotEqual(catalog.definitions.quests[0].id, copy.definitions.quests[0].id);
                Assert.AreEqual(copy.definitions.quests[0].characterId, copy.definitions.relationships[0].id);
                Assert.AreSame(catalog.definitions.cutscenes[0].content, copy.definitions.cutscenes[0].content);
                Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(copy, "fixture add");
                int count = copy.definitions.rewards.Count;
                Type("ProgressionStudio").GetMethod("AddRecord").Invoke(null, new object[] { copy, "rewards" });
                Undo.FlushUndoRecordObjects(); Assert.AreEqual(count + 1, copy.definitions.rewards.Count);
                Undo.PerformUndo(); Assert.AreEqual(count, copy.definitions.rewards.Count);
            }
            finally { Undo.ClearUndo(copy); Object.DestroyImmediate(copy); }
        }
    }
}
