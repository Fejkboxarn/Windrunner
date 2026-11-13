using System;

namespace Windrunner.Production.Progression
{
    public interface ICinematicControl { bool CanStart { get; } IDisposable Acquire(); }
    public sealed class CinematicSession : IDisposable
    {
        private readonly ProgressionService progression;
        private readonly ICinematicControl control;
        private IDisposable lease;
        private bool replay;
        public string Current { get; private set; }
        public CinematicSession(ProgressionService progression, ICinematicControl control) { this.progression = progression; this.control = control; }
        public bool Start(string id, bool isReplay = false)
        {
            if (Current != null || !control.CanStart) return false;
            var acquired = control.Acquire();
            try
            {
                if (!progression.StartCutscene(id, isReplay)) { acquired.Dispose(); return false; }
                lease = acquired; Current = id; replay = isReplay; return true;
            }
            catch { acquired.Dispose(); throw; }
        }
        public bool Finish(bool skipped)
        {
            if (Current == null) return false;
            if (!replay && !progression.ResolveCutscene(Current, skipped)) return false;
            Release(); return true;
        }
        public void Interrupt()
        {
            if (Current == null) return;
            try { if (!replay) progression.InterruptCutscene(Current); }
            finally { Release(); }
        }
        private void Release() { Current = null; var request = lease; lease = null; request?.Dispose(); }
        public void Dispose() => Interrupt();
    }
    public interface IProgressionControlSafety { bool CanStartCinematic { get; } }
}
