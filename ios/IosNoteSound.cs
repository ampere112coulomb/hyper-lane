using AVFoundation;
using Foundation;

namespace HyperLane.iOS;

// A single persistent engine and predecoded PCM, no file seeks/decoding per hit.
internal sealed class IosNoteSound : IDisposable
{
    private readonly AVAudioEngine engine = new();
    private readonly AVAudioPlayerNode[] voices = new AVAudioPlayerNode[8];
    private AVAudioPcmBuffer? buffer;
    private int nextVoice;
    private bool active;

    internal IosNoteSound(string path)
    {
        try
        {
            using var file = new AVAudioFile(NSUrl.FromFilename(path), out var error);
            if (error is not null || file.Length <= 0 || file.Length > file.ProcessingFormat.SampleRate * 5)
                throw new InvalidDataException(error?.LocalizedDescription ?? "SE音源の長さが不正です。");
            buffer = new AVAudioPcmBuffer(file.ProcessingFormat, checked((uint)file.Length));
            if (!file.ReadIntoBuffer(buffer, out error))
                throw new InvalidDataException(error?.LocalizedDescription ?? "SEのPCM読み込みに失敗しました。");
            for (int i = 0; i < voices.Length; i++)
            {
                var node = new AVAudioPlayerNode(); voices[i] = node;
                engine.AttachNode(node);
                engine.Connect(node, engine.MainMixerNode, file.ProcessingFormat);
            }
            engine.Prepare();
        }
        catch { Dispose(); throw; }
    }

    internal void Begin(int volume)
    {
        if (buffer is null) return;
        foreach (var voice in voices) voice.Volume = Math.Clamp(volume, 0, 100) / 100f * .5f;
        if (active && engine.Running) return;
        if (!engine.StartAndReturnError(out var error))
            throw new InvalidOperationException(error?.LocalizedDescription ?? "SEエンジンを開始できません。");
        foreach (var voice in voices) voice.Play();
        nextVoice = 0; active = true;
    }

    internal void Play()
    {
        if (!active || buffer is null) return;
        var voice = voices[nextVoice++ % voices.Length];
        voice.ScheduleBuffer(buffer, null, AVAudioPlayerNodeBufferOptions.Interrupts, null);
    }

    internal void Pause()
    {
        active = false;
        foreach (var voice in voices) voice?.Stop();
        engine.Pause();
    }

    public void Dispose()
    {
        active = false; engine.Stop();
        foreach (var voice in voices) voice?.Dispose();
        engine.Dispose(); buffer?.Dispose(); buffer = null;
    }
}
