using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Windrunner.Frontend
{
    internal sealed class MenuLayout
    {
        private readonly Font font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private readonly Color ink=new Color(.91f,.95f,.93f), muted=new Color(.65f,.77f,.76f);
        internal readonly GameObject Root;
        private readonly Text heading,message;
        private readonly ScrollRect scroll;
        private readonly RectTransform rows;
        private Button first;
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
            var scaler=Root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            Root.AddComponent<GraphicRaycaster>();
            var background=Rect("Backdrop",Root.transform);Bounds(background,0,0,1,1);
            background.gameObject.AddComponent<Image>().color=new Color(.034f,.080f,.087f,.98f);
            var rule=Rect("Accent",background);Bounds(rule,.075f,.54f,.14f,.545f);
            rule.gameObject.AddComponent<Image>().color=new Color(.92f,.69f,.35f);
            var title=Label(background,"WINDRUNNER",59,ink);title.fontStyle=FontStyle.Bold;Bounds(title.rectTransform,.075f,.55f,.49f,.72f);
            heading=Label(background,"",38,ink);Bounds(heading.rectTransform,.50f,.79f,.93f,.92f);
            message=Label(background,"",20,muted);Bounds(message.rectTransform,.075f,.25f,.41f,.52f);
            var view=Rect("Choices",background);Bounds(view,.50f,.10f,.93f,.78f);
            scroll=view.gameObject.AddComponent<ScrollRect>();view.gameObject.AddComponent<RectMask2D>();
            var hit=view.gameObject.AddComponent<Image>();hit.color=Color.clear;
            rows=Rect("Rows",view);rows.anchorMin=new Vector2(0,1);rows.anchorMax=Vector2.one;rows.pivot=new Vector2(.5f,1);
            rows.sizeDelta=Vector2.zero;rows.anchoredPosition=Vector2.zero;
            var layout=rows.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=7;layout.childForceExpandHeight=false;
            var fit=rows.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=view;scroll.content=rows;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=34;
            Root.SetActive(false);
        }
        internal void Begin(string title,string note)
        {
            focusIndex=-1;choiceIndex=0;
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(heading.text==title && selected!=null && selected.transform.parent==rows)
            {
                foreach(Transform row in rows)
                {
                    if(!row.gameObject.activeSelf)continue;
                    focusIndex++;
                    if(row.gameObject==selected)break;
                }
            }
            Root.SetActive(true);heading.text=title;message.text=note;first=null;lastSelection=null;
            for(int i=rows.childCount-1;i>=0;i--)
            {
                var item=rows.GetChild(i).gameObject;item.SetActive(false);UnityEngine.Object.Destroy(item);
            }
            rows.anchoredPosition=Vector2.zero;
        }
        internal void Status(string value)=>message.text=value;
        internal void Choice(string caption,Action action,bool enabled=true)
        {
            var row=Rect(caption,rows);
            var bg=row.gameObject.AddComponent<Image>();bg.color=Color.white;
            var button=row.gameObject.AddComponent<Button>();button.targetGraphic=bg;
            var colors=button.colors;colors.normalColor=new Color(.09f,.18f,.19f);colors.selectedColor=new Color(.20f,.39f,.38f);
            colors.highlightedColor=colors.selectedColor;colors.pressedColor=new Color(.30f,.47f,.42f);
            colors.disabledColor=new Color(.065f,.12f,.13f);button.colors=colors;button.interactable=enabled;
            var label=Label(row,caption,23,enabled?ink:muted);Bounds(label.rectTransform,0,0,1,1);
            label.rectTransform.offsetMin=new Vector2(18,2);label.rectTransform.offsetMax=new Vector2(-14,-2);
            var settings=label.GetGenerationSettings(new Vector2(Mathf.Max(250,scroll.viewport.rect.width-32),0));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight=Mathf.Max(56,label.cachedTextGeneratorForLayout.GetPreferredHeight(caption,settings)/label.pixelsPerUnit+14);
            if(enabled && (first==null || choiceIndex==focusIndex))first=button;
            choiceIndex++;
            button.onClick.AddListener(()=>{try{action();}catch(Exception ex){Status(ex.Message);Debug.LogWarning("Menu: "+ex.Message);}});
        }
        internal void Focus()
        {
            Canvas.ForceUpdateCanvases();
            EventSystem.current?.SetSelectedGameObject(first!=null?first.gameObject:null);
            scroll.verticalNormalizedPosition=1;
        }
        internal void RevealSelection()
        {
            var current=EventSystem.current?.currentSelectedGameObject;
            if(current==null || current==lastSelection || current.transform.parent!=rows)return;
            lastSelection=current;
            var box=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,current.transform);
            float low=scroll.viewport.rect.yMin,high=scroll.viewport.rect.yMax;
            float change=box.min.y<low?low-box.min.y:box.max.y>high?high-box.max.y:0;
            rows.anchoredPosition+=Vector2.up*change;
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
        internal void RestoreList()=>Bounds(scroll.GetComponent<RectTransform>(),.50f,.10f,.93f,.78f);
    }
}
