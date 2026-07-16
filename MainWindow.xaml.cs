using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;
using XunxianDpkViewer.Controls;
using XunxianDpkViewer.Core;
using XunxianDpkViewer.Models;

namespace XunxianDpkViewer;

public sealed partial class MainWindow : Window
{
    private const string AppVersion = "2.1";
    private const string AppAuthor = "黑风岭-梵心似火";
    private readonly DpkWorkspace _workspace = new();
    private List<AssetItemViewModel> _items = new();
    private readonly DispatcherTimer _searchTimer;
    private readonly MediaPlayer _mediaPlayer = new();
    private readonly MediaPlayerElement _audioPlayer;
    private readonly ModelPreviewControl _modelPreview;
    private List<AssetEntry> _filteredAssets = new();
    private AssetKind _currentKind = AssetKind.Image;
    private AssetEntry? _selectedAsset;
    private CompositeModelEntry? _selectedComposite;
    private MbTableViewModel? _currentMbTableView;
    private List<DungeonSummaryViewModel> _dungeonSummaries = new();
    private DungeonSummaryViewModel? _selectedDungeonSummary;
    private bool _dungeonSummariesBuilt;
    private int _sortMode;
    private int _thumbnailGeneration;
    private bool _isBusy;
    private bool _modelExpanded;
    private bool _buildingFolderTree;
    private bool _multiSelectMode;
    private bool _settingModelTextureSelection;
    private FolderNodeInfo? _selectedFolder;
    private int _previewGeneration;
    private readonly Dictionary<string, string> _mbSearchTextCache = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string[]>? _legendEquipAtbs;
    private Dictionary<string, string[]>? _legendEquipAtbValues;
    private Dictionary<string, string[]>? _chaFightRows;
    private Dictionary<string, string[]>? _chaPicRows;
    private Dictionary<string, string[]>? _stateDataRows;
    private Dictionary<string, string[]>? _stateGroupRows;
    private Dictionary<string, string[]>? _chaListRows;
    private Dictionary<string, string[]>? _itemRandRows;
    private Dictionary<string, string>? _itemNameById;
    private Dictionary<string, List<string[]>>? _chaSkillRowsByPicId;

    private static IReadOnlySet<string> Ids(params string[] ids) =>
        new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> Labels(params (string Id, string Label)[] labels) =>
        labels.ToDictionary(item => item.Id, item => item.Label, StringComparer.OrdinalIgnoreCase);

    private static readonly DungeonDefinition[] DungeonDefinitions =
    {
        new("葬龙渊", "170 级起 · 龙宫线副本",
            new[] { "葬龙渊", "zanglongyuan", "fb_zanglongyuan" },
            new[] { "41412", "41413", "41414", "41415", "41416", "41417", "41418", "41419", "41420", "41421" },
            new[] { "41412" },
            Array.Empty<IntRange>(),
            Labels(("41412", "困难首领"))),
        new("寒潭禁地", "180 级起 · 六百里号山",
            new[] { "寒潭禁地", "fb_liubailihaoshan" },
            new[] { "41512", "41513", "41514", "41531", "41532", "41534" },
            new[] { "41512", "41513", "41514", "41532" },
            Array.Empty<IntRange>(),
            Labels(
                ("41512", "首领"),
                ("41513", "首领"),
                ("41514", "首领"),
                ("41531", "宝箱/秘宝"),
                ("41532", "首领"),
                ("41534", "小怪")),
            MechanismRoleIds: Ids("41531")),
        new("玄水塔", "180 级起 · 封魔塔",
            new[] { "玄水塔", "封魔塔·玄水塔", "fb_shuita", "玄水峰" },
            new[] { "30501", "30502", "30503", "30504" },
            new[] { "30501", "30502", "30503", "30504" },
            Array.Empty<IntRange>(),
            Labels(
                ("30501", "首领"),
                ("30502", "首领"),
                ("30503", "首领"),
                ("30504", "首领"))),
        new("响马寨", "210 级起 · 高昌县",
            new[] { "响马寨", "fb_gaochangxian", "gcx_xiangmazhai" },
            new[] { "42055", "42056", "42057", "42058", "42059", "42060", "42061", "42062", "42063", "42064", "42065", "42077", "42078", "42080" },
            new[] { "42055", "42056", "42057", "42058", "42059" },
            Array.Empty<IntRange>(),
            Labels(
                ("42055", "首领"),
                ("42056", "首领"),
                ("42057", "首领"),
                ("42058", "首领"),
                ("42059", "首领"))),
        new("千秋莲华阵·天权阵眼", "210 级起 · 元寿山",
            new[] { "千秋莲华阵·天权阵眼", "fb_ssjshangu" },
            new[] { "42185" },
            new[] { "42185" },
            Array.Empty<IntRange>(),
            Labels(("42185", "首领"))),
        new("千秋莲华阵·天枢阵眼", "210 级起 · 元寿山",
            new[] { "千秋莲华阵·天枢阵眼", "fb_ssjshanmen" },
            new[] { "42194" },
            new[] { "42194" },
            Array.Empty<IntRange>(),
            Labels(("42194", "首领"))),
        new("千秋莲华阵·天玑阵眼", "210 级起 · 元寿山",
            new[] { "千秋莲华阵·天玑阵眼", "fb_ssjshulin" },
            new[] { "42192" },
            new[] { "42192" },
            Array.Empty<IntRange>(),
            Labels(("42192", "首领"))),
        new("千秋莲华阵·天璇阵眼", "210 级起 · 元寿山",
            new[] { "千秋莲华阵·天璇阵眼", "fb_ssjxiulianchang" },
            new[] { "42195" },
            new[] { "42195" },
            Array.Empty<IntRange>(),
            Labels(("42195", "首领"))),
        new("桃香谷", "210 级起 · 万寿山",
            new[] { "桃香谷", "万寿山·桃香谷", "fb_wsstaoxianggu" },
            new[] { "31512", "31513", "31514", "31515", "31516", "31517", "31518", "31519", "31520", "31521", "31522" },
            new[] { "31515", "31522" },
            Array.Empty<IntRange>(),
            Labels(
                ("31515", "首领"),
                ("31522", "首领"))),
        new("盘丝洞", "240 级起 · 乌鸡国",
            new[] { "盘丝洞", "乌鸡国·盘丝洞", "fb_pansidong" },
            new[] { "31577", "31578", "31579", "31580", "31581", "31585", "31586" },
            new[] { "31577", "31578", "31585", "31586" },
            Array.Empty<IntRange>(),
            Labels(
                ("31577", "困难首领"),
                ("31578", "困难首领"),
                ("31585", "简单首领"),
                ("31586", "简单首领"))),
        new("七星定魂", "250 级起 · 乌鸡国",
            new[] { "七星定魂", "fb_wujiguo1" },
            new[]
            {
                "31648", "31649", "31650", "31651", "31652", "31653", "31654", "31655",
                "31656", "31657", "31658", "31659", "31660", "31661", "31662"
            },
            new[] { "31648", "31649", "31650", "31651", "31652" },
            Array.Empty<IntRange>(),
            Labels(
                ("31648", "首领/核心"),
                ("31649", "首领"),
                ("31650", "首领"),
                ("31651", "首领"),
                ("31652", "首领"))),
        new("车迟国斗法", "260 级起",
            new[] { "车迟国斗法", "fb_chechiguo" },
            new[] { "31697", "31698", "31699", "31731", "31732", "31733" },
            new[] { "31697", "31698", "31699", "31731", "31732", "31733" },
            Array.Empty<IntRange>(),
            Labels(
                ("31697", "困难首领"),
                ("31698", "困难首领"),
                ("31699", "困难首领"),
                ("31731", "简单首领"),
                ("31732", "简单首领"),
                ("31733", "简单首领")))
    };

    private bool UseBeginnerNames => false;

    static MainWindow()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public MainWindow()
    {
        InitializeComponent();
        _audioPlayer = new MediaPlayerElement { AreTransportControlsEnabled = true };
        AudioPlayerHost.Content = _audioPlayer;
        _modelPreview = new ModelPreviewControl();
        ModelPreviewHost.Content = _modelPreview;
        ExtendsContentIntoTitleBar = false;
        string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Xunxian.ico");
        if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
        DisplayArea displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        AppWindow.Resize(new SizeInt32(
            Math.Min(1600, Math.Max(1100, displayArea.WorkArea.Width - 32)),
            Math.Min(960, Math.Max(720, displayArea.WorkArea.Height - 32))));

        ImageGrid.ItemsSource = _items;
        AssetList.ItemsSource = _items;
        MbAssetList.ItemsSource = _items;
        _audioPlayer.SetMediaPlayer(_mediaPlayer);
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            ApplyFilter();
        };

        Closed += (_, _) =>
        {
            _mediaPlayer.Dispose();
            _workspace.Dispose();
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        string? explicitArgument = Environment.GetCommandLineArgs()
            .FirstOrDefault(argument => argument.StartsWith("--resource-folder=", StringComparison.OrdinalIgnoreCase));
        string? explicitFolder = explicitArgument is null
            ? null
            : explicitArgument[(explicitArgument.IndexOf('=') + 1)..].Trim('"');
        if (!string.IsNullOrWhiteSpace(explicitFolder))
        {
            await LoadResourceFolderAsync(explicitFolder);
            return;
        }

        string? rememberedFolder = UserPreferences.LoadResourceFolder();
        if (!string.IsNullOrWhiteSpace(rememberedFolder) &&
            File.Exists(System.IO.Path.Combine(rememberedFolder, "gui.dpk")))
        {
            await LoadResourceFolderAsync(rememberedFolder);
            return;
        }

        PathSetupPanel.Visibility = Visibility.Visible;
        SetStatus("首次使用，请选择《新寻仙》客户端或 res 目录");
    }

