using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Windrunner.Production.Unity;

namespace Windrunner.Production.Progression
{
    /// <summary>Engineering-fixture controls only. Reads the production service; never awards progress.</summary>
    public sealed class ProgressionFixtureGuide : MonoBehaviour
    {
        public const string ScenePath = "Assets/WindrunnerProduction/Progression/Fixtures/ProgressionFixture.unity";
        public const string Slot = "progression-engineering-fixture";
        public ProgressionActor actor;
        public ProgressionJournal journal;
        public PlayerInteractionController interaction;
        public RaceHost race;
        public PuzzleHost puzzle;
        public ProgressionCinematicPlayer cinematic;
        private bool expanded = true, diagnostics, confirmReset, busy;
        private string error, observedTarget, observedRejection;
        private float observedDistance, observedRange;
        private Vector2 scroll;
        private Vector2 controlsScroll;
        private ProgressionState state;
        private ProgressionService service;
        private void Start() { service = actor?.Service; if (service != null) { service.Changed += Refresh; Refresh(); } }
        private void Refresh() => state = service?.Snapshot();
        private void OnDestroy() { if (service != null) service.Changed -= Refresh; }
        private void Update()
        {
            if (actor?.Input?.IsMenuOpen == true)
            {
                if (actor.Input.ProgressionPressed("JournalBack")) actor.host.InputOwner.SetPaused(false);
                return;
            }
            if (actor?.Input?.ProgressionPressed("FixtureGuide") == true) expanded = !expanded;
            if (actor?.Input?.ProgressionPressed("FixtureTools") == true) { if (controls) CloseControls(); else { journal.SetOpen(false); OpenControls(); } }
            if (controls && actor?.Input?.ProgressionPressed("JournalBack") == true) CloseControls();
        }
        public bool CanReset => IsFixtureIdentity(gameObject.scene.path, actor?.host?.saveSlot, actor?.Service?.PlayerId) &&
            ProjectRoot.Active != null && ProjectRoot.Active.Runtime != null && ProjectRoot.Active.Runtime.Services.TryGet<ProgressionService>(out var current) && ReferenceEquals(current, service);
        public static bool IsFixtureIdentity(string scene, string slot, string player) => scene == ScenePath && slot == Slot && player == "engineering-fixture";
        public static void ResetIsolatedSave(string directory, string scene, string slot, string player)
        {
            if (!IsFixtureIdentity(scene, slot, player)) throw new InvalidOperationException("Only the engineering fixture may reset its isolated save.");
            new FileProgressionStore(Path.Combine(directory, Slot + ".json")).Commit(new ProgressionState { playerId = "engineering-fixture" });
        }
        public void ReloadSaved() { if (!busy && CanReset) StartCoroutine(Reload(false)); }
        public void ResetConfirmed() { if (!busy && CanReset) StartCoroutine(Reload(true)); }
        private IEnumerator Reload(bool reset)
        {
            busy = true; CloseControls(); journal.SetOpen(false); cinematic.enabled = false;
            if (reset)
            {
                try { ResetIsolatedSave(Application.persistentDataPath, gameObject.scene.path, actor.host.saveSlot, actor.Service.PlayerId); }
                catch (Exception ex) { error = ex.Message; busy = false; cinematic.enabled = true; yield break; }
            }
            // Reconstruct the same service through its existing composition root, rather than mutate a second state database.
            Destroy(ProjectRoot.Active.gameObject); yield return null;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("ProgressionFixture", LoadSceneMode.Single);
#endif
        }
        private void OnGUI()
        {
            if (state == null || actor?.Input == null) return;
            var old = ProgressionUiTheme.BeginCanvas();
            try
            {
                if (actor.Input.IsMenuOpen) { DrawPause(); return; }
                // In-journal controls are a separate footer, never over the quest details.
                if (journal.IsOpen)
                {
                    GUILayout.BeginArea(new Rect(48, 648, 1184, 42)); GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Fixture controls / guide", ProgressionUiTheme.Button)) { journal.SetOpen(false); OpenControls(); }
                    GUILayout.EndHorizontal(); GUILayout.EndArea(); return;
                }
                GUILayout.BeginArea(new Rect(24, 24, 420, expanded ? 520 : 70), ProgressionUiTheme.Card);
                if (GUILayout.Button("ENGINEERING FIELD GUIDE  " + (expanded ? "−" : "+") + "  " + journal.Binding("FixtureGuide"), ProgressionUiTheme.Button)) expanded = !expanded;
                if (expanded)
                {
                    scroll = GUILayout.BeginScrollView(scroll);
                    GUILayout.Label("Aim the + reticle, move within range, then " + journal.Binding("Interact") + ". Colored signs mark every station.", ProgressionUiTheme.Body);
                    Step("1", "Accept both quests at the blue and violet offer stations.", "fixture-a-quest", "fixture-b-quest");
                    GUILayout.Label("2  " + journal.Binding("Journal") + " journal · " + journal.Binding("JournalNext") + " select · " + journal.Binding("TrackQuest") + " track. Mouse buttons and wheel work in the journal.", ProgressionUiTheme.Body);
                    GUILayout.Label("3  Activate the gold World Switch. Both active chapters observe it.", ProgressionUiTheme.Body);
                    GUILayout.Label("4  Use Race Start, then move through the cyan CHECKPOINT and green FINISH arches.", ProgressionUiTheme.Body);
                    GUILayout.Label("Race: " + (race.Session == null ? "Not started" : race.Session.Status + " · " + race.Session.Elapsed.ToString("0.0") + " s · checkpoint " + race.Session.NextCheckpoint), ProgressionUiTheme.Muted);
                    GUILayout.Label("5  Return to Turn-in A/B beyond the finish. Each character advances independently.", ProgressionUiTheme.Body);
                    foreach (var r in state.relationships) GUILayout.Label(r.id + " · Rank " + r.rank, ProgressionUiTheme.Muted);
                    GUILayout.Label("6  Stop on the floor. Journal cinematic buttons play/replay; " + journal.Binding("Skip") + " skips. Open the fixture controls to interrupt a replay.", ProgressionUiTheme.Body);
                    GUILayout.Label("7  Puzzle switches 1 → 2. Incorrect order resets the attempt. A solved permanent puzzle survives reload; a fresh fixture reset lets you test again.", ProgressionUiTheme.Body);
                    GUILayout.Label("Puzzle: " + (puzzle.Session?.Status.ToString() ?? "Initializing"), ProgressionUiTheme.Muted);
                    GUILayout.Label("8  Reload saved progress, or explicitly reset only the engineering slot. The previous save becomes a rolling .bak (replaced by later writes).", ProgressionUiTheme.Body);
                    GUILayout.Label(journal.Binding("FixtureTools") + " opens fixture controls / diagnostics. Controls stay paused while open.", ProgressionUiTheme.Muted);
                    if (GUILayout.Button("Controls & diagnostics", ProgressionUiTheme.Button)) OpenControls();
                    GUILayout.EndScrollView();
                }
                GUILayout.EndArea();
                if (controls) DrawControls();
            }
            finally { GUI.matrix = old; }
        }
        private bool controls;
        private Vector2 pauseScroll;
        private string bindingStatus;
        private void DrawPause()
        {
            GUILayout.BeginArea(new Rect(240, 32, 800, 656), ProgressionUiTheme.Card);
            GUILayout.Label("PAUSED · INPUT & BINDINGS", ProgressionUiTheme.Title);
            GUILayout.Label("The existing pause controller owns this pause. Closing it leaves any open journal paused.", ProgressionUiTheme.Body);
            if (GUILayout.Button("Resume · " + journal.Binding("JournalBack"), ProgressionUiTheme.Button)) actor.host.InputOwner.SetPaused(false);
            if (actor.Input.IsRebinding)
            {
                GUILayout.Label("Press the new control. " + bindingStatus, ProgressionUiTheme.Heading);
                if (GUILayout.Button("Cancel rebind", ProgressionUiTheme.Button)) actor.Input.CancelRebind();
            }
            pauseScroll = GUILayout.BeginScrollView(pauseScroll);
            var map = actor.Input.Actions.FindActionMap("Progression");
            foreach (var action in map.actions)
            {
                GUILayout.Label(action.name, ProgressionUiTheme.Heading);
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    int index = i; string name = action.name;
                    if (GUILayout.Button(UnityEngine.InputSystem.InputActionRebindingExtensions.GetBindingDisplayString(action, i), ProgressionUiTheme.Button) && !actor.Input.IsRebinding)
                        actor.Input.BeginRebind("Progression", name, index, success => bindingStatus = success ? "Binding saved." : "Rebind canceled.");
                }
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private IDisposable inputLease, timeLease, cursorLease;
        public void OpenControls()
        {
            if (controls || actor.Input.IsMenuOpen) return;
            observedTarget = interaction.Candidate != null ? interaction.Candidate.name : "none";
            observedRange = interaction.Candidate != null ? interaction.Candidate.range : 0; observedDistance = interaction.TargetDistance; observedRejection = interaction.Rejection;
            controls = true; inputLease = actor.Input.BlockGameplay(); timeLease = UnityTimeAuthority.Pause(); cursorLease = UnityCursorAuthority.Menu();
        }
        public void CloseControls()
        { controls = false; cursorLease?.Dispose(); timeLease?.Dispose(); inputLease?.Dispose(); cursorLease = timeLease = inputLease = null; }
        private void OnDisable() => CloseControls();
        private void DrawControls()
        {
            GUILayout.BeginArea(new Rect(464, 24, 792, 520), ProgressionUiTheme.Card);
            controlsScroll = GUILayout.BeginScrollView(controlsScroll);
            GUILayout.Label("FIXTURE TOOLS · not player progression UI", ProgressionUiTheme.Heading);
            if (GUILayout.Button("Resume exploration", ProgressionUiTheme.Button)) CloseControls();
            if (GUILayout.Button("Interrupt cinematic (retain pending state)", ProgressionUiTheme.Button)) { cinematic.enabled = false; cinematic.enabled = true; }
            if (GUILayout.Button("Reload saved fixture", ProgressionUiTheme.Button)) { CloseControls(); ReloadSaved(); }
            if (GUILayout.Button("Reset Fixture Progress…", ProgressionUiTheme.Button)) confirmReset = true;
            if (confirmReset)
            {
                GUILayout.Label("Only progression-engineering-fixture.json will be replaced with a fresh state. Its previous contents become a rolling .bak, replaced by later writes. Normal saves are never targeted.", ProgressionUiTheme.Body);
                if (GUILayout.Button("Confirm fresh engineering progress", ProgressionUiTheme.Button)) { CloseControls(); ResetConfirmed(); }
                if (GUILayout.Button("Cancel reset", ProgressionUiTheme.Button)) confirmReset = false;
            }
            diagnostics = GUILayout.Toggle(diagnostics, "Interaction diagnostics (read-only)", ProgressionUiTheme.Button);
            if (diagnostics)
            {
                GUILayout.Label("Target: " + observedTarget + "\nInteract: " + journal.Binding("Interact") + "\nLast distance: " + observedDistance.ToString("0.00") + " / " + observedRange.ToString("0.00") + " m range\nLast unpaused probe: " + (string.IsNullOrEmpty(observedRejection) ? "Available" : observedRejection), ProgressionUiTheme.Body);
                GUILayout.Label("Quest A: " + service.Status("fixture-a-quest") + " · Quest B: " + service.Status("fixture-b-quest"), ProgressionUiTheme.Muted);
            }
            if (!string.IsNullOrEmpty(error)) GUILayout.Label(error, ProgressionUiTheme.Body);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        private void Step(string number, string text, string a, string b)
        { GUILayout.Label(number + "  " + text, ProgressionUiTheme.Body); GUILayout.Label(service.Status(a) + " / " + service.Status(b), ProgressionUiTheme.Muted); }
    }
}
