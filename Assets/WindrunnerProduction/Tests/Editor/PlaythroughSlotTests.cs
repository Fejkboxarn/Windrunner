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
        [Test,Platform("Win")] public void FailedReservePublicationLeavesAuthorityAndSlotRetryable()
        {
            var slot=slots.Create("Held destination");
            var store=slots.Open(slot.slotId);
            using var service=new ProgressionService(new ProgressionDefinitions(),"local-player",store);
            var loadout=new DaggerLoadout{initialized=true,equipped="farseer"};
            loadout.owned.Add("farseer");loadout.SetStored("farseer",9);
            service.SaveDaggers(loadout);
            var previous=slots.Inspect(slot.slotId).Record.revision;
            int notifications=0;
            service.Changed+=()=>notifications++;
            // On Windows an open reader without delete sharing allows Inspect but blocks File.Replace.
            using(var reader=new FileStream(FileFor(slot.slotId),FileMode.Open,FileAccess.Read,FileShare.Read))
                Assert.Throws<IOException>(()=>service.SetDaggerReserve("farseer",13));
            Assert.Zero(notifications);
            Assert.AreEqual(9,service.StoredDaggerReserve("farseer"));
            Assert.AreEqual(previous,slots.Inspect(slot.slotId).Record.revision);
            Assert.AreEqual(9,slots.Open(slot.slotId).Load().daggers.Stored("farseer"));
            Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(FileFor(slot.slotId)),"*.tmp"));
            service.SetDaggerReserve("farseer",13);
            Assert.AreEqual(1,notifications);
            Assert.AreEqual(13,slots.Open(slot.slotId).Load().daggers.Stored("farseer"));
        }

        [Test,Platform("Win")] public void FailedOverwriteOfRecoveredSlotCannotPublishAnUncommittedBackup()
        {
            var original=slots.Create("Original");
            string file=FileFor(original.slotId);
            File.WriteAllText(file,"interrupted");
            Assert.IsTrue(slots.Inspect(original.slotId).Recovered);
            using(var held=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))
                Assert.Throws<IOException>(()=>slots.Create("Replacement",original.slotId,true,world:PlaythroughSlots.Zone01));
            var after=slots.Inspect(original.slotId);
            Assert.IsTrue(after.Recovered);
            Assert.AreEqual(original.revision,after.Record.revision);
            Assert.AreEqual(PlaythroughSlots.World,after.Record.world);
            slots.Create("Replacement",original.slotId,true,world:PlaythroughSlots.Zone01);
            File.WriteAllText(file,"interrupted again");
            Assert.AreEqual(PlaythroughSlots.Zone01,slots.Inspect(original.slotId).Record.world);
        }

        [Test] public void LargeUnicodeRecordSurvivesBothPublishedCopies()
        {
            string content=new string('界',30000)+"🌬";
            var slot=slots.Create("Long journey");
            var store=slots.Open(slot.slotId);
            var state=store.Load();state.equipmentReferences.Add(content);
            store.Commit(state);store.Commit(state);
            Assert.AreEqual(content,slots.Open(slot.slotId).Load().equipmentReferences[0]);
            File.WriteAllText(FileFor(slot.slotId),"broken");
            Assert.AreEqual(content,slots.Open(slot.slotId).Load().equipmentReferences[0]);
        }

        [Test,Platform("Win")] public void CommittedOverwriteRetainsPreviousRecoveryIfReseedingFails()
        {
            var original=slots.Create("Previous");
            string file=FileFor(original.slotId);
            using(var held=new FileStream(file+".bak",FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,new System.Text.RegularExpressions.Regex("Could not refresh the recovery copy"));
                slots.Create("New",original.slotId,true,world:PlaythroughSlots.Zone01);
            }
            Assert.AreEqual("New",slots.Inspect(original.slotId).Record.displayName);
            File.WriteAllText(file,"broken");
            Assert.IsTrue(slots.Inspect(original.slotId).Recovered);
            Assert.AreEqual(original.revision,slots.Inspect(original.slotId).Record.revision);
        }

        [Test] public void MissingPrimaryUsesPreviousDurableRewardAndIgnoresUnpublishedStaging()
        {
            var slot=slots.Create("Recovery");
            var store=slots.Open(slot.slotId);
            var state=store.Load();
            state.daggers=new DaggerLoadout{initialized=true,equipped="farseer"};
            state.daggers.owned.Add("farseer");
            state.daggers.SetStored("farseer",9);store.Commit(state);
            state.daggers.SetStored("farseer",13);store.Commit(state);
            string primary=FileFor(slot.slotId);
            File.Move(primary,primary+".orphan.tmp");
            var recovered=slots.Inspect(slot.slotId);
            Assert.IsTrue(recovered.Recovered);
            Assert.AreEqual(9,recovered.Record.progression.daggers.Stored("farseer"));
            var reopened=slots.Open(slot.slotId);
            var next=reopened.Load();next.daggers.SetStored("farseer",12);reopened.Commit(next);
            Assert.AreEqual(12,slots.Open(slot.slotId).Load().daggers.Stored("farseer"));
            File.WriteAllText(primary,"truncated");
            Assert.AreEqual(9,slots.Open(slot.slotId).Load().daggers.Stored("farseer"));
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
