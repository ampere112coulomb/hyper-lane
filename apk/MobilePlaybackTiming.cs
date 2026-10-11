namespace HyperLane.Android;

// Audio owns the clock. UI rendering must not assume that Start() means sound
// has already reached the output device (buffering and output latency vary).
internal sealed class MobilePlaybackTiming
{
    private double mediaSeconds, systemSeconds, rate;
    internal void Anchor(double media, double system, double clockRate)
    { mediaSeconds = media; systemSeconds = system; rate = clockRate; }
    internal double At(double system) => Math.Max(0, mediaSeconds + (system - systemSeconds) * rate);
    internal static double ChartSeconds(double playback, int offsetMs) => playback - offsetMs / 1000.0;
}
