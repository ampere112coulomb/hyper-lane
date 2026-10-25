using System;
using System.Collections.Generic;

namespace HyperLane;

public static class ReplayJudgementMigration
{
    // Only replay JSON files belong to this migration. Never touch score/profile data.
    public static void Apply(string dataRoot)
    {
        string directory = System.IO.Path.Combine(dataRoot, "replay");
        System.IO.Directory.CreateDirectory(directory);
        string marker = System.IO.Path.Combine(directory, ".judgement-50-80-100-120-v1");
        if (System.IO.File.Exists(marker)) return;
        if ((System.IO.File.GetAttributes(directory) & System.IO.FileAttributes.ReparsePoint) != 0)
            throw new System.IO.IOException("Replay migration refuses a linked directory.");
        // Write the marker only after all old files have been removed successfully.
        foreach (string path in System.IO.Directory.GetFiles(directory, "*.json", System.IO.SearchOption.TopDirectoryOnly))
            System.IO.File.Delete(path);
        System.IO.File.WriteAllText(marker, "50,80,100,120");
    }
}

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
