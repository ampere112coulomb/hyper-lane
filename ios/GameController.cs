using AVFoundation;
using CoreAnimation;
using CoreGraphics;
using HyperLane.Android;
using Foundation;
using UIKit;
using UniformTypeIdentifiers;

namespace HyperLane.iOS;

// Native iOS audio/input lifecycle, connected to the shared chart/session core
// and the skin-based visual stage. Simulator and device checks remain separate.
internal sealed partial class GameController : UIViewController
{
    private readonly string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private MobileSongLibrary library = null!;
    private IosSettings settings = null!;
    private IReadOnlyList<MobileSong> songs = [];
    private MobileSession? session;
    private AVAudioPlayer? audio;
    private IosNoteSound? noteSound;
    private string? noteSoundError;
    private int nextSound;
    private double[] soundTimes = [];
    private CADisplayLink? displayLink;
    private int displayRate=-1;
    private MigrationStage stage = null!;
    private UIView menu = null!;
    private readonly HashSet<int> held = new();
    private UIDocumentPickerViewController? picker;
    private ImportDelegate? importDelegate;
    private bool running;
    private bool preparingAudio;
    private HyperLane.UserProfile profile = null!;
    private HyperLane.PlayerRating rating = null!;
    private double startClock, rawStart;
    private bool waitingForAudio, practice;
    private bool recordEligible;
    private double practiceAnchor;
    private string SettingsPath => Path.Combine(documents, "settings.json");

