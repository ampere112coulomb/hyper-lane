using CoreAnimation;
using Foundation;
using HyperLane.Android;

namespace HyperLane.iOS;
internal sealed partial class GameController
{
    // Only enabled by the simulator job's environment, never normal installations.
    private bool visualQa;
    private void ConfigureVisualQa()
    {
        string? name=Environment.GetEnvironmentVariable("HL_VISUAL_QA");if(string.IsNullOrEmpty(name))return;visualQa=true;
        string folder=Path.Combine(documents,"visual-qa");Directory.CreateDirectory(folder);
        string text="TITLE: Visual verification\nCOMPOSER: Hyper Lane\nCHARTER: Test\nBPM: 120\nLANES: 6\nJACKET: jacket.png\nDIFFICULTY: Master\nLEVEL: 30\n";
        string path=Path.Combine(folder,"chart.2d");File.WriteAllText(path,text);
        File.Copy(Path.Combine(NSBundle.MainBundle.BundlePath,"hyper-lane-icon.png"),Path.Combine(folder,"jacket.png"),true);
        var chart=MobileChart.Parse(text);var d=chart.Difficulties[0];
        // Parser owns the timing/layer indexes; append through a normal serialized chart.
        string notes=string.Join("\n",Enumerable.Range(1,6).Select(l=>$"{4+l*.5:0.0},{l}"));
        chart=MobileChart.Parse(text+notes+"\n");d=chart.Difficulties[0];var song=new MobileSong(path,folder,chart);stage.SetSongs([song]);stage.UserName="Visual QA";stage.Session=new MobileSession(song,d){AutoPlay=true};stage.Settings=settings;stage.Seconds=2.2;
        if(name.StartsWith("filter-")){string kind=name[7..].Replace("-background","").ToUpperInvariant();d.FilterChanges.Add(new MobileFilterChange(0,kind,75,100,false,name.EndsWith("-background")));}
        stage.ApplyVisualQa(name,song);
        File.WriteAllText(Path.Combine(folder,name+".ready"),name);
    }
}
internal sealed partial class MigrationStage
{
    internal void ApplyVisualQa(string name,MobileSong song)
    {
        LoadSong(song);resultAt=CAAnimation.CurrentMediaTime()-10;
        if(name is "dan" or "dan-result"){
            var definition=new HyperLane.DanDefinition{Id="visual-qa",Title="Visual DAN",Conditions=new(){GaugeMin=90,BadMissMaxExclusive=20}};
            for(int i=0;i<3;i++)definition.Tracks.Add(new(){Chart="chart.2d",Difficulty="Master",ShowTitle=i!=0});
            var course=new IosDanCourse(song.Folder,definition,Enumerable.Range(0,3).Select(_=>(song,song.Chart.Difficulties[0])).ToList());Courses=[course];ActiveDan=course;
            if(name=="dan-result"){for(int i=0;i<3;i++){Revealed.Add(RevealKey(course,i));DanResults.Add(new(song,song.Chart.Difficulties[0],990000,[6,0,0,0,0],6,100,100,[-.012,-.006,0,.009,.015]));}DanCounts=[18,0,0,0,0];DanSucceeded=true;DanEligible=true;}
        }
        if(name=="loading"){loadingDifficulty=song.Chart.Difficulties[0];loadingAt=CAAnimation.CurrentMediaTime()-1.5;}
        if(name=="result")Session?.Advance(99,new HashSet<int>());
        Page=name.StartsWith("filter-")?Screen.Playing:name switch{"modes"=>Screen.Modes,"select"=>Screen.Select,"loading"=>Screen.Loading,"practice"=>Screen.Practice,"dan"=>Screen.Dan,"dan-result"=>Screen.DanResult,"playing" or "playing-nodepth"=>Screen.Playing,"result"=>Screen.Result,"settings"=>Screen.Settings,_=>Screen.Title};
    }
}
