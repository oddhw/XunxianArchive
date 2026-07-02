using System.Collections.ObjectModel;
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
    private const int PageSize = 200;
    private readonly DpkWorkspace _workspace = new();
    private readonly ObservableCollection<AssetItemViewModel> _items = new();
    private readonly DispatcherTimer _searchTimer;
    private readonly MediaPlayer _mediaPlayer = new();
    private readonly MediaPlayerElement _audioPlayer;
    private readonly ModelPreviewControl _modelPreview;
    private List<AssetEntry> _filteredAssets = new();
    private AssetKind _currentKind = AssetKind.Image;
    private AssetEntry? _selectedAsset;
    private int _currentPage;
    private int _thumbnailGeneration;
    private bool _isBusy;
    private bool _modelExpanded;

    public MainWindow()
    {
        InitializeComponent();
        _audioPlayer = new MediaPlayerElement { AreTransportControlsEnabled = true };
        AudioPlayerHost.Content = _audioPlayer;
        _modelPreview = new ModelPreviewControl();
        ModelPreviewHost.Content = _modelPreview;
        ExtendsContentIntoTitleBar = false;
        AppWindow.Resize(new SizeInt32(1600, 960));

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
        ArchiveSummaryText.Text = $"{_workspace.ArchivePaths.Count} 个包 · {images:N0} 图像 · {sounds:N0} 声音 · {models:N0} 模型";
        BatchExportButton.IsEnabled = true;
        _currentPage = 0;
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

    private void ApplyFilter()
    {
        string[] terms = SearchBox.Text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _filteredAssets = _workspace.Assets
            .Where(asset => asset.Kind == _currentKind)
            .Where(asset => terms.Length == 0 || terms.All(term =>
                asset.Entry.Path.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                asset.ArchiveName.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        _currentPage = Math.Clamp(_currentPage, 0, Math.Max(0, PageCount - 1));
        PopulatePage();
    }

    private int PageCount => _filteredAssets.Count == 0 ? 0 : (_filteredAssets.Count + PageSize - 1) / PageSize;

    private void PopulatePage()
    {
        int generation = ++_thumbnailGeneration;
        _items.Clear();
        foreach (AssetEntry asset in _filteredAssets.Skip(_currentPage * PageSize).Take(PageSize))
            _items.Add(new AssetItemViewModel(asset));

        int pageCount = PageCount;
        PageIndicatorText.Text = pageCount == 0 ? "0 / 0" : $"{_currentPage + 1} / {pageCount}";
        PreviousPageButton.IsEnabled = _currentPage > 0;
        NextPageButton.IsEnabled = _currentPage + 1 < pageCount;
        EmptyPanel.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SetStatus($"找到 {_filteredAssets.Count:N0} 个资源，每页最多 {PageSize} 个");

        if (_currentKind == AssetKind.Image)
            _ = LoadThumbnailsAsync(_items.ToArray(), generation);
    }

    private async Task LoadThumbnailsAsync(IReadOnlyList<AssetItemViewModel> items, int generation)
    {
        foreach (AssetItemViewModel item in items)
        {
            if (generation != _thumbnailGeneration) return;
            try
            {
                byte[] data = await Task.Run(() => _workspace.Extract(item.Asset));
                if (generation != _thumbnailGeneration) return;
                item.Thumbnail = await CreateBitmapAsync(data, 128);
            }
            catch
            {
                item.Subtitle = "无法生成缩略图";
            }
        }
    }

    private async void Asset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListViewBase list || list.SelectedItem is not AssetItemViewModel item) return;
        _selectedAsset = item.Asset;
        ExportSelectedButton.IsEnabled = true;
        ExportModelButton.IsEnabled = item.Asset.Kind == AssetKind.Model;
        SelectedNameText.Text = item.Asset.Name;
        SelectedPathText.Text = item.Asset.DisplayPath;
        SelectedMetadataText.Text = "正在读取…";

        try
        {
            byte[] data = await Task.Run(() => _workspace.Extract(item.Asset));
            if (_selectedAsset != item.Asset) return;

            if (item.Asset.Kind == AssetKind.Image)
            {
                PreviewImage.Source = await CreateBitmapAsync(data, 0);
                SelectedMetadataText.Text = $"{item.Asset.Extension.TrimStart('.').ToUpperInvariant()} · {FormatBytes(data.Length)}";
            }
            else if (item.Asset.Kind == AssetKind.Sound)
            {
                await PlaySoundAsync(item.Asset, data);
                SelectedMetadataText.Text = $"{item.Asset.Extension.TrimStart('.').ToUpperInvariant()} · {FormatBytes(data.Length)}";
            }
            else if (item.Asset.Kind == AssetKind.Model)
            {
                PmfMesh mesh = await Task.Run(() => PmfParser.Parse(data));
                if (_selectedAsset != item.Asset) return;
                _modelPreview.SetMesh(mesh);
                SelectedMetadataText.Text = $"PMF v{mesh.Version} · {mesh.Vertices.Count:N0} 顶点 · {mesh.DeclaredTriangleCount:N0} 三角面 · {mesh.UvChannelCount} UV 通道 · {FormatBytes(data.Length)}";
            }
        }
        catch (Exception ex)
        {
            SelectedMetadataText.Text = $"预览失败：{ex.Message}";
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
        if (_selectedAsset is null || _isBusy) return;
        StorageFolder? folder = await PickOutputFolderAsync();
        if (folder is null) return;
        SetBusy(true, "正在导出所选资源…");
        try
        {
            await Task.Run(() => _workspace.ExtractTo(_selectedAsset, folder.Path));
            SetStatus($"已导出：{_selectedAsset.Name}");
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
        AssetListColumn.MinWidth = _modelExpanded ? 0 : 340;
        AssetListColumn.Width = _modelExpanded ? new GridLength(0) : new GridLength(380);
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
        if (args.SelectedItemContainer?.Tag is not string tag) return;
        _currentKind = tag switch
        {
            "sound" => AssetKind.Sound,
            "model" => AssetKind.Model,
            _ => AssetKind.Image
        };
        _currentPage = 0;
        SearchBox.Text = string.Empty;
        ConfigureCategoryUi();
        ApplyFilter();
    }

    private void ConfigureCategoryUi()
    {
        bool images = _currentKind == AssetKind.Image;
        bool sounds = _currentKind == AssetKind.Sound;
        bool models = _currentKind == AssetKind.Model;
        PageTitleText.Text = images ? "图标与贴图" : sounds ? "声音" : "模型";
        PageDescriptionText.Text = images
            ? "浏览 GUI 图标、装备图和 DDS 场景贴图"
            : sounds
                ? "直接试听 OGG 音乐、环境音与 WAV 音效"
                : "大尺寸实体预览 PMF 模型，并可转换导出为 OBJ";
        SearchBox.PlaceholderText = images ? "搜索图标名称或路径" : sounds ? "搜索音效、音乐或路径" : "搜索模型名称或路径";
        ImageGrid.Visibility = images ? Visibility.Visible : Visibility.Collapsed;
        AssetList.Visibility = images ? Visibility.Collapsed : Visibility.Visible;
        ImagePreviewPanel.Visibility = images ? Visibility.Visible : Visibility.Collapsed;
        SoundPreviewPanel.Visibility = sounds ? Visibility.Visible : Visibility.Collapsed;
        ModelPreviewHost.Visibility = models ? Visibility.Visible : Visibility.Collapsed;
        ExportModelButton.Visibility = models ? Visibility.Visible : Visibility.Collapsed;
        ExpandModelButton.Visibility = models ? Visibility.Visible : Visibility.Collapsed;
        _modelExpanded = false;
        AssetBrowserPanel.Visibility = Visibility.Visible;
        AssetListColumn.MinWidth = models ? 340 : 440;
        AssetListColumn.Width = models ? new GridLength(380) : new GridLength(1.35, GridUnitType.Star);
        PreviewColumn.Width = models ? new GridLength(1, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
        ExpandModelButtonText.Text = "放大模型预览";
        if (!sounds) _mediaPlayer.Pause();
        if (_currentKind != AssetKind.Model) _modelPreview.SetMesh(null);
        PreviewImage.Source = null;
        _selectedAsset = null;
        ExportSelectedButton.IsEnabled = false;
        ExportModelButton.IsEnabled = false;
        SelectedNameText.Text = "尚未选择资源";
        SelectedPathText.Text = images ? "从左侧选择一张图像" : sounds ? "从左侧选择一个声音" : "从左侧选择一个 PMF 模型";
        SelectedMetadataText.Text = string.Empty;
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void PreviousPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage <= 0) return;
        _currentPage--;
        PopulatePage();
    }

    private void NextPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage + 1 >= PageCount) return;
        _currentPage++;
        PopulatePage();
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
}
