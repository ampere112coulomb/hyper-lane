using System.Numerics;
namespace HyperLane.Android;
internal sealed class MobileChordLinks
{
    private readonly List<MobileNote[]> groups=new();
    private readonly Dictionary<int,(double Scroll,double Ns)> layerValues=new();
    internal MobileChordLinks(MobileDifficulty diff){var notes=diff.Notes.Where(n=>n.Kind is not ('D' or 'G')).OrderBy(n=>n.StartSeconds).ToArray();for(int i=0;i<notes.Length;){int j=i+1;while(j<notes.Length&&Math.Abs(notes[j].StartSeconds-notes[i].StartSeconds)<=.000001)j++;if(j-i>1)groups.Add(notes[i..j].OrderBy(n=>n.Lane).ToArray());i=j;}}
    internal void Append(List<float> output,MobileSession s,MobileProjection p,double seconds,double speed,double[] depths,bool premultiply=false)
    {
        layerValues.Clear();foreach(int layer in s.Difficulty.NotesByLayer.Keys){var timing=layer==1?s.Difficulty:s.Difficulty.Layers[layer];double beat=MobileChart.BeatAtSeconds(timing,s.Song.Chart.Bpm,seconds-s.Song.Chart.OffsetFor(s.Difficulty));layerValues[layer]=(MobileChart.ScrollPosition(timing,s.Song.Chart.Bpm,beat),MobileChart.NsMultiplierAt(timing,beat));}
        foreach(var group in groups){MobileNote? prior=null;foreach(var n in group){if(n.Done||n.Kind=='L'&&n.Started||n.Lane>s.Difficulty.Lanes||n.Lane+n.LaneSpan<=1)continue;if(prior is {} a&&n.Lane>a.Lane+a.LaneSpan-1){int first=Math.Clamp(a.Lane+a.LaneSpan-1,1,s.Difficulty.Lanes),last=Math.Clamp(n.Lane,1,s.Difficulty.Lanes);double x1=1.15-(first-.5)*2.3/s.Difficulty.Lanes,x2=1.15-(last-.5)*2.3/s.Difficulty.Lanes,z1=Depth(a,first),z2=Depth(n,last);if(Clip(ref x1,ref z1,ref x2,ref z2))MobileWorldGeometry.Line(output,p,new((float)x1,.047f,(float)z1),new((float)x2,.047f,(float)z2),185,225,255,.55,.016f,premultiply);}if(prior==null||n.Lane+n.LaneSpan>prior.Lane+prior.LaneSpan)prior=n;}}
        double Depth(MobileNote n,int lane){var values=layerValues[n.Layer];return (n.ScrollStart-values.Scroll)*speed*10*values.Ns*n.Ss+depths[lane-1];}
    }
    private static bool Clip(ref double x1,ref double z1,ref double x2,ref double z2){double dz=z2-z1,dx=x2-x1;if(!double.IsFinite(dz)||!double.IsFinite(dx))return false;if(Math.Abs(dz)<1e-10)return z1>=-12&&z1<=45;double a=(-12-z1)/dz,b=(45-z1)/dz;if(a>b)(a,b)=(b,a);double from=Math.Max(0,a),to=Math.Min(1,b);if(from>to)return false;double x=x1,z=z1;x1=x+dx*from;z1=z+dz*from;x2=x+dx*to;z2=z+dz*to;return true;}
}
