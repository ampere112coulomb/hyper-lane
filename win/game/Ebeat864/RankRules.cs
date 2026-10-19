namespace HyperLane;
internal static class RankRules
{
    internal static bool IsFullCombo(int[]? counts)=>counts is {Length:5}&&counts[0]+counts[1]+counts[2]>0&&counts[3]==0&&counts[4]==0;
}
