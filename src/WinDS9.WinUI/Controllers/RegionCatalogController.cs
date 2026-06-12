using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinDS9.Engine;
using WinDS9.WinUI.Pages;
using WinRT.Interop;

namespace WinDS9.WinUI.Controllers;

internal sealed class RegionCatalogController
{
    private readonly Ds9RegionParser regionParser = new();
    private readonly CatalogParser catalogParser = new();
    private readonly Ds9RegionSerializer regionSerializer = new();
    private readonly CoordinateTransformService coordinateTransforms;
    private readonly RegionInteractionController regionInteractionController;
    private readonly Func<ImageFrameViewModel?> getCurrentFrame;
    private readonly Func<IntPtr> getWindowHandle;
    private readonly Action drawRegions;
    private readonly Action<string> setStatus;
    private readonly Action<string> addLog;

    public RegionCatalogController(
        CoordinateTransformService coordinateTransforms,
        RegionInteractionController regionInteractionController,
        Func<ImageFrameViewModel?> getCurrentFrame,
        Func<IntPtr> getWindowHandle,
        Action drawRegions,
        Action<string> setStatus,
        Action<string> addLog)
    {
        this.coordinateTransforms = coordinateTransforms;
        this.regionInteractionController = regionInteractionController;
        this.getCurrentFrame = getCurrentFrame;
        this.getWindowHandle = getWindowHandle;
        this.drawRegions = drawRegions;
        this.setStatus = setStatus;
        this.addLog = addLog;
    }

    public async Task PickAndLoadRegionsAsync()
    {
        if (getCurrentFrame() is null)
        {
            setStatus("Open an image before loading regions");
            return;
        }

        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".reg");
        picker.FileTypeFilter.Add(".txt");

        InitializeWithWindow.Initialize(picker, getWindowHandle());

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            LoadRegions(file.Path);
        }
    }

    public async Task PickAndLoadCatalogAsync()
    {
        if (getCurrentFrame() is null)
        {
            setStatus("Open an image before loading a catalog");
            return;
        }

        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".tsv");
        picker.FileTypeFilter.Add(".cat");
        picker.FileTypeFilter.Add(".txt");

        InitializeWithWindow.Initialize(picker, getWindowHandle());

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            LoadCatalog(file.Path);
        }
    }

    public async Task PickAndSaveRegionsAsync()
    {
        var frame = getCurrentFrame();
        if (frame is null || frame.Regions.Count == 0)
        {
            setStatus("No regions to save");
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = $"{Path.GetFileNameWithoutExtension(frame.Image.FilePath)}.reg"
        };
        picker.FileTypeChoices.Add("DS9 region file", [".reg"]);

        InitializeWithWindow.Initialize(picker, getWindowHandle());

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        await FileIO.WriteTextAsync(file, regionSerializer.Serialize(frame.Regions));
        addLog($"saved regions {file.Path}");
        setStatus($"Saved {frame.Regions.Count} regions");
    }

    public void ClearRegions()
    {
        var frame = getCurrentFrame();
        if (frame is null)
        {
            return;
        }

        frame.Regions.Clear();
        frame.Contours.Clear();
        drawRegions();
        regionInteractionController.ClearSelection("Cleared");
        setStatus("Regions cleared");
    }

    public void ApplyRegionCommand(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return;
        }

        if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
        {
            ClearRegions();
            return;
        }

        LoadRegions(args[0]);
    }

    public void LoadRegions(string path)
    {
        var frame = getCurrentFrame();
        if (frame is null)
        {
            return;
        }

        try
        {
            var regions = regionParser.ParseFile(path);
            frame.Regions.Clear();
            var skipped = 0;
            foreach (var region in regions)
            {
                var converted = coordinateTransforms.ToImageRegion(region, frame.Image.Wcs);
                if (converted is null)
                {
                    skipped++;
                    continue;
                }

                frame.Regions.Add(converted);
            }

            drawRegions();
            regionInteractionController.SelectFirstRegion();
            addLog($"regions {Path.GetFileName(path)}: {frame.Regions.Count} loaded, {skipped} skipped");
            setStatus($"Loaded {frame.Regions.Count} regions");
        }
        catch (Exception ex)
        {
            addLog(ex.Message);
            setStatus("Region load failed");
        }
    }

    public void LoadCatalog(string path)
    {
        var frame = getCurrentFrame();
        if (frame is null)
        {
            return;
        }

        try
        {
            var entries = catalogParser.ParseFile(path);
            var added = 0;
            var skipped = 0;
            foreach (var entry in entries)
            {
                (double X, double Y)? imagePoint = entry.CoordinateSystem == CatalogCoordinateSystem.Image
                    ? (entry.ImageX, entry.ImageY)
                    : coordinateTransforms.WorldToImage(frame.Image.Wcs, entry.First, entry.Second, entry.SkyFrame);

                if (!imagePoint.HasValue)
                {
                    skipped++;
                    continue;
                }

                frame.Regions.Add(new Ds9Region(
                    Ds9RegionKind.Point,
                    [imagePoint.Value.X, imagePoint.Value.Y],
                    "image",
                    entry.Label));
                added++;
            }

            drawRegions();
            regionInteractionController.SelectLastRegion();
            addLog($"catalog {Path.GetFileName(path)}: {added} projected, {skipped} skipped");
            setStatus($"Loaded {added} catalog points");
        }
        catch (Exception ex)
        {
            addLog(ex.Message);
            setStatus("Catalog load failed");
        }
    }
}
