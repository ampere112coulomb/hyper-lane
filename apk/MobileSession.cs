namespace HyperLane.Android;

internal sealed class MobileSession
{
    public MobileSong Song { get; }
    public MobileDifficulty Difficulty { get; }
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int MaxCombo { get; private set; }
    public double Gauge { get; private set; }
    public int[] Counts { get; } = new int[5];
    public string LastJudgement { get; private set; } = "";
    public double JudgeUntilSeconds { get; private set; }
    public List<double> TimingOffsets { get; } = new();
    public bool AutoPlay { get; set; }
    public double GlobalGauge {get;private set;}
    private int globalUnits;
    public void ConfigureDanGauge(double initial,int totalUnits,double globalInitial){Gauge=initial;GlobalGauge=globalInitial;globalUnits=Math.Max(1,totalUnits);}
    public static int ScoreUnits(MobileDifficulty difficulty)=>Math.Max(1,difficulty.Notes.Sum(n=>n.Kind=='G'?0:n.Kind=='L'?2:1));
    public event Action<MobileNote, bool>? NoteSound;
    public MobileHitEffects HitEffects { get; } = new();
    internal MobileStageMap StageMap { get; } = new();
    internal MobileChordLinks ChordLinks { get; }
    private double sceneSeconds;
    internal HyperLane.DanLifeState? Life {get;set;}
    private readonly int scoreUnits;
    private int nextJudgeIndex;
    private readonly HashSet<MobileNote> activeLongs = new();
    internal IEnumerable<MobileNote> ActiveLongs => activeLongs;
    private readonly HashSet<int> heldLanes = new();

    public MobileSession(MobileSong song, MobileDifficulty difficulty)
    {
        Song = song; Difficulty = difficulty;
        ChordLinks=new(difficulty);
        NoteSound += (note, _) => { if (note.Done && note.Kind is not ('D' or 'G')) HitEffects.Add(note); };
        NoteSound += (note, _) => StageMap.Register(this,note,sceneSeconds);
        scoreUnits = Math.Max(1, difficulty.Notes.Count(n => n.Kind != 'G') + difficulty.Notes.Count(n => n.Kind == 'L'));
        foreach (MobileNote note in difficulty.Notes) { note.Done = false; note.Started = false; }
    }

    public void Press(int lane, double seconds)
    {
        sceneSeconds=seconds;
        if (AutoPlay||Life?.Failed==true) return;
        heldLanes.Add(lane);
        MobileNote? closest = null, tap = null, hold = null;
        double delta = double.MaxValue;
        foreach (MobileNote note in Difficulty.Notes)
        {
            if (note.StartSeconds > seconds + 0.12) break;
            if (note.Done || note.Started || lane < note.Lane || lane >= note.Lane + note.LaneSpan || note.Kind is 'D' or 'G') continue;
            double ms = (seconds - note.StartSeconds) * 1000;
            if (note.Kind is 'H' or 'W' && (ms < 0 || ms > 60)) continue;
            if (Math.Abs(ms) < Math.Abs(delta)) { closest = note; delta = ms; }
            if (note.Kind is 'N' or 'M' && (tap is null || Math.Abs(ms) < Math.Abs((seconds-tap.StartSeconds)*1000))) tap=note;
            if (note.Kind is 'H' or 'W' && (hold is null || Math.Abs(ms) < Math.Abs((seconds-hold.StartSeconds)*1000))) hold=note;
        }
        if(tap is not null && hold is not null && (ReferenceEquals(closest,tap)||ReferenceEquals(closest,hold))) {
            if(tap.StartSeconds<=hold.StartSeconds){ApplyPress(tap,seconds,lane);ApplyPress(hold,seconds,lane);}
            else{ApplyPress(hold,seconds,lane);ApplyPress(tap,seconds,lane);}
            return;
        }
        if(closest is not null)ApplyPress(closest,seconds,lane);
    }

    private void ApplyPress(MobileNote closest,double seconds,int lane)
    {
        if(Life?.Failed==true)return;
        double delta=(seconds-closest.StartSeconds)*1000;
        int grade = Grade(delta);
        if (grade < 0) return;
        if (closest.Kind == 'L') { closest.Started = true; activeLongs.Add(closest); }
        else closest.Done = true;
        Judge(grade, seconds, delta);
        NoteSound?.Invoke(closest, false);
    }

    public void Release(int lane, double seconds)
    {
        sceneSeconds=seconds;
        if (AutoPlay||Life?.Failed==true) return;
        heldLanes.Remove(lane);
        foreach(var note in activeLongs.Where(n=>lane>=n.Lane&&(long)lane<(long)n.Lane+n.LaneSpan&&!AnyHeld(n,heldLanes)).ToArray()){
            activeLongs.Remove(note);note.Done = true;
            int grade = Grade((seconds - note.EndSeconds) * 1000);
            Judge(grade < 0 ? 4 : grade, seconds, (seconds - note.EndSeconds) * 1000);
            if (grade >= 0) NoteSound?.Invoke(note, true);
        }
    }

