using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace HyperLane;
internal sealed class BadgeCondition
{
    public string Metric {get;set;}="rank";
    public string Op {get;set;}=">=";
    public int Value {get;set;}
    internal bool Valid => (Op is ">=" or "<=") && Value>=0 && (Metric=="rank" ? Value<=7 : (Metric is "score" or "perfect" or "great" or "good" or "bad" or "miss" or "combo") && Value<=1000000);
}
internal sealed class BadgeDefinition
{
    public string Image {get;set;}="";
    public List<BadgeCondition> Conditions {get;set;}=new();
    internal static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    internal bool Valid => SafeRelativeImage(Image) && Conditions is {Count:>0 and <=32} && Conditions.All(c=>c!=null&&c.Valid);
    internal static bool SafeRelativeImage(string? image) => !string.IsNullOrWhiteSpace(image) && !Path.IsPathRooted(image) && !image.Contains(':') && !image.Any(char.IsControl) && !image.Replace('\\','/').Split('/').Any(s=>s is ".." or "." or "") && new[]{".png",".jpg",".jpeg",".bmp",".gif"}.Contains(Path.GetExtension(image).ToLowerInvariant());
    internal static BadgeDefinition? Parse(string text){try{var badge=JsonSerializer.Deserialize<BadgeDefinition>(text,JsonOptions);return badge?.Valid==true?badge:null;}catch(JsonException){return null;}}
    internal static int Rank(int score)=>score>=1000000?7:score>=995000?6:score>=990000?5:score>=950000?4:score>=900000?3:score>=860000?2:score>=820000?1:score>=780000?0:-1;
    internal bool Matches(int score,int[] counts,int maxCombo) => Valid && counts is {Length:5} && counts.All(n=>n>=0) && counts.Sum()>0 && Conditions.All(c=>{int actual=c.Metric switch{"rank"=>Rank(score),"score"=>score,"perfect"=>counts[0],"great"=>counts[1],"good"=>counts[2],"bad"=>counts[3],"miss"=>counts[4],"combo"=>maxCombo,_=>-1};return c.Op==">="?actual>=c.Value:actual<=c.Value;});
}
internal sealed class BadgeAward
{
    public string Id {get;set;}="";
    public string Image {get;set;}="";
    public string Label {get;set;}="";
}
internal sealed class BadgeCollection
{
    public List<BadgeAward> Awards {get;set;}=new();
    public string Equipped {get;set;}="";
    internal static BadgeCollection Load(string path){try{var data=JsonSerializer.Deserialize<BadgeCollection>(File.ReadAllText(path));if(data?.Awards!=null){data.Awards=data.Awards.Where(a=>a!=null&&!string.IsNullOrWhiteSpace(a.Id)&&BadgeDefinition.SafeRelativeImage(a.Image)).DistinctBy(a=>a.Id).ToList();return data;}}catch(Exception){}return new();}
    internal bool Unlock(BadgeAward award){if(Awards.Any(a=>a.Id==award.Id))return false;Awards.Add(award);return true;}
    internal bool Equip(string id){if(!Awards.Any(a=>a.Id==id))return false;Equipped=id;return true;}
    internal void Save(string path){Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,path,true);}
}
