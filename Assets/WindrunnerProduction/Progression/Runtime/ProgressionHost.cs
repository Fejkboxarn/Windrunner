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
        public Windrunner.Production.Input.WindrunnerInputService Input { get; private set; }
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
                if (service.PlayerId != playerId || !service.MatchesDefinitions(catalog.definitions)) throw new InvalidOperationException("Progression host/player/catalog mismatch.");
                Service = service;
                if (!root.Runtime.Services.TryGet<ProgressionFeedbackObserver>(out var observer))
                {
                    root.Runtime.Services.TryGet<IFeedbackService>(out var feedback);
                    if (feedback != null) root.Runtime.Services.Register(new ProgressionFeedbackObserver(service, feedback, root.transform));
                }
                root.Runtime.Services.TryGet<Windrunner.Production.Input.WindrunnerInputService>(out var input);
                Input = input;
                Service.ListenerFailed += LogFailure;
            }
            catch (Exception ex) { Debug.LogException(ex, this); enabled = false; }
        }
        private void LogFailure(Exception ex) => Debug.LogException(ex, this);
        private void OnDestroy()
        {
            if (Service == null) return;
            Service.ListenerFailed -= LogFailure; Service = null;
        }
    }
}
