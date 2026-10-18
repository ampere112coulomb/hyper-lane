using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using MediaPlayer = Android.Media.MediaPlayer;
using IOPath = System.IO.Path;

namespace HyperLane.Android;

[Activity(Label = "Hyper Lane", Icon = "@drawable/hyperlane_icon", MainLauncher = true, Theme = "@android:style/Theme.Material.NoActionBar",
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class MainActivity : Activity
{
    private MobileSongLibrary? library;
    private GameSurface? surface;
    private TextureView? videoView;
    private MobileGpuSurface? gpu;
    private AndroidSongFolders? songFolders;
    private bool syncingFolders;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.SetFlags(WindowManagerFlags.Fullscreen, WindowManagerFlags.Fullscreen);
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        library = new MobileSongLibrary(FilesDir!.AbsolutePath);
        songFolders = new AndroidSongFolders(ContentResolver!,FilesDir.AbsolutePath);
        videoView = new TextureView(this);
        gpu = new MobileGpuSurface(this);
        surface = new GameSurface(this, OpenSongImportMenu, videoView, gpu);
        var layout = new FrameLayout(this);
        layout.SetBackgroundColor(Color.Black);
        layout.AddView(gpu, new FrameLayout.LayoutParams(-1, -1));
        layout.AddView(surface, new FrameLayout.LayoutParams(-1, -1));
        SetContentView(layout);
        surface.SetSongs(library.Scan());
    }

    protected override void OnPause()
    { surface?.Suspend(); gpu?.OnPause(); base.OnPause(); }
    protected override void OnResume()
    { base.OnResume(); gpu?.OnResume(); if(surface?.CanSyncSongFolders==true)RefreshSongFolders(); }

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
        => (keyCode == Keycode.R && e?.RepeatCount > 0) || surface?.HandleMenuKey(keyCode) == true || base.OnKeyDown(keyCode, e);

#pragma warning disable CS0672, CS0618
    public override void OnBackPressed() => surface?.NavigateBack();
#pragma warning restore CS0672, CS0618

    private void OpenZipPicker()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        // Some document providers identify ZIP as octet-stream. Let the user
        // select it regardless of MIME; ImportZip validates the archive.
        intent.SetType("*/*");
        StartActivityForResult(intent, 864);
    }

    private void OpenSongImportMenu()
    {
        new AlertDialog.Builder(this)!.SetTitle("曲を追加・更新")!.SetItems(new[]{"曲フォルダを登録","登録済みフォルダを更新","ZIPを追加"},(_,e)=>{
            if(e.Which==0){var intent=new Intent(Intent.ActionOpenDocumentTree);intent.AddFlags(ActivityFlags.GrantReadUriPermission|ActivityFlags.GrantPersistableUriPermission);StartActivityForResult(intent,865);}
            else if(e.Which==1)RefreshSongFolders(manual:true);
            else OpenZipPicker();
        })!.SetNegativeButton("キャンセル",(_,_)=>{})!.Show();
    }
    private async void RefreshSongFolders(string? added=null,bool manual=false)
    {
        if(syncingFolders||songFolders==null||surface==null||library==null)return;
        var folders=songFolders;
        var sources=folders.Sources();if(added!=null&&!sources.Contains(added))sources.Add(added);
        if(sources.Count==0){if(manual)new AlertDialog.Builder(this)!.SetMessage("先に曲フォルダを登録してください。")!.SetPositiveButton("OK",(_,_)=>{})!.Show();return;}
        syncingFolders=true;
        using var progress=new AlertDialog.Builder(this)!.SetTitle("曲フォルダを更新中")!.SetMessage("譜面・音源・画像を読み込んでいます。元のファイルは変更しません。")!.SetCancelable(false)!.Create()!;progress.Show();
        var errors=new List<string>();int charts=0;
        try
        {
            await Task.Run(()=>{foreach(string source in sources){try{charts+=folders.Sync(source);if(source==added)folders.Register(global::Android.Net.Uri.Parse(source)!);}catch(Exception error){errors.Add(error.Message);}}});
            if(IsFinishing||IsDestroyed)return;
            surface.SetSongs(library.Scan());
            if(errors.Count>0)new AlertDialog.Builder(this)!.SetTitle("一部のフォルダを更新できませんでした")!.SetMessage(string.Join("\n",errors)+"\nフォルダを移動した場合は登録し直してください。")!.SetPositiveButton("OK",(_,_)=>{})!.Show();
            else if(added!=null||manual)new AlertDialog.Builder(this)!.SetTitle("更新完了")!.SetMessage($"{charts}譜面を読み込みました。エディタで保存後、ゲームへ戻ると自動で更新されます。")!.SetPositiveButton("OK",(_,_)=>{})!.Show();
        }
        catch(Exception error){if(!IsFinishing&&!IsDestroyed)new AlertDialog.Builder(this)!.SetMessage(error.Message)!.SetPositiveButton("OK",(_,_)=>{})!.Show();}
        finally{progress.Dismiss();syncingFolders=false;}
    }

#pragma warning disable CS0672 // Kept for Android's document-picker result callback.
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if(requestCode==865&&resultCode==Result.Ok&&data?.Data!=null)
        {
            try{ContentResolver!.TakePersistableUriPermission(data.Data,ActivityFlags.GrantReadUriPermission);RefreshSongFolders(data.Data.ToString());}
            catch(Exception error){new AlertDialog.Builder(this)!.SetMessage("フォルダの読み取り許可を保存できません。\n"+error.Message)!.SetPositiveButton("OK",(_,_)=>{})!.Show();}
            return;
        }
        if (requestCode != 864 || resultCode != Result.Ok || data?.Data is null || library is null || surface is null) return;
        try
        {
            using System.IO.Stream input = ContentResolver!.OpenInputStream(data.Data)!;
            string name = data.Data.LastPathSegment ?? "song.zip";
            library.ImportZip(input, name);
            surface.SetSongs(library.Scan());
            new AlertDialog.Builder(this)!.SetTitle("曲ZIPの取り込み")!.SetMessage("取り込みが完了しました。選曲画面から曲を選んでください。")!.SetPositiveButton("OK", (_, _) => { })!.Show();
        }
        catch (Exception error) { new AlertDialog.Builder(this)!.SetTitle("取り込みに失敗しました")!.SetMessage("譜面と音源が入ったZIPを選択してください。\n" + error.Message)!.SetPositiveButton("OK", (_, _) => { })!.Show(); }
    }
