using Android.Graphics;
namespace HyperLane.Android;
internal sealed partial class GameSurface
{
    private readonly Dictionary<string,Bitmap?> judgementImages=new();
    private bool DrawMobileJudgement(Canvas canvas,string name,RectF target)
    {
        if(!judgementImages.TryGetValue(name,out var image)){
            try{
                using var stream=Context!.Assets!.Open("judgements/"+name+".png");
                using var source=BitmapFactory.DecodeStream(stream);
                if(source is null)return false;
                int[] pixels=new int[source.Width*source.Height];source.GetPixels(pixels,0,source.Width,0,0,source.Width,source.Height);
                int left=source.Width,top=source.Height,right=0,bottom=0;
                for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)if(((uint)pixels[y*source.Width+x]>>24)>8){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
                image=left<=right?Bitmap.CreateBitmap(source,left,top,right-left+1,bottom-top+1):null;
            }catch{image=null;}
            judgementImages[name]=image;
        }
        if(image is null)return false;
        float scale=Math.Min(target.Width()/image.Width,target.Height()/image.Height),w=image.Width*scale,h=image.Height*scale;
        paint.Color=Color.White;paint.SetStyle(Paint.Style.Fill);
        canvas.DrawBitmap(image,null,new RectF(target.CenterX()-w/2,target.CenterY()-h/2,target.CenterX()+w/2,target.CenterY()+h/2),paint);return true;
    }
}