    public override bool PrefersStatusBarHidden() => true;
    public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations() => UIInterfaceOrientationMask.Landscape;
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        HyperLane.ReplayJudgementMigration.Apply(documents);
        Console.Error.WriteLine("HL startup: controller");
        Directory.CreateDirectory(documents);
        library = new MobileSongLibrary(documents);
        settings = IosSettings.Load(SettingsPath);
        profile = HyperLane.UserProfile.Load(Path.Combine(documents,"profile.json"));
        rating = new HyperLane.PlayerRating(Path.Combine(documents,"player-rating.json"));
        View!.BackgroundColor = UIColor.Black;
        Console.Error.WriteLine("HL startup: constructing stage");
        try { stage = new MigrationStage { Frame = View.Bounds, AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight }; }
        catch(Exception error){Console.Error.WriteLine(error);View.AddSubview(new UILabel(View.Bounds){Text=error.ToString(),TextColor=UIColor.Red,Lines=0,Font=UIFont.SystemFontOfSize(16)});return;}
        stage.InputChanged = InputChanged;
        Console.Error.WriteLine("HL startup: stage ready");
        stage.PauseRequested = Pause;
        stage.Settings = settings; stage.UserName = profile.UserName; stage.Rating = rating;
        stage.ImportRequested = Import; stage.StartRequested = (s,d) => { RestoreReplay();activeDan=null;practice=false; Start(s,d); };
        stage.BackRequested = EndPlay; stage.ResumeRequested = Resume;stage.RetryRequested=RetryPlay;
        stage.SaveSettings = () => { settings.Save(SettingsPath); ApplyAudioBuffer(); if(audio!=null)audio.Volume=settings.BgmVolume/100f; };
        stage.NameRequested = ShowName;
        stage.SearchRequested=SearchSongs;
        stage.BeatRequested=BeatInput;
        stage.NumberRequested = NumberInput;
        stage.PresetRequested = (slot,save) => {
            string path=Path.Combine(documents,$"settings-preset-{slot}.json");
            if(save)settings.Save(path);
            else if(File.Exists(path)){
                bool auto=settings.AutoPlay;
                settings=IosSettings.Load(path);
                if(stage.SettingsFromPause)settings.AutoPlay=auto;
                stage.Settings=settings;settings.Save(SettingsPath);ApplyAudioBuffer();
                if(audio!=null)audio.Volume=settings.BgmVolume/100f;
            }
        };
        stage.PracticeRequested = (s,d,beat) => { RestoreReplay();activeDan=null;practice=true;practiceAnchor=beat;Start(s,d); };
        stage.DanRequested = OpenDan;stage.ReplayRequested = StartReplay;
        View.AddSubview(stage);
        menu = new UIView { BackgroundColor = UIColor.FromRGB(8, 15, 27) };
        View.AddSubview(menu);
        AVAudioSession.SharedInstance().SetCategory(AVAudioSession.CategoryPlayback, out _);
        ApplyAudioBuffer();
        AVAudioSession.SharedInstance().SetActive(true, out _);
        ApplyAudioBuffer();
        string? se = NSBundle.MainBundle.PathForResource("SE", "wav");
        if (se is not null)
            try { noteSound = new IosNoteSound(se); }
            catch (Exception error) { noteSoundError = error.Message; }
        displayLink = CADisplayLink.Create(this, new ObjCRuntime.Selector("tick:"));
        displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
        ShowSongs();
        stage.Page=MigrationStage.Screen.Title;
        ConfigureVisualQa();
        if(!visualQa&&!HyperLane.UserProfile.ValidName(profile.UserName))BeginInvokeOnMainThread(ShowName);
    }

    public override void ViewDidLayoutSubviews()
    { base.ViewDidLayoutSubviews(); if (menu != null) menu.Frame = View!.Bounds.Inset(35, 25); }

    private void ClearMenu()
    { foreach (UIView view in menu.Subviews) { view.RemoveFromSuperview(); view.Dispose(); } menu.Hidden = false; }
    private void Button(string text, nfloat x, nfloat y, nfloat width, Action action)
    {
        var button = UIButton.FromType(UIButtonType.System);
        button.Frame = new CGRect(x, y, width, 48); button.SetTitle(text, UIControlState.Normal);
        button.SetTitleColor(UIColor.White, UIControlState.Normal);
        button.BackgroundColor = UIColor.FromRGB(17, 46, 69);
        button.TouchUpInside += (_, _) => action(); menu.AddSubview(button);
    }

    private void ShowSongs()
    {
        RestoreReplay();
        activeDan=null;
        running = false; audio?.Stop(); noteSound?.Pause(); held.Clear(); stage.ClearTouches(); ClearMenu();
        songs = library.Scan();
        foreach(var song in songs)MigrationStage.PrepareMedia(song);
        stage.SetSongs(songs);stage.ShowSelect();menu.Hidden=true;
        return;
#pragma warning disable CS0162
        var title = new UILabel(new CGRect(20, 15, 800, 45)) { Text = "Hyper Lane — iOS移植確認用", TextColor = UIColor.White, Font = UIFont.SystemFontOfSize(26) };
        menu.AddSubview(title);
        Button("楽曲ZIPを取り込む", 20, 70, 220, Import);
        Button("設定", 255, 70, 100, ShowSettings);
        var scroll = new UIScrollView(new CGRect(20, 135, 850, 420)) { AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight };
        menu.AddSubview(scroll);
        for (int i = 0; i < songs.Count; i++)
        {
            MobileSong song = songs[i]; var button = UIButton.FromType(UIButtonType.System);
            button.Frame = new CGRect(0, i * 58, 800, 52); button.SetTitle(song.Chart.Title + " / " + song.Chart.Composer, UIControlState.Normal);
            button.SetTitleColor(UIColor.White, UIControlState.Normal);
            button.TouchUpInside += (_, _) => ChooseDifficulty(song); scroll.AddSubview(button);
        }
        scroll.ContentSize = new CGSize(800, songs.Count * 58);
#pragma warning restore CS0162
    }

    private void ChooseDifficulty(MobileSong song)
    {
        var dialog = UIAlertController.Create(song.Chart.Title, "難易度を選択", UIAlertControllerStyle.Alert);
        foreach (MobileDifficulty difficulty in song.Chart.Difficulties)
            dialog.AddAction(UIAlertAction.Create($"{difficulty.Name} Lv.{difficulty.Level} / {difficulty.Lanes}K", UIAlertActionStyle.Default, _ => Start(song, difficulty)));
        dialog.AddAction(UIAlertAction.Create("キャンセル", UIAlertActionStyle.Cancel, null));
        PresentViewController(dialog, true, null);
    }

    private void Import()
    {
        picker = new UIDocumentPickerViewController([UTTypes.Zip], true);
        importDelegate = new ImportDelegate(this); picker.Delegate = importDelegate;
        PresentViewController(picker, true, null);
    }
    private sealed class ImportDelegate(GameController owner) : UIDocumentPickerDelegate
    {
        public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
        {
            if (urls.Length == 0) return;
            bool scoped = urls[0].StartAccessingSecurityScopedResource();
            try { bool dan=owner.stage.Page==MigrationStage.Screen.Dan;using var stream = File.OpenRead(urls[0].Path!); owner.library.ImportZip(stream, Path.GetFileName(urls[0].Path)); owner.ShowSongs();if(dan)owner.OpenDan(); }
            catch (Exception error) { owner.Alert("取り込み失敗", error.Message); }
            finally { if (scoped) urls[0].StopAccessingSecurityScopedResource(); }
        }
    }

    private async void Start(MobileSong song, MobileDifficulty difficulty)
    {
        if (preparingAudio) return;
        if (!TouchZones.Supports(difficulty.Lanes)) { Alert("未対応", "このレーン数はタッチ範囲に未対応です。"); return; }
        string path = Path.GetFullPath(Path.Combine(song.Folder, song.Chart.AudioFor(difficulty)));
        if (!path.StartsWith(Path.GetFullPath(song.Folder) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        { Alert("音源エラー", "楽曲フォルダー外の音源は読み込めません。"); return; }
        audio?.Stop(); audio?.Dispose(); audio = null;
        running=false;menu.Hidden=true;stage.PracticePlaying=practice;stage.BeginLoading(song,difficulty, BeginPlayback);
        AVAudioPlayer? created = null;
        preparingAudio = true;
        menu.UserInteractionEnabled = false;
        var preparingLabel = new UILabel(new CGRect(20, 120, 850, 50))
        { Text = "音源を準備中…（初回はM4Aへ変換します）", TextColor = UIColor.White };
        menu.AddSubview(preparingLabel);
        try
        {
            if (Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
                path = await IosAudioConverter.ToM4aAsync(path, Path.Combine(documents, "audio-cache"));
            created = AVAudioPlayer.FromUrl(NSUrl.FromFilename(path), out _);
            if (created is null)
            {
                path = await IosAudioConverter.ToM4aAsync(path, Path.Combine(documents, "audio-cache"));
                created = AVAudioPlayer.FromUrl(NSUrl.FromFilename(path), out var error);
                if (created is null) throw new InvalidDataException(error?.LocalizedDescription ?? "変換後の音源を読み込めません。");
            }
        }
        catch (Exception error)
        {
            created?.Dispose();
            stage.CancelLoading();
            Alert("音源エラー", $"{Path.GetFileName(song.Chart.AudioFor(difficulty))}\n{error.Message}");
            return;
        }
        finally
        {
            preparingAudio = false; menu.UserInteractionEnabled = true;
            preparingLabel.RemoveFromSuperview(); preparingLabel.Dispose();
        }
        audio = created; audio.Volume = settings.BgmVolume / 100f; audio.PrepareToPlay();
        try { noteSound?.Begin(settings.SeVolume); }
        catch (Exception error) { noteSoundError = error.Message; }
        if (noteSoundError is not null)
        { stage.CancelLoading(); Alert("SEエラー", noteSoundError); noteSoundError = null; return; }
        audio.FinishedPlaying += (_, _) => BeginInvokeOnMainThread(Finish);
        session = new MobileSession(song, difficulty) { AutoPlay = settings.AutoPlay };
        ConfigureDanSession();
        danFailureAudio?.Dispose();danFailureAudio=null;if(activeDan?.Definition.Life.Enabled==true&&audio.Url?.Path is {} failurePath)try{danFailureAudio=new(failurePath);}catch(Exception ex){Console.Error.WriteLine("Dan effect prepare: "+ex.Message);}
        if(practice)MobilePracticeTiming.At(song.Chart,difficulty,practiceAnchor,settings.AudioOffsetMs).SkipPreviousNotes(difficulty);
        session.NoteSound += (_, _) => { if (MobileSoundPolicy.OnJudgement(settings.AutoPlay, settings.AutoNoteSound)) PlaySe(); };
        soundTimes = difficulty.Notes.Where(n => !n.Done && n.Kind is not ('D' or 'G')).SelectMany(n => n.Kind == 'L'
            ? new[] { n.StartSeconds, n.EndSeconds } : new[] { n.StartSeconds }).Distinct().Order().ToArray();
        nextSound = 0; held.Clear(); stage.ClearTouches(); menu.Hidden = true;
        stage.Session = session; stage.Settings = settings;stage.Seconds=0;stage.LoadingReady();
    }

    [Export("tick:")]
    private void Tick(CADisplayLink link)
    {
        int rate=stage.Page==MigrationStage.Screen.Playing?0:60;
        if(displayRate!=rate){displayRate=rate;link.PreferredFramesPerSecond=rate;}
        UpdatePreview();
        if (!running || audio is null || session is null){stage.Refresh(CAAnimation.CurrentMediaTime());return;}
        if(UpdateDanFailure())return;
        double raw=waitingForAudio?rawStart+CAAnimation.CurrentMediaTime()-startClock:audio.CurrentTime;
        if(waitingForAudio&&raw>=Math.Max(0,audio.CurrentTime)){waitingForAudio=false;audio.Play();stage.ResumeVideo();raw=audio.CurrentTime;}
        double seconds = MobilePlaybackTiming.ChartSeconds(raw, settings.AudioOffsetMs);
        while (nextSound < soundTimes.Length && soundTimes[nextSound] <= seconds)
        { nextSound++; if (MobileSoundPolicy.Scheduled(settings.AutoPlay, settings.AutoNoteSound)) PlaySe(); }
        session.AutoPlay=settings.AutoPlay;
        session.StageMap.Enabled=settings.StageDecorations;session.StageMap.Speed=settings.NoteSpeed;
        recordEligible&=!settings.AutoPlay;if(activeDan!=null)danEligible&=!settings.AutoPlay;
        AdvanceReplay(seconds);
        if(!practice||!MobilePracticeTiming.At(session.Song.Chart,session.Difficulty,practiceAnchor,settings.AudioOffsetMs).IsPreroll(seconds))session.Advance(seconds, held);
        if(practice)audio.Volume=(float)(settings.BgmVolume/100.0*MobilePracticeTiming.At(session.Song.Chart,session.Difficulty,practiceAnchor,settings.AudioOffsetMs).Gain(seconds));
        stage.Seconds = seconds;
        stage.Refresh(CAAnimation.CurrentMediaTime());
        if(seconds>Math.Max(audio.Duration,session.Difficulty.Notes.Select(n=>n.EndSeconds).DefaultIfEmpty(0).Max())+.2)Finish();
    }

    private void Finish()
    {
        if(session?.Life?.Failed==true)return;
        if (session is null || audio is null || !running) return;
        session.Advance(Math.Max(audio.Duration, session.Difficulty.Notes.Select(n => n.EndSeconds).DefaultIfEmpty(0).Max()) + .2, held);
        running = false;
        noteSound?.Pause();stage.PauseVideo();
        if(practice){stage.Page=MigrationStage.Screen.Practice;return;}
        if(activeDan!=null){FinishDan();return;}
        if(recordEligible&&!replaying)rating.Update(MigrationStage.Key(session.Song,session.Difficulty),MigrationStage.Hash(session.Song),MigrationStage.Level(session.Difficulty),session.Score,session.Counts,session.MaxCombo);
        SaveReplay();stage.AwardBadge(recordEligible&&!replaying);stage.ShowResult();
    }

    private void InputChanged(IReadOnlySet<int> next, double timestamp)
    {
        if (!running || audio is null || session is null || session.Life?.Failed==true || settings.AutoPlay || replaying) return;
        double raw=waitingForAudio?rawStart+CAAnimation.CurrentMediaTime()-startClock:audio.CurrentTime;
        double seconds = MobilePlaybackTiming.ChartSeconds(raw, settings.AudioOffsetMs)
            - Math.Clamp(CAAnimation.CurrentMediaTime() - timestamp, 0, .25);
        foreach (int lane in next.Except(held).ToArray()) session.Press(lane, seconds);
        foreach (int lane in held.Except(next).ToArray()) session.Release(lane, seconds);
        RecordInput(next,seconds);
        held.Clear(); held.UnionWith(next);
    }
    private void PlaySe()
    {
        if (settings.SeVolume > 0) noteSound?.Play();
    }
    internal void Pause()
    { if (running) { if(waitingForAudio)rawStart+=CAAnimation.CurrentMediaTime()-startClock;audio?.Pause(); noteSound?.Pause(); running = false; held.Clear(); stage.ClearTouches();stage.PauseVideo();stage.Page=MigrationStage.Screen.Paused; } }
    private void Resume(){try{noteSound?.Begin(settings.SeVolume);}catch(Exception e){Alert("SEエラー",e.Message);return;}stage.Page=MigrationStage.Screen.Playing;running=true;startClock=CAAnimation.CurrentMediaTime();if(!waitingForAudio){audio?.Play();stage.ResumeVideo();}}
    private void RetryPlay()
    {
        if(session==null||preparingAudio)return;
        running=false;audio?.Stop();noteSound?.Pause();held.Clear();stage.ClearTouches();
        if(activeDan is {} course){BeginDan(course);return;}
        if(replaying){Start(session.Song,session.Difficulty);return;}
        Start(session.Song,session.Difficulty);
    }
    private void EndPlay()
    {
        running=false;audio?.Stop();noteSound?.Pause();held.Clear();stage.ClearTouches();stage.PauseVideo();
        if(practice){stage.Page=MigrationStage.Screen.Practice;return;}
        if(activeDan!=null){activeDan=null;stage.ActiveDan=null;OpenDan();return;}
        RestoreReplay();ShowSongs();
    }
    private void BeginPlayback()
    {
        if(audio==null||session==null)return;
        recordEligible=!settings.AutoPlay&&!replaying&&!practice;
        rawStart=-settings.StartMarginMs/1000.0;
        if(practice){var timing=MobilePracticeTiming.At(session.Song.Chart,session.Difficulty,practiceAnchor,settings.AudioOffsetMs);rawStart=timing.StartRaw;audio.CurrentTime=Math.Max(0,rawStart);}
        startClock=CAAnimation.CurrentMediaTime();waitingForAudio=rawStart<0;running=true;
        if(!waitingForAudio){stage.Seconds=MobilePlaybackTiming.ChartSeconds(audio.CurrentTime,settings.AudioOffsetMs);audio.Play();stage.ResumeVideo();}else stage.PauseVideo();
        nextSound=0;while(nextSound<soundTimes.Length&&soundTimes[nextSound]<rawStart)nextSound++;
        BeginReplayRecording();
    }
    private void BeatInput(double value,Action<double> save)
    {
        var dialog=UIAlertController.Create("練習の開始拍",null,UIAlertControllerStyle.Alert);dialog.OverrideUserInterfaceStyle=UIUserInterfaceStyle.Dark;
        dialog.AddTextField(t=>{t.Text=value.ToString("0.####",System.Globalization.CultureInfo.InvariantCulture);t.KeyboardType=UIKeyboardType.DecimalPad;});
        dialog.AddAction(UIAlertAction.Create("決定",UIAlertActionStyle.Default,_=>{if(double.TryParse(dialog.TextFields![0].Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double beat)&&double.IsFinite(beat))save(Math.Max(0,beat));}));
        dialog.AddAction(UIAlertAction.Create("キャンセル",UIAlertActionStyle.Cancel,null));PresentViewController(dialog,true,null);
    }
    private void SearchSongs()
    {
        var dialog=UIAlertController.Create("楽曲検索",null,UIAlertControllerStyle.Alert);dialog.OverrideUserInterfaceStyle=UIUserInterfaceStyle.Dark;
        dialog.AddTextField(t=>{t.Text=stage.SongSearch;t.AutocorrectionType=UITextAutocorrectionType.No;});
        dialog.AddAction(UIAlertAction.Create("検索",UIAlertActionStyle.Default,_=>stage.ApplySearch(dialog.TextFields![0].Text??"")));
        dialog.AddAction(UIAlertAction.Create("キャンセル",UIAlertActionStyle.Cancel,null));PresentViewController(dialog,true,null);
    }
    private void ShowName()
    {
        ClearMenu();menu.BackgroundColor=UIColor.Clear;menu.Frame=View!.Bounds;
        nfloat w=menu.Bounds.Width*.7f,h=280;var popup=new UIView(new CGRect((menu.Bounds.Width-w)/2,(menu.Bounds.Height-h)/2,w,h)){BackgroundColor=UIColor.FromRGBA(5,3,12,235)};menu.AddSubview(popup);
        popup.AddSubview(new UILabel(new CGRect(20,20,w-40,50)){Text="ユーザーネームを設定してください。",TextColor=UIColor.White,Font=UIFont.SystemFontOfSize(24),TextAlignment=UITextAlignment.Center,AdjustsFontSizeToFitWidth=true});
        var hint=new UILabel(new CGRect(20,75,w-40,30)){Text="1～10文字",TextColor=UIColor.White,TextAlignment=UITextAlignment.Center};popup.AddSubview(hint);
        var input=new UITextField(new CGRect(30,118,w-60,65)){Text=profile.UserName,TextColor=UIColor.White,BackgroundColor=UIColor.FromRGBA(35,31,44,220),Font=UIFont.SystemFontOfSize(28),AutocorrectionType=UITextAutocorrectionType.No};input.Layer.BorderWidth=2;input.Layer.BorderColor=UIColor.Yellow.CGColor;popup.AddSubview(input);
        void FinishName(string name)
        {
            if(!HyperLane.UserProfile.ValidName(name)){hint.Text="1～10文字で入力してください。";hint.TextColor=UIColor.Red;return;}
            profile.UserName=name;profile.Save(Path.Combine(documents,"profile.json"));stage.UserName=name;
            input.ResignFirstResponder();View!.EndEditing(true);
            ClearMenu();menu.Hidden=true;stage.Page=MigrationStage.Screen.Title;stage.SetNeedsDisplay();
        }
        var confirm=UIButton.FromType(UIButtonType.System);confirm.Frame=new CGRect(30,204,(w-75)/2,50);confirm.SetTitle("決定",UIControlState.Normal);confirm.SetTitleColor(UIColor.White,UIControlState.Normal);confirm.BackgroundColor=UIColor.FromRGB(46,24,65);popup.AddSubview(confirm);
        var skip=UIButton.FromType(UIButtonType.System);skip.Frame=new CGRect(45+(w-75)/2,204,(w-75)/2,50);skip.SetTitle("スキップ",UIControlState.Normal);skip.SetTitleColor(UIColor.White,UIControlState.Normal);skip.BackgroundColor=UIColor.FromRGB(35,31,44);popup.AddSubview(skip);
        confirm.TouchUpInside+=(_,_)=>FinishName((input.Text??"").Trim());
        skip.TouchUpInside+=(_,_)=>FinishName("GUEST");
        input.ReturnKeyType=UIReturnKeyType.Done;
        input.ShouldReturn=_=>{FinishName((input.Text??"").Trim());return true;};
        // Keep actions reachable even when the landscape keyboard covers the popup.
        var keyboardActions=new UIView(new CGRect(0,0,View!.Bounds.Width,48)){BackgroundColor=UIColor.FromRGB(20,16,30)};
        var keyboardSkip=UIButton.FromType(UIButtonType.System);keyboardSkip.Frame=new CGRect(12,0,140,48);keyboardSkip.SetTitle("スキップ",UIControlState.Normal);keyboardSkip.SetTitleColor(UIColor.White,UIControlState.Normal);keyboardSkip.TouchUpInside+=(_,_)=>FinishName("GUEST");keyboardActions.AddSubview(keyboardSkip);
        var keyboardDone=UIButton.FromType(UIButtonType.System);keyboardDone.Frame=new CGRect(View.Bounds.Width-152,0,140,48);keyboardDone.SetTitle("決定",UIControlState.Normal);keyboardDone.SetTitleColor(UIColor.White,UIControlState.Normal);keyboardDone.TouchUpInside+=(_,_)=>FinishName((input.Text??"").Trim());keyboardActions.AddSubview(keyboardDone);
        input.InputAccessoryView=keyboardActions;
        // Open the keyboard only after tapping the field, so skipping needs no typing.
    }
    private void ShowPause()
    {
        ClearMenu();
        Button("再開", 20, 40, 200, () =>
        {
            try { noteSound?.Begin(settings.SeVolume); }
            catch (Exception error) { Alert("SEエラー", error.Message); return; }
            menu.Hidden = true; running = true; audio?.Play();
        });
        Button("曲選択", 20, 105, 200, ShowSongs);
    }
    private void ShowSettings()
    {
        var dialog = UIAlertController.Create("移植確認用設定", "ノーツ速度 / 譜面補正(ms) / SE音量(0～100)", UIAlertControllerStyle.Alert);
        dialog.AddTextField(t => t.Text = settings.NoteSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        dialog.AddTextField(t => t.Text = settings.AudioOffsetMs.ToString());
        dialog.AddTextField(t => t.Text = settings.SeVolume.ToString());
        dialog.AddAction(UIAlertAction.Create("保存", UIAlertActionStyle.Default, _ =>
        {
            if (double.TryParse(dialog.TextFields![0].Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double speed)) settings.NoteSpeed = Math.Clamp(speed, .5, 10);
            if (int.TryParse(dialog.TextFields[1].Text, out int offset)) settings.AudioOffsetMs = Math.Clamp(offset, -1000, 1000);
            if (int.TryParse(dialog.TextFields[2].Text, out int se)) settings.SeVolume = Math.Clamp(se, 0, 100);
            settings.Save(SettingsPath);
        }));
        dialog.AddAction(UIAlertAction.Create(settings.AutoPlay ? "オートをOFF" : "オートをON", UIAlertActionStyle.Default, _ => { settings.AutoPlay = !settings.AutoPlay; settings.Save(SettingsPath); }));
        dialog.AddAction(UIAlertAction.Create("キャンセル", UIAlertActionStyle.Cancel, null));
        PresentViewController(dialog, true, null);
    }
    private void Alert(string title, string text)
    { var dialog = UIAlertController.Create(title, text, UIAlertControllerStyle.Alert); dialog.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null)); PresentViewController(dialog, true, null); }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { previewRequest++;previewAudio?.Dispose();displayLink?.Invalidate(); displayLink?.Dispose(); audio?.Dispose(); noteSound?.Dispose(); }
        base.Dispose(disposing);
    }
}