    private async Task LoadResourceFolderAsync(string folder)
    {
        if (_isBusy) return;
        SetBusy(true, "正在读取 DPK 索引…");
        try
        {
            string resourceFolder = ResolveResourceFolder(folder);
            await Task.Run(() => _workspace.OpenClientResourceFolder(resourceFolder));
            UserPreferences.SaveResourceFolder(resourceFolder);
            CurrentPathText.Text = resourceFolder;
            PathSetupPanel.Visibility = Visibility.Collapsed;
            AfterWorkspaceLoaded();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("资源目录读取失败", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoadArchiveAsync(string path)
    {
        if (_isBusy) return;
        SetBusy(true, "正在读取 DPK 索引…");
        try
        {
            await Task.Run(() => _workspace.OpenSingleArchive(path));
            CurrentPathText.Text = path;
            PathSetupPanel.Visibility = Visibility.Collapsed;
            AfterWorkspaceLoaded();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("DPK 读取失败", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void AfterWorkspaceLoaded()
    {
        _mbSearchTextCache.Clear();
        _legendEquipAtbs = null;
        _legendEquipAtbValues = null;
        _chaFightRows = null;
        _chaPicRows = null;
        _stateDataRows = null;
        _stateGroupRows = null;
        _chaListRows = null;
        _itemRandRows = null;
        _itemNameById = null;
        _chaSkillRowsByPicId = null;
        _dungeonSummaries.Clear();
        _selectedDungeonSummary = null;
        _dungeonSummariesBuilt = false;
        int images = _workspace.Assets.Count(asset => asset.Kind == AssetKind.Image);
        int sounds = _workspace.Assets.Count(asset => asset.Kind == AssetKind.Sound);
        int models = _workspace.Assets.Count(asset => asset.Kind == AssetKind.Model);
        int fonts = _workspace.Assets.Count(asset => asset.Kind == AssetKind.Font);
        int mbTables = _workspace.Assets.Count(asset => asset.Kind == AssetKind.MbTable);
        int others = _workspace.Assets.Count(asset => asset.Kind == AssetKind.Other);
        string mbSummary = mbTables > 0 ? $" · {mbTables:N0} MB表" : string.Empty;
        ArchiveSummaryText.Text = $"{_workspace.ArchivePaths.Count} 个包 · {images:N0} 图像 · {sounds:N0} 声音 · {models:N0} 模型 · {fonts:N0} 字体{mbSummary} · {others:N0} 其他";
        BatchExportButton.IsEnabled = true;
        BuildFolderTree();
        ApplyFilter();
    }

    private static string ResolveResourceFolder(string selectedFolder)
    {
        string fullPath = System.IO.Path.GetFullPath(selectedFolder);
        string nestedRes = System.IO.Path.Combine(fullPath, "res");
        if (File.Exists(System.IO.Path.Combine(fullPath, "gui.dpk"))) return fullPath;
        if (File.Exists(System.IO.Path.Combine(nestedRes, "gui.dpk"))) return nestedRes;
        throw new DirectoryNotFoundException("所选位置不是有效的新寻仙客户端目录：没有找到 res\\gui.dpk。");
    }

    private void BuildFolderTree()
    {
        _buildingFolderTree = true;
        FolderTree.RootNodes.Clear();
        _selectedFolder = null;

        TreeViewNode? firstRoot = null;
        TreeViewNode? preferredNode = null;
        string preferredArchive = _currentKind switch
        {
            AssetKind.Image => "gui.dpk",
            AssetKind.Sound => "sound.dpk",
            AssetKind.Model => "obj.dpk",
            AssetKind.Font => "font.dpk",
            AssetKind.MbTable => "mb.dpk",
            AssetKind.Other => "gfx.dpk",
            _ => string.Empty
        };
        foreach (IGrouping<string, AssetEntry> archive in _workspace.Assets
                     .Where(asset => asset.Kind == _currentKind)
                     .GroupBy(asset => asset.ArchivePath, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => string.Equals(System.IO.Path.GetFileName(group.Key), preferredArchive, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(group => System.IO.Path.GetFileName(group.Key), StringComparer.OrdinalIgnoreCase))
        {
            string archiveName = System.IO.Path.GetFileName(archive.Key);
            string archiveDisplayName = UseBeginnerNames
                ? ResourceExplanationService.GetArchiveDisplayName(archiveName)
                : archiveName;
            var root = new TreeViewNode
            {
                Content = new FolderNodeInfo(archiveDisplayName, archive.Key, string.Empty),
                IsExpanded = false
            };
            FolderTree.RootNodes.Add(root);
            firstRoot ??= root;
            if (string.Equals(archiveName, preferredArchive, StringComparison.OrdinalIgnoreCase))
                preferredNode ??= root;

            var nodesByPath = new Dictionary<string, TreeViewNode>(StringComparer.OrdinalIgnoreCase)
            {
                [string.Empty] = root
            };
            var directorySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string assetDirectory in archive.Select(asset => GetInternalDirectory(asset.Entry.Path)))
            {
                string directory = assetDirectory;
                while (directory.Length > 0 && directorySet.Add(directory))
                {
                    int parentSlash = directory.LastIndexOf('/');
                    directory = parentSlash < 0 ? string.Empty : directory[..parentSlash];
                }
            }
            IEnumerable<string> directories = directorySet
                .OrderBy(path => path.Count(character => character == '/'))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (string directory in directories)
            {
                int slash = directory.LastIndexOf('/');
                string parentPath = slash < 0 ? string.Empty : directory[..slash];
                string folderName = slash < 0 ? directory : directory[(slash + 1)..];
                string folderDisplayName = UseBeginnerNames
                    ? ResourceExplanationService.GetFolderDisplayName(folderName)
                    : folderName;
                if (!nodesByPath.TryGetValue(parentPath, out TreeViewNode? parent)) continue;

                var node = new TreeViewNode
                {
                    Content = new FolderNodeInfo(folderDisplayName, archive.Key, directory)
                };
                parent.Children.Add(node);
                nodesByPath[directory] = node;
            }

        }

        TreeViewNode? initialNode = preferredNode ?? firstRoot;
        if (initialNode?.Content is FolderNodeInfo firstFolder)
        {
            _selectedFolder = firstFolder;
            FolderTree.SelectedNode = initialNode;
            CurrentFolderText.Text = firstFolder.DisplayPath;
        }
        else
        {
            CurrentFolderText.Text = "当前分类没有资源目录";
        }
        _buildingFolderTree = false;
    }

    private void FolderTree_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        if (_buildingFolderTree || sender.SelectedNode?.Content is not FolderNodeInfo folder) return;
        _selectedFolder = folder;
        CurrentFolderText.Text = folder.DisplayPath;
        ApplyFilter();
    }

    private bool IsAssetInSelectedFolder(AssetEntry asset)
    {
        if (_selectedFolder is null) return false;
        if (!string.Equals(asset.ArchivePath, _selectedFolder.ArchivePath, StringComparison.OrdinalIgnoreCase))
            return false;

        if (_currentKind == AssetKind.MbTable && !string.IsNullOrWhiteSpace(SearchBox.Text))
            return true;

        string assetDirectory = GetInternalDirectory(asset.Entry.Path);
        if (string.IsNullOrWhiteSpace(SearchBox.Text))
            return string.Equals(assetDirectory, _selectedFolder.InternalPath, StringComparison.OrdinalIgnoreCase);

        if (_selectedFolder.InternalPath.Length == 0) return true;
        return string.Equals(assetDirectory, _selectedFolder.InternalPath, StringComparison.OrdinalIgnoreCase) ||
               assetDirectory.StartsWith(_selectedFolder.InternalPath + '/', StringComparison.OrdinalIgnoreCase);
    }

    private static string GetInternalDirectory(string entryPath)
    {
        string normalized = entryPath.Replace('\\', '/').Trim('/');
        int slash = normalized.LastIndexOf('/');
        return slash < 0 ? string.Empty : normalized[..slash];
    }

    private void ApplyFilter()
    {
        if (_currentKind == AssetKind.DungeonSummary)
        {
            RefreshDungeonSummary();
            return;
        }

        string[] terms = GetSearchTerms();
        IEnumerable<AssetEntry> query = _workspace.Assets
            .Where(asset => asset.Kind == _currentKind)
            .Where(IsAssetInSelectedFolder)
            .Where(asset => terms.Length == 0 || terms.All(term => AssetMatchesSearchTerm(asset, term)));
        query = _sortMode switch
        {
            1 => query.OrderByDescending(asset => asset.Name, NaturalStringComparer.Instance),
            2 => query.OrderBy(asset => asset.Extension, StringComparer.OrdinalIgnoreCase)
                      .ThenBy(asset => asset.Name, NaturalStringComparer.Instance),
            3 => query.OrderBy(asset => asset.Entry.Path, NaturalStringComparer.Instance),
            4 => query,
            _ => query.OrderBy(asset => asset.Name, NaturalStringComparer.Instance)
        };
        _filteredAssets = query.ToList();
        PopulateAssets();
    }

    private string[] GetSearchTerms() => SearchBox.Text
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private bool AssetMatchesSearchTerm(AssetEntry asset, string term)
    {
        string[] variants = GetSearchTermVariants(term).ToArray();
        if (variants.Any(variant =>
                asset.Entry.Path.Contains(variant, StringComparison.OrdinalIgnoreCase) ||
                asset.ArchiveName.Contains(variant, StringComparison.OrdinalIgnoreCase) ||
                (UseBeginnerNames && ResourceExplanationService.GetSearchText(asset)
                    .Contains(variant, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        return asset.Kind == AssetKind.MbTable &&
               term.Length >= 2 &&
               TryGetMbSearchText(asset, out string text) &&
               variants.Any(variant => text.Contains(variant, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryGetMbSearchText(AssetEntry asset, out string text)
    {
        string key = asset.DisplayPath;
        if (_mbSearchTextCache.TryGetValue(key, out string? cachedText))
        {
            text = cachedText;
            return text.Length > 0;
        }

        try
        {
            byte[] data = _workspace.Extract(asset);
            if (!TryDecodeTextPreview(asset, data, out text))
                text = string.Empty;
        }
        catch
        {
            text = string.Empty;
        }

        _mbSearchTextCache[key] = text;
        return text.Length > 0;
    }

    private void PopulateAssets()
    {
        _thumbnailGeneration++;
        var items = new List<AssetItemViewModel>();
        int compositeCount = 0;
        if (_currentKind == AssetKind.Model && _selectedFolder is not null && string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            IReadOnlyList<CompositeModelEntry> composites = _workspace.FindCompositeModels(
                _selectedFolder.ArchivePath,
                _selectedFolder.InternalPath);
            items.AddRange(composites.Select(composite => new AssetItemViewModel(composite)));
            compositeCount = composites.Count;
        }
        items.AddRange(_filteredAssets.Select(CreateAssetItem));
        _items = items;
        ImageGrid.ItemsSource = _items;
        AssetList.ItemsSource = _items;
        MbAssetList.ItemsSource = _items;
        EmptyPanel.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        string scope = string.IsNullOrWhiteSpace(SearchBox.Text) ? "当前文件夹" : "当前目录及子目录";
        string compositeStatus = compositeCount > 0 ? $"，其中 {compositeCount:N0} 个完整组合模型" : string.Empty;
        SetStatus($"{scope}找到 {_items.Count:N0} 个资源{compositeStatus}，已全部显示");
        UpdateSelectionUi(0);
    }

    private AssetItemViewModel CreateAssetItem(AssetEntry asset)
    {
        if (!UseBeginnerNames) return new AssetItemViewModel(asset);
        ResourceExplanation explanation = ResourceExplanationService.Explain(asset);
        if (asset.Kind == AssetKind.MbTable)
            return new AssetItemViewModel(asset, CreateMbTableListName(asset),
                $"{asset.Name} · {explanation.Purpose}");
        return new AssetItemViewModel(asset, explanation.FriendlyName,
            $"原始文件：{asset.Name} · {explanation.Purpose}");
    }

    private static string CreateMbTableListName(AssetEntry asset)
    {
        string normalizedPath = asset.Entry.Path.Replace('\\', '/');
        string[] parts = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string tableName = GetMbTableDisplayName(asset);
        if (parts.Length == 0) return tableName;
        string folderName = ResourceExplanationService.GetFolderDisplayName(parts[0]);
        return $"{tableName}（{folderName}）";
    }

    private void ImageGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.Item is not AssetItemViewModel item) return;
        _ = LoadThumbnailAsync(item, _thumbnailGeneration);
    }

    private async Task LoadThumbnailAsync(AssetItemViewModel item, int generation)
    {
        if (item.Asset is null || item.Thumbnail is not null || item.IsThumbnailLoading) return;
        item.IsThumbnailLoading = true;
        try
        {
            if (generation != _thumbnailGeneration) return;
            byte[] data = await Task.Run(() => _workspace.Extract(item.Asset));
            if (generation != _thumbnailGeneration) return;
            item.Thumbnail = await CreateBitmapAsync(data, 128);
        }
        catch
        {
            item.Subtitle = "无法生成缩略图";
        }
        finally
        {
            item.IsThumbnailLoading = false;
        }
    }

    private async void Asset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListViewBase list) return;
        int selectedCount = list.SelectedItems.Count;
        UpdateSelectionUi(selectedCount);
        AssetItemViewModel? item = e.AddedItems.OfType<AssetItemViewModel>().LastOrDefault() ??
                                   list.SelectedItems.OfType<AssetItemViewModel>().LastOrDefault();
        if (item is null)
        {
            _previewGeneration++;
            PreviewLoadingOverlay.Visibility = Visibility.Collapsed;
            _selectedAsset = null;
            ResetPreviewSelection();
            return;
        }

        int previewGeneration = ++_previewGeneration;
        PreviewLoadingOverlay.Visibility = Visibility.Collapsed;
        if (item.Composite is CompositeModelEntry composite)
        {
            _selectedAsset = null;
            _selectedComposite = composite;
            SelectedNameText.Text = composite.Name;
            SelectedPathText.Text = composite.DisplayPath;
            SelectedMetadataText.Text = $"正在组合 {composite.Parts.Count:N0} 个模型部件及贴图…";
            ModelTextureSelector.Visibility = Visibility.Collapsed;
            ShowPreviewLoading(previewGeneration, $"正在加载组合模型：{composite.Parts.Count:N0} 个部件");
            try
            {
                await PreviewCompositeModelAsync(composite, previewGeneration);
            }
            finally
            {
                HidePreviewLoading(previewGeneration);
            }
            return;
        }
        if (item.Asset is not AssetEntry asset) return;

        _selectedComposite = null;
        _selectedAsset = asset;
        SelectedNameText.Text = selectedCount > 1 ? $"{item.Name}（已选择 {selectedCount:N0} 项）" : item.Name;
        SelectedPathText.Text = asset.DisplayPath;
        SelectedMetadataText.Text = "正在读取…";
        if (asset.Kind == AssetKind.Model)
            ShowPreviewLoading(previewGeneration, $"正在加载模型：{asset.Name}");

        try
        {
            byte[] data = await Task.Run(() => _workspace.Extract(asset));
            if (_selectedAsset != asset || !IsPreviewCurrent(previewGeneration)) return;

            if (asset.Kind == AssetKind.Image)
            {
                PreviewImage.Source = await CreateBitmapAsync(data, 0);
                SelectedMetadataText.Text = $"{asset.Extension.TrimStart('.').ToUpperInvariant()} · {FormatBytes(data.Length)}";
            }
            else if (asset.Kind == AssetKind.Sound)
            {
                await PlaySoundAsync(asset, data);
                SelectedMetadataText.Text = $"{asset.Extension.TrimStart('.').ToUpperInvariant()} · {FormatBytes(data.Length)}";
            }
            else if (asset.Kind == AssetKind.Model)
            {
                (PmfMesh mesh, IReadOnlyList<ModelTextureBinding> textures) = await Task.Run(() =>
                    (PmfParser.Parse(data), _workspace.ResolveModelTextures(asset)));
                if (_selectedAsset != asset || !IsPreviewCurrent(previewGeneration)) return;
                _modelPreview.SetMesh(mesh);
                ModelTextureSelector.Visibility = Visibility.Visible;
                _settingModelTextureSelection = true;
                ModelTextureComboBox.ItemsSource = textures;
                ModelTextureComboBox.SelectedIndex = textures.Count > 0 ? 0 : -1;
                _settingModelTextureSelection = false;
                SelectedMetadataText.Text = $"PMF v{mesh.Version} · {mesh.Vertices.Count:N0} 顶点 · {mesh.DeclaredTriangleCount:N0} 三角面 · {mesh.UvChannelCount} UV 通道 · {textures.Count:N0} 个关联贴图 · {FormatBytes(data.Length)}";
                if (textures.Count > 0) await LoadModelTextureAsync(asset, textures[0]);
            }
            else
            {
                ResourceExplanation explanation = ResourceExplanationService.Explain(asset);
                bool mbTable = asset.Kind == AssetKind.MbTable;
                if (mbTable)
                {
                    GenericPreviewPanel.Visibility = Visibility.Collapsed;
                    MbTableDataPanel.Visibility = Visibility.Visible;
                    MbTablePreviewPanel.Visibility = Visibility.Collapsed;
                    if (TryBuildMbTableView(asset, data, GetSearchTerms(), out MbTableViewModel? tableView, out string mbTableMessage) &&
                        tableView is not null)
                    {
                        ShowMbTableView(tableView);
                    }
                    else
                    {
                        ShowMbTableError(asset, mbTableMessage);
                    }
                }
                else
                {
                    MbTableDataPanel.Visibility = Visibility.Collapsed;
                    GenericPreviewPanel.Visibility = Visibility.Visible;
                    SetGenericPreviewChromeVisibility(true);
                    GenericPreviewNameText.Text = UseBeginnerNames ? explanation.FriendlyName : asset.Name;
                    GenericPreviewRawNameText.Text = $"原始文件：{asset.Name}\n包内路径：{asset.DisplayPath}";
                    GenericPreviewIcon.Glyph = asset.Kind == AssetKind.Font ? "\uE8D2" : "\uE8A5";
                    GenericPreviewPurposeText.Text = explanation.Purpose;
                    GenericPreviewUsageText.Text = explanation.UsedWhen;
                    GenericPreviewConfidenceText.Text = $"识别程度：{explanation.Confidence}";
                    GenericPreviewTechnicalText.Text = $"技术信息：{ResourceExplanationService.GetTechnicalSummary(asset, data.Length)}";
                    GenericPreviewHintText.Text = explanation.PreviewAdvice;
                    MbTablePreviewPanel.Visibility = Visibility.Collapsed;
                    MbTablePreviewBox.Text = string.Empty;
                }
                string? textPreview = TryCreateTextPreview(asset, data);
                GenericTextPreviewBox.Text = textPreview ?? string.Empty;
                GenericTextExpander.Visibility = mbTable || textPreview is null ? Visibility.Collapsed : Visibility.Visible;
                GenericTextExpander.IsExpanded = false;
                SelectedMetadataText.Text = $"{explanation.FriendlyName} · {ResourceExplanationService.GetTechnicalSummary(asset, data.Length)}";
            }
        }
        catch (Exception ex)
        {
            SelectedMetadataText.Text = $"预览失败：{ex.Message}";
        }
        finally
        {
            if (asset.Kind == AssetKind.Model)
                HidePreviewLoading(previewGeneration);
        }
    }

    private async Task PreviewCompositeModelAsync(CompositeModelEntry composite, int previewGeneration)
    {
        try
        {
            (ModelRenderPart[] renderParts, int skippedParts) = await Task.Run(() =>
            {
                var parts = new List<ModelRenderPart>();
                int skipped = 0;
                foreach (CompositeModelPart part in composite.Parts)
                {
                    try
                    {
                        PmfMesh mesh = PmfParser.Parse(_workspace.Extract(part.MeshAsset));
                        DecodedTexture? texture = null;
                        string textureName = string.Empty;
                        if (part.TextureBinding is ModelTextureBinding binding)
                        {
                            try
                            {
                                texture = DdsDecoder.Decode(_workspace.Extract(binding.TextureAsset));
                                textureName = binding.DisplayName;
                            }
                            catch
                            {
                                // 单个部件贴图不支持时，仍保留其实体着色几何。
                            }
                        }

                        parts.Add(new ModelRenderPart(part.MeshAsset.Name, mesh, texture, textureName));
                    }
                    catch
                    {
                        skipped++;
                    }
                }

                return (parts.ToArray(), skipped);
            });
            if (_selectedComposite != composite || !IsPreviewCurrent(previewGeneration)) return;
            if (renderParts.Length == 0)
            {
                SelectedMetadataText.Text = "组合模型预览失败：没有可用的 PMF 部件";
                return;
            }

            _modelPreview.SetComposite(renderParts, composite.Name);
            int vertices = renderParts.Sum(part => part.Mesh.Vertices.Count);
            long triangles = renderParts.Sum(part => (long)part.Mesh.DeclaredTriangleCount);
            int texturedParts = renderParts.Count(part => part.Texture is not null);
            string skippedStatus = skippedParts > 0 ? $" · 跳过 {skippedParts:N0} 个异常部件" : string.Empty;
            SelectedMetadataText.Text = $"完整组合 · {renderParts.Length:N0} 个 PMF 部件 · {vertices:N0} 顶点 · {triangles:N0} 三角面 · {texturedParts:N0} 个部件已加载贴图{skippedStatus}";
        }
        catch (Exception ex)
        {
            if (_selectedComposite != composite || !IsPreviewCurrent(previewGeneration)) return;
            SelectedMetadataText.Text = $"组合模型预览失败：{ex.Message}";
        }
    }

    private async void ModelTextureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_settingModelTextureSelection || _selectedAsset?.Kind != AssetKind.Model ||
            ModelTextureComboBox.SelectedItem is not ModelTextureBinding binding) return;
        await LoadModelTextureAsync(_selectedAsset, binding);
    }

    private async Task LoadModelTextureAsync(AssetEntry modelAsset, ModelTextureBinding binding)
    {
        try
        {
            byte[] textureBytes = await Task.Run(() => _workspace.Extract(binding.TextureAsset));
            DecodedTexture texture = await Task.Run(() => DdsDecoder.Decode(textureBytes));
            if (_selectedAsset != modelAsset || !Equals(ModelTextureComboBox.SelectedItem, binding)) return;
            _modelPreview.SetTexture(texture, binding.DisplayName);
            SelectedMetadataText.Text = $"{SelectedMetadataText.Text.Split(" · 贴图：", StringSplitOptions.None)[0]} · 贴图：{texture.Width}×{texture.Height} {texture.Format}";
        }
        catch (Exception ex)
        {
            if (_selectedAsset != modelAsset) return;
            _modelPreview.SetTexture(null, null);
            SelectedMetadataText.Text = $"贴图预览失败：{ex.Message}；可切换到实体或线框模式";
        }
    }

    private async Task PlaySoundAsync(AssetEntry asset, byte[] data)
    {
        string cacheFolder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XunxianDpkViewer", "preview");
        Directory.CreateDirectory(cacheFolder);
        string hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(asset.DisplayPath))).Substring(0, 20);
        string target = System.IO.Path.Combine(cacheFolder, hash + asset.Extension);
        await File.WriteAllBytesAsync(target, data);
        StorageFile storageFile = await StorageFile.GetFileFromPathAsync(target);
        _mediaPlayer.Source = MediaSource.CreateFromStorageFile(storageFile);
        SoundNameText.Text = asset.Name;
        _mediaPlayer.Play();
    }

    private static async Task<BitmapImage> CreateBitmapAsync(byte[] data, int decodeWidth)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(data);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }
        stream.Seek(0);
        var bitmap = new BitmapImage();
        if (decodeWidth > 0) bitmap.DecodePixelWidth = decodeWidth;
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    private async void OpenFolderButton_Click(object sender, RoutedEventArgs e) =>
        await PickAndLoadResourceFolderAsync();

    private async void ChooseInitialPathButton_Click(object sender, RoutedEventArgs e) =>
        await PickAndLoadResourceFolderAsync();

    private async Task PickAndLoadResourceFolderAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is not null) await LoadResourceFolderAsync(folder.Path);
    }

    private async void OpenArchiveButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add(".dpk");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is not null) await LoadArchiveAsync(file.Path);
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var pathBox = new TextBox
        {
            Header = "当前资源路径",
            Text = string.IsNullOrWhiteSpace(CurrentPathText.Text) ? "尚未选择" : CurrentPathText.Text,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var chooseButton = new Button
        {
            Content = "更换资源目录",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(18, 9, 18, 9)
        };
        var panel = new StackPanel { Spacing = 14, Width = 620 };
        panel.Children.Add(pathBox);
        panel.Children.Add(new TextBlock
        {
            Text = "可选择《新寻仙》安装目录，也可以直接选择其中的 res 目录。设置成功后程序会记住路径。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SecondaryTextBrush"]
        });
        panel.Children.Add(chooseButton);

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "设置",
            Content = panel,
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close
        };
        chooseButton.Click += async (_, _) =>
        {
            dialog.Hide();
            await PickAndLoadResourceFolderAsync();
        };
        await dialog.ShowAsync();
    }

    private async void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Spacing = 10, Width = 480 };
        panel.Children.Add(new Image
        {
            Source = new BitmapImage(new Uri("ms-appx:///Assets/XunxianIcon.png")),
            Width = 72,
            Height = 72,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        panel.Children.Add(new TextBlock
        {
            Text = "寻仙 DPK 资源浏览器",
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        panel.Children.Add(new TextBlock { Text = $"版本：{AppVersion}" });
        panel.Children.Add(new TextBlock { Text = $"作者：{AppAuthor}" });
        panel.Children.Add(new TextBlock
        {
            Text = "用于浏览、解释和导出《新寻仙》客户端 DPK 资源。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SecondaryTextBrush"]
        });

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "关于",
            Content = panel,
            CloseButtonText = "关闭"
        };
        await dialog.ShowAsync();
    }

    private async void ExportSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        AssetEntry[] assets = GetSelectedAssets();
        if (assets.Length == 0 || _isBusy) return;
        StorageFolder? folder = await PickOutputFolderAsync();
        if (folder is null) return;
        SetBusy(true, $"正在导出 0 / {assets.Length:N0}…");
        try
        {
            await Task.Run(() =>
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    _workspace.ExtractTo(assets[i], folder.Path);
                    if (i % 20 == 0 || i == assets.Length - 1)
                    {
                        int completed = i + 1;
                        DispatcherQueue.TryEnqueue(() => SetStatus($"正在导出 {completed:N0} / {assets.Length:N0}…"));
                    }
                }
            });
            SetStatus($"已导出所选 {assets.Length:N0} 个资源");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("导出失败", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void PropertiesButton_Click(object sender, RoutedEventArgs e)
    {
        AssetEntry[] selected = GetSelectedAssets();
        if (selected.Length != 1 || _isBusy) return;
        AssetEntry asset = selected[0];
        SetBusy(true, "正在读取文件属性…");
        try
        {
            byte[] data = await Task.Run(() => _workspace.Extract(asset));
            var archiveInfo = new FileInfo(asset.ArchivePath);
            string sha256 = Convert.ToHexString(SHA256.HashData(data));
            string kind = asset.Kind switch
            {
                AssetKind.Image => "图像",
                AssetKind.Sound => "声音",
                AssetKind.Model => "模型",
                AssetKind.Font => "字体",
                AssetKind.MbTable => "MB表",
                _ => "其他"
            };
            string details =
                $"文件名：{asset.Name}\n" +
                $"资源类型：{kind}\n" +
                $"文件格式：{asset.Extension.TrimStart('.').ToUpperInvariant()}\n" +
                $"解包大小：{FormatBytes(data.Length)}（{data.Length:N0} 字节）\n" +
                $"所属 DPK：{asset.ArchiveName}\n" +
                $"包内路径：{asset.Entry.Path}\n" +
                $"索引根块：{asset.Entry.RootBlock:N0}（0x{asset.Entry.RootBlock:X8}）\n" +
                $"SHA-256：{sha256}\n\n" +
                $"DPK 文件：{asset.ArchivePath}\n" +
                $"DPK 大小：{FormatBytes(archiveInfo.Length)}（{archiveInfo.Length:N0} 字节）\n" +
                $"DPK 修改时间：{archiveInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}";

            SetBusy(false);
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "文件属性",
                MinWidth = 760,
                Content = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Width = 700,
                    Height = 390,
                    Padding = new Thickness(16),
                    Content = new TextBlock
                    {
                        Text = details,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true
                    }
                },
                CloseButtonText = "关闭"
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("读取属性失败", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ExportModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        AssetEntry? selectedAsset = _selectedAsset?.Kind == AssetKind.Model ? _selectedAsset : null;
        CompositeModelEntry? selectedComposite = _selectedComposite;
        if (selectedAsset is null && selectedComposite is null) return;

        string suggestedName = selectedComposite is not null
            ? SanitizeFileName(selectedComposite.Name)
            : System.IO.Path.GetFileNameWithoutExtension(selectedAsset!.Name);
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedName
        };
        picker.FileTypeChoices.Add("Wavefront OBJ 模型", new List<string> { ".obj" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null) return;
        ModelTextureBinding? selectedTextureBinding = ModelTextureComboBox.SelectedItem as ModelTextureBinding;

        SetBusy(true, selectedComposite is null ? "正在转换并导出 OBJ 部件…" : "正在合并并导出完整 OBJ 模型…");
        try
        {
            string outputPath = file.Path;
            await Task.Run(() =>
            {
                if (selectedComposite is not null)
                {
                    IReadOnlyList<ObjExporter.ObjPart> parts = BuildCompositeObjParts(selectedComposite, outputPath);
                    ObjExporter.Export(parts, outputPath, selectedComposite.Name);
                }
                else if (selectedAsset is not null)
                {
                    PmfMesh mesh = PmfParser.Parse(_workspace.Extract(selectedAsset));
                    string? textureFileName = selectedTextureBinding is null
                        ? null
                        : CopyObjTexture(selectedTextureBinding.TextureAsset, outputPath, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    string? materialName = selectedTextureBinding?.MaterialName;
                    if (string.IsNullOrWhiteSpace(materialName)) materialName = System.IO.Path.GetFileNameWithoutExtension(selectedAsset.Name);
                    ObjExporter.Export(
                        new[] { new ObjExporter.ObjPart(System.IO.Path.GetFileNameWithoutExtension(selectedAsset.Name), mesh, materialName, textureFileName) },
                        outputPath,
                        System.IO.Path.GetFileNameWithoutExtension(selectedAsset.Name));
                }
            });
            SetStatus(selectedComposite is null
                ? $"OBJ 部件已导出：{file.Path}"
                : $"完整 OBJ 模型已导出：{file.Path}");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("OBJ 导出失败", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private IReadOnlyList<ObjExporter.ObjPart> BuildCompositeObjParts(CompositeModelEntry composite, string outputPath)
    {
        var parts = new List<ObjExporter.ObjPart>();
        var copiedTextures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedTextureFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < composite.Parts.Count; i++)
        {
            CompositeModelPart part = composite.Parts[i];
            PmfMesh mesh = PmfParser.Parse(_workspace.Extract(part.MeshAsset));
            string? textureFileName = part.TextureBinding is null
                ? null
                : CopyObjTexture(part.TextureBinding.TextureAsset, outputPath, copiedTextures, usedTextureFileNames);
            string materialName = string.IsNullOrWhiteSpace(part.MaterialName)
                ? System.IO.Path.GetFileNameWithoutExtension(part.MeshAsset.Name)
                : part.MaterialName;
            string objectName = $"{i + 1:000}_{System.IO.Path.GetFileNameWithoutExtension(part.MeshAsset.Name)}";
            parts.Add(new ObjExporter.ObjPart(objectName, mesh, materialName, textureFileName));
        }

        if (parts.Count == 0) throw new InvalidDataException("完整组合没有可导出的 PMF 部件。");
        return parts;
    }

    private string CopyObjTexture(
        AssetEntry textureAsset,
        string outputPath,
        Dictionary<string, string> copiedTextures,
        HashSet<string> usedFileNames)
    {
        string key = textureAsset.DisplayPath;
        if (copiedTextures.TryGetValue(key, out string? existing)) return existing;

        string? outputDirectory = System.IO.Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            outputDirectory = Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outputDirectory);

        string fileName = MakeUniqueExportFileName(textureAsset.Name, usedFileNames);
        File.WriteAllBytes(System.IO.Path.Combine(outputDirectory, fileName), _workspace.Extract(textureAsset));
        copiedTextures[key] = fileName;
        return fileName;
    }

    private void ExpandModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentKind != AssetKind.Model) return;
        _modelExpanded = !_modelExpanded;
        AssetBrowserPanel.Visibility = _modelExpanded ? Visibility.Collapsed : Visibility.Visible;
        AssetListColumn.MinWidth = _modelExpanded ? 0 : 600;
        AssetListColumn.Width = _modelExpanded ? new GridLength(0) : new GridLength(650);
        ExpandModelButtonText.Text = _modelExpanded ? "恢复模型列表" : "放大模型预览";
    }

    private async void BatchExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_filteredAssets.Count == 0 || _isBusy) return;
        var confirmation = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "导出当前查询结果",
            Content = $"将按 DPK 包名和原始目录结构导出 {_filteredAssets.Count:N0} 个资源。",
            PrimaryButtonText = "开始导出",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary) return;
        StorageFolder? folder = await PickOutputFolderAsync();
        if (folder is null) return;

        AssetEntry[] assets = _filteredAssets.ToArray();
        SetBusy(true, $"正在导出 0 / {assets.Length:N0}…");
        try
        {
            await Task.Run(() =>
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    _workspace.ExtractTo(assets[i], folder.Path);
                    if (i % 20 == 0 || i == assets.Length - 1)
                    {
                        int completed = i + 1;
                        DispatcherQueue.TryEnqueue(() => SetStatus($"正在导出 {completed:N0} / {assets.Length:N0}…"));
                    }
                }
            });
            SetStatus($"导出完成：{assets.Length:N0} 个资源");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("批量导出中断", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<StorageFolder?> PickOutputFolderAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        return await picker.PickSingleFolderAsync();
    }

    private void CategoryNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        NavigationViewItemBase? selectedItem = args.SelectedItem as NavigationViewItemBase ?? args.SelectedItemContainer;
        if (selectedItem?.Tag is not string tag) return;
        _currentKind = tag switch
        {
            "sound" => AssetKind.Sound,
            "model" => AssetKind.Model,
            "font" => AssetKind.Font,
            "mb" => AssetKind.MbTable,
            "dungeon" => AssetKind.DungeonSummary,
            "other" => AssetKind.Other,
            _ => AssetKind.Image
        };
        SearchBox.Text = string.Empty;
        ConfigureCategoryUi();
        if (_currentKind == AssetKind.DungeonSummary)
        {
            RefreshDungeonSummary();
            return;
        }
        BuildFolderTree();
        ApplyFilter();
    }

    private void ConfigureCategoryUi()
    {
        bool images = _currentKind == AssetKind.Image;
        bool sounds = _currentKind == AssetKind.Sound;
        bool models = _currentKind == AssetKind.Model;
        bool fonts = _currentKind == AssetKind.Font;
        bool mbTables = _currentKind == AssetKind.MbTable;
        bool dungeonSummary = _currentKind == AssetKind.DungeonSummary;
        bool others = _currentKind == AssetKind.Other;
        PageTitleText.Text = _currentKind switch
        {
            AssetKind.Image => "图标与贴图",
            AssetKind.Sound => "声音",
            AssetKind.Model => "模型",
            AssetKind.Font => "字体",
            AssetKind.MbTable => "MB 表",
            AssetKind.DungeonSummary => "副本怪物",
            _ => "配置与其他"
        };
        PageDescriptionText.Text = _currentKind switch
        {
            AssetKind.Image => "浏览 GUI 图标、装备图和 DDS 场景贴图",
            AssetKind.Sound => "直接试听 OGG 音乐、环境音与 WAV 音效",
            AssetKind.Model => "大尺寸实体预览 PMF 模型，并可转换导出为 OBJ",
            AssetKind.Font => "浏览 font.dpk 内的 TTF、OTF 和 TTC 字体资源",
            AssetKind.MbTable => "浏览 mb.dpk 内的玩法、物品、任务、技能等数据表",
            AssetKind.DungeonSummary => "按副本汇总怪物头像、核心战斗属性、隐藏状态和掉落组",
            _ => "浏览特效、场景、地形、天空、影片及各类配置文件"
        };
        SearchBox.PlaceholderText = _currentKind switch
        {
            AssetKind.Image => "搜索图标名称或路径",
            AssetKind.Sound => "搜索音效、音乐或路径",
            AssetKind.Model => "搜索模型名称或路径",
            AssetKind.Font => "搜索字体名称或路径",
            AssetKind.MbTable => "搜索 MB 表名称或路径",
            AssetKind.DungeonSummary => "搜索副本或怪物名称",
            _ => "搜索配置、特效、场景或路径"
        };
        ImageGrid.Visibility = images ? Visibility.Visible : Visibility.Collapsed;
        AssetList.Visibility = images || mbTables ? Visibility.Collapsed : Visibility.Visible;
        MbAssetList.Visibility = mbTables ? Visibility.Visible : Visibility.Collapsed;
        ImagePreviewPanel.Visibility = images ? Visibility.Visible : Visibility.Collapsed;
        SoundPreviewPanel.Visibility = sounds ? Visibility.Visible : Visibility.Collapsed;
        ModelPreviewHost.Visibility = models ? Visibility.Visible : Visibility.Collapsed;
        MbTableDataPanel.Visibility = mbTables ? Visibility.Visible : Visibility.Collapsed;
        GenericPreviewPanel.Visibility = fonts || others ? Visibility.Visible : Visibility.Collapsed;
        DungeonSummaryPanel.Visibility = dungeonSummary ? Visibility.Visible : Visibility.Collapsed;
        BeginnerModeToggle.Visibility = Visibility.Collapsed;
        FolderTreeTitleText.Text = mbTables ? "MB 表目录" : "DPK 目录";
        FolderTreeHintText.Text = mbTables ? "按 mb.dpk 表目录浏览" : "按包内真实路径浏览";
        ModelTextureSelector.Visibility = Visibility.Collapsed;
        ModelTextureComboBox.ItemsSource = null;
        ExportModelButton.Visibility = models ? Visibility.Visible : Visibility.Collapsed;
        ExpandModelButton.Visibility = models ? Visibility.Visible : Visibility.Collapsed;
        SetMultiSelectMode(false);
        _modelExpanded = false;
        AssetBrowserPanel.Visibility = dungeonSummary ? Visibility.Collapsed : Visibility.Visible;
        PreviewPanel.Visibility = dungeonSummary ? Visibility.Collapsed : Visibility.Visible;
        FolderTreeColumn.Width = mbTables ? new GridLength(180) : new GridLength(230);
        AssetListColumn.MinWidth = mbTables ? 360 : models ? 600 : 650;
        AssetListColumn.Width = mbTables ? new GridLength(500) : models ? new GridLength(650) : new GridLength(1.35, GridUnitType.Star);
        PreviewColumn.MinWidth = mbTables ? 500 : 360;
        PreviewColumn.Width = models ? new GridLength(1, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
        SelectedFooterPanel.Visibility = mbTables || dungeonSummary ? Visibility.Collapsed : Visibility.Visible;
        ExpandModelButtonText.Text = "放大模型预览";
        if (!sounds) _mediaPlayer.Pause();
        if (_currentKind != AssetKind.Model) _modelPreview.SetMesh(null);
        PreviewImage.Source = null;
        ImageGrid.SelectedItem = null;
        AssetList.SelectedItem = null;
        MbAssetList.SelectedItem = null;
        _selectedAsset = null;
        _selectedComposite = null;
        _currentMbTableView = null;
        ExportSelectedButton.IsEnabled = false;
        MbExportButton.IsEnabled = false;
        ExportSelectedButtonText.Text = "导出原始资源";
        PropertiesButton.IsEnabled = false;
        MbPropertiesButton.IsEnabled = false;
        ExportModelButton.IsEnabled = false;
        SelectedNameText.Text = "尚未选择资源";
        SelectedPathText.Text = _currentKind switch
        {
            AssetKind.Image => "从左侧选择一张图像",
            AssetKind.Sound => "从左侧选择一个声音",
            AssetKind.Model => "从左侧选择一个 PMF 模型",
            AssetKind.Font => "从左侧选择一个字体文件",
            AssetKind.MbTable => "从左侧选择一个 MB 表文件",
            AssetKind.DungeonSummary => "从左侧选择一个副本",
            _ => "从左侧选择一个资源文件"
        };
        SelectedMetadataText.Text = string.Empty;
        MbTablePreviewPanel.Visibility = Visibility.Collapsed;
        MbTableSummaryText.Text = string.Empty;
        MbTablePreviewBox.Text = string.Empty;
        ClearMbTableView();
        SetGenericPreviewChromeVisibility(_currentKind != AssetKind.MbTable);
        GenericTextExpander.Visibility = Visibility.Collapsed;
        GenericTextPreviewBox.Text = string.Empty;
    }

    private void BeginnerModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (FolderTree is null || SearchBox is null || _workspace.Assets.Count == 0) return;
        BuildFolderTree();
        ApplyFilter();
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortComboBox.SelectedIndex < 0) return;
        _sortMode = SortComboBox.SelectedIndex;
        if (ImageGrid is not null && AssetList is not null) ApplyFilter();
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_multiSelectMode) return;
        GetActiveAssetList().SelectAll();
    }

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        GetActiveAssetList().SelectedItem = null;
    }

    private void ToggleMultiSelectButton_Click(object sender, RoutedEventArgs e) =>
        SetMultiSelectMode(!_multiSelectMode);

    private void SetMultiSelectMode(bool enabled)
    {
        _multiSelectMode = enabled;
        if (ImageGrid.ItemsSource is null || AssetList.ItemsSource is null || MbAssetList.ItemsSource is null) return;
        if (!enabled)
        {
            ImageGrid.SelectedItem = null;
            AssetList.SelectedItem = null;
            MbAssetList.SelectedItem = null;
        }

        ListViewSelectionMode selectionMode = enabled ? ListViewSelectionMode.Multiple : ListViewSelectionMode.Single;
        ImageGrid.SelectionMode = selectionMode;
        AssetList.SelectionMode = selectionMode;
        MbAssetList.SelectionMode = selectionMode;
        ImageGrid.IsMultiSelectCheckBoxEnabled = enabled;
        AssetList.IsMultiSelectCheckBoxEnabled = enabled;
        MbAssetList.IsMultiSelectCheckBoxEnabled = enabled;
        MultiSelectButton.Content = enabled ? "完成" : "多选";
        SelectAllButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        ClearSelectionButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        UpdateSelectionUi(GetActiveAssetList().SelectedItems.Count);
        if (!enabled) ResetPreviewSelection();
    }

    private ListViewBase GetActiveAssetList() => _currentKind switch
    {
        AssetKind.Image => ImageGrid,
        AssetKind.MbTable => MbAssetList,
        _ => AssetList
    };

    private AssetItemViewModel[] GetSelectedItems() => GetActiveAssetList().SelectedItems
        .OfType<AssetItemViewModel>()
        .Distinct()
        .ToArray();

    private AssetEntry[] GetSelectedAssets() => GetSelectedItems()
        .OfType<AssetItemViewModel>()
        .Select(item => item.Asset)
        .OfType<AssetEntry>()
        .Distinct()
        .ToArray();

    private void UpdateSelectionUi(int selectedCount)
    {
        AssetItemViewModel[] selectedItems = GetSelectedItems();
        AssetEntry[] selected = GetSelectedAssets();
        ExportSelectedButton.IsEnabled = selected.Length > 0;
        MbExportButton.IsEnabled = selected.Length > 0;
        ExportSelectedButtonText.Text = _multiSelectMode
            ? $"导出所选 ({selected.Length:N0})"
            : "导出原始资源";
        PropertiesButton.IsEnabled = selected.Length == 1;
        MbPropertiesButton.IsEnabled = selected.Length == 1;
        bool canExportSingleModel = selectedItems.Length == 1 &&
                                    (selectedItems[0].Composite is not null ||
                                     selectedItems[0].Asset?.Kind == AssetKind.Model);
        ExportModelButton.IsEnabled = canExportSingleModel;
        ExportModelButtonText.Text = selectedItems.Length == 1 && selectedItems[0].Composite is not null
            ? "导出完整 OBJ 模型"
            : selectedItems.Length == 1 && selectedItems[0].Asset?.Kind == AssetKind.Model
                ? "导出 OBJ 部件"
                : "导出 OBJ 模型";
    }

    private void ResetPreviewSelection()
    {
        PreviewLoadingOverlay.Visibility = Visibility.Collapsed;
        ExportModelButton.IsEnabled = false;
        ExportModelButtonText.Text = "导出 OBJ 模型";
        PropertiesButton.IsEnabled = false;
        MbPropertiesButton.IsEnabled = false;
        MbExportButton.IsEnabled = false;
        SelectedNameText.Text = "尚未选择资源";
        SelectedPathText.Text = _currentKind == AssetKind.Image
            ? "可多选图像后批量导出"
            : _currentKind == AssetKind.Sound
                ? "可多选声音后批量导出"
                : _currentKind == AssetKind.Model
                    ? "可多选模型后批量导出"
                    : "可多选资源后批量导出";
        SelectedMetadataText.Text = string.Empty;
        PreviewImage.Source = null;
        MbTablePreviewPanel.Visibility = Visibility.Collapsed;
        MbTableSummaryText.Text = string.Empty;
        MbTablePreviewBox.Text = string.Empty;
        ClearMbTableView();
        SetGenericPreviewChromeVisibility(_currentKind != AssetKind.MbTable);
        GenericTextExpander.Visibility = Visibility.Collapsed;
        GenericTextPreviewBox.Text = string.Empty;
        _selectedComposite = null;
        _currentMbTableView = null;
        if (_currentKind == AssetKind.Model) _modelPreview.SetMesh(null);
    }

    private void ClearMbTableView()
    {
        _currentMbTableView = null;
        MbTableNameText.Text = "选择一个 MB 表";
        MbTablePathText.Text = string.Empty;
        MbDataSummaryText.Text = string.Empty;
        MbRecordCountText.Text = string.Empty;
        MbRecordList.ItemsSource = null;
        MbRecordList.SelectedItem = null;
        MbRecordEmptyPanel.Visibility = Visibility.Collapsed;
        MbRecordTitleText.Text = "选择左侧记录查看字段";
        MbRecordSubtitleText.Text = string.Empty;
        MbFieldList.ItemsSource = null;
        MbRecordExtraPanel.Visibility = Visibility.Collapsed;
        MbRecordExtraText.Text = string.Empty;
    }

    private void ShowMbTableError(AssetEntry asset, string message)
    {
        _currentMbTableView = null;
        MbTableNameText.Text = CreateMbTableListName(asset);
        MbTablePathText.Text = asset.DisplayPath;
        MbDataSummaryText.Text = message;
        MbRecordCountText.Text = string.Empty;
        MbRecordList.ItemsSource = null;
        MbRecordEmptyPanel.Visibility = Visibility.Visible;
        MbRecordTitleText.Text = "无法解析表格";
        MbRecordSubtitleText.Text = message;
        MbFieldList.ItemsSource = null;
        MbRecordExtraPanel.Visibility = Visibility.Collapsed;
        MbRecordExtraText.Text = string.Empty;
    }

    private void ShowMbTableView(MbTableViewModel tableView)
    {
        _currentMbTableView = tableView;
        MbTableNameText.Text = tableView.TableName;
        MbTablePathText.Text = tableView.Path;
        MbDataSummaryText.Text = tableView.Summary;
        MbRecordCountText.Text = $"{tableView.Records.Count:N0} 条";
        MbRecordList.ItemsSource = tableView.Records;
        MbRecordEmptyPanel.Visibility = tableView.Records.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (tableView.Records.Count > 0)
        {
            MbRecordList.SelectedIndex = 0;
            ShowMbRecordDetails(tableView.Records[0]);
        }
        else
        {
            MbRecordList.SelectedIndex = -1;
            MbRecordTitleText.Text = "没有记录";
            MbRecordSubtitleText.Text = "这个表解析成功，但没有可显示的数据行。";
            MbFieldList.ItemsSource = null;
            MbRecordExtraPanel.Visibility = Visibility.Collapsed;
            MbRecordExtraText.Text = string.Empty;
        }
    }

    private void MbRecordList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.OfType<MbRecordViewModel>().LastOrDefault() is { } record)
            ShowMbRecordDetails(record);
    }

    private void ShowMbRecordDetails(MbRecordViewModel record)
    {
        if (_currentMbTableView is not { } tableView) return;

        MbRecordTitleText.Text = record.Title;
        MbRecordSubtitleText.Text = $"{tableView.TableName} · 原始第 {record.SourceRow:N0} 行";
        MbFieldList.ItemsSource = BuildMbFieldViewModels(tableView, record.Row);

        string extraDetails = BuildMbRecordExtraDetails(tableView.Asset, record.Row);
        MbRecordExtraPanel.Visibility = string.IsNullOrWhiteSpace(extraDetails) ? Visibility.Collapsed : Visibility.Visible;
        MbRecordExtraText.Text = extraDetails.Trim();
    }

    private IReadOnlyList<MbFieldViewModel> BuildMbFieldViewModels(MbTableViewModel tableView, IReadOnlyList<string> row)
    {
        var fields = new List<MbFieldViewModel>();
        foreach (int column in tableView.ActiveColumns)
        {
            string value = column < row.Count ? row[column].Trim() : string.Empty;
            string displayValue = string.IsNullOrWhiteSpace(value) ? "空" : value;
            string name = GetColumnName(tableView.Headers, column);
            string note = GetMbFieldNote(tableView.Asset, name, column, value);
            fields.Add(new MbFieldViewModel(name, displayValue, note, column));
        }

        return fields;
    }

    private bool IsPreviewCurrent(int generation) => _previewGeneration == generation;

    private void ShowPreviewLoading(int generation, string text)
    {
        if (!IsPreviewCurrent(generation)) return;
        PreviewLoadingText.Text = text;
        PreviewLoadingOverlay.Visibility = Visibility.Visible;
    }

    private void HidePreviewLoading(int generation)
    {
        if (IsPreviewCurrent(generation))
            PreviewLoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private void SetGenericPreviewChromeVisibility(bool visible)
    {
        Visibility visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        GenericPreviewIconBorder.Visibility = visibility;
        GenericPreviewNameText.Visibility = visibility;
        GenericPreviewRawNameText.Visibility = visibility;
        GenericExplanationCard.Visibility = visibility;
        GenericPreviewTechnicalText.Visibility = visibility;
        GenericPreviewHintText.Visibility = visibility;
    }

    private static string? TryCreateTextPreview(AssetEntry asset, byte[] data)
    {
        if (!TryDecodeTextPreview(asset, data, out string text)) return null;
        if (asset.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
            asset.Extension.Equals(".cct", StringComparison.OrdinalIgnoreCase) ||
            asset.Extension.Equals(".cmf", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                text = XDocument.Parse(text).ToString();
            }
            catch
            {
                // Keep the decoded original when an old client file is only XML-like.
            }
        }

        const int maximumCharacters = 200_000;
        return text.Length <= maximumCharacters
            ? text
            : text[..maximumCharacters] + "\n\n……内容过长，预览到此为止；导出原始文件可查看完整内容。";
    }

    private bool TryBuildMbTableView(
        AssetEntry asset,
        byte[] data,
        IReadOnlyList<string> focusTerms,
        out MbTableViewModel? tableView,
        out string message)
    {
        tableView = null;
        message = string.Empty;
        if (asset.Kind != AssetKind.MbTable)
        {
            message = "这不是 MB 表资源。";
            return false;
        }

        if (!TryDecodeTextPreview(asset, data, out string text))
        {
            message = "这个 MB 文件暂时无法识别为可读文本表。";
            return false;
        }

        if (text.StartsWith("文件共有 ", StringComparison.Ordinal))
        {
            message = text;
            return false;
        }

        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (lines.Length == 0)
        {
            message = "这个 MB 表没有可显示的文本行。";
            return false;
        }

        char delimiter = ChooseMbTableDelimiter(lines);
        string[][] allRows = lines
            .Select(line => SplitMbTableLine(line, delimiter))
            .Where(row => row.Length > 0)
            .ToArray();
        int maxColumns = allRows.Length == 0 ? 0 : allRows.Max(row => row.Length);
        if (maxColumns <= 1)
        {
            string[] textHeaders = { "内容" };
            int[] textColumns = { 0 };
            var indexedTextRows = lines
                .Select((line, index) => (Row: new[] { line }, SourceRow: index + 1))
                .ToArray();
            var selectedTextRows = SelectFocusedMbRows(indexedTextRows, focusTerms, out int textMatches);
            var textRecords = selectedTextRows
                .Select(row => new MbRecordViewModel(
                    row.SourceRow,
                    TrimPreviewCell(row.Row[0]),
                    row.Row[0],
                    row.Row))
                .ToList();
            string textFocus = BuildMbTableFocusText(focusTerms, textMatches, textRecords.Count, lines.Length);
            tableView = new MbTableViewModel(
                asset,
                CreateMbTableListName(asset),
                asset.DisplayPath,
                $"纯文本表 · 共 {lines.Length:N0} 行。{textFocus}",
                textHeaders,
                textColumns,
                textRecords);
            return true;
        }

        bool hasHeader = TryGetMbHeaders(asset, allRows, maxColumns, out string[] headers, out int firstDataRow);
        string[][] dataRows = allRows.Skip(firstDataRow).ToArray();
        int[] activeColumns = Enumerable.Range(0, maxColumns)
            .Where(column =>
                !string.IsNullOrWhiteSpace(GetColumnName(headers, column)) ||
                dataRows.Any(row => column < row.Length && !string.IsNullOrWhiteSpace(row[column])))
            .ToArray();
        if (activeColumns.Length == 0)
            activeColumns = Enumerable.Range(0, maxColumns).ToArray();

        var indexedRows = dataRows
            .Select((row, index) => (Row: row, SourceRow: firstDataRow + index + 1))
            .ToArray();
        var selectedRows = SelectFocusedMbRows(indexedRows, focusTerms, out int matchedRows);
        List<MbRecordViewModel> records = selectedRows
            .Select(row => new MbRecordViewModel(
                row.SourceRow,
                BuildMbRecordTitle(headers, row.Row),
                BuildMbRowPreviewText(headers, activeColumns, row.Row),
                row.Row))
            .ToList();

        bool hasNamedHeaders = headers.Any(header => !string.IsNullOrWhiteSpace(header));
        string fieldSource = hasHeader
            ? "字段来自表头"
            : hasNamedHeaders
                ? "字段来自已知模板/转译"
                : "字段按列自动识别";
        string focusText = BuildMbTableFocusText(focusTerms, matchedRows, records.Count, dataRows.Length);
        string delimiterName = delimiter == '\t' ? "制表符" : delimiter == ',' ? "逗号" : "空白";
        message = $"{fieldSource} · {delimiterName}分隔 · 共 {dataRows.Length:N0} 条记录、{activeColumns.Length:N0} 个有效字段。{focusText}";
        tableView = new MbTableViewModel(
            asset,
            CreateMbTableListName(asset),
            asset.DisplayPath,
            message,
            headers,
            activeColumns,
            records);
        return true;
    }

    private static (string[] Row, int SourceRow)[] SelectFocusedMbRows(
        IReadOnlyList<(string[] Row, int SourceRow)> rows,
        IReadOnlyList<string> focusTerms,
        out int matchedRows)
    {
        matchedRows = 0;
        string[] usableTerms = focusTerms
            .Where(term => term.Length >= 2)
            .SelectMany(GetSearchTermVariants)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (usableTerms.Length == 0) return rows.ToArray();

        (string[] Row, int SourceRow)[] matches = rows
            .Where(row => usableTerms.All(term =>
                row.Row.Any(cell => cell.Contains(term, StringComparison.OrdinalIgnoreCase))))
            .ToArray();
        matchedRows = matches.Length;
        return matches.Length > 0 ? matches : rows.ToArray();
    }

    private static string BuildMbTableFocusText(
        IReadOnlyList<string> focusTerms,
        int matchedRows,
        int visibleRows,
        int totalRows)
    {
        bool hasSearch = focusTerms.Any(term => term.Length >= 2);
        if (!hasSearch)
            return $"已把全部 {visibleRows:N0} 条记录放入下面的可滚动列表。";

        return matchedRows > 0
            ? $"当前搜索命中 {matchedRows:N0} 条，已全部显示；清空搜索可查看全表 {totalRows:N0} 条。"
            : $"当前搜索没有命中具体记录，下面暂时显示全表 {visibleRows:N0} 条。";
    }

    private static string BuildMbRowPreviewText(
        IReadOnlyList<string> headers,
        IReadOnlyList<int> activeColumns,
        IReadOnlyList<string> row)
    {
        string[] parts = activeColumns
            .Where(column => column < row.Count && !string.IsNullOrWhiteSpace(row[column]))
            .Take(6)
            .Select(column => $"{GetColumnName(headers, column)}: {TrimPreviewCell(row[column])}")
            .ToArray();
        return parts.Length == 0 ? "这一行没有明显字段值" : string.Join(" · ", parts);
    }

    private static string GetMbFieldNote(AssetEntry asset, string fieldName, int column, string value)
    {
        string normalizedPath = asset.Entry.Path.Replace('\\', '/').ToLowerInvariant();
        if (normalizedPath.StartsWith("object/cha_list", StringComparison.Ordinal))
        {
            return column switch
            {
                1 => "角色/怪物的唯一编号，其他表或脚本会用它引用这个单位。",
                4 => "关联 object/cha_fight.txt，里面是生命、伤害、防御、抗性、会心等战斗属性。",
                5 => "关联 object/cha_pic.txt，里面是模型配置、部件和外观资源。",
                13 => "出生或常驻状态 ID 列表，可在 skill/state_data.txt 里查状态名称。",
                18 => "技能或效果相关 ID 列表，通常用于战斗行为、特殊机制或状态触发。",
                _ => GetGenericMbFieldNote(fieldName, value)
            };
        }

        if (normalizedPath.StartsWith("object/cha_fight", StringComparison.Ordinal))
        {
            return fieldName.Contains("生命", StringComparison.Ordinal) ||
                   fieldName.Contains("伤害", StringComparison.Ordinal) ||
                   fieldName.Contains("防御", StringComparison.Ordinal) ||
                   fieldName.Contains("抗性", StringComparison.Ordinal) ||
                   fieldName.Contains("会心", StringComparison.Ordinal)
                ? "战斗数值字段，通常会被角色/怪物表的战斗属性 ID 关联使用。"
                : GetGenericMbFieldNote(fieldName, value);
        }

        if (normalizedPath.StartsWith("life/legend_equip/legend_equip_list", StringComparison.Ordinal))
        {
            return column switch
            {
                0 => "装备或物品 ID，用于背包、掉落、奖励等系统引用。",
                1 => "装备显示名称。",
                >= 10 and <= 13 => "固定属性组 ID，可继续关联 legend_equip_atbs.txt 和 legend_equip_atb_value.txt 查看实际属性。",
                _ => GetGenericMbFieldNote(fieldName, value)
            };
        }

        return GetGenericMbFieldNote(fieldName, value);
    }

    private static string GetGenericMbFieldNote(string fieldName, string value)
    {
        if (fieldName.Contains("保留", StringComparison.Ordinal))
            return "保留或暂未转译字段，通常不是优先看的业务属性。";
        if (fieldName.Contains("名称", StringComparison.Ordinal) || fieldName.Contains("名字", StringComparison.Ordinal))
            return "显示名或内部配置名，适合用来搜索定位。";
        if (fieldName.Contains("ID", StringComparison.OrdinalIgnoreCase) || fieldName.Contains("编号", StringComparison.Ordinal))
            return "编号/关联键，常用于连接其他 MB 表或脚本配置。";
        if (fieldName.Contains("状态", StringComparison.Ordinal) || value.Contains('*', StringComparison.Ordinal))
            return "多值字段，通常是一组 ID，需要结合相关表继续解析。";
        if (fieldName.Contains("等级", StringComparison.Ordinal))
            return "等级或阶段字段，可用于判断使用门槛、怪物强度或配置区间。";
        if (fieldName.Contains("生命", StringComparison.Ordinal) ||
            fieldName.Contains("伤害", StringComparison.Ordinal) ||
            fieldName.Contains("攻击", StringComparison.Ordinal) ||
            fieldName.Contains("防御", StringComparison.Ordinal) ||
            fieldName.Contains("抗性", StringComparison.Ordinal) ||
            fieldName.Contains("评分", StringComparison.Ordinal))
            return "数值属性字段，通常直接影响强度、掉落、门槛或系统计算。";
        if (string.IsNullOrWhiteSpace(value))
            return "这一行该字段为空。";
        return "已按字段模板显示；如果它是 ID，可以用全局搜索继续追到关联表。";
    }

    private string? TryCreateMbTablePreview(AssetEntry asset, byte[] data, IReadOnlyList<string> focusTerms, out string summary)
    {
        summary = string.Empty;
        if (asset.Kind != AssetKind.MbTable) return null;
        if (!TryDecodeTextPreview(asset, data, out string text))
        {
            summary = "这个 MB 文件暂时无法识别为可读文本表。";
            return null;
        }

        if (text.StartsWith("文件共有 ", StringComparison.Ordinal))
        {
            summary = text;
            return null;
        }

        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (lines.Length == 0)
        {
            summary = "这个 MB 表没有可显示的文本行。";
            return null;
        }

        char delimiter = ChooseMbTableDelimiter(lines);
        string[][] allRows = lines
            .Select(line => SplitMbTableLine(line, delimiter))
            .Where(row => row.Length > 0)
            .ToArray();
        int maxColumns = allRows.Length == 0 ? 0 : allRows.Max(row => row.Length);
        if (maxColumns <= 1)
        {
            summary = $"识别为文本表：共 {lines.Length:N0} 行。下方显示前 {Math.Min(lines.Length, 60):N0} 行。";
            return string.Join(Environment.NewLine, lines.Take(60));
        }

        bool hasHeader = TryGetMbHeaders(asset, allRows, maxColumns, out string[] headers, out int firstDataRow);
        string[][] dataRows = allRows.Skip(firstDataRow).ToArray();
        string[][] previewRows = GetFocusedMbRows(dataRows, focusTerms, out int matchedRows);
        int[] activeColumns = Enumerable.Range(0, maxColumns)
            .Where(column =>
                !string.IsNullOrWhiteSpace(GetColumnName(headers, column)) ||
                dataRows.Take(80).Any(row => column < row.Length && !string.IsNullOrWhiteSpace(row[column])))
            .ToArray();
        if (activeColumns.Length == 0) return null;

        const int maximumPreviewRows = 30;
        const int maximumPreviewColumns = 20;
        string delimiterName = delimiter == '\t' ? "制表符" : delimiter == ',' ? "逗号" : "空白";
        int[] visibleColumns = activeColumns.Take(maximumPreviewColumns).ToArray();
        int visibleRows = Math.Min(previewRows.Length, maximumPreviewRows);
        bool hasNamedHeaders = headers.Any(header => !string.IsNullOrWhiteSpace(header));
        summary = hasHeader
            ? $"识别到中文表头：共 {dataRows.Length:N0} 条记录、{activeColumns.Length:N0} 个有效字段；当前按字段名显示{BuildFocusSummary(matchedRows, visibleRows)}。"
            : hasNamedHeaders
                ? $"使用已知字段模板：共 {dataRows.Length:N0} 条记录、{activeColumns.Length:N0} 个有效字段；当前按字段名显示{BuildFocusSummary(matchedRows, visibleRows)}。"
                : $"解析为{delimiterName}分隔表：共 {dataRows.Length:N0} 行、约 {maxColumns:N0} 列；当前显示{BuildFocusSummary(matchedRows, visibleRows)}、{visibleColumns.Length:N0} 个有效字段。";

        var builder = new StringBuilder();
        builder.AppendLine($"表文件：{asset.Name}");
        builder.AppendLine($"包内路径：{asset.Entry.Path}");
        builder.AppendLine(hasHeader ? "字段来源：MB 表首行中文表头" : "字段来源：已知模板/数值形态推测");
        builder.AppendLine();

        builder.AppendLine("字段注释：");
        foreach (int column in visibleColumns)
        {
            builder.AppendLine($"{column + 1}. {GetColumnName(headers, column)}");
        }

        if (activeColumns.Length > visibleColumns.Length)
            builder.AppendLine($"还有 {activeColumns.Length - visibleColumns.Length:N0} 个有效字段未在当前预览中展开，可查看原始文本。");

        builder.AppendLine();
        builder.AppendLine("表格内容：");
        for (int rowIndex = 0; rowIndex < visibleRows; rowIndex++)
        {
            string[] row = previewRows[rowIndex];
            builder.AppendLine($"[{rowIndex + 1}] {BuildMbRecordTitle(headers, row)}");
            foreach (int column in visibleColumns)
            {
                if (column >= row.Length) continue;
                string value = row[column].Trim();
                if (string.IsNullOrWhiteSpace(value)) continue;
                builder.AppendLine($"  {GetColumnName(headers, column)}: {TrimPreviewCell(value)}");
            }

            string extraDetails = BuildMbRecordExtraDetails(asset, row);
            if (!string.IsNullOrWhiteSpace(extraDetails))
                builder.Append(extraDetails);
            builder.AppendLine();
        }

        int hiddenRows = previewRows.Length - visibleRows;
        if (hiddenRows > 0)
            builder.AppendLine($"……还有 {hiddenRows:N0} 条当前范围内的记录未显示，可展开原始文本或导出查看。");

        return builder.ToString();
    }

    private string BuildMbRecordExtraDetails(AssetEntry asset, IReadOnlyList<string> row)
    {
        string normalizedPath = asset.Entry.Path.Replace('\\', '/').ToLowerInvariant();
        if (normalizedPath.StartsWith("object/cha_list", StringComparison.Ordinal))
            return BuildCharacterRecordExtraDetails(row);

        if (!normalizedPath.StartsWith("life/legend_equip/legend_equip_list", StringComparison.Ordinal))
            return string.Empty;

        Dictionary<string, string[]> atbs = GetLegendEquipAtbs();
        Dictionary<string, string[]> values = GetLegendEquipAtbValues();
        if (atbs.Count == 0 || values.Count == 0) return string.Empty;

        int[] fixedAttributeColumns = { 9, 10, 11, 12, 13 };
        var lines = new List<string>();
        foreach (int column in fixedAttributeColumns)
        {
            if (column >= row.Count) continue;
            string groupId = row[column].Trim();
            if (string.IsNullOrWhiteSpace(groupId) || !atbs.TryGetValue(groupId, out string[]? groupRow)) continue;

            string valueId = groupRow.Length > 3 ? groupRow[3].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(valueId) || !values.TryGetValue(valueId, out string[]? valueRow)) continue;

            string groupName = groupRow.Length > 0 ? groupRow[0].Trim() : $"属性组{groupId}";
            string attributeName = valueRow.Length > 0 ? valueRow[0].Trim() : $"属性{valueId}";
            string percentText = valueRow.Length > 3 && valueRow[3].Trim() == "1" ? "，百分比属性" : string.Empty;
            string valueText = FormatLegendAttributeValues(valueRow);
            lines.Add($"    - 属性组 {groupId}（{groupName}）: {attributeName}{percentText}{valueText}");
        }

        if (lines.Count == 0) return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("  固定属性解析:");
        foreach (string line in lines)
            builder.AppendLine(line);
        return builder.ToString();
    }

    private string BuildCharacterRecordExtraDetails(IReadOnlyList<string> row)
    {
        if (row.Count <= 4) return string.Empty;
        string fightId = row[4].Trim();
        if (string.IsNullOrWhiteSpace(fightId)) return string.Empty;

        Dictionary<string, string[]> fightRows = GetChaFightRows();
        if (!fightRows.TryGetValue(fightId, out string[]? fightRow)) return string.Empty;

        string name = row.Count > 0 ? row[0].Trim() : "角色/怪物";
        string roleId = row.Count > 1 ? row[1].Trim() : string.Empty;
        string picId = row.Count > 5 ? row[5].Trim() : string.Empty;
        string modelSummary = BuildModelSummary(picId);
        string states = row.Count > 13 ? FormatStateList(row[13], "出生/常驻状态") : string.Empty;
        string effects = row.Count > 18 ? FormatStateList(row[18], "技能/效果关联") : string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("  怪物/Boss 属性解析:");
        builder.AppendLine($"    - 名称: {name}");
        if (!string.IsNullOrWhiteSpace(roleId))
            builder.AppendLine($"    - 角色ID: {roleId}");
        builder.AppendLine($"    - 战斗属性ID: {fightId}");
        if (!string.IsNullOrWhiteSpace(modelSummary))
            builder.Append(modelSummary);
        builder.Append(BuildFightAttributeSummary(fightRow));
        if (!string.IsNullOrWhiteSpace(states))
            builder.Append(states);
        if (!string.IsNullOrWhiteSpace(effects))
            builder.Append(effects);
        return builder.ToString();
    }

    private string BuildModelSummary(string picId)
    {
        if (string.IsNullOrWhiteSpace(picId)) return string.Empty;
        Dictionary<string, string[]> picRows = GetChaPicRows();
        if (!picRows.TryGetValue(picId, out string[]? picRow)) return string.Empty;

        var builder = new StringBuilder();
        string modelName = picRow.Length > 0 ? picRow[0].Trim() : string.Empty;
        string config = picRow.Length > 2 ? picRow[2].Trim() : string.Empty;
        string parts = picRow.Length > 3 ? picRow[3].Trim() : string.Empty;
        if (!string.IsNullOrWhiteSpace(modelName))
            builder.AppendLine($"    - 外观名称: {modelName}");
        if (!string.IsNullOrWhiteSpace(config))
            builder.AppendLine($"    - 模型配置: {config}");
        if (!string.IsNullOrWhiteSpace(parts))
            builder.AppendLine($"    - 模型部件: {TrimPreviewCell(parts)}");
        return builder.ToString();
    }

    private static string BuildFightAttributeSummary(IReadOnlyList<string> fightRow)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"    - 等级: {GetCell(fightRow, 1)}");
        builder.AppendLine($"    - 生命上限: {FormatNumberCell(GetCell(fightRow, 8))}（最终 {FormatNumberCell(GetCell(fightRow, 25))}）");
        builder.AppendLine($"    - 伤害力: {FormatNumberCell(GetCell(fightRow, 11))}（最终 {FormatNumberCell(GetCell(fightRow, 28))}）");
        builder.AppendLine($"    - 防御力: {FormatNumberCell(GetCell(fightRow, 12))}（最终 {FormatNumberCell(GetCell(fightRow, 29))}）");
        builder.AppendLine($"    - 法术抗性: {FormatNumberCell(GetCell(fightRow, 13))}");
        builder.AppendLine($"    - 物理会心: {FormatNumberCell(GetCell(fightRow, 14))}（最终 {FormatNumberCell(GetCell(fightRow, 30))}）");
        builder.AppendLine($"    - 法术会心: {FormatNumberCell(GetCell(fightRow, 15))}（最终 {FormatNumberCell(GetCell(fightRow, 32))}）");
        builder.AppendLine($"    - 综合评分/战力: {FormatNumberCell(GetCell(fightRow, 49))}");
        return builder.ToString();
    }

    private string FormatStateList(string value, string label)
    {
        string[] ids = SplitIdList(value)
            .Where(id => id != "0")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
        if (ids.Length == 0) return string.Empty;

        Dictionary<string, string[]> stateRows = GetStateDataRows();
        var parts = ids.Select(id =>
        {
            if (!stateRows.TryGetValue(id, out string[]? row) || row.Length == 0 || string.IsNullOrWhiteSpace(row[0]))
                return id;
            return $"{id}={row[0].Trim()}";
        });

        return $"    - {label}: {string.Join("；", parts)}\n";
    }

    private static IEnumerable<string> SplitIdList(string value) =>
        value.Split(new[] { '*', '|', ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private Dictionary<string, string[]> GetChaFightRows() =>
        _chaFightRows ??= LoadMbTableByColumn("object/cha_fight.txt", 0);

    private Dictionary<string, string[]> GetChaPicRows() =>
        _chaPicRows ??= LoadMbTableByColumn("object/cha_pic.txt", 1);

    private Dictionary<string, string[]> GetStateDataRows() =>
        _stateDataRows ??= LoadMbTableByColumn("skill/state_data.txt", 1);

    private Dictionary<string, string[]> GetStateGroupRows() =>
        _stateGroupRows ??= LoadMbTableByColumn("skill/state_group.txt", 1);

    private Dictionary<string, string[]> GetLegendEquipAtbs() =>
        _legendEquipAtbs ??= LoadMbTableByColumn("life/legend_equip/legend_equip_atbs.txt", 1);

    private Dictionary<string, string[]> GetLegendEquipAtbValues() =>
        _legendEquipAtbValues ??= LoadMbTableByColumn("life/legend_equip/legend_equip_atb_value.txt", 1);

    private Dictionary<string, string[]> LoadMbTableByColumn(string path, int keyColumn)
    {
        AssetEntry? table = _workspace.Assets.FirstOrDefault(asset =>
            asset.Kind == AssetKind.MbTable &&
            asset.Entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (table is null) return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        try
        {
            byte[] data = _workspace.Extract(table);
            if (!TryDecodeTextPreview(table, data, out string text))
                return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] row = line.Split('\t');
                if (keyColumn >= row.Length) continue;
                string key = row[keyColumn].Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;
                result[key] = row;
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string FormatLegendAttributeValues(IReadOnlyList<string> row)
    {
        string[] labels = { "低档", "中档", "高档", "极品", "满值" };
        int[] columns = { 7, 10, 11, 13, 14 };
        var parts = new List<string>();
        for (int i = 0; i < columns.Length; i++)
        {
            int column = columns[i];
            if (column >= row.Count || string.IsNullOrWhiteSpace(row[column])) continue;
            parts.Add($"{labels[i]} {row[column].Trim()}");
        }

        return parts.Count == 0 ? string.Empty : $"（{string.Join("，", parts)}）";
    }

    private static string[][] GetFocusedMbRows(IReadOnlyList<string[]> rows, IReadOnlyList<string> focusTerms, out int matchedRows)
    {
        matchedRows = 0;
        string[] usableTerms = focusTerms
            .Where(term => term.Length >= 2)
            .SelectMany(GetSearchTermVariants)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (usableTerms.Length == 0) return rows.ToArray();

        string[][] matches = rows
            .Where(row => usableTerms.All(term =>
                row.Any(cell => cell.Contains(term, StringComparison.OrdinalIgnoreCase))))
            .ToArray();
        matchedRows = matches.Length;
        return matches.Length > 0 ? matches : rows.ToArray();
    }

    private static string BuildFocusSummary(int matchedRows, int visibleRows) =>
        matchedRows > 0
            ? $"匹配到的前 {visibleRows:N0} 条"
            : $"前 {visibleRows:N0} 条";

    private static IEnumerable<string> GetSearchTermVariants(string term)
    {
        yield return term;
        string scaleVariant = term.Replace('麟', '鳞');
        if (!scaleVariant.Equals(term, StringComparison.Ordinal))
            yield return scaleVariant;
        string unicornVariant = term.Replace('鳞', '麟');
        if (!unicornVariant.Equals(term, StringComparison.Ordinal))
            yield return unicornVariant;

        if (term.Contains("骑宠", StringComparison.OrdinalIgnoreCase) ||
            term.Contains("坐骑", StringComparison.OrdinalIgnoreCase) ||
            term.Contains("ride", StringComparison.OrdinalIgnoreCase) ||
            term.Contains("mount", StringComparison.OrdinalIgnoreCase))
        {
            yield return "ride";
            yield return "坐骑";
            yield return "骑宠";
            yield return "pet";
            yield return "ride_list";
            yield return "fly_ride";
            yield return "car_equip";
        }
    }

    private static bool TryDecodeTextPreview(AssetEntry asset, byte[] data, out string text)
    {
        text = string.Empty;
        if (!ResourceExplanationService.IsTextPreviewSupported(asset) || data.Length == 0) return false;
        int maximumPreviewBytes = asset.Kind == AssetKind.MbTable
            ? 8 * 1024 * 1024
            : 2 * 1024 * 1024;
        if (data.Length > maximumPreviewBytes)
        {
            text = $"文件共有 {FormatBytes(data.Length)}，为避免界面卡顿，请导出后使用文本编辑器查看。";
            return true;
        }

        text = DecodeText(data).Trim('\0', '\uFEFF', ' ', '\r', '\n');
        if (text.Length == 0) return false;
        int controlCharacters = text.Count(character =>
            char.IsControl(character) && character is not '\r' and not '\n' and not '\t');
        return controlCharacters <= Math.Max(4, text.Length / 100);
    }

    private static string DecodeText(byte[] data)
    {
        Encoding[] encodings =
        {
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            Encoding.GetEncoding(936),
            Encoding.GetEncoding("GB18030")
        };

        string bestText = string.Empty;
        int bestScore = int.MaxValue;
        foreach (Encoding encoding in encodings)
        {
            string candidate;
            try
            {
                candidate = encoding.GetString(data);
            }
            catch (DecoderFallbackException)
            {
                continue;
            }

            int score = ScoreDecodedText(candidate);
            if (score >= bestScore) continue;
            bestScore = score;
            bestText = candidate;
        }

        return bestText.Length > 0 ? bestText : Encoding.UTF8.GetString(data);
    }

    private static int ScoreDecodedText(string text)
    {
        int replacementCharacters = text.Count(character => character == '\uFFFD');
        int controlCharacters = text.Count(character =>
            char.IsControl(character) && character is not '\r' and not '\n' and not '\t');
        return replacementCharacters * 10_000 + controlCharacters * 100;
    }

    private static char ChooseMbTableDelimiter(IReadOnlyList<string> lines)
    {
        int tabCount = lines.Take(20).Sum(line => line.Count(character => character == '\t'));
        if (tabCount > 0) return '\t';
        int commaCount = lines.Take(20).Sum(line => line.Count(character => character == ','));
        return commaCount > 0 ? ',' : ' ';
    }

    private static string[] SplitMbTableLine(string line, char delimiter)
    {
        if (delimiter == ' ')
            return Regex.Split(line.Trim(), @"\s+");
        return line.Split(delimiter);
    }

    private static bool TryGetMbHeaders(
        AssetEntry asset,
        IReadOnlyList<string[]> rows,
        int maxColumns,
        out string[] headers,
        out int firstDataRow)
    {
        headers = GetKnownMbHeaders(asset, maxColumns);
        firstDataRow = 0;
        if (rows.Count == 0) return headers.Any(header => !string.IsNullOrWhiteSpace(header));

        string[] firstRow = rows[0];
        if (LooksLikeHeaderRow(firstRow, rows.Skip(1).Take(12).ToArray()))
        {
            headers = NormalizeHeaderRow(firstRow, maxColumns);
            firstDataRow = 1;
            return true;
        }

        return false;
    }

    private static string[] GetKnownMbHeaders(AssetEntry asset, int maxColumns)
    {
        string normalizedPath = asset.Entry.Path.Replace('\\', '/').ToLowerInvariant();
        string fileName = System.IO.Path.GetFileNameWithoutExtension(asset.Name).ToLowerInvariant();
        string[] headers = new string[maxColumns];

        if (fileName.StartsWith("insect_type_", StringComparison.Ordinal))
        {
            FillHeaders(headers, "等级名称", "等级", "门槛/消耗数值", "奖励或对象ID", "类型/组ID");
        }
        else if (fileName == "insect_items")
        {
            FillHeaders(headers, "名称", "物品或技能ID", "类别/阵营", "标记值", "图标或关联ID", "关联ID列表");
        }
        else if (fileName == "insect_attributes")
        {
            FillHeaders(headers, "单位名称", "等级/序号", "生命/血量", "攻击/伤害", "比例/概率", "参数6", "参数7", "参数8", "参数9", "参数10", "参数11", "参数12", "参数13", "参数14", "参数15", "参数16", "参数17", "参数18", "参数19", "参数20");
        }
        else if (fileName == "insect_extra_attributes")
        {
            FillHeaders(headers, "效果名称", "效果ID");
        }
        else if (fileName == "insect_team")
        {
            FillHeaders(headers, "队伍/阵营名称", "队伍ID", "出生点或入口ID", "出生点名称", "关联玩法ID");
        }
        else if (normalizedPath.StartsWith("equip/", StringComparison.Ordinal))
        {
            FillHeaders(headers, "外观/装备名称", "外观ID", "男模型资源", "女模型资源", "职业限制", "性别限制", "显示ID", "备注");
        }
        else if (normalizedPath.StartsWith("life/legend_equip/legend_equip_list", StringComparison.Ordinal))
        {
            FillHeaders(headers, "物品ID", "装备名称", "保留字段", "装备模板/模型ID", "职业/系别ID", "基础生命", "基础攻击/法效", "基础防御/抗性", "基础属性", "附加属性", "固定属性组1", "固定属性组2", "固定属性组3", "固定属性组4", "套装或部位ID", "保留", "保留", "保留", "保留", "品质/阶段", "可随机属性组", "可洗练属性组", "标记");
        }
        else if (normalizedPath.StartsWith("life/legend_equip/legend_equip_atbs", StringComparison.Ordinal))
        {
            FillHeaders(headers, "属性组名称", "属性组ID", "权重", "属性值ID", "附加属性2", "附加属性3", "附加属性4", "附加属性5", "附加属性6", "备注");
        }
        else if (normalizedPath.StartsWith("life/legend_equip/legend_equip_atb_value", StringComparison.Ordinal))
        {
            FillHeaders(headers, "属性名称", "属性值ID", "属性类型ID", "是否百分比", "条件1", "条件2", "条件3", "低档值", "低档显示", "保底值", "中档值", "高档值", "浮动", "极品值", "满值", "显示倍率", "权重组");
        }
        else if (normalizedPath.StartsWith("scn/scn_info", StringComparison.Ordinal))
        {
            FillHeaders(headers, "场景代码", "场景ID", "关联区域/副本ID", "场景大类", "场景类型", "进入限制/阵营", "保留", "推荐人数", "可视距离", "地图宽", "地图高", "是否副本", "出生X", "出生Y", "缩放", "加载图", "默认出生点", "关联标记", "场景名称", "建议等级");
        }
        else if (normalizedPath.StartsWith("object/cha_list", StringComparison.Ordinal))
        {
            FillHeaders(headers, "角色/怪物名称", "角色ID", "等级显示", "角色类型", "战斗属性ID", "外观模型ID", "保留", "保留", "显示/阵营类型", "保留", "难度/玩法标记", "保留", "保留", "出生/常驻状态ID列表", "保留", "保留", "保留", "保留", "技能/效果ID列表", "保留", "保留", "保留", "保留", "保留", "保留", "保留", "保留", "视野/警戒范围", "追击/活动范围");
        }
        else if (normalizedPath.StartsWith("object/cha_fight", StringComparison.Ordinal))
        {
            FillHeaders(headers, "战斗属性ID", "等级", "类型", "攻击系/模板", "体魄基数", "力量基数", "筋骨基数", "元神基数", "生命上限", "法力/体力", "基础抗会心", "伤害力", "防御力", "法术抗性", "物理会心", "法术会心", "列16", "列17", "保留", "保留", "保留", "保留", "属性合计", "忽略防御", "移动/速度参数", "最终生命", "最终法力/体力", "最终基础抗会心", "最终伤害力", "最终防御力", "最终物理会心", "伤害倍率", "最终法术会心", "抗性倍率", "列34", "列35", "最终属性合计", "生命缩放", "速度/范围", "列39", "伤害力放大值", "防御力放大值", "会心放大值", "抗性放大值", "列44", "列45", "评分/战力分项", "总评分/战力", "额外评分", "综合评分");
        }
        else if (normalizedPath.StartsWith("object/cha_pic", StringComparison.Ordinal))
        {
            FillHeaders(headers, "外观名称", "外观模型ID", "组合配置路径", "挂件/部件列表", "缩放", "颜色/染色", "保留", "保留", "保留", "保留", "外观类型", "性别", "职业", "头部参数", "身体参数", "保留", "保留", "保留", "保留", "保留", "图标", "头像宽", "头像高");
        }

        return headers;
    }

    private static void FillHeaders(string[] headers, params string[] names)
    {
        for (int i = 0; i < headers.Length && i < names.Length; i++)
            headers[i] = names[i];
    }

    private static bool LooksLikeHeaderRow(IReadOnlyList<string> firstRow, IReadOnlyList<string[]> nextRows)
    {
        if (firstRow.Count < 3) return false;
        int namedColumns = firstRow.Count(value =>
            !string.IsNullOrWhiteSpace(value) &&
            (ContainsChinese(value) ||
             value.Contains("ID", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("id", StringComparison.OrdinalIgnoreCase)));
        int headerKeywords = firstRow.Count(value =>
            value.Contains("名称", StringComparison.Ordinal) ||
            value.Contains("属性", StringComparison.Ordinal) ||
            value.Contains("等级", StringComparison.Ordinal) ||
            value.Contains("备注", StringComparison.Ordinal) ||
            value.Contains("说明", StringComparison.Ordinal) ||
            value.Contains("限制", StringComparison.Ordinal) ||
            value.Contains("价格", StringComparison.Ordinal));
        int mostlyNumericInNextRows = 0;
        foreach (int column in Enumerable.Range(0, Math.Min(firstRow.Count, 12)))
        {
            int filled = 0;
            int numeric = 0;
            foreach (string[] row in nextRows)
            {
                if (column >= row.Length || string.IsNullOrWhiteSpace(row[column])) continue;
                filled++;
                if (double.TryParse(row[column], out _)) numeric++;
            }

            if (filled > 0 && numeric >= filled / 2) mostlyNumericInNextRows++;
        }

        return headerKeywords >= 2 || namedColumns >= Math.Max(3, firstRow.Count / 3) && mostlyNumericInNextRows >= 2;
    }

    private static string[] NormalizeHeaderRow(IReadOnlyList<string> row, int maxColumns)
    {
        string[] headers = new string[maxColumns];
        for (int i = 0; i < headers.Length; i++)
        {
            string name = i < row.Count ? row[i].Trim() : string.Empty;
            headers[i] = string.IsNullOrWhiteSpace(name) ? $"字段{i + 1}" : name;
        }

        return headers;
    }

    private static bool ContainsChinese(string value) =>
        value.Any(character => character >= '\u4E00' && character <= '\u9FFF');

    private static string GetColumnName(IReadOnlyList<string> headers, int column)
    {
        if (column < headers.Count && !string.IsNullOrWhiteSpace(headers[column]))
            return headers[column];
        return $"字段{column + 1}";
    }

    private static string BuildMbRecordTitle(IReadOnlyList<string> headers, IReadOnlyList<string> row)
    {
        int nameColumn = FindColumn(headers, "名称", "名字", "备注", "场景名称", "装备名称", "物品名称", "单位名称");
        int idColumn = FindColumn(headers, "ID", "编号", "等级");
        string name = nameColumn >= 0 && nameColumn < row.Count && !string.IsNullOrWhiteSpace(row[nameColumn])
            ? row[nameColumn].Trim()
            : row.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "记录";
        string id = idColumn >= 0 && idColumn < row.Count && !string.IsNullOrWhiteSpace(row[idColumn])
            ? row[idColumn].Trim()
            : string.Empty;
        return string.IsNullOrWhiteSpace(id) || id == name ? name : $"{name}（ID/编号：{id}）";
    }

    private static int FindColumn(IReadOnlyList<string> headers, params string[] keywords)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            string header = headers[i];
            if (string.IsNullOrWhiteSpace(header)) continue;
            if (keywords.Any(keyword => header.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                return i;
        }

        return -1;
    }

    private static string GuessMbColumnName(AssetEntry asset, int columnIndex, IReadOnlyList<string[]> sampleRows)
    {
        string fileName = System.IO.Path.GetFileNameWithoutExtension(asset.Name).ToLowerInvariant();
        if (fileName == "insect_items")
        {
            return columnIndex switch
            {
                0 => "名称/显示文本",
                1 => "物品或技能ID",
                2 => "类别/阵营",
                3 => "标记值",
                4 => "图标或关联ID",
                5 => "关联ID列表",
                _ => "扩展参数"
            };
        }

        if (fileName == "insect_attributes")
        {
            return columnIndex switch
            {
                0 => "单位名称",
                1 => "等级/序号",
                2 => "生命/血量类数值",
                3 => "攻击/伤害类数值",
                4 => "比例/概率参数",
                _ => "属性参数"
            };
        }

        if (fileName == "insect_extra_attributes")
        {
            return columnIndex switch
            {
                0 => "效果名称",
                1 => "效果ID",
                _ => "属性槽/效果值"
            };
        }

        if (fileName.StartsWith("insect_type_", StringComparison.Ordinal))
        {
            return columnIndex switch
            {
                0 => "等级名称",
                1 => "等级",
                2 => "门槛/消耗数值",
                3 => "奖励或对象ID",
                4 => "类型/组ID",
                _ => "扩展参数"
            };
        }

        if (fileName == "insect_team")
        {
            return columnIndex switch
            {
                0 => "队伍/阵营名称",
                1 => "队伍ID",
                _ => "队伍参数"
            };
        }

        if (columnIndex == 0) return "名称/显示文本";
        if (ColumnLooksLikeMultiValue(sampleRows, columnIndex)) return "关联ID列表/多值";
        if (ColumnLooksNumeric(sampleRows, columnIndex)) return columnIndex == 1 ? "编号ID/等级" : "数值/编号";
        return "文本/参数";
    }

    private static bool ColumnLooksNumeric(IReadOnlyList<string[]> rows, int columnIndex)
    {
        int filled = 0;
        int numeric = 0;
        foreach (string[] row in rows)
        {
            if (columnIndex >= row.Length || string.IsNullOrWhiteSpace(row[columnIndex])) continue;
            filled++;
            if (double.TryParse(row[columnIndex], out _)) numeric++;
        }

        return filled > 0 && numeric >= Math.Max(1, filled * 2 / 3);
    }

    private static bool ColumnLooksLikeMultiValue(IReadOnlyList<string[]> rows, int columnIndex)
    {
        int filled = 0;
        int multiValue = 0;
        foreach (string[] row in rows)
        {
            if (columnIndex >= row.Length || string.IsNullOrWhiteSpace(row[columnIndex])) continue;
            filled++;
            if (row[columnIndex].Contains('*') || row[columnIndex].Contains('|') || row[columnIndex].Contains(';'))
                multiValue++;
        }

        return filled > 0 && multiValue >= Math.Max(1, filled / 2);
    }

    private static string TrimPreviewCell(string value)
    {
        string normalized = value.Trim();
        return normalized.Length <= 72 ? normalized : normalized[..69] + "...";
    }

    private static string GetCell(IReadOnlyList<string> row, int index) =>
        index < row.Count ? row[index].Trim() : string.Empty;

    private static string FormatNumberCell(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string normalized = value.Trim();
        if (decimal.TryParse(normalized.TrimEnd('%'), out decimal number))
        {
            string suffix = normalized.EndsWith('%') ? "%" : string.Empty;
            return number == decimal.Truncate(number)
                ? $"{number:N0}{suffix}"
                : $"{number:N2}{suffix}";
        }

        return normalized;
    }

    private static string GetMbTableDisplayName(AssetEntry asset)
    {
        string fileName = System.IO.Path.GetFileNameWithoutExtension(asset.Name);
        string normalized = fileName.ToLowerInvariant();
        string normalizedPath = asset.Entry.Path.Replace('\\', '/').ToLowerInvariant();
        if (normalizedPath == "object/ride_list.txt") return "骑宠/坐骑总表";
        if (normalizedPath == "object/ride_connection.txt") return "骑宠挂接/连接表";
        if (normalizedPath == "object/ride_merge.txt") return "骑宠融合/合成表";
        if (normalizedPath == "object/fly_ride_energy.txt") return "飞行骑宠能量表";
        if (normalizedPath == "pet/pet_list.txt") return "宠物/侍宠总表";
        if (normalizedPath == "pet/pet_model.txt") return "宠物/骑宠模型表";
        if (normalizedPath.StartsWith("pet/car_", StringComparison.Ordinal)) return "骑宠装备/车类配置表";
        if (normalized == "insect_team") return "虫子对抗队伍表";
        if (normalized == "insect_items") return "虫子对抗道具/技能表";
        if (normalized == "insect_attributes") return "虫子属性表";
        if (normalized == "insect_extra_attributes") return "虫子额外效果表";
        if (normalized.StartsWith("insect_type_", StringComparison.Ordinal))
            return $"虫子等级配置表 {normalized["insect_type_".Length..]}";
        return fileName;
    }

    private void RefreshDungeonSummary()
    {
        if (_workspace.Assets.Count == 0)
        {
            DungeonList.ItemsSource = null;
            DungeonMonsterList.ItemsSource = null;
            DungeonSummaryStatusText.Text = "请先打开资源目录。";
            DungeonSummaryCountText.Text = string.Empty;
            ShowDungeonMonsterDetails(null);
            return;
        }

        EnsureDungeonSummaries();
        string[] terms = GetSearchTerms();
        List<DungeonSummaryViewModel> visibleDungeons = _dungeonSummaries
            .Where(dungeon => terms.Length == 0 || DungeonMatchesSearch(dungeon, terms))
            .ToList();

        DungeonList.ItemsSource = visibleDungeons;
        DungeonSummaryCountText.Text = $"{visibleDungeons.Sum(dungeon => GetVisibleDungeonMonsters(dungeon, terms).Count):N0} 个怪物";
        DungeonSummaryStatusText.Text = terms.Length == 0
            ? "已汇总副本怪物、头像、核心属性、隐藏状态和掉落组。"
            : $"当前搜索：{string.Join(" ", terms)}";

        DungeonSummaryViewModel? preferred = visibleDungeons.FirstOrDefault(dungeon =>
            _selectedDungeonSummary is not null && dungeon.Name == _selectedDungeonSummary.Name) ??
            visibleDungeons.FirstOrDefault();
        DungeonList.SelectedItem = preferred;
        if (preferred is not null)
            ShowDungeonSummary(preferred);
        else
        {
            DungeonMonsterList.ItemsSource = null;
            DungeonMonsterListTitleText.Text = "怪物";
            DungeonMonsterListCountText.Text = "0 个";
            ShowDungeonMonsterDetails(null);
        }
    }

    private void EnsureDungeonSummaries()
    {
        if (_dungeonSummariesBuilt) return;
        _dungeonSummaries = DungeonDefinitions
            .Select(BuildDungeonSummary)
            .ToList();
        _dungeonSummariesBuilt = true;
        _ = LoadDungeonPortraitsAsync(_dungeonSummaries.SelectMany(dungeon => dungeon.Monsters).ToArray());
    }

    private DungeonSummaryViewModel BuildDungeonSummary(DungeonDefinition definition)
    {
        Dictionary<string, string[]> chaRows = GetChaListRows();
        Dictionary<string, string[]> fightRows = GetChaFightRows();
        Dictionary<string, string[]> picRows = GetChaPicRows();
        Dictionary<string, List<string[]>> skillRowsByPic = GetChaSkillRowsByPicId();
        Dictionary<string, List<string>> rewardProxyIdsByName = GetDungeonRewardProxyIds(definition);
        HashSet<string> rewardProxyRoleIds = rewardProxyIdsByName.Values
            .SelectMany(ids => ids)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bossRoleIds = new HashSet<string>(definition.BossMonsterIds, StringComparer.OrdinalIgnoreCase);
        var candidateRoleIds = new HashSet<string>(definition.KnownMonsterIds, StringComparer.OrdinalIgnoreCase);

        var candidatePicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, List<string[]>> pair in skillRowsByPic)
        {
            if (pair.Value.Any(row => definition.Aliases.Any(alias =>
                    row.Any(cell => cell.Contains(alias, StringComparison.OrdinalIgnoreCase)))))
            {
                candidatePicIds.Add(pair.Key);
            }
        }

        foreach (KeyValuePair<string, string[]> pair in picRows)
        {
            if (definition.Aliases.Any(alias => pair.Value.Any(cell =>
                    cell.Contains(alias, StringComparison.OrdinalIgnoreCase))))
            {
                candidatePicIds.Add(pair.Key);
            }
        }

        var monsters = new List<DungeonMonsterViewModel>();
        foreach (string[] row in chaRows.Values)
        {
            string name = CleanMonsterName(GetCell(row, 0));
            string roleId = GetCell(row, 1);
            string fightId = GetCell(row, 4);
            string picId = GetCell(row, 5);
            if (definition.ExcludedRoleIds?.Contains(roleId) == true) continue;
            if (string.IsNullOrWhiteSpace(roleId) ||
                string.IsNullOrWhiteSpace(fightId) ||
                !fightRows.TryGetValue(fightId, out string[]? fightRow))
            {
                continue;
            }

            bool rowMentionsDungeon = definition.Aliases.Any(alias =>
                row.Any(cell => cell.Contains(alias, StringComparison.OrdinalIgnoreCase)));
            bool picMentionsDungeon = candidatePicIds.Contains(picId);
            bool knownRole = candidateRoleIds.Contains(roleId);
            bool inKnownRange = IsInKnownRange(roleId, definition.RoleIdRanges);
            bool directCandidate = knownRole || inKnownRange;
            bool aliasCandidate = (rowMentionsDungeon || picMentionsDungeon) &&
                                  IsCredibleAliasDungeonMonster(row, fightRow, definition);
            if (!directCandidate && !aliasCandidate) continue;

            if (rewardProxyRoleIds.Contains(roleId) && IsRewardProxyOnly(name)) continue;
            if (ShouldSkipDungeonMonsterRow(name, row, directCandidate)) continue;

            picRows.TryGetValue(picId, out string[]? picRow);
            monsters.Add(BuildDungeonMonster(row, fightRow, picRow, definition, bossRoleIds, rewardProxyIdsByName, chaRows));
        }

        List<DungeonMonsterViewModel> deduped = monsters
            .GroupBy(monster => $"{monster.Name}|{monster.FightId}|{monster.PicId}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                DungeonMonsterViewModel chosen = group
                    .OrderBy(monster => monster.SortRank)
                    .ThenBy(monster =>
                        definition.KnownMonsterIds.Contains(monster.RoleId, StringComparer.OrdinalIgnoreCase) ||
                        IsInKnownRange(monster.RoleId, definition.RoleIdRanges) ? 0 : 1)
                    .ThenByDescending(HasResolvedDrops)
                    .ThenByDescending(monster => ParseIntOrDefault(monster.RoleId))
                    .First();
                chosen.SourceIds = string.Join("、", group
                    .Select(monster => monster.RoleId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => ParseIntOrDefault(id)));
                return chosen;
            })
            .OrderBy(monster => monster.SortRank)
            .ThenBy(monster => monster.Name, NaturalStringComparer.Instance)
            .ToList();

        return new DungeonSummaryViewModel(definition.Name, $"{definition.Subtitle} · {deduped.Count:N0} 个可识别怪物", deduped);
    }

    private DungeonMonsterViewModel BuildDungeonMonster(
        IReadOnlyList<string> chaRow,
        IReadOnlyList<string> fightRow,
        IReadOnlyList<string>? picRow,
        DungeonDefinition definition,
        IReadOnlySet<string> bossRoleIds,
        IReadOnlyDictionary<string, List<string>> rewardProxyIdsByName,
        IReadOnlyDictionary<string, string[]> chaRows)
    {
        string name = CleanMonsterName(GetCell(chaRow, 0));
        string roleId = GetCell(chaRow, 1);
        string fightId = GetCell(chaRow, 4);
        string picId = GetCell(chaRow, 5);
        string iconName = picRow is not null ? NormalizePortraitIconName(GetCell(picRow, 26)) : string.Empty;
        string roleLabel = definition.RoleLabels is not null &&
                           definition.RoleLabels.TryGetValue(roleId, out string? label)
            ? label
            : string.Empty;
        bool isMechanism = definition.MechanismRoleIds?.Contains(roleId) == true ||
                           name.Contains("宝箱", StringComparison.Ordinal) ||
                           name.Contains("首饰盒", StringComparison.Ordinal) ||
                           name.Contains("蛛囊", StringComparison.Ordinal) ||
                           name.Contains("尖刺", StringComparison.Ordinal);
        bool isBoss = !isMechanism && IsBossMonster(name, roleId, bossRoleIds);
        string kind = !string.IsNullOrWhiteSpace(roleLabel)
            ? roleLabel
            : isBoss ? "首领" : isMechanism ? "机制/箱子" : "小怪";
        FightStatSnapshot adjustedStats = BuildAdjustedFightStats(fightRow, chaRow);
        IReadOnlyList<StatLineViewModel> stats = BuildDungeonMonsterStats(fightRow, chaRow);
        IReadOnlyList<string> hiddenStates = BuildHiddenStateSummaries(chaRow);
        IReadOnlyList<string[]> rewardRows = ResolveRewardProxyRows(name, roleId, rewardProxyIdsByName, chaRows);
        IReadOnlyList<DropLineViewModel> drops = BuildMonsterDrops(chaRow, rewardRows);
        string hiddenSummary = hiddenStates.Count == 0
            ? string.Empty
            : string.Join("\n", hiddenStates.Select(state => "· " + state));
        string phaseSummary = BuildPhaseSummary(picId);
        if (!string.IsNullOrWhiteSpace(phaseSummary))
            hiddenSummary = string.IsNullOrWhiteSpace(hiddenSummary)
                ? "· " + phaseSummary
                : hiddenSummary + "\n· " + phaseSummary;

        return new DungeonMonsterViewModel
        {
            Name = name,
            RoleId = roleId,
            FightId = fightId,
            PicId = picId,
            IconName = iconName,
            KindText = kind,
            Subtitle = $"{definition.Name} · {kind} · ID {roleId} · 战斗属性 {fightId}",
            CompactStats = $"等级 {adjustedStats.Level} · 生命 {adjustedStats.Health} · 伤害 {adjustedStats.Damage} · 物防 {adjustedStats.PhysicalDefense} · 法抗 {adjustedStats.MagicResistance}",
            Explanation = string.Empty,
            HiddenSummary = hiddenSummary,
            SourceIds = roleId,
            Stats = stats,
            Drops = drops,
            SortRank = isBoss ? 0 : isMechanism ? 20 : 10
        };
    }

    private static bool HasResolvedDrops(DungeonMonsterViewModel monster) =>
        monster.Drops.Any(drop => !drop.Name.Contains("未识别", StringComparison.Ordinal));

    private static int ParseIntOrDefault(string value) =>
        int.TryParse(value, out int parsed) ? parsed : 0;

    private static bool IsInKnownRange(string roleId, IReadOnlyList<IntRange> ranges)
    {
        if (!int.TryParse(roleId, out int id)) return false;
        return ranges.Any(range => id >= range.Start && id <= range.End);
    }

    private static bool IsCredibleAliasDungeonMonster(
        IReadOnlyList<string> chaRow,
        IReadOnlyList<string> fightRow,
        DungeonDefinition definition)
    {
        bool hasCuratedIds = definition.KnownMonsterIds.Count > 0 ||
                             definition.BossMonsterIds.Count > 0 ||
                             definition.RoleIdRanges.Count > 0;
        if (hasCuratedIds) return false;
        if (IsLikelyNonMonsterName(CleanMonsterName(GetCell(chaRow, 0)))) return false;
        if (GetCell(chaRow, 4) == "1" && !HasDropGroups(chaRow)) return false;
        if (!int.TryParse(GetCell(fightRow, 1), out int level)) return false;

        int minimumLevel = ExtractMinimumDungeonLevel(definition.Subtitle);
        return minimumLevel <= 0 || level >= Math.Max(1, minimumLevel - 30);
    }

    private static bool ShouldSkipDungeonMonsterRow(string name, IReadOnlyList<string> row, bool directCandidate)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        if (IsLikelyNonMonsterName(name)) return true;
        if (GetCell(row, 4) == "1" && !HasDropGroups(row)) return true;
        if (!directCandidate && GetCell(row, 8) == "110") return true;
        return false;
    }

    private static bool IsLikelyNonMonsterName(string name)
    {
        string[] blockedTerms =
        {
            "演出用", "透明", "占位", "测试", "传送", "入口", "出口", "返回", "离开",
            "出生点", "光柱", "特效", "镜头", "寻路", "空气墙", "无敌"
        };
        return blockedTerms.Any(term => name.Contains(term, StringComparison.Ordinal));
    }

    private static int ExtractMinimumDungeonLevel(string subtitle)
    {
        Match match = Regex.Match(subtitle, @"(\d+)\s*级");
        return match.Success && int.TryParse(match.Groups[1].Value, out int level) ? level : 0;
    }

    private Dictionary<string, List<string>> GetDungeonRewardProxyIds(DungeonDefinition definition)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string[] row in LoadMbRows("etc/recomdaily/recomdaily_boss.txt"))
        {
            string line = string.Join('\t', row);
            if (!definition.Aliases.Any(alias => line.Contains(alias, StringComparison.OrdinalIgnoreCase)))
                continue;

            foreach (Match match in Regex.Matches(line, @"<help_bank:7,(\d+),([^>]+)>"))
            {
                string roleId = match.Groups[1].Value.Trim();
                string monsterName = ExtractRecommendationMonsterName(match.Groups[2].Value);
                if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(monsterName))
                    continue;

                if (!result.TryGetValue(monsterName, out List<string>? ids))
                {
                    ids = new List<string>();
                    result[monsterName] = ids;
                }

                if (!ids.Contains(roleId, StringComparer.OrdinalIgnoreCase))
                    ids.Add(roleId);
            }
        }

        return result;
    }

    private static string ExtractRecommendationMonsterName(string text)
    {
        string result = text.Trim();
        int index = result.LastIndexOf('的');
        if (index >= 0 && index + 1 < result.Length)
            result = result[(index + 1)..];
        result = result.Replace("（简单难度）", string.Empty)
            .Replace("（正常难度）", string.Empty)
            .Replace("副本", string.Empty)
            .Trim();
        return CleanMonsterName(result);
    }

    private static bool MonsterNameMatches(string name, string expected)
    {
        string left = CleanMonsterName(name);
        string right = CleanMonsterName(expected);
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        if (left.Equals(right, StringComparison.OrdinalIgnoreCase)) return true;
        if (right.Length >= 2 && left.Contains(right, StringComparison.OrdinalIgnoreCase)) return true;
        return left.Length >= 3 && right.Contains(left, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRewardProxyOnly(string name) =>
        name.Contains("宝箱", StringComparison.Ordinal) ||
        name.Contains("首饰盒", StringComparison.Ordinal);

    private static IReadOnlyList<string[]> ResolveRewardProxyRows(
        string monsterName,
        string roleId,
        IReadOnlyDictionary<string, List<string>> rewardProxyIdsByName,
        IReadOnlyDictionary<string, string[]> chaRows)
    {
        var rows = new List<string[]>();
        foreach (KeyValuePair<string, List<string>> pair in rewardProxyIdsByName)
        {
            if (!MonsterNameMatches(monsterName, pair.Key)) continue;
            foreach (string proxyId in pair.Value)
            {
                if (proxyId.Equals(roleId, StringComparison.OrdinalIgnoreCase)) continue;
                if (chaRows.TryGetValue(proxyId, out string[]? row))
                    rows.Add(row);
            }
        }

        return rows;
    }

    private string BuildPhaseSummary(string picId)
    {
        if (string.IsNullOrWhiteSpace(picId)) return string.Empty;
        if (!GetChaSkillRowsByPicId().TryGetValue(picId, out List<string[]>? skillRows))
            return string.Empty;

        var ranges = new List<(decimal Low, decimal High)>();
        foreach (string[] row in skillRows)
        {
            for (int column = 0; column + 2 < row.Length; column++)
            {
                if (GetCell(row, column) != "10") continue;
                if (!decimal.TryParse(GetCell(row, column + 1), out decimal low) ||
                    !decimal.TryParse(GetCell(row, column + 2), out decimal high))
                {
                    continue;
                }

                if (low < 0) low = 0;
                if (high <= 0 || high > 100) continue;
                ranges.Add((low, high));
            }
        }

        var texts = ranges
            .Distinct()
            .OrderByDescending(range => range.High)
            .ThenByDescending(range => range.Low)
            .Take(8)
            .Select(range => $"{range.Low:0}-{range.High:0}%")
            .ToArray();
        return texts.Length == 0 ? string.Empty : $"血量阶段：{string.Join("、", texts)}";
    }

    private IReadOnlyList<StatLineViewModel> BuildDungeonMonsterStats(IReadOnlyList<string> fightRow, IReadOnlyList<string> chaRow)
    {
        string lockBlood = BuildLockBloodSummary(chaRow);
        FightStatSnapshot adjustedStats = BuildAdjustedFightStats(fightRow, chaRow);
        Dictionary<string, List<StateAttributeAdjustment>> directAttributes = GetDirectStateAttributes(chaRow);
        var stats = new List<StatLineViewModel>
        {
            new("等级", adjustedStats.Level, string.Empty),
            new("生命", adjustedStats.Health, string.Empty),
            new("锁血", lockBlood, string.Empty),
            new("物理/普通伤害", adjustedStats.Damage, string.Empty),
            new("物理防御", adjustedStats.PhysicalDefense, string.Empty),
            new("法术抗性", adjustedStats.MagicResistance, string.Empty),
            new("抵抗会心几率", adjustedStats.CritResistance, string.Empty),
            new("会心伤害减免", FormatDirectAttribute(directAttributes, "68"), string.Empty),
            new("伤害/幸运减免", FormatDirectAttribute(directAttributes, "32"), string.Empty),
            new("忽略物防", FormatDirectAttribute(directAttributes, "45"), string.Empty),
            new("忽略法抗", FormatDirectAttribute(directAttributes, "46"), string.Empty),
            new("物理会伤上限", FormatDirectAttribute(directAttributes, "19"), string.Empty),
            new("法术会伤上限", FormatDirectAttribute(directAttributes, "20"), string.Empty),
            new("综合评分/战力", adjustedStats.FightValue, string.Empty)
        };
        return stats.Where(stat => !string.IsNullOrWhiteSpace(stat.Value)).ToList();
    }

    private FightStatSnapshot BuildAdjustedFightStats(IReadOnlyList<string> fightRow, IReadOnlyList<string> chaRow)
    {
        Dictionary<string, List<StateAttributeAdjustment>> attributes = GetDirectStateAttributes(chaRow);

        decimal physicalDefense = CalculateAdjustedAttribute(ParseDecimalCell(GetCell(fightRow, 30)), attributes, "15");
        decimal magicResistance = CalculateAdjustedAttribute(ParseDecimalCell(GetCell(fightRow, 32)), attributes, "29");
        decimal critResistance = CalculateAdjustedAttribute(ParseDecimalCell(GetCell(fightRow, 39)), attributes, "21");

        return new FightStatSnapshot(
            FormatNumberCell(GetCell(fightRow, 1)),
            FormatNumberCell(GetCell(fightRow, 25)),
            FormatNumberCell(GetCell(fightRow, 28)),
            FormatDecimalCell(physicalDefense),
            FormatDecimalCell(magicResistance),
            FormatDecimalCell(critResistance),
            FormatNumberCell(GetCell(fightRow, 49)));
    }

    private static decimal CalculateAdjustedAttribute(
        decimal baseValue,
        IReadOnlyDictionary<string, List<StateAttributeAdjustment>> attributes,
        string attributeId)
    {
        if (!attributes.TryGetValue(attributeId, out List<StateAttributeAdjustment>? values))
            return baseValue;

        decimal flat = 0m;
        decimal percent = 0m;
        foreach (StateAttributeAdjustment adjustment in values)
        {
            if (adjustment.Mode == 0)
                flat += adjustment.Value;
            else if (adjustment.Mode == 1)
                percent += adjustment.Value;
        }

        return (baseValue + flat) * (1m + percent / 10000m);
    }

    private string BuildLockBloodSummary(IReadOnlyList<string> chaRow)
    {
        string[] locks = GetStateRowsForMonster(chaRow)
            .Select(row => GetCell(row, 0))
            .Where(name => name.Contains("锁血", StringComparison.Ordinal))
            .Select(name =>
            {
                Match match = Regex.Match(name, @"锁血\s*(\d+)%");
                return match.Success ? $"{match.Groups[1].Value}%" : "锁血";
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return locks.Length == 0 ? string.Empty : $"锁血 {string.Join("、", locks)}";
    }

    private Dictionary<string, List<StateAttributeAdjustment>> GetDirectStateAttributes(IReadOnlyList<string> chaRow)
    {
        var attributes = new Dictionary<string, List<StateAttributeAdjustment>>(StringComparer.OrdinalIgnoreCase);
        foreach (StateDataReference state in GetEffectiveStateDataRowsForMonster(chaRow))
        {
            foreach (StateAttributeAdjustment adjustment in ParseStateAttributePack(
                         GetCell(state.Row, 8),
                         state.DataStateId,
                         GetCell(state.Row, 0)))
            {
                if (!attributes.TryGetValue(adjustment.AttributeId, out List<StateAttributeAdjustment>? values))
                {
                    values = new List<StateAttributeAdjustment>();
                    attributes[adjustment.AttributeId] = values;
                }

                values.Add(adjustment);
            }
        }

        return attributes;
    }

    private IEnumerable<string[]> GetStateRowsForMonster(IReadOnlyList<string> chaRow)
    {
        return GetEffectiveStateDataRowsForMonster(chaRow).Select(state => state.Row);
    }

    private IEnumerable<StateDataReference> GetEffectiveStateDataRowsForMonster(IReadOnlyList<string> chaRow)
    {
        Dictionary<string, string[]> stateRows = GetStateDataRows();
        var yieldedDataIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (EffectiveStateGroup stateGroup in GetEffectiveStateGroupsForMonster(chaRow))
        {
            bool yieldedFromGroup = false;
            IReadOnlyList<string> dataIds = stateGroup.GroupRow is not null
                ? SplitIdList(GetCell(stateGroup.GroupRow, 3)).ToArray()
                : new[] { stateGroup.DirectStateId };

            foreach (string rawDataId in dataIds)
            {
                string dataId = rawDataId.TrimStart('-');
                if (string.IsNullOrWhiteSpace(dataId) || dataId == "0") continue;
                if (!stateRows.TryGetValue(dataId, out string[]? row)) continue;
                if (!yieldedDataIds.Add(dataId)) continue;

                yieldedFromGroup = true;
                yield return new StateDataReference(stateGroup.DirectStateId, dataId, row);
            }

            if (!yieldedFromGroup ||
                yieldedDataIds.Contains(stateGroup.DirectStateId) ||
                !ShouldUseDirectStateDataFallback(stateGroup.DirectStateId, stateRows))
            {
                continue;
            }

            if (stateRows.TryGetValue(stateGroup.DirectStateId, out string[]? directRow) &&
                yieldedDataIds.Add(stateGroup.DirectStateId))
            {
                yield return new StateDataReference(stateGroup.DirectStateId, stateGroup.DirectStateId, directRow);
            }
        }
    }

    private IEnumerable<EffectiveStateGroup> GetEffectiveStateGroupsForMonster(IReadOnlyList<string> chaRow)
    {
        Dictionary<string, string[]> stateGroupRows = GetStateGroupRows();
        var bestByGroup = new Dictionary<string, EffectiveStateGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (string directStateId in SplitIdList(GetCell(chaRow, 13))
                     .Where(id => id != "0")
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            stateGroupRows.TryGetValue(directStateId, out string[]? groupRow);
            string groupKey = groupRow is not null && !string.IsNullOrWhiteSpace(GetCell(groupRow, 6))
                ? GetCell(groupRow, 6)
                : directStateId;
            int priority = groupRow is not null ? ParseIntOrDefault(GetCell(groupRow, 7)) : 0;
            var candidate = new EffectiveStateGroup(directStateId, groupKey, priority, groupRow);

            if (!bestByGroup.TryGetValue(groupKey, out EffectiveStateGroup? existing) ||
                candidate.Priority > existing.Priority)
            {
                bestByGroup[groupKey] = candidate;
            }
        }

        return bestByGroup.Values;
    }

    private static bool ShouldUseDirectStateDataFallback(
        string directStateId,
        IReadOnlyDictionary<string, string[]> stateRows)
    {
        if (directStateId == "2720") return false;
        return stateRows.TryGetValue(directStateId, out string[]? row) &&
               ParseStateAttributePack(GetCell(row, 8), directStateId, GetCell(row, 0)).Count > 0;
    }

    private static IReadOnlyList<StateAttributeAdjustment> ParseStateAttributePack(
        string value,
        string sourceId,
        string sourceName)
    {
        string[] parts = SplitIdList(value).ToArray();
        var result = new List<StateAttributeAdjustment>();
        for (int index = 0; index + 2 < parts.Length; index += 3)
        {
            if (!int.TryParse(parts[index], out int mode)) continue;
            string attributeId = parts[index + 1];
            if (string.IsNullOrWhiteSpace(attributeId)) continue;
            if (!decimal.TryParse(parts[index + 2], out decimal rawValue)) continue;
            result.Add(new StateAttributeAdjustment(sourceId, sourceName, attributeId, mode, rawValue));
        }

        return result;
    }

    private static string FormatDirectAttribute(
        IReadOnlyDictionary<string, List<StateAttributeAdjustment>> attributes,
        string attributeId)
    {
        return attributes.TryGetValue(attributeId, out List<StateAttributeAdjustment>? values) && values.Count > 0
            ? FormatAttributeAdjustments(values)
            : string.Empty;
    }

    private static string FormatAttributePack(string value)
    {
        IReadOnlyList<StateAttributeAdjustment> attributes = ParseStateAttributePack(value, string.Empty, string.Empty);
        if (attributes.Count == 0) return string.Empty;
        return string.Join("；", attributes
            .GroupBy(attribute => attribute.AttributeId, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{GetFightAttributeDisplayName(group.Key)}：{FormatAttributeAdjustments(group)}"));
    }

    private static string FormatAttributeAdjustments(IEnumerable<StateAttributeAdjustment> adjustments)
    {
        return string.Join("、", adjustments.Select(FormatAttributeAdjustment).Distinct());
    }

    private static string FormatAttributeAdjustment(StateAttributeAdjustment adjustment)
    {
        string raw = FormatSignedDecimal(adjustment.Value);
        return adjustment.Mode switch
        {
            1 => $"{FormatSignedDecimal(adjustment.Value / 100m)}%",
            0 => raw,
            _ => raw
        };
    }

    private static string FormatSignedDecimal(decimal value)
    {
        string sign = value > 0 ? "+" : string.Empty;
        return $"{sign}{FormatDecimalCell(value)}";
    }

    private static string GetFightAttributeDisplayName(string attributeId) =>
        attributeId switch
        {
            "4" => "生命值上限",
            "14" => "伤害",
            "15" => "防御",
            "17" => "物理会心几率",
            "18" => "法术会心几率",
            "19" => "物理会心伤害上限",
            "20" => "法术会心伤害上限",
            "21" => "抵抗会心几率",
            "22" => "法术效果",
            "28" => "移动速度",
            "29" => "法术抗性",
            "32" => "幸运一击伤害减免",
            "36" => "承受伤害",
            "38" => "最终伤害和治疗输出",
            "39" => "最终防御效果",
            "45" => "忽略物理防御",
            "46" => "忽略法术抗性",
            "68" => "会心伤害减免",
            "69" => "对怪物伤害",
            _ => $"属性 {attributeId}"
        };

    private static decimal ParseDecimalCell(string value)
    {
        return decimal.TryParse(value.TrimEnd('%'), out decimal number) ? number : 0m;
    }

    private static string FormatDecimalCell(decimal number) =>
        number == decimal.Truncate(number)
            ? $"{number:N0}"
            : $"{number:N2}";

    private IReadOnlyList<string> BuildHiddenStateSummaries(IReadOnlyList<string> chaRow)
    {
        string[] ids = new[] { GetCell(chaRow, 13) }
            .SelectMany(SplitIdList)
            .Where(id => id != "0")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var result = new List<string>();
        foreach (StateDataReference state in GetEffectiveStateDataRowsForMonster(chaRow))
        {
            string name = CleanStateText(GetCell(state.Row, 0));
            if (string.IsNullOrWhiteSpace(name))
                continue;

            string attributePack = FormatAttributePack(GetCell(state.Row, 8));
            if (!string.IsNullOrWhiteSpace(attributePack))
                result.Add($"{name}：{attributePack}");
        }

        AppendKnownDungeonMechanismSummaries(result, ids);
        return result;
    }

    private void AppendKnownDungeonMechanismSummaries(ICollection<string> result, IReadOnlyCollection<string> directStateIds)
    {
        if (directStateIds.Contains("59725", StringComparer.OrdinalIgnoreCase))
        {
            result.Add("蓝晶蛛活化：防御/法抗 -100%、-115%、-130%、-145%、-160%");
            result.Add("碎晶护盾：伤害吸收 2,000,000,000");
        }

        if (directStateIds.Contains("59681", StringComparer.OrdinalIgnoreCase))
        {
            result.Add("玄冥破煞符：削弱蛛王战斗力");
        }
    }

    private static bool IsUsefulStateDescription(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string[] keywords = { "锁血", "免疫", "无敌", "护盾", "吸收", "降低", "提高", "减免", "削弱", "伤害" };
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal));
    }

    private static string CleanStateText(string text) =>
        Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();

    private IReadOnlyList<DropLineViewModel> BuildMonsterDrops(
        IReadOnlyList<string> chaRow,
        IReadOnlyList<string[]> rewardRows)
    {
        Dictionary<string, string[]> itemRandRows = GetItemRandRows();
        Dictionary<string, string> itemNames = GetItemNameById();
        string[] groupIds = new[] { chaRow }.Concat(rewardRows)
            .SelectMany(row => SplitIdList(GetCell(row, 18)))
            .Where(id => id != "0")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var drops = new List<DropLineViewModel>();
        foreach (string groupId in groupIds)
        {
            if (!itemRandRows.TryGetValue(groupId, out string[]? groupRow)) continue;
            string groupName = GetCell(groupRow, 0);
            var itemParts = new List<string>();
            for (int column = 4; column + 1 < groupRow.Length; column += 3)
            {
                string itemId = GetCell(groupRow, column);
                if (string.IsNullOrWhiteSpace(itemId) || itemId == "0") continue;
                string chance = GetCell(groupRow, column + 1);
                itemNames.TryGetValue(itemId, out string? itemName);
                itemParts.Add($"{(string.IsNullOrWhiteSpace(itemName) ? itemId : itemName)} {FormatDropChance(chance)}");
                if (itemParts.Count >= 8) break;
            }

            string detail = itemParts.Count == 0
                ? $"掉落组 ID：{groupId}"
                : $"掉落组 ID：{groupId} · {string.Join("，", itemParts)}";
            drops.Add(new DropLineViewModel(groupName, detail));
        }

        return drops;
    }

    private void DungeonList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.OfType<DungeonSummaryViewModel>().LastOrDefault() is { } dungeon)
            ShowDungeonSummary(dungeon);
    }

    private void ShowDungeonSummary(DungeonSummaryViewModel dungeon)
    {
        _selectedDungeonSummary = dungeon;
        string[] terms = GetSearchTerms();
        List<DungeonMonsterViewModel> monsters = GetVisibleDungeonMonsters(dungeon, terms);
        DungeonMonsterListTitleText.Text = dungeon.Name;
        DungeonMonsterListCountText.Text = $"{monsters.Count:N0} 个";
        DungeonMonsterList.ItemsSource = monsters;
        DungeonMonsterList.SelectedItem = monsters.FirstOrDefault();
        ShowDungeonMonsterDetails(monsters.FirstOrDefault());
    }

    private void DungeonMonsterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShowDungeonMonsterDetails(e.AddedItems.OfType<DungeonMonsterViewModel>().LastOrDefault());
    }

    private void ShowDungeonMonsterDetails(DungeonMonsterViewModel? monster)
    {
        if (monster is null)
        {
            DungeonMonsterPortraitImage.Source = null;
            DungeonMonsterNameText.Text = "选择一个怪物";
            DungeonMonsterMetaText.Text = string.Empty;
            DungeonMonsterExplainText.Text = string.Empty;
            DungeonMonsterStatList.ItemsSource = null;
            DungeonMonsterHiddenText.Text = "暂无数据。";
            DungeonMonsterDropList.ItemsSource = null;
            return;
        }

        DungeonMonsterPortraitImage.Source = monster.Portrait;
        DungeonMonsterNameText.Text = monster.Name;
        string sourceIds = string.IsNullOrWhiteSpace(monster.SourceIds) ? monster.RoleId : monster.SourceIds;
        DungeonMonsterMetaText.Text = $"{monster.KindText} · 关联记录 {sourceIds} · 外观 ID {monster.PicId} · 战斗属性 ID {monster.FightId}";
        DungeonMonsterExplainText.Text = monster.Explanation;
        DungeonMonsterStatList.ItemsSource = monster.Stats;
        DungeonMonsterHiddenText.Text = monster.HiddenSummary;
        DungeonMonsterDropList.ItemsSource = monster.Drops;
    }

    private List<DungeonMonsterViewModel> GetVisibleDungeonMonsters(DungeonSummaryViewModel dungeon, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0 || terms.All(term => DungeonTextMatches(dungeon.Name + " " + dungeon.Subtitle, term)))
            return dungeon.Monsters.ToList();

        return dungeon.Monsters
            .Where(monster => terms.All(term =>
                DungeonTextMatches(monster.Name, term) ||
                DungeonTextMatches(monster.Subtitle, term) ||
                DungeonTextMatches(monster.CompactStats, term) ||
                DungeonTextMatches(monster.HiddenSummary, term)))
            .ToList();
    }

    private static bool DungeonMatchesSearch(DungeonSummaryViewModel dungeon, IReadOnlyList<string> terms) =>
        terms.All(term =>
            DungeonTextMatches(dungeon.Name, term) ||
            DungeonTextMatches(dungeon.Subtitle, term) ||
            dungeon.Monsters.Any(monster =>
                DungeonTextMatches(monster.Name, term) ||
                DungeonTextMatches(monster.Subtitle, term) ||
                DungeonTextMatches(monster.HiddenSummary, term)));

    private static bool DungeonTextMatches(string text, string term) =>
        GetSearchTermVariants(term).Any(variant => text.Contains(variant, StringComparison.OrdinalIgnoreCase));

    private async Task LoadDungeonPortraitsAsync(IReadOnlyList<DungeonMonsterViewModel> monsters)
    {
        foreach (DungeonMonsterViewModel monster in monsters)
        {
            if (string.IsNullOrWhiteSpace(monster.IconName)) continue;
            AssetEntry? image = FindPortraitAsset(monster.IconName);
            if (image is null) continue;
            try
            {
                byte[] data = await Task.Run(() => _workspace.Extract(image));
                monster.Portrait = await CreateBitmapAsync(data, 96);
                if (Equals(DungeonMonsterList.SelectedItem, monster))
                    DungeonMonsterPortraitImage.Source = monster.Portrait;
            }
            catch
            {
                // Keep the card readable even when a portrait image is missing or in an unsupported format.
            }
        }
    }

    private AssetEntry? FindPortraitAsset(string iconName)
    {
        string normalized = iconName.Replace('\\', '/');
        string fileName = System.IO.Path.GetFileName(normalized);
        return _workspace.Assets
            .Where(asset => asset.Kind == AssetKind.Image &&
                            (asset.Entry.Path.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                             asset.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                             asset.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(asset => asset.Entry.Path.Contains("portrait", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(asset => asset.Entry.Path.Contains("head", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(asset => asset.Entry.Path.Contains("icon", StringComparison.OrdinalIgnoreCase))
            .ThenBy(asset => asset.Entry.Path, NaturalStringComparer.Instance)
            .FirstOrDefault();
    }

    private Dictionary<string, string[]> GetChaListRows() =>
        _chaListRows ??= LoadMbTableByColumn("object/cha_list.txt", 1);

    private Dictionary<string, string[]> GetItemRandRows() =>
        _itemRandRows ??= LoadMbTableByColumn("item/item_rand.txt", 1);

    private Dictionary<string, List<string[]>> GetChaSkillRowsByPicId()
    {
        if (_chaSkillRowsByPicId is not null) return _chaSkillRowsByPicId;
        var result = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
        foreach (string[] row in LoadMbRows("object/cha_skill_choose.txt"))
        {
            string picId = GetCell(row, 1);
            if (string.IsNullOrWhiteSpace(picId)) continue;
            if (!result.TryGetValue(picId, out List<string[]>? rows))
            {
                rows = new List<string[]>();
                result[picId] = rows;
            }

            rows.Add(row);
        }

        _chaSkillRowsByPicId = result;
        return _chaSkillRowsByPicId;
    }

    private Dictionary<string, string> GetItemNameById()
    {
        if (_itemNameById is not null) return _itemNameById;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string tablePath in new[] { "item/item_list.txt", "item/item_list2.txt", "item/item_list3.txt" })
        {
            foreach (string[] row in LoadMbRows(tablePath))
            {
                string id = GetCell(row, 0);
                string name = GetCell(row, 1);
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                    result[id] = name;
            }
        }

        foreach (string[] row in LoadMbRows("item/raw_list.txt"))
        {
            string name = GetCell(row, 0);
            string id = GetCell(row, 1);
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                result.TryAdd(id, name);
        }

        _itemNameById = result;
        return _itemNameById;
    }

    private IReadOnlyList<string[]> LoadMbRows(string path)
    {
        AssetEntry? table = _workspace.Assets.FirstOrDefault(asset =>
            asset.Kind == AssetKind.MbTable &&
            asset.Entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (table is null) return Array.Empty<string[]>();

        try
        {
            byte[] data = _workspace.Extract(table);
            if (!TryDecodeTextPreview(table, data, out string text))
                return Array.Empty<string[]>();
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Split('\t'))
                .ToArray();
        }
        catch
        {
            return Array.Empty<string[]>();
        }
    }

    private static bool HasDropGroups(IReadOnlyList<string> row) =>
        SplitIdList(GetCell(row, 18)).Any(id => id != "0");

    private static string NormalizePortraitIconName(string value)
    {
        string normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;
        string fileName = System.IO.Path.GetFileName(normalized.Replace('\\', '/'));
        if (fileName.Equals("unknown.png", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("unknown.dds", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return normalized;
    }

    private static bool IsBossMonster(string name, string roleId, IReadOnlySet<string> bossRoleIds)
    {
        if (bossRoleIds.Contains(roleId)) return true;
        if (name.Contains("将军", StringComparison.Ordinal) ||
            name.Contains("蛇王", StringComparison.Ordinal) ||
            name.Contains("蛛王", StringComparison.Ordinal) ||
            name.Contains("纤丝", StringComparison.Ordinal) ||
            name.Contains("长老", StringComparison.Ordinal) ||
            name.Contains("笑千山", StringComparison.Ordinal) ||
            name.Contains("首领", StringComparison.Ordinal) ||
            name.Contains("BOSS", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string CleanMonsterName(string name)
    {
        string result = Regex.Replace(name, @"^\((NPC|BOSS|怪|静|骑)[^)]*\)", string.Empty, RegexOptions.IgnoreCase).Trim();
        result = result.Replace("（简单难度）", string.Empty).Replace("（正常难度）", string.Empty).Trim();
        return string.IsNullOrWhiteSpace(result) ? name : result;
    }

    private static string FormatDropChance(string value)
    {
        if (!decimal.TryParse(value, out decimal raw)) return string.Empty;
        if (raw <= 0) return string.Empty;
        decimal percent = raw / 10000m;
        return percent >= 100 ? "必掉" : $"约 {percent:0.##}%";
    }

    private sealed record DungeonDefinition(
        string Name,
        string Subtitle,
        IReadOnlyList<string> Aliases,
        IReadOnlyList<string> KnownMonsterIds,
        IReadOnlyList<string> BossMonsterIds,
        IReadOnlyList<IntRange> RoleIdRanges,
        IReadOnlyDictionary<string, string>? RoleLabels = null,
        IReadOnlySet<string>? ExcludedRoleIds = null,
        IReadOnlySet<string>? MechanismRoleIds = null);

    private sealed record IntRange(int Start, int End);

    private sealed record FightStatSnapshot(
        string Level,
        string Health,
        string Damage,
        string PhysicalDefense,
        string MagicResistance,
        string CritResistance,
        string FightValue);

    private sealed record EffectiveStateGroup(
        string DirectStateId,
        string GroupKey,
        int Priority,
        string[]? GroupRow);

    private sealed record StateDataReference(
        string DirectStateId,
        string DataStateId,
        string[] Row);

    private sealed record StateAttributeAdjustment(
        string SourceId,
        string SourceName,
        string AttributeId,
        int Mode,
        decimal Value);

    private void SetBusy(bool busy, string? status = null)
    {
        _isBusy = busy;
        BusyRing.IsActive = busy;
        if (status is not null) SetStatus(status);
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "知道了"
        };
        await dialog.ShowAsync();
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
            builder.Append(invalid.Contains(character) ? '_' : character);
        string result = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? "xunxian_model" : result;
    }

    private static string MakeUniqueExportFileName(string fileName, HashSet<string> usedFileNames)
    {
        string safeName = SanitizeFileName(fileName);
        string name = System.IO.Path.GetFileNameWithoutExtension(safeName);
        string extension = System.IO.Path.GetExtension(safeName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".dds";
        string candidate = name + extension;
        int suffix = 2;
        while (!usedFileNames.Add(candidate))
            candidate = $"{name}_{suffix++}{extension}";
        return candidate;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }

    private sealed class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Instance = new();

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;

            int leftIndex = 0;
            int rightIndex = 0;
            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                if (char.IsDigit(left[leftIndex]) && char.IsDigit(right[rightIndex]))
                {
                    int leftEnd = leftIndex;
                    int rightEnd = rightIndex;
                    while (leftEnd < left.Length && char.IsDigit(left[leftEnd])) leftEnd++;
                    while (rightEnd < right.Length && char.IsDigit(right[rightEnd])) rightEnd++;

                    int leftSignificant = leftIndex;
                    int rightSignificant = rightIndex;
                    while (leftSignificant < leftEnd - 1 && left[leftSignificant] == '0') leftSignificant++;
                    while (rightSignificant < rightEnd - 1 && right[rightSignificant] == '0') rightSignificant++;
                    int leftDigits = leftEnd - leftSignificant;
                    int rightDigits = rightEnd - rightSignificant;
                    if (leftDigits != rightDigits) return leftDigits.CompareTo(rightDigits);

                    int digitComparison = string.CompareOrdinal(
                        left, leftSignificant,
                        right, rightSignificant,
                        leftDigits);
                    if (digitComparison != 0) return digitComparison;

                    int runLengthComparison = (leftEnd - leftIndex).CompareTo(rightEnd - rightIndex);
                    if (runLengthComparison != 0) return runLengthComparison;
                    leftIndex = leftEnd;
                    rightIndex = rightEnd;
                    continue;
                }

                int characterComparison = char.ToUpperInvariant(left[leftIndex])
                    .CompareTo(char.ToUpperInvariant(right[rightIndex]));
                if (characterComparison != 0) return characterComparison;
                leftIndex++;
                rightIndex++;
            }
            return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
        }
    }
}
