using System.Text.Json;

namespace HyperLane.iOS;

internal sealed class IosSettings
{
    public double NoteSpeed { get; set; } = 1.6;
    public int AudioOffsetMs { get; set; }
    public int BgmVolume { get; set; } = 80;
    public int SeVolume { get; set; } = 80;
    public bool AutoPlay { get; set; }
    public bool AutoNoteSound { get; set; } = true;
    public int LaneTransparency { get; set; }
    public int LaneCover { get; set; }
    public int BackgroundBrightness { get; set; } = 100;
    public int KeyBeamOpacity { get; set; }
    public int KeyBeamLength { get; set; } = 50;
    public bool PreferMusicVideo { get; set; } = true;
    public int PreviewVolume { get; set; } = 40;
    public int PreviewBackgroundBrightness { get; set; } = 100;
    public int StartMarginMs { get; set; } = 1300;
    public int FeverAuraBrightness { get; set; } = 100;
    public int TouchHeight { get; set; } = 50;
    internal static IosSettings Load(string path)
    {
        try { return JsonSerializer.Deserialize<IosSettings>(File.ReadAllText(path)) ?? new(); }
        catch (IOException) { return new(); }
        catch (JsonException) { return new(); }
    }
    internal void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this));
}
