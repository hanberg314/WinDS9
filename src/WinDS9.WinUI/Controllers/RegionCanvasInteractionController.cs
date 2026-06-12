using WinDS9.Engine;
using WinDS9.WinUI;

namespace WinDS9.WinUI.Controllers;

internal sealed class RegionCanvasInteractionController
{
    private readonly RegionInteractionController regionInteractionController;
    private readonly RegionEditService regionEditService;
    private readonly CoordinateTransformService coordinateTransforms;
    private readonly Func<ImageFrameViewModel?> getCurrentFrame;
    private readonly Func<double> getZoom;
    private readonly Action redrawRegions;
    private readonly Action<string> addLog;
    private (double X, double Y) dragStartImagePoint;
    private List<Ds9Region> dragStartRegions = [];

    public RegionCanvasInteractionController(
        RegionInteractionController regionInteractionController,
        RegionEditService regionEditService,
        CoordinateTransformService coordinateTransforms,
        Func<ImageFrameViewModel?> getCurrentFrame,
        Func<double> getZoom,
        Action redrawRegions,
        Action<string> addLog)
    {
        this.regionInteractionController = regionInteractionController;
        this.regionEditService = regionEditService;
        this.coordinateTransforms = coordinateTransforms;
        this.getCurrentFrame = getCurrentFrame;
        this.getZoom = getZoom;
        this.redrawRegions = redrawRegions;
        this.addLog = addLog;
    }

    public bool IsDragging { get; private set; }

    public void ResetDrag()
    {
        IsDragging = false;
        dragStartRegions.Clear();
    }

    public void MoveDrag(double displayX, double displayY)
    {
        if (!IsDragging || getCurrentFrame() is not { } frame)
        {
            return;
        }

        var selectedIndex = regionInteractionController.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= dragStartRegions.Count)
        {
            return;
        }

