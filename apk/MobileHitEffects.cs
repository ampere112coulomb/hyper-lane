using HyperLane;
namespace HyperLane.Android;

// Shared Android/iOS effect geometry. One bounded batch, no bitmap allocation per note.
internal sealed class MobileHitEffects
{
    private sealed class Burst(int lane,int seed,char kind) { internal int Lane=lane,Seed=seed;internal char Kind=kind;internal double At=double.NaN,Depth; }
    private readonly List<Burst> bursts=new();
    private int seed;
    internal void Add(MobileNote note) {
        for(int lane=Math.Max(1,note.Lane);lane<note.Lane+note.LaneSpan&&lane<=64;lane++)bursts.Add(new(lane,++seed,note.Kind));
        if(bursts.Count>48)bursts.RemoveRange(0,bursts.Count-48);
    }
    internal void Append(List<float> output,MobileSession session,MobileProjection projection,double seconds,bool premultiply=false) {
        int lanes=session.Difficulty.Lanes;double width=2.3/lanes;
        double beat=MobileChart.BeatAtSeconds(session.Difficulty,session.Song.Chart.Bpm,seconds-session.Song.Chart.OffsetFor(session.Difficulty));
        double[] positions=new double[lanes];JudgementLineMotion.Evaluate(session.Difficulty.JudgementLineChanges,beat,positions);
        for(int index=bursts.Count-1;index>=0;index--){var b=bursts[index];
            if(b.Lane>lanes){bursts.RemoveAt(index);continue;}
            if(double.IsNaN(b.At)){b.At=seconds;b.Depth=projection.DepthAtScreenFraction(positions[b.Lane-1]/100);}
            long age=(long)Math.Round((seconds-b.At)*1000);
            if(age<0||age>=HitSparkleMotion.MaxLifetimeMs){bursts.RemoveAt(index);continue;}
            double x=1.15-(b.Lane-.5)*width;
            double r=224/255.0,g=245/255.0,bl=1;
            if(b.Kind is 'H' or 'W'){r=1;g=.93;bl=.65;}else if(b.Kind=='L'){r=.85;g=.7;bl=1;}
            double fade=HitSparkleMotion.Fade(age,HitSparkleMotion.ColumnLifetime(b.Seed));
            Quad(x-width*.48,x+width*.48,.071,.071+1.25+Math.Min(age,600)*.0007,105/255.0*fade,0);
            for(int ray=0;ray<HitSparkleMotion.RayCount;ray++){
                var p=HitSparkleMotion.Sample(age,ray+23,b.Seed);if(p.Alpha<=0)continue;
                double rx=x-width*.5+.006+HitSparkleMotion.RayFraction(ray,b.Seed)*(width-.012);
                double head=.08+p.Y*1.6*2/3,tail=Math.Max(.073,head-(.45+p.Length*4));
                Quad(rx-.012,rx+.012,tail,head,p.Alpha*.35,0);
                Quad(rx-.0035,rx+.0035,tail,head,p.Alpha*.86,0);
            }
            for(int i=0;i<HitSparkleMotion.ParticleCount;i++){
                var p=HitSparkleMotion.Sample(age,i,b.Seed);if(p.Alpha<=0)continue;
                double px=x+p.X*width*1.2,y=.09+p.Y,size=p.Length*(p.Sparkle?1.15:.8)/3;
                Vertex(px,y+size,p.Alpha);Vertex(px+size*.34,y,p.Alpha);Vertex(px,y-size,p.Alpha);
                Vertex(px,y+size,p.Alpha);Vertex(px,y-size,p.Alpha);Vertex(px-size*.34,y,p.Alpha);
            }
            void Quad(double left,double right,double bottom,double top,double a,double topAlpha){
                Vertex(left,top,topAlpha);Vertex(right,top,topAlpha);Vertex(right,bottom,a);
                Vertex(left,top,topAlpha);Vertex(right,bottom,a);Vertex(left,bottom,a);
            }
            void Vertex(double vx,double vy,double alpha){
                Span<float> clip=stackalloc float[4];projection.Clip(vx,vy,b.Depth,clip);
                foreach(float value in clip)output.Add(value);
                output.Add(0);output.Add(0);double multiplier=premultiply?alpha:1;
                output.Add((float)(r*multiplier));output.Add((float)(g*multiplier));output.Add((float)(bl*multiplier));output.Add((float)alpha);
            }
        }
    }
}
