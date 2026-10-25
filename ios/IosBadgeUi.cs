using CoreAnimation;
using CoreGraphics;
using HyperLane;
using HyperLane.Android;
using UIKit;
using static HyperLane.iOS.IosVisuals;
namespace HyperLane.iOS;

internal sealed partial class MigrationStage
{
    private readonly MobileBadges badges=new(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
    private BadgeAward? pendingBadge;
    private readonly Queue<BadgeAward> pendingBadges=new();
    private void NextBadgeAward(){pendingBadge=pendingBadges.TryDequeue(out var award)?award:null;}
    private bool badgePicker;
    private int badgePage;
    private UIImage? MobileRank(int score,bool fc)=>visuals.Asset("skin/rank-"+Rank(score)+(score>=1000000?"-ap":fc?"-fc":"")+".png");
    private bool FullCombo(MobileSong song,MobileDifficulty diff)=>ChartLevel.TryParse(diff.Level,out double level)&&Rating?.BestEntry(Key(song,diff),Hash(song),level)is {} entry&&(entry.FullCombo||RankRules.IsFullCombo(entry.Counts));
    internal void AwardBadge(bool eligible){if(Session is {} s)try{pendingBadges.Clear();foreach(var award in badges.AwardAll(s,eligible))pendingBadges.Enqueue(award);NextBadgeAward();}catch(Exception ex){Console.Error.WriteLine("Badge save failed: "+ex.Message);}}
    private UIImage? BadgeImage(BadgeAward? award)=>badges.Resolve(award,AllSongs)is {} path?visuals.BadgeImage(path):null;
    private void BadgeHeader(CGContext c)
    {
        Polygon(c,[1688,0,1920,0,1920,48,1720,48],Color(5,3,12,.82));Text($"RATE {ratingTotal:0.00}",Rect(1724,0,192,48),27,center:true);
        if(BadgeImage(badges.Equipped)is {} image){c.SaveState();Polygon(c,[1346,0,1688,0,1720,48,1346,48],clip:true);Image(image,Rect(1346,0,374,48));c.RestoreState();}
    }
    private void BadgeOverlay(CGContext c,double now)
    {
        if(pendingBadge!=null&&Page==Screen.Result&&now-resultAt<5)return;
        if(pendingBadge==null&&!badgePicker)return;
        double reveal=pendingBadge!=null?1-Math.Pow(1-Math.Clamp((now-resultAt-5)/.45,0,1),3):1;
        c.SaveState();c.ClipToRect(Rect(300,230,1320*reveal,620));Polygon(c,[350,230,1620,230,1570,850,300,850],Color(5,4,13,.94));
        if(pendingBadge is {} award){Image(BadgeImage(award),Rect(590,325,740,104));Text("称号を入手しました。セットしますか？",Rect(450,475,1020,80),35,center:true);Button(c,"はい",Rect(610,660,280,72),32);Button(c,"いいえ",Rect(1030,660,280,72),32);}
        else{Text("称号",Rect(410,250,1090,50),35);var entries=new[]{(BadgeAward?)null}.Concat(badges.Collection.Awards).ToArray();int start=badgePage*5;for(int i=0;i<5&&start+i<entries.Length;i++){var a=entries[start+i];double y=320+i*70;Text(a==null?"なし":Path.GetFileNameWithoutExtension(a.Image),Rect(430,y,650,32),26);Text(MobileBadges.Requirements(a,AllSongs),Rect(430,y+33,650,32),15,opacity:.8);Image(BadgeImage(a),Rect(1110,y+8,370,52));}Button(c,"前",Rect(440,745,200,70),29);Button(c,"次",Rect(700,745,200,70),29);Button(c,"閉じる",Rect(1200,745,280,70),29);}
        c.RestoreState();
    }
    private bool TouchBadge(float x,float y,int phase)
    {
        if(pendingBadge!=null){if(phase==2&&CAAnimation.CurrentMediaTime()-resultAt>=5.45&&y>=660&&y<=732){if(x>=610&&x<=890){badges.Equip(pendingBadge.Id);NextBadgeAward();}else if(x>=1030&&x<=1310)NextBadgeAward();}return true;}
        if(badgePicker){if(phase!=2)return true;if(y>=745&&y<=815){if(x>=1200&&x<=1480)badgePicker=false;else if(x>=440&&x<=640)badgePage=Math.Max(0,badgePage-1);else if(x>=700&&x<=900)badgePage=Math.Min(badges.Collection.Awards.Count/5,badgePage+1);}else if(x>=430&&x<=1480&&y>=320&&y<670){int i=badgePage*5+(int)((y-320)/70);if(i<=badges.Collection.Awards.Count){badges.Equip(i==0?"":badges.Collection.Awards[i-1].Id);badgePicker=false;}}return true;}
        if(Page is Screen.Select or Screen.Result&&x>=1346&&x<1720&&y>=0&&y<=48){if(phase==2){badgePicker=true;badgePage=0;}return true;}return false;
    }
}
