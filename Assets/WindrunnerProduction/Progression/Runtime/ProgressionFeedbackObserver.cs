using System;
using UnityEngine;
using Windrunner.Production.Feedback;

namespace Windrunner.Production.Progression
{
    /// <summary>One registry-owned semantic publisher, independent of scene adapters.</summary>
    public sealed class ProgressionFeedbackObserver : IDisposable
    {
        private readonly ProgressionService service;
        private readonly IFeedbackService feedback;
        private readonly Transform source;
        private int completed, rewards;
        public ProgressionFeedbackObserver(ProgressionService service, IFeedbackService feedback, Transform source)
        {
            this.service = service; this.feedback = feedback; this.source = source;
            Counts(); service.Changed += Changed;
        }
        private void Counts()
        {
            var snapshot = service.Snapshot();
            completed = snapshot.quests.FindAll(q => q.status == QuestStatus.Completed).Count; rewards = snapshot.rewards.Count;
        }
        private void Changed()
        {
            int oldCompleted = completed, oldRewards = rewards; Counts();
            try { if (completed > oldCompleted) feedback.Publish(FeedbackKey.QuestCompleted, FeedbackRequest.At(source)); }
            finally { if (rewards > oldRewards) feedback.Publish(FeedbackKey.ProgressionRewardGranted, FeedbackRequest.At(source)); }
        }
        public void Dispose() => service.Changed -= Changed;
    }
}
