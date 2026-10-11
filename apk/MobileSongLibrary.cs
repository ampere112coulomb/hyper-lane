using System.IO.Compression;

namespace HyperLane.Android;

internal sealed record MobileSong(string ChartPath, string Folder, MobileChart Chart);

internal sealed class MobileSongLibrary
{
    private readonly string songRoot;

    public MobileSongLibrary(string filesDirectory)
    {
        songRoot = Path.Combine(filesDirectory, "song");
        Directory.CreateDirectory(songRoot);
    }

    public IReadOnlyList<MobileSong> Scan()
    {
        var result = new List<MobileSong>();
        var danRoots=Directory.EnumerateFiles(songRoot,"*",SearchOption.AllDirectories)
            .Where(p=>Path.GetFileName(p).Equals("Dan.json",StringComparison.OrdinalIgnoreCase))
            .Select(p=>Path.GetDirectoryName(p)!+Path.DirectorySeparatorChar).ToArray();
        foreach (string path in Directory.EnumerateFiles(songRoot, "*.2d", SearchOption.AllDirectories)
                     .OrderBy(p => Path.GetRelativePath(songRoot, p), StringComparer.OrdinalIgnoreCase))
        {
            if(danRoots.Any(root=>path.StartsWith(root,StringComparison.OrdinalIgnoreCase)))continue;
            try
            {
                MobileChart chart = MobileChart.Parse(File.ReadAllText(path));
                if (chart.Difficulties.Count > 0)
                    result.Add(new MobileSong(path, Path.GetDirectoryName(path)!, chart));
            }
            catch { /* A broken chart does not hide other songs. */ }
        }
        return result;
    }

    public void ImportZip(Stream input, string name)
    {
        string safeName = Path.GetFileNameWithoutExtension(name);
        foreach (char ch in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(ch, '_');
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "import";
        string target = Path.Combine(songRoot, safeName + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(target);
        string prefix = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        long expanded = 0;
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) continue;
            if (Path.IsPathRooted(entry.FullName)) throw new InvalidDataException("Absolute ZIP path");
            string destination = Path.GetFullPath(Path.Combine(target, entry.FullName.Replace('\\','/').Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe ZIP path");
            expanded += entry.Length;
            if (expanded > 1_073_741_824L) throw new InvalidDataException("ZIP exceeds 1 GiB");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using Stream source = entry.Open();
            using FileStream output = File.Create(destination);
            source.CopyTo(output);
        }
    }
}
