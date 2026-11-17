using System;
using System.Collections.Generic;
using AirflowPrototype;
using UnityEngine;
using UnityEngine.UI;
using Windrunner.Frontend;
using Windrunner.Production.Feedback;

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
        RectTransform reserveMeter;
        Image reserveFill;
        Material reserveStyle;
        Text reserveCaption;
        float chargeFlashUntil;
        float lastCapacity;
        bool lastFull;

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
            Equipment.Listen(true);Equipment.ReserveChanged+=ReserveUpdated;
            CreateHud();CreateReserveMeter();Show(Equipment.Equipped);
        }
        void QueueRefresh()=>refresh=true;
        Transform CreateGrip(Transform hand,HumanBodyBones finger,Transform original)
            => CreateHandGrip(rig,hand,finger,original);

        public static Transform CreateHandGrip(Animator rig, Transform hand,HumanBodyBones finger,Transform original)
        {
            var grip=new GameObject(finger==HumanBodyBones.LeftMiddleProximal?"Left dagger grip":"Right dagger grip").transform;
            grip.SetParent(hand,false);
            var knuckle=rig.GetBoneTransform(finger);
            grip.position=knuckle?Vector3.Lerp(hand.position,knuckle.position,.8f):hand.position;
            // Both equipment meshes use local +Y from hilt to tip. Establish
            // the forward grip across the palm towards the thumb on each hand.
            bool left = finger == HumanBodyBones.LeftMiddleProximal;
            var index = rig.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var little = rig.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            Vector3 blade = index && little ? index.position - little.position : Vector3.zero;
            Vector3 alongPalm = knuckle ? knuckle.position - hand.position : Vector3.zero;
            Vector3 face = Vector3.Cross(blade, alongPalm) * (left ? -1f : 1f);
            grip.rotation = face.sqrMagnitude > 1e-12f
                ? Quaternion.LookRotation(face.normalized, blade.normalized) : original.rotation;
            return grip;
        }
        void Update()
        {
            if(Equipment==null)return;
            if(refresh){refresh=false;Equipment.Synchronize();}
            Equipment.ReconcileEquippedCapacity();
            var input=session && session.input ? session.input.Input : null;
            if(input==null){hud.SetActive(false);waitForRelease=true;return;}
            var action=input.GetAction("Gameplay","DaggerAbility");
            bool allowed=GameplayAllowed;
            if(interruption!=input.InterruptionVersion || !allowed)
            {waitForRelease=true;interruption=input.InterruptionVersion;}
            if(allowed)
            {

                if(!action.IsPressed())waitForRelease=false;
                if(!waitForRelease && action.WasPressedThisFrame())Equipment.TryActivate(true,Time.frameCount);
            }
            hud.SetActive(allowed && shown);
            if (reserveMeter)
            {
                var wind = shown ? shown.active as SecondWind : null;
                reserveMeter.gameObject.SetActive(allowed && wind);
                if (wind && (lastCapacity != wind.capacity || lastFull != (power.Current >= power.Max) ||
                    (chargeFlashUntil > 0 && Time.unscaledTime >= chargeFlashUntil)))
                {
                    DrawReserve();
                }
            }
            if(shown && Time.unscaledTime>=nextHud)
            {
                nextHud=Time.unscaledTime+.12f;
                label.text=Time.time<flashUntil?shown.active.displayName:Equipment.Status(shown,false);
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
            DrawReserve();
            if(!pair)return;
            LeftModel=Mount(pair.leftModel,LeftGrip,pair.leftOffset,pair.leftEuler);
            RightModel=Mount(pair.rightModel,RightGrip,pair.rightOffset,pair.rightEuler);
            foreach(var passive in pair.passives)passiveHandles.Add(passive.Attach(gameObject));
            icon.sprite=pair.icon;icon.color=pair.accent;
        }
        void Pulse(DaggerPair pair)
        {
            flashUntil=Time.time+.65f;nextHud=0;
            if (pair.active is SecondWind && session.project.Runtime.Services.TryGet<IFeedbackService>(out var feedback))
                feedback.Publish(FeedbackKey.SecondWindActivated, FeedbackRequest.At(power.transform));
            if(!pair.activationVfx)return;
            var fx=Instantiate(pair.activationVfx,RightGrip.position,Quaternion.identity,transform);
            fx.Play();Destroy(fx.gameObject,2f);
        }
        void CreateReserveMeter()
        {
            // Both production worlds already serialize the main meter's resource binding.
            foreach (var view in FindObjectsByType<AirPowerProgressBarUI>())
            {
                if (view.Resource != power || !view.Meter || view.gameObject.scene != gameObject.scene) continue;
                var panel = new GameObject("Second Wind reserve",typeof(RectTransform),typeof(Image));
                reserveMeter = (RectTransform)panel.transform;
                reserveMeter.SetParent(view.Meter,false);
                reserveMeter.anchorMin = new Vector2(.18f,.5f);
                reserveMeter.anchorMax = new Vector2(.82f,.5f);
                reserveMeter.sizeDelta = new Vector2(0,24);
                reserveMeter.anchoredPosition = new Vector2(0,27);
                var track = panel.GetComponent<Image>();
                track.color = new Color(.025f,.09f,.11f,.95f); track.raycastTarget=false;
                reserveFill = new GameObject("Stored AirPower",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                reserveFill.transform.SetParent(panel.transform,false);
                reserveFill.raycastTarget=false;
                var main = view.Meter.GetComponent<Image>();
                if (main && main.material.HasProperty("_Fill_Amount"))
                {
                    reserveStyle = new Material(main.material) { name="Second Wind HUD", hideFlags=HideFlags.DontSave };
                    reserveFill.material=reserveStyle;
                    reserveFill.sprite=main.sprite;
                    track.enabled=false;
                    // The shader draws a narrow strip across the center of the source UV rectangle.
                    // Scale both dimensions together so its outline and bevel keep the main bar's proportions.
                    reserveMeter.anchorMin=new Vector2(.18f,.18f);
                    reserveMeter.anchorMax=new Vector2(.82f,.82f);
                    reserveMeter.sizeDelta=Vector2.zero;
                }
                reserveFill.rectTransform.anchorMin=Vector2.zero;
                reserveFill.rectTransform.anchorMax=Vector2.one;
                reserveFill.rectTransform.offsetMin=reserveFill.rectTransform.offsetMax=Vector2.zero;
                reserveCaption = new GameObject("Stored amount",typeof(RectTransform),typeof(Text)).GetComponent<Text>();
                reserveCaption.transform.SetParent(panel.transform,false);
                reserveCaption.rectTransform.anchorMin=Vector2.zero; reserveCaption.rectTransform.anchorMax=Vector2.one;
                reserveCaption.rectTransform.sizeDelta=Vector2.zero; reserveCaption.rectTransform.anchoredPosition=Vector2.zero;
                reserveCaption.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); reserveCaption.fontSize=16;
                reserveCaption.alignment=TextAnchor.MiddleCenter; reserveCaption.raycastTarget=false;
                return;
            }
        }
        void ReserveUpdated(float delta)
        {
            if (delta>0 && shown && shown.active is SecondWind wind)
            {
                chargeFlashUntil=Time.unscaledTime+.25f;
                if(wind.chargeVfx)
                {
                    var fx=Instantiate(wind.chargeVfx,LeftGrip.position,Quaternion.identity,transform);
                    fx.Play();Destroy(fx.gameObject,2f);
                }
            }
            nextHud=0;
            DrawReserve();
        }
        void DrawReserve()
        {
            if(!reserveMeter)return;
            var wind=shown ? shown.active as SecondWind : null;
            reserveMeter.gameObject.SetActive(wind && GameplayAllowed);
            if(!wind)return;
            float value=Equipment.Stored(shown);
            lastCapacity=wind.capacity; lastFull=power.Current>=power.Max;
            if(Time.unscaledTime>=chargeFlashUntil)chargeFlashUntil=0;
            float fraction=Mathf.Clamp01(value/wind.capacity);
            var tint=chargeFlashUntil>0?Color.Lerp(wind.meterColor,Color.white,.45f):wind.meterColor;
            reserveFill.color=tint;
            if (reserveStyle)
            {
                reserveStyle.SetFloat("_Fill_Amount",fraction);
                reserveStyle.SetFloat("_Main_Bar_Fill_Amount",fraction+.001f);
                reserveStyle.SetColor("_Fill_Color_No_Gradient",tint);
                reserveStyle.SetColor("_Fill_Gradient_Color_Left",tint);
                reserveStyle.SetColor("_Fill_Gradient_Color_Right",Color.Lerp(tint,Color.white,.2f));
            }
            else
            {
                reserveFill.sprite=wind.meterSprite;
                reserveFill.rectTransform.anchorMax=new Vector2(fraction,1);
            }
            string status=value>=wind.capacity?" · FULL":"";
            if(value>0 && !lastFull)status+=" · READY";
            reserveCaption.text=$"SECOND WIND   {value:0.#} / {wind.capacity:0.#}{status}";
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
            Equipment.Listen(true);
            originalA.forceRenderingOff=originalB.forceRenderingOff=true;
            Show(Equipment.Equipped);waitForRelease=true;
        }
        void OnDisable()
        {
            if(Equipment==null)return;
            Equipment.EquippedChanged-=Show;Equipment.Activated-=Pulse;
            Equipment.Listen(false);
            if(hud)hud.SetActive(false);
            Show(null);
            if(originalA)originalA.forceRenderingOff=hidA;
            if(originalB)originalB.forceRenderingOff=hidB;
        }
        void OnDestroy()
        {
            if(Equipment==null)return;
            progression.Changed-=QueueRefresh;Equipment.EquippedChanged-=Show;Equipment.Activated-=Pulse;
            Equipment.ReserveChanged-=ReserveUpdated;
            if(reserveStyle)Destroy(reserveStyle);
            if(reserveMeter)Destroy(reserveMeter.gameObject);
            if(hud)Destroy(hud);
            if(LeftGrip)Destroy(LeftGrip.gameObject);if(RightGrip)Destroy(RightGrip.gameObject);
        }
    }
}
