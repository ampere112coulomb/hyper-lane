using System;
using System.IO;

namespace HyperLane;
internal sealed class DanLifeSettings
{
    public bool Enabled { get; set; } = true;
    public double Initial { get; set; } = 100;
    public double? Maximum { get; set; }
    public double PerfectLoss { get; set; }
    public double GreatLoss { get; set; } = 1;
    public double GoodLoss { get; set; } = 3;
    public double BadLoss { get; set; } = 10;
    public double MissLoss { get; set; } = 10;
    public int ComboInterval { get; set; } = 100;
    public double ComboRecovery { get; set; } = 5;
    public double Cap => Maximum ?? Initial;
    public void Validate()
    {
        if(!double.IsFinite(Initial)||Initial<=0||Initial>1000000||!double.IsFinite(Cap)||Cap<Initial||Cap>1000000)
            throw new InvalidDataException("ライフの初期値は0より大きく、上限以下で設定してください（最大1000000）");
        foreach(double value in new[]{PerfectLoss,GreatLoss,GoodLoss,BadLoss,MissLoss,ComboRecovery})
            if(!double.IsFinite(value)||value<0||value>1000000)throw new InvalidDataException("ライフ増減値は0～1000000です");
        if(ComboInterval<1||ComboInterval>1000000)throw new InvalidDataException("回復コンボ間隔は1～1000000です");
    }
}
internal sealed class DanLifeState(DanLifeSettings settings)
{
    public double Value { get; private set; } = settings.Initial;
    public double Maximum => settings.Cap;
    public bool Failed => Value<=0;
    public void Judge(int grade,int combo)
    {
        if(Failed||!settings.Enabled)return;
        double loss=grade switch{0=>settings.PerfectLoss,1=>settings.GreatLoss,2=>settings.GoodLoss,3=>settings.BadLoss,_=>settings.MissLoss};
        Value=Math.Max(0,Value-loss);
        // Zero is terminal: a simultaneous combo bonus cannot revive a failed course.
        if(Value>0&&grade<=2&&combo>0&&combo%settings.ComboInterval==0)
            Value=Math.Min(Maximum,Value+settings.ComboRecovery);
    }
}
internal static class DanLifeTransition
{
    public static double WhiteIn(double seconds)=>Math.Clamp(seconds/3,0,1);
    public static double WhiteOut(double seconds)=>Math.Clamp(1-seconds,0,1);
}
