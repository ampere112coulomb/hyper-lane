using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.App;
using Android.Widget;
using System.Diagnostics;

namespace HyperLane.Android;

internal sealed partial class GameSurface
{
    private int settingsTab;
    private Page settingsReturn = Page.Select;
    private long resultStartedAt;
    private Bitmap? resultBlur;
    private readonly Dictionary<string, Bitmap?> resultSprites = new();
    private readonly HyperLane.PlayerRating rating;
    private double ratingTotal;

    private void OpenSettingsPage(Page returnPage)
    { StopSelectionPreview(); settingsReturn = returnPage; page = Page.Settings; settingsTab = 0; Invalidate(); }

    private void DrawNativeSettings(Canvas canvas)
    {
        int saved = canvas.Save();
        canvas.Scale(1.4f, 1.4f, 960, 540);
        DrawSettingsContents(canvas);
        canvas.RestoreToCount(saved);
    }

    private void DrawSettingsContents(Canvas canvas)
    {
        if(selectionBackground!=null)ResultImage(canvas,selectionBackground,new RectF(0,0,1920,1080),.5f);
        Fill(canvas, new RectF(550, 190, 1370, 890), Color.Argb(225, 0, 0, 0));
        UiText(canvas, "設 定", 570, 202, 25, Color.White);
        string[] tabs = ["プレイ・音声", "映像・演出", "プリセット"];
        for (int i = 0; i < 3; i++)
        {
            RectF rect = new(566 + i * 250, 240, 816 + i * 250, 280);
            Fill(canvas, rect, i == settingsTab ? Color.Rgb(47, 40, 57) : Color.Rgb(15, 13, 19));
            UiText(canvas, tabs[i], rect.CenterX(), 250, 21, center: true);
            if (i == settingsTab) Line(canvas, rect.Left, rect.Bottom, rect.Right, rect.Bottom, Color.Rgb(178, 146, 225), 3);
        }
        if (settingsTab < 2)
        {
            string[] labels = settingsTab == 0 ? ["ノーツスピード", "ゲーム時の楽曲音量", "曲選択プレビュー音量", "ノーツ効果音の音量", "譜面補正（+でノーツを遅く）", "オートプレイ", "自動SE", "タッチ判定の高さ","音声バッファ（frames）", "タッチ方式"]
                : ["レーンの透明度", "ゲーム背景の明るさ", "プレビュー背景の明るさ", "開始前の余白", "フィーバーエフェクトの明るさ", "MVを優先", "キービームの透明度", "キービームの長さ", "レーンカバー"];
            string[] values = settingsTab == 0 ? [$"{settings.NoteSpeed:0.00} ×", $"{settings.BgmVolume}%", $"{settings.PreviewVolume}%", $"{settings.SeVolume}%", $"{settings.AudioOffsetMs} ms", settings.AutoPlay ? "ON" : "OFF", settings.AutoNoteSound ? "ON" : "OFF", $"{settings.TouchHeight}", $"{settings.AudioBufferFrames}", settings.LaneTouch ? "レーンタッチ" : "パネルタッチ"]
                : [$"{settings.LaneTransparency}%", $"{settings.BackgroundBrightness}%", $"{settings.PreviewBackgroundBrightness}%", $"{settings.StartMarginMs} ms", $"{settings.FeverAuraBrightness}%", settings.PreferMusicVideo ? "ON" : "OFF", $"{settings.KeyBeamOpacity}%", $"{settings.KeyBeamLength}", $"{settings.LaneCover}%"];
            for (int i = 0; i < labels.Length; i++)
            {
                float y = 300 + i * 48;
                UiText(canvas, labels[i], 586, y + 7, 22);
                if(IsMobileSlider(settingsTab,i)) {
                    double sliderValue=MobileSliderValue(settingsTab,i),fraction=settingsTab==0&&i==0?(sliderValue-.5)/9.5:sliderValue/100;
                    float thumb=1080+(float)fraction*125;
                    Line(canvas,1080,y+21,1205,y+21,Color.Rgb(85,85,90),3);
                    Line(canvas,1080,y+21,thumb,y+21,Color.Rgb(178,146,225),3);
                    paint.Color=Color.White;paint.SetStyle(Paint.Style.Fill);canvas.DrawCircle(thumb,y+21,6,paint);
                    UiText(canvas,values[i],1258,y+9,20,Color.White,center:true);
                    continue;
                }
                RectF minus = new(1080, y, 1130, y + 42), value = new(1138, y, 1250, y + 42), plus = new(1258, y, 1308, y + 42);
                ScreenButton(canvas, "−", minus, 23); ScreenButton(canvas, "+", plus, 23);
                Fill(canvas, value, Color.Rgb(9, 27, 49)); UiText(canvas, values[i], value.CenterX(), y + 9, 21, Color.Cyan, center: true);
            }
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                UiText(canvas, "プリセット " + (i + 1), 586, 327 + i * 125, 26);
                ScreenButton(canvas, "現在の設定を保存", new RectF(795, 317 + i * 125, 1040, 372 + i * 125), 22);
                ScreenButton(canvas, "読み込み", new RectF(1055, 317 + i * 125, 1308, 372 + i * 125), 22);
            }
        }
        if(settingsTab==0)UiText(canvas,player?.BufferStatus??"次の曲開始時に適用",586,795,17,Color.Cyan);
        ScreenButton(canvas, "保存して閉じる", new RectF(1080, 832, 1340, 878), 23);
    }

    private bool TouchNativeSettings(MotionEvent e, float x, float y)
    {
        x = 960 + (x - 960) / 1.4f;
        y = 540 + (y - 540) / 1.4f;
        int sliderRow=(int)((y-300)/48);
        if(settingsTab<2&&y>=300&&y<=342+sliderRow*48&&x>=1065&&x<=1215&&IsMobileSlider(settingsTab,sliderRow)&&e.ActionMasked is MotionEventActions.Down or MotionEventActions.Move or MotionEventActions.Up) {
            double fraction=Math.Clamp((x-1080)/125,0,1);
            SetMobileSlider(settingsTab,sliderRow,fraction);
            if(e.ActionMasked==MotionEventActions.Up)settings.Save(Context!);
            player?.SetVolume(settings.BgmVolume/100f,settings.BgmVolume/100f);Invalidate();return true;
        }
        if (e.ActionMasked != MotionEventActions.Up) return true;
        if(settingsTab==0&&Hit(x,y,1138,684,1250,726)){ShowBufferInput();return true;}
        if(settingsTab==0&&Hit(x,y,1080,732,1308,774)){settings.LaneTouch=!settings.LaneTouch;settings.Save(Context!);Invalidate();return true;}
        if(settingsTab==1&&Hit(x,y,1138,444,1250,486)){ShowMarginInput();return true;}
        if (settingsTab == 0 && Hit(x, y, 1138, 492, 1250, 534))
        {
            ShowOffsetInput(); return true;
        }
        if (Hit(x, y, 1080, 832, 1340, 878)) { settings.Save(Context!); page = settingsReturn; }
        else if (Hit(x, y, 566, 240, 1316, 280)) settingsTab = Math.Clamp((int)((x - 566) / 250), 0, 2);
        else if (settingsTab == 2)
        {
            int index = (int)((y - 317) / 125);
            if (index is >= 0 and < 3 && y <= 372 + index * 125)
            {
                string name = "preset_" + index;
                if (Hit(x, y, 795, 317 + index * 125, 1040, 372 + index * 125)) settings.Save(Context!, name);
                else if (Hit(x, y, 1055, 317 + index * 125, 1308, 372 + index * 125) && Context!.GetSharedPreferences(name, FileCreationMode.Private)!.Contains("note_speed"))
                {
                    bool auto = settings.AutoPlay;
                    settings = MobileSettings.Load(Context, name);
                    if (settingsReturn == Page.Paused) settings.AutoPlay = auto;
                    ApplyMobileBuffer();
                }
            }
        }
        else
        {
            int row = (int)((y - 300) / 48);
            int direction = x >= 1080 && x <= 1130 ? -1 : x >= 1258 && x <= 1308 ? 1 : 0;
            if (row >= 0 && y <= 342 + row * 48 && direction != 0)
            {
                if (settingsTab == 0)
                {
                    switch (row)
                    {
                        case 0: settings.NoteSpeed = Math.Round(Math.Clamp(settings.NoteSpeed + direction * .05, .5, 10), 2); break;
                        case 1: settings.BgmVolume = Percent(settings.BgmVolume, direction); break;
                        case 2: settings.PreviewVolume = Percent(settings.PreviewVolume, direction); break;
                        case 3: settings.SeVolume = Percent(settings.SeVolume, direction); break;
                        case 4: settings.AudioOffsetMs = Math.Clamp(settings.AudioOffsetMs + 5 * direction, -1000, 1000); break;
                        case 5: if (settingsReturn != Page.Paused) settings.AutoPlay = !settings.AutoPlay; break;
                        case 6: settings.AutoNoteSound = !settings.AutoNoteSound; break;
                        case 9: settings.LaneTouch = !settings.LaneTouch; break;
                        case 8: settings.AudioBufferFrames=Math.Clamp(settings.AudioBufferFrames+direction*256,64,4096);ApplyMobileBuffer();break;
                    }
                }
                else switch (row)
                {
                    case 0: settings.LaneTransparency = Percent(settings.LaneTransparency, direction); break;
                    case 1: settings.BackgroundBrightness = Percent(settings.BackgroundBrightness, direction); break;
                    case 2: settings.PreviewBackgroundBrightness = Percent(settings.PreviewBackgroundBrightness, direction); break;
                    case 3: settings.StartMarginMs = Math.Clamp(settings.StartMarginMs + 50 * direction, 0, 5000); break;
                    case 4: settings.FeverAuraBrightness = Percent(settings.FeverAuraBrightness, direction); break;
                    case 5: settings.PreferMusicVideo = !settings.PreferMusicVideo; break;
                    case 6: settings.KeyBeamOpacity = Percent(settings.KeyBeamOpacity, direction); break;
                    case 7: settings.KeyBeamLength = Percent(settings.KeyBeamLength, direction); break;
                    case 8: settings.LaneCover = Percent(settings.LaneCover, direction); break;
                }
                settings.Save(Context!);
                player?.SetVolume(settings.BgmVolume / 100f, settings.BgmVolume / 100f);
            }
        }
        Invalidate(); return true;
    }
    private void ShowOffsetInput()
    {
        var input = new EditText(Context) { Text = settings.AudioOffsetMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
            InputType = global::Android.Text.InputTypes.ClassNumber | global::Android.Text.InputTypes.NumberFlagSigned };
        input.SetSingleLine(true);
        new AlertDialog.Builder(Context!)!.SetTitle("譜面補正（ms）")!
            .SetMessage("＋：ノーツを遅くする / −：ノーツを早くする\n範囲：−1000～1000 ms")!.SetView(input)!
            .SetPositiveButton("適用", (_, _) =>
            {
                if (int.TryParse(input.Text, out int value))
                { settings.AudioOffsetMs = Math.Clamp(value, -1000, 1000); settings.Save(Context!); Invalidate(); }
            })!.SetNegativeButton("キャンセル", (_, _) => { })!.Show();
    }
    private static int Percent(int value, int direction) => Math.Clamp(value + direction, 0, 100);
    private static bool Hit(float x, float y, float left, float top, float right, float bottom) => x >= left && x <= right && y >= top && y <= bottom;

    internal void Suspend()
    { StopSelectionPreview(); if (page == Page.Playing) PausePlaying(); }
    private void PausePlaying()
    {
        if (page != Page.Playing) return;
        if (audioStarted) { player?.Pause(); videoPlayer?.Pause(); }
        playbackClock.Stop(); held.Clear(); fingers.Clear(); page = Page.Paused; Invalidate();
    }
    private void DrawPaused(Canvas canvas)
    {
        Fill(canvas, new RectF(0, 0, 1920, 1080), Color.Argb(220, 0, 8, 20));
        Fill(canvas, new RectF(682, 350, 1238, 715), Color.Argb(240, 11, 27, 49));
        Stroke(canvas, new RectF(682, 350, 1238, 715), Color.Rgb(95, 173, 225), 2);
        UiText(canvas, "PAUSE", 960, 372, 32, center: true);
        UiText(canvas, "プレイを一時停止しています", 960, 432, 16, Color.Cyan, center: true);
        string[] labels = ["再開", "リトライ", "設定", practicePlaying ? "開始拍選択" : "曲選択"];
        for (int i = 0; i < 4; i++)
        {
            float x = 710 + i % 2 * 260, y = 492 + i / 2 * 76;
            ScreenButton(canvas, labels[i], new RectF(x, y, x + 235, y + 62), 24);
        }
    }
    private bool TouchPaused(MotionEvent e, float x, float y)
    {
        if (e.ActionMasked != MotionEventActions.Up) return true;
        for (int i = 0; i < 4; i++)
        {
            float left = 710 + i % 2 * 260, top = 492 + i / 2 * 76;
            if (!Hit(x, y, left, top, left + 235, top + 62)) continue;
            switch (i)
            {
                case 0: page = Page.Playing; playbackClock.Start(); StartSeScheduler(); if (audioStarted) { player?.Start(); videoPlayer?.Start(); } break;
                case 1: if(danPlaying)BeginMobileDan();else StartSelected(); break;
                case 2: OpenSettingsPage(Page.Paused); break;
                case 3: if (practicePlaying) ReturnToPractice(); else StopPlaying(); break;
            }
        }
        Invalidate(); return true;
    }

    private void FinishPlaying()
    {
        if (page is not (Page.Playing or Page.Paused) || session is null) return;
        StopSeScheduler();
        if (practicePlaying) { ReturnToPractice(); return; }
        session.Advance(Math.Max(player?.Duration / 1000.0 ?? 0, session.Difficulty.Notes.Select(n => n.EndSeconds).DefaultIfEmpty(0).Max()) + .2, held);
        if(danPlaying){FinishMobileDanTrack();return;}
        StopVideo(); page = Page.Result; resultStartedAt = Environment.TickCount64;
        playbackClock.Stop();
        resultBlur?.Dispose(); resultBlur = background is null ? null : MobileResultBackground.Create(background);
        SaveMobileReplay();
        if (!settings.AutoPlay && !mobileReplaying && HyperLane.ChartLevel.TryParse(session.Difficulty.Level, out double level))
        {
            try { rating.Update(RatingKey(session.Song, session.Difficulty), Hash(session.Song), level, session.Score,session.Counts,session.MaxCombo); RefreshRating(); }
            catch (Exception error) { message = "スコア保存エラー: " + error.Message; }
        }
        Invalidate();
    }

    private static string Hash(MobileSong song) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(song.ChartPath)));
    private static string RatingKey(MobileSong song, MobileDifficulty diff) => HyperLane.PlayerRating.Key(System.IO.Path.GetFileName(song.Folder), System.IO.Path.GetFileName(song.ChartPath), diff.Name);
    private void RefreshRating()
    {
        var charts = new List<(string Key, string ChartHash, double Level)>();
        selectionBest.Clear();
        foreach (var song in allChapterSongs)
        {
            string hash = Hash(song);
            foreach (var diff in song.Chart.Difficulties) if (HyperLane.ChartLevel.TryParse(diff.Level, out double level))
            {
                string key = RatingKey(song, diff); charts.Add((key, hash, level));
                int? best = rating.BestScore(key, hash, level); if (best.HasValue) selectionBest[key] = best.Value;
            }
        }
        ratingTotal = rating.TotalFor(charts);
    }

    private void DrawNativeResult(Canvas canvas)
    {
        if (session is null) return;
        double elapsed = (Environment.TickCount64 - resultStartedAt) / 1000.0;
        float Phase(double start, double duration) => (float)Math.Clamp((elapsed - start) / duration, 0, 1);
        static float Ease(float t) => t >= 1 ? 1 : t <= 0 ? 0 : 1 - MathF.Pow(2, -10 * t);
        float info = Phase(1, 1), band = Phase(2, .5), bg = Ease(Phase(1, .7));
        Fill(canvas, new RectF(0, 0, 1920, 1080), Alpha(Color.Rgb(24, 3, 33), Phase(0, 1)));
        if (bg > 0)
        {
            int save = canvas.Save();
            using var center = Polygon([282, 0, 1898, 0, 1635, 1080, 22, 1080]); canvas.ClipPath(center);
            if (resultBlur is not null) ResultImage(canvas, resultBlur, new RectF(0, -155 * (1 - bg), 1920, 1080 - 155 * (1 - bg)), bg);
            Fill(canvas, new RectF(0, 0, 1920, 1080), Alpha(Color.Argb(60, 6, 4, 16), bg)); canvas.RestoreToCount(save);
        }
        if (jacket is not null) ResultImage(canvas, jacket, new RectF(350, 108, 756, 514), info);
        Stroke(canvas, new RectF(346, 104, 760, 518), Alpha(DifficultyColor(session.Difficulty.Name), info), 8);
        string title = session.Song.Chart.Title;
        ResultText(canvas, title, new RectF(790, 92, 1830, 186), title.Length > 29 ? 55 : title.Length > 20 ? 66 : 79, info);
        ResultText(canvas, session.Song.Chart.Composer, new RectF(790, 174, 1750, 244), 50, info);
        ResultText(canvas, "DESIGNER: " + session.Song.Chart.DesignerFor(session.Difficulty), new RectF(793, 257, 1753, 313), 39, info);
        ResultText(canvas, session.Difficulty.Name + " Lv." + session.Difficulty.DisplayLevel, new RectF(792, 464, 1442, 530), 42, info);
        using (var path = Polygon([127, 642, 1742, 642, 1635, 1080, 22, 1080]))
        { paint.Color = Alpha(Color.Argb(200, 8, 7, 14), band); canvas.DrawPath(path, paint); }
        using (var path = Polygon([112, 641, 427, 641, 507, 692, 427, 743, 103, 743]))
        { paint.Color = Alpha(Color.Argb(215, 8, 7, 14), band); canvas.DrawPath(path, paint); }
        ResultText(canvas, session.Score.ToString("D7"), new RectF(148, 648, 476, 731), 70, band);
        string[] labels = ["PERFECT", "GREAT", "GOOD", "BAD", "MISS", "MAX COMBO"];
        for (int i = 0; i < 6; i++)
        {
            float opacity = Phase(2.5 + i * .1, .1), top = 763 + i * 45;
            ResultText(canvas, labels[i], new RectF(145, top, 405, top + 46), 35, opacity);
            ResultText(canvas, (i == 5 ? session.MaxCombo : session.Counts[i]).ToString(), new RectF(357, top, 468, top + 46), 31, opacity, Paint.Align.Right);
        }
        float rankPhase = Phase(3.3, .5), scale = 1.5f - .5f * Ease(rankPhase);
        Bitmap? box = ResultSprite("rank_box.png");
        if (box is not null) ResultImage(canvas, box, CenteredRect(682, 874, 304 * scale), rankPhase);
        string rank = Rank(session.Score);
        Bitmap? letter = rank.Length == 0 ? null : ResultSprite("rank-" + rank + ".png");
        if (letter is not null) ResultImage(canvas, letter, CenteredRect(682, 874, 224 * scale), rankPhase);
        float baseline = Phase(3.1, .2);
        Line(canvas, 911, 1023, 1138, 1023, Alpha(Color.White, baseline), 3);
        ResultText(canvas, "late", new RectF(958, 1022, 1034, 1059), 26, baseline);
        ResultText(canvas, "fast", new RectF(1040, 1022, 1116, 1059), 26, baseline);
        float red = Phase(3.8, .2);
        using (var gradient = new LinearGradient(899, 0, 989, 0, Color.Transparent, Alpha(Color.Argb(175, 200, 24, 31), red), Shader.TileMode.Clamp!))
        { paint.SetShader(gradient); canvas.DrawRect(899, 717, 989, 1023, paint); paint.SetShader(null); }
        using (var gradient = new LinearGradient(1052, 0, 1142, 0, Alpha(Color.Argb(155, 200, 24, 31), red), Color.Transparent, Shader.TileMode.Clamp!))
        { paint.SetShader(gradient); canvas.DrawRect(1052, 717, 1142, 1023, paint); paint.SetShader(null); }
        float graph = Phase(4, .5);
        int[] bins = new int[12];
        foreach (double offset in session.TimingOffsets) bins[Math.Clamp((int)Math.Floor((120 - offset) / 20), 0, 11)]++;
        int max = Math.Max(1, bins.Max());
        for (int i = 0; i < bins.Length; i++)
        {
            float height = 230f * bins[i] / max * Ease(graph);
            Color color = i < 5 ? Color.Rgb(255, 142, 77) : i > 6 ? Color.Rgb(88, 216, 220) : Color.White;
            Fill(canvas, new RectF(924 + i * 16, 1022 - height, 940 + i * 16, 1022), Alpha(color, graph));
        }
        string[] actions = ["曲選択", "リトライ", "見返す"];
        for (int i = 0; i < actions.Length; i++) ScreenButton(canvas, actions[i], new RectF(1250, 694 + i * 91, 1637, 783 + i * 91), 42, Phase(4.5, .4));
        if (elapsed < 4.9) PostInvalidateOnAnimation();
        DrawMobileUserName(canvas,true);
    }

    private static string Rank(int score) => score >= 1000000 ? "Psi" : score >= 995000 ? "Ex+" : score >= 990000 ? "Ex" : score >= 950000 ? "SS" : score >= 900000 ? "S" : score >= 860000 ? "A" : score >= 820000 ? "B" : score >= 780000 ? "C" : "";
    private static Color Alpha(Color color, float opacity) => Color.Argb((int)Math.Round(color.A * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B);
    private static RectF CenteredRect(float x, float y, float size) => new(x - size / 2, y - size / 2, x + size / 2, y + size / 2);
    private static global::Android.Graphics.Path Polygon(float[] points)
    {
        var path = new global::Android.Graphics.Path(); path.MoveTo(points[0], points[1]);
        for (int i = 2; i < points.Length; i += 2) path.LineTo(points[i], points[i + 1]); path.Close(); return path;
    }
    private Bitmap? ResultSprite(string file)
    {
        if (resultSprites.TryGetValue(file, out var sprite)) return sprite;
        try { using var stream = Context!.Assets!.Open("skin/" + file); sprite = BitmapFactory.DecodeStream(stream); }
        catch { sprite = null; }
        resultSprites[file] = sprite; return sprite;
    }
    private void ResultImage(Canvas canvas, Bitmap image, RectF rect, float opacity)
    { paint.Color = Alpha(Color.White, opacity); paint.FilterBitmap = true; canvas.DrawBitmap(image, null, rect, paint); paint.Color = Color.White; }
    private void ResultText(Canvas canvas, string text, RectF rect, float size, float opacity, Paint.Align? align = null)
    {
        paint.Color = Alpha(Color.Rgb(250, 250, 252), opacity); paint.SetTypeface(Typeface.Create("sans-serif", TypefaceStyle.Normal));
        paint.TextSize = size; paint.TextAlign = align ?? Paint.Align.Left;
        int save = canvas.Save(); canvas.ClipRect(rect);
        canvas.DrawText(text, align == Paint.Align.Right ? rect.Right : align == Paint.Align.Center ? rect.CenterX() : rect.Left,
            rect.CenterY() - (paint.Ascent() + paint.Descent()) / 2, paint);
        canvas.RestoreToCount(save); paint.TextAlign = Paint.Align.Left;
    }
    private void ScreenButton(Canvas canvas, string label, RectF rect, float size, float opacity = 1)
    {
        Fill(canvas, rect, Alpha(Color.Argb(205, 4, 3, 10), opacity));
        Stroke(canvas, rect, Alpha(Color.Argb(235, 25, 4, 34), opacity), 4);
        ResultText(canvas, label, rect, size, opacity, Paint.Align.Center);
    }
    private bool TouchNativeResult(MotionEvent e, float x, float y)
    {
        if (e.ActionMasked != MotionEventActions.Up || Environment.TickCount64 - resultStartedAt < 4500) return true;
        if(Hit(x,y,0,0,420,48)){OpenMobileBest30();return true;}
        for (int i = 0; i < 3; i++) if (Hit(x, y, 1250, 694 + i * 91, 1637, 783 + i * 91))
        {
            if (i == 0) StopPlaying();
            else if (i == 1) StartSelected();
            else StartMobileReplay();
        }
        return true;
    }
}
