using HyperLane;
using HyperLane.Android;
using System.Text.Json;
using UIKit;

namespace HyperLane.iOS;
internal sealed partial class GameController
{
    private ReplayData? replay;private bool replaying;private int replayIndex;private readonly List<ReplayInput> recorded=[];
    private (double Speed,int Offset,bool Auto)? originalReplaySettings;
    private string ReplayPath(MobileSong song,MobileDifficulty diff)=>Path.Combine(documents,"replay",Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(MigrationStage.Key(song,diff))))+".json");
    private void RestoreReplay(){if(originalReplaySettings is {} s){settings.NoteSpeed=s.Speed;settings.AudioOffsetMs=s.Offset;settings.AutoPlay=s.Auto;}originalReplaySettings=null;replaying=false;replay=null;}
    private void StartReplay()
    {
        var song=stage.Page==MigrationStage.Screen.Result?session?.Song:stage.Selected;var diff=stage.Page==MigrationStage.Screen.Result?session?.Difficulty:stage.SelectedDifficulty;if(song==null||diff==null)return;
        try{var value=JsonSerializer.Deserialize<ReplayData>(File.ReadAllText(ReplayPath(song,diff)))??throw new InvalidDataException("リプレイがありません");if(value.ChartHash!=MigrationStage.Hash(song))throw new InvalidDataException("譜面が変更されています");RestoreReplay();originalReplaySettings=(settings.NoteSpeed,settings.AudioOffsetMs,settings.AutoPlay);settings.NoteSpeed=value.NoteSpeed;settings.AudioOffsetMs=(int)value.AudioOffset;settings.AutoPlay=false;replay=value;replaying=true;practice=false;activeDan=null;Start(song,diff);}catch(Exception e){Alert("リプレイ",e.Message);}
    }
    private void BeginReplayRecording(){recorded.Clear();replayIndex=0;}
    private void RecordInput(IReadOnlySet<int> next,double time){if(practice||replaying||settings.AutoPlay||activeDan!=null)return;foreach(int lane in held.Except(next))recorded.Add(new(){Time=time,Lane=lane,Down=false});foreach(int lane in next.Except(held))recorded.Add(new(){Time=time,Lane=lane,Down=true});}
    private void AdvanceReplay(double seconds){if(!replaying||replay==null||session==null)return;while(replayIndex<replay.Inputs.Count&&replay.Inputs[replayIndex].Time<=seconds){var input=replay.Inputs[replayIndex++];if(input.Down){held.Add(input.Lane);session.Press(input.Lane,input.Time);}else{held.Remove(input.Lane);session.Release(input.Lane,input.Time);}}}
    private void SaveReplay()
    {
        if(!recordEligible||practice||replaying||settings.AutoPlay||session==null||activeDan!=null)return;string path=ReplayPath(session.Song,session.Difficulty);Directory.CreateDirectory(Path.GetDirectoryName(path)!);var data=new ReplayData{ChartHash=MigrationStage.Hash(session.Song),Title=session.Song.Chart.Title,Difficulty=session.Difficulty.Name,NoteSpeed=settings.NoteSpeed,AudioOffset=settings.AudioOffsetMs,Score=session.Score,MaxCombo=session.MaxCombo,Counts=(int[])session.Counts.Clone(),Inputs=recorded.OrderBy(i=>i.Time).ToList()};File.WriteAllText(path,JsonSerializer.Serialize(data));
    }
    private void NumberInput(string title,int value,Action<int> save)
    {
        var dialog=UIAlertController.Create(title,"数値を入力",UIAlertControllerStyle.Alert);dialog.OverrideUserInterfaceStyle=UIUserInterfaceStyle.Dark;dialog.AddTextField(t=>{t.Text=value.ToString();t.KeyboardType=UIKeyboardType.NumbersAndPunctuation;});dialog.AddAction(UIAlertAction.Create("保存",UIAlertActionStyle.Default,_=>{if(int.TryParse(dialog.TextFields![0].Text,out int n))save(n);}));dialog.AddAction(UIAlertAction.Create("キャンセル",UIAlertActionStyle.Cancel,null));PresentViewController(dialog,true,null);
    }
}
