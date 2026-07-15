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
    MbTable,
    DungeonSummary,
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
        AssetKind.MbTable => "\uE8A5",
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

public sealed class MbTableViewModel
{
    public MbTableViewModel(
        AssetEntry asset,
        string tableName,
        string path,
        string summary,
        IReadOnlyList<string> headers,
        IReadOnlyList<int> activeColumns,
        IReadOnlyList<MbRecordViewModel> records)
    {
        Asset = asset;
        TableName = tableName;
        Path = path;
        Summary = summary;
        Headers = headers;
        ActiveColumns = activeColumns;
        Records = records;
    }

    public AssetEntry Asset { get; }
    public string TableName { get; }
    public string Path { get; }
    public string Summary { get; }
    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<int> ActiveColumns { get; }
    public IReadOnlyList<MbRecordViewModel> Records { get; }
}

public sealed class MbRecordViewModel
{
    public MbRecordViewModel(int sourceRow, string title, string previewText, string[] row)
    {
        SourceRow = sourceRow;
        Title = title;
        PreviewText = previewText;
        Row = row;
    }

    public int SourceRow { get; }
    public string SourceRowText => $"#{SourceRow:N0}";
    public string Title { get; }
    public string PreviewText { get; }
    public string[] Row { get; }
}

public sealed class MbFieldViewModel
{
    public MbFieldViewModel(string name, string value, string note, int columnIndex)
    {
        Name = name;
        Value = value;
        Note = note;
        ColumnIndex = columnIndex;
    }

    public string Name { get; }
    public string Value { get; }
    public string Note { get; }
    public int ColumnIndex { get; }
    public string IndexText => $"字段 {ColumnIndex + 1}";
}

public sealed class DungeonSummaryViewModel
{
    public DungeonSummaryViewModel(string name, string subtitle, IReadOnlyList<DungeonMonsterViewModel> monsters)
    {
        Name = name;
        Subtitle = subtitle;
        Monsters = monsters;
    }

    public string Name { get; }
    public string Subtitle { get; }
    public IReadOnlyList<DungeonMonsterViewModel> Monsters { get; }
}

public sealed class DungeonMonsterViewModel : INotifyPropertyChanged
{
    private ImageSource? _portrait;

    public string Name { get; init; } = string.Empty;
    public string RoleId { get; init; } = string.Empty;
    public string FightId { get; init; } = string.Empty;
    public string PicId { get; init; } = string.Empty;
    public string IconName { get; init; } = string.Empty;
    public string KindText { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string CompactStats { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
    public string HiddenSummary { get; init; } = string.Empty;
    public string SourceIds { get; set; } = string.Empty;
    public int SortRank { get; init; }
    public IReadOnlyList<StatLineViewModel> Stats { get; init; } = Array.Empty<StatLineViewModel>();
    public IReadOnlyList<DropLineViewModel> Drops { get; init; } = Array.Empty<DropLineViewModel>();

    public ImageSource? Portrait
    {
        get => _portrait;
        set
        {
            if (Equals(_portrait, value)) return;
            _portrait = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Portrait)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record StatLineViewModel(string Label, string Value, string Note);

public sealed record DropLineViewModel(string Name, string Detail);

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
