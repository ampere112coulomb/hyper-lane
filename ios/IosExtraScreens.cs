using CoreGraphics;
using HyperLane;
using HyperLane.Android;
using UIKit;
using static HyperLane.iOS.IosVisuals;

namespace HyperLane.iOS;

internal sealed partial class MigrationStage
{
    private double practiceBeat;
    private double practiceViewBeat,practiceDragStart,practiceDownY;
    private bool practiceDragging;
    internal Action<double,Action<double>>? BeatRequested;
    private string[][] SettingLabels=>[["ノーツスピード","ゲーム時の楽曲音量","曲選択プレビュー音量","ノーツ効果音の音量","譜面補正（+でノーツを遅く）","オートプレイ","自動SE","タッチ判定の高さ"],["レーンの透明度","ゲーム背景の明るさ","プレビュー背景の明るさ","開始前の余白","フィーバーエフェクトの明るさ","MVを優先","キービームの透明度","キービームの長さ","レーンカバー"]];
    private double Value(int row)=>settingsTab==0?row switch{0=>Settings.NoteSpeed,1=>Settings.BgmVolume,2=>Settings.PreviewVolume,3=>Settings.SeVolume,4=>Settings.AudioOffsetMs,5=>Settings.AutoPlay?1:0,6=>Settings.AutoNoteSound?1:0,_=>Settings.TouchHeight}:row switch{0=>Settings.LaneTransparency,1=>Settings.BackgroundBrightness,2=>Settings.PreviewBackgroundBrightness,3=>Settings.StartMarginMs,4=>Settings.FeverAuraBrightness,5=>Settings.PreferMusicVideo?1:0,6=>Settings.KeyBeamOpacity,7=>Settings.KeyBeamLength,_=>Settings.LaneCover};
    private bool Slider(int row)=>settingsTab==0?row is 0 or 1 or 2 or 3 or 7:row is 0 or 1 or 2 or 4 or 6 or 7 or 8;
    private void SetValue(int row,double value)
    {
        int v=(int)Math.Round(value);if(settingsTab==0)switch(row){case 0:Settings.NoteSpeed=Math.Clamp(value,.5,10);break;case 1:Settings.BgmVolume=Math.Clamp(v,0,100);break;case 2:Settings.PreviewVolume=Math.Clamp(v,0,100);break;case 3:Settings.SeVolume=Math.Clamp(v,0,100);break;case 4:Settings.AudioOffsetMs=Math.Clamp(v,-1000,1000);break;case 5:Settings.AutoPlay=v!=0;break;case 6:Settings.AutoNoteSound=v!=0;break;case 7:Settings.TouchHeight=Math.Clamp(v,0,100);break;}
        else switch(row){case 0:Settings.LaneTransparency=Math.Clamp(v,0,100);break;case 1:Settings.BackgroundBrightness=Math.Clamp(v,0,100);break;case 2:Settings.PreviewBackgroundBrightness=Math.Clamp(v,0,100);break;case 3:Settings.StartMarginMs=Math.Max(0,v);break;case 4:Settings.FeverAuraBrightness=Math.Clamp(v,0,100);break;case 5:Settings.PreferMusicVideo=v!=0;break;case 6:Settings.KeyBeamOpacity=Math.Clamp(v,0,100);break;case 7:Settings.KeyBeamLength=Math.Clamp(v,0,100);break;case 8:Settings.LaneCover=Math.Clamp(v,0,100);break;}
    }
    internal Action<string,int,Action<int>>? NumberRequested;
    internal Action<int,bool>? PresetRequested;
    private void SettingsScreen(CGContext c)
    {
        c.SaveState();c.TranslateCTM(960,540);c.ScaleCTM(1.4f,1.4f);c.TranslateCTM(-960,-540);Fill(c,Rect(550,190,820,700),Color(0,0,0,.88));Text("設 定",Rect(570,202,500,35),25);
        string[] tabs=["プレイ・音声","映像・演出","プリセット"];for(int i=0;i<3;i++){Fill(c,Rect(566+i*250,240,250,40),i==settingsTab?Color(47,40,57):Color(15,13,19));Text(tabs[i],Rect(566+i*250,240,250,40),21,center:true);}
        if(settingsTab==2)for(int i=0;i<3;i++){Text("プリセット "+(i+1),Rect(586,317+i*125,200,55),26);Button(c,"現在の設定を保存",Rect(795,317+i*125,245,55),22);Button(c,"読み込み",Rect(1055,317+i*125,253,55),22);}
        for(int row=0;settingsTab<2&&row<SettingLabels[settingsTab].Length;row++){double y=300+row*54,v=Value(row);Text(SettingLabels[settingsTab][row],Rect(586,y+7,480,35),22);string value=settingsTab==0&&row==0?$"{v:0.00} ×":((settingsTab==0&&row is 5 or 6)||(settingsTab==1&&row==5))?v>0?"ON":"OFF":$"{v:0}";
            if(Slider(row)){double f=settingsTab==0&&row==0?(v-.5)/9.5:v/100;Fill(c,Rect(1080,y+19,125,3),Color(85,85,90));Fill(c,Rect(1080,y+19,125*f,3),Color(178,146,225));c.SetFillColor(UIColor.White.CGColor);c.FillEllipseInRect(Rect(1074+125*f,y+15,12,12));Text(value,Rect(1220,y,90,42),20,center:true);}
            else{Button(c,"−",Rect(1080,y,50,42),23);Text(value,Rect(1138,y,112,42),21,UIColor.Cyan,true);Button(c,"+",Rect(1258,y,50,42),23);}}
        Button(c,"保存して閉じる",Rect(1080,832,260,46),23);c.RestoreState();
    }
    private void TouchSettings(float x,float y,int phase)
    {
        double sx=960+(x-960)/1.4,sy=540+(y-540)/1.4;int row=(int)((sy-300)/54);
        if(settingsTab<2&&row>=0&&row<SettingLabels[settingsTab].Length&&Slider(row)&&sx>=1065&&sx<=1215&&sy>=300){double f=Math.Clamp((sx-1080)/125,0,1);SetValue(row,settingsTab==0&&row==0?.5+f*9.5:f*100);if(phase==2)SaveSettings?.Invoke();return;}
        if(phase!=2)return;if(sy>=240&&sy<=280&&sx>=566&&sx<=1316)settingsTab=Math.Clamp((int)((sx-566)/250),0,2);
        else if(sx>=1080&&sy>=832){SaveSettings?.Invoke();Page=settingsReturn;}
        else if(settingsTab==2){int slot=(int)((sy-317)/125);if(slot>=0&&slot<3&&sy>=317&&sy<=372+slot*125){if(sx>=795&&sx<=1040)PresetRequested?.Invoke(slot,true);else if(sx>=1055&&sx<=1308)PresetRequested?.Invoke(slot,false);}}
        else if(row>=0&&row<SettingLabels[settingsTab].Length&&sy>=300&&sy<=810){bool toggle=settingsTab==0&&row is 5 or 6||settingsTab==1&&row==5;
            if(sx>=1138&&sx<=1250&&!toggle&&!Slider(row)){int edited=row;NumberRequested?.Invoke(SettingLabels[settingsTab][row],(int)Value(row),v=>{SetValue(edited,v);SaveSettings?.Invoke();});}
            else if(sx>=1080&&sx<=1130||sx>=1258&&sx<=1308){double step=settingsTab==0&&row==4?5:settingsTab==1&&row==3?100:1;SetValue(row,toggle?Value(row)>0?0:1:Value(row)+(sx<1130?-step:step));SaveSettings?.Invoke();}}
    }
    private void BestScreen(CGContext c)
    {
        Fill(c,Rect(0,0,1920,1080),Color(5,3,15,.9));Text(UserName,Rect(70,15,600,70),45);Text($"RATE {ratingTotal:0.00}",Rect(1450,15,400,70),40);
        var entries=AllSongs.SelectMany(s=>s.Chart.Difficulties.Select(d=>{var m=Meta(s,d);return(Song:s,Diff:d,Entry:Rating?.BestEntry(Key(s,d),m.Hash,m.Level));})).Where(e=>e.Entry!=null).OrderByDescending(e=>PlayerRating.ChartRating(e.Entry!.Level,e.Entry.Score)).Take(30).ToArray();
        for(int i=0;i<entries.Length;i++)
        {
            var e=entries[i];double x=35+i%5*377,y=110+i/5*145;
            Polygon(c,[x,y,x+357,y,x+337,y+130,x,y+130],Color(12,8,24,.8));
            Image(visuals.FileImage(Media(e.Song,e.Song.Chart.Jacket)),Rect(x,y,110,130),crop:true);
            Fill(c,Rect(x,y,110,24),Color(5,3,12,.8));Text($"{PlayerRating.ChartRating(e.Entry!.Level,e.Entry.Score):0.000}",Rect(x,y,110,24),17,UIColor.Cyan,true);
            TitleTwoLines(e.Song.Chart.Title,Rect(x+120,y+2,225,38),22);
            Text(DifficultyName(e.Diff.Name)+" Lv."+e.Diff.Level,Rect(x+120,y+41,225,21),15,Difficulty(e.Diff.Name));
            Text(e.Entry.Score.ToString("D7"),Rect(x+120,y+63,225,29),26,UIColor.Cyan);
            int[] n=e.Entry.Counts??[];int total=n.Sum();
            Text(total>0&&n.Length==5?$"ACC {(n[0]+.75*n[1]+.5*n[2]+.25*n[3])*100/total:0.00}%":"ACC —",Rect(x+120,y+91,225,16),12);
            Text(n.Length==5?$"P {n[0]} G {n[1]} GD {n[2]} B {n[3]} M {n[4]}":"",Rect(x+120,y+109,225,16),12);
        }
        Polygon(c,[1700,1000,1880,1000,1880,1080,1745,1080],Color(5,3,12,.65));Text("←",Rect(1740,1000,140,80),40,center:true);
    }
    private void PracticeScreen(CGContext c)
    {
        Button(c,"←",Rect(40,30,250,66),28);Text((Selected?.Chart.Title??"")+" / "+DifficultyName(SelectedDifficulty?.Name??""),Rect(410,25,1100,80),32,center:true);
        if(SelectedDifficulty is not {} d)return;var rect=Rect(650,130,620,750);Fill(c,rect,Color(8,11,18,245/255.0));double laneW=620.0/d.Lanes;double Y(double beat)=>880-(beat-practiceViewBeat)*58;
        for(int i=0;i<=d.Lanes;i++)Fill(c,Rect(650+i*laneW,130,1,750),Color(110,115,135,75/255.0));
        for(double beat=Math.Ceiling(practiceViewBeat);beat<=practiceViewBeat+750.0/58;beat++){double y=Y(beat);Fill(c,Rect(650,y,620,1),Color(110,115,135,75/255.0));Text($"{beat:0}",Rect(580,y-14,60,28),14,opacity:.7);}
        c.SaveState();c.ClipToRect(rect);
        foreach(var n in d.Notes){if(n.EndBeat<practiceViewBeat||n.Beat>practiceViewBeat+750.0/58)continue;int first=Math.Max(1,n.Lane),last=(int)Math.Min(d.Lanes,(long)n.Lane+n.LaneSpan-1);if(last<first)continue;double x=650+(first-1)*laneW+3,w=(last-first+1)*laneW-6,y=Y(n.Beat),end=Y(n.EndBeat);var color=n.Kind switch{'H' or 'W'=>Color(212,167,39),'L'=>Color(188,140,255),'D'=>Color(248,81,73),'G'=>Color(16,185,129),_=>n.LaneSpan>1?Color(111,168,139):n.Lane%2==1?UIColor.White:Color(88,166,255)};
            if(!string.IsNullOrWhiteSpace(n.CustomColor))try{int rgb=Convert.ToInt32(n.CustomColor.TrimStart('#'),16);color=Color((rgb>>16)&255,(rgb>>8)&255,rgb&255);}catch{}
            if(n.Kind is 'L' or 'G')Fill(c,Rect(x,Math.Min(y,end),w,Math.Abs(y-end)+5),color.ColorWithAlpha(.4f));Fill(c,Rect(x,y-5,w,10),color);if(n.Kind=='L')Fill(c,Rect(x,end-5,w,10),n.HoldEnd?Color(212,167,39):color);
        }
        Fill(c,Rect(650,Y(practiceBeat)-1,620,3),UIColor.Cyan);c.RestoreState();Button(c,"▶",Rect(42,950,210,90),42);Button(c,$"開始拍  {practiceBeat:0.####}",Rect(290,950,480,90),32);Text("上下スワイプ：移動   タップ：開始拍",Rect(820,946,1080,50),23,opacity:.8);Text("8拍の余白から開始・スコアとリプレイは保存しません",Rect(820,996,1080,50),23,opacity:.8);
    }
    private void TouchPractice(float x,float y,int phase)
    {
        if(phase==0){practiceDownY=y;practiceDragStart=practiceViewBeat;practiceDragging=x>=650&&x<=1270&&y>=130&&y<=880;}
        if(phase==1&&practiceDragging){practiceViewBeat=Math.Max(0,practiceDragStart+(y-practiceDownY)/58);return;}
        if(phase!=2)return;bool dragged=practiceDragging&&Math.Abs(y-practiceDownY)>20;practiceDragging=false;
        if(x>=40&&x<=290&&y>=30&&y<=96)Page=Screen.Select;
        else if(x>=42&&x<=252&&y>=950&&y<=1040&&Selected!=null&&SelectedDifficulty!=null)PracticeRequested?.Invoke(Selected,SelectedDifficulty,practiceBeat);
        else if(x>=290&&x<=770&&y>=950&&y<=1040)BeatRequested?.Invoke(practiceBeat,value=>{practiceBeat=practiceViewBeat=Math.Max(0,value);});
        else if(!dragged&&x>=650&&x<=1270&&y>=130&&y<=880)practiceBeat=Math.Max(0,Math.Round((practiceViewBeat+(880-y)/58)*4)/4);
    }
}
