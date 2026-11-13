using System;
using System.Collections.Generic;
using System.Linq;

namespace Windrunner.Production.Progression
{
    public static class ProgressionValidation
    {
        public static List<string> Validate(ProgressionDefinitions d, bool requireContent = false, ISet<string> evaluators = null)
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            Action<string, string> id = (value, location) => {
                if (string.IsNullOrWhiteSpace(value) || !ids.Add(value)) errors.Add(location + ": missing/duplicate stable ID '" + value + "'.");
            };
            foreach (var q in d.quests) id(q.id, "Quest");
            foreach (var r in d.relationships) id(r.id, "Relationship");
            foreach (var r in d.rewards) { id(r.id, "Reward"); if (r.amount <= 0 || string.IsNullOrWhiteSpace(r.kind) || string.IsNullOrWhiteSpace(r.contentId)) errors.Add(r.id + ": reward needs kind, content and positive amount."); }
            foreach (var c in d.cutscenes) { id(c.id, "Cutscene"); if (requireContent && c.content == null) errors.Add(c.id + ": unassigned Timeline/Playable content."); }
            foreach (var c in d.challenges) id(c.id, "Challenge");
            foreach (var r in d.races) id(r.id, "Race");
            foreach (var p in d.puzzles) id(p.id, "Puzzle");
            Action<IEnumerable<string>, string> rewards = (list, location) => {
                var seen = new HashSet<string>();
                foreach (string r in list) if (!seen.Add(r) || !d.rewards.Any(x => x.id == r)) errors.Add(location + ": duplicate/missing reward " + r);
            };
            foreach (var q in d.quests)
            {
                rewards(q.rewards, q.id);
                if (q.groups.Count == 0) errors.Add(q.id + ": no objective groups.");
                foreach (var pre in q.requiresQuests) if (!d.quests.Any(x => x.id == pre)) errors.Add(q.id + ": missing prerequisite quest " + pre);
                var groupIds = new HashSet<string>(); var objectiveIds = new HashSet<string>();
                foreach (var g in q.groups)
                {
                    if (string.IsNullOrWhiteSpace(g.id) || !groupIds.Add(g.id) || g.objectives.Count == 0) errors.Add(q.id + "/" + g.id + ": duplicate/empty group.");
                    foreach (var pre in g.requiresGroups) if (!q.groups.Any(x => x.id == pre)) errors.Add(q.id + "/" + g.id + ": missing prerequisite group " + pre);
                    foreach (var o in g.objectives)
                    {
                        string loc = q.id + "/" + g.id + "/" + o.id;
                        if (string.IsNullOrWhiteSpace(o.id) || !objectiveIds.Add(o.id) || o.count < 1 || o.maximumSeconds < 0) errors.Add(loc + ": invalid identity/count/time.");
                        if (o.evaluator != "fact" && (evaluators == null || !evaluators.Contains(o.evaluator))) errors.Add(loc + ": unsupported evaluator " + o.evaluator);
                        if ((o.kind == FactKind.RaceCompleted || o.kind == FactKind.PuzzleCompleted || o.kind == FactKind.TraversalCompleted) &&
                            !d.challenges.Any(c => c.id == o.definitionId && c.resultKind == o.kind)) errors.Add(loc + ": missing/mismatched challenge reference.");
                        if ((o.kind == FactKind.Interaction || o.kind == FactKind.LocationDiscovered || o.kind == FactKind.ItemCollected) && string.IsNullOrWhiteSpace(o.definitionId)) errors.Add(loc + ": stable interaction/location/item definition required.");
                    }
                }
                if (Cycle(q.groups.Select(x => x.id), x => q.groups.First(g => g.id == x).requiresGroups)) errors.Add(q.id + ": circular group prerequisites.");
                if (q.kind == QuestKind.Story)
                {
                    var character = d.relationships.Find(x => x.id == q.characterId);
                    if (character == null || !character.chapters.Any(c => c.id == q.chapterId && c.questId == q.id)) errors.Add(q.id + ": missing owning character/chapter mapping.");
                    if (q.automaticTurnIn) errors.Add(q.id + ": story requires explicit turn-in.");
                }
                else if (!string.IsNullOrEmpty(q.characterId) || !string.IsNullOrEmpty(q.chapterId)) errors.Add(q.id + ": optional quest cannot own relationship progression.");
            }
            if (Cycle(d.quests.Select(x => x.id), x => d.quests.First(q => q.id == x).requiresQuests)) errors.Add("Quests: circular prerequisites.");
            foreach (var r in d.relationships)
            {
                var chapterIds = new HashSet<string>();
                for (int i = 0; i < r.chapters.Count; i++)
                {
                    var c = r.chapters[i];
                    if (string.IsNullOrWhiteSpace(c.id) || !chapterIds.Add(c.id) || c.rank != i + 1 || c.rank > r.ranks.Count) errors.Add(r.id + "/" + c.id + ": chapter rank must advance once in sequence within authored ranks.");
                    if (!d.quests.Any(q => q.id == c.questId && q.kind == QuestKind.Story && q.characterId == r.id && q.chapterId == c.id)) errors.Add(r.id + "/" + c.id + ": quest ownership mismatch.");
                    if (!d.cutscenes.Any(x => x.id == c.cutsceneId)) errors.Add(r.id + "/" + c.id + ": missing required cutscene.");
                    if (d.relationships.SelectMany(x => x.chapters).Count(x => x.cutsceneId == c.cutsceneId) > 1) errors.Add(c.id + ": rank-up cutscene must have a unique completion record.");
                    rewards(c.rewards, r.id + "/" + c.id);
                }
            }
            foreach (var c in d.challenges)
            {
                rewards(c.firstClearRewards, c.id);
                if (c.resultKind != FactKind.RaceCompleted && c.resultKind != FactKind.PuzzleCompleted && c.resultKind != FactKind.TraversalCompleted) errors.Add(c.id + ": invalid challenge result kind.");
                foreach (string pre in c.requiresQuests) if (!d.quests.Any(q => q.id == pre)) errors.Add(c.id + ": missing prerequisite " + pre);
            }
            foreach (var r in d.races)
            {
                if (!d.challenges.Any(c => c.id == r.challengeId && (c.resultKind == FactKind.RaceCompleted || c.resultKind == FactKind.TraversalCompleted))) errors.Add(r.id + ": missing race/traversal challenge.");
                if (r.timeLimit < 0 || r.checkpoints.Count == 0 || r.checkpoints.Any(s => s.alternatives.Count == 0 || s.alternatives.Any(string.IsNullOrWhiteSpace))) errors.Add(r.id + ": invalid checkpoint/time configuration.");
                var checkpoints = r.checkpoints.SelectMany(s => s.alternatives).ToList();
                if (checkpoints.Distinct().Count() != checkpoints.Count) errors.Add(r.id + ": duplicate checkpoint identity.");
            }
            foreach (var p in d.puzzles)
            {
                if (!d.challenges.Any(c => c.id == p.challengeId && c.resultKind == FactKind.PuzzleCompleted)) errors.Add(p.id + ": missing puzzle challenge.");
                if (p.mechanisms.Count == 0 || p.mechanisms.Any(string.IsNullOrWhiteSpace) || p.mechanisms.Distinct().Count() != p.mechanisms.Count || p.timeWindow < 0) errors.Add(p.id + ": invalid mechanisms/time window.");
                if (p.persistPartial && (!p.permanent || p.timeWindow > 0)) errors.Add(p.id + ": partial persistence requires permanent, untimed puzzle.");
            }
            return errors;
        }
        private static bool Cycle(IEnumerable<string> nodes, Func<string, IEnumerable<string>> edges)
        {
            var set = new HashSet<string>(nodes); var visiting = new HashSet<string>(); var visited = new HashSet<string>();
            bool Visit(string node)
            {
                if (!set.Contains(node) || visited.Contains(node)) return false;
                if (!visiting.Add(node)) return true;
                foreach (string next in edges(node)) if (Visit(next)) return true;
                visiting.Remove(node); visited.Add(node); return false;
            }
            foreach (var node in set) if (Visit(node)) return true;
            return false;
        }
    }
}
