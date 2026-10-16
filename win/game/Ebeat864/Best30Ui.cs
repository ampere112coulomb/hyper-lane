using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace HyperLane;
public sealed partial class GameForm
{
    private Best30Panel? best30Panel;
    private sealed record Best30Card(string Title,string Difficulty,double Level,string LevelLabel,PlayerRating.Entry Entry,Bitmap? Jacket);
    private void OpenBest30()
    {
        if(best30Panel!=null)return;
        var cards=new List<Best30Card>();
        foreach(var song in RatingLibrary){
            try{
                string hash=HashChart(Path.Combine(song.BasePath,song.ChartFile));
                foreach(var diff in song.Chart.Difficulties.Values){
                    if(!PlayerRating.IsRatedDifficulty(diff.Name)||!ChartLevel.TryParse(diff.Level,out double level))continue;
                    var entry=playerRating?.BestEntry(PlayerRating.Key(Path.GetRelativePath(root,song.BasePath),song.ChartFile,diff.Name),hash,level);
                    if(entry!=null)cards.Add(new(song.Title,diff.Name,level,diff.DisplayLevel,entry,null));
                }
            }catch(IOException){}catch(UnauthorizedAccessException){}
        }
        var top=cards.OrderByDescending(c=>PlayerRating.ChartRating(c.Level,c.Entry.Score)).ThenBy(c=>c.Title).Take(30).ToList();
        for(int i=0;i<top.Count;i++){
            var song=RatingLibrary.FirstOrDefault(s=>s.Title==top[i].Title && s.Chart.Difficulties.ContainsKey(top[i].Difficulty));
            if(song==null)continue;
            try{string path=Path.Combine(song.BasePath,song.Chart.Jacket);if(File.Exists(path)){using var image=Image.FromFile(path);var thumb=new Bitmap(160,160);using(var g=Graphics.FromImage(thumb)){g.Clear(Color.FromArgb(15,10,24));float scale=Math.Max(160f/image.Width,160f/image.Height);g.DrawImage(image,(160-image.Width*scale)/2,(160-image.Height*scale)/2,image.Width*scale,image.Height*scale);}top[i]=top[i] with{Jacket=thumb};}}catch{}
        }
        best30Panel=new Best30Panel(this,top,userProfile.UserName,ratingTotal){Dock=DockStyle.Fill};
        Controls.Add(best30Panel);best30Panel.BringToFront();best30Panel.Focus();
    }
    private void CloseBest30(){if(best30Panel==null)return;Controls.Remove(best30Panel);best30Panel.Dispose();best30Panel=null;Focus();}
    private sealed class Best30Panel(GameForm game,List<Best30Card> cards,string name,double rate):Panel
    {
        private readonly Dictionary<string,(string Text,float Size)> titleLayouts=new();
        private RectangleF BackBounds=>new(ClientSize.Width-190,ClientSize.Height-62,178,50);
        private bool backHovered;
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);bool hover=BackBounds.Contains(e.Location);if(hover!=backHovered){backHovered=hover;if(hover)game.PlayMenuSound(2);Cursor=hover?Cursors.Hand:Cursors.Default;Invalidate();}}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);backHovered=false;Cursor=Cursors.Default;Invalidate();}
        protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);if(e.Button==MouseButtons.Left && BackBounds.Contains(e.Location)){game.PlayMenuSound(1);game.CloseBest30();}}
        private void DrawCardTitle(Graphics g,string text,RectangleF bounds)
        {
            if(!titleLayouts.TryGetValue(text,out var layout)){
                using var baseFont=new Font("Yu Gothic UI",19,FontStyle.Bold,GraphicsUnit.Pixel);
                using var measure=StringFormat.GenericTypographic;
                float Width(string value)=>g.MeasureString(value,baseFont,int.MaxValue,measure).Width;
                float width=Width(text);layout=(text,19);
                if(width>bounds.Width){
                    var starts=System.Globalization.StringInfo.ParseCombiningCharacters(text);
                    float best=float.MaxValue;int split=0;
                    foreach(int position in starts.Skip(1)){
                        float widest=Math.Max(Width(text[..position]),Width(text[position..]));
                        if(widest<best){best=widest;split=position;}
                    }
                    if(split>0)layout=(text[..split]+"\n"+text[split..],Math.Min(19,19*(bounds.Width-2)/best));
                    else layout=(text,19*(bounds.Width-2)/width);
                }
                titleLayouts[text]=layout;
            }
            using var font=new Font("Yu Gothic UI",Math.Max(1,layout.Size),FontStyle.Bold,GraphicsUnit.Pixel);
            using var format=new StringFormat(StringFormat.GenericTypographic){Trimming=StringTrimming.None,FormatFlags=StringFormatFlags.NoWrap};
            g.DrawString(layout.Text,font,Brushes.White,bounds,format);
        }
        private readonly System.Windows.Forms.Timer animation=new(){Interval=50};
        protected override void OnHandleCreated(EventArgs e){SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint,true);base.OnHandleCreated(e);animation.Tick+=Animate;animation.Start();}
        private void Animate(object? sender,EventArgs e)=>Invalidate();
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        {
            if(keyData==Keys.F11){game.ToggleFullscreen();return true;}
            if(keyData==Keys.Escape){game.CloseBest30();return true;}
            return base.ProcessCmdKey(ref msg,keyData);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.Clear(Color.FromArgb(14,9,28));g.SmoothingMode=SmoothingMode.AntiAlias;
            if(game.DefaultMenuBackground() is Bitmap background){
                using var attributes=new System.Drawing.Imaging.ImageAttributes();float light=(float)game.DefaultBackgroundLight;
                attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix{Matrix00=light,Matrix11=light,Matrix22=light,Matrix33=1,Matrix44=1});
                float scale=Math.Max(ClientSize.Width/(float)background.Width,ClientSize.Height/(float)background.Height);
                var dest=new Rectangle((int)((ClientSize.Width-background.Width*scale)/2),(int)((ClientSize.Height-background.Height*scale)/2),(int)(background.Width*scale),(int)(background.Height*scale));
                g.DrawImage(background,dest,0,0,background.Width,background.Height,GraphicsUnit.Pixel,attributes);
            }
            game.DrawBackgroundParticles(g,ClientSize);
            var state=g.Save();g.ScaleTransform(ClientSize.Width/1920f,Math.Max(1,ClientSize.Height-40)/1040f);
            using var title=new Font("Yu Gothic UI",36,FontStyle.Bold,GraphicsUnit.Pixel);
            using var small=new Font("Yu Gothic UI",16,FontStyle.Regular,GraphicsUnit.Pixel);
            using var scoreFont=new Font("Yu Gothic UI",27,FontStyle.Bold,GraphicsUnit.Pixel);
            using var header=new SolidBrush(Color.FromArgb(195,5,3,12));g.FillPolygon(header,new[]{new PointF(0,0),new PointF(1090,0),new PointF(1150,110),new PointF(0,110)});
            g.DrawString($"{name}    RATE {rate:0.00}",title,Brushes.White,35,20);
            for(int i=0;i<30;i++){
                float x=30+i%6*315,y=135+i/6*176;
                using var fill=new SolidBrush(Color.FromArgb(205,8,4,16));
                var shape=new[]{new PointF(x,y),new PointF(x+276,y),new PointF(x+300,y+160),new PointF(x+24,y+160)};
                g.FillPolygon(fill,shape);
                if(i>=cards.Count){g.DrawString($"#{i+1}  未プレイ",small,Brushes.Gray,x+25,y+15);continue;}
                var c=cards[i];if(c.Jacket!=null){var clip=g.Save();using var path=new GraphicsPath();path.AddPolygon(new[]{new PointF(x,y),new PointF(x+90,y),new PointF(x+110,y+160),new PointF(x+24,y+160)});g.SetClip(path);g.DrawImage(c.Jacket,x-25,y,160,160);g.Restore(clip);}
                g.DrawString($"#{i+1}   {PlayerRating.ChartRating(c.Level,c.Entry.Score):0.000}",small,Brushes.Cyan,x+118,y+6);
                DrawCardTitle(g,c.Title,new RectangleF(x+118,y+27,174,46));
                using var ink=new SolidBrush(DifficultyColor(c.Difficulty));g.DrawString($"{c.Difficulty} Lv.{c.LevelLabel}",small,ink,x+118,y+73);
                g.DrawString(c.Entry.Score.ToString("N0"),scoreFont,Brushes.Cyan,x+118,y+91);
                g.DrawString($"ACC {c.Entry.Score/10000d:0.00}%",small,Brushes.White,x+118,y+120);
                string stats=c.Entry.Counts is {Length:5} n?$"P {n[0]} G {n[1]} GD {n[2]} B {n[3]} M {n[4]}":"P — G — GD — B — M —";
                using var tiny=new Font("Yu Gothic UI",10,FontStyle.Regular,GraphicsUnit.Pixel);g.DrawString(stats,tiny,Brushes.LightGray,new RectangleF(x+118,y+137,174,23));
            }
            g.Restore(state);
            var back=BackBounds;
            using var backFill=new SolidBrush(Color.FromArgb(backHovered?215:160,8,4,16));
            g.FillPolygon(backFill,new[]{new PointF(back.Left+24,back.Top),new PointF(back.Right,back.Top),new PointF(back.Right,back.Bottom),new PointF(back.Left,back.Bottom)});
            using var arrow=new Pen(Color.FromArgb(backHovered?255:220,Color.White),3){StartCap=LineCap.Round,EndCap=LineCap.Round};
            float centerX=back.Left+back.Width/2+6,centerY=back.Top+back.Height/2;
            g.DrawLine(arrow,centerX+20,centerY,centerX-20,centerY);
            g.DrawLine(arrow,centerX-20,centerY,centerX-8,centerY-12);
            g.DrawLine(arrow,centerX-20,centerY,centerX-8,centerY+12);
        }
        protected override void OnResize(EventArgs e){base.OnResize(e);Invalidate();}
        protected override void Dispose(bool disposing){if(disposing){animation.Stop();animation.Tick-=Animate;animation.Dispose();foreach(var c in cards)c.Jacket?.Dispose();}base.Dispose(disposing);}
    }
}
