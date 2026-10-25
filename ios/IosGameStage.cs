using AVFoundation;
using CoreAnimation;
using CoreGraphics;
using CoreMedia;
using CoreVideo;
using Foundation;
using HyperLane;
using HyperLane.Android;
using UIKit;
using static HyperLane.iOS.IosVisuals;

namespace HyperLane.iOS;

internal sealed partial class MigrationStage : UIView
{
    internal enum Screen { Title, Modes, Select, Settings, Loading, Playing, Paused, Result, Best30, Practice, Dan, DanResult }
    internal Screen Page=Screen.Title;
    internal MobileSession? Session;
    internal IosSettings Settings=new();
    internal double Seconds;
    internal Action<IReadOnlySet<int>,double>? InputChanged;
    internal Action? PauseRequested,ImportRequested,BackRequested,ResumeRequested,SaveSettings,NameRequested;
    internal Action<MobileSong,MobileDifficulty>? StartRequested;
    internal Action<MobileSong,MobileDifficulty,double>? PracticeRequested;
    internal Action? ReplayRequested,DanRequested,RetryRequested;
    internal bool PracticePlaying;
    private MobileDifficulty? loadingDifficulty;
    internal IReadOnlyList<MobileSong> Songs=[];
    internal IReadOnlyList<MobileSong> AllSongs=[];
    internal string SongSearch="";
    internal Action? SearchRequested;
    internal string UserName="";
    internal PlayerRating? Rating;
    private double ratingTotal;
    private readonly IosVisuals visuals=new();
    private readonly UIImageView background=new(){ContentMode=UIViewContentMode.ScaleAspectFill,ClipsToBounds=true};
    private readonly UIView dim=new(){BackgroundColor=UIColor.Black,UserInteractionEnabled=false};
    private readonly UIView filterShade=new(){UserInteractionEnabled=false,Hidden=true};
    private readonly UIView danWhite=new(){UserInteractionEnabled=false,BackgroundColor=UIColor.White,Alpha=0};
    internal double DanFailureStrength,DanReturnAt;
    private readonly ParticleOverlay particleOverlay;
    private readonly CAGradientLayer feverLeft=new(),feverRight=new();
    private readonly IosLaneRenderer gpu;
    private readonly Overlay overlay;
    private AVPlayer? movie;private AVPlayerLayer? movieLayer;
    private AVPlayerItemVideoOutput? movieOutput;
    private CVPixelBuffer? effectVideo;
    private byte[]? effectOverlay;
    private CGBitmapContext? effectCanvas;
    private byte[]? effectUnderlay;
    private CGBitmapContext? underlayCanvas;
    private readonly Dictionary<UITouch,int> touches=new();
    private MobileSong? loadedSong;private UIImage? jacket,selectionBackground,previousBackground;
    private int index,difficulty,mode,settingsTab;
    private double position,started=CAAnimation.CurrentMediaTime(),fadeAt,resultAt,loadingAt,exitAt,transitionAt,downY,downPosition;
    private Screen transitionTarget,settingsReturn;
    private bool dragging,loadingReady;private Action? readyAction;
    private nfloat Scale=>(nfloat)Math.Min((double)Bounds.Width/1920,(double)Bounds.Height/1080);
    private CGPoint Origin=>new((Bounds.Width-1920*Scale)/2,(Bounds.Height-1080*Scale)/2);
    private CGRect Viewport=>new(Origin.X,Origin.Y,1920*Scale,1080*Scale);
    private bool DefaultPlayBackground=>Media(Session?.Song,Session?.Song.Chart.Background).Length==0 && movie==null;
    private static double DefaultBrightness(double now){double phase=now%5;return .25+(phase<2?.075*(1-Math.Cos(Math.PI*phase)):0);}
    internal bool SettingsFromPause=>settingsReturn==Screen.Paused;
    internal MobileSong? Selected=>Songs.Count==0?null:Songs[Math.Clamp(index,0,Songs.Count-1)];
    internal MobileDifficulty? SelectedDifficulty=>Selected?.Chart.Difficulties.ElementAtOrDefault(difficulty);
    internal MigrationStage()
    {
        BackgroundColor=UIColor.Black;MultipleTouchEnabled=true;AddSubview(background);AddSubview(dim);AddSubview(filterShade);
        particleOverlay=new ParticleOverlay(this){BackgroundColor=UIColor.Clear,Opaque=false,UserInteractionEnabled=false,Hidden=true};AddSubview(particleOverlay);
        CGColor[] feverColors=Enumerable.Range(0,5).Select(i=>Color(255,255,255,205/255.0*Math.Pow(1-i/4.0,2.2)).CGColor).ToArray();
        foreach(var layer in new[]{feverLeft,feverRight}){layer.Colors=feverColors;layer.StartPoint=new CGPoint(0,.5);layer.EndPoint=new CGPoint(1,.5);layer.Opacity=0;Layer.AddSublayer(layer);}feverRight.StartPoint=new CGPoint(1,.5);feverRight.EndPoint=new CGPoint(0,.5);
        foreach(string word in new[]{"PERFECT","GREAT","GOOD","BAD","MISS","AUTO"})visuals.Judgement(word);
        Console.Error.WriteLine("HL startup: GPU initialization");
        gpu=new IosLaneRenderer(CGRect.Empty,visuals);AddSubview(gpu.View);
        Console.Error.WriteLine("HL startup: GPU ready");
        overlay=new Overlay(this){BackgroundColor=UIColor.Clear,Opaque=false,UserInteractionEnabled=false};AddSubview(overlay);
        AddSubview(danWhite);
    }
    private sealed class Overlay(MigrationStage owner):UIView
    {public override void Draw(CGRect rect){using var c=UIGraphics.GetCurrentContext();c.SaveState();c.TranslateCTM(owner.Origin.X,owner.Origin.Y);c.ScaleCTM(owner.Scale,owner.Scale);c.ClipToRect(new CGRect(0,0,1920,1080));owner.DrawScreen(c);c.RestoreState();}}
    private sealed class ParticleOverlay(MigrationStage owner):UIView
    {public override void Draw(CGRect rect){using var c=UIGraphics.GetCurrentContext();c.SaveState();c.TranslateCTM(owner.Origin.X,owner.Origin.Y);c.ScaleCTM(owner.Scale,owner.Scale);c.ClipToRect(new CGRect(0,0,1920,1080));owner.Particles(c,CAAnimation.CurrentMediaTime());c.RestoreState();}}
    public override void LayoutSubviews(){base.LayoutSubviews();background.Frame=Viewport;dim.Frame=Viewport;filterShade.Frame=Viewport;particleOverlay.Frame=Bounds;gpu.View.Frame=Viewport;overlay.Frame=Bounds;if(movieLayer!=null)movieLayer.Frame=Viewport;feverLeft.Frame=new CGRect(Viewport.X,Viewport.Y,Viewport.Width*.14f,Viewport.Height);feverRight.Frame=new CGRect(Viewport.GetMaxX()-Viewport.Width*.14f,Viewport.Y,Viewport.Width*.14f,Viewport.Height);}
    internal void ClearTouches(){touches.Clear();gpu.Held=new HashSet<int>();}
    internal void SetSongs(IReadOnlyList<MobileSong> songs){AllSongs=songs;chapterMenu.Refresh(MobileChapterMenu.RootFor(songs));ApplySearch(SongSearch);RefreshRating();}
    private int songSort;
    private bool SongSortEnabled=>mode!=1&&(mode==2||chapterMenu.Filter=="@all");
    internal void ApplySearch(string search){SongSearch=search;var found=chapterMenu.Songs(AllSongs,mode==2).Where(s=>(s.Chart.Title+" "+s.Chart.Composer+" "+s.Chart.ChartDesigner+" "+string.Join(" ",s.Chart.Difficulties.Select(d=>d.ChartDesigner))).Contains(search,StringComparison.OrdinalIgnoreCase));var name=SelectedDifficulty?.Name;
        MobileDifficulty? Diff(MobileSong song)=>song.Chart.Difficulties.FirstOrDefault(d=>d.Name.Equals(name,StringComparison.OrdinalIgnoreCase))??song.Chart.Difficulties.FirstOrDefault();
        Songs=SongSortEnabled&&songSort!=0?found.OrderByDescending(s=>songSort==1?(double.TryParse(Diff(s)?.Level,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)?n:double.NegativeInfinity):Diff(s)is {} d?Best(s,d)??-1:-1).ToArray():found.ToArray();index=0;position=0;difficulty=0;loadedSong=null;}
    internal void ShowSelect(){Page=Screen.Select;movie?.Pause();ClearTouches();}
    internal void ShowResult(){Page=Screen.Result;movie?.Pause();resultAt=CAAnimation.CurrentMediaTime();RefreshRating();}
    internal void BeginLoading(MobileSong song,MobileDifficulty difficulty,Action onReady){Page=Screen.Loading;Session=null;loadingDifficulty=difficulty;loadedSong=null;LoadSong(song);loadingAt=CAAnimation.CurrentMediaTime();exitAt=0;loadingReady=false;readyAction=onReady;}
    internal void LoadingReady()=>loadingReady=true;
    internal void CancelLoading(){readyAction=null;ShowSelect();}
    internal void PauseVideo()=>movie?.Pause();
    internal void ResumeVideo(){if(movie==null)return;movie.Seek(CMTime.FromSeconds(Math.Max(0,Seconds+Settings.AudioOffsetMs/1000.0),600));movie.Play();}
    internal void Refresh(double now)
    {
        if(transitionAt>0){double e=now-transitionAt;if(e>=.5)Page=transitionTarget;if(e>=1)transitionAt=0;}
        if(Page==Screen.Loading&&loadingReady&&now-loadingAt>=2&&exitAt==0)exitAt=now;
        if(Page==Screen.Loading&&exitAt>0&&now-exitAt>=.5){Page=Screen.Playing;readyAction?.Invoke();readyAction=null;}
        bool play=Page is Screen.Playing or Screen.Paused;gpu.View.Hidden=!play;gpu.Session=play?Session:null;gpu.Settings=Settings;gpu.Seconds=Seconds;
        double pulse=0;if(play&&Session is {} current){double b=MobileChart.BeatAtSeconds(current.Difficulty,current.Song.Chart.Bpm,Seconds-current.Song.Chart.OffsetFor(current.Difficulty));var f=current.Difficulty.FeverSections.FirstOrDefault(s=>b>=s.Start&&b<=s.End);if(f.End>f.Start){double relative=b-f.Start;pulse=Math.Pow(1-(relative-Math.Floor(relative)),2)*Settings.FeverAuraBrightness/100.0;}}
        CATransaction.Begin();CATransaction.DisableActions=true;feverLeft.Opacity=feverRight.Opacity=(float)pulse;CATransaction.Commit();
        MobileFilterChange? active=null;if(play&&Session is {} filterSession){double filterBeat=MobileChart.BeatAtSeconds(filterSession.Difficulty,filterSession.Song.Chart.Bpm,Seconds-filterSession.Song.Chart.OffsetFor(filterSession.Difficulty));active=MobileFilters.At(filterSession.Difficulty.FilterChanges,filterBeat);}
        if(DanFailureStrength>0)active=new MobileFilterChange(0,"GLITCH",DanFailureStrength*100,0,false);
        bool gpuEffects=active is {Kind:not "BRIGHTNESS"};gpu.ActiveFilter=gpuEffects?active:null;
        if(movieLayer!=null)movieLayer.Hidden=!play||gpuEffects;
        particleOverlay.Hidden=!play||!DefaultPlayBackground;if(!particleOverlay.Hidden)particleOverlay.SetNeedsDisplay();filterShade.Hidden=true;
        if(play){background.Image=visuals.FileImage(Media(Session?.Song,Session?.Song.Chart.Background))??visuals.Asset("skin/background.png");dim.Alpha=1-(nfloat)(Settings.BackgroundBrightness/100.0*(DefaultPlayBackground?DefaultBrightness(now):1));
            dim.BackgroundColor=UIColor.Black;
            if(Session is {} s){double beat=MobileChart.BeatAtSeconds(s.Difficulty,s.Song.Chart.Bpm,Seconds-s.Song.Chart.OffsetFor(s.Difficulty));var filter=MobileFilters.At(s.Difficulty.FilterChanges,beat);if(filter is {Kind:"BRIGHTNESS",BackgroundOnly:true} f){var b=MobileFilters.BrightnessOverlay(f.Strength);filterShade.Hidden=false;filterShade.BackgroundColor=b.Tone==0?UIColor.Black:UIColor.White;filterShade.Alpha=(nfloat)(b.Alpha/255.0);}}}
        else{dim.BackgroundColor=UIColor.Black;background.Image=visuals.Asset("skin/background.png");dim.Alpha=(nfloat)(1-DefaultBrightness(now));}
        overlay.Hidden=gpuEffects&&active?.BackgroundOnly==false;
        if(gpuEffects){gpu.SceneBackground=background.Image?.CGImage;gpu.BackgroundGain=1-(double)dim.Alpha;
            if(movieOutput!=null&&movie!=null){var time=movie.CurrentTime;if(movieOutput.HasNewPixelBufferForItemTime(time)){var displayed=CoreMedia.CMTime.Zero;var buffer=movieOutput.CopyPixelBuffer(time,ref displayed);if(buffer!=null){effectVideo?.Dispose();effectVideo=buffer;}}}gpu.SceneVideo=effectVideo;
            RenderEffectUnderlay(now);gpu.SceneUnderlay=effectUnderlay;
            if(active?.BackgroundOnly==false){RenderEffectOverlay(now);gpu.SceneOverlay=effectOverlay;}else gpu.SceneOverlay=null;
        }
        if(play)gpu.View.Display();overlay.SetNeedsDisplay();
        danWhite.Frame=Viewport;danWhite.Alpha=(nfloat)(DanFailureStrength>0?DanFailureStrength:DanReturnAt>0?Math.Clamp(1-(now-DanReturnAt),0,1):0);if(DanReturnAt>0&&now-DanReturnAt>=1)DanReturnAt=0;
    }
    private void RenderEffectOverlay(double now)
    {
        if(effectCanvas==null){effectOverlay=new byte[1920*1080*4];using var space=CGColorSpace.CreateDeviceRGB();effectCanvas=new CGBitmapContext(effectOverlay,1920,1080,8,1920*4,space,CGImageAlphaInfo.PremultipliedLast);}
        var c=effectCanvas;c.ClearRect(new CGRect(0,0,1920,1080));c.SaveState();c.TranslateCTM(0,1080);c.ScaleCTM(1,-1);UIGraphics.PushContext(c);
        try{Hud(c);if(Page==Screen.Paused)PauseBox(c);}
        finally{UIGraphics.PopContext();c.RestoreState();}
    }
    private void RenderEffectUnderlay(double now)
    {
        if(underlayCanvas==null){effectUnderlay=new byte[1920*1080*4];using var space=CGColorSpace.CreateDeviceRGB();underlayCanvas=new CGBitmapContext(effectUnderlay,1920,1080,8,1920*4,space,CGImageAlphaInfo.PremultipliedLast);}
        var c=underlayCanvas;c.ClearRect(new CGRect(0,0,1920,1080));c.SaveState();c.TranslateCTM(0,1080);c.ScaleCTM(1,-1);UIGraphics.PushContext(c);
        try{if(DefaultPlayBackground)Particles(c,now);double pulse=feverLeft.Opacity;if(pulse>0)for(int i=0;i<24;i++){double a=i/24.0,b=(i+1)/24.0;Fill(c,Rect(1920*.14*a,0,1920*.14/24,1080),Color(255,255,255,205/255.0*pulse*Math.Pow(1-a,2.2)));Fill(c,Rect(1920*(1-.14*b),0,1920*.14/24,1080),Color(255,255,255,205/255.0*pulse*Math.Pow(1-a,2.2)));}}
        finally{UIGraphics.PopContext();c.RestoreState();}
    }
    internal static string Media(MobileSong? song,string? name)
    {if(song==null||string.IsNullOrWhiteSpace(name))return "";try{string path=Path.GetFullPath(Path.Combine(song.Folder,name.Replace('\\','/')));return path.StartsWith(Path.GetFullPath(song.Folder)+Path.DirectorySeparatorChar,StringComparison.Ordinal)&&File.Exists(path)?path:"";}catch{return "";}}
    internal static void PrepareMedia(MobileSong song)
    {
        string Pick(string preferred,params string[] extensions){string path=Media(song,preferred);if(path.Length>0&&extensions.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase))return Path.GetRelativePath(song.Folder,path);return Directory.EnumerateFiles(song.Folder,"*",SearchOption.TopDirectoryOnly).Where(p=>extensions.Contains(Path.GetExtension(p),StringComparer.OrdinalIgnoreCase)).Select(p=>Path.GetRelativePath(song.Folder,p)).FirstOrDefault()??"";}
        song.Chart.Audio=Pick(song.Chart.Audio,".ogg",".wav",".mp3",".m4a",".aac");song.Chart.Jacket=Pick(song.Chart.Jacket,".png",".jpg",".jpeg",".webp");song.Chart.Video=Pick(song.Chart.Video,".mp4");
        // Background deliberately has NO file fallback: missing/blank means the
        // default background and particles, exactly as on Android.
    }
    private void LoadSong(MobileSong song)
    {
        if(ReferenceEquals(loadedSong,song))return;loadedSong=song;jacket=visuals.FileImage(Media(song,song.Chart.Jacket));previousBackground=selectionBackground;
        string path=Media(song,song.Chart.Background);selectionBackground=visuals.Blur(visuals.FileImage(path),path);fadeAt=CAAnimation.CurrentMediaTime();
        movie?.Pause();movieLayer?.RemoveFromSuperLayer();movieLayer?.Dispose();movieOutput?.Dispose();movieOutput=null;effectVideo?.Dispose();effectVideo=null;gpu.SceneVideo=null;movie?.Dispose();movie=null;movieLayer=null;
        string video=Media(song,song.Chart.Video);if(Settings.PreferMusicVideo&&Page==Screen.Loading&&video.Length>0){using var url=NSUrl.FromFilename(video);movie=AVPlayer.FromUrl(url);movie.Muted=true;movieOutput=new AVPlayerItemVideoOutput(new CVPixelBufferAttributes{PixelFormatType=CVPixelFormatType.CV32BGRA});movie.CurrentItem?.AddOutput(movieOutput);movieLayer=AVPlayerLayer.FromPlayer(movie);movieLayer.VideoGravity=AVLayerVideoGravity.ResizeAspectFill;movieLayer.Frame=Viewport;Layer.InsertSublayer(movieLayer,1);}
    }
    private void Transition(Screen to){transitionTarget=to;transitionAt=CAAnimation.CurrentMediaTime();movie?.Pause();}
    internal static string Key(MobileSong song,MobileDifficulty d)=>PlayerRating.Key(song.Folder,song.ChartPath,d.Name);
    internal static string Hash(MobileSong song)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(song.ChartPath)));
    internal static double Level(MobileDifficulty d)=>ChartLevel.TryParse(d.Level,out double level)?level:0;
    private readonly Dictionary<string,(string Hash,double Level)> metadata=new();
    private readonly Dictionary<string,int?> bestScores=new();
    private (string Hash,double Level) Meta(MobileSong song,MobileDifficulty d){string key=Key(song,d);if(!metadata.TryGetValue(key,out var meta))metadata[key]=meta=(Hash(song),Level(d));return meta;}
    private int? Best(MobileSong song,MobileDifficulty d){string key=Key(song,d);if(bestScores.TryGetValue(key,out var score))return score;var m=Meta(song,d);return bestScores[key]=Rating?.BestScore(key,m.Hash,m.Level);}
    internal void RefreshRating(){bestScores.Clear();if(Rating==null)return;ratingTotal=Rating.TotalFor(AllSongs.SelectMany(s=>s.Chart.Difficulties.Select(d=>{var m=Meta(s,d);return(Key(s,d),m.Hash,m.Level);})));}
    private void DrawScreen(CGContext c)
    {
        double now=CAAnimation.CurrentMediaTime();switch(Page){case Screen.Title:case Screen.Modes:Front(c,now);break;case Screen.Select:Select(c,now);break;case Screen.Loading:Loading(c,now);break;case Screen.Playing:case Screen.Paused:Hud(c);if(Page==Screen.Paused)PauseBox(c);break;case Screen.Result:Result(c,now);break;case Screen.Settings:Select(c,now);SettingsScreen(c);break;case Screen.Best30:BestScreen(c);break;case Screen.Practice:PracticeScreen(c);break;case Screen.Dan:DanScreen(c);break;case Screen.DanResult:DanResultScreen(c,now);break;}
        if(transitionAt>0){double e=now-transitionAt,p=e<.5?Ease(e/.5):Math.Clamp((e-.5)/.5,0,1);if(e>=.5)p=p*p*(3-2*p);double edge=-134.4+2188.8*p;c.SaveState();Polygon(c,e<.5?[0,0,edge+134.4,0,edge-134.4,1080,0,1080]:[edge+134.4,0,1920,0,1920,1080,edge-134.4,1080],clip:true);Image(visuals.Asset("FusumaSkin.png"),Rect(0,0,1920,1080),crop:true);c.RestoreState();}
        if(Page==Screen.Playing&&Session is {} s){double beat=MobileChart.BeatAtSeconds(s.Difficulty,s.Song.Chart.Bpm,Seconds-s.Song.Chart.OffsetFor(s.Difficulty));if(MobileFilters.At(s.Difficulty.FilterChanges,beat) is {Kind:"BRIGHTNESS",BackgroundOnly:false} f){var b=MobileFilters.BrightnessOverlay(f.Strength);Fill(c,Rect(0,0,1920,1080),Color(b.Tone,b.Tone,b.Tone,b.Alpha/255.0));}}
    }
    private void Particles(CGContext c,double now)
    {
        for(int i=0;i<48;i++){double age=((now-started)/(16+i*17%15)+i*.61803398875)%1,wave=(now-started)*(.55+i%5*.065)+i*2.399963,x=1920*(.025+.95*((i*.7548776662)%1))+Math.Sin(wave)*1920*(.009+i%4*.003),y=1080*(1.04-age*1.12),r=1.3+i%5*.4,a=95/255.0*Math.Max(0,Math.Min(1,Math.Min(age/.12,(1-age)/.18)))*(.8+.2*Math.Sin(wave*.7));var color=i%3==0?Color(130,205,240,a):Color(185,150,240,a);c.SetFillColor(color.CGColor);c.SetShadow(new CGSize(0,0),6,color.CGColor);c.FillEllipseInRect(Rect(x-r,y-r,r*2,r*2));}c.SetShadow(new CGSize(0,0),0,UIColor.Clear.CGColor);
    }
    private void Front(CGContext c,double now)
    {
        if(Page==Screen.Modes){Particles(c,now);ChapterScreen(c);return;}
        Particles(c,now);Polygon(c,[0,0,1280,0,960,1080,0,1080],Color(12,3,23,.12));var logo=visuals.Asset("skin/title_logo.png");double w=1920*(Page==Screen.Title?.78:.56),h=logo==null?200:w*(double)logo.Size.Height/(double)logo.Size.Width,x=(1920-w)/2,y=(Page==Screen.Title?540:285)-h/2;
        double phase=(now-started)%9.7;double Dip(double at,double width,double depth){double d=Math.Abs(phase-at)/width;return d>=1?0:depth*.5*(1+Math.Cos(Math.PI*d));}double a=Math.Clamp(.97-Dip(2.5,.38,.58)-Dip(3.18,.22,.32)-Dip(7.2,.62,.72),.2,1);
        if(Page==Screen.Title)c.SetShadow(new CGSize(0,0),28,Color(255,255,255,.45).CGColor);Image(logo,Rect(x,y,w,h),a);c.SetShadow(new CGSize(0,0),0,UIColor.Clear.CGColor);
        if(Page==Screen.Title)Text("PRESS ANY KEY",Rect(0,988,1920,65),30,center:true);else{Fill(c,Rect(680,450,560,2),Color(100,220,235,.47));string[] labels=["free play","Dan mode","Practice Mode"];for(int i=0;i<3;i++)Button(c,labels[i],Rect(650,510+i*120,620,96),35,i==mode);Button(c,"←",Rect(40,30,250,66),30);}
    }
    private IEnumerable<(string Name,int Index,CGRect Rect)> Plates()
    {
        if(Selected==null)yield break;var names=new[]{"Master","Expert","Hard","Normal","Easy"}.Concat(Selected.Chart.Difficulties.Select(x=>x.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();double step=940.0/names.Length,w=step-9,h=Math.Min(155,w*.8660254);for(int i=0;i<names.Length;i++)yield return(names[i],Selected.Chart.Difficulties.FindIndex(d=>d.Name.Equals(names[i],StringComparison.OrdinalIgnoreCase)),Rect(978+i*step,738+(155-h)/2,w,h));
    }
    private void Select(CGContext c,double now)
    {
        if(SongSortEnabled){Fill(c,Rect(25,80,215,50),Color(7,12,30,.86));for(int i=0;i<3;i++)Fill(c,Rect(34,89+i*10,32-i*10,2),UIColor.White);Text(new[]{"default","level","score"}[songSort],Rect(82,80,153,50),28);}
        if(Selected is {} song)LoadSong(song);double fade=Math.Clamp((now-fadeAt)/.5,0,1);Image(previousBackground,Rect(0,0,1920,1080),fade<1?Settings.PreviewBackgroundBrightness/100.0:0,crop:true);Image(selectionBackground,Rect(0,0,1920,1080),fade*Settings.PreviewBackgroundBrightness/100.0,crop:true);Particles(c,now);Polygon(c,[0,0,832,0,960,1080,0,1080],Color(7,4,15,.35));Fill(c,Rect(0,461,761,81),Color(240,236,247,.333));
        if(Songs.Count>0){if(!dragging){double delta=index-position;position+=delta*.18;if(Math.Abs(delta)<.001)position=index;}int center=(int)Math.Round(position);
            for(int offset=-7;offset<=7;offset++){int pos=center+offset;if(pos<0||pos>=Songs.Count)continue;int i=pos;double top=461+(pos-position)*92;if(top+86<0||top>1080)continue;var s=Songs[i];var d=s.Chart.Difficulties.FirstOrDefault(d=>d.Name.Equals(SelectedDifficulty?.Name,StringComparison.OrdinalIgnoreCase))??s.Chart.Difficulties.FirstOrDefault();double x=1920*(.035+.065*((top+43)/1080-.464)),width=1920*.410;visuals.CachedText(s.Chart.Title,Rect(x,top,width-176,65),50,center:true);visuals.CachedText(s.Chart.Composer,Rect(x,top+62,width-176,24),16,center:true,opacity:.8);Text(d?.DisplayLevel??"–",Rect(x+width-96,top,96,86),53,Difficulty(d?.Name??""),true);if(d!=null&&Best(s,d) is int best&&Rank(best).Length>0)Image(MobileRank(best,FullCombo(s,d)),Rect(x+width-164,top+9,68,68));}
            Image(jacket,Rect(1152,82,582,582));Fill(c,Rect(1152,677,582,45),Color(5,4,9,.63));Text("HIGH SCORE  "+(SelectedDifficulty!=null&&Best(Selected!,SelectedDifficulty) is int high?high.ToString("N0"):"—"),Rect(1152,677,582,45),26,center:true);
            foreach(var p in Plates()){var r=p.Rect;double x=(double)r.X,y=(double)r.Y,w=(double)r.Width,h=(double)r.Height;double[] points=[x+w/4,y,x+w*3/4,y,x+w,y+h/2,x+w*3/4,y+h,x+w/4,y+h,x,y+h/2];Polygon(c,points,Color(0,0,0,p.Index<0?.59:.94));if(p.Index==difficulty){c.SaveState();c.SetStrokeColor(Difficulty(p.Name).CGColor);c.SetLineWidth(3);c.BeginPath();c.MoveTo((nfloat)points[0],(nfloat)points[1]);for(int i=2;i<points.Length;i+=2)c.AddLineToPoint((nfloat)points[i],(nfloat)points[i+1]);c.ClosePath();c.StrokePath();c.RestoreState();}Text(DifficultyName(p.Name).ToUpperInvariant(),Rect(x,y+h*.18,w,h*.36),Math.Min(25,w/7),Difficulty(p.Name),true,p.Index<0?.4:1);if(p.Index>=0)Text(Selected!.Chart.Difficulties[p.Index].DisplayLevel,Rect(x,y+h*.48,w,h*.38),34,Difficulty(p.Name),true);}
        }else Text("楽曲ZIPを追加してください",Rect(100,430,690,100),32,center:true);
        Button(c,"play",Rect(1023,945,338,107),57);Button(c,mode==2?"モード選択":"replay",Rect(1373,945,338,107),mode==2?34:57);Fill(c,Rect(1757,970,110,110),Color(5,4,9,.63));Image(visuals.GearImage(),Rect(1778,991,68,68));Button(c,"←",Rect(100,12,160,64),26);Stroke(c,Rect(32,26,22,22),Color(187,178,210),2);Fill(c,Rect(54,50,3,15),Color(187,178,210));Fill(c,Rect(0,924,120,80),Color(5,3,12,.7));Fill(c,Rect(0,924,120,2),Color(210,214,232,.5));Fill(c,Rect(0,1002,120,2),Color(210,214,232,.5));Fill(c,Rect(118,924,2,80),Color(210,214,232,.5));Text("＋",Rect(0,924,120,80),46,center:true);if(Settings.AutoPlay)Text("AUTO",Rect(970,900,150,40),19,UIColor.Yellow);Name(c,false);
    }
    private void Name(CGContext c,bool result){if(Page==Screen.Select&&Selected?.Chart.LinkFor(SelectedDifficulty) is {Length:>0}){Fill(c,Rect(1664,96,56,56),Color(8,12,29,.65));Image(visuals.Asset("external-link.png"),Rect(1664,96,56,56));}if(!UserProfile.ValidName(UserName))return;if(result){Polygon(c,[0,0,420,0,388,48,0,48],Color(5,3,12,.82));Text(UserName,Rect(25,0,355,48),UserName.Length>15?22:30);}else{Polygon(c,[0,1018,240,1018,278,1080,0,1080],Color(5,3,12,.82));Text(UserName,Rect(25,1018,208,62),29);}BadgeHeader(c);BadgeOverlay(c,CAAnimation.CurrentMediaTime());}
    private void Loading(CGContext c,double now)
    {
        double e=now-loadingAt,exit=exitAt==0?0:Math.Clamp((now-exitAt)/.5,0,1),p=exitAt==0?Ease(e/.5):exit*exit*(3-2*exit),edge=-134.4+2188.8*p;c.SaveState();Polygon(c,exitAt==0?[0,0,edge+134.4,0,edge-134.4,1080,0,1080]:[edge+134.4,0,1920,0,1920,1080,edge-134.4,1080],clip:true);Fill(c,Rect(0,0,1920,1080),Color(9,12,24));Image(visuals.Asset("FusumaSkin.png"),Rect(0,0,1920,1080),crop:true);
        if(e>.5){c.SaveState();double spread=626.4*Ease((e-.5)/.3),cx=543.6,cy=540,reach=1252.8;Polygon(c,[cx-reach-spread,cy-reach,cx-reach+spread,cy-reach,cx+reach+spread,cy+reach,cx+reach-spread,cy+reach],clip:true);Image(jacket,Rect(230.4,226.8,626.4,626.4),crop:true);Stroke(c,Rect(230.4,226.8,626.4,626.4),Difficulty(Session?.Difficulty.Name??loadingDifficulty?.Name??SelectedDifficulty?.Name??""),4);c.RestoreState();}
        var chart=Session?.Song.Chart??loadedSong?.Chart;var d=Session?.Difficulty??loadingDifficulty??SelectedDifficulty;string[] lines=[chart?.Title??"",chart?.Composer??"","DESIGNER: "+(chart?.DesignerFor(d)??""),d==null?"":DifficultyName(d.Name)+" Lv."+d.DisplayLevel];double[] sizes=[56.16,35.64,31.32,38.88];for(int i=0;i<4;i++){double reveal=Ease((e-.8-i*.1)/.1);if(reveal<=0)continue;c.SaveState();var r=Rect(979.2,1080*(.29+i*.105),825.6,102.6);c.ClipToRect(Rect((double)r.X,(double)r.Y,(double)r.Width*reveal,(double)r.Height));Text(lines[i],r,sizes[i]);c.RestoreState();}c.RestoreState();
    }
    private void Hud(CGContext c)
    {
        if(Session is not {} s)return;
        double beat=MobileChart.BeatAtSeconds(s.Difficulty,s.Song.Chart.Bpm,Seconds-s.Song.Chart.OffsetFor(s.Difficulty));
        double uiOpacity=IosLaneRenderer.Visibility(s.Difficulty.UiVisibilityChanges,beat);
        c.SaveState();c.SetAlpha((nfloat)uiOpacity);
        // UIImage.Draw supplies its own alpha, so pass the HUD opacity explicitly.
        // Reset the context alpha for these images to avoid applying a fade twice.
        c.SaveState();c.SetAlpha(1);
        Image(visuals.Asset("HUDPanel.png"),Rect(-34,38,576,324),uiOpacity);Image(jacket,Rect(78,119,156,156),uiOpacity,crop:true);
        c.RestoreState();
        Stroke(c,Rect(78,119,156,156),Difficulty(s.Difficulty.Name),3);
        Text(s.Song.Chart.Title,Rect(258,141,530,49),40);Text(s.Song.Chart.Composer,Rect(255,192,520,25),16);
        Text("chart "+s.Song.Chart.DesignerFor(s.Difficulty),Rect(247,223,520,25),16);
        Text($"{DifficultyName(s.Difficulty.Name)} Lv.{s.Difficulty.DisplayLevel}",Rect(114,423,380,50),40);
        Fill(c,Rect(0,478,473,4),Difficulty(s.Difficulty.Name));Text(s.Score.ToString("D7"),Rect(126,505,350,57),45);
        int count=s.Counts.Sum();double acc=count==0?100:(s.Counts[0]+.75*s.Counts[1]+.5*s.Counts[2]+.25*s.Counts[3])*100/count;
        Text($"ACCURACY  {acc:0.00}%",Rect(108,579,310,26),20,UIColor.Cyan);
        string[] labels=["PG","GR","GD","BD","MS"];
        for(int i=0;i<5;i++){double y=615+i*42;Text(labels[i],Rect(110,y,70,30),24);Text(s.Counts[i].ToString(),Rect(192,y,80,30),24);Text($"{(count==0?0:s.Counts[i]*100.0/count):0.##}%",Rect(277,y,110,30),24);Fill(c,Rect(0,647+i*42,359,2),Color(44,102,128));}
        Text("Max combo",Rect(49,823,230,30),24);Text(s.MaxCombo.ToString(),Rect(285,823,90,30),24);Fill(c,Rect(0,857,359,2),Color(44,102,128));
        if(ActiveDan==null){Stroke(c,Rect(1780,299,85,735),Color(170,220,255));Fill(c,Rect(1790,1027-723*s.Gauge/100,65,723*s.Gauge/100),UIColor.Cyan);Text($"{s.Gauge:0}",Rect(1780,1044,85,24),16,center:true);double gauge80=1027-723*.8;Fill(c,Rect(1744,gauge80,159,4),Color(255,190,54));Text("80",Rect(1700,gauge80-13,40,24),16,UIColor.Yellow);}
        if(s.Combo>0){Text(s.Combo.ToString("N0"),Rect(760,626,400,60),45,center:true);Text("COMBO",Rect(760,682,400,25),16,UIColor.Cyan,true);}
        if(Seconds<s.JudgeUntilSeconds)Image(visuals.Judgement(s.LastJudgement),Rect(860,849,200,30));
        if(Settings.AutoPlay)Image(visuals.Judgement("AUTO"),Rect(28,978,110,30));
        Text("Ⅱ",Rect(1820,24,80,60),36);c.RestoreState();DanHud(c);
        if(PracticePlaying)Button(c,"↻",Rect(20,18,90,70),42);
        if(!Settings.LaneTouch)foreach(int lane in gpu.Held){if(lane<1||lane>s.Difficulty.Lanes)continue;var z=TouchZones.GetRect(s.Difficulty.Lanes,lane-1,1920,1080,Settings.TouchHeight);Fill(c,Rect(z.Left,z.Top,z.Right-z.Left,z.Bottom-z.Top),Color(80,220,226,.18));}
    }
    private int HitPlayLane(float x,float y)
    {
        var s=Session;if(s is null)return -1;
        if(!Settings.LaneTouch)return TouchZones.HitTest(s.Difficulty.Lanes,x,y,1920,1080,Settings.TouchHeight);
        double beat=MobileChart.BeatAtSeconds(s.Difficulty,s.Song.Chart.Bpm,Seconds-s.Song.Chart.OffsetFor(s.Difficulty));
        return MobileProjection.At(s.Difficulty,beat).HitLane(s.Difficulty.Lanes,x,y);
    }
    private void PauseBox(CGContext c){Fill(c,Rect(0,0,1920,1080),Color(2,4,14,.68));Polygon(c,[-100,300,2020,300,1900,770,-220,770],Color(7,8,24,.8));Text("HYPER LANE  /  BREAK",Rect(70,74,660,46),21,opacity:.65);Text("PAUSE",Rect(510,330,900,80),64,center:true);Text("一時停止",Rect(710,416,500,35),23,center:true,opacity:.8);string[] labels=["曲選択へ戻る","再開","リトライ"],icons=["⇥","▶","↻"];for(int i=0;i<3;i++){double x=430+i*380;Text(icons[i],Rect(x,495,300,105),76,center:true);Text(labels[i],Rect(x,618,300,52),30,center:true);}Button(c,"設定",Rect(1740,60,110,130),25);if(Session is {} s){Text(s.Song.Chart.Title,Rect(110,875,1290,75),42);Text(DifficultyName(s.Difficulty.Name)+" Lv."+s.Difficulty.DisplayLevel,Rect(110,950,1290,50),26,opacity:.7);}}
    private void LegacyResult(CGContext c,double now)
    {
        if(Session is not {} s)return;double t=now-resultAt;double Phase(double at,double d)=>Math.Clamp((t-at)/d,0,1);double info=Phase(1,1),band=Phase(2,.5),bg=Ease(Phase(1,.7));Fill(c,Rect(0,0,1920,1080),Color(24,3,33,Phase(0,1)));c.SaveState();Polygon(c,[282,0,1898,0,1635,1080,22,1080],clip:true);Image(selectionBackground??visuals.Blur(visuals.Asset("skin/background.png"),"default"),Rect(0,-155*(1-bg),1920,1080),bg,crop:true);Fill(c,Rect(0,0,1920,1080),Color(6,4,16,.235*bg));c.RestoreState();Image(jacket,Rect(350,108,406,406),info,crop:true);Stroke(c,Rect(346,104,414,414),Difficulty(s.Difficulty.Name).ColorWithAlpha((nfloat)info),8);Text(s.Song.Chart.Title,Rect(790,92,1040,94),79,opacity:info);Text(s.Song.Chart.Composer,Rect(790,174,960,70),50,opacity:info);Text("DESIGNER: "+s.Song.Chart.DesignerFor(s.Difficulty),Rect(793,257,960,56),39,opacity:info);Text(DifficultyName(s.Difficulty.Name)+" Lv."+s.Difficulty.DisplayLevel,Rect(792,464,650,66),42,opacity:info);Polygon(c,[127,642,1742,642,1635,1080,22,1080],Color(8,7,14,.78*band));Polygon(c,[112,641,427,641,507,692,427,743,103,743],Color(8,7,14,.84*band));Text(s.Score.ToString("D7"),Rect(148,648,328,83),70,opacity:band);string[] labels=["PERFECT","GREAT","GOOD","BAD","MISS","MAX COMBO"];for(int i=0;i<6;i++){double a=Phase(2.5+i*.1,.1),y=763+i*45;Text(labels[i],Rect(145,y,260,46),35,opacity:a);Text((i==5?s.MaxCombo:s.Counts[i]).ToString(),Rect(405,y,63,46),31,opacity:a);}
        double rp=Phase(3.3,.5),scale=1.5-.5*Ease(rp);Image(visuals.Asset("skin/rank_box.png"),Rect(682-152*scale,874-152*scale,304*scale,304*scale),rp);string rank=Rank(s.Score);if(rank.Length>0)Image(MobileRank(s.Score,RankRules.IsFullCombo(s.Counts)),Rect(682-112*scale,874-112*scale,224*scale,224*scale),rp);double line=Phase(3.1,.2);Fill(c,Rect(911,1023,227,3),Color(255,255,255,line));Text("late",Rect(958,1022,76,37),26,Color(255,142,77),opacity:line);Text("fast",Rect(1040,1022,76,37),26,Color(88,216,220),opacity:line);double red=Phase(3.8,.2);for(int i=0;i<45;i++){Fill(c,Rect(899+i*2,717,2,306),Color(200,24,31,.686*red*i/44));Fill(c,Rect(1052+i*2,717,2,306),Color(200,24,31,.608*red*(1-i/44.0)));}double graph=Phase(4,.5);int[] bins=new int[12];foreach(double offset in s.TimingOffsets)bins[Math.Clamp((int)Math.Floor((120-offset)/20),0,11)]++;int max=Math.Max(1,bins.Max());for(int i=0;i<12;i++){double h=230.0*bins[i]/max*Ease(graph);Fill(c,Rect(924+i*16,1022-h,16,h),(i<5?Color(255,142,77):i>6?Color(88,216,220):UIColor.White).ColorWithAlpha((nfloat)graph));}string[] actions=["曲選択","リトライ","見返す"];for(int i=0;i<3;i++){var r=Rect(1250,694+i*91,387,89);Fill(c,r,Color(4,3,10,.8*Phase(4.5,.4)));Stroke(c,r,Color(25,4,34,.92*Phase(4.5,.4)),4);Text(actions[i],r,42,center:true,opacity:Phase(4.5,.4));}Name(c,true);
    }
    private void Touch(NSSet changed,int phase)
    {
        foreach(UITouch touch in changed){var p=touch.LocationInView(this);float x=(float)((p.X-Origin.X)/Scale),y=(float)((p.Y-Origin.Y)/Scale);bool release=phase==2;
            if(TouchBadge(x,y,phase))continue;
            if(DanFailureStrength>0||DanReturnAt>0)continue;
            if(Page==Screen.Select&&x>=1664&&x<=1720&&y>=96&&y<=152&&Selected?.Chart.LinkFor(SelectedDifficulty) is {Length:>0} url){if(release)UIApplication.SharedApplication.OpenUrl(new NSUrl(url),new UIApplicationOpenUrlOptions(),null);continue;}
            if(x<0||x>1920||y<0||y>1080){
                if(Page==Screen.Playing){touches.Remove(touch);var remaining=touches.Values.Where(l=>l>0).ToHashSet();gpu.Held=remaining;InputChanged?.Invoke(remaining,touch.Timestamp);}
                if(release)dragging=false;continue;
            }
            if(Page==Screen.Playing){if(PracticePlaying&&x<110&&y<88){if(release)RetryRequested?.Invoke();continue;}if(release&&x>1766&&y<130){PauseRequested?.Invoke();continue;}if(release||Settings.AutoPlay)touches.Remove(touch);else touches[touch]=HitPlayLane(x,y)+1;var next=touches.Values.Where(l=>l>0).ToHashSet();gpu.Held=next;InputChanged?.Invoke(next,touch.Timestamp);continue;}
            if(Page==Screen.Settings){TouchSettings(x,y,phase);continue;}
            if(Page==Screen.Practice){TouchPractice(x,y,phase);continue;}
            if(Page==Screen.Modes){TouchChapter(x,y,phase);continue;}
            if(Page==Screen.Select&&x<95&&y<80){if(release)SearchRequested?.Invoke();continue;}
            if(Page==Screen.Select&&SongSortEnabled&&x>=25&&x<=240&&y>=80&&y<=130){dragging=false;if(release){var old=Selected;string name=SelectedDifficulty?.Name??"";songSort=(songSort+1)%3;ApplySearch(SongSearch);if(old!=null){index=Math.Max(0,Songs.ToList().IndexOf(old));difficulty=Math.Max(0,Selected?.Chart.Difficulties.FindIndex(d=>d.Name.Equals(name,StringComparison.OrdinalIgnoreCase))??0);}position=index;}continue;}
            if(Page==Screen.Select&&mode==2&&release&&x>=1023&&x<=1361&&y>=945)practiceViewBeat=0;
            if(phase==0){downY=y;downPosition=Page==Screen.Practice?practiceBeat:position;dragging=Page==Screen.Select&&x<960&&y>80&&!(x<=120&&y>=924);}
            if(phase==1){if(dragging){position=Math.Clamp(downPosition+(downY-y)/92,0,Math.Max(0,Songs.Count-1));if(Songs.Count>0){string name=SelectedDifficulty?.Name??"";int pos=(int)Math.Round(position);index=Math.Clamp(pos,0,Songs.Count-1);difficulty=Math.Max(0,Selected!.Chart.Difficulties.FindIndex(d=>d.Name.Equals(name,StringComparison.OrdinalIgnoreCase)));}}if(Page==Screen.Practice)practiceBeat=Math.Max(0,downPosition+(y-downY)/65);continue;}if(!release||transitionAt>0||Page==Screen.Loading)continue;
            if(Page==Screen.Title){if(UserProfile.ValidName(UserName))Transition(Screen.Modes);else NameRequested?.Invoke();}
            else if(Page==Screen.Modes){if(x<300&&y<120)Transition(Screen.Title);else if(x>=650&&x<=1270&&y>=510&&y<=846){mode=Math.Clamp((int)((y-510)/120),0,2);if(mode==1)DanRequested?.Invoke();else Transition(Screen.Select);}}
            else if(Page==Screen.Select){bool dragged=dragging&&Math.Abs(y-downY)>35;dragging=false;if(x>=1688&&y<62)Page=Screen.Best30;else if(x>=100&&x<=260&&y<76)Transition(Screen.Modes);else if(x>=0&&x<=120&&y>=924&&y<=1004)ImportRequested?.Invoke();else if(x>=1757&&x<=1867&&y>=970){settingsReturn=Page;Page=Screen.Settings;}else if(x>=1023&&x<=1361&&y>=945){if(mode==2){Page=Screen.Practice;practiceBeat=0;}else if(Selected!=null&&SelectedDifficulty!=null)StartRequested?.Invoke(Selected,SelectedDifficulty);}else if(x>=1373&&x<=1711&&y>=945){if(mode==2)Transition(Screen.Modes);else ReplayRequested?.Invoke();}else if(x>=978&&y>=738&&y<=893){foreach(var slot in Plates())if(slot.Index>=0&&slot.Rect.Contains(x,y))difficulty=slot.Index;}else if(x<960&&Songs.Count>0){int pos=dragged?(int)Math.Round(position):(int)Math.Round(position+(y-501.5)/92);string name=SelectedDifficulty?.Name??"";index=Math.Clamp(pos,0,Songs.Count-1);difficulty=Math.Max(0,Selected!.Chart.Difficulties.FindIndex(d=>d.Name.Equals(name,StringComparison.OrdinalIgnoreCase)));}}
            else if(Page==Screen.Paused){if(x>=1740&&x<=1850&&y>=60&&y<=190){settingsReturn=Page;Page=Screen.Settings;}else if(y>=480&&y<=700){if(x>=810&&x<=1110)ResumeRequested?.Invoke();else if(x>=1190&&x<=1490&&Session!=null)RetryRequested?.Invoke();else if(x>=430&&x<=730)BackRequested?.Invoke();}}
            else if(Page==Screen.Result){if(CAAnimation.CurrentMediaTime()-resultAt<1.5)continue;if(y<48&&x>=1720)Page=Screen.Best30;else if(y>=960&&y<=1040){if(x>=850&&x<=1070)BackRequested?.Invoke();else if(x>=1085&&x<=1305&&Session!=null)StartRequested?.Invoke(Session.Song,Session.Difficulty);else if(x>=1320&&x<=1540)ReplayRequested?.Invoke();}}
            else if(Page==Screen.Best30){if(x>=1700&&y>=1000)Page=Screen.Select;}
            else if(Page==Screen.Practice){if(x>=1700&&y>=980)Page=Screen.Select;else if(x<=210&&y>=920&&Selected!=null&&SelectedDifficulty!=null)PracticeRequested?.Invoke(Selected,SelectedDifficulty,practiceBeat);}
            else if(Page==Screen.Dan||Page==Screen.DanResult)TouchDan(x,y);
        }
    }
    public override void TouchesBegan(NSSet t,UIEvent? e)=>Touch(t,0);public override void TouchesMoved(NSSet t,UIEvent? e)=>Touch(t,1);public override void TouchesEnded(NSSet t,UIEvent? e)=>Touch(t,2);
    public override void TouchesCancelled(NSSet t,UIEvent? e){ClearTouches();InputChanged?.Invoke(new HashSet<int>(),CAAnimation.CurrentMediaTime());dragging=false;practiceDragging=false;}
    protected override void Dispose(bool disposing){if(disposing){effectCanvas?.Dispose();underlayCanvas?.Dispose();effectVideo?.Dispose();movieOutput?.Dispose();movie?.Dispose();movieLayer?.Dispose();gpu.Dispose();visuals.Dispose();}base.Dispose(disposing);}
}
