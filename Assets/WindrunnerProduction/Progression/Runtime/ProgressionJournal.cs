using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Windrunner.Production.Unity;

namespace Windrunner.Production.Progression
{
    /// <summary>Keyboard/gamepad journal. Uses the shared action bindings, without taking cursor ownership.</summary>
    public sealed class ProgressionJournal : MonoBehaviour
    {
        public ProgressionActor actor;
        public ProgressionCatalog catalog;
        public PlayerInteractionController interaction;
        public ProgressionCinematicPlayer cinematics;
        public ProgressionAreaMarker[] areas = Array.Empty<ProgressionAreaMarker>();
        private ProgressionService service;
        private ProgressionState snapshot;
        private readonly List<string> questIds = new List<string>();
        private string journalText = "", hud = "", notification = "";
        private float notificationUntil;
        private int selected, lastCompleted, lastRewards;
        private IDisposable inputLease, pauseLease;
        public bool IsOpen => inputLease != null;
        private void Start() => Bind();
        private void OnEnable() { if (service != null) { service.Changed += Refresh; Refresh(); } }
        private void Bind()
        {
            if (service != null || actor?.Service == null || catalog == null) return;
            service = actor.Service; service.Changed += Refresh; Refresh();
        }
        private void Refresh()
        {
            if (service == null) return;
            bool previouslyBound = snapshot != null;
            snapshot = service.Snapshot(); questIds.Clear(); var text = new StringBuilder();
            foreach (var q in catalog.definitions.quests)
            {
                var status = service.Status(q.id);
                if (status == QuestStatus.Locked) continue;
                questIds.Add(q.id);
                text.AppendLine(q.displayName + " [" + q.kind + "] — " + status);
                text.AppendLine(q.description);
                var record = snapshot.quests.Find(x => x.id == q.id);
                foreach (var group in q.groups) foreach (var objective in group.objectives)
                    text.AppendLine("  " + objective.description + ": " + (record?.objectives.Find(x => x.id == objective.id)?.count ?? 0) + "/" + objective.count);
                if (!string.IsNullOrEmpty(q.guidance.clue)) text.AppendLine("  Clue: " + q.guidance.clue);
            }
            text.AppendLine("World challenges");
            foreach (var c in snapshot.challenges)
            {
                var definition = catalog.definitions.challenges.Find(x => x.id == c.id);
                if (definition == null || !c.discovered) continue;
                text.AppendLine(definition.displayName + " — " + (c.completed ? "Completed (best " + c.personalBest.ToString("0.00") + "s)" : service.ChallengeAvailable(c.id) ? "Available" : "Discovered / locked"));
            }
            journalText = text.ToString();
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, questIds.Count - 1));
            var tracked = catalog.definitions.quests.Find(x => x.id == snapshot.trackedQuestId);
            hud = "";
            if (tracked != null)
            {
                var record = snapshot.quests.Find(x => x.id == tracked.id); var compact = new StringBuilder(tracked.displayName);
                foreach (var group in tracked.groups) foreach (var o in group.objectives)
                    compact.Append("\n" + o.description + ": " + (record?.objectives.Find(x => x.id == o.id)?.count ?? 0) + "/" + o.count);
                if (tracked.guidance.showArea) compact.Append("\nArea: " + tracked.guidance.areaId);
                hud = compact.ToString();
            }
            int completed = snapshot.quests.FindAll(q => q.status == QuestStatus.Completed).Count;
            if (previouslyBound && (completed > lastCompleted || snapshot.rewards.Count > lastRewards))
            { notification = "Progression updated — quest complete / reward unlocked"; notificationUntil = Time.unscaledTime + 4; }
            lastCompleted = completed; lastRewards = snapshot.rewards.Count;
        }
        public bool SetOpen(bool open)
        {
            if (open == IsOpen) return true;
            if (open)
            {
                if (actor?.Input == null || actor.Input.IsGameplayBlocked || service == null) return false;
                inputLease = actor.Input.BlockGameplay(); pauseLease = UnityTimeAuthority.Pause();
            }
            else { pauseLease?.Dispose(); inputLease?.Dispose(); pauseLease = null; inputLease = null; }
            return true;
        }
        private void Update()
        {
            if (service == null) Bind();
            var input = actor?.Input;
            if (input == null || input.IsMenuOpen) return;
            if (input.ProgressionPressed("Journal")) SetOpen(!IsOpen);
            if (!IsOpen) return;
            if (questIds.Count > 0 && input.ProgressionPressed("JournalNext")) selected = (selected + 1) % questIds.Count;
            if (questIds.Count > 0 && input.ProgressionPressed("TrackQuest")) service.Track(questIds[selected]);
            if (cinematics != null && input.ProgressionPressed("ViewCutscene"))
            {
                var pending = snapshot.cutscenes.Find(c => c.status == CinematicStatus.Pending);
                if (pending == null) return;
                SetOpen(false);
                var definition = catalog.definitions.cutscenes.Find(c => c.id == pending.id);
                if (definition?.content == null) cinematics.AcknowledgeMissingContent(pending.id); else cinematics.Play(pending.id);
            }
        }
        private Vector2 scroll;
        private void OnGUI()
        {
            if (service == null || actor?.Input == null || actor.Input.IsMenuOpen) return;
            GUILayout.BeginArea(new Rect(16, 16, Mathf.Min(650, Screen.width - 32), IsOpen ? Screen.height - 32 : 180), GUI.skin.box);
            if (IsOpen)
            {
                GUILayout.Label("Quest journal — " + actor.Input.ProgressionBindingLabel("Journal") + " close");
                GUILayout.Label(actor.Input.ProgressionBindingLabel("JournalNext") + " select, " + actor.Input.ProgressionBindingLabel("TrackQuest") + " track");
                if (questIds.Count > 0) GUILayout.Label("Selected: " + catalog.definitions.quests.Find(q => q.id == questIds[selected]).displayName);
                scroll = GUILayout.BeginScrollView(scroll); GUILayout.Label(journalText); GUILayout.EndScrollView();
                if (snapshot.cutscenes.Exists(c => c.status == CinematicStatus.Pending))
                {
                    var pending = snapshot.cutscenes.Find(c => c.status == CinematicStatus.Pending);
                    bool missing = catalog.definitions.cutscenes.Find(c => c.id == pending.id)?.content == null;
                    GUILayout.Label(actor.Input.ProgressionBindingLabel("ViewCutscene") + (missing ? " explicitly skip missing authored cinematic (if allowed)" : " play pending cinematic when safely idle"));
                }
            }
            else
            {
                GUILayout.Label(hud);
                if (interaction != null) GUILayout.Label(interaction.Prompt);
                if (Time.unscaledTime < notificationUntil) GUILayout.Label(notification);
                var tracked = catalog.definitions.quests.Find(q => q.id == snapshot.trackedQuestId);
                if (tracked != null && tracked.guidance.showArea)
                    foreach (var area in areas) if (area != null && area.areaId == tracked.guidance.areaId)
                        GUILayout.Label("General area: " + area.displayName + " (" + Mathf.Max(0, Vector3.Distance(actor.Position, area.transform.position) - area.radius).ToString("0") + "m)");
            }
            GUILayout.EndArea();
        }
        private void OnDisable() { SetOpen(false); if (service != null) service.Changed -= Refresh; }
    }
}
