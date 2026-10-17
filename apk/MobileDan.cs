using Android.Graphics;
using Android.Views;
using System.Text.Json;
using HyperLane;

namespace HyperLane.Android;

internal sealed record MobileDanCourse(string Folder,DanDefinition Definition,List<(MobileSong Song,MobileDifficulty Difficulty)> Tracks);
internal sealed record MobileDanResult(MobileSong Song,MobileDifficulty Difficulty,int Score,int[] Counts,int Combo,double Gauge,double GlobalGauge,double[] Timing);

internal sealed partial class GameSurface
{
    private bool danMode,danPlaying,danEligible;
    private List<MobileDanCourse> danCourses=[];
    private int danIndex,danTrackIndex;
    private MobileDanCourse? activeDan;
    private readonly List<MobileDanResult> danResults=[];
    private readonly int[] danCounts=new int[5];
    private double danGlobalGauge;
    private HashSet<string> danPassed=new(StringComparer.OrdinalIgnoreCase),danRevealed=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,Bitmap?> danJackets=new();
    private string DanPath(string name)=>System.IO.Path.Combine(Context!.FilesDir!.AbsolutePath,name);
    private int DanScoreUnits=>activeDan==null?1:Math.Max(1,activeDan.Tracks.Sum(t=>MobileSession.ScoreUnits(t.Difficulty)));
    private MobileDanCourse? SelectedMobileDan=>danCourses.Count==0?null:danCourses[Math.Clamp(danIndex,0,danCourses.Count-1)];
    private string danLoadError="";
    private void LoadMobileDanCourses()
    {
        danCourses.Clear();foreach(var image in danJackets.Values)image?.Dispose();danJackets.Clear();
        HashSet<string> LoadSet(string name){try{return new(JsonSerializer.Deserialize<string[]>(File.ReadAllText(DanPath(name)))??[],StringComparer.OrdinalIgnoreCase);}catch{return new(StringComparer.OrdinalIgnoreCase);}}
        danPassed=LoadSet("dan-progress.json");danRevealed=LoadSet("dan-progress.revealed.json");
        string root=DanPath("song");Directory.CreateDirectory(root);var errors=new List<string>();
        foreach(string json in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Where(p=>System.IO.Path.GetFileName(p).Equals("Dan.json",StringComparison.OrdinalIgnoreCase)).Order()){
            try{
                var definition=DanDefinition.Load(json);string folder=System.IO.Path.GetDirectoryName(json)!;
                var tracks=new List<(MobileSong,MobileDifficulty)>();
                foreach(var track in definition.Tracks){
                    string path=DanDefinition.TrackPath(folder,track.Chart.Replace('\\','/'));var chart=MobileChart.Parse(File.ReadAllText(path));
                    var diff=chart.Difficulties.FirstOrDefault(d=>DifficultyName(d.Name).Equals(DifficultyName(track.Difficulty),StringComparison.OrdinalIgnoreCase))??throw new InvalidDataException(track.Difficulty+" 譜面がありません");
                    string baseFolder=System.IO.Path.GetDirectoryName(path)!;
                    if(!string.IsNullOrWhiteSpace(diff.Audio)?!File.Exists(chart.DifficultyAudioPath(baseFolder,diff)):FindMedia(baseFolder,chart.Audio,".ogg",".wav",".mp3",".m4a").Length==0)throw new InvalidDataException(chart.Title+" の音源がありません");
                    tracks.Add((new MobileSong(path,baseFolder,chart),diff));
                }
                danCourses.Add(new(folder,definition,tracks));
            }catch(Exception ex){errors.Add(System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(json))+": "+ex.Message);}
        }
        danIndex=Math.Clamp(danIndex,0,Math.Max(0,danCourses.Count-1));danLoadError=string.Join(" / ",errors);
    }
    private static string MobileDanRevealKey(MobileDanCourse course,int index)=>course.Definition.Id+"\n"+course.Definition.Tracks[index].Chart.Replace('\\','/')+"\n"+course.Definition.Tracks[index].Difficulty;
    private bool MobileDanVisible(MobileDanCourse course,int index)=>course.Definition.Tracks[index].ShowTitle||danPassed.Contains(course.Definition.Id)||danRevealed.Contains(MobileDanRevealKey(course,index));
    private void SaveDanSet(string name,HashSet<string> set){string path=DanPath(name);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(set));File.Move(path+".tmp",path,true);}
    private void RevealMobileDanTrack()
    {
        if(!danPlaying||activeDan==null||!danRevealed.Add(MobileDanRevealKey(activeDan,danTrackIndex)))return;
        try{SaveDanSet("dan-progress.revealed.json",danRevealed);}catch(Exception ex){message="公開記録を保存できません: "+ex.Message;}
    }
    private void BeginMobileDan()
    {
        activeDan=SelectedMobileDan;if(activeDan==null)return;
        danResults.Clear();Array.Clear(danCounts);danTrackIndex=0;danGlobalGauge=activeDan.Definition.InitialGauge;
        danEligible=!settings.AutoPlay;danPlaying=true;practiceMode=practicePlaying=false;pendingMobileReplay=null;page=Page.Select;StartSelected();
    }
    private List<(string Label,bool Passed)> MobileDanChecks(bool intermediate=false)
    {
        if(activeDan==null)return [];
        return MobileDanRules.Checks(activeDan.Definition,danCounts,danResults.Select(r=>r.GlobalGauge).ToArray(),danResults.Select(r=>(r.Counts,r.Gauge)).ToArray(),intermediate);
    }
    private void FinishMobileDanTrack()
    {
        if(activeDan==null||session==null)return;
        RevealMobileDanTrack();danEligible&=!settings.AutoPlay;
        danResults.Add(new(session.Song,session.Difficulty,session.Score,(int[])session.Counts.Clone(),session.MaxCombo,session.Gauge,session.GlobalGauge,session.TimingOffsets.ToArray()));
        danGlobalGauge=session.GlobalGauge;for(int i=0;i<5;i++)danCounts[i]+=session.Counts[i];
        StopVideo();player?.Stop();playbackClock.Stop();
        bool completed=danResults.Count==activeDan.Tracks.Count;
        bool passed=MobileDanChecks(!completed).All(c=>c.Passed);
        if(passed&&!completed){danTrackIndex++;page=Page.Select;StartSelected();return;}
        if(completed&&passed&&danEligible){danPassed.Add(activeDan.Definition.Id);try{SaveDanSet("dan-progress.json",danPassed);}catch(Exception ex){message="合格記録を保存できません: "+ex.Message;}}
        resultBlur?.Dispose();resultBlur=(background??menuBackground) is {} source?MobileResultBackground.Create(source):null;
        page=Page.DanResult;resultStartedAt=Environment.TickCount64;BeginDanResultTransition();Invalidate();
    }
    private Bitmap? MobileDanJacket(MobileSong song)
    {
        if(danJackets.TryGetValue(song.ChartPath,out var image))return image;
        string path=FindMedia(song.Folder,song.Chart.Jacket,".png",".jpg",".jpeg",".webp");
        try{image=path.Length==0?null:DecodeMobileThumbnail(path,300);}catch{image=null;}
        danJackets[song.ChartPath]=image;return image;
    }
    private void DrawMobileDanSelect(Canvas canvas)
    {
        DrawMenuBackground(canvas);DrawMenuParticles(canvas);
        using(var wall=Polygon([0,0,832,0,960,1080,0,1080])){paint.Color=Color.Argb(90,7,4,15);paint.SetStyle(Paint.Style.Fill);canvas.DrawPath(wall,paint);}
        DrawSelectionHighlight(canvas);
        for(int i=Math.Max(0,danIndex-5);i<Math.Min(danCourses.Count,danIndex+6);i++){
            float top=461+(i-danIndex)*92;FitMobileText(canvas,danCourses[i].Definition.Title,new RectF(40,top,720,top+81),48,1,true);
        }
        FitMobileText(canvas,"Dan mode",new RectF(1010,35,1830,100),36,1);
        var course=SelectedMobileDan;
        if(course!=null){
            FitMobileText(canvas,course.Definition.Title+(danPassed.Contains(course.Definition.Id)?"  CLEAR":""),new RectF(1010,110,1830,185),46,1);
            float row=Math.Min(110,400f/course.Tracks.Count);
            for(int i=0;i<course.Tracks.Count;i++){
                var track=course.Tracks[i];bool visible=MobileDanVisible(course,i);float y=210+i*row,side=Math.Min(82,row-16);
                if(visible&&MobileDanJacket(track.Song) is {} image)DrawMobileSquareImage(canvas,image,new RectF(1010,y+5,1010+side,y+5+side),1);
                string ordinal=i==0?"1st":i==1?"2nd":i==2?"3rd":$"{i+1}th";
                FitMobileText(canvas,$"{ordinal}  {(visible?track.Song.Chart.Title:"???")}",new RectF(1110,y,1835,y+row*.44f),Math.Min(30,row*.33f),1);
                FitMobileText(canvas,$"{DifficultyName(track.Difficulty.Name)} Lv.{track.Difficulty.DisplayLevel}",new RectF(1110,y+row*.44f,1820,y+row*.72f),Math.Min(22,row*.23f),1);
                if(course.Definition.Tracks[i].Conditions is {} own)FitMobileText(canvas,string.Join(" / ",own.Descriptions()),new RectF(1110,y+row*.75f,1820,y+row*.97f),Math.Min(16,row*.16f),1);
            }
            ResultText(canvas,"合格条件",new RectF(1010,620,1830,665),30,1);
            int line=0;foreach(string condition in course.Definition.Conditions.Descriptions())FitMobileText(canvas,condition,new RectF(1010,675+line*40,1830,715+line++*40),24,1);
            ResultText(canvas,course.Definition.CarryGauge?"曲別ゲージ引き継ぎ ON":"曲別ゲージ引き継ぎ OFF",new RectF(1010,885,1810,913),20,1);
        }else FitMobileText(canvas,danLoadError.Length>0?danLoadError:"段位ZIPを取り込んでください（Dan.jsonと課題曲）",new RectF(1000,220,1830,320),26,1);
        SelectionButton(canvas,"play",new RectF(1023,945,1361,1052),42);SelectionButton(canvas,"⚙",new RectF(1757,935,1867,1080),60);
        if(settings.AutoPlay)FitMobileText(canvas,"AUTO使用中のため記録は保存されません。",new RectF(1010,915,1840,940),20,1);
        ScreenButton(canvas,"←",new RectF(100,12,260,76),26);
        ScreenButton(canvas,"＋ 段位を追加・更新",new RectF(978,12,1398,108),30);
        Fill(canvas,new RectF(1688,0,1920,62),Color.Argb(160,5,4,9));ResultText(canvas,$"RATE {ratingTotal:0.00}",new RectF(1688,0,1920,62),28,1,Paint.Align.Center);
        DrawMobileUserName(canvas,false);PostInvalidateDelayed(33);
    }
    private bool TouchMobileDanSelect(MotionEvent e,float x,float y)
    {
        if(e.ActionMasked==MotionEventActions.Down){selectionDownY=y;selectionDragging=x<960&&y>80;}
        if(e.ActionMasked==MotionEventActions.Cancel){selectionDragging=false;return true;}
        if(e.ActionMasked!=MotionEventActions.Up)return true;
        if(Hit(x,y,100,12,260,76))BeginMenuTransition(Page.Modes);
        else if(Hit(x,y,978,12,1398,108))importZip();
        else if(Hit(x,y,1688,0,1920,62))OpenMobileBest30();
        else if(Hit(x,y,1757,935,1920,1080))OpenSettingsPage(Page.Select);
        else if(Hit(x,y,1023,945,1361,1052))BeginMobileDan();
        else if(selectionDragging&&danCourses.Count>0){int move=Math.Abs(y-selectionDownY)>35?(int)Math.Round((selectionDownY-y)/92):(int)Math.Round((y-501.5)/92);danIndex=Math.Clamp(danIndex+move,0,danCourses.Count-1);}
        selectionDragging=false;Invalidate();return true;
    }
    private void DrawMobileSquareImage(Canvas canvas,Bitmap image,RectF rect,float opacity)
    {
        int crop=Math.Min(image.Width,image.Height);paint.Color=Alpha(Color.White,opacity);paint.FilterBitmap=true;
        canvas.DrawBitmap(image,new Rect((image.Width-crop)/2,(image.Height-crop)/2,(image.Width+crop)/2,(image.Height+crop)/2),rect,paint);
    }
    private sealed record MobileDanMeter(double Value,double Limit,bool Minimum,bool Exclusive=false,bool Percent=false);
    private static List<MobileDanMeter> MobileDanMeters(DanConditions c,int[] n,IReadOnlyList<double> gauges)
    {
        List<MobileDanMeter> result=[];
        if(c.GaugeMin is double g)result.Add(new(gauges.Count==0?0:c.GaugeScope=="everySong"?gauges.Min():gauges[^1],g,true,false,true));
        if(c.BadMissMaxExclusive is int bm)result.Add(new(n[3]+n[4],bm,false,true));
        if(c.BadMax is int b)result.Add(new(n[3],b,false));if(c.MissMax is int m)result.Add(new(n[4],m,false));if(c.PerfectMin is int p)result.Add(new(n[0],p,true));return result;
    }
    private void DrawMobileDanMeter(Canvas canvas,MobileDanMeter meter,RectF rect,float progress,float opacity=1)
    {
        bool passed=meter.Minimum?meter.Value>=meter.Limit:meter.Exclusive?meter.Value<meter.Limit:meter.Value<=meter.Limit;
        var color=Alpha(passed?Color.Rgb(70,180,255):Color.Rgb(255,80,90),opacity);
        Fill(canvas,rect,Alpha(Color.Argb(210,6,5,14),opacity));
        string number=meter.Percent?$"{meter.Value:0.##}%":meter.Exclusive?$"{Math.Max(0,meter.Limit-meter.Value):0.##}":$"{meter.Value:0.##} / {meter.Limit:0.##}";
        paint.SetTypeface(Typeface.Default);paint.TextSize=Math.Min(24,rect.Height()*.5f);paint.TextAlign=Paint.Align.Center;paint.Color=color;
        canvas.DrawText(number,rect.CenterX(),rect.Top+rect.Height()*.48f,paint);paint.TextAlign=Paint.Align.Left;
        var bar=new RectF(rect.Left+10,rect.Top+rect.Height()*.58f,rect.Right-10,rect.Bottom-8);Fill(canvas,bar,Alpha(Color.Argb(110,100,91,120),opacity));
        double maximum=meter.Percent?100:Math.Max(1,meter.Limit);
        double fraction=meter.Minimum?meter.Value/maximum:meter.Limit==0?(passed?1:0):1-meter.Value/maximum;
        Fill(canvas,new RectF(bar.Left,bar.Top,bar.Left+bar.Width()*(float)Math.Clamp(fraction,0,1)*progress,bar.Bottom),color);
        Stroke(canvas,bar,Alpha(Color.Rgb(180,174,198),opacity),1);
        if(meter.Minimum){float marker=bar.Left+bar.Width()*(float)Math.Clamp(meter.Limit/maximum,0,1);Line(canvas,marker,bar.Top-3,marker,bar.Bottom+3,Alpha(Color.Gold,opacity),2);}
    }
    private void DrawMobileDanHud(Canvas canvas)
    {
        if(activeDan==null||session==null)return;
        int[] live=(int[])danCounts.Clone();for(int i=0;i<5;i++)live[i]+=session.Counts[i];
        var gauges=danResults.Select(r=>r.GlobalGauge).Append(session.GlobalGauge).ToArray();
        var meters=MobileDanMeters(activeDan.Definition.Conditions,live,gauges);
        if(activeDan.Definition.Tracks[danTrackIndex].Conditions is {} own)meters.AddRange(MobileDanMeters(own,session.Counts,new[]{session.Gauge}));
        FitMobileText(canvas,$"{activeDan.Definition.Title}  {danTrackIndex+1}/{activeDan.Tracks.Count}",new RectF(1320,10,1890,46),22,1);
        float row=Math.Min(67,500f/Math.Max(1,meters.Count));for(int i=0;i<meters.Count;i++)DrawMobileDanMeter(canvas,meters[i],new RectF(1320,52+i*row,1890,52+(i+1)*row-7),1);
    }
    private void BeginDanResultTransition(){ /* Result's wipe and reveal use resultStartedAt. */ }
    private void DrawMobileDanResult(Canvas canvas)
    {
        if(activeDan==null)return;
        double elapsed=(Environment.TickCount64-resultStartedAt)/1000d;
        float Phase(double start,double duration)=>(float)Math.Clamp((elapsed-start)/duration,0,1);
        DrawMenuBackground(canvas);
        Fill(canvas,new RectF(0,0,1920,1080),Alpha(Color.Rgb(24,3,33),Phase(0,1)));
        if(resultBlur!=null){int save=canvas.Save();using(var center=Polygon([282,0,1898,0,1635,1080,22,1080]))canvas.ClipPath(center);ResultImage(canvas,resultBlur,new RectF(0,-155*(1-Phase(1,.7)),1920,1080-155*(1-Phase(1,.7))),Phase(1,.7));canvas.RestoreToCount(save);}
        FitMobileText(canvas,activeDan.Definition.Title+" — DAN RESULT",new RectF(65,65,1840,125),42,Phase(1,.5));
        float column=1170f/Math.Max(3,danResults.Count);
        for(int i=0;i<danResults.Count;i++){
            var r=danResults[i];float x=65+i*column,a=Phase(1.2+i*.15,.5);float side=Math.Min(300,column-50),jx=x+(column-20-side)/2;
            Fill(canvas,new RectF(x,130,x+column-20,1030),Alpha(Color.Argb(190,4,3,12),a));
            bool visible=MobileDanVisible(activeDan,i);
            if(visible&&MobileDanJacket(r.Song) is {} image)DrawMobileSquareImage(canvas,image,new RectF(jx,150,jx+side,150+side),a);
            else {Fill(canvas,new RectF(jx,150,jx+side,150+side),Alpha(Color.Rgb(23,16,35),a));ResultText(canvas,visible?"NO IMAGE":"???",new RectF(jx,150,jx+side,150+side),30,a,Paint.Align.Center);}
            FitMobileText(canvas,$"{i+1}. {(visible?r.Song.Chart.Title:"???")}",new RectF(x+18,465,x+column-38,510),Math.Min(26,column/15),a);
            FitMobileText(canvas,$"{DifficultyName(r.Difficulty.Name)} Lv.{r.Difficulty.DisplayLevel}",new RectF(x+18,510,x+column-38,546),22,a);
            ResultText(canvas,r.Score.ToString("N0"),new RectF(x+18,550,x+column-38,600),36,a);
            string[] labels=["PERFECT","GREAT","GOOD","BAD","MISS"];
            for(int n=0;n<5;n++)FitMobileText(canvas,$"{labels[n]}   {r.Counts[n]:N0}",new RectF(x+18,615+n*34,x+column-38,649+n*34),22,Phase(1.7+i*.15+n*.08,.2));
            FitMobileText(canvas,$"MAX COMBO   {r.Combo}",new RectF(x+18,795,x+column-38,831),21,Phase(2.2+i*.15,.3));
            int[] bins=new int[12];foreach(double offset in r.Timing)bins[Math.Clamp((int)Math.Floor((120-offset)/20),0,11)]++;
            float graph=Phase(2.5+i*.15,.6),bw=(column-85)/12;int max=Math.Max(1,bins.Max());
            for(int b=0;b<12;b++){float h=140f*bins[b]/max*(float)LoadingEase(graph);Fill(canvas,new RectF(x+35+b*bw,980-h,x+35+(b+1)*bw,980),Alpha(b<5?Color.Coral:b>6?Color.Turquoise:Color.White,graph));}
            Line(canvas,x+35,981,x+35+12*bw,981,Alpha(Color.White,graph),1);
            ResultText(canvas,"late",new RectF(x+35+4*bw,988,x+35+6*bw,1018),18,graph);ResultText(canvas,"fast",new RectF(x+35+7*bw,988,x+35+9*bw,1018),18,graph);
        }
        bool passed=danResults.Count==activeDan.Tracks.Count&&MobileDanChecks().All(c=>c.Passed);
        FitMobileText(canvas,!danEligible?"AUTOを使用したため今回の記録は保存されません。":passed?"合格 / PASSED":"不合格 / FAILED",new RectF(1300,130,1850,195),danEligible?34:20,Phase(3.3,.5));
        var meters=MobileDanMeters(activeDan.Definition.Conditions,danCounts,danResults.Select(r=>r.GlobalGauge).ToArray());
        for(int i=0;i<danResults.Count;i++)if(activeDan.Definition.Tracks[i].Conditions is {} own)meters.AddRange(MobileDanMeters(own,danResults[i].Counts,new[]{danResults[i].Gauge}));
        float spacing=Math.Min(76,660f/Math.Max(1,meters.Count));for(int i=0;i<meters.Count;i++)DrawMobileDanMeter(canvas,meters[i],new RectF(1300,215+i*spacing,1850,215+(i+1)*spacing-8),Phase(3.3,.6),Phase(3.3,.5));
        ScreenButton(canvas,"段位選択",new RectF(1300,900,1800,970),36,Phase(4,.4));ScreenButton(canvas,"リトライ",new RectF(1300,985,1800,1055),36,Phase(4,.4));DrawMobileUserName(canvas,true);
        if(elapsed<.5){float edge=(float)(-134.4+2188.8*LoadingEase(elapsed/.5));int save=canvas.Save();using(var mask=Polygon([edge+134.4f,0,1920,0,1920,1080,edge-134.4f,1080]))canvas.ClipPath(mask);Fill(canvas,new RectF(0,0,1920,1080),Color.Rgb(9,12,24));if(mobileLoadingSkin!=null)ResultImage(canvas,mobileLoadingSkin,new RectF(0,0,1920,1080),1);canvas.RestoreToCount(save);}
        if(elapsed<4.4)PostInvalidateOnAnimation();
    }
    private bool TouchMobileDanResult(MotionEvent e,float x,float y)
    {
        if(e.ActionMasked!=MotionEventActions.Up||Environment.TickCount64-resultStartedAt<4400)return true;
        if(Hit(x,y,0,0,420,48)){OpenMobileBest30();return true;}
        if(Hit(x,y,1300,900,1800,970))StopPlaying();else if(Hit(x,y,1300,985,1800,1055))BeginMobileDan();return true;
    }
}
