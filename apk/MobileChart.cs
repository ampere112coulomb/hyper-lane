using System.Globalization;
using System.Text.RegularExpressions;

namespace HyperLane.Android;

internal sealed class MobileChart
{
    public string Title { get; set; } = "Untitled";
    public string Composer { get; set; } = "";
    public string ChartDesigner { get; set; } = "";
    public string Audio { get; set; } = "";
    public string AudioFor(MobileDifficulty? diff)=>string.IsNullOrWhiteSpace(diff?.Audio)?Audio:diff.Audio;
    public string DesignerFor(MobileDifficulty? diff)=>string.IsNullOrWhiteSpace(diff?.ChartDesigner)?ChartDesigner:diff.ChartDesigner;
    public double OffsetFor(MobileDifficulty? diff)=>diff?.AudioOffset??Offset;
    public double PreviewStartFor(MobileDifficulty? diff)=>diff?.PreviewStartBeat??PreviewStartBeat;
    public double PreviewEndFor(MobileDifficulty? diff)=>diff?.PreviewEndBeat??PreviewEndBeat;
    public string DifficultyAudioPath(string folder,MobileDifficulty diff){try{string root=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;string path=Path.GetFullPath(Path.Combine(root,diff.Audio.Replace('\\','/')));return path.StartsWith(root,StringComparison.Ordinal)?path:"";}catch{return "";}}
    public string Jacket { get; set; } = "";
    public string Background { get; set; } = "";
    public string Video { get; set; } = "";
    public double Bpm { get; set; } = 120;
    public double Offset { get; set; }
    public double MeasureLength { get; set; } = 4;
    public double PreviewStartBeat { get; set; }
    public double PreviewEndBeat { get; set; } = 16;
    public int Lanes { get; set; } = 6;
    public List<MobileDifficulty> Difficulties { get; } = new();

