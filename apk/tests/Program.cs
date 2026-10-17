using HyperLane.Android;
{
    var c=MobileChart.Parse("BPM:120\nDIFFICULTY:Master\nLEVEL:10\nLANES:4\n0,1\n0.0625H,1\n");
    var d=c.Difficulties[0];var s=new MobileSession(new MobileSong("cohit.2d",".",c),d);
    s.Press(1,.04);
    if(s.Counts.Sum()!=2||d.Notes.Any(n=>!n.Done))throw new Exception("Late Normal must also hit overlapping Hold");
    var geometry=new List<float>();var p=MobileProjection.At(d,0);
    s.HitEffects.Append(geometry,s,p,.04);
    if(geometry.Count==0||geometry.Count%60!=0||geometry.Any(v=>!float.IsFinite(v)))throw new Exception("Invalid batched hit effects");
    geometry.Clear();s.HitEffects.Append(geometry,s,p,2);
    if(geometry.Count!=0)throw new Exception("Expired effects retained");
    Console.WriteLine("Normal+Hold co-hit and bounded finite hit-effect geometry: OK");
}
{
    foreach(string? name in new string?[]{null,""," ","SPECIAL","special","Custom"," custom "})
        if(HyperLane.PlayerRating.IsRatedDifficulty(name))throw new Exception("SPECIAL aliases must be unrated");
    if(!HyperLane.PlayerRating.IsRatedDifficulty("Master"))throw new Exception("Master remains rated");
    string dir=Path.Combine("G:\\HL ファイル関連\\workspace\\temp","special-rating-"+Guid.NewGuid().ToString("N"));
    string path=Path.Combine(dir,"rating.json");
    var rating=new HyperLane.PlayerRating(path);
    var charts=new List<(string Key,string ChartHash,double Level)>();
    foreach(string name in new[]{"Master","Special","Custom",""}){
        string key=HyperLane.PlayerRating.Key("song","chart.2d",name);
        rating.Update(key,"hash",12,1000000);charts.Add((key,"hash",12));
        if(rating.BestScore(key,"hash",12)!=1000000)throw new Exception("SPECIAL high score must remain stored");
    }
    var reloaded=new HyperLane.PlayerRating(path);
    if(Math.Abs(reloaded.TotalFor(charts)-12.0/30)>1e-10)throw new Exception("Existing SPECIAL scores must be excluded from total");
    Console.WriteLine("SPECIAL/Custom/blank excluded from rating; existing high scores preserved.");
}
{
    var labelled=MobileChart.Parse("BPM:120\nDIFFICULTY:Master\nLEVEL:12.9\nLEVEL_DISPLAY:皆伝\n").Difficulties[0];
    if(labelled.Level!="12.9"||labelled.DisplayLevel!="皆伝")throw new Exception("Separate numeric/display level");
    var numeric=MobileChart.Parse("BPM:120\nDIFFICULTY:Master\nLEVEL:12.9\n").Difficulties[0];
    if(numeric.DisplayLevel!="12+")throw new Exception("12+ display");
    var truncated=MobileChart.Parse("BPM:120\nDIFFICULTY:Master\nLEVEL:8.4\nLEVEL_DISPLAY:難関曲\n").Difficulties[0];
    if(truncated.DisplayLevel!="難関")throw new Exception("Two character label");
    if(HyperLane.PlayerRating.ChartRating(12.9,1000000)!=12.9)throw new Exception("Numeric rating unchanged");
    Console.WriteLine("Android constants, separate display labels, 12+, two-character bound and rating passed.");
}

