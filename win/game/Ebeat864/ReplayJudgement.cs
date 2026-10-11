namespace HyperLane;

internal sealed class ReplayJudgement
{
	public double Time { get; set; }

	public int NoteIndex { get; set; }

	public int Grade { get; set; }

	public bool Done { get; set; }

	public bool Started { get; set; }

	public bool Missed { get; set; }
}
