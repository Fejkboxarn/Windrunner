using System;

namespace Windrunner.Production.Core
{
    /// <summary>Lifetime owner for project-level services and state.</summary>
    public sealed class ProjectRuntime : IDisposable
    {
        private bool disposed;

        public ServiceRegistry Services { get; private set; }
        public GameStateMachine StateMachine { get; private set; }

        public ProjectRuntime()
        {
            Services = new ServiceRegistry();
            StateMachine = new GameStateMachine();
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (!disposed)
                StateMachine.Tick(unscaledDeltaSeconds);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            try { StateMachine.Dispose(); }
            finally { Services.Dispose(); }
        }
    }
}
