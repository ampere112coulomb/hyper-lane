using System.Runtime.InteropServices;
using CoreGraphics;
using GLKit;
using OpenGLES;
using HyperLane.Android;

namespace HyperLane.iOS;

// The atlas is uploaded once. Homogeneous vertices use the exact Android camera
// and GPU perspective interpolation (including camera rotations and near clipping).
internal sealed class IosLaneRenderer : GLKViewDelegate
{
    private readonly EAGLContext context = new(EAGLRenderingAPI.OpenGLES2);
    internal readonly GLKView View;
    internal MobileSession? Session;
    internal IosSettings Settings = new();
    internal double Seconds;
    internal IReadOnlySet<int> Held = new HashSet<int>();
    private readonly List<float> geometry = new(10000), sprites = new(10000);
    private readonly Dictionary<string,int[]> regions = new();
    private readonly Dictionary<int,double> released = new();
    private double[] depths = [];
    private uint program, atlas;
    internal IosLaneRenderer(CGRect frame, IosVisuals visuals)
    {
        View = new GLKView(frame, context) { Delegate = this, Opaque = false, BackgroundColor = UIKit.UIColor.Clear, UserInteractionEnabled = false };
        EAGLContext.SetCurrentContext(context);
        program = Gl.Program("attribute vec4 pos;attribute vec2 uv;attribute vec4 col;varying vec2 tex;varying vec4 tint;void main(){gl_Position=pos;tex=uv;tint=col;}",
            "precision mediump float;varying vec2 tex;varying vec4 tint;uniform sampler2D image;uniform float textured;void main(){gl_FragColor=tint*mix(vec4(1.0),texture2D(image,tex),textured);}");
        using var json=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Foundation.NSBundle.MainBundle.BundlePath,"skin/skin.json")));
        foreach(var p in json.RootElement.GetProperty("sprites").EnumerateObject())regions[p.Name]=p.Value.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        var image=visuals.Asset("skin/atlas.png")!.CGImage!;
        int w=(int)image.Width,h=(int)image.Height;
        byte[] pixels=new byte[w*h*4];
        using(var space=CGColorSpace.CreateDeviceRGB())
        using(var bitmap=new CGBitmapContext(pixels,w,h,8,w*4,space,CGImageAlphaInfo.PremultipliedLast))
            bitmap.DrawImage(new CGRect(0,0,w,h),image);
        Gl.glGenTextures(1,out atlas);Gl.glBindTexture(3553,atlas);
        Gl.glTexParameteri(3553,10241,9729);Gl.glTexParameteri(3553,10240,9729);Gl.glTexParameteri(3553,10242,33071);Gl.glTexParameteri(3553,10243,33071);
        Gl.Texture(w,h,pixels);atlasWidth=w;atlasHeight=h;
    }
    private readonly int atlasWidth,atlasHeight;
    public override void DrawInRect(GLKView view, CGRect rect)
    {
        Gl.glClearColor(0,0,0,0);Gl.glClear(16384);if(Session is not {} s)return;
        geometry.Clear();sprites.Clear();Build(s);
        Gl.glUseProgram(program);Gl.glEnable(3042);Gl.glBlendFunc(1,771);
        Gl.glBindTexture(3553,atlas);Gl.glUniform1i(Gl.glGetUniformLocation(program,"image"),0);
        Gl.Draw(program,geometry,false);Gl.Draw(program,sprites,true);
        if(Settings.LaneCover>0)
        {
            geometry.Clear();double beat=MobileChart.BeatAtSeconds(s.Difficulty,s.Song.Chart.Bpm,Seconds-s.Song.Chart.Offset);
            var p=MobileProjection.At(s.Difficulty,beat);double cover=p.DepthAtScreenFraction(1-Settings.LaneCover/100.0);
            Quad(p,-1.15,1.15,cover,52,.085,98,66,161,170/255.0);Quad(p,-1.15,1.15,cover,Math.Min(52,cover+.085),.088,171,135,225,205/255.0);
            Gl.Draw(program,geometry,false);
        }
    }
    private void Build(MobileSession s)
    {
        var diff=s.Difficulty;var chart=s.Song.Chart;double beat=MobileChart.BeatAtSeconds(diff,chart.Bpm,Seconds-chart.Offset);
        var p=MobileProjection.At(diff,beat);int lanes=diff.Lanes;double width=2.3/lanes,alpha=Visibility(diff.LaneVisibilityChanges,beat)*(1-Settings.LaneTransparency/100.0);
        if(depths.Length!=lanes)depths=new double[lanes];HyperLane.JudgementLineMotion.Evaluate(diff.JudgementLineChanges,beat,depths);
        for(int i=0;i<lanes;i++)depths[i]=p.DepthAtScreenFraction(depths[i]/100);
        for(int lane=1;lane<=lanes;lane++)Quad(p,1.15-lane*width,1.15-(lane-1)*width,-12,52,.004,lane%2==1?8:11,lane%2==1?20:25,lane%2==1?37:44,alpha);
        for(int edge=0;edge<=lanes;edge++){double x=-1.15+edge*width;bool outside=edge==0||edge==lanes;Quad(p,x-(outside?.012:.005),x+(outside?.012:.005),-12,52,.014,outside?85:37,outside?170:77,outside?215:110,alpha);}
        double scroll=MobileChart.ScrollPosition(diff,chart.Bpm,beat),speed=Settings.NoteSpeed*10;
        foreach(double pos in diff.MeasureScrollPositions){double z=(pos-scroll)*speed;if(z < -12)continue;if(z>45)break;Quad(p,-1.15,1.15,z-.012,z+.012,.022,90,170,230,.392*alpha);}
        for(int lane=1;lane<=lanes;lane++)
        {
            double left=1.15-lane*width,depth=depths[lane-1];if(Held.Contains(lane))released[lane]=Seconds;
            double glow=Held.Contains(lane)?1:released.TryGetValue(lane,out double release)?1-Smooth(Math.Clamp((Seconds-release)/.24,0,1)):0;
            double length=26*Settings.KeyBeamLength/50.0;
            if(glow>0&&length>0)for(int section=11;section>=0;section--){double t=(section+.5)/12;double a=130/255.0*(1-Settings.KeyBeamOpacity/100.0)*glow*(Math.Exp(-3.6*t)-Math.Exp(-3.6))/(1-Math.Exp(-3.6));Quad(p,left+.008,left+width-.008,depth+length*section/12,depth+length*(section+1)/12,.006,55,225,255,a);}
            Quad(p,left,left+width,depth-.028,depth+.028,.065,0,255,255,alpha);
        }
        double cover=Settings.LaneCover>0?p.DepthAtScreenFraction(1-Settings.LaneCover/100.0):52;
        foreach(var grouping in diff.NotesByLayer.OrderBy(x=>x.Key))
        {
            var timing=grouping.Key==1?diff:diff.Layers[grouping.Key];double layerBeat=MobileChart.BeatAtSeconds(timing,chart.Bpm,Seconds-chart.Offset);
            double root=MobileChart.ScrollPosition(timing,chart.Bpm,layerBeat),ns=MobileChart.NsMultiplierAt(timing,layerBeat);
            foreach(var note in grouping.Value)
            {
                if(note.Done||note.Lane>lanes||(long)note.Lane+note.LaneSpan-1<1)continue;
                int first=Math.Max(1,note.Lane),last=(int)Math.Min(lanes,(long)note.Lane+note.LaneSpan-1);double depth=depths[first-1];
                bool split=false;for(int l=first+1;l<=last;l++)if(Math.Abs(depths[l-1]-depth)>.0001)split=true;
                if(split)for(int l=first;l<=last;l++)Segment(l,1,depths[l-1]);else Segment(note.Lane,note.LaneSpan,depth);
                void Segment(int lane,int span,double offset)
                {
                    double z=(note.ScrollStart-root)*speed*ns*note.Ss+offset,end=(note.ScrollEnd-root)*speed*ns*note.Ss+offset;
                    bool atLine=note.Kind=='L'&&(note.Started||Seconds>=note.StartSeconds);if(atLine)z=offset;
                    double left=1.15-((double)lane+span-1)*width,right=1.15-((double)lane-1)*width;
                    if(note.Kind is 'L' or 'G')
                    {
                        double near=Math.Max(-12,Math.Min(z,end)),far=Math.Min(Math.Min(45,cover),Math.Max(z,end));if(atLine){if(note.Ss<0)far=Math.Min(far,offset);else near=Math.Max(near,offset);}
                        if(far>near)
                        {
                            if(note.Kind=='G')for(int part=0;part<16;part++){double a=near+(far-near)*part/16,b=near+(far-near)*(part+1)/16,t=Math.Abs(end-z)<1e-12?0:Math.Clamp(((a+b)/2-z)/(end-z),0,1);var rgb=Rgb(note.CustomColor??"#10b981");Quad(p,left+.018,right-.018,a,b,.014,rgb.R,rgb.G,rgb.B,note.GuideOpacity*(note.GuideOpacityMode=="fade"?1-t:note.GuideOpacityMode=="rise"?t:1));}
                            else Sprite(p,"long",left+.009,right-.009,near-.08,Math.Min(cover,far+.08),.034,true);
                        }
                        if(note.Kind=='G')return;
                        if(end>=-12&&end<=Math.Min(45,cover)&&(!note.Started||(note.Ss<0?end<=offset:end>=offset)))Head(note.HoldEnd?"holdEnd":"longEnd",end);
                        if(note.Started)return;
                    }
                    if(z < -12 || z>Math.Min(45,cover))return;
                    Head(note.Kind switch{'D'=>"damage",'L'=>"longStart",'H' or 'W'=>"hold",'M'=>"large",_=>note.Lane%2==0?"normalBlue":"normalWhite"},z);
                    void Head(string name,double at){double thickness=.22+.009*Math.Max(0,at);Sprite(p,name,left+.006,right-.006,at-thickness/2-.018,Math.Min(cover,at+thickness/2+.018),.052,name=="large",note.CustomColor);}
                }
            }
        }
    }
    internal static double Visibility(List<MobileVisibilityChange> changes,double beat)
    {
        double from=1,target=1,start=0,duration=0;foreach(var change in changes){if(change.Beat>beat)break;from+=(target-from)*(duration<=0?1:Math.Clamp((change.Beat-start)/duration,0,1));target=change.Opacity;start=change.Beat;duration=change.Duration;}
        return from+(target-from)*(duration<=0?1:Math.Clamp((beat-start)/duration,0,1));
    }
    private static double Smooth(double t)=>t*t*(3-2*t);
    private static (int R,int G,int B) Rgb(string? hex){try{int n=Convert.ToInt32(hex!.TrimStart('#'),16);return ((n>>16)&255,(n>>8)&255,n&255);}catch{return(255,255,255);}}
    private void Sprite(MobileProjection p,string name,double left,double right,double near,double far,double y,bool strip=false,string? tint=null)
    {
        if(far<=near)return;var rgb=Rgb(tint);string[] names=strip?[name+"Left",name+"Center",name+"Right"]:[name];double[] cuts=strip?[0,.18,.82,1]:[0,1];
        for(int i=0;i<names.Length;i++)if(regions.TryGetValue(names[i],out int[]? r))Quad(p,left+(right-left)*cuts[i],left+(right-left)*cuts[i+1],near,far,y,rgb.R,rgb.G,rgb.B,1,r);
    }
    private void Quad(MobileProjection p,double left,double right,double near,double far,double y,int r,int g,int b,double a,int[]? region=null)
    {
        if(far<=near||right<=left||a<=0)return;float[] v=new float[40];
        Put(0,left,far,0,0);Put(1,right,far,1,0);Put(2,right,near,1,1);Put(3,left,near,0,1);
        var target=region==null?geometry:sprites;foreach(int i in new[]{0,1,2,0,2,3})for(int j=0;j<10;j++)target.Add(v[i*10+j]);
        void Put(int index,double x,double z,int u,int t)
        {
            int at=index*10;p.Clip(x,y,z,v.AsSpan(at,4));
            v[at+4]=region==null?0:(region[0]+.5f+(region[2]-1)*u)/atlasWidth;
            v[at+5]=region==null?0:(region[1]+.5f+(region[3]-1)*t)/atlasHeight;
            v[at+6]=(float)(r/255.0*a);v[at+7]=(float)(g/255.0*a);v[at+8]=(float)(b/255.0*a);v[at+9]=(float)a;
        }
    }
    protected override void Dispose(bool disposing){if(disposing){EAGLContext.SetCurrentContext(context);if(atlas!=0)Gl.glDeleteTextures(1,ref atlas);if(program!=0)Gl.glDeleteProgram(program);View.Delegate=null;View.Dispose();context.Dispose();}base.Dispose(disposing);}
    private static class Gl
    {
        private const string Lib="/System/Library/Frameworks/OpenGLES.framework/OpenGLES";
        [DllImport(Lib)]internal static extern void glClearColor(float r,float g,float b,float a);
        [DllImport(Lib)]internal static extern void glClear(uint mask);
        [DllImport(Lib)]internal static extern void glEnable(uint cap);
        [DllImport(Lib)]internal static extern void glBlendFunc(uint s,uint d);
        [DllImport(Lib)]internal static extern uint glCreateShader(uint type);
        [DllImport(Lib)]private static extern void glShaderSource(uint shader,int count,string[] source,IntPtr lengths);
        [DllImport(Lib)]private static extern void glCompileShader(uint shader);
        [DllImport(Lib)]private static extern void glGetShaderiv(uint shader,uint name,out int value);
        [DllImport(Lib)]private static extern void glGetShaderInfoLog(uint shader,int size,out int length,System.Text.StringBuilder log);
        [DllImport(Lib)]private static extern uint glCreateProgram();
        [DllImport(Lib)]private static extern void glAttachShader(uint program,uint shader);
        [DllImport(Lib)]private static extern void glLinkProgram(uint program);
        [DllImport(Lib)]private static extern void glGetProgramiv(uint program,uint name,out int value);
        [DllImport(Lib)]private static extern void glDeleteShader(uint shader);
        [DllImport(Lib)]internal static extern void glUseProgram(uint program);
        [DllImport(Lib)]internal static extern void glDeleteProgram(uint program);
        [DllImport(Lib)]internal static extern void glGenTextures(int count,out uint texture);
        [DllImport(Lib)]internal static extern void glDeleteTextures(int count,ref uint texture);
        [DllImport(Lib)]internal static extern void glBindTexture(uint target,uint texture);
        [DllImport(Lib)]internal static extern void glTexParameteri(uint target,uint name,int value);
        [DllImport(Lib)]private static extern void glTexImage2D(uint target,int level,int format,int w,int h,int border,uint layout,uint type,IntPtr pixels);
        [DllImport(Lib)]internal static extern int glGetUniformLocation(uint p,string name);
        [DllImport(Lib)]internal static extern void glUniform1i(int location,int value);
        [DllImport(Lib)]private static extern void glUniform1f(int location,float value);
        [DllImport(Lib)]private static extern int glGetAttribLocation(uint p,string name);
        [DllImport(Lib)]private static extern void glEnableVertexAttribArray(uint index);
        [DllImport(Lib)]private static extern void glVertexAttribPointer(uint index,int size,uint type,byte normalized,int stride,IntPtr pointer);
        [DllImport(Lib)]private static extern void glDrawArrays(uint mode,int first,int count);
        internal static uint Program(string vertex,string fragment)
        {
            uint Compile(uint type,string text){uint s=glCreateShader(type);glShaderSource(s,1,[text],IntPtr.Zero);glCompileShader(s);glGetShaderiv(s,35713,out int ok);if(ok==0){var log=new System.Text.StringBuilder(2048);glGetShaderInfoLog(s,2048,out _,log);throw new InvalidOperationException(log.ToString());}return s;}
            uint v=Compile(35633,vertex),f=Compile(35632,fragment),p=glCreateProgram();glAttachShader(p,v);glAttachShader(p,f);glLinkProgram(p);glGetProgramiv(p,35714,out int linked);glDeleteShader(v);glDeleteShader(f);if(linked==0)throw new InvalidOperationException("iOS lane shader link failed");return p;
        }
        internal static void Texture(int w,int h,byte[] pixels){var pin=GCHandle.Alloc(pixels,GCHandleType.Pinned);try{glTexImage2D(3553,0,6408,w,h,0,6408,5121,pin.AddrOfPinnedObject());}finally{pin.Free();}}
        internal static void Draw(uint program,List<float> source,bool texture)
        {
            if(source.Count==0)return;float[] array=source.ToArray();var pin=GCHandle.Alloc(array,GCHandleType.Pinned);
            try{IntPtr address=pin.AddrOfPinnedObject();string[] names=["pos","uv","col"];int[] sizes=[4,2,4],offsets=[0,16,24];for(int i=0;i<3;i++){uint at=(uint)glGetAttribLocation(program,names[i]);glEnableVertexAttribArray(at);glVertexAttribPointer(at,sizes[i],5126,0,40,address+offsets[i]);}glUniform1f(glGetUniformLocation(program,"textured"),texture?1:0);glDrawArrays(4,0,array.Length/10);}finally{pin.Free();}
        }
    }
}