{
    float[] quad=new float[40];
    var clipProjection=MobileProjection.At(MobileChart.Parse("BPM:120\nDIFFICULTY:Master\nLANES:6\n").Difficulties[0],0);
    clipProjection.Clip(-1.15,.004,52,quad.AsSpan(0,4));clipProjection.Clip(1.15,.004,52,quad.AsSpan(10,4));
    clipProjection.Clip(1.15,.004,-12,quad.AsSpan(20,4));clipProjection.Clip(-1.15,.004,-12,quad.AsSpan(30,4));
    for(int i=0;i<4;i++){quad[i*10+4]=i is 1 or 2?1:0;quad[i*10+5]=i>=2?1:0;quad[i*10+9]=1;}
    var triangles=new List<float>();HyperLane.MobileNearClip.AppendQuad(quad,triangles);
    if(triangles.Count<60||triangles.Count%30!=0)throw new Exception("Near clipping must preserve visible lane polygons");
    for(int i=0;i<triangles.Count;i+=10)if(triangles[i+3]<=0||triangles[i+2]+triangles[i+3]<-.000001||triangles[i+4]<0||triangles[i+4]>1||triangles[i+5]<0||triangles[i+5]>1)throw new Exception("Near clipping must preserve positive W and interpolated UV");
    for(int i=0;i<4;i++){quad[i*10]=i is 1 or 2?.5f:-.5f;quad[i*10+1]=i<2?.5f:-.5f;quad[i*10+2]=0;quad[i*10+3]=1;}
    triangles.Clear();HyperLane.MobileNearClip.AppendQuad(quad,triangles);
    if(triangles.Count!=60||!triangles.Take(30).SequenceEqual(quad.Take(30)))throw new Exception("Visible quads must keep the original triangle order");
    for(int i=0;i<4;i++){quad[i*10+2]=-1;quad[i*10+3]=-1;}
    triangles.Clear();HyperLane.MobileNearClip.AppendQuad(quad,triangles);
    if(triangles.Count!=0)throw new Exception("Quads completely behind the camera must not be emitted");
    Console.WriteLine("Homogeneous near clipping: camera-crossing lanes, positive W and UV: OK");
}

var danDefinition=new HyperLane.DanDefinition{Title="test",Conditions=new(){GaugeMin=90,PerfectMin=10,BadMissMaxExclusive=20},Tracks=[new(){Chart="1/chart.2d",Difficulty="Expert",Conditions=new(){GaugeMin=50}},new(){Chart="2/chart.2d",Difficulty="Master"}]};
var danPartialCounts=new[]{2,0,0,0,0};
var danPartialResults=new (int[] Counts,double Gauge)[]{(danPartialCounts,60)};
if(!MobileDanRules.Checks(danDefinition,danPartialCounts,new[]{20d},danPartialResults,true).All(x=>x.Passed))throw new Exception("Final gauge / PERFECT must not stop an incomplete Dan course");
if(MobileDanRules.Checks(danDefinition,danPartialCounts,new[]{20d},danPartialResults,false).All(x=>x.Passed))throw new Exception("Final Dan requirements must be evaluated");
danPartialCounts[3]=20;
if(MobileDanRules.Checks(danDefinition,danPartialCounts,new[]{20d},danPartialResults,true).All(x=>x.Passed))throw new Exception("BAD+MISS exclusive limit must stop at equality");
danPartialCounts[3]=0;danDefinition.Conditions.GaugeScope="everySong";
if(MobileDanRules.Checks(danDefinition,danPartialCounts,new[]{20d},danPartialResults,true).All(x=>x.Passed))throw new Exception("Every-song global gauge must be checked at boundaries");
danDefinition.Conditions.GaugeScope="final";danPartialResults[0]=(danPartialCounts,40);
if(MobileDanRules.Checks(danDefinition,danPartialCounts,new[]{20d},danPartialResults,true).All(x=>x.Passed))throw new Exception("Per-track gauge must stop progression");
var danChart=MobileChart.Parse("TITLE: Dan\nBPM: 120\nDIFFICULTY: Expert\nLANES: 4\nLEVEL: 26\n0,1,N\n1,2,N\n");
var danSession=new MobileSession(new("chart.2d",".",danChart),danChart.Difficulties[0]){AutoPlay=true};danSession.ConfigureDanGauge(10,20,30);danSession.Advance(.01,new HashSet<int>());
if(Math.Abs(danSession.Gauge-(10+400d/3/2))>.001||Math.Abs(danSession.GlobalGauge-(30+400d/3/20))>.001)throw new Exception("Global Dan gauge must use whole-course judgement units independently of track gauge");
if(HyperLane.PlayerRating.BestCount!=30)throw new Exception("Best30 divisor mismatch");
if(!HyperLane.UserProfile.ValidName("Ampere")||HyperLane.UserProfile.ValidName("")||HyperLane.UserProfile.ValidName("12345678901"))throw new Exception("Username validation mismatch");
Console.WriteLine("Dan: intermediate/final/global/per-track checks, exclusive limits, course-wide gauge; Best30; username: OK");

