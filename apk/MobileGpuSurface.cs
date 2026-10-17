using Android.Content;
using Android.Graphics;
using Android.Opengl;
using Android.Views;
using Java.Nio;
using Javax.Microedition.Khronos.Egl;
using Javax.Microedition.Khronos.Opengles;
using System.Text.Json;
using Gl = Android.Opengl.GLES20;

namespace HyperLane.Android;

internal sealed class MobileGpuFrame
{
    internal readonly List<float> Geometry = new();
    internal readonly List<float> Cover = new();
    internal bool BuildingCover;
    internal readonly List<float> Sprites = new();
    internal readonly List<float> HitEffects = new();
    internal Bitmap? Background;
    internal float BackgroundBrightness = 1, HudOpacity = 1;
    internal MobileFilterChange? Filter;
    internal double Seconds;
    internal double FeverPulse;
    internal bool Video;
    internal void Clear() { Geometry.Clear(); Cover.Clear(); Sprites.Clear(); HitEffects.Clear(); BuildingCover = false; Filter = null; }

    internal void Quad(MobileProjection projection, double left, double right, double near, double far, double height,
        Color nearColor, Color farColor, float u0 = 0, float v0 = 0, float u1 = 1, float v1 = 1, bool sprite = false)
    {
        if (right <= left || far <= near) return;
        float[] vertices = new float[40];
        Put(0, left, far, farColor, u0, v0); Put(1, right, far, farColor, u1, v0);
        Put(2, right, near, nearColor, u1, v1); Put(3, left, near, nearColor, u0, v1);
        List<float> target = sprite ? Sprites : BuildingCover ? Cover : Geometry;
        foreach (int index in new[] { 0, 1, 2, 0, 2, 3 })
            for (int component = 0; component < 10; component++) target.Add(vertices[index * 10 + component]);
        void Put(int index, double x, double z, Color color, float u, float v)
        {
            int offset = index * 10;
            projection.Clip(x, height, z, vertices.AsSpan(offset, 4));
            vertices[offset + 4] = u; vertices[offset + 5] = v;
            vertices[offset + 6] = color.R / 255f; vertices[offset + 7] = color.G / 255f;
            vertices[offset + 8] = color.B / 255f; vertices[offset + 9] = color.A / 255f;
        }
    }
}

// The 3D scene and post effects are rendered by GLES, not by a flat Canvas imitation.
internal sealed class MobileGpuSurface : GLSurfaceView
{
    private readonly RendererImpl renderer;
    internal MobileGpuSurface(Context context) : base(context)
    {
        SetEGLContextClientVersion(2);
        SetEGLConfigChooser(8, 8, 8, 8, 16, 0);
        PreserveEGLContextOnPause = true;
        renderer = new RendererImpl(context);
        SetRenderer(renderer);
        RenderMode = Rendermode.WhenDirty;
    }
    internal Surface? VideoSurface => renderer.VideoSurface;
    internal void Submit(Action<MobileGpuFrame, Canvas> build)
    {
        lock (renderer.Gate)
        {
            renderer.Frame.Clear();
            renderer.Hud.EraseColor(Color.Transparent);
            using var canvas = new Canvas(renderer.Hud);
            canvas.Scale(2f / 3, 2f / 3);
            build(renderer.Frame, canvas);
        }
        RequestRender();
    }

    internal bool Sprite(MobileGpuFrame frame, MobileProjection projection, string name, double left, double right,
        double near, double far, double height, bool strip, Color color)
    {
        if (!strip)
        {
            if (!renderer.AtlasRegions.TryGetValue(name, out int[]? region)) return false;
            Add(region, left, right); return true;
        }
        string[] parts = [name + "Left", name + "Center", name + "Right"];
        if (parts.Any(part => !renderer.AtlasRegions.ContainsKey(part))) return false;
        double[] cuts = [0, .18, .82, 1];
        for (int i = 0; i < 3; i++) Add(renderer.AtlasRegions[parts[i]], left + (right - left) * cuts[i], left + (right - left) * cuts[i + 1]);
        return true;
        void Add(int[] region, double a, double b) => frame.Quad(projection, a, b, near, far, height, color, color,
            (region[0] + .5f) / renderer.AtlasWidth, (region[1] + .5f) / renderer.AtlasHeight,
            (region[0] + region[2] - .5f) / renderer.AtlasWidth, (region[1] + region[3] - .5f) / renderer.AtlasHeight, true);
    }

