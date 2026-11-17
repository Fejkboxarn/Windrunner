using UnityEngine;
using AirflowPrototype;
namespace Windrunner.MovementLab
{
    public enum LaneKind { Slopes, Jumps, Dash, Sling, AerialSteering, Momentum, Wind, HighSpeed, Endurance, Combined }
    public enum LanePreset { Short, Medium, Long, Extreme }

    public sealed class GymLane : MonoBehaviour
    {
        public LaneKind kind;
        public LanePreset preset;
        [Min(.5f)] public float spacing = 30;
        public float elevation = 3;
        [Min(15)] public float runUp = 55;
        [Range(-75,75)] public float approachAngle, slopeAngle = 15;
        public Vector3 targetOffset = new Vector3(0, 5, 0), tunnelSize = new Vector3(14, 15, 32);
        public Transform destination, launch, landing, slope, target, startGate, endGate, sign;
        public WindTunnel continuous, impulse;
        public TextMesh label;
        public TextMesh[] distanceMarkers = System.Array.Empty<TextMesh>();
        public Transform[] relayTargets = System.Array.Empty<Transform>();

        public Transform[] sections = System.Array.Empty<Transform>();
        public float declineAngle = 12;
        public string Difficulty => preset==LanePreset.Extreme || ((kind==LaneKind.Jumps || kind==LaneKind.AerialSteering) && preset==LanePreset.Long) ? preset+" challenge" : preset.ToString();
        public string Instruction => kind switch
        {
            LaneKind.Slopes => "Run up, turn on the crest, then descend to the stripe",
            LaneKind.Jumps => "Run to the gold lip, jump once and land across the gap",
            LaneKind.Dash => "Hold Dash along the marked approach to the finish",
            LaneKind.Sling => "Aim at the gold node, hold Sling, release and land on gold",
            LaneKind.AerialSteering => "Jump at the lip and steer toward the offset deck",
            LaneKind.Momentum => "Build speed, release Dash and coast across the stripe",
            LaneKind.Wind => "Run straight through each track and compare speeds",
            LaneKind.HighSpeed => "Accelerate down the long straight and brake beyond the stripe",
            LaneKind.Endurance => "Run the speed course and stop on the finish deck",
            _ => "Follow the relays uphill: jump, Dash, Sling and ride both winds"
        };

        public void SetPreset(LanePreset value)
        {
            preset = value;
            int n = (int)value;
            float[] distances = kind switch
            {
                LaneKind.Jumps => new[]{2f,4f,7f,12f},
                LaneKind.AerialSteering => new[]{2f,3.5f,6f,10f},
                LaneKind.Sling => new[]{16f,22f,30f,38f},
                LaneKind.Dash => new[]{36f,58f,82f,112f},
                LaneKind.Slopes => new[]{18f,24f,30f,36f},
                LaneKind.Combined => new[]{75f,115f,165f,350f},
                LaneKind.HighSpeed => new[]{160f,240f,340f,500f},
                _ => new[]{56f,80f,120f,200f}
            };
            spacing=distances[n]; runUp=kind==LaneKind.Combined && n==3 ? 170 : 16 + n*3;
            slopeAngle=12+n*5; declineAngle=12+n*3;
            elevation=kind switch {
                LaneKind.Combined => new[]{6f,12f,23f,100f}[n],
                LaneKind.Dash => n*2, LaneKind.Sling => n,
                LaneKind.Jumps or LaneKind.AerialSteering => n*.3f, _ => 0 };
            approachAngle=kind==LaneKind.Combined ? n*15 : kind==LaneKind.Dash ? n*8 : 0;
            targetOffset=new Vector3(0,3,0);
        }

