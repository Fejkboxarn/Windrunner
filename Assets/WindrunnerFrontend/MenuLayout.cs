using System;
using System.Collections.Generic;
using Windrunner.Production.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Windrunner.Production;
using UnityEngine.InputSystem;

namespace Windrunner.Frontend
{
    internal sealed class MenuLayout
    {
        private readonly Font font=WindrunnerUITheme.Current.Font;
        private readonly WindrunnerUITheme theme=WindrunnerUITheme.Current;
        private readonly UIPageMotion motion;
        private readonly Text prompts,eyebrow;
        private readonly RectTransform surface;
        private readonly CanvasScaler scaler;
        private string page;
        private readonly Color ink=WindrunnerUITheme.Current.ink, muted=WindrunnerUITheme.Current.muted;
        internal readonly GameObject Root;
        internal bool IsOpen {get;private set;}
        internal void Hide(){if(!IsOpen)return;IsOpen=false;EventSystem.current?.SetSelectedGameObject(null);motion.Exit();}
        private readonly Text heading,message;
        private readonly GameObject brand,brandRule;
        private readonly ScrollRect scroll;
        private readonly RectTransform rows;
        private Selectable first;
        private readonly Dictionary<string,int> focusMemory=new Dictionary<string,int>();
        private bool? promptPad;
        private bool promptOverflow;
        private int focusIndex=-1,choiceIndex;
        private GameObject lastSelection;
        private static RectTransform Rect(string name,Transform parent)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);return rect;
        }
        private static void Bounds(RectTransform r,float x,float y,float right,float top)
        {
            r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(right,top);r.offsetMin=r.offsetMax=Vector2.zero;
        }
        private Text Label(Transform parent,string content,int size,Color color)
        {
            var label=Rect(content,parent).gameObject.AddComponent<Text>();label.font=font;label.text=content;
            label.fontSize=size;label.color=color;label.raycastTarget=false;label.supportRichText=false;
            label.alignment=TextAnchor.MiddleLeft;return label;
        }
        internal MenuLayout(Transform parent)
        {
            Root=Rect("Session menu",parent).gameObject;
            var canvas=Root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=150;
            scaler=Root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=1;
            Root.AddComponent<GraphicRaycaster>();
            var background=Rect("Backdrop",Root.transform);Bounds(background,0,0,1,1);surface=background;
            background.gameObject.AddComponent<Image>().color=theme.background;
            var atmosphere=Rect("Wind and slate",background);Bounds(atmosphere,.32f,0,1,1);atmosphere.gameObject.AddComponent<WindMenuBackdrop>().raycastTarget=false;
            motion=Root.AddComponent<UIPageMotion>();motion.Bind(background);
            eyebrow=Label(background,"E C H O E S   O N   T H E   W I N D",16,muted);Bounds(eyebrow.rectTransform,.075f,.66f,.60f,.71f);
            prompts=Label(background,"",16,muted);Bounds(prompts.rectTransform,.075f,.025f,.94f,.065f);
            var rule=Rect("Accent",background);brandRule=rule.gameObject;Bounds(rule,.075f,.54f,.14f,.545f);
            rule.gameObject.AddComponent<Image>().color=theme.accent;
            var title=Label(background,"WINDRUNNER",theme.brandSize,ink);brand=title.gameObject;title.fontStyle=FontStyle.Bold;Bounds(title.rectTransform,.075f,.55f,.49f,.72f);
            heading=Label(background,"",theme.headingSize,ink);Bounds(heading.rectTransform,.50f,.79f,.93f,.92f);
            message=Label(background,"",theme.bodySize,muted);Bounds(message.rectTransform,.075f,.25f,.41f,.52f);
            var view=Rect("Choices",background);Bounds(view,.50f,.10f,.93f,.78f);
            scroll=view.gameObject.AddComponent<ScrollRect>();view.gameObject.AddComponent<RectMask2D>();
            var hit=view.gameObject.AddComponent<Image>();hit.color=Color.clear;
            rows=Rect("Rows",view);rows.anchorMin=new Vector2(0,1);rows.anchorMax=Vector2.one;rows.pivot=new Vector2(.5f,1);
            rows.sizeDelta=Vector2.zero;rows.anchoredPosition=Vector2.zero;
            var layout=rows.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=theme.spacing;layout.childForceExpandHeight=false;
            var fit=rows.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=view;scroll.content=rows;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=34;
            Root.SetActive(false);
        }
        internal void Begin(string title,string note)
        {
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(selected!=null && selected.transform.parent==rows && page!=null)
            {
                int index=0;
                foreach(Transform row in rows)
                {
                    if(!row.gameObject.activeSelf)continue;
                    if(row.gameObject==selected){focusMemory[page]=index;break;}
                    index++;
                }
            }
            focusIndex=focusMemory.TryGetValue(title,out var remembered)?remembered:-1;choiceIndex=0;promptPad=null;
            IsOpen=true;Root.SetActive(true);heading.text=title;message.text=note;first=null;lastSelection=null;
            page=title;Arrange();motion.Enter();
            for(int i=rows.childCount-1;i>=0;i--)
            {
                var item=rows.GetChild(i).gameObject;item.SetActive(false);UnityEngine.Object.Destroy(item);
            }
            rows.anchoredPosition=Vector2.zero;
        }
        internal void Status(string value){message.text=value;message.color=value.IndexOf("fail",StringComparison.OrdinalIgnoreCase)>=0||value.IndexOf("error",StringComparison.OrdinalIgnoreCase)>=0?theme.error:muted;}
        internal void Choice(string caption,Action action,bool enabled=true)
        {
            var row=Rect(caption,rows);
            var bg=row.gameObject.AddComponent<Image>();bg.color=Color.white;
            var button=row.gameObject.AddComponent<Button>();button.targetGraphic=bg;
            var colors=button.colors;colors.normalColor=new Color(theme.panel.r,theme.panel.g,theme.panel.b,.55f);colors.selectedColor=theme.selected;
            colors.highlightedColor=colors.selectedColor;colors.pressedColor=theme.accent;colors.fadeDuration=theme.focusSeconds;
            colors.disabledColor=new Color(theme.panel.r,theme.panel.g,theme.panel.b,.28f);button.colors=colors;button.interactable=enabled;bg.CrossFadeColor(enabled?colors.normalColor:colors.disabledColor,0,true,true);
            var label=Label(row,page=="Main menu"?caption.ToUpperInvariant():caption,theme.bodySize,enabled?ink:muted);Bounds(label.rectTransform,0,0,1,1);
            label.rectTransform.offsetMin=new Vector2(18,2);label.rectTransform.offsetMax=new Vector2(-14,-2);
            var settings=label.GetGenerationSettings(new Vector2(Mathf.Max(250,scroll.viewport.rect.width-32),0));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight=Mathf.Max(theme.rowHeight,label.cachedTextGeneratorForLayout.GetPreferredHeight(caption,settings)/label.pixelsPerUnit+14);
            var mark=Rect("Focus accent",row);Bounds(mark,0,.18f,0,.82f);mark.sizeDelta=new Vector2(3,0);mark.gameObject.AddComponent<Image>().raycastTarget=false;
            row.gameObject.AddComponent<UIFocusFeedback>().Bind(button,mark,label);
            if(enabled && (first==null || choiceIndex==focusIndex))first=button;
            choiceIndex++;
            button.onClick.AddListener(()=>{try{if(theme.confirmSound)motion.Sound.PlayOneShot(theme.confirmSound,.3f);action();}catch(Exception ex){Status(ex.Message);Debug.LogWarning("Menu: "+ex.Message);}});
        }
        internal void Focus(bool firstChoice=false)
        {
            if(firstChoice)
                foreach(Transform row in rows)
                    if(row.gameObject.activeSelf && row.TryGetComponent<Selectable>(out var choice) && choice.interactable){first=choice;break;}
            Canvas.ForceUpdateCanvases();
            EventSystem.current?.SetSelectedGameObject(first!=null?first.gameObject:null);
            scroll.verticalNormalizedPosition=1;
        }
        internal void RevealSelection()
        {
            ApplySafeArea();
            bool pad=Gamepad.current!=null && Gamepad.current.lastUpdateTime>Math.Max(Mouse.current?.lastUpdateTime??0,Keyboard.current?.lastUpdateTime??0);
            bool overflow=rows.rect.height>scroll.viewport.rect.height+1;
            if(promptPad!=pad || promptOverflow!=overflow)
            {
                promptPad=pad;promptOverflow=overflow;
                prompts.text=Prompt("Navigate",pad)+"  Navigate     "+Prompt("Submit",pad)+"  Select     "+Prompt("Cancel",pad)+"  Back"+(overflow?"     Scroll for more":"");
            }
            var current=EventSystem.current?.currentSelectedGameObject;
            if(current==null || current==lastSelection || current.transform.parent!=rows)return;
            lastSelection=current;
            var box=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,current.transform);
            float low=scroll.viewport.rect.yMin,high=scroll.viewport.rect.yMax;
            float change=box.min.y<low?low-box.min.y:box.max.y>high?high-box.max.y:0;
            rows.anchoredPosition+=Vector2.up*change;
        }
        private static string Prompt(string actionName,bool pad)
        {
            var action=WindrunnerInputHost.Active?.Input?.GetAction("UI",actionName);
            if(action!=null)
                for(int i=0;i<action.bindings.Count;i++)
                {
                    var binding=action.bindings[i];
                    if(binding.isComposite && i+1<action.bindings.Count)
                    {
                        if(action.bindings[i+1].effectivePath.IndexOf(pad?"Gamepad":"Keyboard",StringComparison.OrdinalIgnoreCase)>=0)return action.GetBindingDisplayString(i);
                        continue;
                    }
                    if(string.IsNullOrEmpty(binding.effectivePath))continue;
                    if(binding.effectivePath.IndexOf(pad?"Gamepad":"Keyboard",StringComparison.OrdinalIgnoreCase)>=0)
                        return action.GetBindingDisplayString(i);
                }
            return pad?"Controller":"Keyboard";
        }
        internal void Slider(string caption,float value,Action<float> changed)
        {
            var row=Rect(caption,rows);row.gameObject.AddComponent<LayoutElement>().preferredHeight=theme.rowHeight+12;
            var bg=row.gameObject.AddComponent<Image>();bg.color=Color.white;
            var slider=row.gameObject.AddComponent<Slider>();slider.targetGraphic=bg;slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;
            var colors=slider.colors;colors.normalColor=theme.panel;colors.selectedColor=theme.selected;colors.highlightedColor=theme.selected;colors.pressedColor=theme.selected;colors.fadeDuration=theme.focusSeconds;slider.colors=colors;bg.CrossFadeColor(colors.normalColor,0,true,true);
            var label=Label(row,caption,theme.bodySize,ink);Bounds(label.rectTransform,.035f,.42f,.95f,.98f);
            var track=Rect("Track",row);Bounds(track,.04f,.16f,.94f,.23f);track.gameObject.AddComponent<Image>().color=theme.muted;
            var fill=Rect("Fill",track);Bounds(fill,0,0,1,1);fill.gameObject.AddComponent<Image>().color=theme.accent;slider.fillRect=fill;
            var handle=Rect("Handle",track);Bounds(handle,0,.5f,0,.5f);handle.sizeDelta=new Vector2(12,18);handle.gameObject.AddComponent<Image>().color=ink;slider.handleRect=handle;
            slider.SetValueWithoutNotify(value);
            label.text=caption+"    "+Mathf.RoundToInt(value*100)+"%";
            slider.onValueChanged.AddListener(v=>{try{changed(v);label.text=caption+"    "+Mathf.RoundToInt(v*100)+"%";}catch(Exception e){Status(e.Message);}});
            if(first==null||choiceIndex==focusIndex)first=slider;choiceIndex++;
        }
        internal RawImage VideoSurface(float aspect)
        {
            var screen=Rect("Gallery movie",Root.transform);
            Bounds(screen,.03f,.24f,.97f,.97f);
            var frame=Rect("Picture",screen);Bounds(frame,0,0,1,1);
            var image=frame.gameObject.AddComponent<RawImage>();image.raycastTarget=false;
            var fit=frame.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=aspect;
            var controls=scroll.GetComponent<RectTransform>();Bounds(controls,.50f,.08f,.93f,.18f);
            return image;
        }
        internal RawImage Portrait()
        {
            brand.SetActive(false);brandRule.SetActive(false);
            var frame=Rect("Dagger portrait",Root.transform);
            var image=frame.gameObject.AddComponent<RawImage>();image.raycastTarget=false;
            var fit=frame.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=.75f;
            // Constrain to a dedicated parent so the fitter cannot fill the whole menu.
            var area=Rect("Portrait area",surface);Bounds(area,.32f,.12f,.68f,.84f);
            frame.SetParent(area,false);
            Bounds(message.rectTransform,.71f,.16f,.94f,.76f);
            return image;
        }
        internal void RestoreMessage(){brand.SetActive(true);brandRule.SetActive(true);}
        internal void RestoreList()=>Arrange();
        private void ApplySafeArea()
        {
            var safe=Screen.safeArea;surface.anchorMin=new Vector2(safe.xMin/Screen.width,safe.yMin/Screen.height);surface.anchorMax=new Vector2(safe.xMax/Screen.width,safe.yMax/Screen.height);
            scaler.referenceResolution=new Vector2(1600,900)/WindrunnerUITheme.Scale;
        }
        private void Arrange()
        {
            bool home=page=="Main menu",arsenal=page=="Dagger Arsenal";
            bool slots=page=="Load game"||page=="Save game"||page=="New game"||page=="Controls";
            brand.SetActive(!arsenal);brandRule.SetActive(true);eyebrow.gameObject.SetActive(home);
            var logo=brand.GetComponent<Text>();logo.fontSize=home?theme.brandSize:20;
            Bounds(logo.rectTransform,.075f,home?.72f:.91f,.85f,home?.87f:.96f);
            Bounds(heading.rectTransform,.075f,home?.58f:.79f,.93f,home?.64f:.89f);heading.fontSize=home?18:theme.headingSize;
            Bounds(brandRule.GetComponent<RectTransform>(),.075f,home?.65f:.77f,.15f,home?.653f:.773f);
            Bounds(scroll.GetComponent<RectTransform>(),.075f,.12f,arsenal?.30f:slots?.70f:.47f,home?.56f:.74f);
            Bounds(message.rectTransform,arsenal?.71f:slots?.75f:.61f,.18f,.94f,.70f);message.fontSize=slots?18:theme.bodySize;
            message.color=muted;ApplySafeArea();
        }
    }
}
