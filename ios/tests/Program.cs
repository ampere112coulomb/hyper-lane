using HyperLane.iOS;
using System.Text;

if (args.Length != 2) throw new ArgumentException("Usage: AudioChecks mono.ogg stereo.ogg");
for (int index = 0; index < args.Length; index++)
{
    string source = Path.GetFullPath(args[index]);
    byte[] before = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source));
    string destination = Path.Combine(Path.GetTempPath(), "hyperlane-audio-test-" + Guid.NewGuid() + ".wav");
    try
    {
        VorbisWave.Decode(source, destination);
        using var reader = new BinaryReader(File.OpenRead(destination));
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new Exception("RIFF missing");
        int riffSize = reader.ReadInt32();
        if (riffSize != reader.BaseStream.Length - 8) throw new Exception("RIFF length mismatch");
        reader.BaseStream.Position = 20;
        if (reader.ReadInt16() != 1 || reader.ReadInt16() != index + 1) throw new Exception("PCM/channel mismatch");
        int rate = reader.ReadInt32();
        reader.BaseStream.Position = 40; int dataBytes = reader.ReadInt32();
        if (dataBytes != reader.BaseStream.Length - 44) throw new Exception("Data length mismatch");
        double duration = dataBytes / (double)(rate * (index + 1) * 2);
        if (Math.Abs(duration - 2) > .02) throw new Exception("Timing/duration mismatch");
        long energy = 0; while (reader.BaseStream.Position < reader.BaseStream.Length) energy += Math.Abs((int)reader.ReadInt16());
        if (energy == 0) throw new Exception("Silent conversion");
        if (!before.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source)))) throw new Exception("Original modified");
        bool rejected = false;
        try { VorbisWave.Decode(source, destination); } catch (IOException) { rejected = true; }
        if (!rejected) throw new Exception("Unexpected overwrite");
        Console.WriteLine($"{index + 1}ch Vorbis: PCM16 header, duration, non-silence, original preservation, overwrite guard OK");
    }
    finally { if (File.Exists(destination)) File.Delete(destination); }
}