#pragma warning restore CS0672
}

internal sealed partial class GameSurface : View
{
    private new int Width => 1920;
    private new int Height => 1080;
    private float stageScale = 1;
    private float stageLeft;
    private float stageTop;
    private enum Page { Title, Modes, Select, Practice, Settings, Loading, Playing, Paused, Result, Best30, DanResult }
    private readonly Action importZip;
    private MobileSettings settings;
    private readonly TextureView videoView;
    private readonly MobileGpuSurface gpu;
    private MobileGpuFrame? gpuFrame;
    private readonly Paint paint = new(PaintFlags.AntiAlias);
    private readonly Paint zoneBorder = new(PaintFlags.AntiAlias);
    private readonly Paint zonePressed = new(PaintFlags.AntiAlias);
    private readonly MobileSkin? skin;
    private readonly Bitmap? hudPanel;
    private readonly Typeface? lanobe;
    private readonly Typeface? checkpoint;
    private readonly Dictionary<int, int> fingers = new();
    private readonly HashSet<int> held = new();
    private IReadOnlyList<MobileSong> songs = Array.Empty<MobileSong>();
    private int songIndex;
    private int difficultyIndex;
    private Page page;
    private MobileSession? session;
    private UnifiedAudioPlayer? player;
    private MediaPlayer? videoPlayer;
    private Surface? videoSurface;
    private Bitmap? background;
    private Bitmap? jacket;
    private string message = "曲フォルダまたはZIPを追加してください";
    private double[] soundTimes = [];
    private readonly System.Diagnostics.Stopwatch playbackClock = new();
    private double playbackBase;
    private bool audioStarted;
    private double PlaybackSeconds
    {
        get
        {
            if (!audioStarted || player is null) return playbackBase + playbackClock.Elapsed.TotalSeconds;
            return player.PositionSeconds;
        }
    }
    private double ChartPlaybackSeconds => MobilePlaybackTiming.ChartSeconds(PlaybackSeconds, settings.AudioOffsetMs);

