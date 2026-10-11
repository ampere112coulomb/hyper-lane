namespace HyperLane.Android;

internal static class MobileFilters
{
    internal static (byte Tone, byte Alpha) BrightnessOverlay(double strength)
        => (strength > 0 ? (byte)255 : (byte)0,
            (byte)Math.Round(Math.Clamp(Math.Abs(strength), 0, 100) * 255 / 100));

    internal static MobileFilterChange? At(List<MobileFilterChange> changes, double beat)
    {
        MobileFilterChange? active = null;
        double from = 0;
        static double Strength(MobileFilterChange? value, double initial, double at)
        {
            if (value is not { } filter) return 0;
            if (!filter.Ramp) return at < filter.Beat + filter.Duration ? filter.Strength : 0;
            return initial + (filter.Strength - initial) * (filter.Duration <= 0 ? 1 : Math.Clamp((at - filter.Beat) / filter.Duration, 0, 1));
        }
        foreach (var change in changes)
        {
            if (change.Beat > beat) break;
            double previous = Strength(active, from, change.Beat);
            from = active is {} prior && prior.Kind == change.Kind && prior.BackgroundOnly == change.BackgroundOnly ? previous : 0;
            active = change;
        }
        if (active is not { } current) return null;
        double strength = Strength(active, from, beat);
        return strength != 0 ? current with { Strength = strength } : null;
    }
}
