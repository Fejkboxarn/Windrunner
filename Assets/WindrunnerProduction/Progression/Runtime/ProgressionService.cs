using System;
using System.Collections.Generic;
using System.Linq;

namespace Windrunner.Production.Progression
{
    /// <summary>Single player/save authority. Commands persist a private candidate before publication.</summary>
    public sealed class ProgressionService : IDisposable
    {
        private readonly ProgressionDefinitions definitions;
        private readonly IProgressionStore store;
        private readonly Dictionary<string, IObjectiveEvaluator> evaluators;
        private ProgressionState state;
        private bool disposed, publishing;
        public event Action Changed;
        public event Action<Exception> ListenerFailed;
        public string PlayerId => state.playerId;
        public bool MatchesDefinitions(ProgressionDefinitions candidate) => UnityEngine.JsonUtility.ToJson(definitions) == UnityEngine.JsonUtility.ToJson(candidate);
        public bool HasUnlock(string contentId) => state.rewards.Any(r => r.kind == "unlock" && r.contentId == contentId);
        public bool HasPersistentFact(FactKind kind, string targetId) => state.persistentFacts.Any(f => f.kind == kind && f.targetId == targetId);
        public DaggerLoadout ReadDaggers()=>state.daggers==null?new DaggerLoadout():
            UnityEngine.JsonUtility.FromJson<DaggerLoadout>(UnityEngine.JsonUtility.ToJson(state.daggers));
        public string EquippedDagger=>state.daggers?.equipped;
        public bool OwnsDagger(string id)=>state.daggers?.owned.Contains(id)==true;
        public float DaggerCooldownRemaining(string effectId)=>state.daggers?.Remaining(effectId)??0f;
        public void AdvanceDaggerCooldowns(float seconds)=>state.daggers?.Advance(seconds);
        public void SaveDaggers(DaggerLoadout record)=>Commit(next=>{
            next.daggers=UnityEngine.JsonUtility.FromJson<DaggerLoadout>(UnityEngine.JsonUtility.ToJson(record));
            return true;
        });
        public ProgressionState Snapshot() => ProgressionCodec.Copy(state);
        public ProgressionService(ProgressionDefinitions definitions, string playerId, IProgressionStore store,
            Dictionary<string, IObjectiveEvaluator> evaluators = null)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            this.definitions = UnityEngine.JsonUtility.FromJson<ProgressionDefinitions>(UnityEngine.JsonUtility.ToJson(definitions));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.evaluators = evaluators == null ? new Dictionary<string, IObjectiveEvaluator>() : new Dictionary<string, IObjectiveEvaluator>(evaluators);
            this.evaluators["fact"] = new FactObjectiveEvaluator();
            var errors = ProgressionValidation.Validate(definitions, false, new HashSet<string>(this.evaluators.Keys));
            if (errors.Count > 0) throw new ArgumentException(string.Join("\n", errors));
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("Player identity required.");
            state = store.Load() ?? new ProgressionState { playerId = playerId };
            if (state.playerId != playerId) throw new ArgumentException("Save belongs to another player.");
            foreach (var c in state.cutscenes) if (c.status == CinematicStatus.Playing) c.status = CinematicStatus.Pending;
            foreach (var q in this.definitions.quests)
            {
                var record = state.quests.Find(x => x.id == q.id);
                if (record == null || record.status == QuestStatus.Completed) continue;
                if (record.receivedOccurrences == null) record.receivedOccurrences = new List<string>();
                foreach (var o in q.groups.SelectMany(g => g.objectives))
                    if (!record.objectives.Any(x => x.id == o.id)) record.objectives.Add(new ObjectiveRecord { id = o.id });
                // New objectives on installed content cannot retain a stale ReadyToTurnIn record.
                if (!q.groups.All(g => GroupComplete(g, record))) record.status = QuestStatus.Active;
            }
        }
        private bool Commit(Func<ProgressionState, bool> operation)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ProgressionService));
            if (publishing) throw new InvalidOperationException("Progression mutations cannot reenter notification dispatch.");
            var next = ProgressionCodec.Copy(state);
            if (!operation(next)) return false;
            store.Commit(next); state = next;
            publishing = true;
            try
            {
                if (Changed != null) foreach (Action listener in Changed.GetInvocationList())
                    try { listener(); } catch (Exception ex) { try { ListenerFailed?.Invoke(ex); } catch { /* Diagnostic callbacks cannot invalidate committed state. */ } }
            }
            finally { publishing = false; }
            return true;
        }
        public QuestStatus Status(string id) => Status(state, id);
        private QuestStatus Status(ProgressionState s, string id)
        {
            var q = definitions.quests.Find(x => x.id == id);
            if (q == null) return QuestStatus.Locked;
            var record = s.quests.Find(x => x.id == id);
            if (record != null) return record.status;
            if (q.requiresQuests.Any(x => !Completed(s, x))) return QuestStatus.Locked;
            if (q.kind == QuestKind.Story)
            {
                var owner = definitions.relationships.Find(x => x.id == q.characterId);
                var r = s.relationships.Find(x => x.id == owner.id);
                int chapterIndex = owner.chapters.FindIndex(x => x.id == q.chapterId);
                if ((r?.rank ?? 0) != chapterIndex) return QuestStatus.Locked;
                if (chapterIndex > 0 && !Resolved(s, owner.chapters[chapterIndex - 1].cutsceneId)) return QuestStatus.Locked;
                if (owner.chapters.Any(c => s.quests.Any(x => x.id == c.questId && x.status != QuestStatus.Completed))) return QuestStatus.Locked;
            }
            return QuestStatus.Available;
        }
        private static bool Completed(ProgressionState s, string id) => s.quests.Any(q => q.id == id && q.status == QuestStatus.Completed);
        private static bool Resolved(ProgressionState s, string id) => s.cutscenes.Any(c => c.id == id && (c.status == CinematicStatus.Watched || c.status == CinematicStatus.Skipped));
        public bool ObjectiveOpen(string questId, string objectiveId)
        {
            var record = state.quests.Find(q => q.id == questId);
            if (record == null || record.status != QuestStatus.Active) return false;
            var quest = definitions.quests.Find(q => q.id == questId);
            if (quest == null) return false;
            foreach (var group in quest.groups)
            {
                var objective = group.objectives.Find(o => o.id == objectiveId);
                if (objective == null) continue;
                var progress = record.objectives.Find(o => o.id == objectiveId);
                return progress != null && progress.count < objective.count &&
                    group.requiresGroups.All(id => GroupComplete(quest.groups.Find(g => g.id == id),record));
            }
            return false;
        }
        public bool Accept(string id) => Commit(s => {
            if (Status(s, id) != QuestStatus.Available) return false;
            var q = definitions.quests.Find(x => x.id == id);
            var record = new QuestRecord { id = id, status = QuestStatus.Active };
            foreach (var o in q.groups.SelectMany(g => g.objectives)) record.objectives.Add(new ObjectiveRecord { id = o.id });
            s.quests.Add(record);
            Reconcile(s, q, record); return true;
        });
        public bool Track(string id) => Commit(s => {
            if (!string.IsNullOrEmpty(id) && !s.quests.Any(q => q.id == id && q.status != QuestStatus.Completed)) return false;
            if (s.trackedQuestId == id) return false; s.trackedQuestId = id; return true;
        });
        public bool Observe(GameplayFact fact)
        {
            if (fact == null || !fact.verified || fact.playerId != PlayerId || string.IsNullOrWhiteSpace(fact.occurrenceId) ||
                float.IsNaN(fact.seconds) || float.IsInfinity(fact.seconds) || fact.seconds < 0) return false;
            return Commit(s => Observe(s, fact));
        }
        private bool Observe(ProgressionState s, GameplayFact fact)
        {
            bool changed = false;
            if (fact.persistent && !s.persistentFacts.Any(f => f.occurrenceId == fact.occurrenceId))
            {
                // Copy external payload; callers cannot mutate saved state after dispatch.
                s.persistentFacts.Add(UnityEngine.JsonUtility.FromJson<GameplayFact>(UnityEngine.JsonUtility.ToJson(fact))); changed = true;
            }
            foreach (var q in definitions.quests)
            {
                var r = s.quests.Find(x => x.id == q.id && x.status == QuestStatus.Active);
                if (r == null) continue;
                if (r.receivedOccurrences.Contains(fact.occurrenceId) || !q.groups.SelectMany(g => g.objectives).Any(o => evaluators[o.evaluator].Matches(o, fact))) continue;
                r.receivedOccurrences.Add(fact.occurrenceId); changed = true;
                // Snapshot unlocked groups: one occurrence cannot open and satisfy a later sequential stage.
                var open = q.groups.Where(g => g.requiresGroups.All(pre => GroupComplete(q.groups.Find(x => x.id == pre), r))).ToArray();
                foreach (var g in open) foreach (var o in g.objectives) changed |= Credit(o, r, fact);
                Reconcile(s, q, r);
            }
            return changed;
        }
        private bool Credit(ObjectiveDefinition o, QuestRecord r, GameplayFact f)
        {
            var progress = r.objectives.Find(x => x.id == o.id);
            if (progress.count >= o.count || progress.occurrences.Contains(f.occurrenceId) || !evaluators[o.evaluator].Matches(o, f)) return false;
            if (o.uniqueTargets && (string.IsNullOrEmpty(f.targetId) || progress.targets.Contains(f.targetId))) return false;
            progress.occurrences.Add(f.occurrenceId);
            if (o.uniqueTargets) progress.targets.Add(f.targetId);
            progress.count++; return true;
        }
        private static bool GroupComplete(ObjectiveGroup group, QuestRecord record) => group.objectives.All(o => record.objectives.Any(r => r.id == o.id && r.count >= o.count));
        private void Reconcile(ProgressionState s, QuestDefinition q, QuestRecord r)
        {
            // Bounded propagation allows a persistent discovery to satisfy newly unlocked retroactive groups.
            for (int pass = 0; pass < q.groups.Count; pass++)
                foreach (var g in q.groups)
                    if (g.requiresGroups.All(pre => GroupComplete(q.groups.Find(x => x.id == pre), r)))
                        foreach (var o in g.objectives.Where(o => o.credit == CreditPolicy.PersistentRetroactive))
                            foreach (var fact in s.persistentFacts) Credit(o, r, fact);
            if (q.groups.All(g => GroupComplete(g, r)))
            {
                r.status = QuestStatus.ReadyToTurnIn;
                if (q.automaticTurnIn && q.kind == QuestKind.Optional) Complete(s, q, r);
            }
        }
        public bool TurnIn(string id) => Commit(s => {
            var q = definitions.quests.Find(x => x.id == id);
            var r = s.quests.Find(x => x.id == id && x.status == QuestStatus.ReadyToTurnIn);
            if (q == null || r == null) return false;
            Complete(s, q, r); return true;
        });
        private void Complete(ProgressionState s, QuestDefinition q, QuestRecord r)
        {
            if (q.kind == QuestKind.Story)
            {
                var owner = definitions.relationships.Find(x => x.id == q.characterId);
                var chapter = owner.chapters.Find(x => x.id == q.chapterId);
                var relationship = s.relationships.Find(x => x.id == owner.id);
                if (relationship == null) { relationship = new RelationshipRecord { id = owner.id }; s.relationships.Add(relationship); }
                if (relationship.rank != chapter.rank - 1 || relationship.chapters.Contains(chapter.id)) throw new InvalidOperationException("Chapter state conflict.");
                relationship.rank = chapter.rank; relationship.chapters.Add(chapter.id);
                Grant(s, "chapter/" + owner.id + "/" + chapter.id, chapter.rewards);
                s.cutscenes.Add(new CutsceneRecord { id = chapter.cutsceneId, status = CinematicStatus.Pending });
            }
            r.status = QuestStatus.Completed;
            Grant(s, "quest/" + q.id, q.rewards);
            if (s.trackedQuestId == q.id) s.trackedQuestId = null;
        }
        private void Grant(ProgressionState s, string operation, IEnumerable<string> rewards)
        {
            foreach (string rewardId in rewards)
            {
                string claim = operation + "/" + rewardId;
                if (s.rewards.Any(x => x.id == claim)) continue;
                var reward = definitions.rewards.Find(x => x.id == rewardId);
                s.rewards.Add(new RewardClaim { id = claim, rewardId = rewardId, kind = reward.kind, contentId = reward.contentId, amount = reward.amount });
            }
        }
        public bool StartCutscene(string id, bool replay = false) => Commit(s => {
            var c = s.cutscenes.Find(x => x.id == id);
            if (c == null || s.cutscenes.Any(x => x.status == CinematicStatus.Playing) || (!replay && c.status != CinematicStatus.Pending) ||
                (replay && c.status != CinematicStatus.Watched && c.status != CinematicStatus.Skipped)) return false;
            // Replay is presentation-only; keep previously resolved durable records resolved.
            if (!replay) c.status = CinematicStatus.Playing;
            return true;
        });
        public bool ResolveCutscene(string id, bool skipped) => Commit(s => {
            var c = s.cutscenes.Find(x => x.id == id);
            var definition = definitions.cutscenes.Find(x => x.id == id);
            if (c == null || c.status != CinematicStatus.Playing || definition == null || (skipped && !definition.allowSkip)) return false;
            c.status = skipped ? CinematicStatus.Skipped : CinematicStatus.Watched; return true;
        });
        public bool InterruptCutscene(string id) => Commit(s => {
            var c = s.cutscenes.Find(x => x.id == id && x.status == CinematicStatus.Playing);
            if (c == null) return false; c.status = CinematicStatus.Pending; return true;
        });
        public bool DiscoverChallenge(string id) => Commit(s => {
            if (!definitions.challenges.Any(x => x.id == id) || s.challenges.Any(x => x.id == id && x.discovered)) return false;
            var c = s.challenges.Find(x => x.id == id);
            if (c == null) { c = new ChallengeRecord { id = id }; s.challenges.Add(c); }
            c.discovered = true; return true;
        });
        public bool ChallengeAvailable(string id)
        {
            var d = definitions.challenges.Find(x => x.id == id);
            var r = state.challenges.Find(x => x.id == id);
            return d != null && r != null && r.discovered && (!r.completed || d.replayable) && d.requiresQuests.All(x => Completed(state, x));
        }
        internal bool CompleteChallenge(string id, string attempt, float seconds, string region, PuzzleDefinition puzzle = null) => Commit(s => {
            if (!ChallengeAvailable(id) || string.IsNullOrWhiteSpace(attempt) || seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return false;
            var d = definitions.challenges.Find(x => x.id == id); var r = s.challenges.Find(x => x.id == id);
            if (r.successfulAttempts.Contains(attempt)) return false;
            r.successfulAttempts.Add(attempt);
            if (!r.completed || seconds < r.personalBest) r.personalBest = seconds;
            r.completed = true; Grant(s, "challenge/" + id, d.firstClearRewards);
            if (puzzle != null && puzzle.permanent)
            {
                var record = s.puzzles.Find(x => x.id == puzzle.id);
                if (record == null) { record = new PuzzleRecord { id = puzzle.id }; s.puzzles.Add(record); }
                record.solved = true; record.mechanisms = new List<string>(puzzle.mechanisms);
            }
            Observe(s, new GameplayFact { playerId = s.playerId, occurrenceId = "challenge/" + id + "/" + attempt, attemptId = attempt,
                definitionId = id, targetId = id, regionId = region, kind = d.resultKind, seconds = seconds, verified = true,
                persistent = puzzle != null && puzzle.permanent }); return true;
        });
        internal bool SavePuzzle(string id, bool solved, IEnumerable<string> mechanisms) => Commit(s => {
            var r = s.puzzles.Find(x => x.id == id);
            if (r == null) { r = new PuzzleRecord { id = id }; s.puzzles.Add(r); }
            r.solved = solved; r.mechanisms = new List<string>(mechanisms); return true;
        });
        public void Dispose() { disposed = true; Changed = null; ListenerFailed = null; }
    }
}
