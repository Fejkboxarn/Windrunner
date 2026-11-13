using UnityEngine;

namespace Windrunner.Production.Progression
{
    public sealed class FixtureWorldLabel : MonoBehaviour
    {
        public Camera view;
        private MeshRenderer labelRenderer;
        private void Awake() => labelRenderer = GetComponent<MeshRenderer>();
        private void LateUpdate()
        {
            if (view == null) return;
            transform.rotation = view.transform.rotation;
            // Nearby signs remain legible without distant stations stacking over the same sightline.
            if (labelRenderer != null) labelRenderer.enabled = view.enabled && (transform.position - view.transform.position).sqrMagnitude < 196;
        }
    }
}