    public void Advance(double seconds, IReadOnlySet<int> held)
    {
        sceneSeconds=seconds;
        heldLanes.Clear();heldLanes.UnionWith(held);
        while (nextJudgeIndex < Difficulty.Notes.Count && (Difficulty.Notes[nextJudgeIndex].Done || Difficulty.Notes[nextJudgeIndex].Kind == 'G')) nextJudgeIndex++;
        for (int index = nextJudgeIndex; index < Difficulty.Notes.Count; index++)
        {
            if(Life?.Failed==true)break;
            MobileNote note = Difficulty.Notes[index];
            if (note.Done || note.Kind == 'G') continue;
            if (note.StartSeconds > seconds + 0.2) break;
            double ms = (seconds - note.StartSeconds) * 1000;
            if (AutoPlay)
            {
                if (ms < 0) continue;
                if (note.Kind == 'L')
                {
                    if (!note.Started) { note.Started = true; Judge(0, seconds); NoteSound?.Invoke(note, false); }
                    if (seconds >= note.EndSeconds) { note.Done = true; Judge(0, seconds); NoteSound?.Invoke(note, true); }
                }
                else if (note.Kind != 'D' || ms >= 80) { note.Done = true; Judge(0, seconds); NoteSound?.Invoke(note, false); }
            }
            else if (note.Kind is 'H' or 'W')
            {
                if (ms >= 0 && AnyHeld(note, held)) { note.Done = true; Judge(0, seconds); NoteSound?.Invoke(note, false); }
                else if (ms > 60) { note.Done = true; Judge(4, seconds); }
            }
            else if (note.Kind == 'D')
            {
                if (ms >= 80) { note.Done = true; Judge(AnyHeld(note,held) ? 4 : 0, seconds); }
            }
            else if (note.Kind == 'L')
            {
                if (!note.Started && ms >= 0 && ms <= 130 && AnyHeld(note,held))
                { note.Started = true; activeLongs.Add(note); Judge(0, seconds); NoteSound?.Invoke(note, false); }
                else if (!note.Started && ms > 130)
                { note.Started = true; Judge(4, seconds); }
                else if (note.Started && note.HoldEnd && seconds >= note.EndSeconds && AnyHeld(note,held))
                { note.Done = true; activeLongs.Remove(note); Judge(0, seconds); NoteSound?.Invoke(note, true); }
                else if (note.Started && seconds > note.EndSeconds + 0.13)
                { note.Done = true; activeLongs.Remove(note); Judge(4, seconds); }
            }
            else if (ms > 130) { note.Done = true; Judge(4, seconds); }
        }
    }

    public static int Grade(double milliseconds)
    {
        double ms = Math.Abs(milliseconds);
        return ms <= 50 ? 0 : ms <= 80 ? 1 : ms <= 100 ? 2 : ms <= 120 ? 3 : -1;
    }

    private static bool AnyHeld(MobileNote note, IReadOnlySet<int> held)
    {
        foreach (int lane in held) if (lane >= note.Lane && (long)lane < (long)note.Lane + note.LaneSpan) return true;
        return false;
    }

    private void Judge(int grade, double seconds, double offset = 0)
    {
        if(Life?.Failed==true)return;
        if (grade < 4 && double.IsFinite(offset)) TimingOffsets.Add(Math.Clamp(offset, -120, 120));
        Counts[grade]++;
        LastJudgement = AutoPlay && grade == 0 ? "AUTO" : new[] { "PERFECT", "GREAT", "GOOD", "BAD", "MISS" }[grade];
        JudgeUntilSeconds = seconds + 0.35;
        if (grade <= 2) { Combo++; MaxCombo = Math.Max(MaxCombo, Combo); }
        else Combo = 0;
        Life?.Judge(grade,Combo);
        int total = scoreUnits;
        Score = (int)Math.Round((Counts[0] + Counts[1] * 0.75 + Counts[2] * 0.5 + Counts[3] * 0.25)
            / total * 1_000_000);
        double delta = grade switch { 0 => 1, 1 => .5, 2 => .25, _ => -2 };
        Gauge = Math.Clamp(Gauge + 400.0 / 3 / total * delta, 0, 100);
        if(globalUnits>0)GlobalGauge=Math.Clamp(GlobalGauge+400.0/3/globalUnits*delta,0,100);
        if (Gauge >= 100 - 1e-9) Gauge = 100;
    }
}