var pcmEvents=UnifiedAudioMath.EventFrames(new[]{1.0,1.0,.5,1.00390625},-50,48000);
if(!pcmEvents.SequenceEqual(new long[]{21600,45600,45788}))throw new Exception("PCM sample scheduling / chord deduplication / offset mismatch");
if(UnifiedAudioMath.Clip(40000)!=32767||UnifiedAudioMath.Clip(-40000)!=-32768||UnifiedAudioMath.Clip(12000+4000)!=16000)throw new Exception("PCM mix clipping mismatch");
Console.WriteLine("Unified PCM: sample scheduling, chord deduplication, signed offset and saturation: OK");
if(UnifiedAudioMath.Mix(12000,-12000,0,0)!=(12000,-12000))throw new Exception("SE silence must preserve BGM amplitude");
if(UnifiedAudioMath.Mix(12000,-12000,8000,-8000)!=(16000,-16000))throw new Exception("Only SE reference gain should change");
var loudMix=UnifiedAudioMath.Mix(30000,-30000,16000,-16000);
if(loudMix.Left<30000||loudMix.Right>-30000||loudMix.Left!=32767)throw new Exception("Headroom must limit SE, not attenuate BGM");
Console.WriteLine("Unified PCM: BGM-preserving SE gain and stereo headroom: OK");

foreach (int lanes in new[] { 3, 4, 6 })
{
    for (int lane = 0; lane < lanes; lane++)
    {
        TouchRect rect = TouchZones.GetRect(lanes, lane, 1920, 1080);
        if (Math.Abs(rect.Top - 616) > 0.01 || Math.Abs(rect.Bottom - 1064) > 0.01)
            throw new Exception($"Vertical bounds mismatch: {lanes}K lane {lane}");
        float centerX = (rect.Left + rect.Right) / 2;
        if (TouchZones.HitTest(lanes, centerX, 840, 1920, 1080) != lane)
            throw new Exception($"Reference hit mismatch: {lanes}K lane {lane}");
        if (TouchZones.HitTest(lanes, centerX / 2, 420, 960, 540) != lane)
            throw new Exception($"Scaled hit mismatch: {lanes}K lane {lane}");
    }
    if (TouchZones.HitTest(lanes, 0, 840, 1920, 1080) != -1 ||
        TouchZones.HitTest(lanes, 960, 500, 1920, 1080) != -1)
        throw new Exception($"Outside hit mismatch: {lanes}K");
}
Console.WriteLine("3K / 4K / 6K touch zones: OK");
foreach(int lanes in new[]{3,4,5,6,8,12}) {
    float previousTop=1064;
    for(int value=0;value<=100;value++) {
        var rect=TouchZones.GetRect(lanes,0,1920,1080,value);
        if(rect.Bottom!=1064 || rect.Top>previousTop)throw new Exception("Touch height must extend upward with fixed bottom");
        previousTop=rect.Top;
        float x=(rect.Left+rect.Right)/2;
        if(value>0 && TouchZones.HitTest(lanes,x,(rect.Top+rect.Bottom)/2,1920,1080,value)!=0)throw new Exception("Touch height hit mismatch");
    }
    if(TouchZones.GetRect(lanes,0,1920,1080,50).Top!=616 || TouchZones.GetRect(lanes,0,1920,1080,100).Top!=0 || TouchZones.HitTest(lanes,100,1063,1920,1080,0)!=-1)throw new Exception("Touch height endpoint mismatch");
}
Console.WriteLine("Touch height 0..100, default 50, fixed bottom and all lane counts: OK");
var holdChart = MobileChart.Parse("""
TITLE: Hold test
LANES: 3
BPM: 120
DIFFICULTY: Normal
LEVEL: 1
0.00H,1
1.00-2.00,2
""");
var holdSong = new MobileSong("test.2d", ".", holdChart);
MobileDifficulty holdDiff = holdChart.Difficulties[0];
var holdSession = new MobileSession(holdSong, holdDiff);
holdSession.Press(1, 0);
holdSession.Press(2, .5);
holdSession.Release(2, 1);
if (holdSession.Counts[0] != 3 || holdSession.Score != 1_000_000)
    throw new Exception("Hold/key judgement mismatch");
if (holdSession.Combo != 3 || holdSession.Gauge <= 0)
    throw new Exception("Gauge/combo mismatch");
Console.WriteLine("Hold/key judgement: OK");
var largeChart = MobileChart.Parse("""
TITLE: Large test
LANES: 4
BPM: 120
DIFFICULTY: Normal
LEVEL: 1
0.00M,2,SIZE=2
1.00M,1,SIZE=3
""");
var largeDiff = largeChart.Difficulties[0];
if (largeDiff.Notes.Count != 2 || largeDiff.Notes[0].LaneSpan != 2 || largeDiff.Notes[1].LaneSpan != 3)
    throw new Exception("Large note parsing mismatch");
