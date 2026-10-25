using System;
using System.Numerics;

namespace HyperLane;

internal static class StageDecorationMotion
{
    internal static uint Seed(long index,int row,int side)
    {
        uint seed=unchecked((uint)index*747796405u+(uint)row*2891336453u+(side<0?12582917u:98764321u));
        seed=(seed^(seed>>16))*2246822519u;return seed^(seed>>13);
    }
    internal static Vector3 WallPoint(long index,int column,int side,double depth)
    {
        uint seed=Seed(index,column,side);
        // Upright relief extending downwards, rendered by the actual lane camera.
        double vertical=column switch {0=>6,1=>2.5,2=>-1.5,3=>-6,_=>-14};
        if(column is >0 and <4)vertical+=(((seed>>10)%1000)/1000.0-.5)*1.4;
        // Broad ridges plus sharp local relief; peaks never cross the existing inner limit.
        double ridge=(Seed(index/3,0,side)%1000)/1000.0;
        double relief=(seed%1000)/1000.0;
        double x=3.4+column*.03+ridge*.9+relief*2.1;
        return new((float)(side*x),(float)vertical,(float)depth);
    }
    internal static int TriangleAlpha(long index,int column,int side,int triangle)
        => (int)(Seed(index,column*2+triangle+31,side)%101)*255/100;
    internal static double Fade(double depth)=>Math.Clamp((62-depth)/16,0,1);
}
