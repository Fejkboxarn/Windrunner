using System;
using System.Globalization;
using UnityEngine;
namespace Windrunner.Production.Progression
{
    public enum WayfinderPhase { Objective, TurnIn }
    [Serializable] public sealed class ObjectiveAnchor
    {
        public string questId, objectiveId;
        public WayfinderPhase phase;
        public Transform anchor;
        public string label;
        public bool approximateArea;
        public string areaId;
        public TimedRetrieval retrieval;
        public Transform Resolve(ProgressionActor actor)
        {
            var target = retrieval && retrieval.Carrying && retrieval.Remaining > 0
                ? (retrieval.destination ? retrieval.destination.transform : null) : anchor;
            return target && actor && target.gameObject.activeInHierarchy && target.gameObject.scene == actor.gameObject.scene ? target : null;
        }
    }
    public static class WayfinderRules
    {
        public static ObjectiveAnchor Select(ProgressionService service, ProgressionState state, ProgressionCatalog catalog, ObjectiveAnchor[] anchors, ProgressionActor actor)
        {
            if(service==null || state==null || catalog==null || anchors==null)return null;
            var quest=catalog.definitions.quests.Find(q=>q.id==state.trackedQuestId);
            if(quest==null)return null;
            if(service.Status(quest.id)==QuestStatus.ReadyToTurnIn)return Find(quest,null,WayfinderPhase.TurnIn,anchors,actor);
            if(service.Status(quest.id)!=QuestStatus.Active)return null;
            foreach(var group in quest.groups)foreach(var objective in group.objectives)
                if(service.ObjectiveOpen(quest.id,objective.id))return Find(quest,objective,WayfinderPhase.Objective,anchors,actor);
            return null;
        }
        static ObjectiveAnchor Find(QuestDefinition quest,ObjectiveDefinition objective,WayfinderPhase phase,ObjectiveAnchor[] anchors,ProgressionActor actor)
        {
            foreach(var a in anchors)
            {
                if(a==null || a.questId!=quest.id || a.phase!=phase || (objective!=null && a.objectiveId!=objective.id) || !a.Resolve(actor))continue;
                var guidance=objective!=null && objective.guidance.showArea ? objective.guidance : quest.guidance;
                if(a.approximateArea && (!guidance.showArea || guidance.areaId!=a.areaId))continue;
                return a;
            }
            return null;
        }
        public static string Distance(float metres) => metres>=999.5f ? (metres/1000).ToString("0.0",CultureInfo.InvariantCulture)+" km" : Mathf.RoundToInt(Mathf.Max(0,metres))+" m";
        public static Vector2 Project(Vector3 viewport,out bool edge,out float angle,bool wasEdge=false)
        {
            var delta=new Vector2((viewport.x-.5f)*1280,(.5f-viewport.y)*720);
            if(viewport.z<=0){delta=-delta;if(delta.sqrMagnitude<.01f)delta=Vector2.down;}
            edge=viewport.z<=0 || Mathf.Abs(delta.x)>(wasEdge?512:520) || Mathf.Abs(delta.y)>(wasEdge?202:210);
            if(edge)delta*=Mathf.Min(520/Mathf.Max(.001f,Mathf.Abs(delta.x)),210/Mathf.Max(.001f,Mathf.Abs(delta.y)));
            angle=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg+90;
            var result=new Vector2(640,360)+delta;
            // Lift on-screen cards above existing character labels; keep edge cards at the boundary.
            if(!edge)result.y=Mathf.Clamp(result.y-75,150,570);
            // Reserve the upper-right quest summary and lower HUD meters.
            if(result.x>830 && result.y<290)result.y=290;
            if(result.x>250 && result.x<1030 && result.y>450)result.y=450;
            return result;
        }
    }
    public sealed class WayfinderPresentation
    {
        public ObjectiveAnchor Binding { get; private set; }
        public Transform Target { get; private set; }
        public bool Visible { get; private set; }
        public bool Offscreen { get; private set; }
        public string DistanceText { get; private set; }
        Vector2 position; bool positioned; float angle,nextText; string caption;
        GUIStyle text,icon;
        public void SetBinding(ObjectiveAnchor value){if(Binding==value)return;Binding=value;positioned=false;nextText=0;}
        public void Update(ProgressionActor actor,Camera view,bool blocked)
        {
            var previous=Target;Target=Binding?.Resolve(actor);
            Visible=WindrunnerUITheme.ObjectiveWayfinder && !blocked && Target && view && view.isActiveAndEnabled;
            if(!Visible){positioned=false;return;}
            if(previous!=Target){nextText=0;positioned=false;}
            var screen=view.WorldToScreenPoint(Target.position+Vector3.up*2);
            var safe=Screen.safeArea;float scale=Mathf.Min(safe.width/1280f,safe.height/720f);
            var viewport=new Vector3((screen.x-safe.x-(safe.width-1280*scale)/2)/(1280*scale),(screen.y-safe.y-(safe.height-720*scale)/2)/(720*scale),screen.z);
            var projected=WayfinderRules.Project(viewport,out bool edge,out angle,Offscreen);
            Offscreen=edge;
            position=positioned && !WindrunnerUITheme.ReducedMotion ? Vector2.Lerp(position,projected,1-Mathf.Exp(-18*Time.unscaledDeltaTime)) : projected;positioned=true;
            if(Time.unscaledTime>=nextText)
            {
                nextText=Time.unscaledTime+.1f;DistanceText=WayfinderRules.Distance(Vector3.Distance(actor.transform.position,Target.position));
                caption=Binding.retrieval && Binding.retrieval.Carrying ? "Return destination" : Binding.label;
            }
        }
        public void Draw()
        {
            if(!Visible || !Target || !WindrunnerUITheme.ObjectiveWayfinder)return;
            var theme=WindrunnerUITheme.Current;
            if(text==null){text=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=16};icon=new GUIStyle(text){fontSize=28};}
            text.font=icon.font=theme.Font;text.normal.textColor=theme.ink;icon.normal.textColor=theme.accent;
            var color=GUI.color;var matrix=GUI.matrix;
            GUI.color=new Color(.025f,.04f,.05f,.83f);GUI.DrawTexture(new Rect(position.x-102,position.y-25,204,82),Texture2D.whiteTexture);GUI.color=Color.white;
            if(Offscreen)GUI.matrix=matrix * Matrix4x4.Translate(position) * Matrix4x4.Rotate(Quaternion.Euler(0,0,angle)) * Matrix4x4.Translate(-position);
            GUI.Label(new Rect(position.x-18,position.y-18,36,36),Offscreen?"\u25b2":"\u25c7",icon);GUI.matrix=matrix;
            GUI.Label(new Rect(position.x-100,position.y+10,200,24),caption??"",text);
            GUI.Label(new Rect(position.x-100,position.y+32,200,24),DistanceText??"",text);GUI.color=color;
        }
    }
}
