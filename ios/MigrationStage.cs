using CoreGraphics;
using HyperLane.Android;
using Foundation;
using UIKit;

namespace HyperLane.iOS;

// Geometry/touch verification only. Do not describe this solid-color renderer
// as the final PC-equivalent skin, video, or post-processing implementation.
internal sealed class MigrationStage : UIView
{
    internal MobileSession? Session;
    internal IosSettings Settings = new();
    internal double Seconds;
    internal Action<IReadOnlySet<int>, double>? InputChanged;
    internal Action? PauseRequested;
    private readonly Dictionary<UITouch, int> touches = new();
    private nfloat Scale => (nfloat)Math.Min((double)Bounds.Width / 1920, (double)Bounds.Height / 1080);
    private CGPoint Origin => new((Bounds.Width - 1920 * Scale) / 2, (Bounds.Height - 1080 * Scale) / 2);
    internal MigrationStage() { BackgroundColor = UIColor.Black; MultipleTouchEnabled = true; }
    internal void ClearTouches() => touches.Clear();
    public override void Draw(CGRect rect)
    {
        base.Draw(rect);
        if (Session is not { } session) return;
        using CGContext context = UIGraphics.GetCurrentContext();
        context.SaveState(); context.TranslateCTM(Origin.X, Origin.Y); context.ScaleCTM(Scale, Scale);
        context.ClipToRect(new CGRect(0, 0, 1920, 1080));
        MobileDifficulty diff = session.Difficulty; MobileChart chart = session.Song.Chart;
        double beat = MobileChart.BeatAtSeconds(diff, chart.Bpm, Seconds - chart.Offset);
        MobileProjection projection = MobileProjection.At(diff, beat);
        double width = 2.3 / diff.Lanes;
        double[] lines = new double[diff.Lanes];
        HyperLane.JudgementLineMotion.Evaluate(diff.JudgementLineChanges, beat, lines);
        for (int lane = 0; lane < lines.Length; lane++) lines[lane] = projection.DepthAtScreenFraction(lines[lane] / 100);
        for (int lane = 1; lane <= diff.Lanes; lane++)
        {
            double left = 1.15 - lane * width;
            Quad(left, left + width, .004, -12, 52, UIColor.FromRGBA(8, 20, 37, (int)(255 * (1 - Settings.LaneTransparency / 100.0))));
            Quad(left, left + width, .065, lines[lane - 1] - .028, lines[lane - 1] + .028, UIColor.Cyan);
        }
        foreach (var grouping in diff.NotesByLayer.OrderBy(layer => layer.Key))
        {
            MobileDifficulty timing = grouping.Key == 1 ? diff : diff.Layers[grouping.Key];
            double layerBeat = MobileChart.BeatAtSeconds(timing, chart.Bpm, Seconds - chart.Offset);
            double scroll = MobileChart.ScrollPosition(timing, chart.Bpm, layerBeat);
            double speed = Settings.NoteSpeed * 10 * MobileChart.NsMultiplierAt(timing, layerBeat);
            foreach (MobileNote note in grouping.Value)
            {
                if (note.Done) continue;
                int first = Math.Max(1, note.Lane), last = (int)Math.Min(diff.Lanes, (long)note.Lane + note.LaneSpan - 1);
                for (int lane = first; lane <= last; lane++)
                {
                    double z = (note.ScrollStart - scroll) * speed * note.Ss + lines[lane - 1];
                    double end = (note.ScrollEnd - scroll) * speed * note.Ss + lines[lane - 1];
                    if (note.Kind == 'L' && (note.Started || Seconds >= note.StartSeconds)) z = lines[lane - 1];
                    double left = 1.15 - lane * width + .006, right = left + width - .012;
                    UIColor color = note.Kind switch { 'H' or 'W' => UIColor.FromRGB(218, 172, 35), 'L' => UIColor.FromRGB(185, 130, 255),
                        'M' => UIColor.FromRGB(111, 168, 139), 'D' => UIColor.FromRGB(255, 75, 90), 'G' => UIColor.FromRGB(16, 185, 129),
                        _ => lane % 2 == 0 ? UIColor.FromRGB(79, 163, 255) : UIColor.White };
                    if (note.Kind is 'L' or 'G') Quad(left, right, .034, Math.Max(-12, Math.Min(z, end)), Math.Min(45, Math.Max(z, end)), color);
                    if (note.Kind != 'G' && z >= -12 && z <= 45 && !note.Started) Quad(left, right, .052, z - .15, z + .15, color);
                }
            }
        }
        context.SetStrokeColor(UIColor.FromRGBA(80, 220, 226, 70).CGColor); context.SetLineWidth(2);
        for (int lane = 0; lane < diff.Lanes; lane++)
        {
            TouchRect zone = TouchZones.GetRect(diff.Lanes, lane, 1920, 1080);
            context.StrokeRect(new CGRect(zone.Left, zone.Top, zone.Right - zone.Left, zone.Bottom - zone.Top));
        }
        using var hudFont = UIFont.SystemFontOfSize(32);
        var attributes = new UIStringAttributes { ForegroundColor = UIColor.White, Font = hudFont };
        using var text = new NSString($"{chart.Title} / {diff.Name} Lv.{diff.Level}\nSCORE {session.Score:D7}    {session.Combo} COMBO");
        text.DrawString(new CGRect(70, 50, 1250, 100), attributes);
        context.RestoreState();
        void Quad(double left, double right, double y, double near, double far, UIColor color)
        {
            if (far <= near) return;
            float[] points = projection.Polygon(left, y, far, right, y, far, right, y, near, left, y, near);
            if (points.Length < 6) return;
            context.BeginPath(); context.MoveTo(points[0], points[1]);
            for (int i = 2; i < points.Length; i += 2) context.AddLineToPoint(points[i], points[i + 1]);
            context.ClosePath(); context.SetFillColor(color.CGColor); context.FillPath();
        }
    }
    private void Update(NSSet changed, bool release)
    {
        if (Session is null) return;
        double timestamp = 0;
        foreach (UITouch touch in changed)
        {
            timestamp = touch.Timestamp; CGPoint point = touch.LocationInView(this);
            float x = (float)((point.X - Origin.X) / Scale), y = (float)((point.Y - Origin.Y) / Scale);
            if (release && x > 1766 && y < 130) { PauseRequested?.Invoke(); return; }
            if (release || Settings.AutoPlay) touches.Remove(touch);
            else touches[touch] = TouchZones.HitTest(Session.Difficulty.Lanes, x, y, 1920, 1080) + 1;
        }
        InputChanged?.Invoke(touches.Values.Where(lane => lane > 0).ToHashSet(), timestamp);
    }
    public override void TouchesBegan(NSSet touches, UIEvent? evt) => Update(touches, false);
    public override void TouchesMoved(NSSet touches, UIEvent? evt) => Update(touches, false);
    public override void TouchesEnded(NSSet touches, UIEvent? evt) => Update(touches, true);
    public override void TouchesCancelled(NSSet touches, UIEvent? evt) => Update(touches, true);
}
