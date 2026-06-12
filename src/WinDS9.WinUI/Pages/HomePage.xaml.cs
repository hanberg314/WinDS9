using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using WinDS9.Engine;
using WinDS9.WinUI.Controllers;
using WinDS9.WinUI.Rendering;
using WinRT.Interop;

namespace WinDS9.WinUI.Pages;

public sealed partial class HomePage : Page
{
    private readonly NativeImageLoader loader = new();
    private readonly CoordinateTransformService coordinateTransforms = new();
    private readonly WcsGridGenerator wcsGridGenerator = new();
    private readonly RegionEditService regionEditService = new();
    private readonly FrameWorkspaceController frameWorkspace = new();
    private bool syncingFrameTabSelection;
    private IReadOnlyList<WcsGridSegment> wcsGridSegments = [];
    private bool isWcsGridVisible;
    private OverlayRenderer overlayRenderer = null!;
    private ViewportRenderController viewportRenderController = null!;
    private InspectorController inspectorController = null!;
    private CommandExecutionController commandExecutionController = null!;
    private ScaleColorController scaleColorController = null!;
    private CubePlaneController cubePlaneController = null!;
    private RegionInteractionController regionInteractionController = null!;
    private RegionCanvasInteractionController regionCanvasInteractionController = null!;
    private FrameBlinkController frameBlinkController = null!;
    private FrameCommandController frameCommandController = null!;
    private MetadataReadoutController metadataReadoutController = null!;
    private AnalysisContourController analysisContourController = null!;
    private FileOpenRecentController fileOpenRecentController = null!;
    private RegionCatalogController regionCatalogController = null!;
    private HeaderExportController headerExportController = null!;
    private bool isResizingInspector;
    private bool isInspectorSizerPointerOver;
    private double inspectorResizeStartX;
    private double inspectorResizeStartWidth;
    private readonly DispatcherQueueTimer cutRenderTimer;

    private ObservableCollection<ImageFrameViewModel> frames => frameWorkspace.Frames;

    private ImageFrameViewModel? currentFrame => frameWorkspace.CurrentFrame;

    internal ObservableCollection<ImageFrameViewModel> FrameItems => frames;

    internal ImageFrameViewModel? CurrentFrame => currentFrame;

    internal event EventHandler? FramesChanged;

    internal event EventHandler? CurrentFrameChanged;