    public GameSurface(Context context, Action importZip, TextureView videoView, MobileGpuSurface gpu) : base(context)
    {
        this.importZip = importZip;
        this.videoView = videoView;
        this.gpu = gpu;
        settings = MobileSettings.Load(context);
        menuAudio = new MobileMenuAudio(context);
        rating = new HyperLane.PlayerRating(System.IO.Path.Combine(context.FilesDir!.AbsolutePath, "player-rating.json"));
        profile=HyperLane.UserProfile.Load(ProfilePath);
        skin = MobileSkin.Load(context);
        using (var hudStream = context.Assets!.Open("HUDPanel.png")) hudPanel = BitmapFactory.DecodeStream(hudStream);
        LoadFrontAssets();
        try { lanobe = Typeface.CreateFromAsset(context.Assets, "fonts/LanobePOP.otf"); } catch { }
        try { checkpoint = Typeface.CreateFromAsset(context.Assets, "fonts/Checkpoint.otf"); } catch { }
        zoneBorder.Color = Color.Argb(70, 80, 220, 226);
        zoneBorder.SetStyle(Paint.Style.Stroke);
        zonePressed.Color = Color.Argb(45, 80, 220, 226);
        SystemUiFlags = global::Android.Views.SystemUiFlags.Fullscreen | global::Android.Views.SystemUiFlags.HideNavigation |
            global::Android.Views.SystemUiFlags.ImmersiveSticky | global::Android.Views.SystemUiFlags.LayoutFullscreen | global::Android.Views.SystemUiFlags.LayoutHideNavigation;
    }

    public void SetSongs(IReadOnlyList<MobileSong> value)
    {
        string? selectedPath=SelectedSong?.ChartPath,selectedName=SelectedDifficulty?.Name;
        allChapterSongs=value;RefreshChapters();songs=chapterMenu.Songs(value,practiceMode).ToArray();
        songIndex = Math.Max(0,songs.ToList().FindIndex(s=>s.ChartPath==selectedPath));
        difficultyIndex = songs.Count>0?Math.Max(0,songs[songIndex].Chart.Difficulties.FindIndex(d=>d.Name==selectedName)):0;
        selectionPosition=selectionTarget=songIndex;selectionLoaded=null;
        foreach(var bitmap in bestJackets.Values)bitmap?.Dispose();bestJackets.Clear();
        foreach(var bitmap in judgementImages.Values)bitmap?.Dispose();judgementImages.Clear();
        foreach(var bitmap in danJackets.Values)bitmap?.Dispose();danJackets.Clear();
        RefreshRating();
        LoadMobileDanCourses();
        if(!HyperLane.UserProfile.ValidName(profile.UserName))Post(ShowMobileNameInput);
        message = value.Count == 0 ? "譜面が見つかりません" : $"{value.Count} chart(s) imported";
        Invalidate();
    }
    internal bool CanSyncSongFolders=>page==Page.Select;

    public void ShowMessage(string value) { message = value; Invalidate(); }

    private MobileSong? SelectedSong => danPlaying&&activeDan!=null?activeDan.Tracks[danTrackIndex].Song:songs.Count == 0 ? null : songs[Math.Clamp(songIndex, 0, songs.Count - 1)];
    private MobileDifficulty? SelectedDifficulty
    {
        get
        {
            if(danPlaying&&activeDan!=null)return activeDan.Tracks[danTrackIndex].Difficulty;
            MobileSong? song = SelectedSong;
            return song is null || song.Chart.Difficulties.Count == 0 ? null :
                song.Chart.Difficulties[Math.Clamp(difficultyIndex, 0, song.Chart.Difficulties.Count - 1)];
        }
    }

