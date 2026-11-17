using System;

namespace Windrunner.Production.Progression
{
    /// <summary>One transient attempt. The caller supplies scaled game time.</summary>
    public sealed class TimedCarryAttempt
    {
        private double deadline, availableAt;
        public bool Carrying { get; private set; }
        public double Remaining(double now) => Carrying ? Math.Max(0, deadline-now) : 0;
        public bool Available(double now) => !Carrying && now >= availableAt;

        public bool Collect(double now, double duration)
        {
            if (!Available(now) || !(duration > 0) || double.IsInfinity(duration)) return false;
            deadline = now + duration;
            Carrying = true;
            return true;
        }
        public bool Expire(double now, double recovery)
        {
            if (!Carrying || now < deadline) return false;
            Carrying = false;
            availableAt = deadline + Math.Max(0,recovery);
            return true;
        }
        public void Delivered() => Carrying = false;
    }
}
