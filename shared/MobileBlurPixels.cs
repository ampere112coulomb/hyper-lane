namespace HyperLane;

// Area reduction and Catmull-Rom expansion shared by both native platforms.
internal static class MobileBlurPixels
{
    internal static int[] Create(int[] pixels,int width,int height)=>Resize(Resize(pixels,width,height,120,68),120,68,1920,1080);
    private static int[] Resize(int[] source,int w,int h,int tw,int th)
    {
        var xs=Samples(w,tw);var ys=Samples(h,th);int[] horizontal=new int[tw*h],output=new int[tw*th];
        for(int y=0;y<h;y++)for(int x=0;x<tw;x++)horizontal[y*tw+x]=Sample(source,y*w,1,xs[x]);
        for(int y=0;y<th;y++)for(int x=0;x<tw;x++)output[y*tw+x]=Sample(horizontal,x,tw,ys[y]);return output;
    }
    private static (int Index,double Weight)[][] Samples(int source,int target)
    {
        var result=new (int,double)[target][];double scale=(double)source/target;
        for(int x=0;x<target;x++){var samples=new List<(int,double)>();if(scale>1){double left=x*scale,right=(x+1)*scale;for(int i=(int)Math.Floor(left);i<Math.Ceiling(right);i++)samples.Add((Math.Clamp(i,0,source-1),(Math.Min(right,i+1)-Math.Max(left,i))/scale));}
            else{double center=(x+.5)*scale-.5;int first=(int)Math.Floor(center);for(int i=first-1;i<=first+2;i++)samples.Add((Math.Clamp(i,0,source-1),Cubic(center-i)));}result[x]=samples.ToArray();}return result;
    }
    private static double Cubic(double d){double x=Math.Abs(d);return x<=1?(1.5*x-2.5)*x*x+1:x<2?((-.5*x+2.5)*x-4)*x+2:0;}
    private static int Sample(int[] source,int start,int stride,(int Index,double Weight)[] samples)
    {
        double r=0,g=0,b=0;foreach(var(index,weight)in samples){int pixel=source[start+index*stride];r+=((pixel>>16)&255)*weight;g+=((pixel>>8)&255)*weight;b+=(pixel&255)*weight;}
        int Channel(double v)=>Math.Clamp((int)Math.Round(v),0,255);return unchecked((int)0xff000000)|(Channel(r)<<16)|(Channel(g)<<8)|Channel(b);
    }
}
