using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Windrunner.Production.Unity;

namespace Windrunner.Production.Progression
{
    /// <summary>Journal projection of the authoritative service, with scoped input, time and cursor ownership.</summary>
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
        private string hud = "", notification = "";
        private float notificationUntil;
        private int selected, lastCompleted, lastRewards;
        private IDisposable inputLease, pauseLease, cursorLease;
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
            var previous = snapshot;
            snapshot = service.Snapshot(); questIds.Clear();
            foreach (var q in catalog.definitions.quests)
                if (service.Status(q.id) != QuestStatus.Locked) questIds.Add(q.id);
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
            if (previouslyBound && completed > lastCompleted)
                foreach (var r in catalog.definitions.relationships)
                {
                    int advances = snapshot.relationships.Find(x => x.id == r.id)?.rank ?? 0;
                    if (advances > (previous.relationships.Find(x => x.id == r.id)?.rank ?? 0))
                    { notification = RankLabel(r, advances) + " · milestone unlocked"; break; }
                }
            lastCompleted = completed; lastRewards = snapshot.rewards.Count;
        }
        public bool SetOpen(bool open)
        {
            if (open == IsOpen) return true;
            if (open)
            {
                if (actor?.Input == null || actor.Input.IsGameplayBlocked || service == null) return false;
                inputLease = actor.Input.BlockGameplay(); pauseLease = UnityTimeAuthority.Pause(); cursorLease = UnityCursorAuthority.Menu();
            }
            else { pauseLease?.Dispose(); inputLease?.Dispose(); cursorLease?.Dispose(); pauseLease = null; inputLease = null; cursorLease = null; }
            return true;
        }
        private void Update()
        {
            if (service == null) Bind();
            var input = actor?.Input;
            if (input == null || input.IsMenuOpen) return;
            if (input.ProgressionPressed("Journal")) SetOpen(!IsOpen);
            if (!IsOpen) return;
            if (input.ProgressionPressed("JournalBack")) { SetOpen(false); return; }
            if (input.ProgressionPressed("JournalCategoryNext")) SetCategory((category + 1) % 3);
            if (input.ProgressionPressed("JournalCategoryPrevious")) SetCategory((category + 2) % 3);
            if (input.ProgressionPressed("JournalNext")) SelectNext(1);
            if (input.ProgressionPressed("JournalPrevious")) SelectNext(-1);
            if (input.ProgressionPressed("JournalScrollDown")) { detailScroll.y += 180; contextScroll.y += 180; }
            if (input.ProgressionPressed("JournalScrollUp")) { detailScroll.y = Mathf.Max(0, detailScroll.y - 180); contextScroll.y = Mathf.Max(0, contextScroll.y - 180); }
            if (input.ProgressionPressed("TrackQuest")) TrackSelected();
            if (cinematics != null && input.ProgressionPressed("ViewCutscene"))
            {
                var pending = snapshot.cutscenes.Find(c => c.status == CinematicStatus.Pending);
                if (pending == null) pending = snapshot.cutscenes.Count > 0 ? snapshot.cutscenes[0] : null;
                if (pending == null) return;
                SetOpen(false);
                var definition = catalog.definitions.cutscenes.Find(c => c.id == pending.id);
                if (definition?.content == null) cinematics.AcknowledgeMissingContent(pending.id); else cinematics.Play(pending.id, pending.status != CinematicStatus.Pending);
            }
        }
        private Vector2 navigationScroll, detailScroll, hudScroll, contextScroll;
        private int category, selectedChallenge;
        private bool revealSelection;
        public string SelectedQuestId => questIds.Count == 0 || (category == 1 && catalog.definitions.quests.Find(q => q.id == questIds[selected]).kind != QuestKind.Story) ? null : questIds[selected];
        private void SetCategory(int value)
        {
            if (category == value) return;
            category = value; navigationScroll = detailScroll = Vector2.zero;
            if (category == 1 && SelectedQuestId == null)
            {
                int first = questIds.FindIndex(id => catalog.definitions.quests.Find(q => q.id == id).kind == QuestKind.Story);
                if (first >= 0) selected = first;
            }
        }
        public void SelectNext(int direction)
        {
            detailScroll = Vector2.zero;
            if (category == 2)
            {
                var challenges = snapshot.challenges.FindAll(c => c.discovered);
                if (challenges.Count > 0) selectedChallenge = (selectedChallenge + direction + challenges.Count) % challenges.Count;
                navigationScroll.y = selectedChallenge * 70; return;
            }
            if (questIds.Count == 0) return;
            var visible = new List<int>();
            foreach (QuestStatus status in new[] { QuestStatus.Available, QuestStatus.Active, QuestStatus.ReadyToTurnIn, QuestStatus.Completed })
                for (int i = 0; i < questIds.Count; i++)
                    if (service.Status(questIds[i]) == status && (category != 1 || catalog.definitions.quests.Find(q => q.id == questIds[i]).kind == QuestKind.Story)) visible.Add(i);
            if (visible.Count == 0) return;
            selected = visible[(visible.IndexOf(selected) + direction + visible.Count) % visible.Count];
            revealSelection = true;
        }
        public bool TrackSelected() => category != 2 && SelectedQuestId != null && service.Track(snapshot.trackedQuestId == SelectedQuestId ? null : SelectedQuestId);
        public string Binding(string name) => actor?.Input?.ProgressionBindingLabel(name) ?? "";
        private void OnGUI()
        {
            if (service == null || actor?.Input == null || actor.Input.IsMenuOpen) return;
            if (cinematics && cinematics.IsPlaying)
            {
                GUI.Box(new Rect(Screen.width/2-240,Screen.height-60,480,36),(string.IsNullOrEmpty(notification) ? "Rank milestone" : notification)+"   ·   "+Binding("Skip")+" to skip");
                return;
            }
            if (!IsOpen && actor.Input.IsGameplayBlocked) return;
            var old = ProgressionUiTheme.BeginCanvas();
            try { if (IsOpen) DrawJournal(); else DrawHud(); }
            finally { GUI.matrix = old; }
        }
        private void DrawJournal()
        {
            var previousColor=GUI.color;GUI.color=WindrunnerUITheme.Current.background;GUI.DrawTexture(new Rect(0,0,1280,720),Texture2D.whiteTexture);GUI.color=previousColor;
            GUILayout.BeginArea(new Rect(32, 24, 1216, 660), ProgressionUiTheme.Card);
            GUILayout.BeginHorizontal(); GUILayout.Label("QUEST JOURNAL", ProgressionUiTheme.Title);
            if (GUILayout.Button("Close  ·  " + Binding("JournalBack"), ProgressionUiTheme.Button, GUILayout.Width(250))) SetOpen(false);
            GUILayout.EndHorizontal();
            GUILayout.Label(Binding("JournalPrevious") + " / " + Binding("JournalNext") + " select   •   " + Binding("JournalCategoryNext") + " category   •   " + Binding("TrackQuest") + " track   •   " + Binding("JournalScrollUp") + " / " + Binding("JournalScrollDown") + " scroll", ProgressionUiTheme.Muted);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(300));
            SetCategory(GUILayout.Toolbar(category, new[] { "All quests", "Stories", "Challenges" }, ProgressionUiTheme.Button));
            navigationScroll = GUILayout.BeginScrollView(navigationScroll);
            if (category != 2)
            {
                foreach (QuestStatus status in new[] { QuestStatus.Available, QuestStatus.Active, QuestStatus.ReadyToTurnIn, QuestStatus.Completed })
                {
                    bool heading = false;
                    for (int i = 0; i < questIds.Count; i++)
                    {
                        var q = catalog.definitions.quests.Find(x => x.id == questIds[i]);
                        if (service.Status(q.id) != status || (category == 1 && q.kind != QuestKind.Story)) continue;
                        if (!heading) { GUILayout.Label(StatusLabel(status), ProgressionUiTheme.Heading); heading = true; }
                        var color = GUI.backgroundColor; if (i == selected) GUI.backgroundColor = ProgressionUiTheme.Accent;
                        if (GUILayout.Button((i == selected ? "▶  " : "") + q.displayName + (snapshot.trackedQuestId == q.id ? "\nTRACKED" : ""), ProgressionUiTheme.Button)) { selected = i; detailScroll = Vector2.zero; }
                        GUI.backgroundColor = color;
                        if (revealSelection && i == selected && Event.current.type == EventType.Repaint)
                        { navigationScroll.y = Mathf.Max(0, GUILayoutUtility.GetLastRect().y - 120); revealSelection = false; }
                    }
                }
            }
            else
            {
                var challenges = snapshot.challenges.FindAll(c => c.discovered);
                for (int i = 0; i < challenges.Count; i++)
                {
                    var d = catalog.definitions.challenges.Find(x => x.id == challenges[i].id); if (d == null) continue;
                    if (GUILayout.Button((i == selectedChallenge ? "▶  " : "") + d.displayName, ProgressionUiTheme.Button)) { selectedChallenge = i; detailScroll = Vector2.zero; }
                }
            }
            GUILayout.EndScrollView(); GUILayout.EndVertical(); GUILayout.Space(24);
            GUILayout.BeginVertical(GUILayout.Width(520));
            detailScroll = GUILayout.BeginScrollView(detailScroll);
            var selectedQuest = catalog.definitions.quests.Find(q => q.id == SelectedQuestId);
            if (category == 2)
            {
                var challenges = snapshot.challenges.FindAll(c => c.discovered);
                if (challenges.Count == 0) GUILayout.Label("Explore to discover world challenges.", ProgressionUiTheme.Body);
                else
                {
                    selectedChallenge = Mathf.Clamp(selectedChallenge, 0, challenges.Count - 1);
                    var c = challenges[selectedChallenge]; var d = catalog.definitions.challenges.Find(x => x.id == c.id);
                    GUILayout.Label("WORLD CHALLENGE", ProgressionUiTheme.Muted);
                    GUILayout.Label(d?.displayName ?? c.id, ProgressionUiTheme.Title);
                    GUILayout.Label(c.completed ? "Completed · best " + c.personalBest.ToString("0.00") + " s" : service.ChallengeAvailable(c.id) ? "Available · visit the start station" : "Discovered · prerequisites required", ProgressionUiTheme.Heading);
                }
            }
            else if (selectedQuest == null) GUILayout.Label("Explore the labeled quest offer stations to begin.", ProgressionUiTheme.Heading);
            else
            {
                var status = service.Status(selectedQuest.id);
                var owner = catalog.definitions.relationships.Find(r => r.id == selectedQuest.characterId);
                GUILayout.Label(owner != null ? "CHARACTER STORY · " + owner.displayName : "OPTIONAL WORLD QUEST", ProgressionUiTheme.Muted);
                GUILayout.Label(selectedQuest.displayName, ProgressionUiTheme.Title);
                GUILayout.Label(StatusLabel(status) + (snapshot.trackedQuestId == selectedQuest.id ? "   •   TRACKED" : "   •   Not tracked"), ProgressionUiTheme.Heading);
                GUILayout.Label(selectedQuest.description, ProgressionUiTheme.Body); GUILayout.Space(16);
                GUILayout.Label("OBJECTIVES", ProgressionUiTheme.Heading);
                var record = snapshot.quests.Find(r => r.id == selectedQuest.id);
                foreach (var group in selectedQuest.groups) foreach (var o in group.objectives)
                {
                    int count = record?.objectives.Find(r => r.id == o.id)?.count ?? 0;
                    GUILayout.Label((count >= o.count ? "✓  " : "○  ") + o.description + "   " + count + " / " + o.count, ProgressionUiTheme.Body);
                    var rect = GUILayoutUtility.GetRect(1, 6, GUILayout.ExpandWidth(true)); var color = GUI.color;
                    GUI.color = new Color(0.18f, 0.25f, 0.31f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = ProgressionUiTheme.Accent; GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01((float)count / o.count), rect.height), Texture2D.whiteTexture); GUI.color = color;
                    GUILayout.Space(10);
                }
                if (!string.IsNullOrEmpty(selectedQuest.guidance.clue)) GUILayout.Label("Guidance: " + selectedQuest.guidance.clue, ProgressionUiTheme.Body);
                GUILayout.Space(16); GUILayout.Label("NEXT ACTION", ProgressionUiTheme.Heading);
                GUILayout.Label(status == QuestStatus.Available ? "Find the quest giver or world interaction that offers this quest." : status == QuestStatus.ReadyToTurnIn ? "Visit the matching Quest Turn-in station. Completion and rank advancement happen there." : status == QuestStatus.Completed ? "Chapter complete. Check the cinematic section below." : "Complete the remaining objectives in any allowed order.", ProgressionUiTheme.Body);
                using (new GUIEnabledScope(status == QuestStatus.Active || status == QuestStatus.ReadyToTurnIn))
                    if (GUILayout.Button(snapshot.trackedQuestId == selectedQuest.id ? "Stop tracking" : "Track this quest  ·  " + Binding("TrackQuest"), ProgressionUiTheme.Button))
                        service.Track(snapshot.trackedQuestId == selectedQuest.id ? null : selectedQuest.id);
            }
            GUILayout.EndScrollView();GUILayout.EndVertical();GUILayout.Space(24);
            GUILayout.BeginVertical();contextScroll=GUILayout.BeginScrollView(contextScroll);
            GUILayout.Label("YOUR CONNECTIONS", ProgressionUiTheme.Heading);
            foreach (var r in catalog.definitions.relationships)
                GUILayout.Label(RankLabel(r,snapshot.relationships.Find(x => x.id == r.id)?.rank ?? 0),ProgressionUiTheme.Body);
            if (cinematics != null)
                foreach (var c in snapshot.cutscenes)
                {
                    var d = catalog.definitions.cutscenes.Find(x => x.id == c.id); if (d == null) continue;
                    if (GUILayout.Button(d.displayName + " · " + c.status + " · " + (c.status == CinematicStatus.Pending ? "Play" : "Replay"), ProgressionUiTheme.Button))
                    { SetOpen(false); if (d.content == null) cinematics.AcknowledgeMissingContent(c.id); else cinematics.Play(c.id, c.status != CinematicStatus.Pending); }
                }
            GUILayout.EndScrollView(); GUILayout.EndVertical(); GUILayout.EndHorizontal(); GUILayout.EndArea();
        }
        private static string RankLabel(RelationshipDefinition relationship,int advances)
        {
            int initial = string.IsNullOrEmpty(relationship.initialRankName) ? 0 : 1;
            string label = advances > 0 && advances <= relationship.ranks.Count ? relationship.ranks[advances-1] : relationship.initialRankName;
            return $"{relationship.displayName}  ·  Rank {initial+advances} / {initial+relationship.ranks.Count}  {label}";
        }
        public static string StatusLabel(QuestStatus value) => value == QuestStatus.ReadyToTurnIn ? "Ready to turn in" : value.ToString();
        private void DrawHud()
        {
            if (!string.IsNullOrEmpty(hud))
            {
                GUILayout.BeginArea(new Rect(940, 32, 308, 210), ProgressionUiTheme.Card);
                GUILayout.Label("TRACKED QUEST", ProgressionUiTheme.Heading);
                hudScroll = GUILayout.BeginScrollView(hudScroll); GUILayout.Label(hud, ProgressionUiTheme.Body); GUILayout.EndScrollView();
                GUILayout.Label(Binding("Journal") + " · full objectives", ProgressionUiTheme.Muted); GUILayout.EndArea();
            }
            if(interaction?.Target==null && interaction?.Candidate==null && Time.unscaledTime>=notificationUntil)return;
            GUILayout.BeginArea(new Rect(360, 520, 560, 104), ProgressionUiTheme.Card);
            GUILayout.Label(interaction != null && interaction.Target != null ? interaction.Prompt : interaction?.Candidate != null ? interaction.Candidate.prompt + " · " + interaction.Rejection : "Aim at a nearby character or object • " + Binding("Journal") + " opens your journal", ProgressionUiTheme.Body);
            if (Time.unscaledTime < notificationUntil) GUILayout.Label(notification, ProgressionUiTheme.Muted);
            GUILayout.EndArea();
        }
        private sealed class GUIEnabledScope : IDisposable
        {
            private readonly bool previous;
            public GUIEnabledScope(bool enabled) { previous = GUI.enabled; GUI.enabled &= enabled; }
            public void Dispose() { GUI.enabled = previous; }
        }
        private void OnDisable() { SetOpen(false); if (service != null) service.Changed -= Refresh; }
    }
}
