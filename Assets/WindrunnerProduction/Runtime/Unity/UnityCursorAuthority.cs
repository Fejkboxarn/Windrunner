using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windrunner.Production.Unity
{
    /// <summary>Nested menu cursor ownership; closing either overlay never restores another overlay's stale snapshot.</summary>
    public static class UnityCursorAuthority
    {
        private static readonly HashSet<Request> requests = new HashSet<Request>();
        private static CursorLockMode originalLock;
        private static bool originalVisible;
        public static IDisposable Menu()
        {
            if (requests.Count == 0) { originalLock = Cursor.lockState; originalVisible = Cursor.visible; }
            var request = new Request(); requests.Add(request);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return request;
        }
        private sealed class Request : IDisposable
        {
            public void Dispose()
            {
                if (!requests.Remove(this) || requests.Count != 0) return;
                Cursor.lockState = originalLock; Cursor.visible = originalVisible;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { requests.Clear(); }
    }
}
