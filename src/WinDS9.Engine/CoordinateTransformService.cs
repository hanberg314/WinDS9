namespace WinDS9.Engine;

public sealed class CoordinateTransformService
{
    private const double DegToRad = Math.PI / 180;
    private const double RadToDeg = 180 / Math.PI;

    public Ds9Region? ToImageRegion(Ds9Region region, WcsMetadata? wcs)
    {
        if (region.IsImageLike)
        {
            return region;
        }

        if (!region.IsWorldLike || wcs is null || region.Values.Count < 2)
        {
            return null;
        }

        return region.Kind switch
        {
            Ds9RegionKind.Circle => ConvertWorldCircle(region, wcs),
            Ds9RegionKind.Annulus => ConvertWorldAnnulus(region, wcs),
            Ds9RegionKind.Box => ConvertWorldBox(region, wcs),
            Ds9RegionKind.Ellipse => ConvertWorldEllipse(region, wcs),
            Ds9RegionKind.Point or Ds9RegionKind.Text => ConvertWorldPoint(region, wcs),
            Ds9RegionKind.Line or Ds9RegionKind.Ruler or Ds9RegionKind.Vector or Ds9RegionKind.Projection => ConvertWorldLineLike(region, wcs),
            Ds9RegionKind.Polygon or Ds9RegionKind.Segment => ConvertWorldPairs(region, wcs),
            _ => null
        };
    }

    public (double ImageX, double ImageY)? WorldToImage(WcsMetadata? wcs, double world1, double world2, string coordinateSystem = "fk5")
    {
        if (wcs is null)
        {
            return null;
        }

        var (ra, dec) = ToEquatorial(world1, world2, coordinateSystem);
        var pixel = wcs.WorldToPixel(ra, dec);
        return pixel.HasValue ? (pixel.Value.Pixel1, pixel.Value.Pixel2) : null;
    }

    private Ds9Region? ConvertWorldPoint(Ds9Region region, WcsMetadata wcs)
    {
        var point = WorldToImage(wcs, region.Values[0], region.Values[1], region.CoordinateSystem);
        return point.HasValue
            ? region with { Values = [point.Value.ImageX, point.Value.ImageY], CoordinateSystem = "image" }
            : null;
    }

    private Ds9Region? ConvertWorldCircle(Ds9Region region, WcsMetadata wcs)
    {
        var center = WorldToImage(wcs, region.Values[0], region.Values[1], region.CoordinateSystem);
        if (!center.HasValue)
        {
            return null;
        }

        var radius = region.Values.Count > 2 ? Math.Abs(region.Values[2]) : 0;
        var edge = WorldToImage(wcs, region.Values[0] + radius, region.Values[1], region.CoordinateSystem);
        var pixelRadius = edge.HasValue
            ? Distance(center.Value, edge.Value)
            : radius;

        return region with
        {
            Values = [center.Value.ImageX, center.Value.ImageY, Math.Max(pixelRadius, 0.5)],
            CoordinateSystem = "image"
        };
    }

    private Ds9Region? ConvertWorldAnnulus(Ds9Region region, WcsMetadata wcs)
    {
        var center = WorldToImage(wcs, region.Values[0], region.Values[1], region.CoordinateSystem);
        if (!center.HasValue)
        {
            return null;
        }

        var converted = new List<double> { center.Value.ImageX, center.Value.ImageY };
        foreach (var radius in region.Values.Skip(2))
        {
            var edge = WorldToImage(wcs, region.Values[0] + Math.Abs(radius), region.Values[1], region.CoordinateSystem);
            converted.Add(edge.HasValue ? Distance(center.Value, edge.Value) : Math.Abs(radius));
        }

        return region with { Values = converted, CoordinateSystem = "image" };
    }

