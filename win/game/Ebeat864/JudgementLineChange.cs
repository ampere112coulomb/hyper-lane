using System;
using System.Collections.Generic;
using System.Globalization;

namespace HyperLane;

internal readonly record struct JudgementLineChange(double Beat, double[] Positions, double Duration, bool Ramp, string Motion = "RAMP");

internal static class JudgementLineMotion
{
	internal static bool TryParse(double beat, string value, out JudgementLineChange change)
	{
		change = default;
		string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
		if (parts.Length != 3 || !double.IsFinite(beat) || beat < 0 ||
			!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double duration) ||
			!double.IsFinite(duration) || duration < 0 ||
			(!parts[2].Equals("RAMP", StringComparison.OrdinalIgnoreCase) && !parts[2].Equals("INSTANT", StringComparison.OrdinalIgnoreCase) &&
			 !parts[2].Equals("EASE_OUT", StringComparison.OrdinalIgnoreCase) && !parts[2].Equals("EASE_IN", StringComparison.OrdinalIgnoreCase))) return false;
		string[] values = parts[0].Split(';', StringSplitOptions.TrimEntries);
		if (values.Length is < 1 or > 12) return false;
		double[] positions = new double[values.Length];
		for (int lane = 0; lane < values.Length; lane++)
		{
			if (!double.TryParse(values[lane], NumberStyles.Float, CultureInfo.InvariantCulture, out double position) ||
				!double.IsFinite(position) || position < 0 || position > 100) return false;
			positions[lane] = position;
		}
		change = new(beat, positions, duration, !parts[2].Equals("INSTANT", StringComparison.OrdinalIgnoreCase), parts[2].ToUpperInvariant());
		return true;
	}

	// Evaluate from chart time so seek, retry, autoplay and recorded video agree.
	internal static void Evaluate(List<JudgementLineChange> changes, double beat, Span<double> positions)
	{
		positions.Clear();
		Span<double> from = stackalloc double[positions.Length];
		from.Clear();
		JudgementLineChange? active = null;
		foreach (JudgementLineChange change in changes)
		{
			if (change.Beat > beat) break;
			Advance(active, from, positions, change.Beat);
			positions.CopyTo(from);
			active = change;
		}
		Advance(active, from, positions, beat);
	}

	private static void Advance(JudgementLineChange? active, ReadOnlySpan<double> from, Span<double> positions, double beat)
	{
		if (active is not JudgementLineChange change) return;
		double progress = change.Duration > 0 ? Math.Clamp((beat - change.Beat) / change.Duration, 0, 1) : 1;
		progress = change.Motion == "EASE_OUT" ? 1 - Math.Pow(1 - progress, 3)
			: change.Motion == "EASE_IN" ? progress * progress * progress : progress;
		for (int lane = 0; lane < positions.Length; lane++)
		{
			double target = lane < change.Positions.Length ? change.Positions[lane] : 0;
			positions[lane] = change.Ramp ? from[lane] + (target - from[lane]) * progress
				: beat < change.Beat + change.Duration ? target : 0;
		}
	}
}
