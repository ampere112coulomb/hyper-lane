using HyperLane;
namespace HyperLane.Android;

// Shared by Android and iOS. Only app-private progress is written here.
internal sealed class MobileBadges
{
    private readonly string path;
    internal BadgeCollection Collection {get;}
    internal MobileBadges(string directory){path=Path.Combine(directory,"badges.json");Collection=BadgeCollection.Load(path);}
    internal static string Key(MobileSong song,MobileDifficulty diff)=>Path.GetFullPath(song.ChartPath)+"\n"+diff.Name;
    internal BadgeAward? Award(MobileSession session,bool eligible)
    {
        if(!eligible||session.Difficulty.Badge is not {} spec||!spec.Matches(session.Score,session.Counts,session.MaxCombo))return null;
        if(ImagePath(session.Song,spec.Image) is not {} image||!File.Exists(image))return null;
        var award=new BadgeAward{Id=Key(session.Song,session.Difficulty),Image=spec.Image,Label=Path.GetFileNameWithoutExtension(spec.Image)};
        if(!Collection.Unlock(award))return null;
        Collection.Save(path);return award;
    }
    internal void Equip(string id){if(id.Length==0)Collection.Equipped="";else if(!Collection.Equip(id))return;Collection.Save(path);}
    internal static string? ImagePath(MobileSong song,string relative)
    {
        if(!BadgeDefinition.SafeRelativeImage(relative))return null;
        string root=Path.GetFullPath(song.Folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        string file=Path.GetFullPath(Path.Combine(root,relative.Replace('\\','/')));
        return file.StartsWith(root,StringComparison.Ordinal)?file:null;
    }
    internal string? Resolve(BadgeAward? award,IEnumerable<MobileSong> songs)
    {
        if(award==null)return null;
        foreach(var song in songs)foreach(var diff in song.Chart.Difficulties)if(Key(song,diff)==award.Id)return ImagePath(song,award.Image);
        return null;
    }
    internal BadgeAward? Equipped=>Collection.Awards.FirstOrDefault(a=>a.Id==Collection.Equipped);
}