        public void Apply()
        {
            Quaternion heading=Quaternion.Euler(0,approachAngle,0);
            Vector3 At(float z,float y=0,float x=0) => heading*new Vector3(x,y,z);
            void Deck(Transform t,float a,float b,float ya,float yb,float width,float x=0)
            {
                Vector3 start=At(a,ya,x), end=At(b,yb,x);
                Quaternion rotation=Quaternion.LookRotation(end-start,Vector3.up);
                Place(t,(start+end)*.5f-rotation*Vector3.up*.35f,rotation,new Vector3(width,.7f,(end-start).magnitude));
            }
            bool wind=kind==LaneKind.Wind, combo=kind==LaneKind.Combined;
            float width=wind?54:18;
            Deck(launch,-runUp-4,0,0,0,width);
            destination.SetLocalPositionAndRotation(At(-runUp+3,1.2f),heading);
            foreach(var t in sections) t.gameObject.SetActive(false);
            slope.gameObject.SetActive(false);
            float finish=spacing+4, finishY=elevation, side=0;
            if(kind==LaneKind.Slopes && sections.Length>=2)
            {
                finishY=0;
                float height=spacing*Mathf.Tan(Mathf.Clamp(slopeAngle,2,45)*Mathf.Deg2Rad);
                float bottom=spacing+8+height/Mathf.Tan(Mathf.Max(2,declineAngle)*Mathf.Deg2Rad);
                slope.gameObject.SetActive(true);Deck(slope,0,spacing,0,height,18);
                sections[0].gameObject.SetActive(true);Deck(sections[0],spacing,spacing+8,height,height,18);
                sections[1].gameObject.SetActive(true);Deck(sections[1],spacing+8,bottom,height,0,18);
                Deck(landing,bottom,bottom+18,0,0,20); finish=bottom+6;
            }
            else if(kind==LaneKind.Jumps || kind==LaneKind.AerialSteering || kind==LaneKind.Sling)
            {
                bool sling=kind==LaneKind.Sling;
                side=kind==LaneKind.AerialSteering?5:0;
                Deck(landing,spacing,spacing+(sling?100:20),elevation,elevation,sling?32:kind==LaneKind.AerialSteering?8:14,side);
                finish=spacing+(sling?60:3);
            }
            else if(combo && sections.Length>=6)
            {
                // Ridges and terraces alternate along a continuous route. Nodes allow faster aerial lines.
                float last=0;
                for(int j=0;j<6;j++)
                {
                    float height=elevation*(j+1)/6 + (j%2==0?2:0);
                    sections[j].gameObject.SetActive(true);
                    Deck(sections[j],spacing*j/6+(j==0?2:0),spacing*(j+1)/6,last,height,18);
                    last=height;
                }
                Deck(landing,spacing,spacing+38,elevation,elevation,28);
            }
            else
            {
                slope.gameObject.SetActive(true);Deck(slope,0,spacing,0,elevation,width);
                Deck(landing,spacing,spacing+(kind==LaneKind.HighSpeed?180:40),elevation,elevation,width);
            }
            // Advanced Dash starts straight and changes heading at the gold lip.
            if(kind==LaneKind.Dash && preset>=LanePreset.Long)
            {
                Quaternion approach=Quaternion.Euler(0,-approachAngle,0);
                launch.localPosition=approach*launch.localPosition;launch.localRotation=approach*launch.localRotation;
                destination.localPosition=approach*destination.localPosition;destination.localRotation=Quaternion.identity;
            }
            target.localPosition=At(spacing,elevation)+heading*targetOffset;
            target.gameObject.SetActive(kind==LaneKind.Sling || combo);
            for(int i=0;i<relayTargets.Length;i++)
            {
                float t=(i+1f)/(relayTargets.Length+1);
                relayTargets[i].localPosition=At(t*spacing,t*elevation+5);
            }
            Place(startGate,At(-.35f,.09f),heading,new Vector3(width,.08f,.45f));
            Place(endGate,At(finish,finishY+.09f,side),heading,new Vector3(wind?width:16,.08f,.5f));
            sign.SetLocalPositionAndRotation(At(3,2.8f,wind?-11:-4),heading);
            var size=SafeSize(tunnelSize);
            float tunnelStart=wind?4:spacing*.2f;
            Place(continuous.transform,At(tunnelStart+size.z*.5f,size.y*.28f+(combo?elevation*.25f:0)),heading,size);
            Place(impulse.transform,At(wind?tunnelStart+2:spacing*.75f,size.y*.28f+(combo?elevation*.75f:0),wind?18:0),heading,new Vector3(size.x,size.y,4));
            continuous.gameObject.SetActive(wind||combo);impulse.gameObject.SetActive(wind||combo);
            label.characterSize=.075f;
            label.anchor=TextAnchor.MiddleCenter;
            label.text="START   "+kind+" / "+Difficulty+"   >>>";
            for(int i=0;i<distanceMarkers.Length;i++)
            {
                float t=(i+1f)/distanceMarkers.Length;
                var m=distanceMarkers[i];m.transform.SetLocalPositionAndRotation(At(finish*t,2+finishY*t,width*.5f+1),heading);
                m.text=i==distanceMarkers.Length-1?"FINISH":(finish*t).ToString("0")+" m";
            }
        }
        private static Vector3 SafeSize(Vector3 size) => Vector3.Max(Vector3.one, size);
        private static void Place(Transform t, Vector3 p, Quaternion q, Vector3 scale)
        { t.localPosition = p; t.localRotation = q; t.localScale = scale; }
    }
}