    public static MobileChart Parse(string text)
    {
        var chart = new MobileChart();
        MobileDifficulty? difficulty = null;
        int activeLayer = 1;
        foreach (string raw in text.Replace("\r", "").Split('\n'))
        {
            string line = raw.Split("//", 2)[0].Trim();
            if (line.Length == 0) continue;
            Match field = Regex.Match(line, "^([A-Z_]+)\\s*:\\s*(.*)$");
            if (field.Success)
            {
                string key = field.Groups[1].Value;
                string value = field.Groups[2].Value.Trim();
                switch (key)
                {
                    case "TITLE": chart.Title = value; break;
                    case "COMPOSER": chart.Composer = value; break;
                    case "CHARTER": case "CHARTDESIGNER": case "CHART_DESIGNER": chart.ChartDesigner = value; break;
                    case "SONG": case "AUDIO": chart.Audio = value; break;
                    case "DIFFICULTY_CHART_DESIGNER": if(difficulty!=null)difficulty.ChartDesigner=value;break;
                    case "DIFFICULTY_SONG": if(difficulty!=null)difficulty.Audio=value;break;
                    case "DIFFICULTY_OFFSET": if(difficulty!=null&&Number(value,out double audioOffset)&&double.IsFinite(audioOffset))difficulty.AudioOffset=audioOffset;break;
                    case "DIFFICULTY_PREVIEW":
                        var range=value.Split('-',2,StringSplitOptions.TrimEntries);
                        if(difficulty!=null&&range.Length==2&&Number(range[0],out double from)&&Number(range[1],out double to)&&double.IsFinite(from)&&double.IsFinite(to)&&from>=0&&to>from){difficulty.PreviewStartBeat=from;difficulty.PreviewEndBeat=to;}break;
                    case "JACKET": case "COVER": chart.Jacket = value; break;
                    case "BACKGROUND": case "BG": chart.Background = value; break;
                    case "MOVIE": case "VIDEO": case "MV": chart.Video = value; break;
                    case "BPM": if (Number(value, out double bpm) && bpm > 0) chart.Bpm = bpm; break;
                    case "OFFSET": if (Number(value, out double offset)) chart.Offset = offset; break;
                    case "TIMESIG": if (TimeSignature(value, out double measure)) chart.MeasureLength = measure; break;
                    case "PREVIEW":
                        string[] preview = value.Split('-', 2, StringSplitOptions.TrimEntries);
                        if (preview.Length == 2 && Number(preview[0], out double previewStart) &&
                            Number(preview[1], out double previewEnd) && previewStart >= 0 && previewEnd > previewStart)
                        { chart.PreviewStartBeat = previewStart; chart.PreviewEndBeat = previewEnd; }
                        break;
                    case "LANES":
                        if (int.TryParse(value, out int lanes))
                        {
                            if (difficulty is null) chart.Lanes = Math.Clamp(lanes, 3, 12);
                            else difficulty.Lanes = Math.Clamp(lanes, 3, 12);
                        }
                        break;
                    case "DIFFICULTY":
                        activeLayer = 1;
                        difficulty = new MobileDifficulty { Name = value, Lanes = chart.Lanes };
                        chart.Difficulties.Add(difficulty);
                        break;
                    case "LAYER":
                        if (difficulty is not null && int.TryParse(value, out int number) && number >= 1)
                        {
                            activeLayer = number;
                            if (number > 1 && !difficulty.Layers.ContainsKey(number))
                                difficulty.Layers[number] = new MobileDifficulty { Name = difficulty.Name, Lanes = difficulty.Lanes };
                        }
                        break;
                    case "LEVEL": if (difficulty is not null) difficulty.Level = value; break;
                    case "LEVEL_DISPLAY": if (difficulty is not null) difficulty.LevelLabel = HyperLane.ChartLevel.NormalizeLabel(value); break;
                    case "FEVER":
                        if (difficulty is not null)
                        {
                            string[] fever = value.Split('-', 2, StringSplitOptions.TrimEntries);
                            if (fever.Length == 2 && Number(fever[0], out double feverStart) &&
                                Number(fever[1], out double feverEnd) && feverEnd > feverStart)
                                difficulty.FeverSections.Add((feverStart, feverEnd));
                        }
                        break;
                    case "CHANGE":
                        if (difficulty is not null)
                        {
                            string[] parts = value.Split(',', 3, StringSplitOptions.TrimEntries);
                            if (parts.Length == 3 && Number(parts[0], out double beat))
                            {
                                MobileDifficulty timing = activeLayer == 1 ? difficulty : difficulty.Layers[activeLayer];
                                if (parts[1].Equals("BPM", StringComparison.OrdinalIgnoreCase) && Number(parts[2], out double nextBpm) && nextBpm > 0)
                                    timing.BpmChanges.Add((beat, nextBpm));
                                else if (parts[1].Equals("BS", StringComparison.OrdinalIgnoreCase) && Number(parts[2], out double bs))
                                    timing.BsChanges.Add((beat, bs));
                                else if (parts[1].Equals("SIG", StringComparison.OrdinalIgnoreCase) && TimeSignature(parts[2], out double sig))
                                    timing.SigChanges.Add((beat, sig));
                                else if (parts[1].Equals("NS", StringComparison.OrdinalIgnoreCase))
                                {
                                    string[] spec = parts[2].Split(',', StringSplitOptions.TrimEntries);
                                    if (spec.Length is 1 or 3 && Number(spec[0], out double ns) && ns >= 0)
                                    {
                                        if (spec.Length == 1) timing.NsChanges.Add(new MobileNsChange(beat, ns, 1, 0));
                                        else if (int.TryParse(spec[1], out int mode) && mode is >= 1 and <= 3 &&
                                                 Number(spec[2], out double duration) && duration is >= 0 and <= 32)
                                            timing.NsChanges.Add(new MobileNsChange(beat, ns, mode, duration));
                                    }
                                }
                                else if (activeLayer != 1) continue;
                                else if (parts[1].Equals("JUDGE_LINE", StringComparison.OrdinalIgnoreCase) &&
                                    HyperLane.JudgementLineMotion.TryParse(beat, parts[2], out var lineChange))
                                    difficulty.JudgementLineChanges.Add(lineChange);
                                else if (parts[1].Equals("CAMERA", StringComparison.OrdinalIgnoreCase) &&
                                         CameraSpec(parts[2], out MobileCameraChange camera))
                                    difficulty.CameraChanges.Add(camera with { Beat = beat });
                                else if (parts[1].Equals("CAMERA_ADV", StringComparison.OrdinalIgnoreCase) &&
                                         AdvancedCameraSpec(parts[2], out MobileCameraChange advancedCamera))
                                    difficulty.CameraChanges.Add(advancedCamera with { Beat = beat });
                                else if (parts[1].Equals("LANE_VIS", StringComparison.OrdinalIgnoreCase) ||
                                         parts[1].Equals("UI_VIS", StringComparison.OrdinalIgnoreCase))
                                {
                                    string[] spec = parts[2].Split(',', 2, StringSplitOptions.TrimEntries);
                                    if (Number(spec[0], out double opacity) && opacity is >= 0 and <= 100)
                                    {
                                        double duration = spec.Length > 1 && Number(spec[1], out double parsedDuration) ? parsedDuration : 0;
                                        if (duration is >= 0 and <= 32)
                                        {
                                            var change = new MobileVisibilityChange(beat, opacity / 100, duration);
                                            if (parts[1].Equals("LANE_VIS", StringComparison.OrdinalIgnoreCase)) difficulty.LaneVisibilityChanges.Add(change);
                                            else difficulty.UiVisibilityChanges.Add(change);
                                        }
                                    }
                                }
                                else if (parts[1].Equals("GLITCH", StringComparison.OrdinalIgnoreCase))
                                {
                                    string[] spec = parts[2].Split(',', 2, StringSplitOptions.TrimEntries);
                                    if (Number(spec[0], out double strength) && strength is >= 0 and <= 100)
                                    {
                                        double duration = spec.Length > 1 && Number(spec[1], out double parsedDuration) ? parsedDuration : double.PositiveInfinity;
                                        if (duration >= 0) difficulty.FilterChanges.Add(new MobileFilterChange(beat, "GLITCH", strength, duration, false));
                                    }
                                }
                                else if (parts[1].Equals("FILTER", StringComparison.OrdinalIgnoreCase))
                                {
                                    string[] spec = parts[2].Split(',', StringSplitOptions.TrimEntries);
                                    if (spec.Length is >= 3 and <= 5 &&
                                        spec[0].ToUpperInvariant() is "GLITCH" or "MONO" or "INVERT" or "BLOOM" or "SCANLINE" or "MOSAIC" or "BRIGHTNESS" &&
                                        Number(spec[1], out double strength) && double.IsFinite(strength) && strength >= (spec[0].Equals("BRIGHTNESS", StringComparison.OrdinalIgnoreCase) ? -100 : 0) && strength <= 100 &&
                                        Number(spec[2], out double duration) && double.IsFinite(duration) && duration >= 0 &&
                                        (spec.Length == 3 || spec[3].Equals("RAMP", StringComparison.OrdinalIgnoreCase) ||
                                         spec[3].Equals("INSTANT", StringComparison.OrdinalIgnoreCase)))
                                        difficulty.FilterChanges.Add(new MobileFilterChange(beat, spec[0].ToUpperInvariant(), strength, duration,
                                            spec.Length >= 4 && spec[3].Equals("RAMP", StringComparison.OrdinalIgnoreCase),
                                            spec.Length == 5 && spec[4].Equals("BACKGROUND", StringComparison.OrdinalIgnoreCase)));
                                }
                            }
                        }
                        break;
                }
                continue;
            }
            if (difficulty is null) continue;
            Match noteMatch = Regex.Match(line, "^([0-9.]+)([HDMW]?)(?:-([0-9.]+)([GH]?))?\\s*,\\s*(-?[0-9]+)(?:\\s*,\\s*(.*))?$");
            if (!noteMatch.Success || !Number(noteMatch.Groups[1].Value, out double beatStart) ||
                !int.TryParse(noteMatch.Groups[5].Value, out int lane)) continue;
            double beatEnd = beatStart;
            if (noteMatch.Groups[3].Success && !Number(noteMatch.Groups[3].Value, out beatEnd)) continue;
            char kind = noteMatch.Groups[3].Success
                ? (noteMatch.Groups[4].Value == "G" ? 'G' : 'L')
                : (noteMatch.Groups[2].Value.Length == 1 ? noteMatch.Groups[2].Value[0] : 'N');
            double ss = 1;
            int laneSpan = 1;
            string? color = null;
            double guideOpacity = 1;
            string guideMode = "solid";
            foreach (string extra in noteMatch.Groups[6].Value.Split(',', StringSplitOptions.TrimEntries))
            {
                if (extra.StartsWith("SS=", StringComparison.OrdinalIgnoreCase) && Number(extra[3..], out double parsedSs))
                    ss = parsedSs == 0 ? .01 : Math.Sign(parsedSs) * Math.Clamp(Math.Abs(parsedSs), .01, 1000);
                else if (kind is 'M' or 'W' && extra.StartsWith("SIZE=", StringComparison.OrdinalIgnoreCase) && int.TryParse(extra[5..], out int size) && size >= 2)
                    laneSpan = size;
                else if (extra.StartsWith('#') && Regex.IsMatch(extra, "^#[0-9a-fA-F]{6}$")) color = extra;
                else if (kind == 'G' && extra is "solid" or "fade" or "rise") guideMode = extra;
                else if (kind == 'G' && Number(extra, out double opacity)) guideOpacity = Math.Clamp(opacity, 0, 1);
            }
            if (kind is 'M' or 'W' ? (laneSpan < 2 || lane > difficulty.Lanes || (long)lane + laneSpan - 1 < 1)
                : lane < 1 || lane > difficulty.Lanes) continue;
            difficulty.Notes.Add(new MobileNote(beatStart, beatEnd, lane, kind, ss, noteMatch.Groups[4].Value == "H",
                color, guideOpacity, guideMode, laneSpan) { Layer = activeLayer });
        }
        foreach (MobileDifficulty diff in chart.Difficulties)
        {
            foreach (MobileDifficulty layer in diff.Layers.Values)
            {
                layer.BpmChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
                layer.BsChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
                layer.NsChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
                layer.SigChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
                layer.Timing = new MobileTiming(layer, chart.Bpm);
            }
            diff.BpmChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            diff.BsChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            diff.NsChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            diff.SigChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            diff.LaneVisibilityChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            diff.UiVisibilityChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            diff.FilterChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            diff.CameraChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            diff.JudgementLineChanges.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            diff.Timing = new MobileTiming(diff, chart.Bpm);
            diff.Notes.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            foreach (MobileNote note in diff.Notes)
            {
                MobileDifficulty timing = note.Layer == 1 ? diff : diff.Layers[note.Layer];
                note.StartSeconds = chart.OffsetFor(diff) + BeatSeconds(timing, chart.Bpm, note.Beat);
                note.EndSeconds = chart.OffsetFor(diff) + BeatSeconds(timing, chart.Bpm, note.EndBeat);
                note.ScrollStart = ScrollPosition(timing, chart.Bpm, note.Beat);
                note.ScrollEnd = ScrollPosition(timing, chart.Bpm, note.EndBeat);
            }
            diff.Notes.Sort((a, b) => a.StartSeconds.CompareTo(b.StartSeconds));
            diff.NotesByLayer = diff.Notes.GroupBy(n => n.Layer).ToDictionary(g => g.Key, g => g.ToArray());
            double barBeat = 0, length = chart.MeasureLength;
            int signatureIndex = 0;
            double lastBeat = diff.Notes.Where(n => n.Layer == 1).Select(n => n.EndBeat).DefaultIfEmpty(0).Max() + 64;
            for (int count = 0; count < 100000 && barBeat <= lastBeat; count++)
            {
                while (signatureIndex < diff.SigChanges.Count && diff.SigChanges[signatureIndex].Beat <= barBeat + 1e-9)
                    length = diff.SigChanges[signatureIndex++].Length;
                diff.MeasureScrollPositions.Add(diff.Timing.Scroll(barBeat));
                double next = barBeat + length;
                if (signatureIndex < diff.SigChanges.Count && diff.SigChanges[signatureIndex].Beat < next) next = diff.SigChanges[signatureIndex].Beat;
                if (next <= barBeat) break;
                barBeat = next;
            }
            diff.MeasureScrollPositions.Sort();
        }
        return chart;
    }

