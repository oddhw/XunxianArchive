using System.Security.Cryptography;
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
    private int _sortMode;
    private int _thumbnailGeneration;
    private bool _isBusy;
    private bool _modelExpanded;
    private bool _buildingFolderTree;
    private bool _multiSelectMode;
    private bool _settingModelTextureSelection;
    private FolderNodeInfo? _selectedFolder;

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
        int images = _workspace.Assets.Count(asset => asset.Kind == AssetKind.Image);
        int sounds = _workspace.Assets.Count(asset => asset.Kind == AssetKind.Sound);
        int models = _workspace.Assets.Count(asset => asset.Kind == AssetKind.Model);
        int fonts = _workspace.Assets.Count(asset => asset.Kind == AssetKind.Font);
        int others = _workspace.Assets.Count(asset => asset.Kind == AssetKind.Other);
        ArchiveSummaryText.Text = $"{_workspace.ArchivePaths.Count} 个包 · {images:N0} 图像 · {sounds:N0} 声音 · {models:N0} 模型 · {fonts:N0} 字体 · {others:N0} 其他";
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
            AssetKind.Other => "gfx.dpk",
            _ => string.Empty
        };
        string preferredPath = _currentKind switch
        {
            AssetKind.Image => "icon",
            AssetKind.Model => "share/mesh",
            _ => string.Empty
        };
        foreach (IGrouping<string, AssetEntry> archive in _workspace.Assets
                     .Where(asset => asset.Kind == _currentKind)
                     .GroupBy(asset => asset.ArchivePath, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => string.Equals(System.IO.Path.GetFileName(group.Key), preferredArchive, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(group => System.IO.Path.GetFileName(group.Key), StringComparer.OrdinalIgnoreCase))
        {
            string archiveName = System.IO.Path.GetFileName(archive.Key);
            var root = new TreeViewNode
            {
                Content = new FolderNodeInfo(archiveName, archive.Key, string.Empty),
                IsExpanded = true
            };
            FolderTree.RootNodes.Add(root);
            firstRoot ??= root;

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
                if (!nodesByPath.TryGetValue(parentPath, out TreeViewNode? parent)) continue;

                var node = new TreeViewNode
                {
                    Content = new FolderNodeInfo(folderName, archive.Key, directory)
                };
                parent.Children.Add(node);
                nodesByPath[directory] = node;
            }

            if (string.Equals(archiveName, preferredArchive, StringComparison.OrdinalIgnoreCase) &&
                nodesByPath.TryGetValue(preferredPath, out TreeViewNode? categoryDefault))
            {
                preferredNode = categoryDefault;
                categoryDefault.IsExpanded = true;
                for (TreeViewNode? ancestor = categoryDefault.Parent; ancestor is not null; ancestor = ancestor.Parent)
                    ancestor.IsExpanded = true;
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
        string[] terms = SearchBox.Text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IEnumerable<AssetEntry> query = _workspace.Assets
            .Where(asset => asset.Kind == _currentKind)
            .Where(IsAssetInSelectedFolder)
            .Where(asset => terms.Length == 0 || terms.All(term =>
                asset.Entry.Path.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                asset.ArchiveName.Contains(term, StringComparison.OrdinalIgnoreCase)));
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
        items.AddRange(_filteredAssets.Select(asset => new AssetItemViewModel(asset)));
        _items = items;
        ImageGrid.ItemsSource = _items;
        AssetList.ItemsSource = _items;
        EmptyPanel.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        string scope = string.IsNullOrWhiteSpace(SearchBox.Text) ? "当前文件夹" : "当前目录及子目录";
        string compositeStatus = compositeCount > 0 ? $"，其中 {compositeCount:N0} 个完整组合模型" : string.Empty;
        SetStatus($"{scope}找到 {_items.Count:N0} 个资源{compositeStatus}，已全部显示");
        UpdateSelectionUi(0);
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
            _selectedAsset = null;
            ResetPreviewSelection();
            return;
        }

        if (item.Composite is CompositeModelEntry composite)
        {
            _selectedAsset = null;
            _selectedComposite = composite;
            SelectedNameText.Text = composite.Name;
            SelectedPathText.Text = composite.DisplayPath;
            SelectedMetadataText.Text = $"正在组合 {composite.Parts.Count:N0} 个模型部件及贴图…";
            ModelTextureSelector.Visibility = Visibility.Collapsed;
            await PreviewCompositeModelAsync(composite);
            return;
        }
        if (item.Asset is not AssetEntry asset) return;

        _selectedComposite = null;
        _selectedAsset = asset;
        SelectedNameText.Text = selectedCount > 1 ? $"{asset.Name}（已选择 {selectedCount:N0} 项）" : asset.Name;
        SelectedPathText.Text = asset.DisplayPath;
        SelectedMetadataText.Text = "正在读取…";

        try
        {
            byte[] data = await Task.Run(() => _workspace.Extract(asset));
            if (_selectedAsset != asset) return;

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
                if (_selectedAsset != asset) return;
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
                GenericPreviewNameText.Text = asset.Name;
                GenericPreviewIcon.Glyph = asset.Kind == AssetKind.Font ? "\uE8D2" : "\uE8A5";
                GenericPreviewHintText.Text = asset.Kind == AssetKind.Font
                    ? "TrueType/OpenType 字体资源，可导出后安装或检查字形"
                    : "配置、场景、特效或影片资源，可查看属性并导出原始文件";
                SelectedMetadataText.Text = $"{asset.Extension.TrimStart('.').ToUpperInvariant()} · {FormatBytes(data.Length)}";
            }
        }
        catch (Exception ex)
        {
            SelectedMetadataText.Text = $"预览失败：{ex.Message}";
        }
    }

    private async Task PreviewCompositeModelAsync(CompositeModelEntry composite)
    {
        try
        {
            ModelRenderPart[] renderParts = await Task.Run(() => composite.Parts.Select(part =>
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
                return new ModelRenderPart(part.MeshAsset.Name, mesh, texture, textureName);
            }).ToArray());
            if (_selectedComposite != composite) return;
            _modelPreview.SetComposite(renderParts, composite.Name);
            int vertices = renderParts.Sum(part => part.Mesh.Vertices.Count);
            long triangles = renderParts.Sum(part => (long)part.Mesh.DeclaredTriangleCount);
            int texturedParts = renderParts.Count(part => part.Texture is not null);
            SelectedMetadataText.Text = $"完整组合 · {renderParts.Length:N0} 个 PMF 部件 · {vertices:N0} 顶点 · {triangles:N0} 三角面 · {texturedParts:N0} 个部件已加载贴图";
        }
        catch (Exception ex)
        {
            if (_selectedComposite != composite) return;
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
        if (_selectedAsset?.Kind != AssetKind.Model || _isBusy) return;
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = System.IO.Path.GetFileNameWithoutExtension(_selectedAsset.Name)
        };
        picker.FileTypeChoices.Add("Wavefront OBJ 模型", new List<string> { ".obj" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null) return;

        SetBusy(true, "正在转换并导出 OBJ 模型…");
        try
        {
            AssetEntry asset = _selectedAsset;
            await Task.Run(() =>
            {
                PmfMesh mesh = PmfParser.Parse(_workspace.Extract(asset));
                ObjExporter.Export(mesh, file.Path, System.IO.Path.GetFileNameWithoutExtension(asset.Name));
            });
            SetStatus($"OBJ 模型已导出：{file.Path}");
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
            "other" => AssetKind.Other,
            _ => AssetKind.Image
        };
        SearchBox.Text = string.Empty;
        ConfigureCategoryUi();
        BuildFolderTree();
        ApplyFilter();
    }

    private void ConfigureCategoryUi()
    {
        bool images = _currentKind == AssetKind.Image;
        bool sounds = _currentKind == AssetKind.Sound;
        bool models = _currentKind == AssetKind.Model;
        bool fonts = _currentKind == AssetKind.Font;
        bool others = _currentKind == AssetKind.Other;
        PageTitleText.Text = _currentKind switch
        {
            AssetKind.Image => "图标与贴图",
            AssetKind.Sound => "声音",
            AssetKind.Model => "模型",
            AssetKind.Font => "字体",
            _ => "配置与其他"
        };
        PageDescriptionText.Text = _currentKind switch
        {
            AssetKind.Image => "浏览 GUI 图标、装备图和 DDS 场景贴图",
            AssetKind.Sound => "直接试听 OGG 音乐、环境音与 WAV 音效",
            AssetKind.Model => "大尺寸实体预览 PMF 模型，并可转换导出为 OBJ",
            AssetKind.Font => "浏览 font.dpk 内的 TTF、OTF 和 TTC 字体资源",
            _ => "浏览特效、场景、地形、天空、影片及各类配置文件"
        };
        SearchBox.PlaceholderText = _currentKind switch
        {
            AssetKind.Image => "搜索图标名称或路径",
            AssetKind.Sound => "搜索音效、音乐或路径",
            AssetKind.Model => "搜索模型名称或路径",
            AssetKind.Font => "搜索字体名称或路径",
            _ => "搜索配置、特效、场景或路径"
        };
        ImageGrid.Visibility = images ? Visibility.Visible : Visibility.Collapsed;
        AssetList.Visibility = images ? Visibility.Collapsed : Visibility.Visible;
        ImagePreviewPanel.Visibility = images ? Visibility.Visible : Visibility.Collapsed;
        SoundPreviewPanel.Visibility = sounds ? Visibility.Visible : Visibility.Collapsed;
        ModelPreviewHost.Visibility = models ? Visibility.Visible : Visibility.Collapsed;
        GenericPreviewPanel.Visibility = fonts || others ? Visibility.Visible : Visibility.Collapsed;
        ModelTextureSelector.Visibility = Visibility.Collapsed;
        ModelTextureComboBox.ItemsSource = null;
        ExportModelButton.Visibility = models ? Visibility.Visible : Visibility.Collapsed;
        ExpandModelButton.Visibility = models ? Visibility.Visible : Visibility.Collapsed;
        SetMultiSelectMode(false);
        _modelExpanded = false;
        AssetBrowserPanel.Visibility = Visibility.Visible;
        AssetListColumn.MinWidth = models ? 600 : 650;
        AssetListColumn.Width = models ? new GridLength(650) : new GridLength(1.35, GridUnitType.Star);
        PreviewColumn.Width = models ? new GridLength(1, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
        ExpandModelButtonText.Text = "放大模型预览";
        if (!sounds) _mediaPlayer.Pause();
        if (_currentKind != AssetKind.Model) _modelPreview.SetMesh(null);
        PreviewImage.Source = null;
        ImageGrid.SelectedItem = null;
        AssetList.SelectedItem = null;
        _selectedAsset = null;
        _selectedComposite = null;
        ExportSelectedButton.IsEnabled = false;
        ExportSelectedButtonText.Text = "导出原始资源";
        PropertiesButton.IsEnabled = false;
        ExportModelButton.IsEnabled = false;
        SelectedNameText.Text = "尚未选择资源";
        SelectedPathText.Text = _currentKind switch
        {
            AssetKind.Image => "从左侧选择一张图像",
            AssetKind.Sound => "从左侧选择一个声音",
            AssetKind.Model => "从左侧选择一个 PMF 模型",
            AssetKind.Font => "从左侧选择一个字体文件",
            _ => "从左侧选择一个资源文件"
        };
        SelectedMetadataText.Text = string.Empty;
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
        if (ImageGrid.ItemsSource is null || AssetList.ItemsSource is null) return;
        if (!enabled)
        {
            ImageGrid.SelectedItem = null;
            AssetList.SelectedItem = null;
        }

        ListViewSelectionMode selectionMode = enabled ? ListViewSelectionMode.Multiple : ListViewSelectionMode.Single;
        ImageGrid.SelectionMode = selectionMode;
        AssetList.SelectionMode = selectionMode;
        ImageGrid.IsMultiSelectCheckBoxEnabled = enabled;
        AssetList.IsMultiSelectCheckBoxEnabled = enabled;
        MultiSelectButton.Content = enabled ? "完成" : "多选";
        SelectAllButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        ClearSelectionButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        UpdateSelectionUi(GetActiveAssetList().SelectedItems.Count);
        if (!enabled) ResetPreviewSelection();
    }

    private ListViewBase GetActiveAssetList() => _currentKind == AssetKind.Image ? ImageGrid : AssetList;

    private AssetEntry[] GetSelectedAssets() => GetActiveAssetList().SelectedItems
        .OfType<AssetItemViewModel>()
        .Select(item => item.Asset)
        .OfType<AssetEntry>()
        .Distinct()
        .ToArray();

    private void UpdateSelectionUi(int selectedCount)
    {
        AssetEntry[] selected = GetSelectedAssets();
        ExportSelectedButton.IsEnabled = selected.Length > 0;
        ExportSelectedButtonText.Text = _multiSelectMode
            ? $"导出所选 ({selected.Length:N0})"
            : "导出原始资源";
        PropertiesButton.IsEnabled = selected.Length == 1;
        ExportModelButton.IsEnabled = selected.Length == 1 && selected[0].Kind == AssetKind.Model;
    }

    private void ResetPreviewSelection()
    {
        ExportModelButton.IsEnabled = false;
        PropertiesButton.IsEnabled = false;
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
        _selectedComposite = null;
        if (_currentKind == AssetKind.Model) _modelPreview.SetMesh(null);
    }

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
