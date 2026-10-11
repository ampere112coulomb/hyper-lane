using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HyperLane;

internal sealed class DanDefinition
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public bool CarryGauge { get; set; } = true;
    public double InitialGauge { get; set; }
    public List<DanTrack> Tracks { get; set; } = new();
    public DanConditions Conditions { get; set; } = new();
    internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public static DanDefinition Load(string path)
    {
        if(new FileInfo(path).Length > 1024*1024) throw new InvalidDataException("Dan.jsonが大きすぎます");
        var value = JsonSerializer.Deserialize<DanDefinition>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("Dan.jsonが空です");
        if(value.Version != 1 || string.IsNullOrWhiteSpace(value.Title) || value.Tracks == null || value.Tracks.Count == 0 || value.Tracks.Any(t=>t==null || string.IsNullOrWhiteSpace(t.Chart) || string.IsNullOrWhiteSpace(t.Difficulty))) throw new InvalidDataException("段位名・課題曲・難易度を確認してください");
        if(!double.IsFinite(value.InitialGauge) || value.InitialGauge<0 || value.InitialGauge>100) throw new InvalidDataException("初期ゲージは0～100です");
        value.Conditions?.Validate();
        foreach(var track in value.Tracks) track.Conditions?.Validate();
        if(value.Conditions==null || !(value.Conditions.Descriptions().Any() || value.Tracks.Any(t=>t.Conditions?.Descriptions().Any()==true))) throw new InvalidDataException("合格条件が必要です");
        if(string.IsNullOrWhiteSpace(value.Id)) value.Id=Path.GetFileName(Path.GetDirectoryName(path))!;
        return value;
    }
    public static string TrackPath(string folder,string relative)
    {
        string root=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        string path=Path.GetFullPath(Path.Combine(root,relative));
        if(Path.IsPathRooted(relative) || !path.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("課題曲は段位フォルダ内の相対パスで指定してください");
        return path;
    }
}
internal sealed class DanTrack
{
    public string Chart { get; set; } = "";
    public string Difficulty { get; set; } = "Master";
    public bool ShowTitle { get; set; } = true;
    public DanConditions? Conditions { get; set; }
}
internal sealed class DanConditions
{
    public double? GaugeMin { get; set; }
    public string GaugeScope { get; set; } = "final";
    public int? BadMissMaxExclusive { get; set; }
    public int? BadMax { get; set; }
    public int? MissMax { get; set; }
    public int? PerfectMin { get; set; }
    public void Validate()
    {
        if(GaugeMin.HasValue && (!double.IsFinite(GaugeMin.Value)||GaugeMin<0||GaugeMin>100)) throw new InvalidDataException("ゲージ条件は0～100です");
        if(GaugeScope is not ("final" or "everySong")) throw new InvalidDataException("gaugeScopeはfinalまたはeverySongです");
        if(BadMissMaxExclusive<0 || BadMax<0 || MissMax<0 || PerfectMin<0) throw new InvalidDataException("判定数条件は0以上です");
    }
    public IEnumerable<string> Descriptions()
    {
        if(GaugeMin.HasValue) yield return $"{(GaugeScope=="everySong"?"各曲終了時":"最終")}ゲージ {GaugeMin:0.##}% 以上";
        if(BadMissMaxExclusive.HasValue) yield return $"BAD＋MISS 合計 {BadMissMaxExclusive} 未満";
        if(BadMax.HasValue) yield return $"BAD 合計 {BadMax} 以下";
        if(MissMax.HasValue) yield return $"MISS 合計 {MissMax} 以下";
        if(PerfectMin.HasValue) yield return $"PERFECT 合計 {PerfectMin} 以上";
    }
    public List<(string Label,bool Passed)> Evaluate(int[] counts,IReadOnlyList<double> gauges)
    {
        var result=new List<(string,bool)>();
        if(GaugeMin.HasValue){double actual=gauges.Count==0?0:GaugeScope=="everySong"?gauges.Min():gauges[^1];result.Add(($"ゲージ {actual:0.00}% / {GaugeMin:0.##}% 以上",gauges.Count>0&&actual>=GaugeMin));}
        if(BadMissMaxExclusive.HasValue) result.Add(($"BAD＋MISS {counts[3]+counts[4]} / {BadMissMaxExclusive} 未満",counts[3]+counts[4]<BadMissMaxExclusive));
        if(BadMax.HasValue) result.Add(($"BAD {counts[3]} / {BadMax} 以下",counts[3]<=BadMax));
        if(MissMax.HasValue) result.Add(($"MISS {counts[4]} / {MissMax} 以下",counts[4]<=MissMax));
        if(PerfectMin.HasValue) result.Add(($"PERFECT {counts[0]} / {PerfectMin} 以上",counts[0]>=PerfectMin));
        return result;
    }
}
