using System;
namespace PortableAnimation
{
    public enum MontageRequestResult { Started,Queued,Rejected,AlreadyPlaying }
    public enum MontageSignalKind { Started,Canceled,Completed,Queued,Marker }
    public readonly struct MontageSignal
    {
        public readonly MontageSignalKind Kind;
        public readonly string ActionId,MarkerId;
        public readonly double ClipTime;
        public MontageSignal(MontageSignalKind kind,string actionId,string markerId,double clipTime)
        {Kind=kind;ActionId=actionId;MarkerId=markerId;ClipTime=clipTime;}
        public override string ToString()=>Kind+": "+ActionId+(Kind==MontageSignalKind.Marker?" / "+MarkerId:"")+" @ "+ClipTime.ToString("0.000")+"s";
    }
    public sealed class ActionMontageController
    {
        private readonly ActionMontageProfile profile;
        private readonly AbilityWeightController weights;
        private readonly double[] times;
        private readonly bool[][] fired;
        private int active=-1,queued=-1;
        public event Action<MontageSignal> SignalRaised;
        public int ActiveIndex=>active;
        public int QueuedIndex=>queued;
        public int TargetIndex=>weights.TargetIndex;
        public bool IsBlending=>weights.IsTransitioning;
        public ActionMontageController(ActionMontageProfile profile)
        {
            profile.AssertValid();this.profile=profile;
            weights=new AbilityWeightController(profile.Actions.Count);
            times=new double[profile.Actions.Count];fired=new bool[profile.Actions.Count][];
            for(int i=0;i<fired.Length;i++)fired[i]=new bool[profile.Actions[i].Markers.Count];
        }
        public float GetWeight(int index)=>weights.GetWeight(index);
        public double GetClipTime(int index)=>times[index];
        public string GetName(int index)=>index<0||index>=times.Length?"None":profile.Actions[index].Id;
        public MontageRequestResult Request(int index)
        {
            if(index<0||index>=times.Length)throw new ArgumentOutOfRangeException(nameof(index));
            if(index==active)return MontageRequestResult.AlreadyPlaying;
            if(active<0){Start(index);return MontageRequestResult.Started;}
            var current=profile.Actions[active];var incoming=profile.Actions[index];
            if(current.Interruptible&&incoming.Priority>current.Priority)
            {Raise(MontageSignalKind.Canceled,active,"",times[active]);Start(index);return MontageRequestResult.Started;}
            if(incoming.QueueWhenBlocked){queued=index;Raise(MontageSignalKind.Queued,index,"",times[index]);return MontageRequestResult.Queued;}
            return MontageRequestResult.Rejected;
        }
        public void Cancel(bool clearQueue=true)
        {
            if(clearQueue)queued=-1;
            if(active<0)return;
            int departure=active;active=-1;
            Raise(MontageSignalKind.Canceled,departure,"",times[departure]);
            if(queued>=0){int next=queued;queued=-1;Start(next);}
            else weights.Request(-1,profile.Actions[departure].BlendOut);
        }
        public void ClearQueue()=>queued=-1;
        public void Advance(float deltaTime)
        {
            float dt=UnityEngine.Mathf.Max(0f,deltaTime);
            if(active>=0)
            {
                int i=active;var entry=profile.Actions[i];
                double previous=times[i];double duration=entry.Clip.length;
                double current=Math.Min(duration,previous+dt*entry.PlaybackSpeed);
                times[i]=current;
                for(int marker=0;marker<entry.Markers.Count;marker++)
                {
                    if(fired[i][marker])continue;
                    double eventTime=duration*entry.Markers[marker].NormalizedTime;
                    if(previous<=eventTime&&current>=eventTime&&(current>previous||eventTime==0d))
                    {fired[i][marker]=true;Raise(MontageSignalKind.Marker,i,entry.Markers[marker].EventId,eventTime);}
                }
                if(current>=duration)
                {
                    Raise(MontageSignalKind.Completed,i,"",duration);active=-1;
                    if(queued>=0){int next=queued;queued=-1;Start(next);}
                    else if(entry.EndBehavior==MontageEndBehavior.ReturnToLocomotion)weights.Request(-1,entry.BlendOut);
                }
            }
            weights.Advance(dt);
        }
        private void Start(int index)
        {
            active=index;times[index]=0;Array.Clear(fired[index],0,fired[index].Length);queued=-1;
            var entry=profile.Actions[index];weights.Request(index,entry.BlendIn);
            Raise(MontageSignalKind.Started,index,"",0d);
            for(int i=0;i<entry.Markers.Count;i++)if(entry.Markers[i].NormalizedTime<=0f)
            {fired[index][i]=true;Raise(MontageSignalKind.Marker,index,entry.Markers[i].EventId,0d);}
        }
        private void Raise(MontageSignalKind kind,int index,string marker,double time)
        {SignalRaised?.Invoke(new MontageSignal(kind,GetName(index),marker,time));}
    }
}
