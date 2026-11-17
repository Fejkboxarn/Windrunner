using System;
using System.IO;
using NUnit.Framework;
using Windrunner.Production.Progression;

namespace Windrunner.Production.Tests
{
    public sealed class PlaythroughSlotTests
    {
        private string path;
        private PlaythroughSlots slots;
        [SetUp] public void Start(){path=Path.Combine(Path.GetTempPath(),"Windrunner-test-"+Guid.NewGuid().ToString("N"));slots=new PlaythroughSlots(path);}
        [TearDown] public void End(){if(Directory.Exists(path))Directory.Delete(path,true);}
        private string FileFor(string id)=>Path.Combine(path,id,"session.json");

        [Test] public void IndependentSlotsAutoAndManualSaveUseOneProgressionRecord()
        {
            var a=slots.Create("Same name");var b=slots.Create("Same name");Assert.AreNotEqual(a.slotId,b.slotId);
            var store=slots.Open(a.slotId);
            using var authority=new ProgressionService(new ProgressionDefinitions(),"local-player",store);
            Assert.IsTrue(authority.Observe(new GameplayFact{occurrenceId="arrival-1",playerId="local-player",targetId="clearing",
                definitionId="discovery",kind=FactKind.LocationDiscovered,verified=true,persistent=true}));
            var revision=slots.Inspect(a.slotId).Record.revision;
            store.Commit(authority.Snapshot());
            Assert.AreNotEqual(revision,slots.Inspect(a.slotId).Record.revision);
            Assert.AreEqual(1,slots.Open(a.slotId).Load().persistentFacts.Count);
            Assert.IsEmpty(slots.Open(b.slotId).Load().persistentFacts);
            Assert.AreEqual(a.slotId,slots.Latest().Id);
        }
        [Test] public void ConfirmationAndRevisionProtectOverwritesAndDeletes()
        {
            var a=slots.Create("First");var stale=slots.Open(a.slotId);
            Assert.Throws<InvalidOperationException>(()=>slots.Create("Replacement",a.slotId));
            Assert.Throws<InvalidOperationException>(()=>slots.Delete(a.slotId,false));
            Assert.Throws<ArgumentException>(()=>slots.Delete("../outside",true));
            slots.Create("Replacement",a.slotId,true);
            Assert.Throws<IOException>(()=>stale.Commit(stale.Load()));
            slots.Delete(a.slotId,true);Assert.IsEmpty(slots.List());
        }
        [Test] public void CorruptionRecoversLastGoodCopyAndUnsupportedVersionsStayUntouched()
        {
            var older=slots.Create("Older");var a=slots.Create("Active");var store=slots.Open(a.slotId);
            var state=store.Load();state.equipmentReferences.Add("pair-reference");store.Commit(state);
            File.WriteAllText(FileFor(a.slotId),"broken");
            Assert.IsTrue(slots.Inspect(a.slotId).Recovered);
            var recovered=slots.Open(a.slotId);recovered.Commit(recovered.Load());Assert.IsFalse(slots.Inspect(a.slotId).Recovered);
            File.WriteAllText(FileFor(a.slotId),"{\"version\":99}");
            Assert.IsNull(slots.Inspect(a.slotId).Record);Assert.AreEqual(older.slotId,slots.Latest().Id);
            Assert.Throws<InvalidDataException>(()=>slots.Open(a.slotId));
            Assert.AreEqual("{\"version\":99}",File.ReadAllText(FileFor(a.slotId)));
        }
        [Test] public void LegacyImportCopiesResolvedProgressAndRejectsFixtureIdentity()
        {
            var legacy=new ProgressionState{playerId="local-player"};
            legacy.cutscenes.Add(new CutsceneRecord{id="cinematic",status=CinematicStatus.Watched});
            legacy.rewards.Add(new RewardClaim{id="grant",amount=1});
            legacy.relationships.Add(new RelationshipRecord{id="character",rank=2});
            var record=slots.Create("Imported",import:legacy);var loaded=slots.Open(record.slotId).Load();
            Assert.AreEqual(CinematicStatus.Watched,loaded.cutscenes[0].status);
            Assert.AreEqual(2,loaded.relationships[0].rank);Assert.AreEqual(1,loaded.rewards.Count);
            Assert.IsEmpty(slots.Create("Independent").progression.cutscenes);
            legacy.playerId="engineering-fixture";
            Assert.Throws<InvalidDataException>(()=>slots.Create("Wrong",import:legacy));
        }
    }
}
