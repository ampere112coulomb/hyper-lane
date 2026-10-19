using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HyperLane;

internal sealed class PlayerRating
{
	internal const int BestCount = 30;
	// Legacy Custom and unnamed difficulties are displayed as SPECIAL too.
	internal static bool IsRatedDifficulty(string? name) => !string.IsNullOrWhiteSpace(name)
		&& !name.Trim().Equals("Special", StringComparison.OrdinalIgnoreCase)
		&& !name.Trim().Equals("Custom", StringComparison.OrdinalIgnoreCase);
	private static bool IsRatedKey(string key) => IsRatedDifficulty(key[(key.LastIndexOf('|') + 1)..]);
	private readonly string path;
	private readonly Dictionary<string, Entry> best = new(StringComparer.OrdinalIgnoreCase);

	internal sealed class Entry
	{
		public string Key { get; set; } = "";
		public string ChartHash { get; set; } = "";
		public double Level { get; set; }
		public int Score { get; set; }
		public int[]? Counts { get; set; }
		public int? MaxCombo { get; set; }
		public bool FullCombo { get; set; }
	}

	internal PlayerRating(string path)
	{
		this.path = path;
		try
		{
			if (!File.Exists(path)) return;
			foreach (Entry entry in JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path)) ?? [])
			{
				if (!string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.ChartHash)
					&& double.IsFinite(entry.Level) && entry.Score is >= 0 and <= 1000000)
					best[entry.Key] = entry;
			}
		}
		catch (Exception)
		{
			// A damaged rating file must not prevent the game from starting.
		}
	}

	internal static string Key(string songFolder, string chartFile, string difficulty) =>
		string.Join("|", songFolder.Replace('\\', '/'), chartFile.Replace('\\', '/'), difficulty).ToUpperInvariant();

	internal static double ChartRating(double level, int score)
	{
		double fraction = Math.Clamp(score, 0, 1000000) / 1000000.0;
		return Math.Max(0, level) * fraction * fraction * fraction;
	}

	internal double TotalFor(IEnumerable<(string Key, string ChartHash, double Level)> charts) =>
		charts.Where(chart => IsRatedKey(chart.Key) && best.TryGetValue(chart.Key, out Entry? entry)
			&& entry.ChartHash.Equals(chart.ChartHash, StringComparison.OrdinalIgnoreCase)
			&& entry.Level == chart.Level)
			.Select(chart => ChartRating(chart.Level, best[chart.Key].Score))
			.OrderByDescending(x => x).Take(BestCount).Sum() / BestCount;

	internal int? BestScore(string key, string chartHash, double level) =>
		best.TryGetValue(key, out var entry) && entry.ChartHash.Equals(chartHash, StringComparison.OrdinalIgnoreCase)
		&& entry.Level == level ? entry.Score : null;

	internal Entry? BestEntry(string key,string chartHash,double level) => BestScore(key,chartHash,level).HasValue ? best[key] : null;
	internal bool Update(string key, string chartHash, double level, int score, int[]? counts = null, int? maxCombo = null)
	{
		if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(chartHash)
			|| !double.IsFinite(level) || score is < 0 or > 1000000)
			return false;
		bool fullCombo=RankRules.IsFullCombo(counts);
		if (best.TryGetValue(key, out Entry? previous)
			&& previous.ChartHash.Equals(chartHash, StringComparison.OrdinalIgnoreCase)
			&& previous.Level == level && (previous.Score > score || previous.Score == score && (previous.Counts != null || counts == null)))
		{
			if(!fullCombo || previous.FullCombo || RankRules.IsFullCombo(previous.Counts))return false;
			score=previous.Score;counts=previous.Counts;maxCombo=previous.MaxCombo;
		}
		Dictionary<string, Entry> updated = new(best, StringComparer.OrdinalIgnoreCase)
		{
			[key] = new Entry { Key = key, ChartHash = chartHash, Level = level, Score = score, Counts = counts is { Length: 5 } ? (int[])counts.Clone() : null, MaxCombo = maxCombo, FullCombo=fullCombo || previous!=null && previous.ChartHash.Equals(chartHash,StringComparison.OrdinalIgnoreCase) && previous.Level==level && (previous.FullCombo||RankRules.IsFullCombo(previous.Counts)) }
		};
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		string temporary = path + ".tmp";
		File.WriteAllText(temporary, JsonSerializer.Serialize(updated.Values.OrderBy(x => x.Key).ToArray()));
		File.Move(temporary, path, true);
		best.Clear();
		foreach (var pair in updated) best.Add(pair.Key, pair.Value);
		return true;
	}
}
