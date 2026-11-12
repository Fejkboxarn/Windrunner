using System;
using NUnit.Framework;
namespace Windrunner.Production.Tests
{
    public sealed class CharacterTuningEditorTests
    {
        [Test] public void ProfilesPreserveAuthoredValuesAndDraftsRemainIsolated()
        {
            Type.GetType("AirflowPrototype.Editor.CharacterTuningChecks, Assembly-CSharp-Editor",true).GetMethod("Run").Invoke(null,null);
        }
    }
}
