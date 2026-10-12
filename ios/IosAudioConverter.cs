using AVFoundation;
using Foundation;
using System.Security.Cryptography;
using System.Text;

namespace HyperLane.iOS;

internal static class IosAudioConverter
{
    internal static async Task<string> ToM4aAsync(string source, string cacheDirectory)
    {
        var info = new FileInfo(source);
        if (!info.Exists) throw new FileNotFoundException("譜面で指定された音源がありません。", source);
        string fingerprint = $"m4a-v1\n{source}\n{info.Length}\n{info.LastWriteTimeUtc.Ticks}";
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
        Directory.CreateDirectory(cacheDirectory);
        string destination = Path.Combine(cacheDirectory, key + ".m4a");
        if (File.Exists(destination) && new FileInfo(destination).Length > 0) return destination;
        string temporaryM4a = Path.Combine(cacheDirectory, Guid.NewGuid().ToString("N") + ".m4a");
        string temporaryWave = Path.Combine(cacheDirectory, Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            string exportSource = source;
            if (Path.GetExtension(source).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Run(() => VorbisWave.Decode(source, temporaryWave));
                exportSource = temporaryWave;
            }
            using var asset = AVUrlAsset.Create(NSUrl.FromFilename(exportSource));
            using var export = new AVAssetExportSession(asset, AVAssetExportSession.PresetAppleM4A);
            export.OutputUrl = NSUrl.FromFilename(temporaryM4a);
            export.OutputFileType = "com.apple.m4a-audio";
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            export.ExportAsynchronously(() => completion.TrySetResult());
            await completion.Task;
            if (export.Status != AVAssetExportSessionStatus.Completed || !File.Exists(temporaryM4a))
                throw new InvalidDataException(export.Error?.LocalizedDescription ?? "M4Aへの変換に失敗しました。");
            using var probe = AVAudioPlayer.FromUrl(NSUrl.FromFilename(temporaryM4a), out var error);
            if (probe is null || probe.Duration <= 0)
                throw new InvalidDataException(error?.LocalizedDescription ?? "変換したM4Aを読み込めません。");
            File.Move(temporaryM4a, destination, overwrite: true);
            return destination;
        }
        finally
        {
            // Only our own unique temporary conversion files, never original music.
            if (File.Exists(temporaryWave)) File.Delete(temporaryWave);
            if (File.Exists(temporaryM4a)) File.Delete(temporaryM4a);
        }
    }
}
