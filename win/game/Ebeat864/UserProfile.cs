using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
namespace HyperLane;
internal sealed class UserProfile
{
    public int Version {get;set;}=1;
    public string UserName {get;set;}="";
    internal static bool ValidName(string? name){if(string.IsNullOrWhiteSpace(name)||new StringInfo(name.Trim()).LengthInTextElements is <1 or >10)return false;foreach(char c in name)if(char.IsControl(c))return false;return true;}
    internal static UserProfile Load(string path){try{var p=JsonSerializer.Deserialize<UserProfile>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});if(p?.Version==1&&ValidName(p.UserName)){p.UserName=p.UserName.Trim();return p;}}catch{}return new();}
    internal void Save(string path){if(!ValidName(UserName))throw new ArgumentException("1～10文字で入力してください。");UserName=UserName.Trim();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);string temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(this,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true}));File.Move(temp,path,true);}
}
