using UnityEngine;
using Windrunner.Production.Input;

namespace Windrunner.Production.Progression
{
    public sealed class ProgressionActor : MonoBehaviour
    {
        public ProgressionHost host;
        public WindrunnerInputHost inputHost;
        public Transform interactionOrigin;
        private ProgressionService injected;
        private WindrunnerInputService injectedInput;
        public ProgressionService Service => injected ?? host?.Service;
        public WindrunnerInputService Input => injectedInput ?? inputHost?.Input;
        public Vector3 Position => interactionOrigin != null ? interactionOrigin.position : transform.position;
        public void Bind(ProgressionService service, WindrunnerInputService input) { injected = service; injectedInput = input; }
    }
}
