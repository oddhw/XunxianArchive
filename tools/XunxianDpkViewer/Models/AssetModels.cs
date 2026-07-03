using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;

namespace XunxianDpkViewer.Models;

public enum AssetKind
{
    Image,
    Sound,
    Model,
    Other
}

public sealed record DpkEntry(string Path, uint RootBlock);

public sealed record AssetEntry(
    string ArchivePath,
    DpkEntry Entry,
    AssetKind Kind)
{
    public string Name => System.IO.Path.GetFileName(Entry.Path);
    public string Extension => System.IO.Path.GetExtension(Entry.Path).ToLowerInvariant();
    public string ArchiveName => System.IO.Path.GetFileName(ArchivePath);
    public string DisplayPath => $"{ArchiveName}  /  {Entry.Path}";
}

public sealed record FolderNodeInfo(
    string Name,
    string ArchivePath,
    string InternalPath)
{
    public string ArchiveName => System.IO.Path.GetFileName(ArchivePath);
    public string DisplayPath => string.IsNullOrEmpty(InternalPath)
        ? ArchiveName
        : $"{ArchiveName}  /  {InternalPath}";

    public override string ToString() => Name;
}

public sealed class AssetItemViewModel : INotifyPropertyChanged
{
    private ImageSource? _thumbnail;
    private string _subtitle;

    public AssetItemViewModel(AssetEntry asset)
    {
        Asset = asset;
        _subtitle = asset.Entry.Path;
    }

    public AssetEntry Asset { get; }
    public bool IsThumbnailLoading { get; set; }
    public string Name => Asset.Name;
    public string Path => Asset.Entry.Path;
    public string ArchiveName => Asset.ArchiveName;
    public string Glyph => Asset.Kind switch
    {
        AssetKind.Sound => "\uE8D6",
        AssetKind.Model => "\uE809",
        _ => "\uEB9F"
    };

    public string Subtitle
    {
        get => _subtitle;
        set => SetField(ref _subtitle, value);
    }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set => SetField(ref _thumbnail, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed record PmfMesh(
    IReadOnlyList<System.Numerics.Vector3> Vertices,
    IReadOnlyList<System.Numerics.Vector2> TextureCoordinates,
    IReadOnlyList<ushort> Indices,
    uint Version,
    uint VertexFlags,
    uint UvChannelCount,
    uint DeclaredTriangleCount);
