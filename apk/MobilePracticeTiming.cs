namespace HyperLane.Android;

// Shared with the desktop-run checks: the lead-in is eight chart beats, not a
// fixed number of seconds. Offset changes move audio and chart together.
internal readonly record struct MobilePracticeTiming(double StartRaw, double AnchorChart, int OffsetMs)
{
    internal static MobilePracticeTiming At(MobileChart chart, MobileDifficulty diff, double beat, int offsetMs)
        => new(chart.Offset + MobileChart.BeatSeconds(diff, chart.Bpm, beat - 8) + offsetMs / 1000.0,
            chart.Offset + MobileChart.BeatSeconds(diff, chart.Bpm, beat), offsetMs);
    internal bool IsPreroll(double chartSeconds) => chartSeconds < AnchorChart;
    internal double Gain(double chartSeconds)
    {
        double from = Math.Max(0, StartRaw) - OffsetMs / 1000.0;
        return AnchorChart <= from ? 1 : Math.Clamp((chartSeconds - from) / (AnchorChart - from), 0, 1);
    }
    internal void SkipPreviousNotes(MobileDifficulty diff)
    {
        foreach (var note in diff.Notes) note.Done = note.StartSeconds < AnchorChart;
    }
}
