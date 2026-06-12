namespace WinDS9.Engine;

public sealed class WcsGridGenerator
{
    public IReadOnlyList<WcsGridSegment> Generate(LoadedImage image, int lineCount = 8, int samplesPerLine = 96)
    {
        if (image.Wcs is null || !image.Wcs.HasCelestialAxes || image.SourceWidth <= 0 || image.SourceHeight <= 0)
        {
            return [];
        }

        var corners = new[]
        {
            image.Wcs.PixelToWorld(0.5, 0.5),
            image.Wcs.PixelToWorld(image.SourceWidth + 0.5, 0.5),
            image.Wcs.PixelToWorld(0.5, image.SourceHeight + 0.5),
            image.Wcs.PixelToWorld(image.SourceWidth + 0.5, image.SourceHeight + 0.5)
        }.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        if (corners.Length < 2)
        {
            return [];
        }

        var raValues = corners.Select(value => value.World1).ToArray();
        var decValues = corners.Select(value => value.World2).ToArray();
        var raRange = NormalizeWrappedRange(raValues);
        var decMin = decValues.Min();
        var decMax = decValues.Max();

        lineCount = Math.Clamp(lineCount, 2, 24);
        samplesPerLine = Math.Clamp(samplesPerLine, 8, 512);
        var segments = new List<WcsGridSegment>();
        foreach (var ra in NiceTicks(raRange.Min, raRange.Max, lineCount))
        {
            AddWorldPolyline(image, segments, ra, decMin, ra, decMax, samplesPerLine, isLongitude: true);
        }

        foreach (var dec in NiceTicks(decMin, decMax, lineCount))
        {
            AddWorldPolyline(image, segments, raRange.Min, dec, raRange.Max, dec, samplesPerLine, isLongitude: false);
        }

        return segments;
    }

    private static void AddWorldPolyline(
        LoadedImage image,
        List<WcsGridSegment> segments,
        double worldStart1,
        double worldStart2,
        double worldEnd1,
        double worldEnd2,
        int samples,
        bool isLongitude)
    {
        (double X, double Y)? previous = null;
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (double)samples;
            var world1 = NormalizeDegrees(worldStart1 + (worldEnd1 - worldStart1) * t);
            var world2 = worldStart2 + (worldEnd2 - worldStart2) * t;
            var pixel = image.Wcs!.WorldToPixel(world1, world2);
            if (!pixel.HasValue)
            {
                previous = null;
                continue;
            }

            var mapped = MapImagePoint(image, pixel.Value.Pixel1, pixel.Value.Pixel2);
            if (mapped.X < -image.Width || mapped.X > image.Width * 2 ||
                mapped.Y < -image.Height || mapped.Y > image.Height * 2)
            {
                previous = null;
                continue;
            }

            if (previous.HasValue)
            {
                segments.Add(new WcsGridSegment(
                    previous.Value.X,
                    previous.Value.Y,
                    mapped.X,
                    mapped.Y,
                    isLongitude ? $"RA {world1:0.###}" : $"Dec {world2:0.###}",
                    isLongitude));
            }

            previous = mapped;
        }
    }

    private static (double X, double Y) MapImagePoint(LoadedImage image, double imageX, double imageY)
    {
        var sourceWidth = image.SourceWidth > 0 ? image.SourceWidth : image.Width;
        var sourceHeight = image.SourceHeight > 0 ? image.SourceHeight : image.Height;
        return (
            (imageX - 0.5) / sourceWidth * image.Width,
            (sourceHeight - imageY + 0.5) / sourceHeight * image.Height);
    }

    private static IEnumerable<double> NiceTicks(double min, double max, int count)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max) || max <= min)
        {
            yield break;
        }

        var rawStep = (max - min) / Math.Max(1, count - 1);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var normalized = rawStep / magnitude;
        var niceNormalized = normalized switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10
        };
        var step = niceNormalized * magnitude;
        var first = Math.Ceiling(min / step) * step;
        for (var value = first; value <= max + step * 0.25; value += step)
        {
            yield return value;
        }
    }

    private static (double Min, double Max) NormalizeWrappedRange(IReadOnlyList<double> degrees)
    {
        var values = degrees.Select(NormalizeDegrees).Order().ToArray();
        if (values.Length == 0)
        {
            return (0, 0);
        }

        if (values[^1] - values[0] <= 180)
        {
            return (values[0], values[^1]);
        }

        var shifted = values.Select(value => value < 180 ? value + 360 : value).Order().ToArray();
        return (shifted[0], shifted[^1]);
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
