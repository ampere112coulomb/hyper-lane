using System;
using System.Numerics;

namespace HyperLane;

internal static class StarMapMotion
{
    internal const int Fragments=24;
    internal const float MaxLinkDistance=7;
    internal static int BuildChain(ReadOnlySpan<Vector3> points,int start,Span<int> nodes)
    {
        if(start<0||start>=points.Length||nodes.Length==0)return 0;
        nodes[0]=start;int count=1;
        while(count<nodes.Length){
            int next=-1;float closest=MaxLinkDistance*MaxLinkDistance;
            for(int candidate=0;candidate<points.Length;candidate++){
                bool used=false;for(int i=0;i<count;i++)if(nodes[i]==candidate){used=true;break;}
                if(used)continue;
                float distance=Vector3.DistanceSquared(points[nodes[count-1]],points[candidate]);
                if(distance<closest){closest=distance;next=candidate;}
            }
            if(next<0)break;nodes[count++]=next;
        }
        return count;
    }
    internal static double ChainProgress(double age,int hop)=>age<0?0:1;
    internal static double ChainFade(double age,int hop)
    {
        return age<0||age>=.8?0:Math.Pow(1-age/.8,1.3);
    }
    internal static Vector3 Fragment(int id,int side,double scrollDepth,double beat)
    {
        uint seed=StageDecorationMotion.Seed(id,7,side);
        double z=52-PositiveMod(scrollDepth+id*2.6,56);
        return new Vector3((float)(side*(3.6+(seed%1000)*.0035)),
            (float)(-.7+((seed>>10)%1000)*.0045+.13*Math.Sin(beat*.17+id)),(float)z);
    }
    internal static double PositiveMod(double value,double period)=>(value%period+period)%period;
    internal static double PulseFade(double age)=>age<0||age>=1.2?0:Math.Pow(1-age/1.2,2);
    internal static Vector3 ShotPoint(Vector3 start,Vector3 end,double t)
        =>Vector3.Lerp(start,end,(float)Math.Clamp(t,0,1));
    internal static double ShotVisibility(double x)
    {
        // Track edges are +/-1.15; keep a small invisible safety margin around them.
        double t=Math.Clamp((Math.Abs(x)-1.35)/1.05,0,1);
        return t*t*(3-2*t);
    }
    internal static Vector3 Orbit(Vector3 start,Vector3 end,double t,double beat)
    {
        var p=Vector3.Lerp(start,end,(float)t);
        p.Y+=(float)(Math.Sin(t*Math.PI)*(.35+.08*Math.Sin(beat*.4)));
        return p;
    }
}
