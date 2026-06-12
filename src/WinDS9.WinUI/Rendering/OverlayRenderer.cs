using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using WinDS9.Engine;
using WinDS9.WinUI;

namespace WinDS9.WinUI.Rendering;

internal sealed class OverlayRenderer
{
    private const double RegionStrokeThickness = 1.5;

    private readonly Canvas canvas;
    private readonly CoordinateTransformService coordinateTransforms;
    private readonly Func<string, Brush> brushProvider;
    private Brush? activeRegionBrush;
    private double zoom = 1;

    public OverlayRenderer(
        Canvas canvas,
        CoordinateTransformService coordinateTransforms,
        Func<string, Brush> brushProvider)
    {
        this.canvas = canvas;
        this.coordinateTransforms = coordinateTransforms;
        this.brushProvider = brushProvider;
    }

    public void Clear()
    {
        canvas.Children.Clear();
    }

    public void Render(
        ImageFrameViewModel? frame,
        IReadOnlyList<WcsGridSegment> wcsGridSegments,
        int selectedRegionIndex,
        double zoom)
    {
        canvas.Children.Clear();
        if (frame is null)
        {
            return;
        }

        this.zoom = zoom;
        foreach (var grid in wcsGridSegments)
        {
            DrawOverlayLine(grid.X1, grid.Y1, grid.X2, grid.Y2, WcsGridBrush, 0.8 / Math.Max(zoom, 0.001));
        }

        DrawWcsGridLabels(wcsGridSegments);

        for (var i = 0; i < frame.Regions.Count; i++)
        {
            var imageRegion = coordinateTransforms.ToImageRegion(frame.Regions[i], frame.Image.Wcs);
            if (imageRegion is not null)
            {
                activeRegionBrush = i == selectedRegionIndex ? SelectedRegionBrush : RegionBrush;
                DrawRegion(frame.Image, imageRegion);
            }
        }

        activeRegionBrush = null;

        foreach (var contour in frame.Contours)
        {
            DrawContourSegment(contour);
        }
    }

    private void DrawRegion(LoadedImage image, Ds9Region region)
    {
        switch (region.Kind)
        {
            case Ds9RegionKind.Circle:
                DrawEllipseRegion(image, region.Values[0], region.Values[1], region.Values[2], region.Values[2], 0);
                break;
            case Ds9RegionKind.Ellipse:
                DrawEllipseRegion(
                    image,
                    region.Values[0],
                    region.Values[1],
                    region.Values[2],
                    region.Values[3],
                    region.Values.Count > 4 ? region.Values[4] : 0);
                break;
            case Ds9RegionKind.Box:
                DrawBoxRegion(
                    image,
                    region.Values[0],
                    region.Values[1],
                    region.Values[2],
                    region.Values[3],
                    region.Values.Count > 4 ? region.Values[4] : 0);
                break;
            case Ds9RegionKind.Point:
                DrawPointRegion(image, region.Values[0], region.Values[1]);
                break;
            case Ds9RegionKind.Line:
                DrawLineRegion(image, region.Values[0], region.Values[1], region.Values[2], region.Values[3]);
                break;
            case Ds9RegionKind.Polygon:
                DrawPolygonRegion(image, region.Values, close: true);
                break;
            case Ds9RegionKind.Annulus:
                DrawAnnulusRegion(image, region);
                break;
            case Ds9RegionKind.Text:
                DrawTextRegion(image, region);
                break;
            case Ds9RegionKind.Ruler:
            case Ds9RegionKind.Vector:
            case Ds9RegionKind.Projection:
                DrawLineRegion(image, region.Values[0], region.Values[1], region.Values[2], region.Values[3]);
                break;
            case Ds9RegionKind.Segment:
                DrawPolygonRegion(image, region.Values, close: false);
                break;
        }
    }

    private void DrawEllipseRegion(LoadedImage image, double x, double y, double radiusX, double radiusY, double angle)
    {
        var center = MapImagePoint(image, x, y);
        var width = ScaleImageX(image, radiusX) * 2;
        var height = ScaleImageY(image, radiusY) * 2;
        var ellipse = CreateOutline<Ellipse>();
        ellipse.Width = width;
        ellipse.Height = height;
        ellipse.RenderTransform = new RotateTransform { Angle = -angle, CenterX = width / 2, CenterY = height / 2 };
        Canvas.SetLeft(ellipse, center.X - width / 2);
        Canvas.SetTop(ellipse, center.Y - height / 2);
        canvas.Children.Add(ellipse);
    }

    private void DrawBoxRegion(LoadedImage image, double x, double y, double widthValue, double heightValue, double angle)
    {
        var center = MapImagePoint(image, x, y);
        var width = ScaleImageX(image, widthValue);
        var height = ScaleImageY(image, heightValue);
        var rectangle = CreateOutline<Rectangle>();
        rectangle.Width = width;
        rectangle.Height = height;
        rectangle.RenderTransform = new RotateTransform { Angle = -angle, CenterX = width / 2, CenterY = height / 2 };
        Canvas.SetLeft(rectangle, center.X - width / 2);
        Canvas.SetTop(rectangle, center.Y - height / 2);
        canvas.Children.Add(rectangle);
    }

