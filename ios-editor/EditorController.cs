using AVFoundation;
using Foundation;
using UIKit;
using WebKit;
using UniformTypeIdentifiers;
using System.Text;
using System.Text.Json;
namespace HyperLane.Editor.iOS;

public sealed class EditorController : UIViewController, IWKScriptMessageHandler
{
    WKWebView web = null!;
    DialogDelegate dialogs = null!;
    NavigationDelegate navigation = null!;
    readonly List<NSUrl> grants = [];
    UIDocumentPickerViewController? picker;
    UIDocumentPickerDelegate? pickerDelegate;
    string chartPath = "", folderPath = "", requestedSong = "";
    int audioGeneration;
    readonly string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EditorCache");
    public override bool PrefersStatusBarHidden() => true;
    public override bool PrefersHomeIndicatorAutoHidden => true;
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Console.Error.WriteLine("HL Editor ViewDidLoad");
        Directory.CreateDirectory(cache);
        AVAudioSession.SharedInstance().SetCategory(AVAudioSession.CategoryPlayback, out _);
        AVAudioSession.SharedInstance().SetActive(true, out _);
        var configuration = new WKWebViewConfiguration { AllowsInlineMediaPlayback = true, MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None };
        configuration.UserContentController.AddScriptMessageHandler(this, "editor");
        dialogs = new DialogDelegate(this); navigation = new NavigationDelegate(this);
        web = new WKWebView(View!.Bounds, configuration) { AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight, UIDelegate = dialogs, NavigationDelegate = navigation };
        web.ScrollView.ScrollEnabled = false; web.ScrollView.Bounces = false;
        View.AddSubview(web);
        Console.Error.WriteLine("HL Editor WebKit created");
        string root = NSBundle.MainBundle.BundlePath;
        string html = File.ReadAllText(Path.Combine(root, "editor.html"))
            .Replace("const LANE_WIDTH = 50;", "let LANE_WIDTH = 50;")
            .Replace("function saveEditorBackupNow() {", "function saveEditorBackupNow() { return;")
            .Replace("setTimeout(restoreEditorBackupIfNeeded, 300);", "// iOS: backup restore disabled")
            .Replace("new (window.AudioContext || window.webkitAudioContext)()", "new (window.AudioContext || window.webkitAudioContext)({latencyHint:'interactive'})")
            .Replace("new (window.AudioContext||window.webkitAudioContext)()", "new (window.AudioContext||window.webkitAudioContext)({latencyHint:'interactive'})")
            .Replace("const TEST_PLAY_SYNC_OFFSET_SECONDS = -0.096;", "const TEST_PLAY_SYNC_OFFSET_SECONDS = 0;")
            .Replace("(audioCtx.currentTime - startTime) * playbackRate", "(mobileOutputTime(audioCtx) - startTime) * playbackRate")
            .Replace("((audioCtx?.currentTime||testPlay.clockAt) - testPlay.clockAt)", "((audioCtx ? mobileOutputTime(audioCtx) : testPlay.clockAt) - testPlay.clockAt)")
            .Replace("<head>", "<head><meta name=\"viewport\" content=\"width=device-width,initial-scale=1,maximum-scale=1,viewport-fit=cover\"><script src=\"ios-editor.js\"></script>")
            .Replace("</head>", "<link rel=\"stylesheet\" href=\"mobile-editor.css\"></head>")
            .Replace("</body>", "<script src=\"mobile-editor.js\"></script><script src=\"editor-qa.js\"></script></body>");
        // Stable file origin retains editor preferences between launches.
        string www = Path.Combine(cache, "www"); Directory.CreateDirectory(www);
        foreach (string asset in new[] { "ios-editor.js", "mobile-editor.js", "mobile-editor.css", "editor-qa.js" }) File.Copy(Path.Combine(root, asset), Path.Combine(www, asset), true);
        File.WriteAllText(Path.Combine(www, "editor.html"), html);
        web.LoadFileUrl(NSUrl.FromFilename(Path.Combine(www, "editor.html")), NSUrl.FromFilename(www));
        Console.Error.WriteLine("HL Editor local page requested");
    }
    Task Eval(string script)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        InvokeOnMainThread(() => web.EvaluateJavaScript(script, (_, error) => { if (error is null) completion.TrySetResult(); else completion.TrySetException(new InvalidOperationException(error.LocalizedDescription)); }));
        return completion.Task;
    }
    public void Pause() => _ = Eval("if(testPlay.active)exitTestPlay();if(isPlaying)togglePlayback();");
    static string J(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";
    async Task Status(string text) => await Eval($"mobileStatus({J(text)});");
    void Alert(string message)
    {
        var alert = UIAlertController.Create("Hyper Lane Editor", message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null)); PresentViewController(alert, true, null);
    }
    public async void DidReceiveScriptMessage(WKUserContentController controller, WKScriptMessage message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message.Body.ToString()); var root = doc.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "open-chart": Pick("chart"); break;
                case "open-audio": Pick("audio"); break;
                case "resolve-song": requestedSong = root.GetProperty("name").GetString() ?? ""; await ResolveSong(); break;
                case "save-chart": await Save(root.GetProperty("text").GetString() ?? "", root.GetProperty("filename").GetString() ?? "chart.2d"); break;
                case "native-test": await Eval("startTestPlay(false);"); break;
                case "native-video": Alert("MP4書き出しはPC版エディタで行ってください。iOS版ではエディタ内のテストプレイを利用できます。"); break;
                case "load-se": await SendAudio(Path.Combine(NSBundle.MainBundle.BundlePath, "SE.wav"), "SE.wav", true); break;
                case "audio-error": await Status("音源を読み込めません。音源を選び直してください。"); break;
                case "finish": await Status("ホーム画面に戻るには、端末のホーム操作を使用してください。"); break;
                case "qa-report":
                    if (Environment.GetEnvironmentVariable("HL_EDITOR_QA") is { Length: > 0 } phase)
                    {
                        string qa = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "editor-qa"); Directory.CreateDirectory(qa);
                        await File.WriteAllTextAsync(Path.Combine(qa, phase + ".json"), root.GetProperty("report").GetRawText());
                        await File.WriteAllTextAsync(Path.Combine(qa, phase + ".ready"), "ready");
                    }
                    break;
            }
        }
        catch (Exception e) { Alert(e.Message); }
    }
    void Pick(string kind)
    {
        picker = new UIDocumentPickerViewController(kind == "folder" ? [UTTypes.Folder] : [UTTypes.Item], false);
        pickerDelegate = new PickerDelegate(this, kind); picker.Delegate = pickerDelegate;
        PresentViewController(picker, true, null);
    }
    sealed class PickerDelegate(EditorController owner, string kind) : UIDocumentPickerDelegate
    {
        public override async void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
        {
            if (urls.Length == 0) return;
            try
            {
                var url = urls[0]; if (url.StartAccessingSecurityScopedResource()) owner.grants.Add(url);
                string path = url.Path ?? throw new IOException("ファイルを開けません。");
                if (kind == "audio") { await owner.LoadAudio(path, true); return; }
                if (kind == "folder") { owner.folderPath = path; await owner.ResolveSong(false); return; }
                if (!Path.GetExtension(path).Equals(".2d", StringComparison.OrdinalIgnoreCase)) throw new IOException("chart.2dなどの.2d譜面を選択してください。");
                if (new FileInfo(path).Length > 16_777_216) throw new IOException("譜面が大きすぎます。");
                string text = await File.ReadAllTextAsync(path); owner.chartPath = path; owner.audioGeneration++;
                await owner.Eval($"audioBuffer=null;waveformData=[];document.getElementById('meta-song').value='';importChartText({J(text)},{J(Path.GetFileName(path))});mobileStatus('譜面を読み込みました');AndroidEditor.postMessage(JSON.stringify({{type:'resolve-song',name:document.getElementById('meta-song').value}}));");
            }
            catch (Exception e) { owner.Alert(e.Message); }
        }
    }
    async Task ResolveSong(bool ask = true)
    {
        if (string.IsNullOrWhiteSpace(requestedSong)) return;
        string relative = requestedSong.Replace('\\', '/').Trim('"');
        if (Path.IsPathRooted(relative) || relative.Split('/').Any(x => x == "..")) { await Status("音源は譜面フォルダ内の相対パスで指定してください。"); return; }
        foreach (string directory in new[] { Path.GetDirectoryName(chartPath) ?? "", folderPath }.Where(x => x.Length > 0).Distinct())
        {
            string candidate = Path.Combine(directory, relative);
            try { if (File.Exists(candidate)) { await LoadAudio(candidate, false); return; } } catch (UnauthorizedAccessException) { }
        }
        await Status("指定音源が見つかりません。音源の入ったフォルダ、またはメニューから音源を選択してください。");
        if (ask)
        {
            var dialog = UIAlertController.Create("音源フォルダ", "譜面と音源が入っているフォルダを選ぶと、指定音源を自動で読み込みます。", UIAlertControllerStyle.Alert);
            dialog.AddAction(UIAlertAction.Create("フォルダを選ぶ", UIAlertActionStyle.Default, _ => Pick("folder")));
            dialog.AddAction(UIAlertAction.Create("キャンセル", UIAlertActionStyle.Cancel, null)); PresentViewController(dialog, true, null);
        }
    }
    async Task LoadAudio(string path, bool manual)
    {
        int generation = ++audioGeneration;
        await Status("音源を読み込み中…（OGGはiOS用に変換します）");
        string ready = Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            ? await HyperLane.iOS.IosAudioConverter.ToM4aAsync(path, Path.Combine(cache, "audio")) : path;
        if (generation != audioGeneration) return;
        if (manual) await Eval($"document.getElementById('meta-song').value={J(Path.GetFileName(path))};markEditorDirty();");
        await SendAudio(ready, Path.GetFileName(path), false);
    }
    async Task SendAudio(string path, string name, bool se)
    {
        await Eval(se ? "beginNativeSE();" : $"beginNativeAudio({J(name)});");
        using var file = File.OpenRead(path); byte[] buffer = new byte[48 * 1024]; int count;
        while ((count = await file.ReadAsync(buffer)) > 0)
            await Eval($"{(se ? "appendNativeSEChunk" : "appendNativeAudioChunk")}({J(Convert.ToBase64String(buffer, 0, count))});");
        await Eval(se ? "void finishNativeSE();" : "void finishNativeAudio();");
    }
    async Task Save(string text, string name)
    {
        if (Encoding.UTF8.GetByteCount(text) > 16_777_216) throw new IOException("譜面が大きすぎます。");
        if (chartPath.Length > 0)
        {
            try { await File.WriteAllTextAsync(chartPath, text); await Eval("markEditorSaved();mobileStatus('保存しました');"); return; }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        string filename = Path.GetFileName(name); if (!filename.EndsWith(".2d", StringComparison.OrdinalIgnoreCase)) filename = "chart.2d";
        string directory = Path.Combine(cache, "exports", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        string export = Path.Combine(directory, filename); await File.WriteAllTextAsync(export, text);
        var save = new UIDocumentPickerViewController([NSUrl.FromFilename(export)], UIDocumentPickerMode.ExportToService);
        pickerDelegate = new SaveDelegate(this); save.Delegate = pickerDelegate; picker = save; PresentViewController(save, true, null);
    }
    sealed class SaveDelegate(EditorController owner) : UIDocumentPickerDelegate
    {
        public override async void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
        {
            if (urls.Length == 0) return;
            if (urls[0].StartAccessingSecurityScopedResource()) owner.grants.Add(urls[0]);
            owner.chartPath = urls[0].Path ?? "";
            await owner.Eval("markEditorSaved();mobileStatus('保存しました');");
        }
    }
    async void Ready()
    {
        Console.Error.WriteLine("HL Editor page loaded");
        if (Environment.GetEnvironmentVariable("HL_EDITOR_QA") is not { Length: > 0 } phase) return;
        try { await Task.Delay(1000); await Eval($"void iosEditorQa({J(phase)});"); }
        catch (Exception e) { Console.Error.WriteLine("Editor QA: " + e); }
    }
    sealed class NavigationDelegate(EditorController owner) : WKNavigationDelegate
    {
        public override void DidFinishNavigation(WKWebView view, WKNavigation navigation) => owner.Ready();
        public override void DecidePolicy(WKWebView view, WKNavigationAction action, Action<WKNavigationActionPolicy> decision) => decision(action.Request.Url?.IsFileUrl == true ? WKNavigationActionPolicy.Allow : WKNavigationActionPolicy.Cancel);
    }
    sealed class DialogDelegate(EditorController owner) : WKUIDelegate
    {
        public override void RunJavaScriptAlertPanel(WKWebView view, string message, WKFrameInfo frame, Action completion)
        {
            var alert = UIAlertController.Create("Hyper Lane Editor", message, UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, _ => completion())); owner.PresentViewController(alert, true, null);
        }
        public override void RunJavaScriptConfirmPanel(WKWebView view, string message, WKFrameInfo frame, Action<bool> completion)
        {
            var alert = UIAlertController.Create("Hyper Lane Editor", message, UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, _ => completion(true)));
            alert.AddAction(UIAlertAction.Create("キャンセル", UIAlertActionStyle.Cancel, _ => completion(false))); owner.PresentViewController(alert, true, null);
        }
        public override void RunJavaScriptTextInputPanel(WKWebView view, string prompt, string? defaultText, WKFrameInfo frame, Action<string?> completion)
        {
            var alert = UIAlertController.Create("Hyper Lane Editor", prompt, UIAlertControllerStyle.Alert); alert.AddTextField(t => t.Text = defaultText);
            alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, _ => completion(alert.TextFields?[0].Text)));
            alert.AddAction(UIAlertAction.Create("キャンセル", UIAlertActionStyle.Cancel, _ => completion(null))); owner.PresentViewController(alert, true, null);
        }
    }
}