var largeSession = new MobileSession(new MobileSong("large.2d", ".", largeChart), largeDiff);
largeSession.Press(3, 0);
largeSession.Press(2, 0);
largeSession.Press(1, .5);
if (largeSession.Counts[0] != 2 || largeSession.Combo != 2)
    throw new Exception("Large note lane judgement mismatch");
Console.WriteLine("Large note parsing/judgement: OK");
var effectsChart = MobileChart.Parse("""
TITLE: Effects
CHART_DESIGNER: Ampere
TIMESIG: 7/8
LANES: 6
DIFFICULTY: Master
LEVEL: 30
CHANGE: 4, BPM, 180
CHANGE: 5, BS, -1
CHANGE: 6, NS, 2,3,4
CHANGE: 7, CAMERA, (0,5,-10)3,1
CHANGE: 8, CAMERA_ADV, (0,2.5,-6),(-12.8,0,360)3,2
CHANGE: 9, LANE_VIS, 0,8
CHANGE: 10, UI_VIS, 50,2
CHANGE: 11, FILTER, BLOOM,60,8,RAMP
FEVER: 12-16
4,1,SS=2
5-6G,2,#10b981,fade,0.25
""");
MobileDifficulty effects = effectsChart.Difficulties[0];
if (effectsChart.ChartDesigner != "Ampere" || Math.Abs(effectsChart.MeasureLength - 3.5) > .001 ||
    effects.BpmChanges.Count != 1 || effects.BsChanges.Count != 1 || effects.NsChanges.Count != 1 ||
    effects.CameraChanges.Count != 2 || effects.LaneVisibilityChanges.Count != 1 ||
    effects.UiVisibilityChanges.Count != 1 || effects.FilterChanges.Count != 1 ||
    effects.FeverSections.Count != 1 || Math.Abs(effects.Notes[0].Ss - 2) > .001 ||
    effects.Notes[1].CustomColor != "#10b981" || effects.Notes[1].GuideOpacityMode != "fade" ||
    Math.Abs(effects.Notes[1].GuideOpacity - .25) > .001)
    throw new Exception("Effects parser mismatch");
if (Math.Abs(MobileChart.BeatAtSeconds(effects, effectsChart.Bpm,
        MobileChart.BeatSeconds(effects, effectsChart.Bpm, 8)) - 8) > .0001 ||
    MobileChart.ScrollPosition(effects, effectsChart.Bpm, 8) >= MobileChart.ScrollPosition(effects, effectsChart.Bpm, 5) ||
    Math.Abs(MobileChart.NsMultiplierAt(effects, 8) - 1.875) > .0001)
    throw new Exception("BPM/BS/NS timing mismatch");
Console.WriteLine("Chart effects parser: OK");
MobileProjection projection = MobileProjection.At(effects, 0);
if (!projection.Project(-1.15, .004, 0, out float nearLeft, out float nearY) ||
    !projection.Project(1.15, .004, 0, out float nearRight, out _) ||
    !projection.Project(-1.15, .004, 45, out float farLeft, out float farY) ||
    !projection.Project(1.15, .004, 45, out float farRight, out _) ||
    Math.Abs(nearRight - nearLeft) <= Math.Abs(farRight - farLeft) || nearY <= farY)
    throw new Exception("3D projection mismatch");
Console.WriteLine("3D lane projection: OK");
var layered = MobileChart.Parse("""
TITLE: Port parity
BPM: 120
LANES: 6
DIFFICULTY: SPECIAL
LEVEL: 30
CHANGE: 0, BS, 0
CHANGE: 1, BS, 64
CHANGE: 1.00390625, BS, 0
CHANGE: 4, JUDGE_LINE, 70;70;0;0;70;70,4,EASE_OUT
CHANGE: 12, FILTER, MOSAIC,60,4,RAMP
2W,-1,SIZE=3,SS=-0.5
3M,6,SIZE=4
LAYER: 2
CHANGE: 0, BPM, 240
CHANGE: 0, BS, -2
CHANGE: 0, JUDGE_LINE, 50;50;50;50;50;50,4,RAMP
CHANGE: 0, FILTER, INVERT,100,4,RAMP
4,3
""");
var parity = layered.Difficulties[0];
if (parity.Notes.Count != 3 || parity.Layers.Count != 1 || parity.JudgementLineChanges.Count != 1 || parity.FilterChanges.Count != 1)
    throw new Exception("Layer/large hold/global restriction mismatch");
