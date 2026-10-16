using AVFoundation;
using CoreAnimation;
using Foundation;
using HyperLane.Android;

namespace HyperLane.iOS;
internal sealed partial class GameController
{
    private AVAudioPlayer? previewAudio;private int previewRequest;private MobileSong? previewSong;private MobileDifficulty? previewDiff;private double previewFrom,previewTo,previewStarted;
    private void UpdatePreview()
    {
        bool show=stage.Page==MigrationStage.Screen.Select;
        var s=show?stage.Selected:null;var d=show?stage.SelectedDifficulty:null;
        if(!ReferenceEquals(s,previewSong)||!ReferenceEquals(d,previewDiff)){previewSong=s;previewDiff=d;ChangePreview(s,d);}
        if(previewAudio is not {} player||!show)return;
        double seconds=player.CurrentTime;if(seconds>=previewTo||!player.Playing){player.CurrentTime=previewFrom;player.Play();previewStarted=CAAnimation.CurrentMediaTime();}
        double duration=Math.Max(.08,Math.Min(.8,(previewTo-previewFrom)/3));player.Volume=(float)(settings.PreviewVolume/100.0*Math.Clamp(Math.Min((CAAnimation.CurrentMediaTime()-previewStarted)/duration,(previewTo-seconds)/duration),0,1));
    }
    private async void ChangePreview(MobileSong? song,MobileDifficulty? diff)
    {
        int request=++previewRequest;var previous=previewAudio;previewAudio=null;
        if(previous!=null){float volume=previous.Volume;for(int i=1;i<=8;i++){previous.Volume=volume*(1-i/8f);await Task.Delay(25);}previous.Stop();previous.Dispose();}
        if(song==null||diff==null)return;await Task.Delay(200);if(request!=previewRequest||stage.Page!=MigrationStage.Screen.Select)return;
        string path=MigrationStage.Media(song,song.Chart.AudioFor(diff));if(path.Length==0)return;AVAudioPlayer? next=null;
        try{if(Path.GetExtension(path).Equals(".ogg",StringComparison.OrdinalIgnoreCase))path=await IosAudioConverter.ToM4aAsync(path,Path.Combine(documents,"audio-cache"));if(request!=previewRequest||stage.Page!=MigrationStage.Screen.Select)return;next=AVAudioPlayer.FromUrl(NSUrl.FromFilename(path),out _);if(next==null)return;
            previewFrom=Math.Clamp(song.Chart.OffsetFor(diff)+MobileChart.BeatSeconds(diff,song.Chart.Bpm,song.Chart.PreviewStartFor(diff)),0,next.Duration);previewTo=Math.Clamp(song.Chart.OffsetFor(diff)+MobileChart.BeatSeconds(diff,song.Chart.Bpm,song.Chart.PreviewEndFor(diff)),previewFrom,next.Duration);if(previewTo<=previewFrom)previewTo=next.Duration;
            if(request!=previewRequest||stage.Page!=MigrationStage.Screen.Select){next.Dispose();return;}previewAudio=next;next.CurrentTime=previewFrom;next.Volume=0;next.Play();previewStarted=CAAnimation.CurrentMediaTime();
        }catch{next?.Dispose();}
    }
}
