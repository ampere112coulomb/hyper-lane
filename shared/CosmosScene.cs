namespace HyperLane.Android;

// Anchors and local-neighbor choices are built once, never searched per frame.
internal static class CosmosScene
{
    internal readonly record struct Star(float X,float Y,float Radius,float Phase,float Speed);
    internal readonly record struct Link(int A,int B,float Period,float Phase,float Life);
    internal static readonly Star[] Stars=CreateStars();
    internal static readonly Link[] Links=CreateLinks();
    internal static float Random(int i,int salt){uint v=unchecked((uint)i*747796405u+(uint)salt*2891336453u+12345u);v=unchecked(((v>>((int)(v>>28)+4))^v)*277803737u);return ((v>>22)^v)%16777216/16777216f;}
    internal static float Smooth(float t)=>t*t*(3-2*t);
    private static Star[] CreateStars(){var stars=new Star[1440];for(int i=0;i<stars.Length;i++){float x=Random(i,1)*1920,y=Random(i,2)*1080;if(i>=1200)switch((i-1200)/60){case 0:y=Random(i,2)*75;break;case 1:y=1005+Random(i,2)*75;break;case 2:x=Random(i,1)*90;break;default:x=1830+Random(i,1)*90;break;}stars[i]=new(x,y,i%19==0?1.5f+Random(i,3)*.7f:.45f+Random(i,3)*.65f,Random(i,4)*6.28f,.3f+Random(i,5)*.7f);}return stars;}
    private static Link[] CreateLinks(){var links=new List<Link>();for(int i=0;i<1200;i+=3){int first=-1,second=-1;float a=float.MaxValue,b=float.MaxValue;for(int j=i+1;j<Stars.Length;j++){float dx=Stars[i].X-Stars[j].X,dy=Stars[i].Y-Stars[j].Y,d=dx*dx+dy*dy;if(d<900||d>25600)continue;float score=d*(.6f+Random(i+j,8));if(score<a){second=first;b=a;first=j;a=score;}else if(score<b){second=j;b=score;}}foreach(int peer in new[]{first,second})if(peer>=0){int n=links.Count;float period=20+Random(n,9)*24;links.Add(new(i,peer,period,Random(n,10)*period,3.5f+Random(n,11)*3));}}return links.ToArray();}
    internal static float LinkOpacity(Link link,double seconds,out float reveal){float age=(float)((Math.Max(0,seconds)+link.Phase)%link.Period);reveal=Smooth(Math.Clamp(age/.8f,0,1));return age>=link.Life?0:reveal*Smooth(Math.Clamp((link.Life-age)/1.8f,0,1));}
}