var wideHold = parity.Notes.Single(n => n.Kind == 'W');
if (wideHold.Lane != -1 || wideHold.LaneSpan != 3 || wideHold.Ss != -.5)
    throw new Exception("Overhanging large hold/negative SS mismatch");
var secondLayer = parity.Notes.Single(n => n.Layer == 2);
if (Math.Abs(secondLayer.StartSeconds - 1) > 1e-12 || Math.Abs(secondLayer.ScrollStart + 4) > 1e-12)
    throw new Exception("Independent layer timing mismatch");
double pulse = MobileChart.ScrollPosition(parity, 120, 2) - MobileChart.ScrollPosition(parity, 120, 1);
if (Math.Abs(pulse - .25) > 1e-12 || Math.Abs(MobileChart.ScrollPosition(parity, 120, 100) - .25) > 1e-12)
    throw new Exception("Fine BS pulse/BS zero mismatch");
var wideSession = new MobileSession(new MobileSong("parity.2d", ".", layered), parity);
wideSession.Advance(1, new HashSet<int> { 1 });
if (!wideHold.Done || wideSession.Counts[0] != 1) throw new Exception("Overhanging large hold judgement mismatch");
double[] positions = new double[6];
HyperLane.JudgementLineMotion.Evaluate(parity.JudgementLineChanges, 6, positions);
if (Math.Abs(positions[0] - 61.25) > 1e-12 || positions[2] != 0) throw new Exception("Native judgement-line easing mismatch");
projection.Clip(-1.15, .004, 0, new float[4]);
if (projection.Polygon(-1.15, .004, -12, 1.15, .004, -12, 1.15, .004, 52, -1.15, .004, 52).Length < 6)
    throw new Exception("Track clipping mismatch");
Console.WriteLine("Layers / Large Hold / negative SS / 1/256 BS pulse / native line easing: OK");
var audioTiming = new MobilePlaybackTiming();
audioTiming.Anchor(0, 100, 0); // decoder is not yet producing sound
if (audioTiming.At(100.4) != 0) throw new Exception("Audio buffering must stop chart time");
audioTiming.Anchor(.25, 100.5, 1);
if (Math.Abs(audioTiming.At(100.6) - .35) > 1e-9) throw new Exception("Audio timestamp extrapolation mismatch");
if (Math.Abs(MobilePlaybackTiming.ChartSeconds(1, 200) - .8) > 1e-12 ||
    Math.Abs(MobilePlaybackTiming.ChartSeconds(1, -200) - 1.2) > 1e-12)
    throw new Exception("Chart offset sign mismatch");
audioTiming.Anchor(5, 101, 0);
if (audioTiming.At(105) != 5) throw new Exception("Paused audio must not advance chart time");
Console.WriteLine("Audio buffering / timestamp extrapolation / positive and negative offset / pause: OK");
var autoChart = MobileChart.Parse("BPM:120\nDIFFICULTY:Master\nLANES:4\n0,1\n1-3,2\n4H,3\n5D,4\n");
var autoSession = new MobileSession(new MobileSong("auto.2d", ".", autoChart), autoChart.Difficulties[0]) { AutoPlay = true };
autoSession.Press(1, .06);
if (autoSession.Counts.Sum() != 0) throw new Exception("Autoplay accepted manual press");
autoSession.Advance(0, new HashSet<int>());
autoSession.Press(2, .55);
autoSession.Advance(.5, new HashSet<int> { 2 });
autoSession.Release(2, .6);
autoSession.Advance(1.5, new HashSet<int>());
autoSession.Press(3, 2);
autoSession.Advance(2, new HashSet<int> { 3 });
autoSession.Release(3, 2.01);
autoSession.Advance(2.6, new HashSet<int> { 4 });
if (autoSession.Score != 1000000 || autoSession.Counts[0] != 5 || autoSession.Counts.Skip(1).Sum() != 0)
    throw new Exception("Manual touch/release interfered with autoplay");
