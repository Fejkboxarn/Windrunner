using System;
using UnityEngine;
using UnityEngine.Playables;
using Windrunner.Production.Unity;

namespace Windrunner.Production.Progression
{
    public sealed class ProgressionCinematicPlayer : MonoBehaviour, ICinematicControl
    {
        public ProgressionActor actor;
        public ProgressionCatalog catalog;
        public PlayableDirector director;
        public Camera gameplayCamera, cinematicCamera;
        public MonoBehaviour safety;
        private CinematicSession session;
        private CutsceneDefinition playing;
        private bool pausedByMenu;
        public bool IsPlaying => session?.Current != null;
        public bool IsPlaceholderPlaying => isActiveAndEnabled && IsPlaying && playing != null && playing.isPlaceholder;
        private void OnGUI() => PlaceholderCinematicWarning.Draw(IsPlaceholderPlaying);
        public bool CanStart => actor?.Service != null && actor.Input != null && !actor.Input.IsGameplayBlocked &&
            !UnityTimeAuthority.IsPaused && safety is IProgressionControlSafety guard && guard.CanStartCinematic;
        public bool Play(string id, bool replay = false)
        {
            if (!isActiveAndEnabled || IsPlaying || catalog == null || director == null || gameplayCamera == null || cinematicCamera == null || gameplayCamera == cinematicCamera) return false;
            var definition = catalog.definitions.cutscenes.Find(x => x.id == id);
            if (definition == null || definition.content == null) return false;
            // Portable animation retains the player graph. Timeline may animate authored NPCs/cameras only.
            foreach (var output in definition.content.outputs)
            {
                var binding = director.GetGenericBinding(output.sourceObject);
                var component = binding as Component;
                var obj = binding as GameObject;
                Transform target = component != null ? component.transform : obj != null ? obj.transform : null;
                if (target != null && (target == actor.transform || target.IsChildOf(actor.transform))) return false;
            }
            if (session == null) session = new CinematicSession(actor.Service, this);
            if (!session.Start(id, replay)) return false;
            try
            {
                playing = definition; director.playableAsset = definition.content; director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
                director.extrapolationMode = DirectorWrapMode.Hold; director.stopped += Stopped; director.Play(); return true;
            }
            catch { StopAndRecover(); throw; }
        }
        public bool Skip()
        {
            if (!IsPlaying || playing == null || !playing.allowSkip) return false;
            try { if (!session.Finish(true)) return false; }
            catch (Exception ex) { StopAndRecover(); Debug.LogException(ex, this); return false; }
            director.stopped -= Stopped; director.Stop(); playing = null; return true;
        }
        public bool AcknowledgeMissingContent(string id)
        {
            // Explicit authoring fallback. Never silently resolve a missing Timeline on load/start.
            var definition = catalog?.definitions.cutscenes.Find(x => x.id == id);
            if (definition == null || definition.content != null || !definition.allowSkip || !CanStart) return false;
            if (session == null) session = new CinematicSession(actor.Service, this);
            return session.Start(id) && session.Finish(true);
        }
        private void Stopped(PlayableDirector stopped)
        {
            director.stopped -= Stopped;
            // An external early stop is interruption, not a watched record.
            try
            {
                if (IsPlaying && stopped.duration > 0 && stopped.time >= stopped.duration) session.Finish(false);
                else session?.Interrupt();
            }
            catch (Exception ex) { Debug.LogException(ex, this); }
            finally { playing = null; }
        }
        private void Update()
        {
            if (!IsPlaying) return;
            if (actor.Input.IsMenuOpen)
            { if (!pausedByMenu) { director.Pause(); pausedByMenu = true; } return; }
            if (pausedByMenu) { director.Resume(); pausedByMenu = false; }
            if (director.duration > 0 && !double.IsInfinity(director.duration) && director.time >= director.duration)
            {
                try { if (session.Finish(false)) { director.stopped -= Stopped; director.Stop(); playing = null; } }
                catch (Exception ex) { StopAndRecover(); Debug.LogException(ex, this); }
                return;
            }
            if (actor.Input.ProgressionPressed("Skip")) Skip();
        }
        private void StopAndRecover()
        {
            if (director != null) { director.stopped -= Stopped; director.Stop(); }
            try { session?.Interrupt(); }
            catch (Exception ex) { Debug.LogException(ex, this); }
            finally { playing = null; pausedByMenu = false; }
        }
        private void OnDisable() => StopAndRecover();
        public IDisposable Acquire() => new ControlLease(actor, gameplayCamera, cinematicCamera);
        private sealed class ControlLease : IDisposable
        {
            private IDisposable input, time;
            private readonly Camera gameplay, cinematic;
            private readonly bool gameplayEnabled, cinematicEnabled;
            public ControlLease(ProgressionActor actor, Camera gameplay, Camera cinematic)
            {
                this.gameplay = gameplay; this.cinematic = cinematic;
                gameplayEnabled = gameplay != null && gameplay.enabled; cinematicEnabled = cinematic != null && cinematic.enabled;
                input = actor.Input.BlockGameplay(); time = UnityTimeAuthority.Pause();
                if (gameplay != null) gameplay.enabled = false;
                if (cinematic != null) cinematic.enabled = true;
            }
            public void Dispose()
            {
                if (input == null) return;
                if (gameplay != null) gameplay.enabled = gameplayEnabled;
                if (cinematic != null) cinematic.enabled = cinematicEnabled;
                time.Dispose(); input.Dispose(); time = null; input = null;
            }
        }
    }
}
