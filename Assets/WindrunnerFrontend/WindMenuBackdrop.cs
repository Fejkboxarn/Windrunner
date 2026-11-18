using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Windrunner.Production;
namespace Windrunner.Frontend
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WindMenuBackdrop : MaskableGraphic
    {
        float last;
        void Update(){if(!WindrunnerUITheme.ReducedMotion&&Time.unscaledTime-last>.05f){last=Time.unscaledTime;SetVerticesDirty();}}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Rect r=rectTransform.rect;
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color col)
            {int i=vh.currentVertCount;vh.AddVert(a,col,Vector2.zero);vh.AddVert(b,col,Vector2.zero);vh.AddVert(c,col,Vector2.zero);vh.AddVert(d,col,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
            for(int layer=0;layer<4;layer++)
            {
                var col=Color.Lerp(WindrunnerUITheme.Current.panel,WindrunnerUITheme.Current.background,layer*.22f);
                for(int j=0;j<12;j++)
                {
                    float x=r.xMin+r.width*j/11f,x2=x+r.width/11f;
                    float y=r.yMin+r.height*(.25f+.14f*Mathf.Sin(j*1.7f+layer)+layer*.09f);
                    float y2=r.yMin+r.height*(.25f+.14f*Mathf.Sin((j+1)*1.7f+layer)+layer*.09f);
                    Quad(new Vector2(x,r.yMin),new Vector2(x,y),new Vector2(x2,y2),new Vector2(x2,r.yMin),col);
                }
            }
            float time=WindrunnerUITheme.ReducedMotion?0:Time.unscaledTime*.018f;
            for(int k=0;k<12;k++)
            {
                float x=r.xMin+Mathf.Repeat(k*.137f+time,1)*r.width,y=r.yMin+r.height*(.20f+k*.055f);
                Color col=WindrunnerUITheme.Current.accent;col.a=.08f;
                Quad(new Vector2(x,y),new Vector2(x+90,y+25),new Vector2(x+90,y+26),new Vector2(x,y+1),col);
            }
        }
    }
}