        var imagePoint = DisplayToImagePoint(frame.Image, displayX, displayY);
        var dx = imagePoint.X - dragStartImagePoint.X;
        var dy = imagePoint.Y - dragStartImagePoint.Y;
        frame.Regions[selectedIndex] = regionEditService.Move(dragStartRegions[selectedIndex], dx, dy);
        redrawRegions();
        regionInteractionController.UpdateSelectedRegionControls();
    }

    public RegionPointerPressResult Press(double displayX, double displayY)
    {
        if (getCurrentFrame() is not { } frame)
        {
            return RegionPointerPressResult.None;
        }

        var imagePoint = DisplayToImagePoint(frame.Image, displayX, displayY);
        if (TryCreateRegionAt(frame, imagePoint))
        {
            return new RegionPointerPressResult(Handled: true, CapturePointer: false);
        }

        if (regionInteractionController.CurrentTool != RegionEditTool.Select)
        {
            return RegionPointerPressResult.None;
        }

        var hitIndex = HitTestRegion(frame.Image, imagePoint);
        regionInteractionController.SelectRegion(hitIndex);
        if (hitIndex >= 0)
        {
            IsDragging = true;
            dragStartImagePoint = imagePoint;
            dragStartRegions = frame.Regions.ToList();
            regionInteractionController.SetRegionStatus($"Selected region {hitIndex + 1}");
            redrawRegions();
            return new RegionPointerPressResult(Handled: true, CapturePointer: true);
        }

        redrawRegions();
        regionInteractionController.ClearSelection("No region selected");
        return RegionPointerPressResult.None;
    }

    public bool Release()
    {
        if (!IsDragging)
        {
            return false;
        }

        IsDragging = false;
        dragStartRegions = [];
        regionInteractionController.UpdateSelectedRegionControls();
        addLog($"moved region {regionInteractionController.SelectedIndex + 1}");
        return true;
    }

    private bool TryCreateRegionAt(ImageFrameViewModel frame, (double X, double Y) imagePoint)
    {
        var image = frame.Image;
        var radius = Math.Max(4, Math.Min(image.SourceWidth > 0 ? image.SourceWidth : image.Width, image.SourceHeight > 0 ? image.SourceHeight : image.Height) / 40.0);
        Ds9Region? region = regionInteractionController.CurrentTool switch
        {
            RegionEditTool.Point => new Ds9Region(Ds9RegionKind.Point, [imagePoint.X, imagePoint.Y], "image"),
            RegionEditTool.Circle => new Ds9Region(Ds9RegionKind.Circle, [imagePoint.X, imagePoint.Y, radius], "image"),
            RegionEditTool.Box => new Ds9Region(Ds9RegionKind.Box, [imagePoint.X, imagePoint.Y, radius * 2, radius * 2, 0], "image"),
            RegionEditTool.Text => new Ds9Region(Ds9RegionKind.Text, [imagePoint.X, imagePoint.Y], "image", "text"),
            _ => null
        };

        if (regionInteractionController.CurrentTool == RegionEditTool.Line)
        {
            if (regionInteractionController.PendingLineStart is null)
            {
                regionInteractionController.PendingLineStart = imagePoint;
                regionInteractionController.SetRegionStatus($"Line start x {imagePoint.X:0.#}, y {imagePoint.Y:0.#}");
                return true;
            }

            var lineStart = regionInteractionController.PendingLineStart.Value;
            region = new Ds9Region(
                Ds9RegionKind.Line,
                [lineStart.X, lineStart.Y, imagePoint.X, imagePoint.Y],
                "image");
            regionInteractionController.PendingLineStart = null;
        }

        if (region is null)
        {
            return false;
        }

        frame.Regions.Add(region);
        regionInteractionController.SelectLastRegion($"Added {region.Kind}");
        redrawRegions();
        addLog($"region {region.Kind} x={imagePoint.X:0.#} y={imagePoint.Y:0.#}");
        return true;
    }

    private int HitTestRegion(LoadedImage image, (double X, double Y) imagePoint)
    {
        if (getCurrentFrame() is not { } frame)
        {
            return -1;
        }

        var tolerance = Math.Max(image.SourceWidth > 0 ? image.SourceWidth : image.Width, image.SourceHeight > 0 ? image.SourceHeight : image.Height) / (Math.Max(getZoom(), 0.001) * 120);
        tolerance = Math.Clamp(tolerance, 3, 40);

        for (var i = frame.Regions.Count - 1; i >= 0; i--)
        {
            var region = coordinateTransforms.ToImageRegion(frame.Regions[i], image.Wcs);
            if (region is not null && IsRegionHit(region, imagePoint, tolerance))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsRegionHit(Ds9Region region, (double X, double Y) point, double tolerance)
    {
        return region.Kind switch
        {
            Ds9RegionKind.Point or Ds9RegionKind.Text => Distance(point, (region.Values[0], region.Values[1])) <= tolerance,
            Ds9RegionKind.Circle => Distance(point, (region.Values[0], region.Values[1])) <= region.Values[2] + tolerance,
            Ds9RegionKind.Annulus => Distance(point, (region.Values[0], region.Values[1])) <= region.Values.Skip(2).DefaultIfEmpty(0).Max() + tolerance,
            Ds9RegionKind.Box or Ds9RegionKind.Ellipse => Math.Abs(point.X - region.Values[0]) <= region.Values[2] / 2 + tolerance &&
                                                          Math.Abs(point.Y - region.Values[1]) <= region.Values[3] / 2 + tolerance,
            Ds9RegionKind.Line or Ds9RegionKind.Ruler or Ds9RegionKind.Vector or Ds9RegionKind.Projection =>
                DistanceToSegment(point, (region.Values[0], region.Values[1]), (region.Values[2], region.Values[3])) <= tolerance,
            Ds9RegionKind.Polygon or Ds9RegionKind.Segment => IsPairPolylineHit(region.Values, point, tolerance),
            _ => false
        };
    }

    private static bool IsPairPolylineHit(IReadOnlyList<double> values, (double X, double Y) point, double tolerance)
    {
        for (var i = 0; i + 3 < values.Count; i += 2)
        {
            if (DistanceToSegment(point, (values[i], values[i + 1]), (values[i + 2], values[i + 3])) <= tolerance)
            {
                return true;
            }
        }

        if (values.Count >= 6 &&
            DistanceToSegment(point, (values[^2], values[^1]), (values[0], values[1])) <= tolerance)
        {
            return true;
        }

        return false;
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double DistanceToSegment((double X, double Y) point, (double X, double Y) a, (double X, double Y) b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        if (dx == 0 && dy == 0)
        {
            return Distance(point, a);
        }

        var t = Math.Clamp(((point.X - a.X) * dx + (point.Y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
        return Distance(point, (a.X + t * dx, a.Y + t * dy));
    }

    private static (double X, double Y) DisplayToImagePoint(LoadedImage image, double displayX, double displayY)
    {
        var sourceWidth = image.SourceWidth > 0 ? image.SourceWidth : image.Width;
        var sourceHeight = image.SourceHeight > 0 ? image.SourceHeight : image.Height;
        return (
            displayX / image.Width * sourceWidth + 0.5,
            sourceHeight + 0.5 - displayY / image.Height * sourceHeight);
    }
}

internal sealed record RegionPointerPressResult(bool Handled, bool CapturePointer)
{
    public static RegionPointerPressResult None { get; } = new(Handled: false, CapturePointer: false);
}
