using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Windrunner.Production;
namespace Windrunner.Frontend
{
    public sealed class UIPageMotion : MonoBehaviour
    {
        CanvasGroup group;RectTransform content;float started,exitAlpha;Vector2 origin;bool exiting;
        AudioSource sound;
        public AudioSource Sound {get{if(!sound){sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;sound.ignoreListenerPause=true;}return sound;}}
        public void Bind(RectTransform rect){content=rect;origin=rect.anchoredPosition;group=rect.gameObject.AddComponent<CanvasGroup>();}
        public void Enter(){exiting=false;started=Time.unscaledTime;if(group){group.blocksRaycasts=group.interactable=true;group.alpha=WindrunnerUITheme.ReducedMotion?1:.55f;}}
        public void Exit(){exitAlpha=group.alpha;exiting=true;started=Time.unscaledTime;group.blocksRaycasts=group.interactable=false;if(WindrunnerUITheme.ReducedMotion)gameObject.SetActive(false);}
        void Update()
        {
            if(!group)return;float t=WindrunnerUITheme.ReducedMotion?1:Mathf.Clamp01((Time.unscaledTime-started)/Mathf.Max(.01f,WindrunnerUITheme.Current.enterSeconds));
            if(exiting){group.alpha=exitAlpha*(1-t);content.anchoredPosition=origin+Vector2.right*t*8;if(t>=1)gameObject.SetActive(false);return;}
            float ease=1-Mathf.Pow(1-t,3);group.alpha=Mathf.Lerp(.55f,1,ease);content.anchoredPosition=origin+Vector2.right*(1-ease)*12;
        }
    }
}
