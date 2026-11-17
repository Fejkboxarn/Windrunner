using System;
using System.Collections.Generic;
using AirflowPrototype;
using UnityEngine;
using UnityEngine.UI;
using Windrunner.Frontend;

namespace Windrunner.Daggers
{
    [DisallowMultipleComponent,DefaultExecutionOrder(10)]
    public sealed class DaggerPlayer : MonoBehaviour
    {
        public PlayerSession session;
        public DaggerCatalog catalog;
        public Animator rig;
        public AirPower power;
        public MeshRenderer originalA,originalB;
        public DaggerEquipment Equipment {get;private set;}
        public Transform LeftGrip {get;private set;}
        public Transform RightGrip {get;private set;}
        public GameObject LeftModel {get;private set;}
        public GameObject RightModel {get;private set;}
        readonly List<IDisposable> passiveHandles=new();
        Windrunner.Production.Progression.ProgressionService progression;
        GameObject hud;Image icon;Text label;
        bool refresh, waitForRelease, hidA,hidB;
        int interruption;
        float nextHud,flashUntil;
        DaggerPair shown;
        public bool GameplayAllowed=>isActiveAndEnabled && session && !session.Busy && session.character && session.character.enabled &&
            session.character.gameObject.activeInHierarchy && session.input.Input!=null && !session.input.Input.IsGameplayBlocked && Time.timeScale>0;
        void Start()
        {
            if(!session || !catalog || !rig || !rig.isHuman || !power || !originalA || !originalB || originalA==originalB)
            {Debug.LogError("Dagger player requires a session, catalog, humanoid rig, AirPower and two distinct hand props.",this);enabled=false;return;}
            if(session.Progression==null)return;
            var error=catalog.ValidateSetup(true);
            if(error!=null){Debug.LogError(error,this);enabled=false;return;}
            var leftBone=rig.GetBoneTransform(HumanBodyBones.LeftHand);
            var leftProp=originalA.transform.IsChildOf(leftBone)?originalA:originalB;
            LeftGrip=CreateGrip(leftBone,HumanBodyBones.LeftMiddleProximal,leftProp.transform);
            RightGrip=CreateGrip(rig.GetBoneTransform(HumanBodyBones.RightHand),HumanBodyBones.RightMiddleProximal,
                (leftProp==originalA?originalB:originalA).transform);
            hidA=originalA.forceRenderingOff;hidB=originalB.forceRenderingOff;
            originalA.forceRenderingOff=originalB.forceRenderingOff=true;
            progression=session.Progression;
            Equipment=new DaggerEquipment(catalog,progression,power);
            interruption=session.input.Input.InterruptionVersion;
            progression.Changed+=QueueRefresh;
            Equipment.EquippedChanged+=Show;Equipment.Activated+=Pulse;
            CreateHud();Show(Equipment.Equipped);
        }
        void QueueRefresh()=>refresh=true;
        Transform CreateGrip(Transform hand,HumanBodyBones finger,Transform original)
        {
            var grip=new GameObject(finger==HumanBodyBones.LeftMiddleProximal?"Left dagger grip":"Right dagger grip").transform;
            grip.SetParent(hand,false);
            var knuckle=rig.GetBoneTransform(finger);
            grip.position=knuckle?Vector3.Lerp(hand.position,knuckle.position,.8f):hand.position;
            grip.rotation=original.rotation;
            return grip;
        }
        void Update()
        {
            if(Equipment==null)return;
            if(refresh){refresh=false;Equipment.Synchronize();}
            var input=session && session.input ? session.input.Input : null;
            if(input==null){hud.SetActive(false);waitForRelease=true;return;}
            var action=input.GetAction("Gameplay","DaggerAbility");
            bool allowed=GameplayAllowed;
            if(interruption!=input.InterruptionVersion || !allowed)
            {waitForRelease=true;interruption=input.InterruptionVersion;}
            if(allowed)
            {
                Equipment.Tick(Time.deltaTime);
                if(!action.IsPressed())waitForRelease=false;
                if(!waitForRelease && action.WasPressedThisFrame())Equipment.TryActivate(true,Time.frameCount);
            }
            hud.SetActive(allowed && shown);
            if(shown && Time.unscaledTime>=nextHud)
            {
                nextHud=Time.unscaledTime+.12f;
                label.text=Time.time<flashUntil?shown.active.displayName:Equipment.Status(shown);
            }
        }
        public static GameObject Mount(GameObject prefab,Transform grip,Vector3 offset,Vector3 euler)
        {
            var model=Instantiate(prefab,grip,false);
            model.transform.localPosition=offset;
            model.transform.localRotation=Quaternion.Euler(euler);
            return model;
        }
        static void Retire(GameObject go){if(go){go.SetActive(false);Destroy(go);}}
        void Show(DaggerPair pair)
        {
            foreach(var item in passiveHandles)item?.Dispose();passiveHandles.Clear();
            Retire(LeftModel);Retire(RightModel);LeftModel=RightModel=null;
            shown=pair;flashUntil=0;nextHud=0;
            if(!pair)return;
            LeftModel=Mount(pair.leftModel,LeftGrip,pair.leftOffset,pair.leftEuler);
            RightModel=Mount(pair.rightModel,RightGrip,pair.rightOffset,pair.rightEuler);
            foreach(var passive in pair.passives)passiveHandles.Add(passive.Attach(gameObject));
            icon.sprite=pair.icon;icon.color=pair.accent;
        }
        void Pulse(DaggerPair pair)
        {
            flashUntil=Time.time+.65f;nextHud=0;
            if(!pair.activationVfx)return;
            var fx=Instantiate(pair.activationVfx,RightGrip.position,Quaternion.identity,transform);
            fx.Play();Destroy(fx.gameObject,2f);
        }
        void CreateHud()
        {
            hud=new GameObject("Dagger status",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            hud.transform.SetParent(transform,false);
            var canvas=hud.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=5;
            var scaler=hud.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);
            var badge=new GameObject("Equipped pair",typeof(RectTransform),typeof(Image));badge.transform.SetParent(hud.transform,false);
            icon=badge.GetComponent<Image>();icon.raycastTarget=false;
            var r=icon.rectTransform;r.anchorMin=r.anchorMax=new Vector2(1,0);r.anchoredPosition=new Vector2(-175,56);r.sizeDelta=new Vector2(44,44);
            var caption=new GameObject("Ability status",typeof(RectTransform),typeof(Text));caption.transform.SetParent(badge.transform,false);
            label=caption.GetComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=19;label.raycastTarget=false;
            label.alignment=TextAnchor.MiddleLeft;label.rectTransform.sizeDelta=new Vector2(145,52);label.rectTransform.anchoredPosition=new Vector2(102,0);
        }
        void OnEnable()
        {
            if(Equipment==null)return;
            Equipment.EquippedChanged+=Show;Equipment.Activated+=Pulse;
            originalA.forceRenderingOff=originalB.forceRenderingOff=true;
            Show(Equipment.Equipped);waitForRelease=true;
        }
        void OnDisable()
        {
            if(Equipment==null)return;
            Equipment.EquippedChanged-=Show;Equipment.Activated-=Pulse;
            if(hud)hud.SetActive(false);
            Show(null);
            if(originalA)originalA.forceRenderingOff=hidA;
            if(originalB)originalB.forceRenderingOff=hidB;
        }
        void OnDestroy()
        {
            if(Equipment==null)return;
            progression.Changed-=QueueRefresh;Equipment.EquippedChanged-=Show;Equipment.Activated-=Pulse;
            if(hud)Destroy(hud);
            if(LeftGrip)Destroy(LeftGrip.gameObject);if(RightGrip)Destroy(RightGrip.gameObject);
        }
    }
}
