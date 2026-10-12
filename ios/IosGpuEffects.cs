using CoreGraphics;
using CoreVideo;
using HyperLane.Android;
using System.Runtime.InteropServices;

namespace HyperLane.iOS;

// Post-process the scene on the GPU, retaining the original perspective/depth
// lane renderer. No readback of the 3D framebuffer is needed.
internal sealed partial class IosLaneRenderer
{
    internal MobileFilterChange? ActiveFilter;
    internal CGImage? SceneBackground;
    internal CVPixelBuffer? SceneVideo;
    internal double BackgroundGain=1;
    internal byte[]? SceneOverlay;
    internal byte[]? SceneUnderlay;
    internal int OverlayWidth=1920,OverlayHeight=1080;
    private uint effectProgram,sceneTexture,effectFramebuffer,effectDepth,backgroundTexture,overlayTexture,underlayTexture,bloomOne,bloomTwo;
    private uint screenFramebuffer;
    private int sceneWidth,sceneHeight;
    private CGImage? uploadedBackground;
    private bool hasVideo;
    private bool reportedEffect;
    private byte[] videoPixels=[];
    private readonly Dictionary<uint,(int Width,int Height,uint Format)> uploadSizes=new();
    private bool Effects=>ActiveFilter is {Kind:not "BRIGHTNESS"};
    private void BeginEffects(int width,int height)
    {
        if(!Effects)return;
        Gl.glGetIntegerv(36006,out int bound);screenFramebuffer=(uint)bound;
        if(effectProgram==0){effectProgram=Gl.Program("attribute vec4 pos;attribute vec2 uv;attribute vec4 col;varying vec2 tex;void main(){gl_Position=pos;tex=uv;}",EffectFragment);Gl.glGenFramebuffers(1,out effectFramebuffer);Gl.glGenRenderbuffers(1,out effectDepth);}
        if(sceneWidth!=width||sceneHeight!=height){sceneWidth=width;sceneHeight=height;Allocate(ref sceneTexture,width,height);if(filteredBackground!=0)Allocate(ref filteredBackground,width,height);Allocate(ref bloomOne,Math.Max(1,width/4),Math.Max(1,height/4));Allocate(ref bloomTwo,Math.Max(1,width/4),Math.Max(1,height/4));Gl.glBindRenderbuffer(36161,effectDepth);Gl.glRenderbufferStorage(36161,33189,width,height);}
        if(SceneVideo is {} video)
        {
            video.Lock(CVPixelBufferLock.ReadOnly);
            try{int w=(int)video.Width,h=(int)video.Height,stride=(int)video.BytesPerRow;if(videoPixels.Length!=w*h*4)videoPixels=new byte[w*h*4];for(int y=0;y<h;y++)Marshal.Copy(video.BaseAddress+y*stride,videoPixels,y*w*4,w*4);Upload(ref backgroundTexture,w,h,videoPixels,32993);hasVideo=true;}
            finally{video.Unlock(CVPixelBufferLock.ReadOnly);}
        }
        else if(hasVideo||!ReferenceEquals(uploadedBackground,SceneBackground))
        {
            uploadedBackground=SceneBackground;hasVideo=false;
            if(SceneBackground is {} image){int w=(int)image.Width,h=(int)image.Height;byte[] pixels=new byte[w*h*4];using var space=CGColorSpace.CreateDeviceRGB();using var bitmap=new CGBitmapContext(pixels,w,h,8,w*4,space,CGImageAlphaInfo.PremultipliedLast);bitmap.DrawImage(new CGRect(0,0,w,h),image);Upload(ref backgroundTexture,w,h,pixels);}
        }
        Target(sceneTexture,width,height,true);Gl.glClearColor(0,0,0,1);Gl.glClear(16384|256);Gl.glDisable(2929);Gl.glDisable(3042);
        // Uploaded bitmap and decoded movie buffers have opposite row origins.
        Fullscreen(backgroundTexture,-1,1,hasVideo,0,0,(float)BackgroundGain,true);
        if(ActiveFilter!.Value.BackgroundOnly)FilterBackground();
        if(SceneUnderlay is {} underlay){Upload(ref underlayTexture,OverlayWidth,OverlayHeight,underlay);Gl.glEnable(3042);Gl.glBlendFunc(1,771);Fullscreen(underlayTexture,-1,1,true);}
    }
    // Background-only filtering uses a separate full-size texture to avoid sampling
    // a texture while it is attached as the current draw destination.
    private uint filteredBackground;
    private void FilterBackground()
    {
        // Retain the filtered image before restoring the scene draw attachment.
        if(filteredBackground==0)Allocate(ref filteredBackground,sceneWidth,sceneHeight);
        Filter(sceneTexture,filteredBackground,ActiveFilter!.Value);
        Target(sceneTexture,sceneWidth,sceneHeight,true);Fullscreen(filteredBackground,-1,1,false);
    }
    private void EndEffects()
    {
        if(!Effects)return;
        Gl.glDisable(2929);
        if(ActiveFilter!.Value.BackgroundOnly){Target(0,sceneWidth,sceneHeight,false);Gl.glDisable(3042);Fullscreen(sceneTexture,-1,1,false);}
        else
        {
            if(SceneOverlay is {} pixels){Upload(ref overlayTexture,OverlayWidth,OverlayHeight,pixels);Target(sceneTexture,sceneWidth,sceneHeight,true);Gl.glEnable(3042);Gl.glBlendFunc(1,771);Fullscreen(overlayTexture,-1,1,true);}
            Filter(sceneTexture,0,ActiveFilter!.Value);
        }
        if(!reportedEffect&&Environment.GetEnvironmentVariable("HL_VISUAL_QA")!=null){reportedEffect=true;Console.Error.WriteLine($"HL effect: {ActiveFilter.Value.Kind} strength={ActiveFilter.Value.Strength} background={ActiveFilter.Value.BackgroundOnly} size={sceneWidth}x{sceneHeight} texel={Gl.Uniform(effectProgram,"texel")} mode={Gl.Uniform(effectProgram,"mode")} intensity={Gl.Uniform(effectProgram,"intensity")} time={Gl.Uniform(effectProgram,"time")} error={Gl.glGetError()} center={Gl.Sample(sceneWidth/2,sceneHeight*430/1080)}");}
    }
    private void Filter(uint source,uint destination,MobileFilterChange filter)
    {
        Gl.glDisable(2929);Gl.glDisable(3042);float strength=(float)Math.Clamp(filter.Strength/100,0,1);
        if(filter.Kind=="BLOOM")
        {
            int w=Math.Max(1,sceneWidth/4),h=Math.Max(1,sceneHeight/4);
            Target(bloomOne,w,h,false);Fullscreen(source,2,1,false);
            Target(bloomTwo,w,h,false);Fullscreen(bloomOne,3,1,false,1f/w,1f/h);
            Target(bloomOne,w,h,false);Fullscreen(bloomTwo,4,1,false,1f/w,1f/h);
            Target(destination,sceneWidth,sceneHeight,false);Fullscreen(source,-1,1,false);Gl.glEnable(3042);Gl.glBlendFunc(1,1);Fullscreen(bloomOne,5,strength,false);Gl.glDisable(3042);
        }
        else{Target(destination,sceneWidth,sceneHeight,false);Fullscreen(source,filter.Kind switch{"MONO"=>0,"INVERT"=>1,"MOSAIC"=>6,"SCANLINE"=>7,"GLITCH"=>8,_=>-1},strength,false);}
    }
    private void Target(uint texture,int width,int height,bool depth)
    {
        Gl.glBindFramebuffer(36160,texture==0?screenFramebuffer:effectFramebuffer);
        if(texture!=0){Gl.glFramebufferTexture2D(36160,36064,3553,texture,0);Gl.glFramebufferRenderbuffer(36160,36096,36161,depth?effectDepth:0);if(Gl.glCheckFramebufferStatus(36160)!=36053)throw new InvalidOperationException("Incomplete iOS effect framebuffer");}
        Gl.glViewport(0,0,width,height);
    }
    private void Fullscreen(uint texture,int mode,float intensity,bool flip,float dx=0,float dy=0,float gain=1,bool crop=false)
    {
        Gl.glUseProgram(effectProgram);Gl.glActiveTexture(33984);Gl.glBindTexture(3553,texture);
        Gl.glUniform1i(Gl.glGetUniformLocation(effectProgram,"frameTex"),0);Gl.glUniform1i(Gl.glGetUniformLocation(effectProgram,"mode"),mode);
        Gl.glUniform1f(Gl.glGetUniformLocation(effectProgram,"intensity"),intensity);Gl.glUniform1f(Gl.glGetUniformLocation(effectProgram,"flip"),flip?1:0);Gl.glUniform1f(Gl.glGetUniformLocation(effectProgram,"gain"),gain);Gl.glUniform1f(Gl.glGetUniformLocation(effectProgram,"time"),(float)Seconds);
        Gl.glUniform2f(Gl.glGetUniformLocation(effectProgram,"texel"),dx==0?1f/sceneWidth:dx,dy==0?1f/sceneHeight:dy);
        double u0=0,u1=1,v0=0,v1=1;if(crop){double ratio=hasVideo&&SceneVideo!=null?(double)SceneVideo.Width/SceneVideo.Height:SceneBackground==null?16.0/9:(double)SceneBackground.Width/SceneBackground.Height;double screen=(double)sceneWidth/sceneHeight;if(ratio>screen){u0=(1-screen/ratio)/2;u1=1-u0;}else{v0=(1-ratio/screen)/2;v1=1-v0;}}
        var vertices=new List<float>(60);foreach(var vertex in new[]{(-1f,-1f,u0,v0),(1f,-1f,u1,v0),(1f,1f,u1,v1),(-1f,-1f,u0,v0),(1f,1f,u1,v1),(-1f,1f,u0,v1)})vertices.AddRange([vertex.Item1,vertex.Item2,0,1,(float)vertex.Item3,(float)vertex.Item4,1,1,1,1]);
        Gl.Draw(effectProgram,vertexBuffer,vertices,true);
    }
    private static void Allocate(ref uint texture,int w,int h){if(texture==0)Gl.glGenTextures(1,out texture);Gl.glBindTexture(3553,texture);Gl.glTexParameteri(3553,10241,9729);Gl.glTexParameteri(3553,10240,9729);Gl.glTexParameteri(3553,10242,33071);Gl.glTexParameteri(3553,10243,33071);Gl.glTexImage2D(3553,0,6408,w,h,0,6408,5121,IntPtr.Zero);}
    private void Upload(ref uint texture,int w,int h,byte[] pixels,uint format=6408){if(texture==0)Gl.glGenTextures(1,out texture);Gl.glBindTexture(3553,texture);Gl.glTexParameteri(3553,10241,9729);Gl.glTexParameteri(3553,10240,9729);Gl.glTexParameteri(3553,10242,33071);Gl.glTexParameteri(3553,10243,33071);var pin=GCHandle.Alloc(pixels,GCHandleType.Pinned);try{if(uploadSizes.TryGetValue(texture,out var size)&&size==(w,h,format))Gl.glTexSubImage2D(3553,0,0,0,w,h,format,5121,pin.AddrOfPinnedObject());else{Gl.glTexImage2D(3553,0,6408,w,h,0,format,5121,pin.AddrOfPinnedObject());uploadSizes[texture]=(w,h,format);}}finally{pin.Free();}}
    private void DisposeEffects(){foreach(uint value in new[]{sceneTexture,bloomOne,bloomTwo,filteredBackground,backgroundTexture,overlayTexture,underlayTexture})if(value!=0){uint t=value;Gl.glDeleteTextures(1,ref t);}if(effectProgram!=0)Gl.glDeleteProgram(effectProgram);if(effectFramebuffer!=0)Gl.glDeleteFramebuffers(1,ref effectFramebuffer);if(effectDepth!=0)Gl.glDeleteRenderbuffers(1,ref effectDepth);}
    private const string EffectFragment="""
      precision highp float;uniform sampler2D frameTex;uniform int mode;uniform float intensity,flip,time,gain;uniform vec2 texel;varying vec2 tex;
      float rnd(float n){return fract(sin(n*127.1)*43758.5453);}
      vec4 at(vec2 p){return texture2D(frameTex,clamp(p,vec2(0.),vec2(1.)));}
      void main(){vec2 p=vec2(tex.x,flip>.5?1.-tex.y:tex.y);vec4 original=at(p);vec3 c=original.rgb;
      if(mode==0)c=mix(c,vec3(dot(c,vec3(.299,.587,.114))),intensity);
      else if(mode==1)c=mix(c,1.-c,intensity);
      else if(mode==2)c*=smoothstep(.48,.82,max(max(c.r,c.g),c.b));
      else if(mode==3||mode==4){vec2 d=mode==3?vec2(texel.x,0.):vec2(0.,texel.y);c*=.18;c+=(at(p+d).rgb+at(p-d).rgb)*.15;c+=(at(p+d*2.).rgb+at(p-d*2.).rgb)*.12;c+=(at(p+d*3.).rgb+at(p-d*3.).rgb)*.09;c+=(at(p+d*4.).rgb+at(p-d*4.).rgb)*.05;}
      else if(mode==5)c*=intensity*2.5;
      else if(mode==6){vec2 s=texel*(1.+intensity*79.);c=at((floor(p/s)+.5)*s).rgb;}
      else if(mode==7){float y=(1.-p.y)/texel.y;if(mod(floor(y),4.)<2.)c=mix(c,vec3(0.,8./255.,18./255.),intensity*210./255.);}
      else if(mode==8){float tick=floor(time*30.),band=floor(p.y*(32.+intensity*280.)),r=rnd(band+tick*917.);float shift=(rnd(band*2.+tick*43.)-.5)*intensity*.8*step(r,.2+intensity*.8);vec2 q=vec2(fract(p.x+shift),p.y);float rgb=intensity*.018;c=vec3(at(q+vec2(rgb,0.)).r,at(q).g,at(q-vec2(rgb,0.)).b);c=mix(c,c.brg,step(.7,r)*intensity);c=mix(c,vec3(rnd(band+tick),rnd(band+2.+tick),rnd(band+4.+tick)),step(.88,r)*intensity);}
      gl_FragColor=vec4(c*gain,original.a);}
      """;
    private static partial class Gl
    {
        [DllImport(Lib)]internal static extern void glGetIntegerv(uint name,out int value);
        [DllImport(Lib)]internal static extern void glGenFramebuffers(int n,out uint value);
        [DllImport(Lib)]internal static extern void glDeleteFramebuffers(int n,ref uint value);
        [DllImport(Lib)]internal static extern void glBindFramebuffer(uint target,uint value);
        [DllImport(Lib)]internal static extern void glFramebufferTexture2D(uint target,uint attachment,uint textureTarget,uint texture,int level);
        [DllImport(Lib)]internal static extern uint glCheckFramebufferStatus(uint target);
        [DllImport(Lib)]internal static extern void glGenRenderbuffers(int n,out uint value);
        [DllImport(Lib)]internal static extern void glDeleteRenderbuffers(int n,ref uint value);
        [DllImport(Lib)]internal static extern void glBindRenderbuffer(uint target,uint value);
        [DllImport(Lib)]internal static extern void glRenderbufferStorage(uint target,uint format,int w,int h);
        [DllImport(Lib)]internal static extern void glFramebufferRenderbuffer(uint target,uint attachment,uint renderbufferTarget,uint buffer);
        [DllImport(Lib)]internal static extern void glUniform2f(int location,float x,float y);
        [DllImport(Lib)]internal static extern void glTexSubImage2D(uint target,int level,int x,int y,int width,int height,uint format,uint type,IntPtr pixels);
        [DllImport(Lib)]private static extern void glGetUniformfv(uint program,int location,IntPtr values);
        internal static string Uniform(uint program,string name){float[] values=new float[4];var pin=GCHandle.Alloc(values,GCHandleType.Pinned);try{int location=glGetUniformLocation(program,name);if(location<0)return "inactive";glGetUniformfv(program,location,pin.AddrOfPinnedObject());return string.Join(",",values);}finally{pin.Free();}}
    }
}
