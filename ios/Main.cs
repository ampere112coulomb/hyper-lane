using Foundation;
using UIKit;

namespace HyperLane.iOS;

internal static class Program
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }
    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        Console.Error.WriteLine("HL startup: delegate");
        Window = new UIWindow(UIScreen.MainScreen.Bounds) { RootViewController = new GameController() };
        Window.MakeKeyAndVisible();
        application.IdleTimerDisabled = true;
        return true;
    }
    public override void OnResignActivation(UIApplication application)
    { (Window?.RootViewController as GameController)?.Pause(); }
}
