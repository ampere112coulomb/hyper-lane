namespace HyperLane;

// Clip homogeneous ten-float vertices before triangulation. In particular,
// lanes can cross behind the camera, where negative W is unsafe for some GLES
// implementations. Interpolate UV/color with clip coordinates, not screen XY.
public static class MobileNearClip
{
    public static void AppendQuad(ReadOnlySpan<float> quad,List<float> target)
    {
        if(quad.Length!=40)throw new ArgumentException("Four ten-float vertices required",nameof(quad));
        Span<float> polygon=stackalloc float[120],clipped=stackalloc float[120];quad.CopyTo(polygon);int count=4;
        for(int plane=0;plane<6&&count>0;plane++)
        {
            int output=0,component=plane/2;double sign=plane%2==0?1:-1;
            for(int current=0;current<count;current++)
            {
            int previous=(current+count-1)%count;
            var a=polygon.Slice(previous*10,10);var b=polygon.Slice(current*10,10);
            double da=a[3]+sign*a[component],db=b[3]+sign*b[component];
            bool insideA=da>=0,insideB=db>=0;
            if(insideA!=insideB)
            {
                double t=da/(da-db);
                for(int j=0;j<10;j++)clipped[output*10+j]=(float)(a[j]+(b[j]-a[j])*t);
                clipped[output*10+component]=(float)(-sign*clipped[output*10+3]);output++;
            }
            if(insideB){b.CopyTo(clipped.Slice(output*10,10));output++;}
            }
            clipped.Slice(0,output*10).CopyTo(polygon);count=output;
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
