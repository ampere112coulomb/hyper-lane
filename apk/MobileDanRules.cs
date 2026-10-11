using HyperLane;

namespace HyperLane.Android;

internal static class MobileDanRules
{
    internal static List<(string Label,bool Passed)> Checks(DanDefinition definition,int[] totalCounts,IReadOnlyList<double> globalGauges,IReadOnlyList<(int[] Counts,double Gauge)> results,bool intermediate)
    {
        var global=definition.Conditions;
        var check=intermediate?new DanConditions{GaugeMin=global.GaugeScope=="everySong"?global.GaugeMin:null,GaugeScope=global.GaugeScope,BadMissMaxExclusive=global.BadMissMaxExclusive,BadMax=global.BadMax,MissMax=global.MissMax}:global;
        var checks=check.Evaluate(totalCounts,globalGauges);
        for(int i=0;i<results.Count;i++)if(definition.Tracks[i].Conditions is {} own)checks.AddRange(own.Evaluate(results[i].Counts,new[]{results[i].Gauge}));
        return checks;
    }
}
