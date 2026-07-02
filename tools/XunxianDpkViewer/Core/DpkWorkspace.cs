using XunxianDpkViewer.Models;

namespace XunxianDpkViewer.Core;

public sealed class DpkWorkspace : IDisposable
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".dds", ".tga", ".ico"
    };

    private static readonly HashSet<string> SoundExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".ogg"
    };

    private readonly Dictionary<string, DpkReader> _readers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AssetEntry> _assets = new();

    public IReadOnlyList<AssetEntry> Assets => _assets;
    public IReadOnlyCollection<string> ArchivePaths => _readers.Keys;

    public void Clear()
    {
        foreach (DpkReader reader in _readers.Values) reader.Dispose();
        _readers.Clear();
        _assets.Clear();
    }

    public void OpenClientResourceFolder(string folder)
    {
        string[] preferredArchives =
        {
            "gui.dpk", "sound.dpk", "music.dpk", "obj.dpk", "cha.dpk"
        };

        var archives = preferredArchives
            .Select(name => System.IO.Path.Combine(folder, name))
            .Where(File.Exists)
            .ToArray();
        if (archives.Length == 0)
            archives = Directory.GetFiles(folder, "*.dpk", SearchOption.TopDirectoryOnly);
        if (archives.Length == 0)
            throw new DirectoryNotFoundException("所选目录中没有 DPK 文件。");

        Clear();
        foreach (string archive in archives) AddArchive(archive);
    }

    public void OpenSingleArchive(string path)
    {
        Clear();
        AddArchive(path);
    }

    public byte[] Extract(AssetEntry asset) => _readers[asset.ArchivePath].Extract(asset.Entry);

    public void ExtractTo(AssetEntry asset, string rootFolder)
    {
        string archiveFolder = System.IO.Path.GetFileNameWithoutExtension(asset.ArchivePath);
        string relative = asset.Entry.Path.Replace('/', System.IO.Path.DirectorySeparatorChar);
        string fullRoot = System.IO.Path.GetFullPath(rootFolder);
        string target = System.IO.Path.GetFullPath(System.IO.Path.Combine(fullRoot, archiveFolder, relative));
        if (!target.StartsWith(fullRoot + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("资源路径试图越出导出目录。");
        _readers[asset.ArchivePath].ExtractTo(asset.Entry, target);
    }

    private void AddArchive(string path)
    {
        string fullPath = System.IO.Path.GetFullPath(path);
        if (_readers.ContainsKey(fullPath)) return;
        var reader = new DpkReader(fullPath);
        try
        {
            IReadOnlyList<DpkEntry> entries = reader.ReadEntries();
            _readers.Add(fullPath, reader);
            foreach (DpkEntry entry in entries)
                _assets.Add(new AssetEntry(fullPath, entry, Classify(entry.Path)));
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    private static AssetKind Classify(string path)
    {
        string extension = System.IO.Path.GetExtension(path);
        if (ImageExtensions.Contains(extension)) return AssetKind.Image;
        if (SoundExtensions.Contains(extension)) return AssetKind.Sound;
        if (extension.Equals(".pmf", StringComparison.OrdinalIgnoreCase)) return AssetKind.Model;
        return AssetKind.Other;
    }

    public void Dispose() => Clear();
}