    private sealed class RendererImpl : Java.Lang.Object, IRenderer
    {
        internal readonly object Gate = new();
        internal readonly MobileGpuFrame Frame = new();
        internal readonly Bitmap Hud = Bitmap.CreateBitmap(1280, 720, Bitmap.Config.Argb8888!)!;
        internal readonly Dictionary<string, int[]> AtlasRegions = new();
        internal int AtlasWidth, AtlasHeight;
        internal Surface? VideoSurface;
        private readonly Context context;
        private SurfaceTexture? videoTexture;
        private readonly float[] videoMatrix = new float[16];
        private int width, height, program, effectProgram, videoProgram;
        private int atlas, hud, background, external, sceneTexture, bloomA, bloomB, framebuffer, depth, filteredBackground;
        private Bitmap? backgroundSource;
        private FloatBuffer? buffer;
        private readonly float[] screenQuad = [-1, 1, 0, 1, 0, 0, 1, 1, 1, 1,
            1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, -1, 0, 1, 1, 1, 1, 1, 1, 1,
            -1, 1, 0, 1, 0, 0, 1, 1, 1, 1, 1, -1, 0, 1, 1, 1, 1, 1, 1, 1,
            -1, -1, 0, 1, 0, 1, 1, 1, 1, 1];

        internal RendererImpl(Context context)
        {
            this.context = context;
            using var stream = context.Assets!.Open("skin/skin.json");
            using var json = JsonDocument.Parse(stream);
            foreach (var property in json.RootElement.GetProperty("sprites").EnumerateObject())
                AtlasRegions[property.Name] = property.Value.EnumerateArray().Select(x => x.GetInt32()).ToArray();
        }

        public void OnSurfaceCreated(IGL10? gl, Javax.Microedition.Khronos.Egl.EGLConfig? config)
        {
            filteredBackground = 0;
            program = Program(VertexShader, FragmentShader);
            effectProgram = Program(VertexShader, EffectShader);
            videoProgram = Program(VertexShader, VideoShader);
            using var stream = context.Assets!.Open("skin/atlas.png");
            using Bitmap bitmap = BitmapFactory.DecodeStream(stream)!;
            AtlasWidth = bitmap.Width; AtlasHeight = bitmap.Height;
            atlas = Texture(); Upload(atlas, bitmap, true);
            hud = Texture(); background = Texture(); backgroundSource = null;
            int[] names = new int[1]; Gl.GlGenTextures(1, names, 0); external = names[0];
            Gl.GlBindTexture(36197, external);
            Gl.GlTexParameteri(36197, Gl.GlTextureMinFilter, Gl.GlLinear);
            Gl.GlTexParameteri(36197, Gl.GlTextureMagFilter, Gl.GlLinear);
            Gl.GlTexParameteri(36197, Gl.GlTextureWrapS, Gl.GlClampToEdge);
            Gl.GlTexParameteri(36197, Gl.GlTextureWrapT, Gl.GlClampToEdge);
            videoTexture?.Dispose(); VideoSurface?.Dispose();
            videoTexture = new SurfaceTexture(external);
            VideoSurface = new Surface(videoTexture);
            Gl.GlClearColor(0, 0, 0, 1);
        }

        public void OnSurfaceChanged(IGL10? gl, int w, int h)
        {
            width = w; height = h;
            sceneTexture = Allocate(sceneTexture, w, h);
            if (filteredBackground != 0) filteredBackground = Allocate(filteredBackground, w, h);
            bloomA = Allocate(bloomA, Math.Max(1, w / 4), Math.Max(1, h / 4));
            bloomB = Allocate(bloomB, Math.Max(1, w / 4), Math.Max(1, h / 4));
            int[] names = new int[1];
            if (framebuffer == 0) { Gl.GlGenFramebuffers(1, names, 0); framebuffer = names[0]; }
            if (depth == 0) { Gl.GlGenRenderbuffers(1, names, 0); depth = names[0]; }
            Gl.GlBindRenderbuffer(Gl.GlRenderbuffer, depth);
            Gl.GlRenderbufferStorage(Gl.GlRenderbuffer, Gl.GlDepthComponent16, w, h);
        }