    private Ds9Region? ConvertWorldBox(Ds9Region region, WcsMetadata wcs)
    {
        var center = WorldToImage(wcs, region.Values[0], region.Values[1], region.CoordinateSystem);
        if (!center.HasValue || region.Values.Count < 4)
        {
            return null;
        }

        var widthEdge = WorldToImage(wcs, region.Values[0] + Math.Abs(region.Values[2]), region.Values[1], region.CoordinateSystem);
        var heightEdge = WorldToImage(wcs, region.Values[0], region.Values[1] + Math.Abs(region.Values[3]), region.CoordinateSystem);
        var width = widthEdge.HasValue ? Distance(center.Value, widthEdge.Value) : Math.Abs(region.Values[2]);
        var height = heightEdge.HasValue ? Distance(center.Value, heightEdge.Value) : Math.Abs(region.Values[3]);
        var values = new List<double> { center.Value.ImageX, center.Value.ImageY, width, height };
        if (region.Values.Count > 4)
        {
            values.Add(region.Values[4]);
        }

        return region with { Values = values, CoordinateSystem = "image" };
    }

    private Ds9Region? ConvertWorldEllipse(Ds9Region region, WcsMetadata wcs)
    {
        var converted = ConvertWorldBox(region, wcs);
        return converted is null ? null : converted with { Kind = Ds9RegionKind.Ellipse };
    }

    private Ds9Region? ConvertWorldLineLike(Ds9Region region, WcsMetadata wcs)
    {
        if (region.Values.Count < 4)
        {
            return null;
        }

        var p1 = WorldToImage(wcs, region.Values[0], region.Values[1], region.CoordinateSystem);
        var p2 = WorldToImage(wcs, region.Values[2], region.Values[3], region.CoordinateSystem);
        if (!p1.HasValue || !p2.HasValue)
        {
            return null;
        }

        var values = new List<double> { p1.Value.ImageX, p1.Value.ImageY, p2.Value.ImageX, p2.Value.ImageY };
        values.AddRange(region.Values.Skip(4));
        return region with { Values = values, CoordinateSystem = "image" };
    }

    private Ds9Region? ConvertWorldPairs(Ds9Region region, WcsMetadata wcs)
    {
        var values = new List<double>(region.Values.Count);
        for (var i = 0; i + 1 < region.Values.Count; i += 2)
        {
            var point = WorldToImage(wcs, region.Values[i], region.Values[i + 1], region.CoordinateSystem);
            if (!point.HasValue)
            {
                return null;
            }

            values.Add(point.Value.ImageX);
            values.Add(point.Value.ImageY);
        }

        return region with { Values = values, CoordinateSystem = "image" };
    }

    private static (double Ra, double Dec) ToEquatorial(double first, double second, string coordinateSystem)
    {
        return string.Equals(coordinateSystem, "galactic", StringComparison.OrdinalIgnoreCase)
            ? GalacticToEquatorial(first, second)
            : (NormalizeDegrees(first), second);
    }

    private static (double Ra, double Dec) GalacticToEquatorial(double longitude, double latitude)
    {
        var l = longitude * DegToRad;
        var b = latitude * DegToRad;
        var galactic = new[]
        {
            Math.Cos(b) * Math.Cos(l),
            Math.Cos(b) * Math.Sin(l),
            Math.Sin(b)
        };

        var equatorial = new[]
        {
            -0.0548755604 * galactic[0] + 0.4941094279 * galactic[1] - 0.8676661490 * galactic[2],
            -0.8734370902 * galactic[0] - 0.4448296300 * galactic[1] - 0.1980763734 * galactic[2],
            -0.4838350155 * galactic[0] + 0.7469822445 * galactic[1] + 0.4559837762 * galactic[2]
        };

        var ra = Math.Atan2(equatorial[1], equatorial[0]) * RadToDeg;
        var dec = Math.Asin(Math.Clamp(equatorial[2], -1, 1)) * RadToDeg;
        return (NormalizeDegrees(ra), dec);
    }

    private static double Distance((double ImageX, double ImageY) a, (double ImageX, double ImageY) b)
    {
        var dx = a.ImageX - b.ImageX;
        var dy = a.ImageY - b.ImageY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
