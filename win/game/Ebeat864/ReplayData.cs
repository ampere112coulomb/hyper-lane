using System;
using System.Collections.Generic;

namespace HyperLane;

internal sealed class ReplayData
{
	public int Version { get; set; } = 1;

	public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

	public string SongFolder { get; set; } = "";

	public string ChartFile { get; set; } = "";

	public string ChartHash { get; set; } = "";

	public string Difficulty { get; set; } = "";

	public string Title { get; set; } = "";

	public double NoteSpeed { get; set; }

	public double AudioOffset { get; set; }

	public double BgmVolume { get; set; }

	public double SeVolume { get; set; }

	public int StartMarginMs { get; set; }

	public int Score { get; set; }

	public int MaxCombo { get; set; }

	public int[] Counts { get; set; } = new int[5];

	public List<ReplayInput> Inputs { get; set; } = new List<ReplayInput>();

	public List<ReplayJudgement> Judgements { get; set; } = new List<ReplayJudgement>();
}