    private static bool Number(string text, out double result) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && double.IsFinite(result);

    private static bool TimeSignature(string text, out double length)
    {
        length = 4;
        string[] values = text.Split('/', 2, StringSplitOptions.TrimEntries);
        if (values.Length != 2 || !Number(values[0], out double numerator) ||
            !Number(values[1], out double denominator) || numerator <= 0 || denominator <= 0) return false;
        length = numerator * 4 / denominator;
        return double.IsFinite(length) && length > 0;
    }

    private static bool CameraSpec(string text, out MobileCameraChange change)
    {
        change = default;
        Match match = Regex.Match(text, @"^\s*\(\s*([^,]+)\s*,\s*([^,]+)\s*,\s*([^\)]+)\s*\)\s*([123])(?:\s*,\s*([^,]+))?\s*$", RegexOptions.CultureInvariant);
        if (!match.Success || !Number(match.Groups[1].Value, out double x) ||
            !Number(match.Groups[2].Value, out double y) || !Number(match.Groups[3].Value, out double z)) return false;
        double duration = 1;
        if (match.Groups[5].Success && !Number(match.Groups[5].Value, out duration)) return false;
        change = new MobileCameraChange(0, Math.Clamp(x, -4, 4), Math.Clamp(y, .5, 8),
            Math.Clamp(z, -20, -1), 0, 0, 0, Math.Clamp(duration, 0, 32),
            int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture), false);
        return true;
    }

    private static bool AdvancedCameraSpec(string text, out MobileCameraChange change)
    {
        change = default;
        Match match = Regex.Match(text, @"^\s*\(\s*([^,]+),\s*([^,]+),\s*([^\)]+)\)\s*,\s*\(\s*([^,]+),\s*([^,]+),\s*([^\)]+)\)\s*([123])(?:\s*,\s*([^,]+))?\s*$", RegexOptions.CultureInvariant);
        if (!match.Success || !Number(match.Groups[1].Value, out double x) ||
            !Number(match.Groups[2].Value, out double y) || !Number(match.Groups[3].Value, out double z) ||
            !Number(match.Groups[4].Value, out double rx) || !Number(match.Groups[5].Value, out double ry) ||
            !Number(match.Groups[6].Value, out double rz)) return false;
        double duration = 1;
        if (match.Groups[8].Success && !Number(match.Groups[8].Value, out duration)) return false;
        change = new MobileCameraChange(0, Math.Clamp(x, -20, 20), Math.Clamp(y, -20, 20),
            Math.Clamp(z, -50, 20), rx, ry, rz, Math.Clamp(duration, 0, 32),
            int.Parse(match.Groups[7].Value, CultureInfo.InvariantCulture), true);
        return true;
    }

    public static double BeatSeconds(MobileDifficulty diff, double initialBpm, double beat)
    {
        if (diff.Timing is not null) return diff.Timing.Seconds(beat);
        double seconds = 0, previousBeat = 0, bpm = initialBpm;
        foreach (var change in diff.BpmChanges)
        {
            if (change.Beat <= 0) { bpm = change.Value; continue; }
            if (change.Beat >= beat) break;
            seconds += (change.Beat - previousBeat) * 60 / bpm;
            previousBeat = change.Beat;
            bpm = change.Value;
        }
        return seconds + (beat - previousBeat) * 60 / bpm;
    }

    public static double BeatAtSeconds(MobileDifficulty diff, double initialBpm, double seconds)
    {
        if (diff.Timing is not null) return diff.Timing.Beat(seconds);
        double beat = 0, bpm = initialBpm;
        foreach (var change in diff.BpmChanges)
        {
            if (change.Beat <= 0) { bpm = change.Value; continue; }
            double segmentSeconds = (change.Beat - beat) * 60 / bpm;
            if (seconds < segmentSeconds) break;
            seconds -= segmentSeconds;
            beat = change.Beat;
            bpm = change.Value;
        }
        return beat + seconds * bpm / 60;
    }

    public static double ScrollPosition(MobileDifficulty diff, double initialBpm, double beat)
    {
        if (diff.Timing is not null) return diff.Timing.Scroll(beat);
        double scroll = 0, previousBeat = 0, bs = 1;
        foreach (var change in diff.BsChanges)
        {
            if (change.Beat <= 0) { bs = change.Value; continue; }
            if (change.Beat >= beat) break;
            scroll += (BeatSeconds(diff, initialBpm, change.Beat) - BeatSeconds(diff, initialBpm, previousBeat)) * 2 * bs;
            previousBeat = change.Beat;
            bs = change.Value;
        }
        return scroll + (BeatSeconds(diff, initialBpm, beat) - BeatSeconds(diff, initialBpm, previousBeat)) * 2 * bs;
    }

    public static double NsMultiplierAt(MobileDifficulty diff, double beat)
    {
        double from = 1, to = 1, start = 0, duration = 0;
        int mode = 1;
        foreach (MobileNsChange change in diff.NsChanges)
        {
            if (change.Beat > beat) break;
            from = NsAt(from, to, start, duration, mode, change.Beat);
            to = change.Value;
            start = change.Beat;
            duration = change.Duration;
            mode = change.Mode;
            if (duration <= 0) from = to;
        }
        return NsAt(from, to, start, duration, mode, beat);
    }

    private static double NsAt(double from, double to, double start, double duration, int mode, double beat)
    {
        if (duration <= 0) return to;
        double t = Math.Clamp((beat - start) / duration, 0, 1);
        t = mode switch { 2 => t * t * t, 3 => 1 - Math.Pow(1 - t, 3), _ => t };
        return from + (to - from) * t;
    }
}