    public HomePage()
    {
        InitializeComponent();
        overlayRenderer = new OverlayRenderer(RegionCanvas, coordinateTransforms, ThemeBrush);
        commandExecutionController = new CommandExecutionController(
            new CommandDispatcher(),
            OpenPathAsync,
            ApplyScaleCommand,
            ApplyColorMapCommand,
            ApplyZoomCommand,
            ApplyFrameCommandAsync,
            ApplyRegionCommand,
            LoadCatalog,
            ExecuteContourCommand,
            SetStatus,
            AddLog);
        inspectorController = new InspectorController(
        [
            new InspectorController.Section(CurrentSection, CurrentSectionButton, FileText),
            new InspectorController.Section(WcsSection, WcsSectionButton, WcsText),
            new InspectorController.Section(ScaleSection, ScaleSectionButton, ScaleSectionContent),
            new InspectorController.Section(ColorSection, ColorSectionButton, ColorMapCombo),
            new InspectorController.Section(RegionSection, RegionSectionButton, RegionSectionContent),
            new InspectorController.Section(FrameInfoSection, FrameInfoSectionButton, FrameInfoSectionContent),
            new InspectorController.Section(CubeSection, CubeSectionButton, CubeSectionContent),
            new InspectorController.Section(AnalysisSection, AnalysisSectionButton, AnalysisText)
        ],
            InspectorSizer,
            ThemeBrush);

        FrameTabs.TabCloseRequested += FrameTabs_TabCloseRequested;
        ApplyAcrylicSurfaces();
        scaleColorController = new ScaleColorController(
            StretchCombo,
            CutUnitCombo,
            ColorMapCombo,
            LowCutSlider,
            HighCutSlider,
            LowCutValueText,
            HighCutValueText,
            CutText,
            () => currentFrame);
        scaleColorController.Initialize();
        cubePlaneController = new CubePlaneController(
            CubeText,
            PlaneSlider,
            PlanePreviousButton,
            PlaneNextButton,
            PlaneText,
            () => currentFrame);
        cubePlaneController.Reset();
        regionInteractionController = new RegionInteractionController(
            RegionToolCombo,
            RegionStatusText,
            SelectedRegionText,
            RegionLabelText,
            RegionValuesText,
            regionEditService,
            () => currentFrame,
            DrawRegions,
            SetStatus,
            AddLog);
        regionInteractionController.Initialize();
        regionCatalogController = new RegionCatalogController(
            coordinateTransforms,
            regionInteractionController,
            () => currentFrame,
            () => WindowNative.GetWindowHandle(App.MainWindow),
            DrawRegions,
            SetStatus,
            AddLog);
        regionCanvasInteractionController = new RegionCanvasInteractionController(
            regionInteractionController,
            regionEditService,
            coordinateTransforms,
            () => currentFrame,
            () => viewportRenderController.Zoom,
            DrawRegions,
            AddLog);
        cutRenderTimer = DispatcherQueue.CreateTimer();
        cutRenderTimer.Interval = TimeSpan.FromMilliseconds(120);
        cutRenderTimer.Tick += CutRenderTimer_Tick;
        frameBlinkController = new FrameBlinkController(
            DispatcherQueue,
            frameWorkspace,
            BlinkButton,
            SelectFrameAsync,
            SetStatus);
        frameCommandController = new FrameCommandController(
            frameWorkspace,
            SelectFrameAsync);
        metadataReadoutController = new MetadataReadoutController(
            FileText,
            FrameText,
            HduText,
            TypeText,
            RasterText,
            RowsText,
            RangeText,
            CutText,
            LoadText,
            WcsText,
            AnalysisText,
            ZoomText,
            ValueText,
            ImageCoordText,
            PhysicalCoordText,
            DisplayCoordText,
            WcsCoordText,
            () => currentFrame);
        viewportRenderController = new ViewportRenderController(
            ImageScroll,
            ViewportHost,
            ImageHost,
            RasterImage,
            RegionCanvas,
            ImageScale,
            DispatcherQueue,
            () => currentFrame,
            scaleColorController.CurrentStretch,
            scaleColorController.CurrentColorMap,
            metadataReadoutController.UpdateCut,
            metadataReadoutController.UpdateZoom,
            RefreshWcsGridForRenderedImage,
            DrawRegions,
            SetStatus);
        analysisContourController = new AnalysisContourController(
            () => currentFrame,
            DrawRegions,
            metadataReadoutController.UpdateAnalysisSummary,
            SetStatus,
            AddLog);
        headerExportController = new HeaderExportController(
            () => currentFrame,
            () => viewportRenderController.LastRenderedRaster,
            () => WindowNative.GetWindowHandle(App.MainWindow),
            () => XamlRoot,
            SetStatus,
            AddLog);
        fileOpenRecentController = new FileOpenRecentController(
            loader,
            frameWorkspace,
            () => WindowNative.GetWindowHandle(App.MainWindow),
            SetOpenBusy,
            SetStatus,
            AddLog,
            AddFrameTab,
            SelectFrameTab,
            () => regionInteractionController.ClearSelection(),
            () => FramesChanged?.Invoke(this, EventArgs.Empty),
            () => CurrentFrameChanged?.Invoke(this, EventArgs.Empty),
            UpdateMetadata,
            RenderCurrentAsync);
        RecentList.ItemsSource = fileOpenRecentController.RecentFiles;
        SetStatus("Ready");
        metadataReadoutController.ResetFrameState();
        regionInteractionController.UpdateSelectedRegionControls();
        inspectorController.ApplyState();
    }

