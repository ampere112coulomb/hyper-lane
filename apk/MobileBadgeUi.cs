using Android.Graphics;
using Android.Views;
using HyperLane;
using Path=System.IO.Path;
namespace HyperLane.Android;

internal sealed partial class GameSurface
{
    private MobileBadges? mobileBadges;
    private MobileBadges Badges=>mobileBadges??=new(Context!.FilesDir!.AbsolutePath);
    private BadgeAward? pendingBadge;
    private bool badgePicker;
    private bool badgeEligible;
    private int badgePage;
    private Bitmap? MobileRank(int score,bool fullCombo)=>ResultSprite("rank-"+Rank(score)+(score>=1000000?"-ap":fullCombo?"-fc":"")+".png");
    private readonly Dictionary<string,Bitmap?> badgeImages=new();
    private Bitmap? BadgeImage(BadgeAward? award)
    {
        string? path=Badges.Resolve(award,allChapterSongs);if(path==null)return null;
        if(badgeImages.TryGetValue(path,out var cached))return cached;
        var source=DecodeMobileThumbnail(path,1024);if(source==null)return badgeImages[path]=null;
        int w=source.Width,h=source.Height;int[] pixels=new int[w*h];source.GetPixels(pixels,0,w,0,0,w,h);
        int l=w,t=h,r=-1,b=-1;for(int y=0;y<h;y++)for(int x=0;x<w;x++)if((uint)pixels[y*w+x]>>24>8){l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y);}
        if(r<l){source.Dispose();return badgeImages[path]=null;}
        var image=Bitmap.CreateBitmap(source,l,t,r-l+1,b-t+1);if(!ReferenceEquals(source,image))source.Dispose();return badgeImages[path]=image;
    }
    private void DrawBadgeHeader(Canvas canvas)
    {
        using var box=Polygon([1688,0,1920,0,1920,48,1720,48]);paint.Color=Color.Argb(210,5,3,12);paint.SetStyle(Paint.Style.Fill);canvas.DrawPath(box,paint);
        ResultText(canvas,$"RATE {ratingTotal:0.00}",new RectF(1724,0,1916,48),27,1,Paint.Align.Center);
        if(BadgeImage(Badges.Equipped) is {} image){int save=canvas.Save();using var mask=Polygon([1346,0,1688,0,1720,48,1346,48]);canvas.ClipPath(mask);ResultImage(canvas,image,new RectF(1346,0,1720,48),1);canvas.RestoreToCount(save);}
    }
    private void DrawBadgeOverlay(Canvas canvas)
    {
        if(pendingBadge!=null&&page==Page.Result&&(Environment.TickCount64-resultStartedAt)<5000){PostInvalidateOnAnimation();return;}
        if(pendingBadge==null&&!badgePicker)return;
        float reveal=pendingBadge!=null?(float)Math.Clamp((Environment.TickCount64-resultStartedAt-5000)/450.0,0,1):1;reveal=1-MathF.Pow(1-reveal,3);
        int save=canvas.Save();canvas.ClipRect(300,230,300+1320*reveal,850);
        using(var box=Polygon([350,230,1620,230,1570,850,300,850])){paint.Color=Color.Argb(240,5,4,13);paint.SetStyle(Paint.Style.Fill);canvas.DrawPath(box,paint);}
        if(pendingBadge is {} award){if(BadgeImage(award) is {} image)ResultImage(canvas,image,new RectF(590,325,1330,429),1);ResultText(canvas,"称号を入手しました。セットしますか？",new RectF(450,475,1470,555),35,1,Paint.Align.Center);ScreenButton(canvas,"はい",new RectF(610,660,890,732),32);ScreenButton(canvas,"いいえ",new RectF(1030,660,1310,732),32);PostInvalidateOnAnimation();}
        else{
            var awards=Badges.Collection.Awards;ResultText(canvas,"称号",new RectF(410,250,1500,300),35,1);
            var entries=new[]{(BadgeAward?)null}.Concat(awards).ToArray();int start=badgePage*5;
            for(int i=0;i<5&&start+i<entries.Length;i++){var a=entries[start+i];float y=320+i*70;ResultText(canvas,a==null?"なし":Path.GetFileNameWithoutExtension(a.Image),new RectF(430,y,1080,y+62),30,1);if(BadgeImage(a)is {} image)ResultImage(canvas,image,new RectF(1110,y+8,1480,y+60),1);}
            ScreenButton(canvas,"前",new RectF(440,745,640,815),29);ScreenButton(canvas,"次",new RectF(700,745,900,815),29);ScreenButton(canvas,"閉じる",new RectF(1200,745,1480,815),29);
        }
        canvas.RestoreToCount(save);
    }
    private bool TouchBadges(MotionEvent e,float x,float y)
    {
        if(pendingBadge!=null){if(e.ActionMasked==MotionEventActions.Up&&Environment.TickCount64-resultStartedAt>=5450&&y>=660&&y<=732){if(x>=610&&x<=890){Badges.Equip(pendingBadge.Id);pendingBadge=null;}else if(x>=1030&&x<=1310)pendingBadge=null;}Invalidate();return true;}
        if(badgePicker){if(e.ActionMasked!=MotionEventActions.Up)return true;if(y>=745&&y<=815){if(x>=1200&&x<=1480)badgePicker=false;else if(x>=440&&x<=640)badgePage=Math.Max(0,badgePage-1);else if(x>=700&&x<=900)badgePage=Math.Min(Badges.Collection.Awards.Count/5,badgePage+1);}else if(x>=430&&x<=1480&&y>=320&&y<670){int i=badgePage*5+(int)((y-320)/70);if(i<=Badges.Collection.Awards.Count){Badges.Equip(i==0?"":Badges.Collection.Awards[i-1].Id);badgePicker=false;}}Invalidate();return true;}
        if(page is Page.Select or Page.Result&&x>=1346&&x<1720&&y>=0&&y<=48){if(e.ActionMasked==MotionEventActions.Up){badgePicker=true;badgePage=0;Invalidate();}return true;}return false;
    }
}
