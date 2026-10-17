using System;
namespace HyperLane;
internal static class HitSparkleMotion
{
    internal const int LifetimeMs=600;
    internal const int MaxLifetimeMs=780;
    internal static int ParticleLifetime(int index,int seed)=>420+(int)(Random(seed,index,5)*360);
    internal static int ColumnLifetime(int seed)=>420+(int)(Random(seed,0,6)*360);
    internal static int FlashLifetime(int seed)=>100+(int)(Random(seed,0,7)*110);
    internal static double Fade(long age,int duration)=>age<0?0:Math.Pow(Math.Max(0,1-age/(double)duration),2);
    internal const int ParticleCount=16;
    internal const int RayCount=12;
    internal static double RayFraction(int index,int seed)
    {
        double r=Random(seed,index+23,4);
        if(index==0)return r*.02;
        if(index==RayCount-1)return 1-r*.02;
        return (index+(r-.5)*.7)/(RayCount-1);
    }
    internal readonly record struct Particle(double X,double Y,double Length,double Alpha,bool Sparkle);
    private static double Random(int seed,int index,uint channel)
    {
        uint x=unchecked((uint)seed*0x9e3779b9u+(uint)index*0x85ebca6bu+channel*0xc2b2ae35u);
        x^=x>>16;x=unchecked(x*0x7feb352du);x^=x>>15;x=unchecked(x*0x846ca68bu);x^=x>>16;
        return (x>>8)/16777216.0;
    }
    internal static Particle Sample(long age,int index,int seed)
    {
        double random=Random(seed,index,0),speed=Random(seed,index,1),size=Random(seed,index,2),phase=Random(seed,index,3)*Math.PI*2;
        int duration=ParticleLifetime(index,seed);
        if(age<0||age>=duration)return default;
        double t=Math.Clamp(age/(double)LifetimeMs,0,1);
        double fade=Fade(age,duration);
        double spread=.5+.7*t;
        return new((random-.5)*spread,t*(.8+speed*1.1)*.5,.025+size*.055,
            fade*(.65+.35*Math.Pow(Math.Sin(t*31+phase),2)),Math.Sin(t*35+phase)>.65);
    }
}