    private bool preparingMobilePlay;
    private async void StartSelected()
    {
        if (preparingMobilePlay) return;
        MobileSong? song = SelectedSong;
        MobileDifficulty? diff = SelectedDifficulty;
        if (song is null || diff is null) return;
        if (!TouchZones.Supports(diff.Lanes)) { ShowMessage("This build supports 3K / 4K / 6K charts"); return; }
        string audioPath = string.IsNullOrWhiteSpace(diff.Audio)?FindMedia(song.Folder, song.Chart.Audio, ".ogg", ".wav", ".mp3", ".m4a", ".wma"):song.Chart.DifficultyAudioPath(song.Folder,diff);
        if (audioPath.Length == 0||!File.Exists(audioPath))
        { ShowMessage("Audio file missing: " + song.Chart.AudioFor(diff)); return; }
        preparingMobilePlay = true;
        MenuCue(3);
        try
        {
            bool useLoading = page == Page.Select || danPlaying;
            practicePlaying = practiceMode && pendingMobileReplay is null;
            // Load the selected track's jacket before starting the reveal timer.
            // PCM/music preparation can take longer than the entire loading animation.
            jacket?.Dispose();jacket=null;
            string jacketPath = FindMedia(song.Folder, song.Chart.Jacket, ".png", ".jpg", ".jpeg", ".webp");
            if(jacketPath.Length>0)jacket=DecodeMobileThumbnail(jacketPath,1024);
            // Never draw an old session against a newly preparing MediaPlayer.
            if (practicePlaying && !useLoading) page = Page.Practice;
            if (useLoading) BeginMobileLoading();
            StopSelectionPreview();
            ResetMobileReplay();
            StopSeScheduler();
            player?.Release();
            player?.Dispose();
            player = new UnifiedAudioPlayer(Context!);
            player.SetBufferFrames(settings.AudioBufferFrames);
            player.SetDataSource(audioPath);
            await Task.Run(() => player.Prepare());
            StopVideo();
            string videoPath = FindMedia(song.Folder, song.Chart.Video, ".mp4");
            if (settings.PreferMusicVideo && videoPath.Length > 0 && gpu.VideoSurface is { } texture)
            {
                videoPlayer = new MediaPlayer();
                videoPlayer.SetDataSource(videoPath);
                videoPlayer.SetSurface(texture);
                videoPlayer.SetVolume(0, 0);
                await Task.Run(() => videoPlayer.Prepare());
            }
            using(var warmBitmap=Bitmap.CreateBitmap(1,1,Bitmap.Config.Argb8888!)!)
            using(var warmCanvas=new Canvas(warmBitmap))foreach(string word in new[]{"PERFECT","GREAT","GOOD","BAD","MISS","AUTO"})DrawMobileJudgement(warmCanvas,word,new RectF(0,0,1,1));
            session = new MobileSession(song, diff) { AutoPlay = settings.AutoPlay };
            if(danPlaying&&activeDan!=null)session.ConfigureDanGauge(activeDan.Definition.CarryGauge&&danResults.Count>0?danResults[^1].Gauge:activeDan.Definition.InitialGauge,DanScoreUnits,danGlobalGauge);
            if (practicePlaying) PracticeTiming.SkipPreviousNotes(diff);
            soundTimes = diff.Notes.Where(n => n.Kind is not ('D' or 'G')).SelectMany(n => n.Kind == 'L'
                ? new[] { n.StartSeconds, n.EndSeconds } : new[] { n.StartSeconds }).OrderBy(t => t).ToArray();
            session.NoteSound += (note, end) =>
            {
                if (MobileSoundPolicy.OnJudgement(settings.AutoPlay, settings.AutoNoteSound)) player?.PlaySe(settings.SeVolume);
                if (settings.AutoPlay)
                    for (int lane = Math.Max(1, note.Lane); lane <= Math.Min(diff.Lanes, (long)note.Lane + note.LaneSpan - 1); lane++)
                        beamRelease[lane] = ChartPlaybackSeconds + .1;
            };
            background?.Dispose();
            string imagePath = FindChartBackground(song);
            background = imagePath.Length > 0 ? BitmapFactory.DecodeFile(imagePath) : null;
            fingers.Clear(); held.Clear();
            page = useLoading ? Page.Loading : Page.Playing;
            player.Completion += (_, _) => FinishPlaying();
            float volume = settings.BgmVolume / 100f;
            player.SetVolume(volume, volume);
            audioStarted = false;
            StartSeScheduler();
            playbackBase = -settings.StartMarginMs / 1000.0;
            if (practicePlaying)
            {
                playbackBase = PracticeStartRawSeconds;
                await SeekPracticeMedia();
                player.SetVolume(0, 0);
            }
            if (useLoading) { playbackClock.Reset(); mobileLoadingReady = true; }
            else playbackClock.Restart();
            Invalidate();
        }
        catch (Exception error) { player?.Release();player=null;StopVideo();page = Page.Select;danPlaying=false; ShowMessage("Could not play: " + error.Message); }
        finally { preparingMobilePlay = false; Invalidate(); }
    }

