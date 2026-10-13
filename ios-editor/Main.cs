using Foundation;
using UIKit;
namespace HyperLane.Editor.iOS;
internal static class Program
{
    static void Main(string[] args) => UIApplication.Main(args, null, typeof(EditorAppDelegate));
}
[Register("EditorAppDelegate")]
public sealed class EditorAppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }
    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        Window = new UIWindow(UIScreen.MainScreen.Bounds) { RootViewController = new EditorController() };
        Window.MakeKeyAndVisible(); app.IdleTimerDisabled = true; return true;
    }
    public override void OnResignActivation(UIApplication app) => (Window?.RootViewController as EditorController)?.Pause();
}
