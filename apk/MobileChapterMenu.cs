using System.Text.Json;

namespace HyperLane.Android;
internal sealed record MobileChapterEntry(string Key,string Name,string Image,string Kind);
internal readonly record struct MobileChapterRect(float X,float Y,float Width,float Height)
{
    public float Cut=>Height*.17f;
    public bool Contains(float x,float y){if(y<Y||y>Y+Height)return false;float edge=Cut*(1-2*(y-Y)/Height);return x>=X+edge&&x<=X+Width+edge;}
}
internal sealed class MobileChapterMenu
{
    public List<MobileChapterEntry> Entries {get;}=[];
    public int Index {get;private set;}
    public string Filter {get;private set;}="@all";
    private double from;
    private long started;
    private float[] sizes=[];
    private static double Ease(double x){x=Math.Clamp(x,0,1);return x*x*(3-2*x);}
    public double Position=>started==0?Index:from+(Index-from)*Ease((Environment.TickCount64-started)/360.0);
    public float Expansion(int i)=>started==0?(i==Index?1:0):(i<sizes.Length?sizes[i]:0)*(float)(1-Ease((Environment.TickCount64-started)/360.0))+(i==Index?(float)Ease((Environment.TickCount64-started-700)/360.0):0);
    public void Refresh(string root)
    {
        string key=Entries.Count>0?Entries[Index].Key:"@all";Entries.Clear();Entries.Add(new("@all","ALL chart","","all"));
        if(Directory.Exists(root))foreach(string file in Directory.EnumerateFiles(root,"chapter.json",SearchOption.AllDirectories).OrderBy(p=>Path.GetRelativePath(root,p),StringComparer.OrdinalIgnoreCase))try{
            if(new FileInfo(file).Length>65536)continue;using var json=JsonDocument.Parse(File.ReadAllText(file),new JsonDocumentOptions{AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip});string folder=Path.GetDirectoryName(file)!,name=Path.GetFileName(folder),image="header.jpg";
            foreach(var p in json.RootElement.EnumerateObject())if(p.Value.ValueKind==JsonValueKind.String){if(p.Name.Equals("name",StringComparison.OrdinalIgnoreCase)||p.Name.Equals("title",StringComparison.OrdinalIgnoreCase))name=p.Value.GetString()??name;if(p.Name.Equals("image",StringComparison.OrdinalIgnoreCase))image=p.Value.GetString()??image;}
            if(Path.IsPathRooted(image)||image.Replace('\\','/').Split('/').Contains("..")||image.Contains(':'))image="";
            Entries.Add(new(folder,string.IsNullOrWhiteSpace(name)?Path.GetFileName(folder):name,Path.Combine(folder,image),"chapter"));
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException){}
        Entries.Add(new("@archive","SIDE ARCHIVE","","archive"));Entries.Add(new("@dan","段位モード / DAN","","dan"));Entries.Add(new("@practice","練習モード / PRACTICE","","practice"));
        Index=Math.Max(0,Entries.FindIndex(e=>e.Key==key));if(!Entries.Any(e=>e.Key==Filter))Filter="@all";started=0;
    }
    public void Move(int delta){int next=Math.Clamp(Index+delta,0,Entries.Count-1);if(next==Index)return;var previous=Enumerable.Range(0,Entries.Count).Select(Expansion).ToArray();from=Position;sizes=previous;Index=next;started=Environment.TickCount64;}
    public IEnumerable<int> Visible()=>Enumerable.Range(Math.Max(0,(int)Math.Floor(Position)-3),Math.Min(Entries.Count-1,(int)Math.Ceiling(Position)+3)-Math.Max(0,(int)Math.Floor(Position)-3)+1);
    public MobileChapterRect Rect(int i){double d=i-Position;float t=(float)Math.Min(1,Math.Abs(d)),side=d<0?-1:1,e=Expansion(i),extra=(float)Math.Max(0,Math.Abs(d)-1);float h=105+405*e;return new(510+side*80*t-70*e+extra*side*24,500+side*350*t+extra*side*160-h/2,900+140*e,h);}
    public void ActivateFilter(){if(Entries[Index].Kind is "all" or "archive" or "chapter")Filter=Entries[Index].Key;}
    public IEnumerable<MobileSong> Songs(IEnumerable<MobileSong> songs,bool all=false)
    {
        bool In(MobileSong song,string folder)=>Path.GetFullPath(song.ChartPath).StartsWith(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.Ordinal);
        return songs.Where(s=>all||Filter=="@all"||(Filter=="@archive"?!Entries.Any(e=>e.Kind=="chapter"&&In(s,e.Key)):In(s,Filter))).OrderBy(s=>s.ChartPath,StringComparer.OrdinalIgnoreCase);
    }
    public static string RootFor(IEnumerable<MobileSong> songs){string? path=songs.FirstOrDefault()?.Folder;while(path!=null){if(Path.GetFileName(path).Equals("song",StringComparison.OrdinalIgnoreCase))return path;path=Path.GetDirectoryName(path);}return "";}
}
