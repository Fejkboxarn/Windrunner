using UnityEngine;
using Windrunner.Production.Core;

namespace Windrunner.Production.Unity
{
    /// <summary>
    /// Unity lifecycle bridge. Gameplay and business logic belong in regular
    /// classes registered with ProjectRuntime, not in this component.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class ProjectRoot : MonoBehaviour
    {
        [SerializeField] private bool persistAcrossScenes = true;

        // Diagnostics and composition root only. Gameplay should receive explicit
        // service references, not reach through this static property every frame.
        public static ProjectRoot Active { get; private set; }
        public ProjectRuntime Runtime { get; private set; }

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Debug.LogError("[Windrunner Production] Duplicate ProjectRoot detected; duplicate disabled.", this);
                enabled = false;
                return;
            }

            Active = this;
            Runtime = new ProjectRuntime();

            var loader = new UnitySceneLoader(this);
            Runtime.Services.Register<IProjectSceneLoader>(loader);

            if (persistAcrossScenes)
                DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Runtime != null)
                Runtime.Tick(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            if (Active != this)
                return;

            try
            {
                if (Runtime != null)
                    Runtime.Dispose();
            }
            finally
            {
                Runtime = null;
                Active = null;
            }
        }
    }
}
