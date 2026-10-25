using CoreAnimation;
using HyperLane;
namespace HyperLane.iOS;
internal sealed partial class GameController
{
    private DanLifeState? danLife;
    private double danFailureAt,danFailureSeconds;
    private IosDanFailureAudio? danFailureAudio;
    private bool UpdateDanFailure()
    {
        if(activeDan==null||session?.Life?.Failed!=true)return false;
        double now=CAAnimation.CurrentMediaTime();if(danFailureAt==0){danFailureAt=now;danFailureSeconds=stage.Seconds;danEligible=false;held.Clear();stage.ClearTouches();noteSound?.Pause();try{danFailureAudio?.Start(audio?.CurrentTime??0,settings.BgmVolume/100d);}catch(Exception ex){Console.Error.WriteLine("Dan audio effect: "+ex.Message);}audio?.Stop();}
        double age=now-danFailureAt;stage.Seconds=danFailureSeconds+age;stage.DanFailureStrength=Math.Clamp(age/3,0,1);
        if(age>=3){danFailureAudio?.Dispose();danFailureAudio=null;EndPlay();danLife=null;danFailureAt=0;stage.DanFailureStrength=0;stage.DanReturnAt=now;}
        stage.Refresh(now);return true;
    }
}