internal sealed class MobileDifficulty
{
    public string ChartDesigner { get; set; } = "";
    public string Audio { get; set; } = "";
    public double? AudioOffset { get; set; }
    public double? PreviewStartBeat { get; set; }
    public double? PreviewEndBeat { get; set; }
    public MobileTiming? Timing { get; set; }
    public Dictionary<int, MobileNote[]> NotesByLayer { get; set; } = new();
    public List<double> MeasureScrollPositions { get; } = new();
    public Dictionary<int, MobileDifficulty> Layers { get; } = new();
    public List<HyperLane.JudgementLineChange> JudgementLineChanges { get; } = new();
    public string Name { get; set; } = "Normal";
    public string Level { get; set; } = "-";
    public string LevelLabel { get; set; } = "";
    public string DisplayLevel => string.IsNullOrWhiteSpace(LevelLabel) ? HyperLane.ChartLevel.Display(Level) : HyperLane.ChartLevel.NormalizeLabel(LevelLabel);
    public int Lanes { get; set; } = 6;
    public List<MobileNote> Notes { get; } = new();
    public List<(double Beat, double Value)> BpmChanges { get; } = new();
    public List<(double Beat, double Value)> BsChanges { get; } = new();
    public List<MobileNsChange> NsChanges { get; } = new();
    public List<(double Beat, double Length)> SigChanges { get; } = new();
    public List<MobileVisibilityChange> LaneVisibilityChanges { get; } = new();
    public List<MobileVisibilityChange> UiVisibilityChanges { get; } = new();
    public List<MobileFilterChange> FilterChanges { get; } = new();
    public List<MobileCameraChange> CameraChanges { get; } = new();
    public List<(double Start, double End)> FeverSections { get; } = new();
}

