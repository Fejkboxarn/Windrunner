using System;
using System.IO;
using UnityEngine;
using Windrunner.Production.Unity;
using Windrunner.Production.Feedback;

namespace Windrunner.Production.Progression
{
    [DefaultExecutionOrder(-8000)]
    public sealed class ProgressionHost : MonoBehaviour
    {
        public ProjectRoot project;
        public ProgressionCatalog catalog;
        public string playerId = "local-player";
        public string saveSlot = "progression";
        public ProgressionService Service { get; private set; }
        private IFeedbackService feedback;
        private int completedCount, rewardCount;
        private void Awake()
        {
            if (project == null || catalog == null) { Debug.LogError("ProgressionHost requires explicit ProjectRoot and catalog.", this); enabled = false; return; }
            var root = project.IsDuplicate ? ProjectRoot.Active : project;
            try
            {
                if (!root.Runtime.Services.TryGet<ProgressionService>(out var service))
                {
                    if (string.IsNullOrWhiteSpace(saveSlot) || saveSlot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || saveSlot.Contains("..")) throw new ArgumentException("Invalid progression save slot.");
                    service = new ProgressionService(catalog.definitions, playerId,
                        new FileProgressionStore(Path.Combine(Application.persistentDataPath, saveSlot + ".json")));
                    root.Runtime.Services.Register(service);
                }
                if (service.PlayerId != playerId) throw new InvalidOperationException("Progression host/player mismatch.");
                Service = service; root.Runtime.Services.TryGet(out feedback);
                ObserveCounts(); Service.Changed += OnChanged; Service.ListenerFailed += LogFailure;
            }
            catch (Exception ex) { Debug.LogException(ex, this); enabled = false; }
        }
        private void ObserveCounts()
        {
            var snapshot = Service.Snapshot();
            completedCount = snapshot.quests.FindAll(q => q.status == QuestStatus.Completed).Count; rewardCount = snapshot.rewards.Count;
        }
        private void OnChanged()
        {
            int beforeQuests = completedCount, beforeRewards = rewardCount; ObserveCounts();
            if (completedCount > beforeQuests) feedback?.Publish(FeedbackKey.QuestCompleted, FeedbackRequest.At(transform));
            if (rewardCount > beforeRewards) feedback?.Publish(FeedbackKey.ProgressionRewardGranted, FeedbackRequest.At(transform));
        }
        private void LogFailure(Exception ex) => Debug.LogException(ex, this);
        private void OnDestroy()
        {
            if (Service == null) return;
            Service.Changed -= OnChanged; Service.ListenerFailed -= LogFailure; Service = null;
        }
    }
}
