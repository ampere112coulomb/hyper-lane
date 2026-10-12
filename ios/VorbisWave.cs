using System.Text;
using NVorbis;

namespace HyperLane.iOS;

internal static class VorbisWave
{
    // Decode in small blocks: do not keep the whole song in memory.
    internal static void Decode(string input, string output)
    {
        using var vorbis = new VorbisReader(input);
        int channels = vorbis.Channels, rate = vorbis.SampleRate;
        if (channels is < 1 or > 2 || rate is < 8000 or > 192000)
            throw new InvalidDataException("OGGのチャンネル数またはサンプルレートが未対応です。");
        using var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
        using var writer = new BinaryWriter(file, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(0);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)channels); writer.Write(rate);
        writer.Write(rate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(0);
        var samples = new float[4096 * channels];
        long bytes = 0;
        int count;
        while ((count = vorbis.ReadSamples(samples, 0, samples.Length)) > 0)
        {
            bytes += count * 2L;
            if (bytes > 512L * 1024 * 1024) throw new InvalidDataException("変換後の音源が512MiBを超えます。");
            for (int i = 0; i < count; i++)
                writer.Write((short)Math.Clamp((int)Math.Round(Math.Clamp(samples[i], -1f, 1f) * 32767), -32768, 32767));
        }
        if (bytes == 0 || bytes % (channels * 2) != 0) throw new InvalidDataException("OGGに有効な音声がありません。");
        writer.Flush(); file.Position = 4; writer.Write((int)(36 + bytes));
        file.Position = 40; writer.Write((int)bytes);
    }
}