        public void OnDrawFrame(IGL10? gl)
        {
            lock (Gate)
            {
                Target(sceneTexture, width, height, true);
                Gl.GlClear(Gl.GlColorBufferBit | Gl.GlDepthBufferBit);
                Gl.GlDisable(Gl.GlDepthTest);
                Gl.GlDisable(Gl.GlBlend);
                if (Frame.Video && videoTexture is not null)
                {
                    videoTexture.UpdateTexImage(); videoTexture.GetTransformMatrix(videoMatrix);
                    Gl.GlUseProgram(videoProgram);
                    Gl.GlUniformMatrix4fv(Gl.GlGetUniformLocation(videoProgram, "videoMatrix"), 1, false, videoMatrix, 0);
                    Gl.GlUniform1f(Gl.GlGetUniformLocation(videoProgram, "brightness"), Frame.BackgroundBrightness);
                    Draw(screenQuad, videoProgram, external, 36197);
                }
                else if (Frame.Background is { } image)
                {
                    if (!ReferenceEquals(backgroundSource, image)) { Upload(background, image, true); backgroundSource = image; }
                    Flat(background, 1, Frame.BackgroundBrightness);
                }
                if (Frame.Filter is { BackgroundOnly: true } bgFilter)
                {
                    if (bgFilter.Kind == "BRIGHTNESS") DrawBrightness(bgFilter.Strength);
                    else
                    {
                        // Only allocate a second full-size target when a background
                        // sampling effect needs it. Brightness uses a cheap overlay.
                        if (filteredBackground == 0) filteredBackground = Allocate(0, width, height);
                        RenderFiltered(sceneTexture, filteredBackground, bgFilter);
                        Target(sceneTexture, width, height, true);
                        Gl.GlDisable(Gl.GlBlend);
                        Effect(filteredBackground, -1, 1, true);
                    }
                }
                Gl.GlEnable(Gl.GlBlend); Gl.GlBlendFunc(Gl.GlSrcAlpha, Gl.GlOneMinusSrcAlpha);
                if (Frame.FeverPulse > 0) DrawFever();
                Gl.GlEnable(Gl.GlDepthTest); Gl.GlDepthFunc(Gl.GlLequal);
                Gl.GlUseProgram(program);
                Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "textured"), 0);
                Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "opacity"), 1);
                Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "brightness"), 1);
                Draw(Frame.Geometry.ToArray(), program, 0);
                Gl.GlDepthMask(false);
                Draw(Frame.Cover.ToArray(), program, 0);
                Gl.GlDepthMask(true);
                Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "textured"), 1);
                Draw(Frame.Sprites.ToArray(), program, atlas);
                Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "textured"), 0);
                Gl.GlDepthMask(false);
                Draw(Frame.HitEffects.ToArray(), program, 0);
                Gl.GlDepthMask(true);
                Gl.GlDisable(Gl.GlDepthTest);
                Upload(hud, Hud, true); Flat(hud, Frame.HudOpacity);
                RenderFiltered(sceneTexture, 0, Frame.Filter is { BackgroundOnly: false } allFilter ? allFilter : null);
            }
        }

        private void DrawBrightness(double strength)
        {
            var (tone,alpha) = MobileFilters.BrightnessOverlay(strength);
            Color color = Color.Argb(alpha,tone,tone,tone);
            Gl.GlEnable(Gl.GlBlend);Gl.GlBlendFunc(Gl.GlSrcAlpha,Gl.GlOneMinusSrcAlpha);
            Gl.GlUseProgram(program);Gl.GlUniform1f(Gl.GlGetUniformLocation(program,"textured"),0);
            Gl.GlUniform1f(Gl.GlGetUniformLocation(program,"opacity"),1);Gl.GlUniform1f(Gl.GlGetUniformLocation(program,"brightness"),1);
            Draw(ScreenMesh(0,0,width,height,0,0,1,1,color,color),program,0);
            Gl.GlDisable(Gl.GlBlend);
        }

        private void RenderFiltered(int sourceTexture, int destination, MobileFilterChange? filter)
        {
            Gl.GlDisable(Gl.GlBlend);
            if (filter is { Kind: "BLOOM" } bloom)
            {
                int sw=Math.Max(1,width/4),sh=Math.Max(1,height/4);
                Target(bloomA,sw,sh,false);Effect(sourceTexture,2,1,true);
                Target(bloomB,sw,sh,false);Effect(bloomA,3,1,true,1f/sw,1f/sh);
                Target(bloomA,sw,sh,false);Effect(bloomB,4,1,true,1f/sw,1f/sh);
                Target(destination,width,height,false);Effect(sourceTexture,-1,1,true);
                Gl.GlEnable(Gl.GlBlend);Gl.GlBlendFunc(Gl.GlOne,Gl.GlOne);
                Effect(bloomA,5,(float)(bloom.Strength/100),true);Gl.GlDisable(Gl.GlBlend);
            }
            else if (filter is { Kind: "GLITCH" } glitch)
            {
                Target(destination,width,height,false);Effect(sourceTexture,-1,1,true);
                DrawGlitch(glitch.Strength,sourceTexture);
            }
            else
            {
                Target(destination,width,height,false);
                int mode=filter?.Kind switch {"MONO"=>0,"INVERT"=>1,"MOSAIC"=>6,"SCANLINE"=>7,_=>-1};
                Effect(sourceTexture,mode,(float)((filter?.Strength??0)/100),true);
                if (filter is { Kind: "BRIGHTNESS" } brightness) DrawBrightness(brightness.Strength);
            }
        }

        private void DrawFever()
        {
            Gl.GlUseProgram(program); Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "textured"), 0);
            Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "opacity"), 1); Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "brightness"), 1);
            for (int section = 0; section < 24; section++)
            {
                double a = section / 24.0, b = (section + 1) / 24.0;
                Color ca = Color.Argb((int)Math.Round(205 * Frame.FeverPulse * Math.Pow(1 - a, 2.2)), 255, 255, 255);
                Color cb = Color.Argb((int)Math.Round(205 * Frame.FeverPulse * Math.Pow(1 - b, 2.2)), 255, 255, 255);
                Draw(ScreenMesh((float)(width * .14 * a), 0, (float)(width * .14 * b), height, 0, 0, 1, 1, ca, cb), program, 0);
                Draw(ScreenMesh((float)(width * (1 - .14 * b)), 0, (float)(width * (1 - .14 * a)), height, 0, 0, 1, 1, cb, ca), program, 0);
            }
        }

        private void DrawGlitch(double strength, int sourceTexture)
        {
            Gl.GlEnable(Gl.GlBlend); Gl.GlBlendFunc(Gl.GlSrcAlpha, Gl.GlOneMinusSrcAlpha);
            Gl.GlUseProgram(program); Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "textured"), 1);
            Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "opacity"), 1); Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "brightness"), 1);
            int frame = (int)Math.Floor(Math.Max(0, Frame.Seconds) * 60);
            var random = new Random(unchecked(frame * 7919 + 864));
            double saturation = Math.Clamp((strength - 55) / 45, 0, 1);
            if (saturation > 0)
            {
                int broadShift = Math.Max(1, (int)(width * (.015 + .20 * saturation)));
                for (int y = 0; y < height;)
                {
                    int band = Math.Min(height - y, random.Next(Math.Max(3, height / 85), Math.Max(4, height / 22)));
                    int shift = random.Next(-broadShift, broadShift + 1);
                    Strip(y, band, shift, Color.Argb((int)(80 + 175 * saturation), 255, 255, 255));
                    if (random.NextDouble() < saturation)
                    {
                        int offset = Math.Max(2, (int)(width * (.004 + .028 * saturation)));
                        int alpha = (int)(45 + 125 * saturation);
                        Strip(y, band, shift + offset, Color.Argb(alpha, 20, 235, 255));
                        Strip(y, band, shift - offset, Color.Argb(alpha, 255, 35, 175));
                    }
                    y += band;
                }
            }
            int count = Math.Clamp(3 + (int)(strength / 4), 3, 28);
            int maxShift = Math.Max(1, (int)Math.Round(width * .0005 * strength));
            for (int i = 0; i < count; i++)
            {
                int y = random.Next(height);
                int band = Math.Min(height - y, random.Next(1, Math.Max(3, (int)(height * (.0015 + strength * .00008))) + 1));
                int shift = random.Next(-maxShift, maxShift + 1); if (shift == 0) shift = 1;
                Strip(y, band, shift, Color.Argb(i % 4 == 0 ? 255 : 180, 255, 255, 255));
                if (strength >= 5 && i % 2 == 0)
                {
                    int alpha = (int)Math.Clamp(40 + strength * 1.2, 0, 170);
                    int offset = Math.Max(2, (int)(width * (.001 + strength * .00008)));
                    Strip(y, band, shift + offset, Color.Argb(alpha, 30, 235, 255));
                    Strip(y, band, shift - offset, Color.Argb(alpha, 255, 45, 175));
                }
            }
            Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "textured"), 0);
            for (int i = 0; i < Math.Clamp((int)(strength / 5), 1, 20); i++)
            {
                int y = random.Next(height), lineHeight = random.Next(1, Math.Max(2, (int)(height * .003)));
                int x = random.Next(width), lineWidth = Math.Min(width - x, random.Next(Math.Max(2, width / 20), Math.Max(3, width / 2)));
                int alpha = (int)Math.Clamp(10 + strength * .5, 0, 65);
                Color color = i % 3 == 0 ? Color.Argb(alpha, 20, 235, 255) : i % 3 == 1 ? Color.Argb(alpha, 255, 40, 175) : Color.Argb(alpha, 245, 245, 255);
                Draw(ScreenMesh(x, y, x + lineWidth, y + lineHeight, 0, 0, 1, 1, color, color), program, 0);
            }
            Gl.GlDisable(Gl.GlBlend);
            void Strip(int y, int band, int shift, Color color)
            {
                int target = Math.Max(0, shift), source = Math.Max(0, -shift), size = width - Math.Abs(shift);
                if (size <= 0 || band <= 0) return;
                Draw(ScreenMesh(target, y, target + size, y + band, source / (float)width, 1 - y / (float)height,
                    (source + size) / (float)width, 1 - (y + band) / (float)height, color, color), program, sourceTexture);
            }
        }

        private float[] ScreenMesh(float left, float top, float right, float bottom, float u0, float v0, float u1, float v1, Color leftColor, Color rightColor)
        {
            float[] result = new float[60]; int index = 0;
            Put(left, top, u0, v0, leftColor); Put(right, top, u1, v0, rightColor); Put(right, bottom, u1, v1, rightColor);
            Put(left, top, u0, v0, leftColor); Put(right, bottom, u1, v1, rightColor); Put(left, bottom, u0, v1, leftColor);
            return result;
            void Put(float x, float y, float u, float v, Color color)
            {
                result[index++] = x / width * 2 - 1; result[index++] = 1 - y / height * 2;
                result[index++] = 0; result[index++] = 1; result[index++] = u; result[index++] = v;
                result[index++] = color.R / 255f; result[index++] = color.G / 255f; result[index++] = color.B / 255f; result[index++] = color.A / 255f;
            }
        }

        private void Target(int texture, int w, int h, bool withDepth)
        {
            Gl.GlBindFramebuffer(Gl.GlFramebuffer, texture == 0 ? 0 : framebuffer);
            if (texture != 0)
            {
                Gl.GlFramebufferTexture2D(Gl.GlFramebuffer, Gl.GlColorAttachment0, Gl.GlTexture2d, texture, 0);
                Gl.GlFramebufferRenderbuffer(Gl.GlFramebuffer, Gl.GlDepthAttachment, Gl.GlRenderbuffer, withDepth ? depth : 0);
            }
            Gl.GlViewport(0, 0, w, h);
        }
        private void Flat(int texture, float opacity, float brightness = 1)
        {
            Gl.GlUseProgram(program); Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "textured"), 1);
            Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "opacity"), opacity);
            Gl.GlUniform1f(Gl.GlGetUniformLocation(program, "brightness"), brightness);
            Draw(screenQuad, program, texture);
        }
        private void Effect(int texture, int mode, float intensity, bool flip, float tx = 0, float ty = 0)
        {
            Gl.GlUseProgram(effectProgram);
            Gl.GlUniform1i(Gl.GlGetUniformLocation(effectProgram, "mode"), mode);
            Gl.GlUniform1f(Gl.GlGetUniformLocation(effectProgram, "intensity"), intensity);
            Gl.GlUniform1f(Gl.GlGetUniformLocation(effectProgram, "flip"), flip ? 1 : 0);
            Gl.GlUniform1f(Gl.GlGetUniformLocation(effectProgram, "time"), (float)Frame.Seconds);
            Gl.GlUniform2f(Gl.GlGetUniformLocation(effectProgram, "texel"), tx == 0 ? 1f / width : tx, ty == 0 ? 1f / height : ty);
            Draw(screenQuad, effectProgram, texture);
        }
        private void Draw(float[] values, int shader, int texture, int target = Gl.GlTexture2d)
        {
            if (values.Length == 0) return;
            if (buffer is null || buffer.Capacity() < values.Length)
            {
                buffer?.Dispose();
                buffer = ByteBuffer.AllocateDirect(Math.Max(4096, values.Length * 4))!.Order(ByteOrder.NativeOrder()!)!.AsFloatBuffer();
            }
            buffer!.Clear(); buffer.Put(values); buffer.Position(0);
            Gl.GlUseProgram(shader); Gl.GlActiveTexture(Gl.GlTexture0); Gl.GlBindTexture(target, texture);
            Gl.GlUniform1i(Gl.GlGetUniformLocation(shader, "frameTex"), 0);
            Attributes("position", 4, 0); Attributes("uv", 2, 4); Attributes("color", 4, 6);
            Gl.GlDrawArrays(Gl.GlTriangles, 0, values.Length / 10);
            void Attributes(string name, int size, int offset)
            {
                int index = Gl.GlGetAttribLocation(shader, name); if (index < 0) return;
                buffer.Position(offset); Gl.GlEnableVertexAttribArray(index);
                Gl.GlVertexAttribPointer(index, size, Gl.GlFloat, false, 40, buffer);
            }
        }
        private static int Texture()
        {
            int[] names = new int[1]; Gl.GlGenTextures(1, names, 0); Gl.GlBindTexture(Gl.GlTexture2d, names[0]);
            Gl.GlTexParameteri(Gl.GlTexture2d, Gl.GlTextureMinFilter, Gl.GlLinear);
            Gl.GlTexParameteri(Gl.GlTexture2d, Gl.GlTextureMagFilter, Gl.GlLinear);
            Gl.GlTexParameteri(Gl.GlTexture2d, Gl.GlTextureWrapS, Gl.GlClampToEdge);
            Gl.GlTexParameteri(Gl.GlTexture2d, Gl.GlTextureWrapT, Gl.GlClampToEdge);
            return names[0];
        }
        private static int Allocate(int texture, int w, int h)
        {
            if (texture == 0) texture = Texture();
            Gl.GlBindTexture(Gl.GlTexture2d, texture);
            Gl.GlTexImage2D(Gl.GlTexture2d, 0, Gl.GlRgba, w, h, 0, Gl.GlRgba, Gl.GlUnsignedByte, null);
            return texture;
        }
        private static void Upload(int texture, Bitmap bitmap, bool allocate)
        {
            Gl.GlBindTexture(Gl.GlTexture2d, texture);
            if (allocate) GLUtils.TexImage2D(Gl.GlTexture2d, 0, bitmap, 0);
            else GLUtils.TexSubImage2D(Gl.GlTexture2d, 0, 0, 0, bitmap);
        }
        private static int Program(string vertex, string fragment)
        {
            int v = Shader(Gl.GlVertexShader, vertex), f = Shader(Gl.GlFragmentShader, fragment);
            int program = Gl.GlCreateProgram(); Gl.GlAttachShader(program, v); Gl.GlAttachShader(program, f); Gl.GlLinkProgram(program);
            int[] success = new int[1]; Gl.GlGetProgramiv(program, Gl.GlLinkStatus, success, 0);
            Gl.GlDeleteShader(v); Gl.GlDeleteShader(f);
            if (success[0] == 0) throw new InvalidOperationException(Gl.GlGetProgramInfoLog(program));
            return program;
        }
        private static int Shader(int type, string source)
        {
            int shader = Gl.GlCreateShader(type); Gl.GlShaderSource(shader, source); Gl.GlCompileShader(shader);
            int[] success = new int[1]; Gl.GlGetShaderiv(shader, Gl.GlCompileStatus, success, 0);
            if (success[0] == 0) throw new InvalidOperationException(Gl.GlGetShaderInfoLog(shader));
            return shader;
        }
        private const string VertexShader = "attribute vec4 position; attribute vec2 uv; attribute vec4 color; varying vec2 vUv; varying vec4 vColor; void main(){gl_Position=position;vUv=uv;vColor=color;}";
        private const string FragmentShader = "precision mediump float; uniform sampler2D frameTex; uniform float textured; uniform float opacity;uniform float brightness; varying vec2 vUv; varying vec4 vColor; void main(){ vec4 c=textured>.5?texture2D(frameTex,vUv):vec4(1.);if(c.a<.001)discard;if(textured>.5)c.rgb/=max(c.a,.00001);gl_FragColor=c*vColor;gl_FragColor.rgb*=brightness;gl_FragColor.a*=opacity;}";
        private const string VideoShader = "#extension GL_OES_EGL_image_external : require\nprecision mediump float;uniform samplerExternalOES frameTex;uniform mat4 videoMatrix;uniform float brightness;varying vec2 vUv;void main(){vec2 p=(videoMatrix*vec4(vUv.x,1.-vUv.y,0.,1.)).xy;gl_FragColor=vec4(texture2D(frameTex,p).rgb*brightness,1.);}";
        private const string EffectShader = """
            precision highp float;
            uniform sampler2D frameTex; uniform int mode; uniform float intensity, flip, time; uniform vec2 texel;
            varying vec2 vUv;
            float rnd(float n){return fract(sin(n*127.1)*43758.5453);}
            vec3 sampleAt(vec2 p){return texture2D(frameTex,clamp(p,vec2(0.),vec2(1.))).rgb;}
            void main(){
              vec2 p=vec2(vUv.x,flip>.5?1.-vUv.y:vUv.y); vec3 c=sampleAt(p);
              if(mode==0){c=mix(c,vec3(dot(c,vec3(.299,.587,.114))),intensity);}
              else if(mode==1){c=mix(c,1.-c,intensity);}
              else if(mode==2){float l=max(max(c.r,c.g),c.b);c*=smoothstep(.48,.82,l);}
              else if(mode==3 || mode==4){vec2 d=mode==3?vec2(texel.x,0.):vec2(0.,texel.y);c*=.18;
                c+=(sampleAt(p+d)+sampleAt(p-d))*.15;c+=(sampleAt(p+d*2.)+sampleAt(p-d*2.))*.12;
                c+=(sampleAt(p+d*3.)+sampleAt(p-d*3.))*.09;c+=(sampleAt(p+d*4.)+sampleAt(p-d*4.))*.05;}
              else if(mode==5){c*=intensity*2.5;}
              else if(mode==6){vec2 s=texel*(1.+intensity*79.);c=sampleAt((floor(p/s)+.5)*s);}
              else if(mode==7){float y=(1.-p.y)/texel.y;if(mod(floor(y),4.)<2.)c=mix(c,vec3(0.,8./255.,18./255.),intensity*210./255.);}
              else if(mode==8){float tick=floor(time*30.);float band=floor(p.y*(32.+intensity*280.));float r=rnd(band+tick*917.);
                float shift=(rnd(band*2.+tick*43.)-.5)*intensity*.8*step(r, .2+intensity*.8);
                vec2 q=vec2(fract(p.x+shift),p.y);float rgb=intensity*.018;
                c=vec3(sampleAt(q+vec2(rgb,0.)).r,sampleAt(q).g,sampleAt(q-vec2(rgb,0.)).b);
                c=mix(c,c.brg,step(.7,r)*intensity);c=mix(c,vec3(rnd(band+tick),rnd(band+2.+tick),rnd(band+4.+tick)),step(.88,r)*intensity);}
              gl_FragColor=vec4(c,1.);
            }
            """;
    }
}
