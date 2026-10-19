using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace HyperLane.Android;

internal sealed partial class GameSurface
{
    private readonly string[] plateNames = ["Master", "Expert", "Hard", "Normal", "Easy"];
    private Bitmap? selectionJacket, selectionBackground, previousSelectionBackground;
    private MobileSong? selectionLoaded;
    private string songSearch = "";
    private float selectionDownY;
    private double selectionPosition, selectionTarget;
    private bool selectionDragging;
    private double selectionDragStart;
    private long selectionFadeAt;
    private string selectionPreviewDifficulty="";
    private readonly Dictionary<string, int> selectionBest = new(StringComparer.OrdinalIgnoreCase);
    private static string DifficultyName(string name) => string.IsNullOrWhiteSpace(name) || name.Equals("Custom", StringComparison.OrdinalIgnoreCase) ? "SPECIAL" : name;

    private int songSort;
    private bool SongSortEnabled => !danMode && (practiceMode || chapterMenu.Filter=="@all");
    private List<int> SelectionIndices() {
        var indices=Enumerable.Range(0,songs.Count).Where(i=>(songs[i].Chart.Title+" "+songs[i].Chart.Composer).Contains(songSearch,StringComparison.OrdinalIgnoreCase));
        MobileDifficulty? Diff(int i)=>songs[i].Chart.Difficulties.FirstOrDefault(d=>d.Name.Equals(SelectedDifficulty?.Name,StringComparison.OrdinalIgnoreCase))??songs[i].Chart.Difficulties.FirstOrDefault();
        return SongSortEnabled&&songSort!=0?indices.OrderByDescending(i=>songSort==1?(double.TryParse(Diff(i)?.Level,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)?n:double.NegativeInfinity):BestScore(songs[i],Diff(i))??-1).ThenBy(i=>i).ToList():indices.ToList();
    }

