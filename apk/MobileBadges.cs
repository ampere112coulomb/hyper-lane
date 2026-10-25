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
        return AwardAll(session,eligible).FirstOrDefault();
    }
    internal List<BadgeAward> AwardAll(MobileSession session,bool eligible)
    {
        var result=new List<BadgeAward>();if(!eligible)return result;
        var specs=session.Difficulty.Badges.Count>0?session.Difficulty.Badges:session.Difficulty.Badge is {} single?new(){single}:new List<BadgeDefinition>();
        // The old mobile parser retained only the final BADGE row under the legacy key.
        // Move that earned image to its matching definition before freeing the first badge's key.
        bool migrated=false;string chartKey=Key(session.Song,session.Difficulty);
        if(Collection.Awards.FirstOrDefault(a=>a.Id==chartKey) is {} legacy){int index=specs.FindIndex(b=>b.Image.Equals(legacy.Image,StringComparison.OrdinalIgnoreCase));if(index>=0){var spec=specs[index];string target=spec.AwardKey(chartKey,index);if(target!=chartKey){if(Collection.Awards.Any(a=>a.Id==target))Collection.Awards.Remove(legacy);else legacy.Id=target;if(Collection.Equipped==chartKey)Collection.Equipped=target;legacy.SongTitle=session.Song.Chart.Title;legacy.DifficultyName=session.Difficulty.Name;legacy.Conditions=spec.Conditions;migrated=true;}}}
        for(int i=0;i<specs.Count;i++){var spec=specs[i];if(!spec.Matches(session.Score,session.Counts,session.MaxCombo)||ImagePath(session.Song,spec.Image) is not {} image||!File.Exists(image))continue;
            var award=new BadgeAward{Id=spec.AwardKey(Key(session.Song,session.Difficulty),i),Image=spec.Image,Label=Path.GetFileNameWithoutExtension(spec.Image),SongTitle=session.Song.Chart.Title,DifficultyName=session.Difficulty.Name,Conditions=spec.Conditions};if(Collection.Unlock(award))result.Add(award);}
        if(result.Count>0||migrated)Collection.Save(path);return result;
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
        foreach(var song in songs)foreach(var diff in song.Chart.Difficulties)if(Key(song,diff)==award.Id||award.Id.StartsWith(Key(song,diff)+"|BADGE:",StringComparison.Ordinal))return ImagePath(song,award.Image);
        return null;
    }
    internal BadgeAward? Equipped=>Collection.Awards.FirstOrDefault(a=>a.Id==Collection.Equipped);
    internal static string Requirements(BadgeAward? award,IEnumerable<MobileSong> songs)
    {
        if(award==null)return "";if(award.Conditions.Count>0)return BadgeRequirementText.Format(award.SongTitle,award.DifficultyName,award.Conditions);
        foreach(var song in songs)foreach(var diff in song.Chart.Difficulties)if(award.Id==Key(song,diff)||award.Id.StartsWith(Key(song,diff)+"|BADGE:",StringComparison.Ordinal)){var spec=diff.Badges.FirstOrDefault(b=>b.Image==award.Image)??diff.Badge;return BadgeRequirementText.Format(song.Chart.Title,diff.Name,spec?.Conditions);}return BadgeRequirementText.Format("","",null);
    }
}
