using AVFoundation;
using CoreAnimation;
using CoreGraphics;
using HyperLane.Android;
using Foundation;
using UIKit;
using UniformTypeIdentifiers;

namespace HyperLane.iOS;

// Migration harness: native menus and a projection-check renderer are deliberately
// separate from the final PC-skin/Metal renderer, which requires a Mac to verify.
internal sealed class GameController : UIViewController
{
    private readonly string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private MobileSongLibrary library = null!;
    private IosSettings settings = null!;
    private IReadOnlyList<MobileSong> songs = [];
    private MobileSession? session;
    private AVAudioPlayer? audio;
    private readonly List<AVAudioPlayer> seVoices = [];
    private int seVoice, nextSound;
    private double[] soundTimes = [];
    private CADisplayLink? displayLink;
    private MigrationStage stage = null!;
    private UIView menu = null!;
    private readonly HashSet<int> held = new();
    private UIDocumentPickerViewController? picker;
    private ImportDelegate? importDelegate;
    private bool running;
    private string SettingsPath => Path.Combine(documents, "settings.json");

    public override bool PrefersStatusBarHidden() => true;
    public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations() => UIInterfaceOrientationMask.Landscape;
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Directory.CreateDirectory(documents);
        library = new MobileSongLibrary(documents);
        settings = IosSettings.Load(SettingsPath);
        View!.BackgroundColor = UIColor.Black;
        stage = new MigrationStage { Frame = View.Bounds, AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight };
        stage.InputChanged = InputChanged;
        stage.PauseRequested = Pause;
        View.AddSubview(stage);
        menu = new UIView { BackgroundColor = UIColor.FromRGB(8, 15, 27) };
        View.AddSubview(menu);
        AVAudioSession.SharedInstance().SetCategory(AVAudioSession.CategoryPlayback, out _);
        AVAudioSession.SharedInstance().SetActive(true, out _);
        string? se = NSBundle.MainBundle.PathForResource("SE", "wav");
        if (se is not null) for (int i = 0; i < 16; i++)
        {
            var voice = AVAudioPlayer.FromUrl(NSUrl.FromFilename(se), out _);
            if (voice is not null) { voice.PrepareToPlay(); seVoices.Add(voice); }
        }
        displayLink = CADisplayLink.Create(this, new ObjCRuntime.Selector("tick:"));
        displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
        ShowSongs();
    }

    public override void ViewDidLayoutSubviews()
    { base.ViewDidLayoutSubviews(); menu.Frame = View!.Bounds.Inset(35, 25); }

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
        running = false; audio?.Stop(); held.Clear(); stage.ClearTouches(); ClearMenu();
        songs = library.Scan();
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
            try { using var stream = File.OpenRead(urls[0].Path!); owner.library.ImportZip(stream, Path.GetFileName(urls[0].Path)); owner.ShowSongs(); }
            catch (Exception error) { owner.Alert("取り込み失敗", error.Message); }
            finally { if (scoped) urls[0].StopAccessingSecurityScopedResource(); }
        }
    }

    private void Start(MobileSong song, MobileDifficulty difficulty)
    {
        if (!TouchZones.Supports(difficulty.Lanes)) { Alert("未対応", "このレーン数はタッチ範囲に未対応です。"); return; }
        string path = Path.GetFullPath(Path.Combine(song.Folder, song.Chart.Audio));
        if (!path.StartsWith(Path.GetFullPath(song.Folder) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        { Alert("音源エラー", "楽曲フォルダー外の音源は読み込めません。"); return; }
        audio?.Stop(); audio?.Dispose(); audio = null;
        var created = AVAudioPlayer.FromUrl(NSUrl.FromFilename(path), out var error);
        if (created is null) { Alert("音源エラー", error?.LocalizedDescription ?? "iOSで読み込めない音源形式です。OGG等のデコーダーは未移植です。"); return; }
        audio = created; audio.Volume = settings.BgmVolume / 100f; audio.PrepareToPlay();
        audio.FinishedPlaying += (_, _) => BeginInvokeOnMainThread(Finish);
        session = new MobileSession(song, difficulty) { AutoPlay = settings.AutoPlay };
        session.NoteSound += (_, _) => { if (MobileSoundPolicy.OnJudgement(settings.AutoPlay, settings.AutoNoteSound)) PlaySe(); };
        soundTimes = difficulty.Notes.Where(n => n.Kind is not ('D' or 'G')).SelectMany(n => n.Kind == 'L'
            ? new[] { n.StartSeconds, n.EndSeconds } : new[] { n.StartSeconds }).Order().ToArray();
        nextSound = 0; held.Clear(); stage.ClearTouches(); menu.Hidden = true;
        stage.Session = session; stage.Settings = settings; running = true; audio.Play();
    }

    [Export("tick:")]
    private void Tick(CADisplayLink link)
    {
        if (!running || audio is null || session is null) return;
        double seconds = MobilePlaybackTiming.ChartSeconds(audio.CurrentTime, settings.AudioOffsetMs);
        while (nextSound < soundTimes.Length && soundTimes[nextSound] <= seconds)
        { nextSound++; if (MobileSoundPolicy.Scheduled(settings.AutoPlay, settings.AutoNoteSound)) PlaySe(); }
        session.Advance(seconds, held); stage.Seconds = seconds; stage.SetNeedsDisplay();
    }

    private void Finish()
    {
        if (session is null || audio is null || !running) return;
        session.Advance(Math.Max(audio.Duration, session.Difficulty.Notes.Select(n => n.EndSeconds).DefaultIfEmpty(0).Max()) + .2, held);
        running = false;
        string result = $"SCORE {session.Score:N0}\nMAX COMBO {session.MaxCombo}";
        ShowSongs(); Alert("RESULT（移植確認用）", result);
    }

    private void InputChanged(IReadOnlySet<int> next, double timestamp)
    {
        if (!running || audio is null || session is null || settings.AutoPlay) return;
        double seconds = MobilePlaybackTiming.ChartSeconds(audio.CurrentTime, settings.AudioOffsetMs)
            - Math.Clamp(CAAnimation.CurrentMediaTime() - timestamp, 0, .25);
        foreach (int lane in held.Except(next).ToArray()) session.Release(lane, seconds);
        foreach (int lane in next.Except(held).ToArray()) session.Press(lane, seconds);
        held.Clear(); held.UnionWith(next);
    }
    private void PlaySe()
    {
        if (settings.SeVolume <= 0 || seVoices.Count == 0) return;
        AVAudioPlayer voice = seVoices[seVoice++ % seVoices.Count];
        voice.CurrentTime = 0; voice.Volume = settings.SeVolume / 100f; voice.Play();
    }
    internal void Pause()
    { if (running) { audio?.Pause(); running = false; held.Clear(); stage.ClearTouches(); ShowPause(); } }
    private void ShowPause()
    {
        ClearMenu();
        Button("再開", 20, 40, 200, () => { menu.Hidden = true; running = true; audio?.Play(); });
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
        if (disposing) { displayLink?.Invalidate(); displayLink?.Dispose(); audio?.Dispose(); foreach (var voice in seVoices) voice.Dispose(); }
        base.Dispose(disposing);
    }
}
