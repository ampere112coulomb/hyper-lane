using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using System.Globalization;

namespace HyperLane.Android;

internal sealed partial class GameSurface
{
    private HyperLane.UserProfile profile;
    private bool nameDialogOpen;
    private string ProfilePath=>System.IO.Path.Combine(Context!.FilesDir!.AbsolutePath,"profile.json");
    private void ShowMobileNameInput()
    {
        if(nameDialogOpen)return;
        nameDialogOpen=true;
        var layout=new LinearLayout(Context){Orientation=Orientation.Vertical};layout.SetPadding(36,24,36,24);
        var heading=new TextView(Context){Text="ユーザーネームを設定してください。",TextSize=24};heading.SetTextColor(Color.White);layout.AddView(heading);
        var hint=new TextView(Context){Text="1～10文字",TextSize=16};hint.SetTextColor(Color.LightGray);layout.AddView(hint);
        var input=new EditText(Context){Text=profile.UserName,TextSize=24};input.SetSingleLine(true);input.SetTextColor(Color.White);input.SetBackgroundColor(Color.Argb(180,35,31,44));layout.AddView(input);
        var error=new TextView(Context){TextSize=16};error.SetTextColor(Color.Rgb(255,100,110));layout.AddView(error);
        var confirm=new Button(Context){Text="決定"};layout.AddView(confirm);
        var dialog=new Dialog(Context!);dialog.SetContentView(layout);dialog.SetCancelable(false);
        var skip=new Button(Context){Text="スキップ"};layout.AddView(skip);skip.Click+=(_,_)=>{try{profile.UserName="GUEST";profile.Save(ProfilePath);dialog.Dismiss();nameDialogOpen=false;page=Page.Title;Invalidate();}catch(Exception ex){error.Text="保存できません: "+ex.Message;}};
        confirm.Click+=(_,_)=>{
            string name=(input.Text??"").Trim();
            if(!HyperLane.UserProfile.ValidName(name)){error.Text="1～10文字で入力してください。";return;}
            try{profile.UserName=name;profile.Save(ProfilePath);dialog.Dismiss();nameDialogOpen=false;page=Page.Title;Invalidate();}
            catch(Exception ex){error.Text="保存できません: "+ex.Message;}
        };
        dialog.Show();
        dialog.Window?.SetBackgroundDrawable(new global::Android.Graphics.Drawables.ColorDrawable(Color.Argb(230,5,3,12)));
        dialog.Window?.SetLayout((int)(Resources!.DisplayMetrics!.WidthPixels*.65),ViewGroup.LayoutParams.WrapContent);
        dialog.Window?.SetSoftInputMode(SoftInput.AdjustResize);
        input.RequestFocus();
    }
    private void DrawMobileUserName(Canvas canvas,bool result)
    {
        if(!HyperLane.UserProfile.ValidName(profile.UserName))return;
        float y=result?0:1018;
        using var box= result?Polygon([0,y,120,y,156,y+62,540,y+62,540,y,0,y]):Polygon([0,y,240,y,278,y+62,0,y+62]);
        // Separate, inward-slanted name and rating badges on results.
        if(result){
            using var rate=Polygon([1688,0,1920,0,1920,48,1720,48]);
            using var name=Polygon([0,0,420,0,388,48,0,48]);
            paint.Color=Color.Argb(210,5,3,12);paint.SetStyle(Paint.Style.Fill);canvas.DrawPath(rate,paint);canvas.DrawPath(name,paint);
            FitMobileText(canvas,profile.UserName,new RectF(25,0,380,48),30,1);
        }else{
            paint.Color=Color.Argb(210,5,3,12);paint.SetStyle(Paint.Style.Fill);canvas.DrawPath(box,paint);
            FitMobileText(canvas,profile.UserName,new RectF(25,y,233,y+62),29,1);
        }
        DrawBadgeHeader(canvas);
    }
    private void FitMobileText(Canvas canvas,string text,RectF rect,float size,float opacity,bool center=false)
    {
        paint.SetTypeface(Typeface.Default);paint.TextSize=size;
        float width=paint.MeasureText(text);
        if(width>rect.Width())size*=rect.Width()/Math.Max(1,width);
        ResultText(canvas,text,rect,size,opacity,center?Paint.Align.Center:Paint.Align.Left);
    }

