using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.ThreeD.PowerUps
{
    // Replaceable native fallback icons; names and quantities stay real text.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FloodPowerUpIcon3D : MaskableGraphic
    {
        public FloodPowerUpKind kind;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=GetPixelAdjustedRect();float size=Mathf.Min(rect.width,rect.height);Vector2 center=rect.center;
            void Triangle(Vector2 a,Vector2 b,Vector2 c)
            {int start=vh.currentVertCount;vh.AddVert(center+a*size,color,Vector2.zero);vh.AddVert(center+b*size,color,Vector2.zero);vh.AddVert(center+c*size,color,Vector2.zero);vh.AddTriangle(start,start+1,start+2);}
            void Disc(Vector2 p,float radius)
            {for(int i=0;i<24;i++){float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;Triangle(p,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius);}}
            void Box(float x,float y,float w,float h)
            {var a=new Vector2(x-w/2,y-h/2);var b=new Vector2(x+w/2,y-h/2);var c=new Vector2(x+w/2,y+h/2);var d=new Vector2(x-w/2,y+h/2);Triangle(a,b,c);Triangle(a,c,d);}
            if(kind==FloodPowerUpKind.ColorSwap){Disc(new Vector2(0,-.12f),.25f);Triangle(new Vector2(-.22f,0),new Vector2(0,.45f),new Vector2(.22f,0));}
            else if(kind==FloodPowerUpKind.SmartHint){Disc(new Vector2(0,.13f),.27f);Box(0,-.18f,.22f,.12f);Box(0,-.34f,.18f,.07f);}
            else{Triangle(new Vector2(.08f,.45f),new Vector2(-.30f,-.06f),new Vector2(.04f,-.06f));Triangle(new Vector2(-.05f,-.45f),new Vector2(.30f,.07f),new Vector2(-.04f,.07f));}
        }
    }
}
