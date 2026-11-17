using System;
using Windrunner.Production.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Windrunner.Production.Progression
{
    [Serializable]
    public sealed class PlaythroughRecord
    {
        public string slotId, displayName, world, anchor, revision;
        public long savedUtc;
        public ProgressionState progression;
    }

    public sealed class SlotEntry
    {
        public string Id, Problem;
        public PlaythroughRecord Record;
        public bool Recovered;
    }

    public sealed class PlaythroughRevisionException : IOException
    {
        public PlaythroughRevisionException() : base("Playthrough changed since it was loaded.") { }
    }

    public sealed class PlaythroughSlots
    {
        public const string World = "starting-world", Arrival = "arrival", Zone01 = "zone-01";
        [Serializable] private sealed class Envelope { public int version; public string body, checksum; }
        private static readonly UTF8Encoding SlotEncoding = new UTF8Encoding(false, true);
        private readonly string root;
        public PlaythroughSlots(string directory) { root = Path.GetFullPath(directory); }
        private string DirectoryFor(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid playthrough ID.");
            return Path.Combine(root, id);
        }
        private static string Digest(string payload)
        {
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }
        private static PlaythroughRecord Read(string path,string id)
        {
            var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
            if (envelope == null || envelope.version == 0) throw new InvalidDataException("Missing save header.");
            if (envelope.version != 1) throw new NotSupportedException("Requires a different save version.");
            if (envelope.body == null || Digest(envelope.body) != envelope.checksum) throw new InvalidDataException("Save checksum failed.");
            var record = JsonUtility.FromJson<PlaythroughRecord>(envelope.body);
            if (record == null || record.slotId != id || string.IsNullOrWhiteSpace(record.displayName) ||
                !Guid.TryParseExact(record.revision,"N",out _) || record.savedUtc <= 0 || record.savedUtc > DateTime.MaxValue.Ticks)
                throw new InvalidDataException("Invalid playthrough metadata.");
            if ((record.world != World && record.world != Zone01) || record.anchor != Arrival) throw new NotSupportedException("World or checkpoint is unavailable.");
            if (record.progression==null) throw new InvalidDataException("Missing progression records.");
            if (record.progression.schemaVersion>1) throw new NotSupportedException("Requires a newer progression version.");
            record.progression = ProgressionCodec.Decode(ProgressionCodec.Encode(record.progression));
            if(record.progression.playerId!="local-player") throw new InvalidDataException("Unexpected player identity.");
            return record;
        }
        private static bool Damaged(Exception e) => e is InvalidDataException || e is ArgumentException || e is FileNotFoundException;
        public SlotEntry Inspect(string id)
        {
            string file = Path.Combine(DirectoryFor(id),"session.json");
            var result = new SlotEntry { Id=id };
            try { result.Record=Read(file,id); }
            catch(Exception first) when(Damaged(first))
            {
                try { result.Record=Read(file+".bak",id); result.Recovered=true; }
                catch(Exception backup) when(Damaged(backup) || backup is NotSupportedException) { result.Problem=first.Message; }
            }
            catch(NotSupportedException e) { result.Problem=e.Message; }
            return result;
        }
        public List<SlotEntry> List()
        {
            if(!Directory.Exists(root)) return new List<SlotEntry>();
            return Directory.GetDirectories(root).Select(Path.GetFileName).Where(id=>Guid.TryParseExact(id,"N",out _))
                .Select(Inspect).OrderByDescending(x=>x.Record?.savedUtc ?? 0).ThenBy(x=>x.Id,StringComparer.Ordinal).ToList();
        }
        public SlotEntry Latest() => List().FirstOrDefault(x=>x.Record!=null);
        private IDisposable LockWrites()
        {
            Directory.CreateDirectory(root);
            return new FileStream(Path.Combine(root,"write.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,1);
        }
        public PlaythroughRecord Create(string name, string replace=null, bool confirmed=false, ProgressionState import=null, string world=World)
        {
            if(world!=World && world!=Zone01)throw new ArgumentException("Unknown world.");
            using var writeLock=LockWrites();
            if(string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a playthrough name.");
            if(replace!=null && (!confirmed || !Directory.Exists(DirectoryFor(replace)))) throw new InvalidOperationException("Confirm replacement of an existing slot.");
            var progression=import==null?new ProgressionState{playerId="local-player"}:ProgressionCodec.Decode(ProgressionCodec.Encode(import));
            if(progression.playerId!="local-player")throw new InvalidDataException("Only local-player progression can be imported.");
            var record=new PlaythroughRecord {slotId=replace??Guid.NewGuid().ToString("N"),displayName=name.Trim(),world=world,anchor=Arrival,progression=progression};
            Write(record,false,true);return record;
        }
        public void Delete(string id,bool confirmed)
        {
            if(!confirmed)throw new InvalidOperationException("Confirm deletion.");
            using var writeLock=LockWrites();
            string folder=DirectoryFor(id);
            // Atomic removal from the active namespace, including both copies.
            Directory.Move(folder,Path.Combine(root,"deleted-"+Guid.NewGuid().ToString("N")));
        }
        public IProgressionStore Open(string id)=>Open(id,out _);
        public IProgressionStore Open(string id,out string world)
        {
            var slot=Inspect(id);
            if(slot.Record==null)throw new InvalidDataException(slot.Problem);
            world=slot.Record.world;
            return new BoundStore(this,slot.Record);
        }
        private sealed class BoundStore : IProgressionStore
        {
            private readonly PlaythroughSlots slots;
            private PlaythroughRecord record;
            public BoundStore(PlaythroughSlots owner,PlaythroughRecord current){slots=owner;record=current;}
            public ProgressionState Load()=>ProgressionCodec.Decode(ProgressionCodec.Encode(record.progression));
            public void Commit(ProgressionState state)
            {
                RewardProbe.Begin(RewardProbe.Phase.Lock);
                using var writeLock=slots.LockWrites();
                RewardProbe.End(RewardProbe.Phase.Lock);
                RewardProbe.Begin(RewardProbe.Phase.Inspect);
                var current=slots.Inspect(record.slotId);
                RewardProbe.End(RewardProbe.Phase.Inspect);
                if(current.Record?.revision!=record.revision)throw new PlaythroughRevisionException();
                var next=current.Record;
                RewardProbe.Begin(RewardProbe.Phase.Validate);
                next.progression=ProgressionCodec.Decode(ProgressionCodec.Encode(state));
                RewardProbe.End(RewardProbe.Phase.Validate);
                if(next.progression.playerId!=record.progression.playerId)throw new InvalidDataException("Player identity changed.");
                slots.Write(next,!current.Recovered,false);record=next;
            }
        }
        private static void Publish(string staged, string destination, string backup)
        {
            if (File.Exists(destination)) File.Replace(staged,destination,backup);
            else File.Move(staged,destination);
        }
        private void Write(PlaythroughRecord record,bool retainPrevious,bool newPlaythrough)
        {
            string directory=DirectoryFor(record.slotId);Directory.CreateDirectory(directory);
            string file=Path.Combine(directory,"session.json"),temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            record.savedUtc=DateTime.UtcNow.Ticks;record.revision=Guid.NewGuid().ToString("N");
            RewardProbe.Begin(RewardProbe.Phase.Serialize);
            string body=JsonUtility.ToJson(record);
            string json=JsonUtility.ToJson(new Envelope{version=1,body=body,checksum=Digest(body)});
            RewardProbe.End(RewardProbe.Phase.Serialize);
            try
            {
                RewardProbe.Begin(RewardProbe.Phase.TempWrite);
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,1))
                {
                    // Avoid two stream buffers on small reward snapshots and a second full-size
                    // byte array when a long-running playthrough grows large.
                    if (json.Length <= 16384)
                    {
                        byte[] payload = SlotEncoding.GetBytes(json);
                        stream.Write(payload, 0, payload.Length);
                    }
                    else
                    {
                        using var writer = new StreamWriter(stream, SlotEncoding, 1024, true);
                        writer.Write(json);
                    }
                    RewardProbe.End(RewardProbe.Phase.TempWrite);
                    RewardProbe.Begin(RewardProbe.Phase.Flush);
                    stream.Flush(true);
                    RewardProbe.End(RewardProbe.Phase.Flush);
                }
                if(newPlaythrough)
                {
                    string backupTemp=temp+".bak";
                    try
                    {
                        File.Copy(temp,backupTemp);
                        using (var backupStream = new FileStream(backupTemp,FileMode.Open,FileAccess.Write,FileShare.None))
                            backupStream.Flush(true);
                        string backup = file+".bak";
                        if (!File.Exists(file) && !File.Exists(backup))
                        {
                            // A fresh slot is recoverable even if interrupted before its primary appears.
                            File.Move(backupTemp,backup);
                            File.Move(temp,file);
                        }
                        else
                        {
                            // Until the replacement commits, the previous backup may be the only
                            // readable snapshot. Never publish a proposed playthrough over that copy.
                            Publish(temp,file,null);
                            try { Publish(backupTemp,backup,null); }
                            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                            {
                                Debug.LogWarning("Playthrough saved. Could not refresh the recovery copy: "+e.Message);
                            }
                        }
                    }
                    finally { if(File.Exists(backupTemp))File.Delete(backupTemp); }
                }
                else
                {
                    RewardProbe.Begin(RewardProbe.Phase.Publish);
                    Publish(temp,file,retainPrevious?file+".bak":null);
                    RewardProbe.End(RewardProbe.Phase.Publish);
                }
            }
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