    private IEnumerable<(string Name, int Index, int SkinColumn, RectF Bounds)> SelectionPlates()
    {
        if (SelectedSong is not { } song) yield break;
        var names = plateNames.Concat(song.Chart.Difficulties.Select(d => d.Name).Where(n => !plateNames.Contains(n, StringComparer.OrdinalIgnoreCase))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        float step = 940f / names.Count, w = step - 9, h = Math.Min(155, w * .8660254f);
        for (int i = 0; i < names.Count; i++)
            yield return (names[i], song.Chart.Difficulties.FindIndex(d => d.Name.Equals(names[i], StringComparison.OrdinalIgnoreCase)), -1,
                new RectF(978 + i * step, 738 + (155-h)/2, 978+i*step+w,738+(155+h)/2));
    }

    private void LoadSelectionArt()
    {
        if (ReferenceEquals(selectionLoaded, SelectedSong)) {
            if(selectionPreviewDifficulty!=(SelectedDifficulty?.Name??"")){selectionPreviewDifficulty=SelectedDifficulty?.Name??"";StartSelectionPreview();}
            return;
        }
        selectionLoaded = SelectedSong;
        selectionJacket?.Dispose();
        previousSelectionBackground?.Dispose(); previousSelectionBackground = selectionBackground;
        selectionJacket = selectionBackground = null;
        if (SelectedSong is not { } song) return;
        string jp = FindMedia(song.Folder, song.Chart.Jacket, ".png", ".jpg", ".jpeg", ".webp");
        string bp = FindChartBackground(song);
        if (jp.Length > 0) selectionJacket = BitmapFactory.DecodeFile(jp);
        if (bp.Length > 0) { using var original = BitmapFactory.DecodeFile(bp); if (original != null) selectionBackground = MobileResultBackground.Create(original); }
        selectionFadeAt = Environment.TickCount64;
        selectionPreviewDifficulty=SelectedDifficulty?.Name??"";StartSelectionPreview();
    }

    private int? BestScore(MobileSong song, MobileDifficulty? diff) => diff != null && selectionBest.TryGetValue(RatingKey(song, diff), out int best) ? best : null;

    private void DrawNativeSelect(Canvas canvas)
    {
        LoadSelectionArt();
        float fade = Math.Clamp((Environment.TickCount64-selectionFadeAt)/500f,0,1);
        DrawMenuBackground(canvas);
        if (previousSelectionBackground != null && fade < 1) ResultImage(canvas, previousSelectionBackground, new RectF(0,0,1920,1080),settings.PreviewBackgroundBrightness/100f);
        if (selectionBackground != null) ResultImage(canvas, selectionBackground,new RectF(0,0,1920,1080),fade*settings.PreviewBackgroundBrightness/100f);
        DrawMenuParticles(canvas);
        using var wall = Polygon([0,0,832,0,960,1080,0,1080]);
        paint.Color=Color.Argb(90,7,4,15);paint.SetStyle(Paint.Style.Fill);canvas.DrawPath(wall,paint);
        DrawSelectionHighlight(canvas);
        List<int> visible=SelectionIndices();
        if(visible.Count>0)
        {
            if(!selectionDragging) {
                selectionTarget=Math.Max(0,visible.IndexOf(songIndex));
                selectionPosition+=(selectionTarget-selectionPosition)*.18;
                if(Math.Abs(selectionTarget-selectionPosition)<.001)selectionPosition=selectionTarget;
            }
            int center=(int)Math.Round(selectionPosition);
            for(int offset=-7;offset<=7;offset++)
            {
                int position=center+offset;if(position<0||position>=visible.Count)continue;int index=visible[position];
                float top=(float)(461+(position-selectionPosition)*92);
                if(top+86<0||top>1080)continue;
                var song=songs[index];var diff=song.Chart.Difficulties.FirstOrDefault(d=>d.Name.Equals(SelectedDifficulty?.Name,StringComparison.OrdinalIgnoreCase))??song.Chart.Difficulties.FirstOrDefault();
                float x=1920*(.035f+.065f*((top+43)/1080-.464f)),width=1920*.410f;
                var color=DifficultyColor(diff?.Name??"");
                var title=new RectF(x,top,x+width-220,top+65);
                ResultText(canvas,song.Chart.Title,title,50,1,Paint.Align.Center);
                ResultText(canvas,song.Chart.Composer,new RectF(title.Left,top+62,title.Right,top+86),16,.8f,Paint.Align.Center);
                SelectionLevel(canvas,diff?.DisplayLevel??"–",new RectF(x+width-140,top,x+width,top+86),53,color,Paint.Align.Right);
                int? best=BestScore(song,diff);
                bool fc=diff!=null&&HyperLane.ChartLevel.TryParse(diff.Level,out double rankLevel)&&rating.BestEntry(RatingKey(song,diff),Hash(song),rankLevel)is {} entry&&(entry.FullCombo||HyperLane.RankRules.IsFullCombo(entry.Counts));
                if(best.HasValue&&Rank(best.Value).Length>0&&MobileRank(best.Value,fc) is {} rank)
                    ResultImage(canvas,rank,new RectF(x+width-208,top+9,x+width-140,top+77),1);
            }
            if(selectionJacket!=null)
            {
                float fit=Math.Min(582f/selectionJacket.Width,582f/selectionJacket.Height),w=selectionJacket.Width*fit,h=selectionJacket.Height*fit;
                ResultImage(canvas,selectionJacket,new RectF(1152+(582-w)/2,82+(582-h)/2,1152+(582+w)/2,82+(582+h)/2),1);
            }
            int? high=BestScore(SelectedSong!,SelectedDifficulty);
            Fill(canvas,new RectF(1152,677,1734,722),Color.Argb(160,5,4,9));
            ResultText(canvas,"HIGH SCORE  "+(high.HasValue?high.Value.ToString("N0"):"—"),new RectF(1152,677,1734,722),26,1,Paint.Align.Center);
            foreach(var(name,index,_,rect)in SelectionPlates())
            {
                Color color=DifficultyColor(name);bool active=index>=0&&index==difficultyIndex;
                using var hex=Polygon([rect.Left+rect.Width()/4,rect.Top,rect.Right-rect.Width()/4,rect.Top,rect.Right,rect.CenterY(),rect.Right-rect.Width()/4,rect.Bottom,rect.Left+rect.Width()/4,rect.Bottom,rect.Left,rect.CenterY()]);
                paint.Color=Color.Argb(index<0?150:240,0,0,0);paint.SetStyle(Paint.Style.Fill);canvas.DrawPath(hex,paint);
                paint.Color=Alpha(color,index<0?.25f:.8f);paint.SetStyle(Paint.Style.Stroke);paint.StrokeWidth=active?3:1;canvas.DrawPath(hex,paint);paint.SetStyle(Paint.Style.Fill);
                paint.Color=Alpha(color,index<0?.4f:1);paint.SetTypeface(checkpoint);paint.TextSize=Math.Min(25,rect.Width()/7);
                string label=DifficultyName(name).ToUpperInvariant();
                canvas.DrawText(label,rect.CenterX()-paint.MeasureText(label)/2,rect.Top+rect.Height()*.36f,paint);
                if(index>=0)SelectionLevel(canvas,SelectedSong!.Chart.Difficulties[index].DisplayLevel,new RectF(rect.Left,rect.Top+rect.Height()*.48f,rect.Right,rect.Top+rect.Height()*.86f),34,color,Paint.Align.Center);
            }
        }
        else ResultText(canvas,"曲フォルダまたはZIPを追加してください",new RectF(100,430,790,530),32,1,Paint.Align.Center);
        SelectionButton(canvas,"play",new RectF(1023,945,1361,1052),57);
        SelectionButton(canvas,practiceMode?"モード選択":"replay",new RectF(1373,945,1711,1052),practiceMode?34:57);
        SelectionButton(canvas,"⚙",new RectF(1757,935,1867,1080),60);
        ResultText(canvas,"⌕",new RectF(25,15,90,75),45,1);
        if(SongSortEnabled){Fill(canvas,new RectF(25,80,240,130),Color.Argb(220,7,12,30));for(int i=0;i<3;i++)Fill(canvas,new RectF(34,89+i*10,66-i*10,91+i*10),Color.White);ResultText(canvas,new[]{"default","level","score"}[songSort],new RectF(82,80,235,130),28,1);}
        ScreenButton(canvas,"←",new RectF(100,12,260,76),26);
        Fill(canvas,new RectF(978,12,1398,108),Color.Argb(235,25,18,38));
        Stroke(canvas,new RectF(978,12,1398,108),Color.Rgb(178,146,225),3);
        ResultText(canvas,"＋ 曲を追加・更新",new RectF(988,18,1388,102),34,1,Paint.Align.Center);
        if(settings.AutoPlay)ResultText(canvas,"AUTO",new RectF(970,900,1120,940),19,1);
        UpdateSelectionPreview();
        DrawMobileUserName(canvas,false);
        DrawBadgeOverlay(canvas);
        PostInvalidateDelayed(33);
    }

    private void SelectionLevel(Canvas canvas,string text,RectF rect,float size,Color color,Paint.Align align)
    {
        paint.SetTypeface(Typeface.Default);paint.TextSize=size;paint.Color=color;paint.TextAlign=align;paint.SetStyle(Paint.Style.Fill);
        float width=paint.MeasureText(text);
        if(width>rect.Width()-12)paint.TextSize*=Math.Max(1,rect.Width()-12)/width;
        canvas.DrawText(text,align==Paint.Align.Right?rect.Right-6:rect.CenterX(),rect.CenterY()-(paint.Ascent()+paint.Descent())/2,paint);paint.TextAlign=Paint.Align.Left;
    }

    private void DrawSelectionHighlight(Canvas canvas)
    {
        using var strip=Polygon([0,461,832+128*461f/1080,461,832+128*542f/1080,542,0,542]);
        paint.SetStyle(Paint.Style.Fill);paint.Color=Color.Argb(85,240,236,247);canvas.DrawPath(strip,paint);
    }

    private void SelectionCentered(Canvas canvas,string text,float center,float baseline,float size,Color color,float maxWidth,bool number=false)
    {
        paint.SetTypeface(number?checkpoint:Typeface.Default);paint.TextSize=size;float width=paint.MeasureText(text);
        if(width>maxWidth)size*=maxWidth/width;
        Text(canvas,text,center-width*Math.Min(1,maxWidth/Math.Max(1,width))/2,baseline,size,color,number);
    }
    private void SelectionButton(Canvas canvas,string label,RectF rect,float size)
    {
        using var shape=Polygon([rect.Left+rect.Width()/10,rect.Top,rect.Right-rect.Width()/10,rect.Top,rect.Right,rect.CenterY(),rect.Right-rect.Width()/10,rect.Bottom,rect.Left+rect.Width()/10,rect.Bottom,rect.Left,rect.CenterY()]);
        paint.Color=Color.Argb(160,5,4,9);paint.SetStyle(Paint.Style.Fill);canvas.DrawPath(shape,paint);
        ResultText(canvas,label,rect,size,1,Paint.Align.Center);
    }

    private bool TouchSelection(MotionEvent e,float x,float y)
    {
        if(SongSortEnabled&&x>=25&&x<=240&&y>=80&&y<=130){selectionDragging=false;if(e.ActionMasked==MotionEventActions.Up){songSort=(songSort+1)%3;var sorted=SelectionIndices();selectionPosition=selectionTarget=Math.Max(0,sorted.IndexOf(songIndex));Invalidate();}return true;}
        if(e.ActionMasked==MotionEventActions.Down) { selectionDownY=y;selectionDragging=x<960&&y>80;selectionDragStart=selectionPosition; }
        if(e.ActionMasked==MotionEventActions.Move&&selectionDragging) { selectionPosition=Math.Clamp(selectionDragStart+(selectionDownY-y)/92,0,Math.Max(0,SelectionIndices().Count-1));Invalidate();return true; }
        if(e.ActionMasked==MotionEventActions.Cancel) { selectionDragging=false;Invalidate();return true; }
        if(e.ActionMasked!=MotionEventActions.Up)return true;
        bool dragged=selectionDragging&&Math.Abs(y-selectionDownY)>35;selectionDragging=false;
        List<int> visible=SelectionIndices();
        if(Hit(x,y,1688,0,1920,62)){OpenMobileBest30();return true;}
        if(x<95&&y<80) {
            var input=new EditText(Context){Text=songSearch};input.SetSingleLine(true);
            new global::Android.App.AlertDialog.Builder(Context!)!.SetTitle("楽曲検索")!.SetView(input)!.SetPositiveButton("検索",(_,_)=>{songSearch=input.Text??"";var matches=SelectionIndices();if(matches.Count>0)songIndex=matches[0];Invalidate();})!.SetNegativeButton("キャンセル",(_,_)=>{})!.Show();
        }
        else if(Hit(x,y,100,12,260,76))BeginMenuTransition(Page.Modes);
        else if(x>=978&&x<=1398&&y>=12&&y<=108)importZip();
        else if(x>=1757&&y>=935)OpenSettingsPage(Page.Select);
        else if(x>=1023&&x<=1361&&y>=945) { if(practiceMode)OpenPractice();else StartSelected(); }
        else if(x>=1373&&x<=1711&&y>=945) { if(practiceMode)BeginMenuTransition(Page.Modes);else StartMobileReplay(); }
        else if(x>=978&&y>=738&&y<=893) { foreach(var slot in SelectionPlates())if(slot.Index>=0&&slot.Bounds.Contains(x,y)){difficultyIndex=slot.Index;break;} }
        else if(x<960&&visible.Count>0) {
            int position=dragged?(int)Math.Round(selectionPosition):(int)Math.Round(selectionPosition+(y-501.5)/92);
            string name=SelectedDifficulty?.Name??"";
            songIndex=visible[Math.Clamp(position,0,visible.Count-1)];
            difficultyIndex=Math.Max(0,SelectedSong!.Chart.Difficulties.FindIndex(d=>d.Name.Equals(name,StringComparison.OrdinalIgnoreCase)));
        }
        Invalidate();return true;
    }
}
