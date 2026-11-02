using System;
using System.Collections.Generic;
using System.Text;

namespace AirflowPrototype.Editor
{
    internal enum SlingTraceEvent
    {
        Started,
        Committed,
        InitialBoost,
        DashStarted,
        Impact,
        Launched,
        PerfectLaunched,
        Canceled
    }

    /// <summary>
    /// Editor-only event order checker. Plain C#; never drives gameplay or changes
    /// a state, Unity object or event. Cancellation while idle is intentionally
    /// treated as an orphan note because CancelSling/OnDisable emit it in legacy code.
    /// </summary>
    internal sealed class WindrunnerSlingEventTraceSession
    {
        private sealed class Attempt
        {
            public string Target;
            public bool Committed;
            public bool Perfect;
            public bool InitialBoost;
            public bool DashStarted;
            public bool Impact;
            public bool Launched;
        }

        private readonly List<string> _events = new List<string>(128);
        private readonly List<string> _violations = new List<string>();
        private Attempt _active;

        public int StartedCount { get; private set; }
        public int NormalCompleted { get; private set; }
        public int PerfectCompleted { get; private set; }
        public int CanceledCount { get; private set; }
        public int OrphanCancelCount { get; private set; }
        public int ViolationCount => _violations.Count;
        public bool HasActiveAttempt => _active != null;

        public void Observe(SlingTraceEvent kind, int frame, float time,
            string target = null, float quality = 0f, bool perfect = false)
        {
            string line = "frame=" + frame + " t=" + time.ToString("F3") +
                          " " + kind + " target=" + (target ?? "-") +
                          (kind == SlingTraceEvent.Committed || kind == SlingTraceEvent.Launched
                              ? " perfect=" + perfect + " quality=" + quality.ToString("F3")
                              : "");
            if (_events.Count >= 512)
                _events.RemoveAt(0);
            _events.Add(line);

            switch (kind)
            {
                case SlingTraceEvent.Started:
                    if (_active != null)
                        Violate("Started while previous attempt was unfinished", line);
                    _active = new Attempt { Target = target };
                    StartedCount++;
                    break;

                case SlingTraceEvent.Committed:
                    if (!RequireActive("Committed", line)) break;
                    if (_active.Committed) Violate("Duplicate commit", line);
                    _active.Committed = true;
                    _active.Perfect = perfect;
                    if (perfect != (quality > 0f))
                        Violate("Commit perfect flag/quality mismatch", line);
                    CheckTarget(target, line);
                    break;

                case SlingTraceEvent.InitialBoost:
                    if (!RequireActive("InitialBoost", line)) break;
                    if (!_active.Committed || !_active.Perfect)
                        Violate("InitialBoost without perfect commit", line);
                    if (_active.InitialBoost)
                        Violate("Duplicate InitialBoost", line);
                    _active.InitialBoost = true;
                    CheckTarget(target, line);
                    break;

                case SlingTraceEvent.DashStarted:
                    if (!RequireActive("DashStarted", line)) break;
                    if (!_active.Committed)
                        Violate("Dash started before commit", line);
                    if (_active.Perfect && !_active.InitialBoost)
                        Violate("Perfect dash started before initial boost", line);
                    if (_active.DashStarted)
                        Violate("Duplicate dash start", line);
                    _active.DashStarted = true;
                    CheckTarget(target, line);
                    break;

                case SlingTraceEvent.Impact:
                    if (!RequireActive("Impact", line)) break;
                    if (!_active.DashStarted)
                        Violate("Impact before dash start", line);
                    if (_active.Impact)
                        Violate("Duplicate impact", line);
                    _active.Impact = true;
                    CheckTarget(target, line);
                    break;

                case SlingTraceEvent.Launched:
                    if (!RequireActive("Launched", line)) break;
                    if (!_active.Impact)
                        Violate("Final launch before impact", line);
                    if (_active.Launched)
                        Violate("Duplicate final launch", line);
                    if (perfect != _active.Perfect)
                        Violate("Final launch perfect flag differs from commit", line);
                    _active.Launched = true;
                    CheckTarget(target, line);
                    if (!_active.Perfect)
                    {
                        NormalCompleted++;
                        _active = null;
                    }
                    break;

                case SlingTraceEvent.PerfectLaunched:
                    if (!RequireActive("PerfectLaunched", line)) break;
                    if (!_active.Perfect || !_active.Launched)
                        Violate("PerfectLaunched before a perfect final launch", line);
                    else
                        PerfectCompleted++;
                    CheckTarget(target, line);
                    _active = null;
                    break;

                case SlingTraceEvent.Canceled:
                    if (_active == null)
                    {
                        // Current legacy controller can emit this during OnDisable or idle.
                        OrphanCancelCount++;
                    }
                    else
                    {
                        CanceledCount++;
                        _active = null;
                    }
                    break;
            }
        }

        public string Report(int lastEvents = 30)
        {
            var result = new StringBuilder();
            result.Append("[Windrunner 5H] ");
            if (ViolationCount > 0)
                result.Append("FAIL");
            else if (NormalCompleted + PerfectCompleted > 0 && _active == null)
                result.Append("PASS");
            else
                result.Append("INCOMPLETE");

            result.Append(" — started=").Append(StartedCount)
                .Append(", normal completed=").Append(NormalCompleted)
                .Append(", perfect completed=").Append(PerfectCompleted)
                .Append(", canceled=").Append(CanceledCount)
                .Append(", idle/orphan cancel=").Append(OrphanCancelCount)
                .Append(", violations=").Append(ViolationCount)
                .Append(", active=").Append(_active != null);

            if (_violations.Count > 0)
            {
                result.Append("\nVIOLATIONS:");
                foreach (string violation in _violations)
                    result.Append("\n  ").Append(violation);
            }

            int first = Math.Max(0, _events.Count - Math.Max(0, lastEvents));
            result.Append("\nLast ").Append(_events.Count - first).Append(" events:");
            for (int i = first; i < _events.Count; i++)
                result.Append("\n  ").Append(_events[i]);
            return result.ToString();
        }

        private bool RequireActive(string kind, string line)
        {
            if (_active != null) return true;
            Violate(kind + " without an active started attempt", line);
            return false;
        }

        private void CheckTarget(string target, string line)
        {
            if (_active != null && _active.Target != null && target != null &&
                !string.Equals(_active.Target, target, StringComparison.Ordinal))
                Violate("Target changed during attempt", line);
        }

        private void Violate(string issue, string line)
        {
            if (_violations.Count < 100)
                _violations.Add(issue + " :: " + line);
        }
    }
}
