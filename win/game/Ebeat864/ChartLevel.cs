using System;
using System.Globalization;

namespace HyperLane;

internal static class ChartLevel
{
    internal static bool TryParse(string value, out double level) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out level) && double.IsFinite(level);

    internal static string NormalizeLabel(string value)
    {
        value = (value ?? "").Trim();
        int[] elements = StringInfo.ParseCombiningCharacters(value);
        return elements.Length > 2 ? value[..elements[2]] : value;
    }

    internal static string Display(string value)
    {
        if (!TryParse(value, out double level) || level < 8) return value;
        double whole = Math.Floor(level);
        return whole.ToString("0", CultureInfo.InvariantCulture) + (level - whole >= .5 ? "+" : "");
    }
}