    private sealed record MobileBestCard(MobileSong Song,MobileDifficulty Difficulty,HyperLane.PlayerRating.Entry Entry,double Level);
    private List<MobileBestCard> bestCards=[];
    private readonly Dictionary<string,Bitmap?> bestJackets=new();
    private Page bestReturn=Page.Select;
    private void OpenMobileBest30()
    {
        StopSelectionPreview();bestReturn=page;bestCards.Clear();
        foreach(var song in allChapterSongs){string hash=Hash(song);foreach(var diff in song.Chart.Difficulties)
            if(HyperLane.PlayerRating.IsRatedDifficulty(diff.Name)&&HyperLane.ChartLevel.TryParse(diff.Level,out double level)&&rating.BestEntry(RatingKey(song,diff),hash,level) is {} entry)
                bestCards.Add(new(song,diff,entry,level));}
        bestCards=bestCards.OrderByDescending(c=>HyperLane.PlayerRating.ChartRating(c.Level,c.Entry.Score)).ThenBy(c=>c.Song.Chart.Title).Take(30).ToList();
        foreach(var card in bestCards){string file=FindMedia(card.Song.Folder,card.Song.Chart.Jacket,".png",".jpg",".jpeg",".webp");
            if(!bestJackets.ContainsKey(card.Song.ChartPath))bestJackets[card.Song.ChartPath]=file.Length>0?DecodeMobileThumbnail(file,180):null;}
        page=Page.Best30;Invalidate();
    }
    private static Bitmap? DecodeMobileThumbnail(string path,int size)
    {
        using var bounds=new BitmapFactory.Options{InJustDecodeBounds=true};BitmapFactory.DecodeFile(path,bounds);
        int sample=1;while(Math.Min(bounds.OutWidth,bounds.OutHeight)/sample>size*2)sample*=2;
        using var options=new BitmapFactory.Options{InSampleSize=sample};return BitmapFactory.DecodeFile(path,options);
    }
    private void CloseMobileBest30(){foreach(var image in bestJackets.Values)image?.Dispose();bestJackets.Clear();bestCards.Clear();page=bestReturn;Invalidate();}
    private void DrawMobileBest30(Canvas canvas)
    {
        DrawMenuBackground(canvas);DrawMenuParticles(canvas);
        using(var header=Polygon([0,0,1090,0,1150,110,0,110])){paint.Color=Color.Argb(195,5,3,12);paint.SetStyle(Paint.Style.Fill);canvas.DrawPath(header,paint);}
        FitMobileText(canvas,$"{profile.UserName}    RATE {ratingTotal:0.00}",new RectF(35,10,1090,100),36,1);
        for(int i=0;i<30;i++){
            float x=30+i%6*315,y=135+i/6*176;
            using var shape=Polygon([x,y,x+276,y,x+300,y+160,x+24,y+160]);paint.Color=Color.Argb(205,8,4,16);paint.SetStyle(Paint.Style.Fill);canvas.DrawPath(shape,paint);
            if(i>=bestCards.Count){ResultText(canvas,$"#{i+1} 未プレイ",new RectF(x+25,y+8,x+270,y+40),16,.5f);continue;}
            var c=bestCards[i];
            if(bestJackets.TryGetValue(c.Song.ChartPath,out var image)&&image!=null){
                int save=canvas.Save();using(var mask=Polygon([x,y,x+90,y,x+110,y+160,x+24,y+160]))canvas.ClipPath(mask);
                int crop=Math.Min(image.Width,image.Height);paint.Color=Color.White;paint.FilterBitmap=true;
                canvas.DrawBitmap(image,new Rect((image.Width-crop)/2,(image.Height-crop)/2,(image.Width+crop)/2,(image.Height+crop)/2),new RectF(x-25,y,x+135,y+160),paint);canvas.RestoreToCount(save);
            }
            ResultText(canvas,$"#{i+1}   {HyperLane.PlayerRating.ChartRating(c.Level,c.Entry.Score):0.000}",new RectF(x+118,y,x+298,y+25),16,1);
            DrawMobileCardTitle(canvas,c.Song.Chart.Title,new RectF(x+118,y+27,x+292,y+73));
            FitMobileText(canvas,$"{DifficultyName(c.Difficulty.Name)} Lv.{c.Difficulty.DisplayLevel}",new RectF(x+118,y+73,x+298,y+94),16,1);
            ResultText(canvas,c.Entry.Score.ToString("N0"),new RectF(x+118,y+91,x+298,y+122),27,1);
            ResultText(canvas,$"ACC {c.Entry.Score/10000d:0.00}%",new RectF(x+118,y+120,x+298,y+141),16,1);
            string stats=c.Entry.Counts is {Length:5} n?$"P {n[0]} G {n[1]} GD {n[2]} B {n[3]} M {n[4]}":"P — G — GD — B — M —";
            FitMobileText(canvas,stats,new RectF(x+118,y+139,x+293,y+160),10,1);
        }
        using(var back=Polygon([1754,1020,1920,1020,1920,1080,1730,1080])){paint.Color=Color.Argb(160,8,4,16);paint.SetStyle(Paint.Style.Fill);canvas.DrawPath(back,paint);}
        Line(canvas,1840,1050,1800,1050,Color.White,3);Line(canvas,1800,1050,1812,1038,Color.White,3);Line(canvas,1800,1050,1812,1062,Color.White,3);
        PostInvalidateDelayed(33);
    }
    private void DrawMobileCardTitle(Canvas canvas,string text,RectF rect)
    {
        // Two lines, shrink-to-fit; never replace a long title with ellipsis.
        var parts=StringInfo.ParseCombiningCharacters(text);if(parts.Length==0)return;
        for(float size=21;size>=6;size--){
            paint.SetTypeface(Typeface.Default);paint.TextSize=size;
            if(paint.MeasureText(text)<=rect.Width()){ResultText(canvas,text,rect,size,1);return;}
            int split=0;for(int i=1;i<parts.Length;i++)if(paint.MeasureText(text[..parts[i]])<=rect.Width())split=parts[i];else break;
            if(split>0&&paint.MeasureText(text[split..])<=rect.Width()){
                ResultText(canvas,text[..split],new RectF(rect.Left,rect.Top,rect.Right,rect.CenterY()),size,1);
                ResultText(canvas,text[split..],new RectF(rect.Left,rect.CenterY(),rect.Right,rect.Bottom),size,1);return;
            }
        }
        FitMobileText(canvas,text,rect,6,1);
    }
    private bool TouchMobileBest30(MotionEvent e,float x,float y){if(e.ActionMasked==MotionEventActions.Up&&Hit(x,y,1730,1020,1920,1080))CloseMobileBest30();return true;}
}
