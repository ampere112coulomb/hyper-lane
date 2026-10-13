using AVFoundation;

namespace HyperLane.iOS;

internal sealed partial class GameController
{
    private int appliedBufferFrames = -1;
    private double appliedBufferRate;
    private void ApplyAudioBuffer()
    {
        settings.AudioBufferFrames = Math.Clamp(settings.AudioBufferFrames, 64, 4096);
        var audioSession = AVAudioSession.SharedInstance();
        double rate = audioSession.SampleRate > 0 ? audioSession.SampleRate : 48000;
        if (appliedBufferFrames != settings.AudioBufferFrames || appliedBufferRate != rate)
        {
            if (audioSession.SetPreferredIOBufferDuration(settings.AudioBufferFrames / rate, out var error))
            {
                appliedBufferFrames = settings.AudioBufferFrames;
                appliedBufferRate = rate;
            }
            else
                Console.Error.WriteLine("HL buffer request failed: " + error?.LocalizedDescription);
        }
        UpdateAudioBufferStatus();
    }

    private void UpdateAudioBufferStatus()
    {
        var s = AVAudioSession.SharedInstance();
        stage.AudioBufferStatus = $"実測 {s.IOBufferDuration * s.SampleRate:0} frames / {s.IOBufferDuration * 1000:0.00} ms";
        Console.Error.WriteLine($"HL audio buffer: requested={settings.AudioBufferFrames}, {stage.AudioBufferStatus}, sampleRate={s.SampleRate}");
    }
}
