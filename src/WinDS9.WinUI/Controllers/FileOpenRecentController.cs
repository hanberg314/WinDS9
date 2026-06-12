using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinDS9.Core;
using WinDS9.Engine;
using WinDS9.WinUI.Pages;
using WinRT.Interop;

namespace WinDS9.WinUI.Controllers;

internal sealed class FileOpenRecentController
{
    private readonly SettingsStore settingsStore;
    private readonly RecentFilesService recentFilesService;
    private readonly NativeImageLoader loader;
    private readonly FrameWorkspaceController frameWorkspace;
    private readonly Func<IntPtr> getWindowHandle;
    private readonly Action<bool> setBusy;
    private readonly Action<string> setStatus;
    private readonly Action<string> addLog;
    private readonly Action<ImageFrameViewModel> addFrameTab;
    private readonly Action<ImageFrameViewModel> selectFrameTab;
    private readonly Action clearRegionSelection;
    private readonly Action notifyFramesChanged;
    private readonly Action notifyCurrentFrameChanged;
    private readonly Action<LoadedImage> updateMetadata;
    private readonly Func<bool, Task> renderCurrentAsync;
    private readonly AppSettings settings;

    public FileOpenRecentController(
        NativeImageLoader loader,
        FrameWorkspaceController frameWorkspace,
        Func<IntPtr> getWindowHandle,
        Action<bool> setBusy,
        Action<string> setStatus,
        Action<string> addLog,
        Action<ImageFrameViewModel> addFrameTab,
        Action<ImageFrameViewModel> selectFrameTab,
        Action clearRegionSelection,
        Action notifyFramesChanged,
        Action notifyCurrentFrameChanged,
        Action<LoadedImage> updateMetadata,
        Func<bool, Task> renderCurrentAsync,
        SettingsStore? settingsStore = null,
        RecentFilesService? recentFilesService = null)
    {
        this.loader = loader;
        this.frameWorkspace = frameWorkspace;
        this.getWindowHandle = getWindowHandle;
        this.setBusy = setBusy;
        this.setStatus = setStatus;
        this.addLog = addLog;
        this.addFrameTab = addFrameTab;
        this.selectFrameTab = selectFrameTab;
        this.clearRegionSelection = clearRegionSelection;
        this.notifyFramesChanged = notifyFramesChanged;
        this.notifyCurrentFrameChanged = notifyCurrentFrameChanged;
        this.updateMetadata = updateMetadata;
        this.renderCurrentAsync = renderCurrentAsync;
        this.settingsStore = settingsStore ?? new SettingsStore();
        this.recentFilesService = recentFilesService ?? new RecentFilesService();
        settings = this.settingsStore.Load();
        RefreshRecentFiles();
        App.Log($"FileOpenRecentController initialized settings={this.settingsStore.SettingsPath} recent={settings.RecentFiles.Count}");
    }

    public ObservableCollection<RecentFileItem> RecentFiles { get; } = [];

    public bool IsLoading { get; private set; }

    public async Task PickAndOpenAsync()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        foreach (var extension in SupportedImageFiles.Extensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        InitializeWithWindow.Initialize(picker, getWindowHandle());

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            await OpenPathAsync(file.Path);
        }
    }

    public async Task OpenSampleAsync()
    {
        var path = FindSampleImage();
        if (path is not null)
        {
            await OpenPathAsync(path);
            return;
        }

        addLog("missing sample image");
        setStatus("Sample not found");
    }

    public void HandleDragOver(DragEventArgs e)
    {
        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems)
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
        e.Handled = true;
    }

    public async Task OpenDroppedAsync(DragEventArgs e)
    {
        e.Handled = true;
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        var path = items
            .OfType<StorageFile>()
            .Select(file => file.Path)
            .FirstOrDefault(SupportedImageFiles.IsSupported);

        if (path is null)
        {
            addLog("drop ignored: no supported FITS/event file");
            setStatus("Unsupported drop");
            return;
        }

        await OpenPathAsync(path);
    }

    public Task OpenRecentAsync(RecentFileItem item) => OpenPathAsync(item.Path);

    public async Task OpenPathAsync(string path)
    {
        App.Log($"FileOpenRecentController open requested path={path}");
        if (IsLoading)
        {
            App.Log("FileOpenRecentController open ignored: busy");
            return;
        }

        try
        {
            if (!File.Exists(path))
            {
                App.Log($"FileOpenRecentController open missing path={path}");
                addLog($"missing {path}");
                setStatus("File not found");
                return;
            }

            if (!SupportedImageFiles.IsSupported(path))
            {
                App.Log($"FileOpenRecentController open unsupported path={path}");
                addLog($"unsupported {path}");
                setStatus("Unsupported file type");
                return;
            }

            IsLoading = true;
            setBusy(true);
            setStatus($"Loading {Path.GetFileName(path)}");
            addLog($"open {path}");

            var loadedImages = await Task.Run(() => loader.LoadAll(path));
            App.Log($"FileOpenRecentController loaded source path={path} frames={loadedImages.Count}");
            var addedFrames = frameWorkspace.AddImages(loadedImages);
            foreach (var frame in addedFrames)
            {
                addFrameTab(frame);
            }

            var selectedFrame = frameWorkspace.CurrentFrame;
            if (selectedFrame is null)
            {
                addLog("open produced no displayable frames");
                setStatus("Open failed");
                return;
            }

            selectFrameTab(selectedFrame);
            clearRegionSelection();
            notifyFramesChanged();
            notifyCurrentFrameChanged();
            updateMetadata(selectedFrame.Image);
            await renderCurrentAsync(true);
            AddRecentFile(path);
            App.Log($"FileOpenRecentController open completed path={path}");
            addLog(loadedImages.Count == 1
                ? $"loaded {loadedImages[0].Width}x{loadedImages[0].Height} in {loadedImages[0].LoadDuration.TotalMilliseconds:0} ms"
                : $"loaded {loadedImages.Count} HDUs from {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            App.Log($"FileOpenRecentController open failed path={path} exception={ex}");
            addLog(ex.Message);
            setStatus("Open failed");
        }
        finally
        {
            setBusy(false);
            IsLoading = false;
        }
    }

    private void AddRecentFile(string path)
    {
        settings.RecentFiles = recentFilesService.Add(path, settings.RecentFiles).ToList();
        RefreshRecentFiles();

        try
        {
            settingsStore.Save(settings);
        }
        catch (Exception ex)
        {
            App.Log($"FileOpenRecentController recent save failed path={settingsStore.SettingsPath} exception={ex}");
            addLog($"recent save skipped: {ex.Message}");
        }

        App.Log($"FileOpenRecentController recent updated first={settings.RecentFiles.FirstOrDefault() ?? "<none>"}");
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var path in settings.RecentFiles)
        {
            RecentFiles.Add(new RecentFileItem(path));
        }
    }

    private static string? FindSampleImage()
    {
        var samplesRoot = Path.Combine(FindWorkspaceRoot(), "samples");
        if (!Directory.Exists(samplesRoot))
        {
            return null;
        }

        return Directory.EnumerateFiles(samplesRoot, "*.*", SearchOption.AllDirectories)
            .Where(SupportedImageFiles.IsSupported)
            .OrderBy(path => path.Contains("hst_fits", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(path => Path.GetExtension(path).Equals(".fits", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WinDS9.sln")) ||
                Directory.Exists(Path.Combine(current.FullName, "samples")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