    private void DrawPointRegion(LoadedImage image, double x, double y)
    {
        var point = MapImagePoint(image, x, y);
        var size = 6.0 / Math.Max(zoom, 0.001);
        DrawOverlayLine(point.X - size, point.Y, point.X + size, point.Y);
        DrawOverlayLine(point.X, point.Y - size, point.X, point.Y + size);
    }

    private void DrawLineRegion(LoadedImage image, double x1, double y1, double x2, double y2)
    {
        var p1 = MapImagePoint(image, x1, y1);
        var p2 = MapImagePoint(image, x2, y2);
        DrawOverlayLine(p1.X, p1.Y, p2.X, p2.Y);
    }

    private void DrawPolygonRegion(LoadedImage image, IReadOnlyList<double> values, bool close)
    {
        if (values.Count < 4)
        {
            return;
        }

        var points = new List<(double X, double Y)>();
        for (var i = 0; i + 1 < values.Count; i += 2)
        {
            points.Add(MapImagePoint(image, values[i], values[i + 1]));
        }

        for (var i = 0; i + 1 < points.Count; i++)
        {
            DrawOverlayLine(points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y);
        }

        if (close && points.Count > 2)
        {
            DrawOverlayLine(points[^1].X, points[^1].Y, points[0].X, points[0].Y);
        }
    }

    private void DrawAnnulusRegion(LoadedImage image, Ds9Region region)
    {
        if (region.Values.Count < 4)
        {
            return;
        }

        for (var i = 2; i < region.Values.Count; i++)
        {
            DrawEllipseRegion(image, region.Values[0], region.Values[1], region.Values[i], region.Values[i], 0);
        }
    }

    private void DrawTextRegion(LoadedImage image, Ds9Region region)
    {
        var point = MapImagePoint(image, region.Values[0], region.Values[1]);
        var text = new TextBlock
        {
            Text = region.Label ?? "text",
            Foreground = ActiveRegionBrush,
            FontSize = 12 / Math.Max(zoom, 0.001)
        };
        Canvas.SetLeft(text, point.X);
        Canvas.SetTop(text, point.Y);
        canvas.Children.Add(text);
    }

    private void DrawContourSegment(ContourSegment segment)
    {
        DrawOverlayLine(
            segment.X1,
            segment.Y1,
            segment.X2,
            segment.Y2,
            ContourBrush,
            RegionStrokeThickness / Math.Max(zoom, 0.001));
    }

    private void DrawWcsGridLabels(IReadOnlyList<WcsGridSegment> wcsGridSegments)
    {
        var drawn = new HashSet<string>(StringComparer.Ordinal);
        foreach (var segment in wcsGridSegments)
        {
            if (!drawn.Add(segment.Label) || segment.X1 < 0 || segment.Y1 < 0)
            {
                continue;
            }

            var label = new TextBlock
            {
                Text = segment.Label,
                Foreground = WcsGridBrush,
                FontSize = 10 / Math.Max(zoom, 0.001)
            };
            Canvas.SetLeft(label, segment.X1 + 3 / Math.Max(zoom, 0.001));
            Canvas.SetTop(label, segment.Y1 + 3 / Math.Max(zoom, 0.001));
            canvas.Children.Add(label);
        }
    }

    private void DrawOverlayLine(
        double x1,
        double y1,
        double x2,
        double y2,
        Brush? brush = null,
        double? strokeThickness = null)
    {
        canvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = brush ?? ActiveRegionBrush,
            StrokeThickness = strokeThickness ?? ScaledRegionStrokeThickness
        });
    }

    private T CreateOutline<T>() where T : Shape, new()
    {
        return new T
        {
            Stroke = ActiveRegionBrush,
            StrokeThickness = ScaledRegionStrokeThickness
        };
    }

    private static (double X, double Y) MapImagePoint(LoadedImage image, double imageX, double imageY)
    {
        var sourceWidth = image.SourceWidth > 0 ? image.SourceWidth : image.Width;
        var sourceHeight = image.SourceHeight > 0 ? image.SourceHeight : image.Height;
        return (
            (imageX - 0.5) / sourceWidth * image.Width,
            (sourceHeight - imageY + 0.5) / sourceHeight * image.Height);
    }

    private static double ScaleImageX(LoadedImage image, double value)
    {
        var sourceWidth = image.SourceWidth > 0 ? image.SourceWidth : image.Width;
        return value / sourceWidth * image.Width;
    }

    private static double ScaleImageY(LoadedImage image, double value)
    {
        var sourceHeight = image.SourceHeight > 0 ? image.SourceHeight : image.Height;
        return value / sourceHeight * image.Height;
    }

    private Brush RegionBrush => brushProvider("RegionOverlayBrush");

    private Brush SelectedRegionBrush => brushProvider("SelectedRegionOverlayBrush");

    private Brush ContourBrush => brushProvider("ContourOverlayBrush");

    private Brush WcsGridBrush => brushProvider("WcsGridOverlayBrush");

    private Brush ActiveRegionBrush => activeRegionBrush ?? RegionBrush;

    private double ScaledRegionStrokeThickness => RegionStrokeThickness / Math.Max(zoom, 0.001);
}
