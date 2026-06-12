using System.Globalization;
using System.Text;

namespace WinDS9.Engine;

public sealed class Ds9RegionSerializer
{
    public string Serialize(IEnumerable<Ds9Region> regions)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Region file format: DS9");
        builder.AppendLine("global color=green width=1");

        var currentCoordinateSystem = string.Empty;
        foreach (var region in regions)
        {
            var coordinateSystem = string.IsNullOrWhiteSpace(region.CoordinateSystem)
                ? "image"
                : region.CoordinateSystem.ToLowerInvariant();
            if (!string.Equals(currentCoordinateSystem, coordinateSystem, StringComparison.OrdinalIgnoreCase))
            {
                builder.AppendLine(coordinateSystem);
                currentCoordinateSystem = coordinateSystem;
            }

            builder.Append(KindName(region.Kind));
            builder.Append('(');
            builder.Append(string.Join(",", region.Values.Select(FormatValue)));
            builder.Append(')');
            if (!string.IsNullOrWhiteSpace(region.Label))
            {
                builder.Append(" # text={");
                builder.Append(region.Label.Replace("}", string.Empty, StringComparison.Ordinal));
                builder.Append('}');
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string KindName(Ds9RegionKind kind)
    {
        return kind switch
        {
            Ds9RegionKind.Circle => "circle",
            Ds9RegionKind.Box => "box",
            Ds9RegionKind.Ellipse => "ellipse",
            Ds9RegionKind.Point => "point",
            Ds9RegionKind.Line => "line",
            Ds9RegionKind.Polygon => "polygon",
            Ds9RegionKind.Annulus => "annulus",
            Ds9RegionKind.Text => "text",
            Ds9RegionKind.Ruler => "ruler",
            Ds9RegionKind.Vector => "vector",
            Ds9RegionKind.Segment => "segment",
            Ds9RegionKind.Projection => "projection",
            _ => "point"
        };
    }

    private static string FormatValue(double value)
    {
        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }
}
