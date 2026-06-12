using Microsoft.UI.Xaml.Controls;
using WinDS9.Engine;
using WinDS9.WinUI;

namespace WinDS9.WinUI.Controllers;

internal sealed class MetadataReadoutController
{
    private readonly TextBlock fileText;
    private readonly TextBlock frameText;
    private readonly TextBlock hduText;
    private readonly TextBlock typeText;
    private readonly TextBlock rasterText;
    private readonly TextBlock rowsText;
    private readonly TextBlock rangeText;
    private readonly TextBlock cutText;
    private readonly TextBlock loadText;
    private readonly TextBlock wcsText;
    private readonly TextBlock analysisText;
    private readonly TextBlock zoomText;
    private readonly TextBlock valueText;
    private readonly TextBlock imageCoordText;
    private readonly TextBlock physicalCoordText;
    private readonly TextBlock displayCoordText;
    private readonly TextBlock wcsCoordText;
    private readonly Func<ImageFrameViewModel?> getCurrentFrame;

    public MetadataReadoutController(
        TextBlock fileText,
        TextBlock frameText,
        TextBlock hduText,
        TextBlock typeText,
        TextBlock rasterText,
        TextBlock rowsText,
        TextBlock rangeText,
        TextBlock cutText,
        TextBlock loadText,
        TextBlock wcsText,
        TextBlock analysisText,
        TextBlock zoomText,
        TextBlock valueText,
        TextBlock imageCoordText,
        TextBlock physicalCoordText,
        TextBlock displayCoordText,
        TextBlock wcsCoordText,
        Func<ImageFrameViewModel?> getCurrentFrame)
    {
        this.fileText = fileText;
        this.frameText = frameText;
        this.hduText = hduText;
        this.typeText = typeText;
        this.rasterText = rasterText;
        this.rowsText = rowsText;
        this.rangeText = rangeText;
        this.cutText = cutText;
        this.loadText = loadText;
        this.wcsText = wcsText;
        this.analysisText = analysisText;
        this.zoomText = zoomText;
        this.valueText = valueText;
        this.imageCoordText = imageCoordText;
        this.physicalCoordText = physicalCoordText;
        this.displayCoordText = displayCoordText;
        this.wcsCoordText = wcsCoordText;
        this.getCurrentFrame = getCurrentFrame;
    }

    public void ResetFrameState()
    {
        fileText.Text = "--";
        frameText.Text = "--";
        hduText.Text = "--";
        typeText.Text = "--";
        rasterText.Text = "--";
        rowsText.Text = "--";
        rangeText.Text = "--";
        cutText.Text = "--";
        loadText.Text = "--";
        wcsText.Text = "none";
        analysisText.Text = "--";
        UpdateZoom(1);
        ResetPointerReadout();
    }

    public void UpdateFrameMetadata(LoadedImage image)
    {
        var frame = getCurrentFrame();
        fileText.Text = image.FilePath;
        frameText.Text = frame is null ? "--" : frame.Index.ToString();
        hduText.Text = image.HduIndex.ToString();
        typeText.Text = image.SourceKind;
        rasterText.Text = image.SourceWidth > 0 && image.SourceHeight > 0
            ? $"{image.Width} x {image.Height} from {image.SourceWidth} x {image.SourceHeight}"
            : $"{image.Width} x {image.Height}";
        rowsText.Text = image.BinnedRows > 0
            ? $"{image.BinnedRows:N0} / {image.SourceRows:N0}"
            : image.SourceRows.ToString("N0");
        rangeText.Text = $"{image.DataMin:0.###} - {image.DataMax:0.###}";
        UpdateCut(frame is null ? image.LowCut : frame.LowCut, frame is null ? image.HighCut : frame.HighCut);
        loadText.Text = $"{image.LoadDuration.TotalMilliseconds:0} ms";
        wcsText.Text = image.Wcs?.Summary ?? "none";
        UpdateAnalysisSummary(frame?.Analysis?.Summary ?? "--");
    }

    public void UpdateCut(double low, double high)
    {
        cutText.Text = ScaleColorController.FormatCut(low, high);
    }

    public void UpdateAnalysisSummary(string summary)
    {
        analysisText.Text = summary;
    }

    public void UpdateZoom(double zoom)
    {
        zoomText.Text = $"{zoom * 100:0.#}%";
    }

    public void UpdatePointerReadout(double displayX, double displayY)
    {
        if (getCurrentFrame() is not { } frame)
        {
            ResetPointerReadout();
            return;
        }

        var image = frame.Image;
        var pixelX = (int)Math.Floor(displayX);
        var pixelY = (int)Math.Floor(displayY);
        if ((uint)pixelX >= (uint)image.Width || (uint)pixelY >= (uint)image.Height)
        {
            ResetPointerReadout();
            return;
        }

        var index = pixelY * image.Width + pixelX;
        var sourceWidth = image.SourceWidth > 0 ? image.SourceWidth : image.Width;
        var sourceHeight = image.SourceHeight > 0 ? image.SourceHeight : image.Height;
        var imageX = displayX / image.Width * sourceWidth + 0.5;
        var imageY = sourceHeight + 0.5 - displayY / image.Height * sourceHeight;
        var value = image.Pixels[index];

        valueText.Text = $"{value:0.###}";
        imageCoordText.Text = $"x {imageX:0.###}, y {imageY:0.###}";
        physicalCoordText.Text = image.BlockFactor > 1
            ? $"x {imageX:0.###}, y {imageY:0.###}, block {image.BlockFactor}"
            : $"x {imageX:0.###}, y {imageY:0.###}";
        displayCoordText.Text = $"x {pixelX + 1}, y {image.Height - pixelY}";

        var world = image.Wcs?.PixelToWorld(imageX, imageY);
        wcsCoordText.Text = world.HasValue
            ? FormatWorldReadout(image.Wcs!, world.Value.World1, world.Value.World2)
            : "none";
    }

    public void ResetPointerReadout()
    {
        valueText.Text = "--";
        imageCoordText.Text = "--";
        physicalCoordText.Text = "--";
        displayCoordText.Text = "--";
        wcsCoordText.Text = getCurrentFrame()?.Image.Wcs is null ? "none" : "--";
    }

    private static string FormatWorldReadout(WcsMetadata wcs, double world1, double world2)
    {
        return wcs.FormatWorld(world1, world2).Replace(", ", "\n", StringComparison.Ordinal);
    }
}
