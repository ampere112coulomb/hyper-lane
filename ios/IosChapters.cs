using CoreGraphics;
using UIKit;
using HyperLane.Android;
using static HyperLane.iOS.IosVisuals;
namespace HyperLane.iOS;
internal sealed partial class MigrationStage
{
    private readonly MobileChapterMenu chapterMenu=new();
    private float chapterDownY;
    private void ChapterScreen(CGContext c)
    {
        Polygon(c,[200,0,1490,0,1652,1080,362,1080],Color(2,4,14,.41));
        foreach(int i in chapterMenu.Visible()){
            var r=chapterMenu.Rect(i);double x=r.X,y=r.Y,w=r.Width,h=r.Height,cut=r.Cut,e=chapterMenu.Expansion(i);bool active=i==chapterMenu.Index;
            double[] points=[x+cut,y,x+w+cut,y,x+w-cut,y+h,x-cut,y+h];c.SaveState();Polygon(c,points,clip:true);Fill(c,Rect(x-cut,y,w+2*cut,h),Color(1,2,7,.86));
            var entry=chapterMenu.Entries[i];if(entry.Kind is not "dan" and not "practice"){
                var image=visuals.FileImage(entry.Image)??visuals.Asset("skin/background.png");if(!active)image=visuals.Blur(image,"chapter:"+entry.Key);
                double left=x+w*(.24-.19*e),width=w*(.52+.38*e);c.SaveState();Polygon(c,[left+cut,y,left+width+cut,y,left+width-cut,y+h,left-cut,y+h],clip:true);Image(image,Rect(left-cut,y,width+2*cut,h),.42+.58*e,crop:true);c.RestoreState();
            }
            bool expanded=e>.5&&entry.Kind is not "dan" and not "practice";var text=expanded?Rect(x+30,y+h-125,w-110-cut,105):Rect(x,y,w,h);Text(entry.Name,text,44+32*e,center:!expanded);c.RestoreState();
            c.SaveState();c.SetStrokeColor(Color(255,255,255,active?.96:.57).CGColor);c.SetLineWidth(4);c.MoveTo((nfloat)(x+cut),(nfloat)y);c.AddLineToPoint((nfloat)(x-cut),(nfloat)(y+h));c.MoveTo((nfloat)(x+w+cut),(nfloat)y);c.AddLineToPoint((nfloat)(x+w-cut),(nfloat)(y+h));c.StrokePath();c.RestoreState();
        }
        Text("←",Rect(40,30,250,66),44,center:true);
    }
    private void TouchChapter(float x,float y,int phase)
    {
        if(phase==0){chapterDownY=y;return;}if(phase!=2||transitionAt>0)return;
        if(Math.Abs(y-chapterDownY)>45){chapterMenu.Move((int)Math.Round((chapterDownY-y)/160));return;}
        if(x<300&&y<120){Transition(Screen.Title);return;}
        foreach(int i in chapterMenu.Visible())if(chapterMenu.Rect(i).Contains(x,y)){
            if(i!=chapterMenu.Index){chapterMenu.Move(i-chapterMenu.Index);break;}
            var entry=chapterMenu.Entries[i];mode=entry.Kind=="dan"?1:entry.Kind=="practice"?2:0;
            if(entry.Kind=="dan"){DanRequested?.Invoke();return;}
            chapterMenu.ActivateFilter();ApplySearch("");Transition(Screen.Select);break;
        }
    }
}
