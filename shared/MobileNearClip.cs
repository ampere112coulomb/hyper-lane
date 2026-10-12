namespace HyperLane;

// Clip homogeneous ten-float vertices before triangulation. In particular,
// lanes can cross behind the camera, where negative W is unsafe for some GLES
// implementations. Interpolate UV/color with clip coordinates, not screen XY.
public static class MobileNearClip
{
    public static void AppendQuad(ReadOnlySpan<float> quad,List<float> target)
    {
        if(quad.Length!=40)throw new ArgumentException("Four ten-float vertices required",nameof(quad));
        Span<float> polygon=stackalloc float[50];int count=0;
        for(int current=0;current<4;current++)
        {
            int previous=(current+3)%4;
            var a=quad.Slice(previous*10,10);var b=quad.Slice(current*10,10);
            double da=(double)a[2]+a[3],db=(double)b[2]+b[3];
            bool insideA=da>=0,insideB=db>=0;
            if(insideA!=insideB)
            {
                double t=da/(da-db);
                for(int j=0;j<10;j++)polygon[count*10+j]=(float)(a[j]+(b[j]-a[j])*t);
                // Round the near-plane coordinate toward the visible half-space.
                polygon[count*10+2]=-polygon[count*10+3];count++;
            }
            if(insideB){b.CopyTo(polygon.Slice(count*10,10));count++;}
        }
        for(int i=1;i<count-1;i++)
        {
            for(int corner=0;corner<3;corner++)
            {
                int index=corner==0?0:corner==1?i:i+1;
                for(int j=0;j<10;j++)target.Add(polygon[index*10+j]);
            }
        }
    }
}
