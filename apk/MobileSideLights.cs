namespace HyperLane.Android;

internal static class MobileSideLights
{
    private static readonly int[] Triangles=[0,1,2,0,2,3];
    internal readonly record struct Strip(double X,double Width,double Near,double Far,double Alpha);
    internal static IEnumerable<Strip> Segments(double scroll,double noteSpeed,double ns)
    {
        double scale=noteSpeed*10*Math.Max(ns,.1);
        if(!double.IsFinite(scroll)||!double.IsFinite(scale)||scale<=0)yield break;
        double spacing=4.8/Math.Max(noteSpeed*10,.01);
        long first=(long)Math.Floor((scroll-12/scale)/spacing);
        int count=Math.Min(160,(int)Math.Ceiling(64/scale/spacing)+2);
        for(int side=-1;side<=1;side+=2)for(int i=0;i<count;i++){
            long index=first+i;uint seed=unchecked((uint)index*747796405u+(side<0?2891336453u:12582917u));
            seed=unchecked((seed^(seed>>16))*2246822519u);seed^=seed>>13;
            double z=(index*spacing-scroll)*scale,near=Math.Max(-11,z),far=Math.Min(51,z+3*(.65+seed%1000/1000.0*1.7));
            if(far<=near)continue;
            double fade=Math.Clamp((z+11)/5,0,1)*Math.Clamp((51-z)/10,0,1);
            yield return new(side*(1.65+((seed>>10)%1000)/1000.0*3.2),.006+seed%7*.001,near,far,fade*(48+(seed>>22)%36)/255.0);
        }
    }
    internal static void Append(List<float> output,MobileProjection projection,double scroll,double speed,double ns,bool premultiply=false)
    {
        var vertices=new float[40];
        foreach(var strip in Segments(scroll,speed,ns)){
            Quad(strip.Width*3,strip.Alpha/5,70,170,255);Quad(strip.Width,strip.Alpha,160,225,255);
            void Quad(double half,double alpha,int r,int g,int b){
                Put(0,strip.X-half,strip.Far,0);Put(1,strip.X+half,strip.Far,0);
                Put(2,strip.X+half,strip.Near,alpha);Put(3,strip.X-half,strip.Near,alpha);
                foreach(int vertex in Triangles)for(int component=0;component<10;component++)output.Add(vertices[vertex*10+component]);
                void Put(int at,double x,double z,double a){at*=10;projection.Clip(x,0,z,vertices.AsSpan(at,4));vertices[at+4]=vertices[at+5]=0;double m=premultiply?a:1;vertices[at+6]=(float)(r/255.0*m);vertices[at+7]=(float)(g/255.0*m);vertices[at+8]=(float)(b/255.0*m);vertices[at+9]=(float)a;}
            }
        }
    }
}
