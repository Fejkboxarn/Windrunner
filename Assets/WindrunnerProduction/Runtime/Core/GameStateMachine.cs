using System;

namespace Windrunner.Production.Core
{
    public interface IProjectState
    {
        void Enter();
        void Tick(float deltaSeconds);
        void Exit();
    }

    /// <summary>Pure lifecycle; never references scenes, game rules or Unity objects.</summary>
    public sealed class GameStateMachine : IDisposable
    {
        private IProjectState current;
        private bool transitioning;
        private bool disposed;

        public IProjectState Current { get { return current; } }

        public void SetState(IProjectState next)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GameStateMachine));
            if (next == null)
                throw new ArgumentNullException(nameof(next));
            if (ReferenceEquals(current, next))
                return;
            if (transitioning)
                throw new InvalidOperationException("Reentrant state transition. Request a transition after Enter/Exit returns.");

            transitioning = true;
            try
            {
                IProjectState previous = current;
                current = null;
                if (previous != null)
                    previous.Exit();

                current = next;
                next.Enter();
            }
            catch
            {
                current = null;
                throw;
            }
            finally
            {
                transitioning = false;
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (disposed || transitioning)
                return;

            if (current != null)
                current.Tick(Math.Max(0f, deltaSeconds));
        }

        public void Clear()
        {
            if (transitioning)
                throw new InvalidOperationException("Cannot clear during a state transition.");

            IProjectState previous = current;
            current = null;
            if (previous != null)
                previous.Exit();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            try { Clear(); }
            finally { disposed = true; }
        }
    }
}
