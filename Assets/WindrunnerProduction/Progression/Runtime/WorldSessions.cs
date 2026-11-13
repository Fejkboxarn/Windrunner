using System;
using System.Collections.Generic;
using System.Linq;

namespace Windrunner.Production.Progression
{
    /// <summary>Caller supplies gameplay-scaled time, including the existing pause/hit-stop policy.</summary>
    public sealed class RaceSession
    {
        private readonly RaceDefinition definition;
        private readonly ProgressionService progression;
        private string attempt;
        public AttemptStatus Status { get; private set; } = AttemptStatus.Ready;
        public int NextCheckpoint { get; private set; }
        public float Elapsed { get; private set; }
        public RaceSession(RaceDefinition definition, ProgressionService progression)
        {
            this.definition = new RaceDefinition { id = definition.id, challengeId = definition.challengeId, timeLimit = definition.timeLimit,
                checkpoints = definition.checkpoints.Select(s => new RaceStep { alternatives = new List<string>(s.alternatives) }).ToList() };
            this.progression = progression;
        }
        public bool Start()
        {
            if (Status == AttemptStatus.Active || !progression.ChallengeAvailable(definition.challengeId)) return false;
            attempt = Guid.NewGuid().ToString("N"); NextCheckpoint = 0; Elapsed = 0; Status = AttemptStatus.Active; return true;
        }
        public void Tick(float gameplayDelta)
        {
            if (Status != AttemptStatus.Active) return;
            if (gameplayDelta < 0 || float.IsNaN(gameplayDelta) || float.IsInfinity(gameplayDelta)) throw new ArgumentOutOfRangeException(nameof(gameplayDelta));
            Elapsed += gameplayDelta;
            if (definition.timeLimit > 0 && Elapsed > definition.timeLimit) Status = AttemptStatus.Failed;
        }
        public bool Reach(string checkpoint, string playerId, string region = null)
        {
            if (Status != AttemptStatus.Active || playerId != progression.PlayerId || NextCheckpoint >= definition.checkpoints.Count ||
                !definition.checkpoints[NextCheckpoint].alternatives.Contains(checkpoint)) return false;
            if (NextCheckpoint + 1 == definition.checkpoints.Count)
            {
                if (!progression.CompleteChallenge(definition.challengeId, attempt, Elapsed, region)) return false;
                Status = AttemptStatus.Succeeded;
            }
            NextCheckpoint++; return true;
        }
        public void Cancel() { if (Status == AttemptStatus.Active) Status = AttemptStatus.Canceled; }
        public void Fail() { if (Status == AttemptStatus.Active) Status = AttemptStatus.Failed; }
    }
    public sealed class PuzzleSession
    {
        private readonly PuzzleDefinition definition;
        private readonly ProgressionService progression;
        private readonly List<string> activated = new List<string>();
        private string attempt;
        private float elapsed;
        private bool retryBlocked;
        public PuzzleStatus Status { get; private set; }
        public IReadOnlyList<string> Activated => activated.AsReadOnly();
        public event Action Changed;
        public PuzzleSession(PuzzleDefinition definition, ProgressionService progression)
        {
            this.definition = new PuzzleDefinition { id = definition.id, challengeId = definition.challengeId, mechanisms = new List<string>(definition.mechanisms),
                ordered = definition.ordered, permanent = definition.permanent, persistPartial = definition.persistPartial,
                resetOnExit = definition.resetOnExit, retry = definition.retry, timeWindow = definition.timeWindow, destinationId = definition.destinationId };
            this.progression = progression;
            var record = progression.Snapshot().puzzles.Find(x => x.id == definition.id);
            if (record != null && definition.permanent && record.solved)
            { activated.AddRange(definition.mechanisms); Status = PuzzleStatus.Solved; }
            else
            {
                if (record != null && definition.persistPartial) activated.AddRange(record.mechanisms.Where(definition.mechanisms.Contains));
                Status = PuzzleStatus.Dormant;
            }
        }
        public bool Begin()
        {
            if (retryBlocked || Status == PuzzleStatus.Solved || Status == PuzzleStatus.Active || !progression.ChallengeAvailable(definition.challengeId)) return false;
            Status = activated.Count > 0 ? PuzzleStatus.Active : PuzzleStatus.Ready; attempt = Guid.NewGuid().ToString("N"); elapsed = 0; Changed?.Invoke(); return true;
        }
        public bool Activate(string mechanism, string playerId, string region = null)
        {
            if ((Status != PuzzleStatus.Ready && Status != PuzzleStatus.Active) || playerId != progression.PlayerId ||
                !definition.mechanisms.Contains(mechanism) || activated.Contains(mechanism)) return false;
            if (definition.ordered && definition.mechanisms[activated.Count] != mechanism) { Reset(false); return false; }
            var next = new List<string>(activated) { mechanism };
            bool complete = next.Count == definition.mechanisms.Count && string.IsNullOrEmpty(definition.destinationId);
            if (complete && !progression.CompleteChallenge(definition.challengeId, attempt, elapsed, region, definition)) return false;
            if (!complete && definition.persistPartial) progression.SavePuzzle(definition.id, false, next);
            activated.Add(mechanism); Status = complete ? PuzzleStatus.Solved : PuzzleStatus.Active; Changed?.Invoke(); return true;
        }
        public bool ReachDestination(string id, string playerId, string region = null)
        {
            if (Status != PuzzleStatus.Active || playerId != progression.PlayerId || string.IsNullOrEmpty(definition.destinationId) ||
                id != definition.destinationId || activated.Count != definition.mechanisms.Count) return false;
            if (!progression.CompleteChallenge(definition.challengeId, attempt, elapsed, region, definition)) return false;
            Status = PuzzleStatus.Solved; Changed?.Invoke(); return true;
        }
        public void Tick(float gameplayDelta)
        {
            if (Status != PuzzleStatus.Active) return;
            if (gameplayDelta < 0 || float.IsNaN(gameplayDelta) || float.IsInfinity(gameplayDelta)) throw new ArgumentOutOfRangeException(nameof(gameplayDelta));
            elapsed += gameplayDelta; if (definition.timeWindow > 0 && elapsed > definition.timeWindow) Reset(false);
        }
        public void Leave() { if (definition.resetOnExit && !(Status == PuzzleStatus.Solved && definition.permanent)) Reset(true); }
        public void Reset(bool leaving)
        {
            if (Status == PuzzleStatus.Solved && definition.permanent) return;
            if (definition.persistPartial) progression.SavePuzzle(definition.id, false, Array.Empty<string>());
            Status = PuzzleStatus.Resetting; activated.Clear(); elapsed = 0; Changed?.Invoke();
            Status = leaving || !definition.retry ? PuzzleStatus.Dormant : PuzzleStatus.Ready;
            retryBlocked = !leaving && !definition.retry;
            attempt = Guid.NewGuid().ToString("N"); Changed?.Invoke();
        }
    }
}