Console.WriteLine("Autoplay ignores manual taps/releases, including long/hold/damage: OK");
var overheadChart = MobileChart.Parse("BPM:120\nDIFFICULTY:Master\nCHANGE:490,CAMERA_ADV,(0,20,3),(-90,0,0)3,4\n");
var overhead = MobileProjection.At(overheadChart.Difficulties[0], 494);
if (!overhead.Project(-1, 0, 3, out float ax, out float ay) ||
    !overhead.Project(1, 0, 3, out float bx, out float by) ||
    !overhead.Project(0, 0, 8, out float cx, out float cy) ||
    Math.Abs(ax - bx) < 100 || Math.Abs(ay - by) > .01 || Math.Abs(cx - 960) > .01 || Math.Abs(cy - 540) < 100)
    throw new Exception("Vertical camera at -90 degrees rotated the lane sideways");
Console.WriteLine("Deus Judicium -90 degree overhead camera orientation: OK");
foreach (bool auto in new[] { false, true })
foreach (bool autoSound in new[] { false, true })
{
    bool scheduled = MobileSoundPolicy.Scheduled(auto, autoSound), onHit = MobileSoundPolicy.OnJudgement(auto, autoSound);
    if (scheduled == onHit || (auto && !scheduled) || (!auto && !autoSound && !onHit))
        throw new Exception("Autoplay/automatic SE policy muted or doubled sound");
}
Console.WriteLine("SE: autoplay ON/OFF x automatic SE ON/OFF all have exactly one sound source: OK");
string iosSettingsPath = Path.Combine(Path.GetTempPath(), "864beat-ios-settings-test-" + Guid.NewGuid().ToString("N") + ".json");
try
{
    var iosSettings = new HyperLane.iOS.IosSettings { AudioOffsetMs = -135, AutoPlay = true, AutoNoteSound = false, SeVolume = 72, LaneCover = 35 };
    iosSettings.Save(iosSettingsPath);
    var restored = HyperLane.iOS.IosSettings.Load(iosSettingsPath);
    if (restored.AudioOffsetMs != -135 || !restored.AutoPlay || restored.AutoNoteSound || restored.SeVolume != 72 || restored.LaneCover != 35)
        throw new Exception("iOS settings persistence mismatch");
}
finally { if (File.Exists(iosSettingsPath)) File.Delete(iosSettingsPath); }
Console.WriteLine("iOS settings persistence (offset/auto/SE/cover): OK");
var practiceChart = MobileChart.Parse("BPM:120\nDIFFICULTY:Master\nLANES:4\nCHANGE:4,BPM,240\n0,1\n7,2\n8,3\n12-14,4\n");
var practiceDiff = practiceChart.Difficulties[0];
foreach (int offset in new[] { -200, 0, 200 })
{
    var timing = MobilePracticeTiming.At(practiceChart, practiceDiff, 8, offset);
    if (Math.Abs(timing.AnchorChart - (timing.StartRaw - offset / 1000.0) - 3) > 1e-9)
        throw new Exception("Practice eight-beat lead-in must integrate BPM changes");
    double from = Math.Max(0, timing.StartRaw) - offset / 1000.0;
    if (timing.Gain(from) != 0 || timing.Gain(timing.AnchorChart) != 1 || !timing.IsPreroll(timing.AnchorChart - .001) || timing.IsPreroll(timing.AnchorChart))
        throw new Exception("Practice offset/fade/judgement gate mismatch");
    var practiceSession = new MobileSession(new MobileSong("practice.2d", ".", practiceChart), practiceDiff);
    timing.SkipPreviousNotes(practiceDiff);
    if (!practiceDiff.Notes[0].Done || !practiceDiff.Notes[1].Done || practiceDiff.Notes[2].Done)
        throw new Exception("Practice skipped the chosen beat or judged earlier notes");
    practiceSession.Press(3, timing.AnchorChart);
    if (practiceSession.Counts[0] != 1) throw new Exception("Practice chosen-beat input mismatch");
    // Restart uses a new session, restoring both completed notes and long state.
    var restarted = new MobileSession(new MobileSong("practice.2d", ".", practiceChart), practiceDiff);
    timing.SkipPreviousNotes(practiceDiff);
    if (practiceDiff.Notes[2].Done || practiceDiff.Notes[3].Started || restarted.Counts.Sum() != 0)
        throw new Exception("Practice retry failed to restore notes and judgement state");
}
var beginning = MobilePracticeTiming.At(practiceChart, practiceDiff, 0, 0);
if (Math.Abs(beginning.StartRaw + 4) > 1e-9 || beginning.AnchorChart != 0)
    throw new Exception("Practice at beat zero must show an eight-beat negative lead-in");
