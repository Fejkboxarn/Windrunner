using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Windrunner.Production;
namespace Windrunner.Frontend
{
    public sealed class UIFocusFeedback : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        Button button;RectTransform accent;Text label;bool selected,hover;float amount;
        public void Bind(Button value,RectTransform mark,Text text){button=value;accent=mark;label=text;}
        public void OnSelect(BaseEventData e){selected=true;Sound(WindrunnerUITheme.Current.focusSound);}
        public void OnDeselect(BaseEventData e){selected=false;}
        public void OnPointerEnter(PointerEventData e){hover=true;if(button&&button.interactable)EventSystem.current?.SetSelectedGameObject(gameObject);}
        public void OnPointerExit(PointerEventData e){hover=false;}
        void Sound(AudioClip clip){if(!clip)return;var source=GetComponentInParent<UIPageMotion>()?.Sound;if(source)source.PlayOneShot(clip,.3f);}
        void Update()
        {
            if(!button||!accent)return;var theme=WindrunnerUITheme.Current;
            float target=button.interactable&&(selected||hover)?1:0;
            amount=WindrunnerUITheme.ReducedMotion?target:Mathf.MoveTowards(amount,target,Time.unscaledDeltaTime/Mathf.Max(.01f,theme.focusSeconds));
            accent.localScale=new Vector3(1,Mathf.Lerp(.15f,1,amount),1);
            accent.GetComponent<Image>().color=Color.Lerp(new Color(theme.accent.r,theme.accent.g,theme.accent.b,0),theme.accent,amount);
            label.rectTransform.offsetMin=new Vector2(22+6*amount,2);
            label.color=button.interactable?Color.Lerp(theme.ink,Color.white,amount):theme.muted;
        }
    }
}
