using CoreAnimation;
using CoreGraphics;
using HyperLane;
using HyperLane.Android;
using System.Text.Json;
using UIKit;
using static HyperLane.iOS.IosVisuals;

namespace HyperLane.iOS;

internal sealed record IosDanCourse(string Folder,DanDefinition Definition,List<(MobileSong Song,MobileDifficulty Diff)> Tracks);
internal sealed record IosDanResult(MobileSong Song,MobileDifficulty Diff,int Score,int[] Counts,int Combo,double Gauge,double GlobalGauge,double[] Timing);
internal sealed partial class GameController
{
    private IosDanCourse? activeDan;private int danTrack;private readonly List<IosDanResult> danResults=[];private readonly int[] danCounts=new int[5];private bool danEligible;
    private HashSet<string> Revealed=>LoadSet("dan-progress.revealed.json");
    private HashSet<string> LoadSet(string name){try{return new(JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(documents,name)))??[]);}catch{return new();}}
    private void OpenDan()
    {
        var courses=new List<IosDanCourse>();var errors=new List<string>();
        foreach(string path in Directory.EnumerateFiles(Path.Combine(documents,"song"),"*",SearchOption.AllDirectories).Where(p=>Path.GetFileName(p).Equals("Dan.json",StringComparison.OrdinalIgnoreCase)))
            try{var def=DanDefinition.Load(path);string folder=Path.GetDirectoryName(path)!;var tracks=new List<(MobileSong,MobileDifficulty)>();foreach(var track in def.Tracks){string chartPath=DanDefinition.TrackPath(folder,track.Chart.Replace('\\','/'));var chart=MobileChart.Parse(File.ReadAllText(chartPath));var diff=chart.Difficulties.First(d=>DifficultyName(d.Name).Equals(DifficultyName(track.Difficulty),StringComparison.OrdinalIgnoreCase));tracks.Add((new(chartPath,Path.GetDirectoryName(chartPath)!,chart),diff));}courses.Add(new(folder,def,tracks));}catch(Exception e){errors.Add(e.Message);}
        foreach(var course in courses)foreach(var track in course.Tracks)MigrationStage.PrepareMedia(track.Song);
        stage.Courses=courses;stage.Revealed=Revealed;stage.Passed=LoadSet("dan-progress.json");stage.DanStart=BeginDan;stage.Page=MigrationStage.Screen.Dan;if(errors.Count>0)Alert("段位読み込み",string.Join("\n",errors));
    }
    private void BeginDan(IosDanCourse course){RestoreReplay();activeDan=course;danTrack=0;danResults.Clear();Array.Clear(danCounts);danEligible=!settings.AutoPlay;practice=false;danLife=course.Definition.Life.Enabled?new(course.Definition.Life):null;danFailureAt=0;stage.DanFailureStrength=0;Start(course.Tracks[0].Song,course.Tracks[0].Diff);}
    private void ConfigureDanSession()
    {
        if(activeDan==null||session==null){stage.ActiveDan=null;return;}
        double previous=danResults.Count==0?activeDan.Definition.InitialGauge:danResults[^1].Gauge,global=danResults.Count==0?activeDan.Definition.InitialGauge:danResults[^1].GlobalGauge;
        session.ConfigureDanGauge(activeDan.Definition.CarryGauge?previous:activeDan.Definition.InitialGauge,activeDan.Tracks.Sum(t=>MobileSession.ScoreUnits(t.Diff)),global);
        session.Life=danLife;
        stage.ActiveDan=activeDan;stage.DanTrack=danTrack;stage.DanCounts=danCounts;stage.DanResults=danResults;
        string key=MigrationStage.RevealKey(activeDan,danTrack);var revealed=Revealed;revealed.Add(key);File.WriteAllText(Path.Combine(documents,"dan-progress.revealed.json"),JsonSerializer.Serialize(revealed));stage.Revealed=revealed;
    }
    private void FinishDan()
    {
        if(activeDan==null||session==null)return;danEligible&=!settings.AutoPlay;danResults.Add(new(session.Song,session.Difficulty,session.Score,(int[])session.Counts.Clone(),session.MaxCombo,session.Gauge,session.GlobalGauge,session.TimingOffsets.ToArray()));for(int i=0;i<5;i++)danCounts[i]+=session.Counts[i];
        bool completed=danResults.Count==activeDan.Tracks.Count;bool pass=MobileDanRules.Checks(activeDan.Definition,danCounts,danResults.Select(r=>r.GlobalGauge).ToArray(),danResults.Select(r=>(r.Counts,r.Gauge)).ToArray(),!completed).All(c=>c.Passed);
        if(pass&&!completed){danTrack++;var next=activeDan.Tracks[danTrack];Start(next.Song,next.Diff);return;}
        if(pass&&completed&&danEligible){var set=LoadSet("dan-progress.json");set.Add(activeDan.Definition.Id);File.WriteAllText(Path.Combine(documents,"dan-progress.json"),JsonSerializer.Serialize(set));stage.Passed=set;}
        stage.DanSucceeded=pass&&completed;stage.DanEligible=danEligible;stage.ShowResult();stage.Page=MigrationStage.Screen.DanResult;
    }
}
internal sealed partial class MigrationStage
{
    internal List<IosDanCourse> Courses=[];internal IosDanCourse? ActiveDan;internal int DanTrack;internal int[] DanCounts=new int[5];internal List<IosDanResult> DanResults=[];internal HashSet<string> Revealed=new(),Passed=new();internal bool DanSucceeded,DanEligible;internal Action<IosDanCourse>? DanStart;private int danIndex;
    internal static string RevealKey(IosDanCourse c,int i)=>c.Definition.Id+"\n"+c.Definition.Tracks[i].Chart.Replace('\\','/')+"\n"+c.Definition.Tracks[i].Difficulty;
    private bool Visible(IosDanCourse c,int i)=>c.Definition.Tracks[i].ShowTitle||Passed.Contains(c.Definition.Id)||Revealed.Contains(RevealKey(c,i));
    private void DanScreen(CGContext c)
    {
        Particles(c,CAAnimation.CurrentMediaTime());Polygon(c,[0,0,832,0,960,1080,0,1080],Color(7,4,15,.35));Fill(c,Rect(0,461,761,81),Color(240,236,247,.333));
        for(int i=Math.Max(0,danIndex-5);i<Math.Min(Courses.Count,danIndex+6);i++)Text(Courses[i].Definition.Title,Rect(40,461+(i-danIndex)*92,680,81),48,center:true);
        if(Courses.Count>0){var course=Courses[danIndex];Text(course.Definition.Title,Rect(1010,110,820,75),46);double row=Math.Min(110,400.0/course.Tracks.Count);for(int i=0;i<course.Tracks.Count;i++){var t=course.Tracks[i];bool visible=Visible(course,i);double y=210+i*row,side=Math.Min(82,row-16);if(visible)Image(visuals.FileImage(Media(t.Song,t.Song.Chart.Jacket)),Rect(1010,y+5,side,side),crop:true);string ordinal=i==0?"1st":i==1?"2nd":i==2?"3rd":$"{i+1}th";Text(ordinal+"  "+(visible?t.Song.Chart.Title:"???"),Rect(1110,y,725,row*.44),Math.Min(30,row*.33));Text(DifficultyName(t.Diff.Name)+" Lv."+t.Diff.DisplayLevel,Rect(1110,y+row*.44,710,row*.28),22);if(course.Definition.Tracks[i].Conditions is {} own)Text(string.Join(" / ",own.Descriptions()),Rect(1110,y+row*.75,710,row*.22),16);}
            Text("合格条件",Rect(1010,620,820,45),30);int line=0;foreach(var condition in course.Definition.Conditions.Descriptions())Text(condition,Rect(1010,675+line++*40,820,40),24);Text(course.Definition.CarryGauge?"曲別ゲージ引き継ぎ ON":"曲別ゲージ引き継ぎ OFF",Rect(1010,885,800,28),20);}
        else Text("段位ZIPを取り込んでください",Rect(1010,300,800,80),32);
        Button(c,"play",Rect(1023,945,338,107),42);Button(c,"←",Rect(100,12,160,64),32);Button(c,"＋ 段位ZIPを取り込む",Rect(978,12,420,96),30);if(Settings.AutoPlay)Text("AUTO使用中のため記録は保存されません。",Rect(1010,915,830,25),20,UIColor.Yellow);Name(c,false);
    }
    private sealed record Meter(double Value,double Limit,bool Minimum,bool Exclusive=false,bool Percent=false);
    private static List<Meter> Meters(DanConditions c,int[] n,IReadOnlyList<double> g)
    {List<Meter> list=[];if(c.GaugeMin is double gauge)list.Add(new(g.Count==0?0:c.GaugeScope=="everySong"?g.Min():g[^1],gauge,true,false,true));if(c.BadMissMaxExclusive is int bm)list.Add(new(n[3]+n[4],bm,false,true));if(c.BadMax is int b)list.Add(new(n[3],b,false));if(c.MissMax is int m)list.Add(new(n[4],m,false));if(c.PerfectMin is int p)list.Add(new(n[0],p,true));return list;}
    private void MeterDraw(CGContext c,Meter m,CGRect r,double opacity=1,double progress=1)
    {
        bool pass=m.Minimum?m.Value>=m.Limit:m.Exclusive?m.Value<m.Limit:m.Value<=m.Limit;var color=(pass?Color(70,180,255):Color(255,80,90)).ColorWithAlpha((nfloat)opacity);Fill(c,r,Color(6,5,14,.82*opacity));Text(m.Percent?$"{m.Value:0.##}%":m.Exclusive?$"{Math.Max(0,m.Limit-m.Value):0.##}":$"{m.Value:0.##} / {m.Limit:0.##}",Rect((double)r.X,(double)r.Y,(double)r.Width,(double)r.Height*.55),Math.Min(24,(double)r.Height*.5),color,true);double max=m.Percent?100:Math.Max(1,m.Limit),f=m.Minimum?m.Value/max:m.Limit==0?pass?1:0:1-m.Value/max;var bar=Rect((double)r.X+10,(double)r.Y+(double)r.Height*.58,(double)r.Width-20,(double)r.Height*.42-8);Fill(c,bar,Color(100,91,120,.43*opacity));Fill(c,Rect((double)bar.X,(double)bar.Y,(double)bar.Width*Math.Clamp(f,0,1)*progress,(double)bar.Height),color);if(m.Minimum)Fill(c,Rect((double)bar.X+(double)bar.Width*Math.Clamp(m.Limit/max,0,1),(double)bar.Y-3,2,(double)bar.Height+6),UIColor.Yellow.ColorWithAlpha((nfloat)opacity));
    }
    private List<Meter> DanMeters(bool live)
    {
        if(ActiveDan==null)return [];int[] n=(int[])DanCounts.Clone();if(live&&Session!=null)for(int i=0;i<5;i++)n[i]+=Session.Counts[i];var gauges=DanResults.Select(r=>r.GlobalGauge).ToList();if(live&&Session!=null)gauges.Add(Session.GlobalGauge);var meters=Meters(ActiveDan.Definition.Conditions,n,gauges);
        if(live&&Session!=null&&ActiveDan.Definition.Tracks[DanTrack].Conditions is {} own)meters.AddRange(Meters(own,Session.Counts,[Session.Gauge]));if(!live)for(int i=0;i<DanResults.Count;i++)if(ActiveDan.Definition.Tracks[i].Conditions is {} condition)meters.AddRange(Meters(condition,DanResults[i].Counts,[DanResults[i].Gauge]));return meters;
    }
    private void DanHud(CGContext c){if(ActiveDan==null)return;double top=50;Text($"{ActiveDan.Definition.Title}  {DanTrack+1}/{ActiveDan.Tracks.Count}",Rect(1530,18,350,28),18);if(Session?.Life is {} life){Fill(c,Rect(1530,top,350,54),Color(8,12,30,.32));Text($"LIFE  {life.Value:0.##} / {life.Maximum:0.##}",Rect(1540,top+4,330,38),20);Fill(c,Rect(1540,top+47,330,3),Color(148,157,197,.25));Fill(c,Rect(1540,top+47,330*Math.Clamp(life.Value/life.Maximum,0,1),3),life.Value/life.Maximum<=.25?UIColor.SystemPink:Color(155,223,255));top+=58;}var meters=DanMeters(true);double h=Math.Min(58,500.0/Math.Max(1,meters.Count));for(int i=0;i<meters.Count;i++){var m=meters[i];double y=top+i*h;bool pass=m.Minimum?m.Value>=m.Limit:m.Exclusive?m.Value<m.Limit:m.Value<=m.Limit;var color=pass?Color(133,215,245):Color(244,144,166);Fill(c,Rect(1530,y,350,h-4),Color(8,12,30,.32));Text($"{(m.Percent?"GAUGE":"条件")}  {m.Value:0.##} / {(m.Exclusive?"< ":"")}{m.Limit:0.##}",Rect(1540,y+3,330,h-17),18,color);double max=m.Percent?100:Math.Max(1,m.Limit),f=m.Minimum?m.Value/max:m.Limit==0?pass?1:0:1-m.Value/max;Fill(c,Rect(1540,y+h-10,330,3),Color(148,157,197,.25));Fill(c,Rect(1540,y+h-10,330*Math.Clamp(f,0,1),3),color);}}
    private void DanResultScreen(CGContext c,double now)
    {
        if(ActiveDan==null)return;double elapsed=now-resultAt;double Phase(double at,double d)=>Math.Clamp((elapsed-at)/d,0,1);Fill(c,Rect(0,0,1920,1080),Color(24,3,33,Phase(0,1)));Image(selectionBackground??visuals.Blur(visuals.Asset("skin/background.png"),"default"),Rect(0,0,1920,1080),Phase(1,.7),crop:true);Text(ActiveDan.Definition.Title+" — DAN RESULT",Rect(65,65,1775,60),42,opacity:Phase(1,.5));double col=1170.0/Math.Max(3,DanResults.Count);
        for(int i=0;i<DanResults.Count;i++){var r=DanResults[i];double x=65+i*col,a=Phase(1.2+i*.15,.5),side=Math.Min(300,col-50);Fill(c,Rect(x,130,col-20,900),Color(4,3,12,.75*a));bool visible=Visible(ActiveDan,i);if(visible)Image(visuals.FileImage(Media(r.Song,r.Song.Chart.Jacket)),Rect(x+(col-20-side)/2,150,side,side),a,crop:true);else Text("???",Rect(x+20,150,col-60,300),30,center:true,opacity:a);Text($"{i+1}. "+(visible?r.Song.Chart.Title:"???"),Rect(x+18,465,col-56,45),26,opacity:a);Text(DifficultyName(r.Diff.Name)+" Lv."+r.Diff.DisplayLevel,Rect(x+18,510,col-56,36),22,opacity:a);Text(r.Score.ToString("N0"),Rect(x+18,550,col-56,50),36,opacity:a);string[] labels=["PERFECT","GREAT","GOOD","BAD","MISS"];for(int n=0;n<5;n++)Text($"{labels[n]}   {r.Counts[n]:N0}",Rect(x+18,615+n*34,col-56,34),22,opacity:Phase(1.7+i*.15+n*.08,.2));Text("MAX COMBO   "+r.Combo,Rect(x+18,795,col-56,36),21,opacity:Phase(2.2+i*.15,.3));int[] bins=new int[12];foreach(double offset in r.Timing)bins[Math.Clamp((int)Math.Floor((120-offset)/20),0,11)]++;double graph=Phase(2.5+i*.15,.6),bw=(col-85)/12;int max=Math.Max(1,bins.Max());for(int b=0;b<12;b++){double h=140.0*bins[b]/max*Ease(graph);Fill(c,Rect(x+35+b*bw,980-h,bw,h),(b<5?Color(255,142,77):b>6?Color(88,216,220):UIColor.White).ColorWithAlpha((nfloat)graph));}Fill(c,Rect(x+35,981,12*bw,1),UIColor.White.ColorWithAlpha((nfloat)graph));}
        Text(!DanEligible?"AUTOを使用したため今回の記録は保存されません。":DanSucceeded?"合格 / PASSED":"不合格 / FAILED",Rect(1300,130,550,65),DanEligible?34:20,opacity:Phase(3.3,.5));var meters=DanMeters(false);double spacing=Math.Min(76,660.0/Math.Max(1,meters.Count));for(int i=0;i<meters.Count;i++)MeterDraw(c,meters[i],Rect(1300,215+i*spacing,550,spacing-8),Phase(3.3,.5),Phase(3.3,.6));Button(c,"段位選択",Rect(1300,900,500,70),36,opacity:Phase(4,.4));Button(c,"リトライ",Rect(1300,985,500,70),36,opacity:Phase(4,.4));Name(c,true);
    }
    private void TouchDan(float x,float y)
    {
        if(Page==Screen.DanResult){if(CAAnimation.CurrentMediaTime()-resultAt<4.4)return;if(x>=1300&&y>=985&&ActiveDan!=null)DanStart?.Invoke(ActiveDan);else if(x>=1300&&y>=900)Page=Screen.Dan;return;}
        if(x>=978&&x<=1398&&y<108)ImportRequested?.Invoke();else if(x>=100&&x<=260&&y<76)Transition(Screen.Modes);else if(x>=1023&&x<=1361&&y>=945&&Courses.Count>0)DanStart?.Invoke(Courses[danIndex]);else if(x<960&&Courses.Count>0){int move=Math.Abs(y-downY)>35?(int)Math.Round((downY-y)/92):(int)Math.Round((y-501.5)/92);danIndex=Math.Clamp(danIndex+move,0,Courses.Count-1);}
    }
}
