using CoreGraphics;
using HyperLane;
using HyperLane.Android;
using UIKit;
using static HyperLane.iOS.IosVisuals;

namespace HyperLane.iOS;

internal sealed partial class MigrationStage
{
    private double practiceBeat;
    private string[][] SettingLabels=>[["ノーツスピード","ゲーム時の楽曲音量","曲選択プレビュー音量","ノーツ効果音の音量","譜面補正（+でノーツを遅く）","オートプレイ","自動SE","タッチ判定の高さ"],["レーンの透明度","ゲーム背景の明るさ","プレビュー背景の明るさ","開始前の余白","フィーバーエフェクトの明るさ","MVを優先","キービームの透明度","キービームの長さ","レーンカバー"]];
    private double Value(int row)=>settingsTab==0?row switch{0=>Settings.NoteSpeed,1=>Settings.BgmVolume,2=>Settings.PreviewVolume,3=>Settings.SeVolume,4=>Settings.AudioOffsetMs,5=>Settings.AutoPlay?1:0,6=>Settings.AutoNoteSound?1:0,_=>Settings.TouchHeight}:row switch{0=>Settings.LaneTransparency,1=>Settings.BackgroundBrightness,2=>Settings.PreviewBackgroundBrightness,3=>Settings.StartMarginMs,4=>Settings.FeverAuraBrightness,5=>Settings.PreferMusicVideo?1:0,6=>Settings.KeyBeamOpacity,7=>Settings.KeyBeamLength,_=>Settings.LaneCover};
    private bool Slider(int row)=>settingsTab==0?row is 0 or 1 or 2 or 3 or 7:row is 0 or 1 or 2 or 4 or 6 or 7 or 8;
    private void SetValue(int row,double value)
    {
        int v=(int)Math.Round(value);if(settingsTab==0)switch(row){case 0:Settings.NoteSpeed=Math.Clamp(value,.5,10);break;case 1:Settings.BgmVolume=Math.Clamp(v,0,100);break;case 2:Settings.PreviewVolume=Math.Clamp(v,0,100);break;case 3:Settings.SeVolume=Math.Clamp(v,0,100);break;case 4:Settings.AudioOffsetMs=Math.Clamp(v,-1000,1000);break;case 5:Settings.AutoPlay=v!=0;break;case 6:Settings.AutoNoteSound=v!=0;break;case 7:Settings.TouchHeight=Math.Clamp(v,0,100);break;}
        else switch(row){case 0:Settings.LaneTransparency=Math.Clamp(v,0,100);break;case 1:Settings.BackgroundBrightness=Math.Clamp(v,0,100);break;case 2:Settings.PreviewBackgroundBrightness=Math.Clamp(v,0,100);break;case 3:Settings.StartMarginMs=Math.Max(0,v);break;case 4:Settings.FeverAuraBrightness=Math.Clamp(v,0,100);break;case 5:Settings.PreferMusicVideo=v!=0;break;case 6:Settings.KeyBeamOpacity=Math.Clamp(v,0,100);break;case 7:Settings.KeyBeamLength=Math.Clamp(v,0,100);break;case 8:Settings.LaneCover=Math.Clamp(v,0,100);break;}
    }
    internal Action<string,int,Action<int>>? NumberRequested;
    private void SettingsScreen(CGContext c)
    {
        c.SaveState();c.TranslateCTM(960,540);c.ScaleCTM(1.4f,1.4f);c.TranslateCTM(-960,-540);Fill(c,Rect(550,190,820,700),Color(0,0,0,.88));Text("設 定",Rect(570,202,500,35),25);
        string[] tabs=["プレイ・音声","映像・演出"];for(int i=0;i<2;i++){Fill(c,Rect(566+i*375,240,375,40),i==settingsTab?Color(47,40,57):Color(15,13,19));Text(tabs[i],Rect(566+i*375,240,375,40),21,center:true);}
        for(int row=0;row<SettingLabels[settingsTab].Length;row++){double y=300+row*54,v=Value(row);Text(SettingLabels[settingsTab][row],Rect(586,y+7,480,35),22);string value=settingsTab==0&&row==0?$"{v:0.00} ×":((settingsTab==0&&row is 5 or 6)||(settingsTab==1&&row==5))?v>0?"ON":"OFF":$"{v:0}";
            if(Slider(row)){double f=settingsTab==0&&row==0?(v-.5)/9.5:v/100;Fill(c,Rect(1080,y+19,125,3),Color(85,85,90));Fill(c,Rect(1080,y+19,125*f,3),Color(178,146,225));c.SetFillColor(UIColor.White.CGColor);c.FillEllipseInRect(Rect(1074+125*f,y+15,12,12));Text(value,Rect(1220,y,90,42),20,center:true);}
            else{Button(c,"−",Rect(1080,y,50,42),23);Text(value,Rect(1138,y,112,42),21,UIColor.Cyan,true);Button(c,"+",Rect(1258,y,50,42),23);}}
        Button(c,"保存して閉じる",Rect(1080,832,260,46),23);c.RestoreState();
    }
    private void TouchSettings(float x,float y,int phase)
    {
        double sx=960+(x-960)/1.4,sy=540+(y-540)/1.4;int row=(int)((sy-300)/54);
        if(row>=0&&row<SettingLabels[settingsTab].Length&&Slider(row)&&sx>=1065&&sx<=1215&&sy>=300){double f=Math.Clamp((sx-1080)/125,0,1);SetValue(row,settingsTab==0&&row==0?.5+f*9.5:f*100);if(phase==2)SaveSettings?.Invoke();return;}
        if(phase!=2)return;if(sy>=240&&sy<=280&&sx>=566&&sx<=1316)settingsTab=sx<941?0:1;
        else if(sx>=1080&&sy>=832){SaveSettings?.Invoke();Page=settingsReturn;}
        else if(row>=0&&row<SettingLabels[settingsTab].Length&&sy>=300&&sy<=810){bool toggle=settingsTab==0&&row is 5 or 6||settingsTab==1&&row==5;
            if(sx>=1138&&sx<=1250&&!toggle&&!Slider(row)){int edited=row;NumberRequested?.Invoke(SettingLabels[settingsTab][row],(int)Value(row),v=>{SetValue(edited,v);SaveSettings?.Invoke();});}
            else if(sx>=1080&&sx<=1130||sx>=1258&&sx<=1308){double step=settingsTab==0&&row==4?5:settingsTab==1&&row==3?100:1;SetValue(row,toggle?Value(row)>0?0:1:Value(row)+(sx<1130?-step:step));SaveSettings?.Invoke();}}
    }
    private void BestScreen(CGContext c)
    {
        Fill(c,Rect(0,0,1920,1080),Color(5,3,15,.9));Text(UserName,Rect(70,15,600,70),45);Text($"RATE {ratingTotal:0.00}",Rect(1450,15,400,70),40);
        var entries=Songs.SelectMany(s=>s.Chart.Difficulties.Select(d=>{var m=Meta(s,d);return(Song:s,Diff:d,Entry:Rating?.BestEntry(Key(s,d),m.Hash,m.Level));})).Where(e=>e.Entry!=null).OrderByDescending(e=>PlayerRating.ChartRating(e.Entry!.Level,e.Entry.Score)).Take(30).ToArray();
        for(int i=0;i<entries.Length;i++){var e=entries[i];double x=35+i%5*377,y=110+i/5*145;Polygon(c,[x,y,x+357,y,x+337,y+130,x,y+130],Color(12,8,24,.8));Image(visuals.FileImage(Media(e.Song,e.Song.Chart.Jacket)),Rect(x,y,110,130),crop:true);Text(e.Song.Chart.Title,Rect(x+120,y+3,225,32),24);Text(DifficultyName(e.Diff.Name)+" Lv."+e.Diff.Level,Rect(x+120,y+36,225,25),16,Difficulty(e.Diff.Name));Text(e.Entry!.Score.ToString("D7"),Rect(x+120,y+61,225,32),27,UIColor.Cyan);int[] n=e.Entry.Counts??[];Text(n.Length==5?$"P {n[0]} G {n[1]} GD {n[2]} B {n[3]} M {n[4]}":"",Rect(x+120,y+99,225,25),13);}Button(c,"←",Rect(1700,1000,180,65),40);
    }
    private void PracticeScreen(CGContext c)
    {
        Fill(c,Rect(0,0,1920,1080),Color(4,6,12,.9));if(SelectedDifficulty is not {} d)return;double laneW=800.0/d.Lanes;for(int i=0;i<=d.Lanes;i++)Fill(c,Rect(560+i*laneW,60,1,960),Color(40,55,76));for(int n=-1;n<=14;n++){double beat=Math.Floor(practiceBeat)+n,y=960-(beat-practiceBeat)*65;Fill(c,Rect(560,y,800,1),Color(45,58,78));Text($"{beat:0.##}",Rect(465,y-15,85,30),18);}
        foreach(var n in d.Notes){double y=960-(n.Beat-practiceBeat)*65;if(y<50||y>1000)continue;var color=n.Kind switch{'H' or 'W'=>Color(218,172,35),'L'=>Color(185,130,255),'D'=>Color(255,75,90),'G'=>Color(16,185,129),'M'=>Color(111,168,139),_=>n.Lane%2==0?Color(79,163,255):UIColor.White};if(n.Kind is 'L' or 'G'){double end=960-(n.EndBeat-practiceBeat)*65;Fill(c,Rect(560+(n.Lane-1)*laneW+3,Math.Max(60,end),Math.Max(1,n.LaneSpan*laneW-6),Math.Max(1,y-Math.Max(60,end))),color.ColorWithAlpha(.5f));}Fill(c,Rect(560+(n.Lane-1)*laneW+3,y,Math.Max(1,n.LaneSpan*laneW-6),14),color);}
        Button(c,"▶",Rect(30,920,180,120),50);Button(c,"←",Rect(1700,980,170,75),40);Text($"開始拍 {practiceBeat:0.##}",Rect(50,50,460,70),32);
    }
}