Console.WriteLine("Practice: exact 8-beat BPM-aware preroll, offset, fade, chosen-beat judgement and restored notes: OK");
var brightChart = MobileChart.Parse("""
BPM:120
DIFFICULTY:Master
LANES:4
CHANGE:0,FILTER,BRIGHTNESS,-100,4,INSTANT,BACKGROUND
CHANGE:8,FILTER,BRIGHTNESS,-100,4,RAMP,ALL
CHANGE:16,FILTER,BRIGHTNESS,100,4,RAMP,ALL
CHANGE:24,FILTER,BRIGHTNESS,-60,4,RAMP,BACKGROUND
CHANGE:32,FILTER,MOSAIC,60,4,INSTANT,BACKGROUND
CHANGE:40,FILTER,BRIGHTNESS,-101,4,INSTANT,ALL
CHANGE:41,FILTER,BRIGHTNESS,101,4,INSTANT,ALL
CHANGE:42,FILTER,MONO,-1,4,INSTANT,ALL
CHANGE:43,FILTER,BRIGHTNESS,NaN,4,RAMP,ALL
CHANGE:44,FILTER,BRIGHTNESS,50,Infinity,RAMP,ALL
""");
var filters = brightChart.Difficulties[0].FilterChanges;
if (filters.Count != 5 || !filters[0].BackgroundOnly || filters[1].BackgroundOnly || !filters[4].BackgroundOnly)
    throw new Exception("Brightness/target parser or finite range validation mismatch");
double StrengthAt(double beat) => MobileFilters.At(filters, beat)?.Strength ?? 0;
if (StrengthAt(0) != -100 || StrengthAt(4) != 0 || StrengthAt(8) != 0 || StrengthAt(10) != -50 || StrengthAt(14) != -100 || StrengthAt(18) != 0 || StrengthAt(20) != 100 || StrengthAt(26) != -30 || StrengthAt(28) != -60 || StrengthAt(32) != 60 || StrengthAt(36) != 0)
    throw new Exception("Brightness instant/ramp/hold/sign crossing/target reset mismatch");
