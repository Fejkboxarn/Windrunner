using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Windrunner.Production.Unity
{
    public interface IProjectSceneLoader
    {
        Coroutine LoadSingle(string sceneName, Action<bool> onComplete = null);
        Coroutine LoadAdditive(string sceneName, Action<bool> onComplete = null);
        Coroutine Unload(string sceneName, Action<bool> onComplete = null);
    }

    /// <summary>Scene operations are plain C# logic; the host only starts coroutines.</summary>
    public sealed class UnitySceneLoader : IProjectSceneLoader
    {
        private readonly MonoBehaviour coroutineHost;

        public UnitySceneLoader(MonoBehaviour coroutineHost)
        {
            this.coroutineHost = coroutineHost != null
                ? coroutineHost
                : throw new ArgumentNullException(nameof(coroutineHost));
        }

        public Coroutine LoadSingle(string sceneName, Action<bool> onComplete = null)
        {
            return coroutineHost.StartCoroutine(Load(sceneName, LoadSceneMode.Single, onComplete));
        }

        public Coroutine LoadAdditive(string sceneName, Action<bool> onComplete = null)
        {
            return coroutineHost.StartCoroutine(Load(sceneName, LoadSceneMode.Additive, onComplete));
        }

        public Coroutine Unload(string sceneName, Action<bool> onComplete = null)
        {
            return coroutineHost.StartCoroutine(UnloadRoutine(sceneName, onComplete));
        }

        private static IEnumerator Load(string sceneName, LoadSceneMode mode, Action<bool> onComplete)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[Windrunner Production] Cannot load an unnamed scene.");
                if (onComplete != null) onComplete(false);
                yield break;
            }

            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(sceneName, mode); }
            catch (Exception ex) { Debug.LogException(ex); }

            if (operation == null)
            {
                if (onComplete != null) onComplete(false);
                yield break;
            }

            while (!operation.isDone)
                yield return null;

            if (onComplete != null) onComplete(true);
        }

        private static IEnumerator UnloadRoutine(string sceneName, Action<bool> onComplete)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                if (onComplete != null) onComplete(false);
                yield break;
            }

            AsyncOperation operation = null;
            try { operation = SceneManager.UnloadSceneAsync(sceneName); }
            catch (Exception ex) { Debug.LogException(ex); }

            if (operation == null)
            {
                if (onComplete != null) onComplete(false);
                yield break;
            }

            while (!operation.isDone)
                yield return null;

            if (onComplete != null) onComplete(true);
        }
    }
}
