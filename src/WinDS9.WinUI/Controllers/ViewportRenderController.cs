using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using WinDS9.Engine;
using WinDS9.WinUI;

namespace WinDS9.WinUI.Controllers;

internal sealed class ViewportRenderController
{
    private const int TileOverscanPixels = 96;

    private static readonly double[] RasterZoomLevels =
    [
        0.03125,
        0.0625,
        0.125,
        0.25,
        0.5,
        1,
        2,
        4,
        8,
        16,
        32
    ];
    private const double EventFitProjectionThresholdFactor = 0.15;
    private const double EventFitMinimumCoverage = 0.05;
    private const int EventFitPaddingPixels = 2;

    private readonly IRasterRenderer renderer = new RasterRenderer();
    private readonly ScrollViewer imageScroll;
    private readonly FrameworkElement viewportHost;
    private readonly FrameworkElement imageHost;
    private readonly Image rasterImage;
    private readonly Canvas regionCanvas;
    private readonly ScaleTransform imageScale;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly Func<ImageFrameViewModel?> getCurrentFrame;
    private readonly Func<ImageStretch> getStretch;
    private readonly Func<ImageColorMap> getColorMap;
    private readonly Action<double, double> updateCut;
    private readonly Action<double> updateZoom;
    private readonly Action<LoadedImage> refreshWcsGrid;
    private readonly Action drawRegions;
    private readonly Action<string> setStatus;
    private double imageWidth = 1;
    private double imageHeight = 1;
    private RenderedRaster? displayedRaster;
    private int displayedTileX = -1;
    private int displayedTileY = -1;
    private int displayedTileWidth;
    private int displayedTileHeight;
    private RasterBounds? fitBounds;

    public ViewportRenderController(
        ScrollViewer imageScroll,
        FrameworkElement viewportHost,
        FrameworkElement imageHost,
        Image rasterImage,
        Canvas regionCanvas,
        ScaleTransform imageScale,
        DispatcherQueue dispatcherQueue,
        Func<ImageFrameViewModel?> getCurrentFrame,
        Func<ImageStretch> getStretch,
        Func<ImageColorMap> getColorMap,
        Action<double, double> updateCut,
        Action<double> updateZoom,
        Action<LoadedImage> refreshWcsGrid,
        Action drawRegions,
        Action<string> setStatus)
    {
        this.imageScroll = imageScroll;
        this.viewportHost = viewportHost;
        this.imageHost = imageHost;
        this.rasterImage = rasterImage;
        this.regionCanvas = regionCanvas;
        this.imageScale = imageScale;
        this.dispatcherQueue = dispatcherQueue;
        this.getCurrentFrame = getCurrentFrame;
        this.getStretch = getStretch;
        this.getColorMap = getColorMap;
        this.updateCut = updateCut;
        this.updateZoom = updateZoom;
        this.refreshWcsGrid = refreshWcsGrid;
        this.drawRegions = drawRegions;
        this.setStatus = setStatus;
    }

    public double Zoom { get; private set; } = 1;

    public bool IsFitToViewport { get; private set; } = true;

    public RenderedRaster? LastRenderedRaster { get; private set; }

    public async Task RenderCurrentAsync(bool fitAfterRender)
    {
        var frame = getCurrentFrame();
        if (frame is null)
        {
            return;
        }

        var image = frame.Image;
        var stretch = getStretch();
        var colorMap = getColorMap();
        var lowCut = frame.LowCut;
        var highCut = frame.HighCut;

        var renderWatch = System.Diagnostics.Stopwatch.StartNew();
        var rendered = await Task.Run(() => renderer.Render(image, stretch, colorMap, lowCut, highCut));
        renderWatch.Stop();
        LastRenderedRaster = rendered;
        fitBounds = DetectFitBounds(image);

        var center = CaptureScrollCenter();
        SetImageSurface(rendered);
        SetImageContentSize(rendered.Width, rendered.Height);
        updateCut(rendered.LowCut, rendered.HighCut);
        setStatus($"Rendered {rendered.Width}x{rendered.Height} in {renderWatch.Elapsed.TotalMilliseconds:0} ms");

        if (fitAfterRender)
        {
            IsFitToViewport = true;
            FitToViewport();
        }
        else
        {
            ApplyZoomLayout(preserveCenter: true, center.X, center.Y);
        }

        refreshWcsGrid(image);
        drawRegions();
    }

