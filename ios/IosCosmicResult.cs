using CoreGraphics;
using UIKit;
using HyperLane.Android;
using static HyperLane.iOS.IosVisuals;
namespace HyperLane.iOS;
internal sealed partial class MigrationStage
{
    private void Result(CGContext c,double now)
    {
        if(Session is not {} s)return;double t=now-resultAt;double Phase(double at,double d)=>Math.Clamp((t-at)/d,0,1);double a=Phase(.15,.65);
        Fill(c,Rect(0,0,1920,1080),Color(8,12,29));Image(selectionBackground??visuals.Blur(visuals.Asset("skin/background.png"),"default"),Rect(0,0,1920,1080),.55,crop:true);DrawCosmicSky(c,t,a);
        void Panel(double x,double y,double w,double h,double opacity){Fill(c,Rect(x,y,w,h),Color(12,13,32,.5*opacity));Fill(c,Rect(x,y,w,2),Color(104,91,159,opacity));}
        Panel(90,170,680,700,a);Panel(810,170,1020,300,a);Panel(810,490,1020,100,Phase(.3,.5));Panel(810,610,1020,260,Phase(.45,.5));
        Text("RESULT",Rect(98,95,500,45),30,opacity:a);Text(DifficultyName(s.Difficulty.Name)+" Lv."+s.Difficulty.DisplayLevel,Rect(1500,100,300,50),30,opacity:a);
        Image(jacket,Rect(130,200,600,560),a,crop:true);Fill(c,Rect(130,790,580,4),Color(70,180,235,a));Text(s.Song.Chart.Title,Rect(135,792,595,52),42,opacity:a);Text(s.Song.Chart.Composer,Rect(135,842,595,33),24,opacity:a);Text("DESIGNER: "+s.Song.Chart.DesignerFor(s.Difficulty),Rect(98,895,662,40),22,opacity:a);
        Text("SCORE",Rect(865,215,435,45),30,opacity:a);Text(((int)(s.Score*(1-Math.Pow(1-Phase(.5,1),3)))).ToString("D7"),Rect(865,280,555,125),96,opacity:a);
        double rp=Phase(.8,.65),size=304*(1+.3*(1-rp));Image(visuals.Asset("skin/rank_box.png"),Rect(1620-size/2,320-size/2,size,size),rp);Image(MobileRank(s.Score,HyperLane.RankRules.IsFullCombo(s.Counts)),Rect(1620-size*.37,320-size*.37,size*.74,size*.74),rp);
        var tint=s.Score>=1000000?Color(255,222,115):HyperLane.RankRules.IsFullCombo(s.Counts)?Color(153,223,255):Color(172,168,244);double[] points=[1464,278,1501,204,1590,190,1716,216,1784,302,1744,410,1647,449,1510,421,1458,352];for(int i=0;i<points.Length/2;i++){c.SetStrokeColor(tint.ColorWithAlpha((nfloat)(Phase(1,.8)*.55)).CGColor);c.SetLineWidth(1.1f);if(i>0){c.MoveTo((nfloat)points[i*2-2],(nfloat)points[i*2-1]);c.AddLineToPoint((nfloat)points[i*2],(nfloat)points[i*2+1]);c.StrokePath();}c.SetFillColor(tint.ColorWithAlpha((nfloat)(Phase(1,.8)*.8)).CGColor);c.FillEllipseInRect(Rect(points[i*2]-2.5,points[i*2+1]-2.5,5,5));}
        int total=s.Counts.Sum();double accuracy=total==0?100:100d*(s.Counts[0]+s.Counts[1]*.75+s.Counts[2]*.5+s.Counts[3]*.25)/total;Text("MAX COMBO  "+s.MaxCombo,Rect(865,510,455,60),33,opacity:a);Text($"ACCURACY  {accuracy:0.00}%",Rect(1370,510,390,60),33,opacity:a);
        string[] labels=["PERFECT","GREAT","GOOD","BAD","MISS"];for(int i=0;i<5;i++){double x=865+i*180;Text(labels[i],Rect(x,630,150,50),24,center:true,opacity:Phase(.65+i*.06,.3));Text(s.Counts[i].ToString(),Rect(x,680,150,50),32,center:true,opacity:a);}
        int[] bins=new int[12];foreach(double offset in s.TimingOffsets)bins[Math.Clamp((int)Math.Floor((120-offset)/20),0,11)]++;int max=Math.Max(1,bins.Max());for(int i=0;i<12;i++){double h=96d*bins[i]/max*Phase(.9,.6);Fill(c,Rect(875+i*21,838-h,20,h),i<6?Color(255,142,100):Color(88,216,220));}Fill(c,Rect(872,839,273,1),UIColor.Gray);Text("LATE       FAST",Rect(910,839,230,26),18,opacity:a);
        string[] actions=["曲選択","リトライ","見返す"];for(int i=0;i<3;i++){double x=850+i*235;Polygon(c,[x+24,960,x+220,960,x+196,1040,x,1040],Color(12,13,32,.82*a));Text(actions[i],Rect(x,960,220,80),32,center:true,opacity:a);}Name(c,true);
    }
    private void DrawCosmicSky(CGContext c,double seconds,double opacity)
    {
        c.SetLineWidth(.85f);foreach(var link in CosmosScene.Links){float alpha=CosmosScene.LinkOpacity(link,seconds,out float reveal)*(float)opacity;if(alpha<=0)continue;var a=CosmosScene.Stars[link.A];var b=CosmosScene.Stars[link.B];c.SetStrokeColor(Color(145,182,242,85/255d*alpha).CGColor);c.MoveTo(a.X,a.Y);c.AddLineToPoint(a.X+(b.X-a.X)*reveal,a.Y+(b.Y-a.Y)*reveal);c.StrokePath();}
        foreach(var star in CosmosScene.Stars){double pulse=.5+.5*Math.Sin(seconds*star.Speed+star.Phase);c.SetFillColor(Color(194,215,255,210/255d*opacity*(.19+.65*pulse)).CGColor);c.FillEllipseInRect(Rect(star.X-star.Radius,star.Y-star.Radius,star.Radius*2,star.Radius*2));if(star.Radius<1.4)continue;c.SetFillColor(Color(150,173,255,26/255d*opacity).CGColor);c.FillEllipseInRect(Rect(star.X-star.Radius*4,star.Y-star.Radius*4,star.Radius*8,star.Radius*8));}
    }
}
