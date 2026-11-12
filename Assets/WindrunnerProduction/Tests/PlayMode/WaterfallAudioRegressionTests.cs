using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Windrunner.Production.Unity;
namespace Windrunner.Production.Tests
{
    public sealed class WaterfallAudioRegressionTests
    {
        private const string ScenePath = "Assets/AirflowPrototype/Generated/Batch1_Playground.unity";
        private static AudioSource[] Waterfalls() => Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(s => s.name.Contains("Waterfall") || (s.clip != null && s.clip.name.Contains("Waterfall"))).ToArray();
        private static void Verify()
        {
            var all = Waterfalls();
            Assert.That(all.Length, Is.EqualTo(5), "One ambience owner and four silent decorative splash adapters.");
            var audible = all.Where(s => s.enabled && s.clip != null && !s.mute).ToArray();
            Assert.That(audible.Length, Is.EqualTo(1), "Reference repairs must not activate redundant waterfall loops.");
            var source = audible.Single();
            Assert.That(source.name, Is.EqualTo("Waterfall"));
            Assert.That(source.clip.name, Is.EqualTo("Water_Waterfall_Big"));
            Assert.That(source.spatialBlend, Is.EqualTo(1f));
            Assert.That(source.volume, Is.EqualTo(.004f).Within(.000001f));
            Assert.That(source.minDistance, Is.EqualTo(42.479553f).Within(.001f));
            Assert.That(source.maxDistance, Is.EqualTo(125f));
            Assert.That(source.rolloffMode, Is.EqualTo(AudioRolloffMode.Custom));
            Assert.That(source.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(1f), Is.EqualTo(.037506104f).Within(.00001f));
            Assert.That(source.outputAudioMixerGroup.name, Is.EqualTo("Waterfall"));
            Assert.That(source.loop && source.playOnAwake, Is.True);
            Assert.That(Vector3.Distance(source.transform.position,new Vector3(48.7f,90.354713f,74.95f)), Is.LessThan(.01f));
            Assert.That(source.gameObject.scene.name, Is.EqualTo("Batch1_Playground"));
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l=>l.enabled), Is.EqualTo(1));
            foreach (var splash in all.Where(s=>s!=source)) Assert.That(splash.clip, Is.Null, "Decorative emitters were silent in the working baseline.");
        }
        [UnityTest] public IEnumerator WaterfallRetainsLocalAmbienceWithoutDuplicateLoopsAcrossReload()
        {
            try
            {
                yield return SceneManager.LoadSceneAsync(ScenePath); yield return null;
                Verify(); var oldSources = Waterfalls();
                yield return SceneManager.LoadSceneAsync(ScenePath); yield return null;
                Verify();
                Assert.That(oldSources.All(s=>s==null), Is.True, "No waterfall source may survive in a persistent service or pool.");
            }
            finally { if (ProjectRoot.Active != null) Object.Destroy(ProjectRoot.Active.gameObject); }
            var cleanup=SceneManager.CreateScene("Waterfall cleanup"); SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync("Batch1_Playground");
        }
    }
}