internal readonly record struct MobileNsChange(double Beat, double Value, int Mode, double Duration);
internal readonly record struct MobileVisibilityChange(double Beat, double Opacity, double Duration);
internal readonly record struct MobileFilterChange(double Beat, string Kind, double Strength, double Duration, bool Ramp, bool BackgroundOnly = false);
internal readonly record struct MobileCameraChange(double Beat, double X, double Y, double Z,
    double Rx, double Ry, double Rz, double Duration, int Mode, bool Advanced);

internal sealed class MobileNote
{
    public int Layer { get; init; } = 1;
    public MobileNote(double beat, double endBeat, int lane, char kind, double ss, bool holdEnd,
        string? customColor = null, double guideOpacity = 1, string guideOpacityMode = "solid", int laneSpan = 1)
    {
        Beat = beat; EndBeat = endBeat; Lane = lane; Kind = kind; Ss = ss; HoldEnd = holdEnd;
        CustomColor = customColor; GuideOpacity = guideOpacity; GuideOpacityMode = guideOpacityMode; LaneSpan = laneSpan;
    }
    public double Beat { get; }
    public double EndBeat { get; }
    public int Lane { get; }
    public int LaneSpan { get; }
    public char Kind { get; }
    public double Ss { get; }
    public bool HoldEnd { get; }
    public string? CustomColor { get; }
    public double GuideOpacity { get; }
    public string GuideOpacityMode { get; }
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public double ScrollStart { get; set; }
    public double ScrollEnd { get; set; }
    public bool Started { get; set; }
    public bool Done { get; set; }
}