    private void ApplyAcrylicSurfaces()
    {
        RootSurface.Background = ThemeBrush("GlassSurfaceBrush");
        ContentSurface.Background = ThemeBrush("GlassSurfaceBrush");
        MainCommandBar.Background = ThemeBrush("GlassBarBrush");
        RecentFlyoutSurface.Background = ThemeBrush("GlassSurfaceBrush");
        InspectorPanel.Background = ThemeBrush("GlassSurfaceBrush");
        ReadoutSection.Background = ThemeBrush("ReadoutSectionBrush");
        UpdateInspectorSizerVisual();
        ImageScroll.Background = ThemeBrush("ImageViewportBrush");
        LogPanel.Background = ThemeBrush("GlassBarBrush");
        StatusBar.Background = ThemeBrush("GlassBarBrush");
        inspectorController.ApplyState();
    }

    private Brush ThemeBrush(string key) => (Brush)Resources[key];

    private void InspectorSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        inspectorController.Toggle(button);
    }

    private void InspectorSizer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        isInspectorSizerPointerOver = true;
        UpdateInspectorSizerVisual();
    }

    private void InspectorSizer_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        isInspectorSizerPointerOver = false;
        UpdateInspectorSizerVisual();
    }

    private void InspectorSizer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        isResizingInspector = true;
        inspectorResizeStartX = e.GetCurrentPoint(ContentSurface).Position.X;
        inspectorResizeStartWidth = InspectorColumn.ActualWidth;
        InspectorSizer.CapturePointer(e.Pointer);
        UpdateInspectorSizerVisual();
        e.Handled = true;
    }

    private void InspectorSizer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!isResizingInspector)
        {
            return;
        }

        var currentX = e.GetCurrentPoint(ContentSurface).Position.X;
        var requestedWidth = inspectorResizeStartWidth + currentX - inspectorResizeStartX;
        InspectorColumn.Width = new GridLength(ClampInspectorWidth(requestedWidth));
        e.Handled = true;
    }

    private void InspectorSizer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!isResizingInspector)
        {
            return;
        }

        isResizingInspector = false;
        InspectorSizer.ReleasePointerCaptures();
        UpdateInspectorSizerVisual();
        e.Handled = true;
    }

    private double ClampInspectorWidth(double requestedWidth)
    {
        var maxByWindow = Math.Max(InspectorColumn.MinWidth, ContentSurface.ActualWidth - 360);
        var maxWidth = Math.Min(InspectorColumn.MaxWidth, maxByWindow);
        return Math.Clamp(requestedWidth, InspectorColumn.MinWidth, maxWidth);
    }

    private void UpdateInspectorSizerVisual()
    {
        inspectorController.UpdateSizerVisual(isResizingInspector, isInspectorSizerPointerOver);
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        await fileOpenRecentController.PickAndOpenAsync();
    }

    private async void OpenRegion_Click(object sender, RoutedEventArgs e)
    {
        await regionCatalogController.PickAndLoadRegionsAsync();
    }

    private async void OpenCatalog_Click(object sender, RoutedEventArgs e)
    {
        await regionCatalogController.PickAndLoadCatalogAsync();
    }

    private async void SaveRegions_Click(object sender, RoutedEventArgs e)
    {
        await regionCatalogController.PickAndSaveRegionsAsync();
    }

    private void ClearRegions_Click(object sender, RoutedEventArgs e)
    {
        regionCatalogController.ClearRegions();
    }

    private async void RegionValuesText_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        regionInteractionController.ApplySelectedProperties();
        await Task.CompletedTask;
    }

    private async void RegionProperty_LostFocus(object sender, RoutedEventArgs e)
    {
        regionInteractionController.ApplySelectedProperties(silent: true);
        await Task.CompletedTask;
    }

    private async void ApplyRegionProperties_Click(object sender, RoutedEventArgs e)
    {
        regionInteractionController.ApplySelectedProperties();
        await Task.CompletedTask;
    }

    private void DuplicateRegion_Click(object sender, RoutedEventArgs e)
    {
        regionInteractionController.DuplicateSelected();
    }

    private void DeleteRegion_Click(object sender, RoutedEventArgs e)
    {
        regionInteractionController.DeleteSelected();
    }

    private void RegionToolCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (regionInteractionController is null)
        {
            return;
        }

        regionInteractionController.SelectToolFromCombo();
    }

    private async void OpenSample_Click(object sender, RoutedEventArgs e)
    {
        await fileOpenRecentController.OpenSampleAsync();
    }

    private async void SavePng_Click(object sender, RoutedEventArgs e)
    {
        await headerExportController.PickAndSavePngAsync();
    }

    public async Task OpenPathAsync(string path)
    {
        await fileOpenRecentController.OpenPathAsync(path);
    }

    private void LoadRegions(string path)
    {
        regionCatalogController.LoadRegions(path);
    }

    private void LoadCatalog(string path)
    {
        regionCatalogController.LoadCatalog(path);
    }

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        fileOpenRecentController.HandleDragOver(e);
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        await fileOpenRecentController.OpenDroppedAsync(e);
    }

    private async void RecentList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecentFileItem item)
        {
            await fileOpenRecentController.OpenRecentAsync(item);
        }
    }

    private async void FrameTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncingFrameTabSelection ||
            FrameTabs.SelectedItem is not TabViewItem { Tag: ImageFrameViewModel frame })
        {
            return;
        }

        await SelectFrameAsync(frame);
    }

    private async void FrameTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab?.Tag is ImageFrameViewModel frame)
        {
            await CloseFrameAsync(frame);
        }
    }

    private async Task CloseFrameAsync(ImageFrameViewModel frame)
    {
        var decision = frameWorkspace.Close(frame);
        if (!decision.Removed)
        {
            return;
        }

        RemoveFrameTab(frame);
        FramesChanged?.Invoke(this, EventArgs.Empty);

        frameBlinkController.StopIfFrameCountBelowMinimum(decision.RemainingFrameCount);

        if (decision.IsEmpty)
        {
            ClearFrameState();
            CurrentFrameChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (decision.WasCurrent && decision.NextFrame is not null)
        {
            await SelectFrameAsync(decision.NextFrame);
        }
        else if (currentFrame is not null)
        {
            SelectFrameTab(currentFrame);
        }
    }

    private void ClearFrameState()
    {
        frameWorkspace.ClearSelection();
        regionInteractionController.ClearSelection();
        regionCanvasInteractionController.ResetDrag();
        wcsGridSegments = [];
        viewportRenderController.Reset();

        syncingFrameTabSelection = true;
        FrameTabs.SelectedItem = null;
        syncingFrameTabSelection = false;

        overlayRenderer.Clear();

        metadataReadoutController.ResetFrameState();
        cubePlaneController.Reset();
        scaleColorController.Reset();

        SetStatus("Frame closed");
        AddLog("frame closed");
    }

    internal async Task SelectFrameAsync(ImageFrameViewModel frame)
    {
        if (!frameWorkspace.Contains(frame))
        {
            return;
        }

        if (frameWorkspace.IsCurrent(frame))
        {
            SelectFrameTab(frame);
            return;
        }

        frameWorkspace.Select(frame);
        regionInteractionController.ClearSelection();
        SelectFrameTab(frame);
        CurrentFrameChanged?.Invoke(this, EventArgs.Empty);
        UpdateMetadata(frame.Image);
        await RenderCurrentAsync(fitAfterRender: viewportRenderController.IsFitToViewport);
    }

    internal void SetToolStatus(string label)
    {
        SetStatus(label);
        AddLog(label);
    }

    private void AddFrameTab(ImageFrameViewModel frame)
    {
        var tab = new TabViewItem
        {
            Header = frame.DisplayName,
            Tag = frame,
            IsClosable = true
        };
        AutomationProperties.SetAutomationId(tab, $"FrameTab_{frame.Index}");
        AutomationProperties.SetName(tab, frame.DisplayName);
        FrameTabs.TabItems.Add(tab);
    }

    private void RemoveFrameTab(ImageFrameViewModel frame)
    {
        for (var i = FrameTabs.TabItems.Count - 1; i >= 0; i--)
        {
            if (FrameTabs.TabItems[i] is TabViewItem { Tag: ImageFrameViewModel tabFrame } &&
                ReferenceEquals(tabFrame, frame))
            {
                FrameTabs.TabItems.RemoveAt(i);
                return;
            }
        }
    }

    private void SelectFrameTab(ImageFrameViewModel frame)
    {
        syncingFrameTabSelection = true;
        foreach (var item in FrameTabs.TabItems)
        {
            if (item is TabViewItem { Tag: ImageFrameViewModel tabFrame } tab &&
                ReferenceEquals(tabFrame, frame))
            {
                FrameTabs.SelectedItem = tab;
                break;
            }
        }

        syncingFrameTabSelection = false;
    }

    private async void StretchCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (currentFrame is not null)
        {
            scaleColorController.UpdateScaleControls(currentFrame.Image);
            await RenderCurrentAsync(fitAfterRender: false);
        }
    }

    private void CutUnitCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (currentFrame is not null)
        {
            scaleColorController.UpdateCutControls(currentFrame.LowCut, currentFrame.HighCut);
        }
    }

    private async void ColorMapCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (currentFrame is not null)
        {
            await RenderCurrentAsync(fitAfterRender: false);
        }
    }

    private void CutSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (scaleColorController is null)
        {
            return;
        }

        if (scaleColorController.TryHandleSliderChange(sender))
        {
            cutRenderTimer.Stop();
            cutRenderTimer.Start();
        }
    }

    private async void CutValueBox_LostFocus(object sender, RoutedEventArgs e)
    {
        await ApplyCutInputAsync(sender);
    }

    private async void CutValueBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        await ApplyCutInputAsync(sender);
    }

    private async Task ApplyCutInputAsync(object sender)
    {
        if (scaleColorController.IsUpdating || currentFrame is null)
        {
            return;
        }

        if (!scaleColorController.TryApplyCutInput(sender, out var errorMessage))
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                AddLog(errorMessage);
                SetStatus("Invalid cut value");
            }
            return;
        }
        await RenderCurrentAsync(fitAfterRender: false);
    }

    private async void CutRenderTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        await RenderCurrentAsync(fitAfterRender: false);
    }

    private async Task RenderCurrentAsync(bool fitAfterRender)
    {
        await viewportRenderController.RenderCurrentAsync(fitAfterRender);
    }

    private void RefreshWcsGridForRenderedImage(LoadedImage image)
    {
        if (isWcsGridVisible)
        {
            wcsGridSegments = wcsGridGenerator.Generate(image);
        }
    }

    private void Fit_Click(object sender, RoutedEventArgs e)
    {
        viewportRenderController.Fit();
    }

    private void ActualSize_Click(object sender, RoutedEventArgs e)
    {
        viewportRenderController.ActualSize();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        viewportRenderController.ZoomIn();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        viewportRenderController.ZoomOut();
    }

    private void Blink_Click(object sender, RoutedEventArgs e)
    {
        frameBlinkController.ToggleRequested();
    }

    private void Bin_Click(object sender, RoutedEventArgs e)
    {
        if (currentFrame is null)
        {
            SetStatus("No frame");
            return;
        }

        SetStatus(string.IsNullOrWhiteSpace(currentFrame.Image.BinDescription)
            ? "Bin: native image"
            : $"Bin: {currentFrame.Image.BinDescription}");
    }

    private void Wcs_Click(object sender, RoutedEventArgs e)
    {
        ShowWcsSummary();
    }

    internal void ShowWcsSummary()
    {
        if (currentFrame?.Image.Wcs is null)
        {
            SetStatus("WCS: none");
            return;
        }

        SetStatus($"WCS: {currentFrame.Image.Wcs.Summary}");
    }

    private void WcsGrid_Click(object sender, RoutedEventArgs e)
    {
        ToggleWcsGrid();
    }

    internal void ToggleWcsGrid()
    {
        if (currentFrame is null)
        {
            SetStatus("No frame");
            return;
        }

        if (isWcsGridVisible)
        {
            isWcsGridVisible = false;
            wcsGridSegments = [];
            DrawRegions();
            SetStatus("WCS grid hidden");
            return;
        }

        wcsGridSegments = wcsGridGenerator.Generate(currentFrame.Image);
        isWcsGridVisible = wcsGridSegments.Count > 0;
        DrawRegions();
        SetStatus(isWcsGridVisible ? $"WCS grid: {wcsGridSegments.Count} segments" : "WCS grid unavailable");
    }

    private async void Header_Click(object sender, RoutedEventArgs e)
    {
        await ShowHeaderAsync();
    }

    internal async Task ShowHeaderAsync()
    {
        await headerExportController.ShowHeaderAsync();
    }

    private async void RunCommand_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteCommandAsync(CommandTextBox.Text);
    }

    private async void CommandTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        await ExecuteCommandAsync(CommandTextBox.Text);
    }

    private async Task ExecuteCommandAsync(string commandText)
    {
        await commandExecutionController.ExecuteAsync(commandText);
    }

    private void ApplyScaleCommand(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return;
        }

        var stretch = Enum.GetNames<ImageStretch>().FirstOrDefault(name =>
            string.Equals(name, args[0], StringComparison.OrdinalIgnoreCase));
        if (stretch is not null)
        {
            StretchCombo.SelectedItem = stretch;
            SetStatus($"Scale {stretch}");
        }
    }

    private void ApplyColorMapCommand(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return;
        }

        var color = Enum.GetNames<ImageColorMap>().FirstOrDefault(name =>
            string.Equals(name, args[0], StringComparison.OrdinalIgnoreCase));
        if (color is not null)
        {
            ColorMapCombo.SelectedItem = color;
            SetStatus($"Color {color}");
        }
    }

    private void ApplyZoomCommand(IReadOnlyList<string> args)
    {
        viewportRenderController.ApplyZoomCommand(args);
    }

    private async Task ApplyFrameCommandAsync(IReadOnlyList<string> args)
    {
        await frameCommandController.ApplyCommandAsync(args);
    }

    private void ApplyRegionCommand(IReadOnlyList<string> args)
    {
        regionCatalogController.ApplyRegionCommand(args);
    }

    private void ExecuteContourCommand()
    {
        analysisContourController.ExecuteContour();
    }

    private void Contour_Click(object sender, RoutedEventArgs e)
    {
        GenerateContourOverlay();
    }

    internal void GenerateContourOverlay()
    {
        analysisContourController.ExecuteContour();
    }

    private async void Analysis_Click(object sender, RoutedEventArgs e)
    {
        await RunAnalysisAsync();
    }

    internal async Task RunAnalysisAsync()
    {
        await analysisContourController.AnalyzeAsync();
    }

    private async void PlanePrevious_Click(object sender, RoutedEventArgs e)
    {
        var planeIndex = cubePlaneController.PreviousPlane();
        if (planeIndex is null)
        {
            return;
        }

        await LoadCurrentPlaneAsync(planeIndex.Value);
    }

    private async void PlaneNext_Click(object sender, RoutedEventArgs e)
    {
        var planeIndex = cubePlaneController.NextPlane();
        if (planeIndex is null)
        {
            return;
        }

        await LoadCurrentPlaneAsync(planeIndex.Value);
    }

    private async void PlaneSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (cubePlaneController is null)
        {
            return;
        }

        var planeIndex = cubePlaneController.PlaneFromSliderValue(e.NewValue);
        if (planeIndex is not null)
        {
            await LoadCurrentPlaneAsync(planeIndex.Value);
        }
    }

    private async Task LoadCurrentPlaneAsync(int planeIndex)
    {
        if (currentFrame is null || currentFrame.Image.CubeDepth <= 1)
        {
            return;
        }

        var image = currentFrame.Image;
        var boundedPlane = Math.Clamp(planeIndex, 0, image.CubeDepth - 1);
        if (boundedPlane == image.PlaneIndex)
        {
            return;
        }

        try
        {
            BusyRing.IsActive = true;
            BusyRing.Visibility = Visibility.Visible;
            SetStatus($"Loading plane {boundedPlane + 1}/{image.CubeDepth}");
            var loaded = await Task.Run(() => loader.LoadHdu(image.FilePath, image.HduIndex, boundedPlane));
            currentFrame.ReplaceImage(loaded);
            UpdateMetadata(loaded);
            await RenderCurrentAsync(fitAfterRender: false);
            AddLog($"plane {boundedPlane + 1}/{loaded.CubeDepth}");
            SetStatus($"Plane {boundedPlane + 1}/{loaded.CubeDepth}");
        }
        catch (Exception ex)
        {
            AddLog(ex.Message);
            SetStatus("Plane load failed");
        }
        finally
        {
            BusyRing.IsActive = false;
            BusyRing.Visibility = Visibility.Collapsed;
        }
    }

    private void ImageScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        viewportRenderController.HandleViewportSizeChanged();
    }

    private void ImageScroll_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        viewportRenderController.HandleViewportChanged();
    }

    private void UpdateMetadata(LoadedImage image)
    {
        metadataReadoutController.UpdateFrameMetadata(image);
        cubePlaneController.Update(image);
        scaleColorController.UpdateScaleControls(image);
        metadataReadoutController.ResetPointerReadout();
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void SetOpenBusy(bool active)
    {
        BusyRing.IsActive = active;
        BusyRing.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LogToggleButton_Click(object sender, RoutedEventArgs e)
    {
        LogPanel.Visibility = LogPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void AddLog(string message)
    {
        LogText.Text = $"{DateTimeOffset.Now:HH:mm:ss} {message}";
    }

    private void ImageHost_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (currentFrame is null)
        {
            metadataReadoutController.ResetPointerReadout();
            return;
        }

        var position = e.GetCurrentPoint(ImageHost).Position;
        var imagePoint = ToRasterPoint(position.X, position.Y);
        metadataReadoutController.UpdatePointerReadout(imagePoint.X, imagePoint.Y);
        regionCanvasInteractionController.MoveDrag(imagePoint.X, imagePoint.Y);
    }

    private void ImageHost_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        metadataReadoutController.ResetPointerReadout();
    }

    private void ImageHost_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (currentFrame is null)
        {
            return;
        }

        var position = e.GetCurrentPoint(ImageHost).Position;
        var imagePoint = ToRasterPoint(position.X, position.Y);
        var result = regionCanvasInteractionController.Press(imagePoint.X, imagePoint.Y);
        if (result.CapturePointer)
        {
            ImageHost.CapturePointer(e.Pointer);
        }

        if (result.Handled)
        {
            e.Handled = true;
        }
    }

    private void ImageHost_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!regionCanvasInteractionController.Release())
        {
            return;
        }

        ImageHost.ReleasePointerCaptures();
        e.Handled = true;
    }

    private void DrawRegions()
    {
        overlayRenderer.Render(currentFrame, wcsGridSegments, regionInteractionController.SelectedIndex, viewportRenderController.Zoom);
    }

    private (double X, double Y) ToRasterPoint(double displayX, double displayY)
    {
        var zoom = Math.Max(viewportRenderController.Zoom, 0.001);
        return (displayX / zoom, displayY / zoom);
    }

}
