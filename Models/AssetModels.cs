using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;

namespace XunxianDpkViewer.Models;

public enum AssetKind
{
    Image,
    Sound,
    Model,
    Font,
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
    private readonly string _name;

    public AssetItemViewModel(AssetEntry asset, string? displayName = null, string? subtitle = null)
    {
        Asset = asset;
        _name = displayName ?? asset.Name;
        _subtitle = subtitle ?? asset.Entry.Path;
    }

    public AssetItemViewModel(CompositeModelEntry composite)
    {
        Composite = composite;
        _name = composite.Name;
        _subtitle = $"完整组合 · {composite.Parts.Count:N0} 个 PMF 部件";
    }

    public AssetEntry? Asset { get; }
    public CompositeModelEntry? Composite { get; }
    public bool IsThumbnailLoading { get; set; }
    public string Name => _name;
    public string Path => Asset?.Entry.Path ?? Composite?.ConfigAsset.Entry.Path ?? string.Empty;
    public string ArchiveName => Asset?.ArchiveName ?? Composite?.ConfigAsset.ArchiveName ?? string.Empty;
    public string Glyph => Composite is not null ? "\uE902" : Asset?.Kind switch
    {
        AssetKind.Sound => "\uE8D6",
        AssetKind.Model => "\uE809",
        AssetKind.Font => "\uE8D2",
        AssetKind.Other => "\uE8A5",
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

public sealed record DecodedTexture(
    int Width,
    int Height,
    byte[] BgraPixels,
    string Format);

public sealed record ModelTextureBinding(
    AssetEntry TextureAsset,
    string MapType,
    string MaterialName,
    string ConfigPath)
{
    public string DisplayName => string.IsNullOrWhiteSpace(MaterialName)
        ? $"{MapType} · {TextureAsset.Name}"
        : $"{MaterialName} · {MapType} · {TextureAsset.Name}";

    public override string ToString() => DisplayName;
}

public sealed record CompositeModelPart(
    AssetEntry MeshAsset,
    string MaterialName,
    ModelTextureBinding? TextureBinding);

public sealed record CompositeModelEntry(
    string Name,
    AssetEntry ConfigAsset,
    IReadOnlyList<CompositeModelPart> Parts)
{
    public string DisplayPath => $"{ConfigAsset.ArchiveName}  /  {ConfigAsset.Entry.Path}";
}

public sealed record ModelRenderPart(
    string Name,
    PmfMesh Mesh,
    DecodedTexture? Texture,
    string TextureName);
