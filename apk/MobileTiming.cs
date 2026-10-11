namespace HyperLane.Android;

// Exact piecewise integrals; keep fine 1/256-beat BS pulses without frame-based rounding.
internal sealed class MobileTiming
{
    private readonly Segment[] tempo;
    private readonly Segment[] scroll;
    private readonly record struct Segment(double Beat, double Seconds, double Position, double Bpm, double Bs);

    public MobileTiming(MobileDifficulty difficulty, double initialBpm)
    {
        var segments = new List<Segment>();
        double bpm = initialBpm, beat = 0, seconds = 0;
        foreach (var change in difficulty.BpmChanges)
        {
            if (change.Beat <= 0) { bpm = change.Value; continue; }
            segments.Add(new(beat, seconds, 0, bpm, 1));
            seconds += (change.Beat - beat) * 60 / bpm;
            beat = change.Beat; bpm = change.Value;
        }
        segments.Add(new(beat, seconds, 0, bpm, 1));
        tempo = segments.ToArray();
        segments.Clear();
        double position = 0, bs = 1;
        beat = 0; seconds = 0;
        int tempoIndex = 0, bsIndex = 0;
        while (bsIndex < difficulty.BsChanges.Count && difficulty.BsChanges[bsIndex].Beat <= 0)
            bs = difficulty.BsChanges[bsIndex++].Value;
        while (true)
        {
            segments.Add(new(beat, seconds, position, tempo[tempoIndex].Bpm, bs));
            double nextTempo = tempoIndex + 1 < tempo.Length ? tempo[tempoIndex + 1].Beat : double.PositiveInfinity;
            double nextBs = bsIndex < difficulty.BsChanges.Count ? difficulty.BsChanges[bsIndex].Beat : double.PositiveInfinity;
            double next = Math.Min(nextTempo, nextBs);
            if (!double.IsFinite(next)) break;
            double nextSeconds = Seconds(next);
            position += (nextSeconds - seconds) * 2 * bs;
            beat = next; seconds = nextSeconds;
            while (tempoIndex + 1 < tempo.Length && tempo[tempoIndex + 1].Beat <= next) tempoIndex++;
            while (bsIndex < difficulty.BsChanges.Count && difficulty.BsChanges[bsIndex].Beat <= next)
                bs = difficulty.BsChanges[bsIndex++].Value;
        }
        scroll = segments.ToArray();
    }

    public double Seconds(double beat)
    { Segment segment = Find(tempo, beat, false); return segment.Seconds + (beat - segment.Beat) * 60 / segment.Bpm; }
    public double Beat(double seconds)
    { Segment segment = Find(tempo, seconds, true); return segment.Beat + (seconds - segment.Seconds) * segment.Bpm / 60; }
    public double Scroll(double beat)
    { Segment segment = Find(scroll, beat, false); return segment.Position + (Seconds(beat) - segment.Seconds) * 2 * segment.Bs; }
    private static Segment Find(Segment[] segments, double value, bool seconds)
    {
        int low = 0, high = segments.Length;
        while (low + 1 < high)
        {
            int middle = (low + high) / 2;
            if ((seconds ? segments[middle].Seconds : segments[middle].Beat) <= value) low = middle;
            else high = middle;
        }
        return segments[low];
    }
}