foreach (int strength in new[] { -100, -50, 0, 50, 100 })
{
    var (tone, alpha) = MobileFilters.BrightnessOverlay(strength);
    foreach (int input in new[] { 0, 80, 128, 255 })
    {
        double output = input * (1 - alpha / 255.0) + tone * alpha / 255.0;
        double expected = input * (1 - Math.Abs(strength) / 100.0) + (strength > 0 ? 255 : 0) * Math.Abs(strength) / 100.0;
        if (Math.Abs(output - expected) > .51) throw new Exception("Brightness color blending differs from PC");
    }
}
Console.WriteLine("Filters: signed brightness, background/all targets, finite validation, exact timing and PC black/white blend: OK");
int blurColor=unchecked((int)0xff6e39c4);
int[] solidBlur=HyperLane.MobileBlurPixels.Create([blurColor],1,1);
if(solidBlur.Length!=1920*1080||solidBlur.Any(pixel=>pixel!=blurColor))throw new Exception("Native blur changed a solid image or output dimensions");
Console.WriteLine("Native blur: Android-equivalent 120x68 proxy and 1920x1080 output preserve solid colors: OK");
foreach(int lanes in new[]{3,4,6,8,12})
{
    var d=new MobileDifficulty{Lanes=lanes};
    var p=MobileProjection.At(d,0);
    for(int lane=0;lane<lanes;lane++)foreach(double z in new[]{0.0,5.0,25.0})
    {
        double x=1.15-(lane+.5)*2.3/lanes;
        if(!p.Project(x,0,z,out float sx,out float sy)||p.HitLane(lanes,sx,sy)!=lane)throw new Exception("Lane touch projection mismatch");
    }
    if(p.HitLane(lanes,-1,500)!=-1||p.HitLane(lanes,0,0)!=-1)throw new Exception("Lane touch accepted outside lane");
}
if(new HyperLane.iOS.IosSettings().LaneTouch)throw new Exception("Default must be panel touch");
var touchSettings=new HyperLane.iOS.IosSettings{LaneTouch=true};
var touchReload=System.Text.Json.JsonSerializer.Deserialize<HyperLane.iOS.IosSettings>(System.Text.Json.JsonSerializer.Serialize(touchSettings));
if(touchReload?.LaneTouch!=true)throw new Exception("Touch selection did not survive serialization");
Console.WriteLine("Lane touch projection, multiple heights, outside rejection and default panel mode: OK");
{
    var chart=MobileChart.Parse("BPM: 120\nSONG: common.wav\nOFFSET: 0.2\nPREVIEW: 2-6\nCHART_DESIGNER: Common\nDIFFICULTY: Normal\nLEVEL: 5\n1,1\nDIFFICULTY: Master\nLEVEL: 11.5\nDIFFICULTY_CHART_DESIGNER: Master Person\nDIFFICULTY_SONG: master.wav\nDIFFICULTY_OFFSET: -0.3\nDIFFICULTY_PREVIEW: 8-12\n1,1\nLAYER: 2\n1,2\nDIFFICULTY: Hard\nDIFFICULTY_OFFSET: 0\n1,1\n");
    var normal=chart.Difficulties[0];var master=chart.Difficulties[1];var hard=chart.Difficulties[2];
    if(chart.AudioFor(normal)!="common.wav"||chart.AudioFor(master)!="master.wav"||chart.DesignerFor(master)!="Master Person"||chart.DesignerFor(normal)!="Common")throw new Exception("Difficulty media/designer inheritance");
    if(chart.OffsetFor(normal)!=.2||chart.OffsetFor(master)!=-.3||chart.OffsetFor(hard)!=0)throw new Exception("Difficulty offset override including zero");
    if(chart.PreviewStartFor(master)!=8||chart.PreviewEndFor(master)!=12||chart.PreviewStartFor(normal)!=2)throw new Exception("Difficulty preview inheritance");
    if(master.Notes.Any(n=>Math.Abs(n.StartSeconds-.2)>1e-8)||Math.Abs(normal.Notes[0].StartSeconds-.7)>1e-8)throw new Exception("Difficulty offset note timing/layers");
    var timing=MobilePracticeTiming.At(chart,master,8,0);if(Math.Abs(timing.AnchorChart-3.7)>1e-8)throw new Exception("Difficulty practice offset");
    Console.WriteLine("Per-difficulty audio, offset, preview, designer, zero override, layers and practice: OK");
}
{
    string root=Path.Combine("G:/HL ファイル関連/workspace/mobile-parity-20261016","chapter-tests-"+Guid.NewGuid().ToString("N"),"song");
    var library=new List<MobileSong>();
    for(int i=1;i<=5;i++){string chapter=Path.Combine(root,"chapter"+i);Directory.CreateDirectory(chapter);File.WriteAllText(Path.Combine(chapter,"chapter.json"),"{\"name\":\"Chapter "+i+"\",\"image\":\"header.jpg\"}");library.Add(new MobileSong(Path.Combine(chapter,"songA","chart.2d"),Path.Combine(chapter,"songA"),MobileChart.Parse("DIFFICULTY: Master\n0,1\n")));}
    library.Add(new MobileSong(Path.Combine(root,"SIDE ARCHIVE","songB","chart.2d"),Path.Combine(root,"SIDE ARCHIVE","songB"),MobileChart.Parse("DIFFICULTY: Master\n0,1\n")));
    var menu=new MobileChapterMenu();menu.Refresh(root);if(menu.Entries[0].Name!="ALL chart"||menu.Entries.Count!=9||menu.Songs(library).Count()!=6)throw new Exception("Chapter discovery/all songs");
    menu.Move(2);menu.ActivateFilter();if(menu.Songs(library).Count()!=1)throw new Exception("Chapter filtering");
    if(menu.Expansion(2)!=0)throw new Exception("Chapter expansion must wait");
    menu.Move(4);menu.ActivateFilter();if(menu.Songs(library).Count()!=1)throw new Exception("Archive excludes chapters");
    if(menu.Songs(library,true).Count()!=6)throw new Exception("Practice and rating use full library");
    menu.Move(100);if(menu.Index!=8||menu.Entries[8].Kind!="practice")throw new Exception("Chapter menu endpoint");
    Console.WriteLine("Chapter metadata, ALL chart, filters, archive, delay, modes and bounded navigation: OK");
}
foreach (string file in args)
{
    MobileChart chart = MobileChart.Parse(File.ReadAllText(file));
    if (chart.Difficulties.Count == 0) throw new Exception($"No difficulty: {file}");
    Console.WriteLine($"{chart.Title}: {chart.Difficulties.Count} difficulties, {chart.Difficulties.Sum(d => d.Notes.Count)} notes");
}