    public void HandleViewportSizeChanged()
    {
        if (getCurrentFrame() is not null && IsFitToViewport)
        {
            FitToViewport();
            return;
        }

        HandleViewportChanged();
    }

    public void HandleViewportChanged()
    {
        UpdateDisplayedBitmap();
    }

    public void Fit()
    {
        IsFitToViewport = true;
        FitToViewport();
    }

    public void ActualSize()
    {
        IsFitToViewport = false;
        SetZoom(1);
    }

    public void ZoomIn()
    {
        IsFitToViewport = false;
        SetZoom(NextRasterZoom(Zoom));
    }

    public void ZoomOut()
    {
        IsFitToViewport = false;
        SetZoom(PreviousRasterZoom(Zoom));
    }

    public void ApplyZoomCommand(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || string.Equals(args[0], "fit", StringComparison.OrdinalIgnoreCase))
        {
            Fit();
            return;
        }

        if (string.Equals(args[0], "in", StringComparison.OrdinalIgnoreCase))
        {
            ZoomIn();
            return;
        }

        if (string.Equals(args[0], "out", StringComparison.OrdinalIgnoreCase))
        {
            ZoomOut();
            return;
        }

        if (double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            IsFitToViewport = false;
            SetZoom(value > 10 ? value / 100 : value);
        }
    }

    public void Reset()
    {
        LastRenderedRaster = null;
        Zoom = 1;
        IsFitToViewport = true;
        imageWidth = 1;
        imageHeight = 1;
        fitBounds = null;
        ClearSurface();
        imageScale.ScaleX = 1;
        imageScale.ScaleY = 1;
        viewportHost.Width = 1;
        viewportHost.Height = 1;
        imageHost.Width = 1;
        imageHost.Height = 1;
        rasterImage.Width = 1;
        rasterImage.Height = 1;
        regionCanvas.Width = 1;
        regionCanvas.Height = 1;
        imageScroll.ChangeView(0, 0, null, disableAnimation: true);
    }

    private void FitToViewport()
    {
        if (getCurrentFrame() is null ||
            imageScroll.ViewportWidth <= 0 ||
            imageScroll.ViewportHeight <= 0 ||
            imageWidth <= 0 ||
            imageHeight <= 0)
        {
            return;
        }

        var bounds = fitBounds ?? new RasterBounds(0, 0, imageWidth, imageHeight);
        var fitX = imageScroll.ViewportWidth / bounds.Width;
        var fitY = imageScroll.ViewportHeight / bounds.Height;
        SetZoom(Math.Min(fitX, fitY) * 0.98, preserveCenter: false);
        QueueScrollToBounds(bounds);
    }

    private void SetZoom(double value, bool preserveCenter = true)
    {
        var center = CaptureScrollCenter();

        Zoom = Math.Clamp(value, 0.05, 32);
        ApplyZoomLayout(preserveCenter, center.X, center.Y);
        updateZoom(Zoom);
        drawRegions();
    }

    private (double X, double Y) CaptureScrollCenter()
    {
        var safeZoom = Math.Max(Zoom, 0.001);
        return (
            (imageScroll.HorizontalOffset + imageScroll.ViewportWidth / 2) / safeZoom,
            (imageScroll.VerticalOffset + imageScroll.ViewportHeight / 2) / safeZoom);
    }

    private void SetImageContentSize(int width, int height)
    {
        imageWidth = width;
        imageHeight = height;
        regionCanvas.Width = width;
        regionCanvas.Height = height;
    }

    private void SetImageSurface(RenderedRaster raster)
    {
        ClearSurface();
    }

    private void ApplyZoomLayout(bool preserveCenter, double centerX = 0, double centerY = 0)
    {
        imageScale.ScaleX = Zoom;
        imageScale.ScaleY = Zoom;

        var displayWidth = Math.Max(1, (int)Math.Ceiling(imageWidth * Zoom));
        var displayHeight = Math.Max(1, (int)Math.Ceiling(imageHeight * Zoom));
        viewportHost.Width = displayWidth;
        viewportHost.Height = displayHeight;
        imageHost.Width = displayWidth;
        imageHost.Height = displayHeight;
        UpdateDisplayedBitmap();

        if (preserveCenter && getCurrentFrame() is not null)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                var targetX = Math.Max(0, centerX * Zoom - imageScroll.ViewportWidth / 2);
                var targetY = Math.Max(0, centerY * Zoom - imageScroll.ViewportHeight / 2);
                imageScroll.ChangeView(targetX, targetY, null, disableAnimation: true);
            });
        }
    }

    private static double NextRasterZoom(double currentZoom)
    {
        foreach (var zoomLevel in RasterZoomLevels)
        {
            if (zoomLevel > currentZoom * 1.0001)
            {
                return zoomLevel;
            }
        }

        return RasterZoomLevels[^1];
    }

    private static double PreviousRasterZoom(double currentZoom)
    {
        for (var i = RasterZoomLevels.Length - 1; i >= 0; i--)
        {
            if (RasterZoomLevels[i] < currentZoom / 1.0001)
            {
                return RasterZoomLevels[i];
            }
        }

        return RasterZoomLevels[0];
    }

    private void QueueScrollToBounds(RasterBounds bounds)
    {
        dispatcherQueue.TryEnqueue(() =>
        {
            var centerX = bounds.X + bounds.Width / 2.0;
            var centerY = bounds.Y + bounds.Height / 2.0;
            var targetX = Math.Max(0, centerX * Zoom - imageScroll.ViewportWidth / 2);
            var targetY = Math.Max(0, centerY * Zoom - imageScroll.ViewportHeight / 2);
            imageScroll.ChangeView(targetX, targetY, null, disableAnimation: true);
        });
    }

    private static RasterBounds? DetectFitBounds(LoadedImage image)
    {
        if (image.SourceKind.Contains("full extent", StringComparison.OrdinalIgnoreCase))
        {
            return new RasterBounds(0, 0, image.Width, image.Height);
        }

        if (!image.BinDescription.Contains("bincols=", StringComparison.OrdinalIgnoreCase) ||
            image.BinnedRows <= 0 ||
            image.Pixels.Length == 0)
        {
            return null;
        }

        var minX = image.Width;
        var minY = image.Height;
        var maxX = -1;
        var maxY = -1;
        var rowTotals = new double[image.Height];
        var columnTotals = new double[image.Width];
        double total = 0;
        for (var y = 0; y < image.Height; y++)
        {
            var rowOffset = y * image.Width;
            for (var x = 0; x < image.Width; x++)
            {
                var value = image.Pixels[rowOffset + x];
                if (value <= 0)
                {
                    continue;
                }

                total += value;
                rowTotals[y] += value;
                columnTotals[x] += value;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return null;
        }

        var nonZeroBounds = new RasterBounds(
            minX,
            minY,
            Math.Max(1, maxX - minX + 1),
            Math.Max(1, maxY - minY + 1));

        return DetectProjectedEventBounds(rowTotals, columnTotals, total, image.Width, image.Height) ?? nonZeroBounds;
    }

    private static RasterBounds? DetectProjectedEventBounds(
        double[] rowTotals,
        double[] columnTotals,
        double total,
        int imageWidth,
        int imageHeight)
    {
        if (total <= 0)
        {
            return null;
        }

        var rowRange = FindProjectedRange(rowTotals, total / Math.Max(1, imageHeight) * EventFitProjectionThresholdFactor);
        var columnRange = FindProjectedRange(columnTotals, total / Math.Max(1, imageWidth) * EventFitProjectionThresholdFactor);
        if (rowRange is null || columnRange is null)
        {
            return null;
        }

        var minX = Math.Max(0, columnRange.Value.Min - EventFitPaddingPixels);
        var maxX = Math.Min(imageWidth - 1, columnRange.Value.Max + EventFitPaddingPixels);
        var minY = Math.Max(0, rowRange.Value.Min - EventFitPaddingPixels);
        var maxY = Math.Min(imageHeight - 1, rowRange.Value.Max + EventFitPaddingPixels);
        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        if (width < imageWidth * EventFitMinimumCoverage ||
            height < imageHeight * EventFitMinimumCoverage)
        {
            return null;
        }

        return new RasterBounds(minX, minY, width, height);
    }

    private static (int Min, int Max)? FindProjectedRange(double[] totals, double threshold)
    {
        var min = -1;
        var max = -1;
        for (var index = 0; index < totals.Length; index++)
        {
            if (totals[index] <= threshold)
            {
                continue;
            }

            if (min < 0)
            {
                min = index;
            }

            max = index;
        }

        return min < 0 || max < min ? null : (min, max);
    }

    private void ClearSurface()
    {
        rasterImage.Source = null;
        displayedRaster = null;
        displayedTileX = -1;
        displayedTileY = -1;
        displayedTileWidth = 0;
        displayedTileHeight = 0;
    }

    private void UpdateDisplayedBitmap()
    {
        var raster = LastRenderedRaster;
        if (raster is null)
        {
            return;
        }

        var displayWidth = Math.Max(1, (int)Math.Ceiling(imageWidth * Zoom));
        var displayHeight = Math.Max(1, (int)Math.Ceiling(imageHeight * Zoom));
        var viewportWidth = imageScroll.ViewportWidth > 0 ? imageScroll.ViewportWidth : displayWidth;
        var viewportHeight = imageScroll.ViewportHeight > 0 ? imageScroll.ViewportHeight : displayHeight;
        var tileX = Math.Max(0, (int)Math.Floor(imageScroll.HorizontalOffset) - TileOverscanPixels);
        var tileY = Math.Max(0, (int)Math.Floor(imageScroll.VerticalOffset) - TileOverscanPixels);
        var tileRight = Math.Min(displayWidth, (int)Math.Ceiling(imageScroll.HorizontalOffset + viewportWidth) + TileOverscanPixels);
        var tileBottom = Math.Min(displayHeight, (int)Math.Ceiling(imageScroll.VerticalOffset + viewportHeight) + TileOverscanPixels);
        var targetWidth = Math.Max(1, tileRight - tileX);
        var targetHeight = Math.Max(1, tileBottom - tileY);

        if (ReferenceEquals(displayedRaster, raster) &&
            displayedTileX == tileX &&
            displayedTileY == tileY &&
            displayedTileWidth == targetWidth &&
            displayedTileHeight == targetHeight)
        {
            return;
        }

        var bitmap = new WriteableBitmap(targetWidth, targetHeight);
        using (var pixelStream = bitmap.PixelBuffer.AsStream())
        {
            var scaled = RenderVisibleTileNearest(raster, tileX, tileY, targetWidth, targetHeight, Zoom);
            pixelStream.Write(scaled, 0, scaled.Length);
        }

        bitmap.Invalidate();
        rasterImage.Source = bitmap;
        rasterImage.Width = targetWidth;
        rasterImage.Height = targetHeight;
        Canvas.SetLeft(rasterImage, tileX);
        Canvas.SetTop(rasterImage, tileY);
        displayedRaster = raster;
        displayedTileX = tileX;
        displayedTileY = tileY;
        displayedTileWidth = targetWidth;
        displayedTileHeight = targetHeight;
    }

    private static byte[] RenderVisibleTileNearest(
        RenderedRaster raster,
        int tileX,
        int tileY,
        int targetWidth,
        int targetHeight,
        double zoom)
    {
        var output = new byte[checked(targetWidth * targetHeight * 4)];
        var source = raster.Bgra32;
        var safeZoom = Math.Max(zoom, 0.001);
        for (var y = 0; y < targetHeight; y++)
        {
            var sourceY = Math.Clamp((int)Math.Floor((tileY + y) / safeZoom), 0, raster.Height - 1);
            var sourceRow = sourceY * raster.Width * 4;
            var targetRow = y * targetWidth * 4;
            for (var x = 0; x < targetWidth; x++)
            {
                var sourceX = Math.Clamp((int)Math.Floor((tileX + x) / safeZoom), 0, raster.Width - 1);
                var sourceOffset = sourceRow + sourceX * 4;
                var targetOffset = targetRow + x * 4;
                output[targetOffset] = source[sourceOffset];
                output[targetOffset + 1] = source[sourceOffset + 1];
                output[targetOffset + 2] = source[sourceOffset + 2];
                output[targetOffset + 3] = source[sourceOffset + 3];
            }
        }

        return output;
    }

    private readonly record struct RasterBounds(double X, double Y, double Width, double Height);
}
