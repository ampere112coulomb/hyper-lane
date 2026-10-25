using System.Numerics;
using HyperLane;
namespace HyperLane.Android;

internal sealed class MobileStageMap
{
    private readonly record struct Pulse(int Side,double At,int[] Nodes);
    private readonly List<Pulse> pulses=new(64);
    internal bool Enabled=true;
    internal void Register(MobileSession session,MobileNote note,double seconds)
    {
        if(!Enabled||note.Kind is 'D' or 'G')return;
        double beat=MobileChart.BeatAtSeconds(session.Difficulty,session.Song.Chart.Bpm,seconds-session.Song.Chart.OffsetFor(session.Difficulty));
        double depth=MobileChart.ScrollPosition(session.Difficulty,session.Song.Chart.Bpm,beat)*10*Speed*Math.Max(MobileChart.NsMultiplierAt(session.Difficulty,beat),.1);
        if(!double.IsFinite(depth))return;
        pulses.RemoveAll(p=>seconds<p.At||seconds-p.At>=1.2);if(pulses.Count>=64)pulses.RemoveAt(0);
        int side=note.Lane<=session.Difficulty.Lanes/2?1:-1,id=(int)((uint)(session.Difficulty.Notes.IndexOf(note)*7+note.Lane*3)%24);
        Span<Vector3> points=stackalloc Vector3[24];for(int i=0;i<24;i++)points[i]=StarMapMotion.Fragment(i,side,depth,beat);
        Span<int> chain=stackalloc int[5];int count=StarMapMotion.BuildChain(points,id,chain);if(count>1)pulses.Add(new(side,seconds,chain[..count].ToArray()));
    }
    internal double Speed=1.6;
    internal void Append(List<float> output,MobileSession session,MobileProjection projection,double seconds,double speed,bool premultiply=false)
    {
        Speed=speed;double beat=MobileChart.BeatAtSeconds(session.Difficulty,session.Song.Chart.Bpm,seconds-session.Song.Chart.OffsetFor(session.Difficulty));
        double depth=MobileChart.ScrollPosition(session.Difficulty,session.Song.Chart.Bpm,beat)*speed*10*Math.Max(MobileChart.NsMultiplierAt(session.Difficulty,beat),.1);
        if(!double.IsFinite(depth))return;
        for(int side=-1;side<=1;side+=2)for(int id=0;id<24;id++){
            var p=StarMapMotion.Fragment(id,side,depth,beat);double fade=Math.Clamp((p.Z+4)/5,0,1)*Math.Clamp((56-p.Z)/8,0,1);float angle=(float)(beat*.12+id*1.7),radius=.09f+id%4*.025f;
            var a=p+new Vector3(MathF.Cos(angle)*radius,MathF.Sin(angle)*radius,0);var b=p+new Vector3(MathF.Cos(angle+2.1f)*radius,MathF.Sin(angle+2.1f)*radius,.08f);var c=p+new Vector3(MathF.Cos(angle+4.2f)*radius,MathF.Sin(angle+4.2f)*radius,-.08f);
            int r=id%3==0?165:91,g=id%3==0?122:184;
            MobileWorldGeometry.Triangle(output,projection,a,b,c,r,g,248,32/255.0*fade,premultiply);
            MobileWorldGeometry.Line(output,projection,a,b,r,g,248,.4*fade,.012f,premultiply);MobileWorldGeometry.Line(output,projection,b,c,r,g,248,.4*fade,.012f,premultiply);MobileWorldGeometry.Line(output,projection,c,a,r,g,248,.4*fade,.012f,premultiply);
        }
        pulses.RemoveAll(p=>seconds<p.At||seconds-p.At>=1.2);
        foreach(var pulse in pulses)for(int hop=0;hop<pulse.Nodes.Length-1;hop++){
            var a=StarMapMotion.Fragment(pulse.Nodes[hop],pulse.Side,depth,beat);var b=StarMapMotion.Fragment(pulse.Nodes[hop+1],pulse.Side,depth,beat);if(Vector3.DistanceSquared(a,b)>49)break;
            double fade=StarMapMotion.ChainFade(seconds-pulse.At,hop);if(fade<=0)continue;
            MobileWorldGeometry.Line(output,projection,a,b,120,202,255,.176*fade,.065f,premultiply);MobileWorldGeometry.Line(output,projection,a,b,210,245,255,.94*fade,.026f,premultiply);
        }
        foreach(var n in session.ActiveLongs){if(n.Done)continue;int side=n.Lane<=session.Difficulty.Lanes/2?1:-1,id=session.Difficulty.Notes.IndexOf(n)%24;var a=new Vector3(side*3.4f,.1f,-2);var end=StarMapMotion.Fragment(id,side,depth,beat);double growth=Math.Clamp((beat-n.Beat)/4+.08,0,1);for(int i=0;i<24;i++){var p=StarMapMotion.Orbit(a,end,i/24d*growth,beat);var q=StarMapMotion.Orbit(a,end,(i+1)/24d*growth,beat);MobileWorldGeometry.Line(output,projection,p,q,145,211,255,.35,.018f,premultiply);}}
    }
}
internal static class MobileWorldGeometry
{
    internal static void Triangle(List<float> output,MobileProjection p,Vector3 a,Vector3 b,Vector3 c,int r,int g,int bColor,double alpha,bool premultiply){Put(a);Put(b);Put(c);void Put(Vector3 v){Span<float> clip=stackalloc float[4];p.Clip(v.X,v.Y,v.Z,clip);foreach(float f in clip)output.Add(f);output.Add(0);output.Add(0);double m=premultiply?alpha:1;output.Add((float)(r/255d*m));output.Add((float)(g/255d*m));output.Add((float)(bColor/255d*m));output.Add((float)alpha);}}
    internal static void Line(List<float> output,MobileProjection p,Vector3 a,Vector3 b,int r,int g,int blue,double alpha,float width,bool premultiply){var n=Vector3.Cross(b-a,Vector3.UnitY);if(n.LengthSquared()<.000001f)n=Vector3.Cross(b-a,Vector3.UnitZ);if(n.LengthSquared()<.000001f)return;n=Vector3.Normalize(n)*width/2;Triangle(output,p,a-n,b-n,b+n,r,g,blue,alpha,premultiply);Triangle(output,p,a-n,b+n,a+n,r,g,blue,alpha,premultiply);}
}