    private static string FindMedia(string folder, string preferred, params string[] extensions)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            string root = IOPath.GetFullPath(folder) + IOPath.DirectorySeparatorChar;
            string candidate = IOPath.GetFullPath(IOPath.Combine(folder, preferred));
            if (candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate) &&
                extensions.Contains(IOPath.GetExtension(candidate), StringComparer.OrdinalIgnoreCase)) return candidate;
        }
        return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => extensions.Contains(IOPath.GetExtension(path), StringComparer.OrdinalIgnoreCase)) ?? "";
    }
    private static string FindChartBackground(MobileSong song)
    {
        if(string.IsNullOrWhiteSpace(song.Chart.Background))return "";
        try{
            string root=IOPath.GetFullPath(song.Folder)+IOPath.DirectorySeparatorChar;
            string path=IOPath.GetFullPath(IOPath.Combine(song.Folder,song.Chart.Background.Replace('\\','/')));
            return path.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&File.Exists(path)?path:"";
        }catch{return "";}
    }

    private void StopPlaying()
    {
        StopSeScheduler();
        RestoreMobileReplaySettings();mobileReplaying=false;
        playbackClock.Reset();
        player?.Stop(); player?.Release(); player?.Dispose(); player = null;
        StopVideo();
        background?.Dispose(); background = null;
        jacket?.Dispose(); jacket = null;
        page = Page.Select;
        practicePlaying = false;
        danPlaying=false;
        fingers.Clear(); held.Clear();
        Invalidate();
    }

    private void StopVideo()
    {
        videoPlayer?.Release(); videoPlayer?.Dispose(); videoPlayer = null;
        videoSurface?.Dispose(); videoSurface = null;
    }

    protected override void OnSizeChanged(int width, int height, int oldWidth, int oldHeight)
    {
        base.OnSizeChanged(width, height, oldWidth, oldHeight);
        stageScale = Math.Min(width / 1920f, height / 1080f);
        stageLeft = (width - 1920 * stageScale) * .5f;
        stageTop = (height - 1080 * stageScale) * .5f;
        gpu.LayoutParameters = new FrameLayout.LayoutParams(
            (int)Math.Round(1920 * stageScale), (int)Math.Round(1080 * stageScale), GravityFlags.Center);
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        if (page == Page.Playing)
        {
            gpu.Submit((frame, hud) =>
            {
                gpuFrame = frame;
                try { DrawPlaying(hud); } finally { gpuFrame = null; }
            });
            return;
        }
        if (videoPlayer is null) canvas.DrawColor(Color.Rgb(2, 8, 20));
        int save = canvas.Save();
        canvas.Translate(stageLeft, stageTop);
        canvas.Scale(stageScale, stageScale);
        canvas.ClipRect(0, 0, Width, Height);
        AdvanceMenuTransition();
        if (page is Page.Title or Page.Modes or Page.Practice) DrawFrontPage(canvas);
        else if (page == Page.Select) DrawSelect(canvas);
        else if (page == Page.Loading) DrawMobileLoading(canvas);
        else if (page == Page.Settings) DrawSettings(canvas);
        else if (page == Page.Best30) DrawMobileBest30(canvas);
        else if (page == Page.DanResult) DrawMobileDanResult(canvas);
        else if (page == Page.Playing) DrawPlaying(canvas);
        else if (page == Page.Paused) DrawPaused(canvas);
        else DrawResult(canvas);
        DrawMenuTransition(canvas);
        canvas.RestoreToCount(save);
    }

    private void DrawSelect(Canvas canvas) {if(danMode)DrawMobileDanSelect(canvas);else DrawNativeSelect(canvas);}

    private void DrawSettings(Canvas canvas) => DrawNativeSettings(canvas);

    private void DrawPlaying(Canvas canvas) => DrawNativePlaying(canvas);

    private int HitPlayLane(float x, float y)
    {
        if (session is null) return -1;
        if (!settings.LaneTouch) return TouchZones.HitTest(session.Difficulty.Lanes,x,y,1920,1080,settings.TouchHeight);
        double beat=MobileChart.BeatAtSeconds(session.Difficulty,session.Song.Chart.Bpm,ChartPlaybackSeconds-session.Song.Chart.OffsetFor(session.Difficulty));
        return MobileProjection.At(session.Difficulty,beat).HitLane(session.Difficulty.Lanes,x,y);
    }

    private void DrawZones(Canvas canvas, int lanes)
    {
        if (settings.LaneTouch) return;
        zoneBorder.StrokeWidth = Math.Max(1.5f, Height * .002f);
        for (int lane = 0; lane < lanes; lane++)
        {
            TouchRect rect = TouchZones.GetRect(lanes, lane, Width, Height, settings.TouchHeight);
            RectF bounds = new(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (held.Contains(lane + 1)) canvas.DrawRect(bounds, zonePressed);
            canvas.DrawRect(bounds, zoneBorder);
        }
    }

    private void DrawResult(Canvas canvas) => DrawNativeResult(canvas);

    private void Text(Canvas canvas, string text, float x, float y, float size, Color color, bool difficultyNumber = false)
    {
        paint.Color = color;
        paint.TextSize = Math.Max(12, size);
        paint.SetStyle(Paint.Style.Fill);
        paint.SetTypeface(difficultyNumber ? checkpoint : lanobe);
        canvas.DrawText(text, x, y, paint);
    }

    private void Button(Canvas canvas, string label, float x0, float y0, float x1, float y1)
    {
        paint.Color = Color.Argb(190, 0, 30, 47);
        paint.SetStyle(Paint.Style.Fill);
        canvas.DrawRect(Width * x0, Height * y0, Width * x1, Height * y1, paint);
        paint.Color = Color.Rgb(69, 166, 224);
        paint.StrokeWidth = 2;
        paint.SetStyle(Paint.Style.Stroke);
        canvas.DrawRect(Width * x0, Height * y0, Width * x1, Height * y1, paint);
        paint.SetStyle(Paint.Style.Fill);
        Text(canvas, label, Width * (x0 + .02f), Height * (y0 + .105f), Height * .038f, Color.White);
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null) return false;
        int actionIndex = e.ActionIndex;
        float x = (e.GetX(actionIndex) - stageLeft) / stageScale;
        float y = (e.GetY(actionIndex) - stageTop) / stageScale;
        if (menuTransitionStarted != 0 || preparingMobilePlay) return true;
        if(!HyperLane.UserProfile.ValidName(profile.UserName))return true;
        HandleMenuSound(e,x,y);
        if (page is Page.Title or Page.Modes or Page.Practice) return TouchFrontPage(e, x, y);
        if (page == Page.Select) return danMode?TouchMobileDanSelect(e,x,y):TouchSelection(e, x, y);
        if (page == Page.Best30) return TouchMobileBest30(e,x,y);
        if (page == Page.DanResult) return TouchMobileDanResult(e,x,y);
        if (page == Page.Loading) return true;
        if (page == Page.Settings) return TouchNativeSettings(e, x, y);
        if (page == Page.Paused) return TouchPaused(e, x, y);
        if (page == Page.Result) return TouchNativeResult(e, x, y);
        if (session is null || player is null) return true;
        if (practicePlaying && e.ActionMasked == MotionEventActions.Up && x < 200 && y < 115)
        { StartSelected(); return true; }
        if (e.ActionMasked == MotionEventActions.Up && x > Width * .92f && y < Height * .12f)
        { PausePlaying(); return true; }
        if (PracticePreroll) { fingers.Clear(); held.Clear(); return true; }
        if (settings.AutoPlay || mobileReplaying) { fingers.Clear(); if (!mobileReplaying) held.Clear(); return true; }
        int pointerId = e.GetPointerId(actionIndex);
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
            case MotionEventActions.PointerDown:
                fingers[pointerId] = x is >= 0 and <= 1920 && y is >= 0 and <= 1080
                    ? HitPlayLane(x, y) + 1 : 0;
                break;
            case MotionEventActions.Move:
                for (int i = 0; i < e.PointerCount; i++)
                {
                    float px = (e.GetX(i) - stageLeft) / stageScale;
                    float py = (e.GetY(i) - stageTop) / stageScale;
                    fingers[e.GetPointerId(i)] = px is >= 0 and <= 1920 && py is >= 0 and <= 1080
                        ? HitPlayLane(px, py) + 1 : 0;
                }
                break;
            case MotionEventActions.Up:
            case MotionEventActions.PointerUp:
                fingers.Remove(pointerId);
                break;
            case MotionEventActions.Cancel:
                fingers.Clear();
                break;
        }
        HashSet<int> next = fingers.Values.Where(v => v > 0).ToHashSet();
        double seconds = ChartPlaybackSeconds;
        // Motion events may be delivered after a busy render frame. Judge the
        // actual touch time, not the later time when this callback is processed.
        seconds -= Math.Clamp((SystemClock.UptimeMillis() - e.EventTime) / 1000.0, 0, .25);
        foreach (int lane in next.Except(held).ToArray()) { session.Press(lane, seconds); RecordMobileInput(lane, seconds, true); }
        foreach (int lane in held.Except(next).ToArray()) { session.Release(lane, seconds); RecordMobileInput(lane, seconds, false); }
        held.Clear(); held.UnionWith(next);
        Invalidate();
        return true;
    }
}
